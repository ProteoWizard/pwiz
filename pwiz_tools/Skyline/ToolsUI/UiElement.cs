/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using SkylineTool;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace pwiz.Skyline.ToolsUI
{
    // ---- Capability interfaces ------------------------------------------------------------------
    // The "kind" a UiAction targets: an action applies to an element when the element implements the
    // action's interface (UiAction<T>.AppliesTo is `element is T`), and the action drives the element
    // through that interface's method. A control's element class declares the capabilities it has by
    // implementing these, instead of a per-element switch over an action enum.

    // EVERY method on these interfaces is a raw gesture: it runs ON the element's UI thread and does no threading,
    // gating or waiting of its own -- UiAction.Invoke does all three around it (see UiAction). That is why they are
    // named "...Now" and return void: an element says only WHAT its gesture is, never how it is dispatched, so a
    // gesture and its dispatch can no longer drift apart.

    /// <summary>An element a click acts on (a button, a menu item, a custom clickable tile).</summary>
    public interface IClickableElement
    {
        void ClickNow();
    }

    /// <summary>An element whose value can be SET -- a text box, a combo box, a check/radio state, a grid cell, a
    /// native dialog's file-name field. Only these support set_value. (Reading a value is not a capability: every
    /// element answers <see cref="UiElement.Value"/>, with null for one that has none.)</summary>
    public interface IValueElement
    {
        void SetValueNow(object value);
    }

    /// <summary>An element whose items are checked/unchecked by their visible text (a CheckedListBox, a
    /// TreeView, a ListView, the pick-list pop-up).</summary>
    public interface ICheckItemsElement { void SetItemCheckedNow(string item, bool isChecked); }

    /// <summary>An element whose items are selected by their visible text (select_item / unselect_item) or
    /// by index (set_selected_index) -- a list box, a tree, a list view. Anything with a selected index also
    /// has the concept of selecting items, so the two are one capability; setting the selected index clears
    /// any other selection and selects only that one item.</summary>
    public interface ISelectItemsElement
    {
        void SetItemSelectedNow(string item, bool isSelected);
        void SetSelectedIndexNow(int index);
    }

    /// <summary>A tree whose nodes can be expanded/collapsed by a path (an array of child names/indexes).</summary>
    public interface IExpandCollapseElement { void ExpandNow(object path); void CollapseNow(object path); }

    /// <summary>A tab control whose tab can be selected by the tab's visible text.</summary>
    public interface ISelectTabElement { void SelectTabNow(string tabText); }

    /// <summary>An element the connector can drive the clipboard gestures on: paste text into (without going
    /// through the clipboard, which an MCP client may not be able to touch -- for the tutorial steps that say
    /// to paste into Skyline) and select all (e.g. before a paste, to replace the contents). Only the elements
    /// that actually do these implement it: a text box, a grid, the Targets tree, and the main Skyline window.</summary>
    public interface IClipboardElement
    {
        void PasteNow(string text);
        void SelectAllNow();
    }

    /// <summary>A tree whose selected node can be renamed in place (the Targets tree -- e.g. renaming a
    /// peptide group), as a user does by editing the node label and pressing Enter.</summary>
    public interface IRenameNodeElement { void RenameNodeNow(string value); }

    /// <summary>An element that offers a fixed list of choices whose visible text can be read (get_options) --
    /// a combo box, a list box, or a checked list box. Unlike get_value (which reports the current
    /// selection / checked items), this returns EVERY choice regardless of state, so a caller can see the
    /// options that are available to select or check.</summary>
    public interface IOptionsElement { IEnumerable<string> GetOptions(); }


    /// <summary>
    /// A connector-facing view of one UI element. A subclass declares what it supports by implementing capability
    /// interfaces (<see cref="IClickableElement"/>, <see cref="ICheckItemsElement"/>, ...), and each
    /// <see cref="UiAction"/> targets one of those -- so the verbs act polymorphically instead of switching on
    /// WinForms types. An element also knows its <see cref="Label"/> (the visible text it is matched by) and its
    /// children (<see cref="EnumerateChildren"/>), so matching and enumeration are one recursive walk.
    ///
    /// <para>Most elements wrap a WinForms <see cref="Control"/> (<see cref="ControlElement"/>), but the base is
    /// control-agnostic, so a non-WinForms surface presents itself the same way: a <see cref="NativeDialog"/> and
    /// its <see cref="NativeControl"/> children are UiElements backed by Win32 window handles.</para>
    /// </summary>
    public abstract class UiElement
    {
        protected UiElement(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
        }

        /// <summary>The cancellation of the request this element was built to serve -- carried by the element so
        /// every marshal and wait it performs can be abandoned when the client that asked for it disconnects. It is
        /// per-request (not per-server) state, so it must travel with the element rather than sit in a static: the
        /// tool server may be serving several clients at once, on several threads, each with its own cancellation.
        /// <see cref="System.Threading.CancellationToken.None"/> for an element built outside a request (in-process,
        /// where there is no client to disconnect).</summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>This element's own addressable path (from a form root), set when the element is resolved
        /// from a path or returned as a form. <see cref="GetControlsNow"/> parents the child paths it returns
        /// onto this, so a caller can use them directly in a later call without re-parenting. Null until set
        /// (then children are reported parentless, as before).</summary>
        public UiElementPath Path { get; internal set; }

        /// <summary>The underlying control's Name -- informational (for discovery via GetControls); the
        /// connector does NOT match on it. Empty for elements with no backing control name.</summary>
        public abstract string Name { get; }

        /// <summary>The element's type, used for the "match by kind" fallback ("ListView"/"TreeView").</summary>
        public abstract Type ElementType { get; }

        /// <summary>The visible text that identifies this element to a user: its own caption (a button,
        /// a checkbox, a tab) or, for a caption-less field, the Label immediately before it in tab order.
        /// Null when nothing labels it (then it is reached by its type or as the form's only one).</summary>
        public virtual string Label => null;

        public abstract bool IsEnabled { get; }

        /// <summary>The managed form this element belongs to, whose modal-block / enabled state gates acting on
        /// the element (see <see cref="VerifyEnabled"/>). Null for an element with no managed form of its
        /// own (a native dialog) -- then only the element's own enabled state is checked.</summary>
        internal virtual Form OwningForm => null;

        /// <summary>Throws if the connector cannot currently act on this element -- the same gates a user faces:
        /// no modal dialog is blocking its <see cref="OwningForm"/>, and the element itself is enabled. The base
        /// checks the form (if any) and the element's own <see cref="IsEnabled"/>; a control narrows the second
        /// check to the control. Called by <see cref="UiAction.Invoke"/> on the element's UI thread (the modal
        /// check reads a window handle).</summary>
        public virtual void VerifyEnabled()
        {
            VerifyFormInteractable(OwningForm, CancellationToken);
            if (!IsEnabled)
                throw new InvalidOperationException(LlmInstruction.Format(
                    @"'{0}' is disabled.", Label ?? Name));
        }

        // The form gate: throws if a modal dialog is blocking the form's window, or the form is disabled. No-op
        // when there is no form (a native dialog gates itself through IsEnabled). The modal check must use the
        // Win32 enabled state of the TOP-LEVEL window (e.g. the main window for a docked form): showing a modal
        // dialog calls EnableWindow(false) on the other windows WITHOUT flipping their managed Control.Enabled,
        // so a managed-only check would miss it.
        protected static void VerifyFormInteractable(Form form, CancellationToken cancellationToken)
        {
            if (form == null)
                return;
            var topLevel = TopLevelFormOf(form);
            if (!User32.IsWindowEnabled(topLevel.Handle))
            {
                // A modal dialog has disabled the form. If it is an alert (CommonAlertDlg), include its text so
                // the caller sees what it says without having to capture a screenshot of it.
                var alertMessage = DialogWatcher.BlockingAlertMessage(cancellationToken);
                throw new InvalidOperationException(LlmInstruction.Format(
                    @"Cannot interact with form '{0}': it is blocked by an open dialog{1}. Handle the open dialog first (see skyline_get_open_forms).",
                    JsonUiService.GetFormId(form),
                    alertMessage != null ? @" which says: " + alertMessage : string.Empty));
            }
            if (!form.Enabled)
                throw new InvalidOperationException(LlmInstruction.Format(
                    @"Form '{0}' is disabled.", JsonUiService.GetFormId(form)));
        }

        // The top-level form hosting a control (e.g. the main window for a docked form). FindForm on a form
        // returns itself, so each step goes through Parent first to climb past the current form.
        private static Form TopLevelFormOf(Control control)
        {
            var form = control as Form ?? control.FindForm();
            while (form?.Parent != null)
            {
                var parentForm = form.Parent.FindForm();
                if (parentForm == null)
                    break;
                form = parentForm;
            }
            return form;
        }

        /// <summary>The element's current value in its natural form (e.g. a grid cell's underlying object), or null
        /// when the control has no value. <see cref="ConvertValue"/> normalizes it to null / bool / double / string
        /// at the point it is exposed to the connector -- as get_value, and echoed in each
        /// <see cref="ControlInfo"/> -- so this need not be one of those types.
        ///
        /// <para>ON the control's thread, like every "...Now": a read reaches this only through
        /// <see cref="UiActions.GetValue"/> (a <see cref="UiFunction{TResult}"/>, which has already marshaled) or
        /// through <see cref="GetControlsNow"/> (itself already there). A METHOD, not a property, so that merely
        /// looking at an element in the debugger does not touch a control from the debugger's thread.</para></summary>
        public virtual object GetValueNow() => null;


        /// <summary>Coerces an arbitrary value to the only types the connector exchanges: null stays null,
        /// a bool stays a bool, any numeric type becomes a double, and everything else becomes its string
        /// form. Applied where a value crosses to/from the connector -- a get_value / ControlInfo read and a
        /// set_value argument -- so the connector only ever sees null, a bool, a double, or a string.</summary>
        public static object ConvertValue(object value)
        {
            switch (value)
            {
                case null: return null;
                case bool b: return b;
                case string s: return s;
            }
            if (value is IConvertible convertible)
            {
                switch (convertible.GetTypeCode())
                {
                    case TypeCode.Byte:
                    case TypeCode.SByte:
                    case TypeCode.Int16:
                    case TypeCode.UInt16:
                    case TypeCode.Int32:
                    case TypeCode.UInt32:
                    case TypeCode.Int64:
                    case TypeCode.UInt64:
                    case TypeCode.Single:
                    case TypeCode.Double:
                    case TypeCode.Decimal:
                        return convertible.ToDouble(CultureInfo.InvariantCulture);
                }
            }
            return value.ToString();
        }

        /// <summary>Finds the one element this verb's <paramref name="text"/> names that supports
        /// <paramref name="action"/>: the single rule the by-text verbs (ClickFormButton, SetFormValue,
        /// GetFormValue, and the grid verbs) share for deciding which control a piece of text refers to.
        /// The search is the same recursive walk the form enumeration uses -- so a control nested in a
        /// UserControl (a grid inside a DataboundGridControl) is reached -- filtered to the elements that
        /// support the action. An exact (case- and symbol-sensitive) Label match is preferred over a loose
        /// one, and within a tier an interactable (visible + enabled) element wins, so a hidden duplicate --
        /// the same field on an unselected, flattened tab -- never shadows the one the user sees. An empty
        /// <paramref name="text"/> means "the single element that supports the action". Throws an LLM-facing
        /// error when nothing (or, for an empty text, more than one thing) matches.</summary>
        public UiElement FindElement(string text, UiAction action)
        {
            if (!string.IsNullOrEmpty(text))
            {
                var element = FindElementOrNull(text, action);
                if (element == null)
                {
                    throw new ArgumentException(LlmInstruction.Format(
                        @"No control matching '{0}' supports the action '{1}'. Use skyline_get_controls to list the controls.",
                        text, action.SnakeCaseName));
                }

                return element;
            }
            // Empty text means "the single element that supports the action".
            var candidates = SelfAndDescendants().Where(action.AppliesTo).ToList();
            if (candidates.Count == 0)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Nothing here supports the action '{0}'.", action.SnakeCaseName));
            if (candidates.Count == 1)
                return candidates[0];
            // Several support it: take the single enabled one, so a disabled sibling never makes "the form's
            // single grid/control" ambiguous (a hidden sibling was already dropped when the elements were
            // built -- we only build an element for a control that was visible). If more than one is still
            // enabled, the caller must name one.
            var enabled = candidates.Where(c => c.IsEnabled).ToList();
            if (enabled.Count == 1)
                return enabled[0];
            throw new ArgumentException(LlmInstruction.Format(
                @"More than one control supports the action '{0}'; pass a label or name to choose one (see skyline_get_controls).",
                action.SnakeCaseName));
        }

        /// <summary>Like <see cref="FindElement"/> for a non-empty text, but returns null instead of throwing
        /// when nothing matches -- for a caller (a menu walk over several toolstrips) that tries one container
        /// and falls through to the next. An exact (strict) match is preferred over a loose one, and within a
        /// tier an interactable element over a hidden one.</summary>
        public UiElement FindElementOrNull(string text, UiAction action)
        {
            var candidates = SelfAndDescendants().Where(action.AppliesTo).ToList();
            return BestMatch(candidates, text, true) ?? BestMatch(candidates, text, false);
        }

        // The best of the candidates whose text matches at the given strictness: prefer an enabled one so a
        // disabled duplicate never shadows the control the user can act on. Returns null when none matches at
        // this strictness (the caller then falls back to a looser match).
        private static UiElement BestMatch(IEnumerable<UiElement> candidates, string text, bool strict)
        {
            UiElement best = null;
            foreach (var element in candidates)
            {
                if (!element.MatchesText(text, strict))
                    continue;
                if (best == null || (!best.IsEnabled && element.IsEnabled))
                    best = element;
            }
            return best;
        }

        /// <summary>Whether this element's visible text identifies it to the requested <paramref name="text"/>:
        /// the centralized text match used both to resolve a path segment (<see cref="GetChild"/>) and to find
        /// a control by a verb's text (<see cref="FindElement"/>). Strict requires the Label to match exactly
        /// (case- and symbol-sensitive); loose ignores case and every non-alphanumeric symbol -- so "Name"
        /// matches a "Name:" label and "Next" a "Next &gt;" button -- but only when the requested text carries
        /// no symbol of its own (a caller who typed a ':' or '&gt;' is taken to mean it exactly). An element
        /// with no Label (a grid, a spacer) matches nothing here; a grid overrides this to match its control
        /// name, the one place the connector matches on a name rather than on visible text.</summary>
        public virtual bool MatchesText(string text, bool strict) => TextMatches(Label, text, strict);

        /// <summary>The connector's text match between a control/item/menu/tab's visible text and a requested
        /// key, at one strictness. Strict requires an exact match (case- and symbol-sensitive); loose ignores
        /// case and every non-alphanumeric symbol -- so "Name" matches "Name:" and "Next" a "Next &gt;"
        /// button -- but only when the key carries no symbol of its own (a caller who typed a ':' or '&gt;' is
        /// taken to mean it exactly). The element matchers (<see cref="MatchesText"/>) and the list/tree/tab/
        /// toolstrip matchers all use this, so they rank candidates the same way (a strict match preferred
        /// over a loose one) without a separate quality enum.</summary>
        public static bool TextMatches(string candidate, string key, bool strict)
        {
            if (candidate == null || key == null)
                return false;
            if (strict)
                // Match the raw text or its normalized form (the mnemonic '&' and trailing punctuation/space
                // removed -- see NormalizeLabel), so the label GetControls/get_children report (which is the
                // normalized one) is matchable even when it carries a symbol of its own, which disables the
                // symbol-insensitive loose match below.
                return string.Equals(candidate, key, StringComparison.Ordinal)
                    || string.Equals(NormalizeLabel(candidate), key, StringComparison.Ordinal);
            if (HasSymbol(key))
                return false;
            return string.Equals(StripToAlphanumeric(candidate), StripToAlphanumeric(key),
                StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>Whether the candidate matches the key at all -- an exact (strict) or symbol-insensitive
        /// (loose) match -- for callers that just need a yes/no rather than a strict-then-loose ranking.</summary>
        public static bool TextMatches(string candidate, string key) =>
            TextMatches(candidate, key, true) || TextMatches(candidate, key, false);

        /// <summary>Whether this element is of the named type -- its <see cref="ElementType"/> or any base
        /// type up to (but not including) Control, so "ListView" matches a ColumnListView and "TreeView" an
        /// AvailableFieldsTree. Used to resolve a path segment's Type and to list the children of a Type
        /// (<see cref="GetChildren(string)"/>).</summary>
        public virtual bool MatchesType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return false;
            for (var type = ElementType; type != null && type != typeof(Control); type = type.BaseType)
                if (string.Equals(type.Name, typeName, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>This element's children of the named type (see <see cref="MatchesType"/>), in child
        /// order -- the candidates a path segment's Type (optionally pinned by Index) selects from.</summary>
        public virtual IEnumerable<UiElement> GetChildren(string typeName)
        {
            return EnumerateChildren().Where(child => child.MatchesType(typeName));
        }

        /// <summary>The elements nested inside this one, for the recursive form walk. A method, not a property,
        /// because enumerating may have side effects: a menu item opens (and recloses) its dropdown so items
        /// built on demand are present before they are listed (see <see cref="ToolStripItemElement"/>).</summary>
        public virtual IEnumerable<UiElement> EnumerateChildren() => Enumerable.Empty<UiElement>();

        /// <summary>This element and every element under it, depth-first.</summary>
        public IEnumerable<UiElement> SelfAndDescendants()
        {
            return EnumerateChildren().SelectMany(child => child.SelfAndDescendants()).Prepend(this);
        }

        /// <summary>The gate-and-gesture a mutating or clicking method owns: it posts the gate + gesture onto the
        /// element's form thread and waits it out (<see cref="DialogWatcher.PerformAction(IntPtr,Action,CancellationToken)"/>),
        /// returning whether it completed or left a dialog open -- so
        /// every mutation returns only once it has taken effect, exactly like the named verbs. A gesture that opens a
        /// modal blocks on the form thread, keeping its action counted until the modal closes, so the wait surfaces
        /// it; a blocked/disabled control throws out of the posted delegate and the wait re-throws it to the caller
        /// (fail-fast). Must be called off the UI thread. A read does not use this: it returns a value, so it goes
        /// through <see cref="CallFunction{TResult}"/> instead.</summary>
        internal ActionResult PerformAction(Action gesture)
        {
            return DialogWatcher.PerformAction(FormHwnd, gesture, CancellationToken);
        }

        /// <summary>The read counterpart of <see cref="PerformAction"/>, marshaled the same way: ONE trip, straight
        /// onto this element's own form thread (<see cref="FormHwnd"/>), inside the dialog-watch so it gives up
        /// rather than hanging behind a modal blocking that form.
        ///
        /// <para>One trip, not two: dialog-watching onto the MAIN UI thread and hopping to the element's form from
        /// there makes the second hop a BLOCKING wait ON the main thread, which then stops pumping -- so the watch's
        /// own Send to it cannot run either, and the read hangs.</para>
        ///
        /// <para>Must be called off the UI thread. Ungated: a disabled control can still be inspected, as a user can
        /// read a greyed-out field.</para></summary>
        internal TResult CallFunction<TResult>(Func<TResult> read)
        {
            return DialogWatcher.CallFunction(FormHwnd, read, CancellationToken);
        }

        /// <summary>The handle of the top-level window this element belongs to -- whose UI thread its gestures run
        /// on. Captured safely when the element is built, never read live off-thread from Control.Handle (which would
        /// trip the cross-thread check).
        ///
        /// <para>ABSTRACT on purpose. As a virtual returning IntPtr.Zero it answered WRONGLY, and silently, for any
        /// element that forgot to override it: the gesture was then marshaled through the MAIN window instead of the
        /// element's own form -- which is right only by luck, and only for a form that IS the main window.
        /// ContextMenuElement had in fact never overridden it. Abstract turns that into a compile error, so a new
        /// kind of element must say which window it lives on rather than defaulting to a wrong answer.</para></summary>
        internal abstract IntPtr FormHwnd { get; }

        /// <summary>The actions this element supports, for discovery via GetActions / GetControls: every
        /// action that <see cref="UiAction.AppliesTo"/> this element (it is the kind the action targets).
        /// The capability is declared by the interfaces the element implements, not by an override here.</summary>
        public IEnumerable<UiAction> SupportedActions =>
            UiActions.AllActions.Where(action => action.AppliesTo(this));

        /// <summary>This element's children as <see cref="ControlInfo"/>, each with a Path whose Parent is
        /// this element's own <see cref="Path"/>, so a caller can pass it straight back in a later call without
        /// re-parenting (when this element's Path is not set, the child Path is parentless as before). The form
        /// walk is one level at a time -- GetControls and the get_children action both return this; descend by
        /// calling GetChildren on a child. Each child's Index is its position among the siblings of its same
        /// Type, so adding a control of another Type never shifts it.</summary>
        public virtual ControlInfo[] GetControlsNow()
        {
            var indexByType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var result = new List<ControlInfo>();
            foreach (var child in EnumerateChildren())
            {
                indexByType.TryGetValue(child.ElementType.Name, out var typeIndex);
                indexByType[child.ElementType.Name] = typeIndex + 1;
                result.Add(new ControlInfo
                {
                    Path = child.PathSegment(typeIndex).ChangeParent(Path),
                    Name = NullIfEmpty(child.Name),
                    Enabled = child.IsEnabled,
                    Value = ConvertValue(child.GetValueNow()),
                });
            }
            return result.ToArray();
        }

        /// <summary>The single child the <paramref name="path"/>'s leaf segment names, with its Parent
        /// ignored (the caller has resolved the chain down to this element). A segment with no selector is
        /// this element itself; Type "ContextMenu" is this control's right-click menu; an Index (which
        /// requires a Type) pins the index-th child of that exact Type; otherwise the child is matched by
        /// Text and/or Type.</summary>
        public virtual UiElement GetChild(UiElementPath path)
        {
            // A segment with no selector at all is this element itself (get_children on a form lists its
            // controls; on a control walks into it).
            if (string.IsNullOrEmpty(path.Type) && path.Text == null && !path.Index.HasValue)
                return this;

            List<UiElement> candidates;
            if (!string.IsNullOrEmpty(path.Type))
            {
                candidates = GetChildren(path.Type).ToList();
            }
            else
            {
                // An Index is the position among the children of an exact Type, so it is meaningless
                // without one; Text (alone) searches all the children.
                if (path.Index.HasValue)
                    throw new ArgumentException(new LlmInstruction(
                        @"A path Index requires a Type: it is the index among the children of that exact Type."));
                candidates = EnumerateChildren().ToList();
            }

            // An Index pins the index-th child of that Type; Text, if also set, must match it.
            if (path.Index.HasValue)
            {
                if (path.Index < 0 || path.Index >= candidates.Count)
                    throw new ArgumentException(LlmInstruction.Format(
                        @"No {0} at index {1}; the parent has {2} of that type.", path.Type, path.Index.Value,
                        candidates.Count));
                var indexed = candidates[path.Index.Value];
                if (path.Text != null && !indexed.MatchesText(path.Text, false))
                    throw new ArgumentException(LlmInstruction.Format(
                        @"The {0} at index {1} does not match the Text '{2}' in the path.", path.Type, path.Index.Value,
                        path.Text));
                return indexed;
            }

            // Text (if set) selects by visible text -- an exact match preferred over a loose one. Otherwise
            // (Type only) the children of that Type. Either way, prefer an interactable (visible + enabled)
            // child over a hidden duplicate -- e.g. the visible bound grid over the collapsed pivot grid.
            UiElement match;
            if (path.Text != null)
                match = PreferInteractable(candidates.Where(child => child.MatchesText(path.Text, true)))
                        ?? PreferInteractable(candidates.Where(child => child.MatchesText(path.Text, false)));
            else
                match = PreferInteractable(candidates);
            if (match == null)
            {
                throw new ArgumentException(new LlmInstruction(
                    @"No control found matching the path. Use skyline_get_controls to list the controls."));
            }
            return match;
        }

        public virtual UiElement GetDescendant(UiElementPath path)
        {
            var segments = new List<UiElementPath>();
            for (var parent = path; parent != null; parent = parent.Parent)
            {
                segments.Add(parent);
            }

            segments.Reverse();

            var descendant = this;
            foreach (var segment in segments)
            {
                descendant = descendant.GetChild(segment);
            }

            return descendant;
        }

        // The first enabled element, or -- when none is enabled -- the first one (so a legitimately disabled
        // target still resolves). Null only for an empty sequence. Lets a Type/Text match prefer the control
        // the user can act on over a disabled duplicate.
        private static UiElement PreferInteractable(IEnumerable<UiElement> elements)
        {
            UiElement first = null;
            foreach (var element in elements)
            {
                if (element.IsEnabled)
                    return element;
                if (first == null)
                    first = element;
            }
            return first;
        }

        protected static string StripToAlphanumeric(string input)
        {
            return new string((input ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
        }

        // True if text has a symbol of its own -- a character that is neither a letter, a digit, nor
        // whitespace (':', '>', '&', '.') -- which a loose text match honors by declining to strip it away.
        protected static bool HasSymbol(string text) =>
            !string.IsNullOrEmpty(text) && text.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c));

        /// <summary>The parentless path segment that addresses this element as the <paramref name="typeIndex"/>-th
        /// child of its parent among the siblings of this element's Type (its Text, that Index, and its Type).
        /// <see cref="GetControlsNow"/> parents it onto the listing element before returning it.</summary>
        internal UiElementPath PathSegment(int typeIndex) =>
            new UiElementPath(null, NullIfEmpty(NormalizeLabel(Label)), typeIndex, ElementType.Name);
        internal static string NullIfEmpty(string text) => string.IsNullOrEmpty(text) ? null : text;
        // The label as a caller would type it: the mnemonic '&' removed and any trailing ellipsis, period,
        // colon (including the full-width Japanese colon '：'), or whitespace trimmed -- so a "Name:" label is
        // reported (and addressable) as "Name", and a "&Peptide Search..." menu caption as "Peptide Search".
        // Matching tolerates either form anyway. If trimming removes everything -- the whole caption is
        // punctuation, e.g. a "..." or ":" button -- keep the original so the control is still reported with a
        // label and stays addressable, rather than vanishing into an empty (null) name.
        public static string NormalizeLabel(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            var normalized = text.Replace(@"&", string.Empty).Trim().TrimEnd('.', '…', '：', ':', ' ').Trim();
            return normalized.Length == 0 ? text : normalized;
        }

        /// <summary>The menu a '&gt;'-separated menu path is matched against FOR THIS ELEMENT -- what the
        /// click-toolstrip-item verb walks. It is what makes one verb reach every kind of menu:
        ///
        /// <list type="bullet">
        /// <item>a <see cref="ToolStripElement"/> (a menu bar, a toolbar) IS the menu -- it returns itself;</item>
        /// <item>a form (<see cref="ContainerElement"/>) offers its menu BAR, else its first toolbar, else its own
        /// right-click menu;</item>
        /// <item>any other control (<see cref="ControlElement"/>) offers its RIGHT-CLICK menu -- which is the only
        /// menu a grid, a tree or a graph has.</item>
        /// </list>
        ///
        /// <para>Null for an element with no menu at all (a native dialog). Must be read on the UI thread: resolving
        /// a context menu BUILDS it.</para></summary>
        public virtual ToolStripElement MainToolStrip => null;
    }

    // ---- Form-bound elements --------------------------------------------------------------------

    /// <summary>A managed UI element that belongs to a form: a control (<see cref="ControlElement"/>) or a
    /// menu/toolbar item (<see cref="ToolStripItemElement"/>). Unlike the form-less base <see cref="UiElement"/>
    /// (which marshals through the main window) or a native dialog element (<see cref="NativeDialog"/>, which
    /// runs inline), it knows its <see cref="FormElement"/>, so it both marshals and gates through that form: a
    /// form on its own thread (e.g. a BackgroundThreadLongWaitDlg) is driven through its own message loop, not
    /// the main window's, and a modal blocking the form stops the element being acted on.</summary>
    public abstract class UiComponent : UiElement
    {
        protected UiComponent(CancellationToken cancellationToken) : base(cancellationToken) { }

        /// <summary>The form this element belongs to -- the root of the element tree it was built in. Set once
        /// when the element is created: by <see cref="StandaloneForm.ElementFor"/> for a control, or in the
        /// constructor of a <see cref="ToolStripItemElement"/>.</summary>
        public StandaloneForm FormElement { get; internal set; }

        // The element's form gates acting on it (a modal blocking the form); a control narrows this to its own
        // hosting form (which also catches a disabled ancestor).
        internal override Form OwningForm => FormElement?.Form;

        // The stored form used only to decide whether PerformGesture is already on the form's thread (see base).
        internal override IntPtr FormHwnd => FormElement?.Hwnd ?? IntPtr.Zero;
    }

    // ---- Control-backed elements ----------------------------------------------------------------

    /// <summary>Base for an element backed by a WinForms <see cref="Control"/>. Every control is clickable
    /// (see <see cref="Click"/>); a subclass adds value/list/grid capabilities by implementing the matching
    /// capability interface.</summary>
    public abstract class ControlElement : UiComponent, IClickableElement
    {
        protected ControlElement(Control control, CancellationToken cancellationToken) : base(cancellationToken)
        {
            Control = control;
        }

        public Control Control { get; }

        // The control's hosting form gates acting on it (a modal blocking the form, or a disabled ancestor). A
        // control hosted in a menu dropdown belongs to the form that owns the menu -- its own FindForm is the
        // transient popup, or null while the dropdown is closed -- so gate it through that form instead.
        internal override Form OwningForm => IsHostedInMenu ? FormElement?.Form : Control.FindForm();

        // A control hosted inside a menu dropdown -- e.g. the ion-type buttons in View > Libraries > Ion Types,
        // hosted via a ToolStripControlHost. Its own FindForm is the transient popup (or null while the dropdown
        // is closed), so it is gated through the form that owns the menu instead. A ToolStripDropDown in its
        // parent chain marks it.
        private bool IsHostedInMenu
        {
            get
            {
                for (var parent = Control.Parent; parent != null; parent = parent.Parent)
                    if (parent is ToolStripDropDown)
                        return true;
                return false;
            }
        }

        // Gate the form, then the control itself: a user cannot act on a disabled control, so reject one.
        // Control.Enabled reflects a disabled ancestor too. The form gate already covered a disabled form.
        // Visibility is not checked: we only ever build an element for a control that was visible, and a control
        // that goes hidden mid-action (e.g. a menu whose dropdown has closed) should still be acted on.
        public override void VerifyEnabled()
        {
            VerifyFormInteractable(OwningForm, CancellationToken);
            if (!Control.Enabled)
                throw new InvalidOperationException(LlmInstruction.Format(
                    @"Control '{0}' is disabled.", Control.Name));
        }

        public override string Name => Control.Name;
        public override Type ElementType => Control.GetType();

        public override bool IsEnabled => true == OwningForm?.Enabled && Control.Enabled;

        // Most controls have no children -- a button, a text box, a list. Only a ContainerElement (a Form
        // or UserControl) owns children; the inherited (empty) EnumerateChildren is correct for everything else.

        // A control is clicked through its IButtonControl handler -- a real button, or a custom tile such as
        // a StartPage action box or recent-file row, whose clickable surface is covered by child labels and
        // an icon; PerformClick is reliable regardless of those children. A control that is not an
        // IButtonControl has no programmatic click (clicking it would have to synthesize a mouse gesture at a
        // guessed point, which is fragile), so it reports that it cannot be clicked. A subclass with a more
        // direct gesture overrides Click: a button uses BM_CLICK (to bypass PerformClick's gates), a
        // ToolStripItem / native dialog button drives its own.


        public virtual void ClickNow()
        {
            if (!(Control is IButtonControl button))
                throw new ArgumentException(LlmInstruction.Format(
                    @"The control '{0}' cannot be clicked: it is not a button. Set its value, or act on the button/menu item that triggers it.",
                    Label ?? NullIfEmpty(Name) ?? ElementType.Name));
            button.PerformClick();

        }

        // A button-like control carries its own caption in Text -- including a custom IButtonControl tile
        // such as a StartPage ActionBoxControl, whose Text is its visible caption ("Blank Document"). A
        // plain field has no caption of its own; it is named by the Label immediately before it in tab order
        // (e.g. "Name:" -> the name textbox, "Use only scans within" -> the RT box). Caption-bearing
        // elements (button, checkbox, tab) override this to return their own text.
        public override string Label
        {
            get
            {
                if (Control is IButtonControl && !string.IsNullOrEmpty(Control.Text))
                    return Control.Text;
                var previous = Control.FindForm()?.GetNextControl(Control, false);
                return (previous as Label)?.Text;
            }
        }

        public override UiElement GetChild(UiElementPath path)
        {
            // Type "ContextMenu" addresses this control's right-click menu (never one of its real children).
            if (string.Equals(path.Type, ContextMenuElement.TypeName, StringComparison.OrdinalIgnoreCase))
                return new ContextMenuElement(this);
            return base.GetChild(path);
        }

        /// <summary>Builds this control's right-click context menu the way a right-click would (so items added
        /// on demand are present), for <see cref="ContextMenuElement"/> to list or invoke. The default is the
        /// control's own <see cref="System.Windows.Forms.Control.ContextMenuStrip"/> (a graph populates its own
        /// menu); a grid and the Targets tree, whose menus are built/owned elsewhere, override this. Runs on
        /// the UI thread.</summary>
        // A plain control's only menu is the one you get by right-clicking it -- which is the ONLY menu a grid, a
        // tree or a graph has. A ToolStripElement overrides this to return itself, and a container to prefer its
        // menu bar / toolbar.
        public override ToolStripElement MainToolStrip => ToolStripElement.ForContextMenu(this);

        public virtual ContextMenuStrip BuildContextMenu()
        {
            // A graph (this control is a ZedGraphControl, or a graph form is just its graph) populates its own menu.
            var graphMenu = TryBuildGraphContextMenu(Control);
            if (graphMenu != null)
                return graphMenu;
            // Otherwise the control's own ContextMenuStrip, with its Opening raised so on-demand items appear.
            if (Control.ContextMenuStrip != null)
                return OpenContextMenu(Control.ContextMenuStrip);
            throw new ArgumentException(LlmInstruction.Format(
                @"{0} has no context menu.", Label ?? NullIfEmpty(Name) ?? ElementType.Name));
        }

        // Fires a context menu's Opening (so items added on demand are present) and returns it, for an element
        // that surfaces an already-built menu (a control's own ContextMenuStrip, the Targets-tree menu). The
        // menu is owned elsewhere -- do not dispose it. Runs on the UI thread.
        internal static ContextMenuStrip OpenContextMenu(ContextMenuStrip menu)
        {
            RaiseProtectedHandler<ToolStripDropDown>(menu, @"OnOpening", new CancelEventArgs());
            return menu;
        }

        // Populates a graph's own context menu through its ContextMenuBuilder, or returns null when the
        // control is not a graph. The graph can be addressed as its ZedGraphControl, or -- since a graph form
        // is just its graph -- as the form itself. The menu returned is the control's own ContextMenuStrip
        // (created with the control's components and disposed with the control), so the caller must not dispose
        // it. Runs on the UI thread.
        internal static ContextMenuStrip TryBuildGraphContextMenu(Control control)
        {
            var zedGraph = control as ZedGraph.ZedGraphControl
                ?? (control as DockableFormEx != null ? JsonUiService.TryGetZedGraphControl((DockableFormEx) control) : null);
            if (zedGraph == null)
                return null;
            // Reuse the ZedGraphControl's own ContextMenuStrip (contextMenuStrip1) rather than building a new
            // one, so there is nothing extra to dispose -- it is owned and disposed by the control.
            var graphMenu = zedGraph.ContextMenuStrip;
            JsonUiService.PopulateGraphContextMenu(zedGraph, graphMenu);
            return graphMenu;
        }

        // Raises a protected On<Event> method (Control.OnKeyDown, ToolStripDropDown.OnOpening,
        // DataGridView.OnCellContextMenuStripNeeded, ...) by reflection, so the wired handlers run the way the
        // real UI event would. TDeclaring is the type that declares the method, given explicitly so there is no
        // need to search the hierarchy for it; a virtual method still dispatches to the target's own override.
        internal static void RaiseProtectedHandler<TDeclaring>(TDeclaring target, string methodName, object eventArgs)
        {
            var method = typeof(TDeclaring).GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { eventArgs.GetType() }, null);
            if (method == null)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Could not raise {0} on {1}.", methodName, typeof(TDeclaring).Name));
            method.Invoke(target, new[] { eventArgs });
        }

        
    }

    /// <summary>Base for an element backed by a strongly-typed control, so subclasses use the control as
    /// its real type (e.g. a CheckedListBox) without casting.</summary>
    internal abstract class ControlElement<T> : ControlElement where T : Control
    {
        protected ControlElement(T control, CancellationToken cancellationToken) : base(control, cancellationToken) { }
        public new T Control => (T) base.Control;
    }

    /// <summary>A Form or a UserControl -- a boundary that owns its (flattened) children. It has no action
    /// of its own; it exists so the walk can list and descend into the controls it contains. Every other
    /// container (Panel, GroupBox, ...) is transparent (its controls are pulled up to the nearest Form or
    /// UserControl), so the only things that need a container element are these two. A Form is a
    /// <see cref="StandaloneForm"/> (a container that is also an addressable <see cref="StandaloneWindow"/>).</summary>
    internal class ContainerElement : ControlElement
    {
        public ContainerElement(Control control, CancellationToken cancellationToken) : base(control, cancellationToken) { }

        protected IEnumerable<UiElement> GetDescendants(Control container)
        {
            // Walk the children in TAB order (Control.TabIndex), not the z-order the Controls collection is in,
            // so an adjacent label -> field pair is reported one right after the other. OrderBy is stable, so
            // controls that share a TabIndex keep their Controls-collection order. Nesting is honored because
            // FlattenChildren recurses per container, ordering each level's own children.
            foreach (Control control in container.Controls.Cast<Control>().OrderBy(c => c.TabIndex))
            {
                // Skip a hidden control and everything under it -- e.g. the controls on an unselected tab page,
                // which a user cannot see or act on either (Control.Visible reflects a hidden ancestor too).
                if (!control.Visible)
                    continue;
                var element = FormElement.ElementFor(control);
                if (element != null)
                    yield return element;
                // Recurse through a transparent container (no element of its own), and through a TabControl
                // (kept above) to flatten its tab contents up alongside it.
                if (element == null || control is TabControl)
                    foreach (var inner in GetDescendants(control))
                        yield return inner;
            }
        }


        public override IEnumerable<UiElement> EnumerateChildren() => GetDescendants(Control);

        // A form's menu BAR (File, Edit, ...) if it has one; failing that its first toolbar; failing both it has no
        // menu strip of its own, so a menu path is matched against its right-click menu instead (the base).
        //
        // DESCENDANTS, not children: a toolbar need not be a direct child of the form. The Document Grid's "Reports"
        // strip is its nav bar, which sits inside the DataboundGridControl -- and a UserControl is a boundary the
        // children walk stops at, so a direct-children search would miss it and fall through to a context menu the
        // form does not have. The MainMenuStrip is itself one of the form's controls, so it is already in the walk:
        // pick it out by identity rather than building a second element for the same strip.
        public override ToolStripElement MainToolStrip
        {
            get
            {
                var mainMenu = (Control as Form)?.MainMenuStrip;
                ToolStripElement firstToolbar = null;
                foreach (var toolStrip in SelfAndDescendants().OfType<ToolStripElement>())
                {
                    if (mainMenu != null && ReferenceEquals(toolStrip.Control, mainMenu))
                        return toolStrip;
                    firstToolbar = firstToolbar ?? toolStrip;
                }
                return firstToolbar ?? base.MainToolStrip;
            }
        }
    }

    /// <summary>A WinForms top-level form, addressed by a formId. It is a <see cref="ContainerElement"/> (it
    /// owns the form's flattened controls) that also implements <see cref="StandaloneWindow"/>: the verbs resolve
    /// a managed formId to this and call its methods, which marshal to the UI thread (and watch for a dialog
    /// a mutation pops) so the connector drives a form the same way whether or not it is native. It is the
    /// factory (<see cref="ElementFor"/>) for the elements in its tree, tagging each with itself.</summary>
    public class StandaloneForm : StandaloneWindow
    {
        // Wraps a managed form, reading its window handle now. Must be built on the form's own UI thread -- reading
        // Form.Handle off it trips the cross-thread check -- which the assertion enforces. Off that thread, build it
        // with the handle already in hand (the two-argument constructor). The check runs BEFORE Form.Handle is read
        // (a constructor initializer would evaluate it first, so the assertion could never fire).
        public StandaloneForm(Form form, CancellationToken cancellationToken) : base(cancellationToken, GetHandle(form))
        {
            Form = form;
        }

        // Reads the handle on the form's own thread, asserting first: off that thread the raw read throws the
        // cross-thread exception, which says far less than the assertion does.
        private static IntPtr GetHandle(Form form)
        {
            Assume.IsFalse(form.InvokeRequired);
            return form.Handle;
        }

        // Wraps a managed form whose window handle is already known (e.g. from a Win32 modal-window enumeration, or
        // recorded when the modal was shown), so the element can be built off the form's UI thread without touching
        // Form.Handle.
        protected StandaloneForm(Form form, IntPtr hwnd, CancellationToken cancellationToken) : base(cancellationToken, hwnd)
        {
            Form = form;
        }

        public static StandaloneForm Create(Form form, IntPtr hwnd, CancellationToken cancellationToken)
        {
            switch (form)
            {
                case SkylineWindow skylineWindow:
                    return new SkylineStandaloneForm(skylineWindow, hwnd, cancellationToken);
                default:
                    return new StandaloneForm(form, hwnd, cancellationToken);
            }
        }

        public Form Form { get; }

        // A form is no longer a ControlElement, so the element tree under it is owned by a ContainerElement wrapping
        // the same Form -- built once, here, and deferred to for everything structural (the children walk, the path
        // walk, and so everything built on them: GetControlsNow, FindElement, FindGrid, ResolvePath). This is the one
        // place the form becomes a control, so the flattening rules live in ContainerElement alone and are not
        // duplicated here.
        private ContainerElement _formContainer;
        private ContainerElement FormContainer => _formContainer ??= (ContainerElement) ElementFor(Form);

        // The form gates itself: VerifyEnabled checks (through VerifyFormInteractable) that no modal is blocking
        // this window. That check MUST be the Win32 one -- a modal calls EnableWindow(false) on the windows it blocks
        // without flipping their managed Control.Enabled, so IsEnabled below stays true and would miss it.
        internal override Form OwningForm => Form;


        // Window-state queries answered from this element's Form.
        public override bool IsModal => Form.Modal;
        // A LongWaitDlg is the one form that closes ITSELF -- when the work it reports on finishes. Every other form
        // stands there until it is dismissed.
        public override bool IsTransient => Form is LongWaitDlg;
        public override bool IsOpen => !Form.IsDisposed && Form.IsHandleCreated && Form.Visible;
        public override bool IsProgressing => Form is ILongWaitForm { IsBusy: true };
        public override string DetailedMessage => (Form as CommonFormEx)?.DetailedMessage ?? Form.Text;

        // Only the main Skyline window pastes / selects all at the window level (into/over the document); any
        // other form has no window-level clipboard gesture, so refuse it with a clear message.

        /// <summary>Builds the <see cref="UiElement"/> for a control in this form's tree, choosing the
        /// subclass by the control's kind and tagging it with this form. Returns null for a control that is
        /// not an element (a label, a spacer, a transparent panel) -- the caller treats it as transparent and
        /// recurses into it.</summary>
        public UiElement ElementFor(Control control)
        {
            var element = CreateElement(control);
            if (element != null)
                element.FormElement = this;
            return element;
        }

        private ControlElement CreateElement(Control control)
        {
            // Each child is built with this form's cancellation, so a client that disconnects abandons work done
            // through any element in the tree, not just the form itself.
            var token = CancellationToken;
            switch (control)
            {
                case CheckBox checkBox: return new CheckBoxElement(checkBox, token);
                case RadioButton radioButton: return new RadioButtonElement(radioButton, token);
                // A button is clicked through IButtonControl (Button is the only ButtonBase that implements it --
                // a checkbox and a radio button are handled above, each with the click it actually has). Anything
                // else deriving from ButtonBase is a control we would not know how to click, so it falls through
                // to the default: not an element at all, rather than one whose click cannot work.
                case ButtonBase button when button is IButtonControl: return new ButtonElement(button, token);
                case ComboBox comboBox: return new ComboBoxElement(comboBox, token);
                case TextBoxBase textBox: return new TextBoxElement(textBox, token);
                case TabControl tabControl: return new TabElement(tabControl, token);
                // CheckedListBox before ListBox -- it derives from ListBox, so its case must win.
                case CheckedListBox checkedListBox: return new CheckedListBoxElement(checkedListBox, token);
                // The Pick Children pop-up's owner-drawn ListBox presents as a CheckedListBox.
                case ListBox listBox when listBox.FindForm() is PopupPickList: return new PopupPickListElement(listBox, token);
                case ListBox listBox: return new ListControlElement<ListBox>(listBox, token);
                // SequenceTree before TreeView -- it derives from TreeView, so its case must win.
                case SequenceTree sequenceTree: return new SequenceTreeElement(sequenceTree, token);
                case TreeView treeView: return new TreeViewElement(treeView, token);
                case ListView listView: return new ItemContainerElement<ListView>(listView, token);
                // The grid itself -- the inner grid of a DataboundGridControl (a BoundDataGridView, driven
                // through its rich copy/paste path) or a standalone DataGridView (direct cell access). The
                // DataboundGridControl is a UserControl you walk into to reach this grid and its nav bar.
                case BoundDataGridView boundDataGridView: return new BoundGridElement(boundDataGridView, token);
                case DataGridView dataGridView: return new GridElement(dataGridView, token);
                // A toolstrip (a menu bar, a toolbar, a grid's nav bar) stays an element of its own: it is what a
                // caller names when it asks for a menu item on a particular strip, and MainToolStrip finds the
                // form's menu bar among these.
                case ToolStrip toolStrip: return ToolStripElement.ForToolStrip(toolStrip, token);
                // A UserControl (including a DataboundGridControl) is a boundary that owns its (flattened)
                // children; everything else that contains controls is transparent. A nested Form (rare as a
                // child) is treated as a plain container under this form's token.
                case UserControl userControl: return new ContainerElement(userControl, token);
                case Form form: return new ContainerElement(form, token);
                default:
                    // A custom clickable that is not a ButtonBase (e.g. a StartPage tile).
                    if (control is IButtonControl)
                        return new ClickableControlElement(control, token);
                    // Anything else reaching here is a leaf with no capability (a label, a spacer, an empty
                    // panel); it is not something a caller can act on, so it is not an element.
                    return null;
            }
        }

        public override string FormId => JsonUiService.GetFormId(Form, Hwnd);
        public override string Title => JsonUiService.GetFormTitle(Form, Hwnd);

        // Sets a control's value (or a grid cell) on the form, in ONE trip to the form's thread: find the target,
        // gate it, set it. A grid cell ("grid[column,row]") moves the current cell there and then pastes, reusing the
        // one grid element so a DataboundGridControl stays in sync; anything else is matched by its own Label. A
        // control that is missing, blocked or disabled throws out of the posted delegate and the wait re-throws it
        // here. The wait returns only once the value has taken effect (the count settling, or a modal that a
        // validation raises appearing). Must be called off the UI thread.
        public override ActionResult SetValue(string controlId, string value)
        {
            return DialogWatcher.PerformAction(Hwnd, () =>
            {
                if (TryParseGridCell(controlId, out var gridName, out var column, out var row))
                {
                    var gridElement = FindGrid(gridName);
                    UiActions.SetCurrentCellAddress.InvokeNow(gridElement, new[] { column, row });
                    UiActions.SetGridText.InvokeNow(gridElement, value);
                    return;
                }
                UiActions.SetValue.InvokeNow(FindElement(controlId, UiActions.SetValue), value);
            }, CancellationToken);
        }

        // Finds the grid to act on: the one named controlId, or -- when controlId is null/empty -- the single
        // grid on the form, resolved through the shared element finder. A grid supports the grid actions and
        // is matched by its control name (it has no caption -- see GridElement.MatchesText), so FindElement
        // picks it out of the form's controls just like any other control. The factory wraps a bound inner
        // grid (a DataboundGridControl's BoundDataGridView) as a BoundGridElement with the rich copy/paste
        // path, and a standalone DataGridView as a plain GridElement; the DataboundGridControl itself is a
        // transparent container the walk descends through to reach the inner grid.
        internal GridElement FindGrid(string controlId)
        {
            return (GridElement) FindElement(controlId ?? string.Empty, UiActions.SetGridText);
        }

        // Parses a grid-cell locator "name[column,row]" (the name is optional -> the form's single grid).
        // Returns false for a plain control id (no "[col,row]" suffix). column/row are zero-based indices
        // into the grid's visible columns and its rows; row may be -1 for a header.
        private static bool TryParseGridCell(string controlId, out string gridName, out int column, out int row)
        {
            gridName = controlId ?? string.Empty;
            column = row = 0;
            if (string.IsNullOrEmpty(controlId))
                return false;
            var match = Regex.Match(controlId, @"^(?<name>.*?)\s*\[\s*(?<col>-?\d+)\s*,\s*(?<row>-?\d+)\s*\]$");
            if (!match.Success)
                return false;
            gridName = match.Groups[@"name"].Value;
            column = int.Parse(match.Groups[@"col"].Value);
            row = int.Parse(match.Groups[@"row"].Value);
            return true;
        }

        // Accepts the form by clicking its default (accept) button -- what pressing Enter does -- so the connector
        // confirms without matching a localized caption. Rides DialogWatcher.OkDialog's shared wait: on this form's
        // UI thread it gates the form and clicks the default button (a blocked form or one with no default button
        // fails, surfaced to the caller); the wait completes once the form has closed AND the count has ridden back
        // to its opener's pre-show level, stopping on a modal the accept opens. Must be called off the UI thread.
        public override ActionResult DismissWithAcceptButton()
        {
            return OkDialog(() =>
            {
                VerifyEnabled();
                IButtonControl acceptButton = Form.AcceptButton;
                if (acceptButton == null)
                    throw new ArgumentException(LlmInstruction.Format(
                        @"The form '{0}' has no default (accept) button.", FormId));
                acceptButton.PerformClick();
            });
        }

        // Like DismissWithAcceptButton, but clicks the form's cancel button (or closes the form when it has none),
        // the same way. Must be called off the UI thread.
        public override ActionResult DismissWithCancelButton()
        {
            return OkDialog(() =>
            {
                VerifyEnabled();
                var cancelButton = Form.CancelButton;
                if (cancelButton != null)
                    cancelButton.PerformClick();
                else
                    Form.Close();
            });
        }

        // How long to wait (ms), and how often to re-check, for the form to reach the foreground before
        // grabbing the screen. The wait stops as soon as the form is in front; the cap just bounds a refused
        // activation.
        private const int ACTIVATE_POLL_MILLIS = 25;
        private const int ACTIVATE_SETTLE_MAX_MILLIS = 500;

        // Activates the form and captures it (redacting any sensitive regions), as a right-click "capture
        // screenshot" would. Called off the UI thread (the connector pipe thread, or a test thread). Bringing
        // the window to the front is processed by the UI thread's message loop, so the activation and the
        // capture are two separate UI-thread trips: in between, this off-UI caller releases the UI thread and
        // polls until the form's top-level window is actually the foreground window -- stopping the moment it
        // is, or after the cap if activation was refused. Capturing before the form is on top would leave any
        // window still over it to be redacted (a cyan block) by CaptureAndRedact.
        public override System.Drawing.Bitmap CaptureImage()
        {
            var topLevelHandle = DialogWatcher.CallFunction(Hwnd, () =>
            {
                ScreenCapture.ActivateForm(Form);
                return (FormUtil.FindTopLevelOwner(Form) ?? Form).Handle;
            }, CancellationToken);
            for (int waited = 0;
                 waited < ACTIVATE_SETTLE_MAX_MILLIS && User32.GetForegroundWindow() != topLevelHandle;
                 waited += ACTIVATE_POLL_MILLIS)
                Thread.Sleep(ACTIVATE_POLL_MILLIS);
            return DialogWatcher.CallFunction(Hwnd, () =>
            {
                // Flush any pending repaint so the screen grab reflects the form's current state rather than a
                // stale frame (e.g. a wizard page captured mid-transition still showing the previous page).
                Form.Update();
                return ScreenCapture.CaptureAndRedact(ScreenCapture.GetWindowRectangle(Form), Form);
            }, CancellationToken);
        }

        public override string Name => Form.Name;
        public override Type ElementType => Form.GetType();

        // The managed enabled state only. A modal blocking this form does NOT show up here (it disables the window
        // at the Win32 level without touching Control.Enabled) -- that is what the OwningForm gate above is for.
        public override bool IsEnabled => Form.Enabled;

        // Structure is the container's job (see FormContainer): the form owns no walk of its own.
        public override IEnumerable<UiElement> EnumerateChildren() => FormContainer.EnumerateChildren();
        public override UiElement GetChild(UiElementPath path) => FormContainer.GetChild(path);

        // Menus, like structure, are the container's job: it holds the form's controls, so it is what knows the
        // form's menu bar and toolbars (see ContainerElement.MainToolStrip).
        public override ToolStripElement MainToolStrip => FormContainer.MainToolStrip;

        /// <summary>
        /// Describes this form, WITHOUT marshaling anywhere. It reports what the CALLING thread may legitimately
        /// read, and leaves the rest empty: the identifying fields (type, title, id, nesting count) are safe from
        /// any thread, but DockState, HasGraph and DetailedMessage all walk into the form's own state and belong to
        /// the thread that owns it. Called ON that thread (InvokeRequired false) it fills in everything.
        ///
        /// <para>Not marshaled here on purpose. The caller decides where this runs -- JsonUiService.GetOpenForms
        /// makes ONE trip to the main window and describes every form there, so nearly every form is complete and
        /// nothing is posted to a form that may be closing. A form on its OWN message loop (a
        /// BackgroundThreadLongWaitDlg) is the only one that comes back partial, and it is a progress dialog:
        /// there is nothing on it worth the round trip.</para>
        /// </summary>
        public override FormInfo GetFormInfo()
        {
            var formInfo = new FormInfo
            {
                Type = Form.GetType().Name,
                Title = JsonUiService.GetFormTitle(Form, Hwnd),
                Id = JsonUiService.GetFormId(Form, Hwnd),
                ModalNestingCount = DialogWatcher.TryGetPreShowActionCount(Hwnd),
            };
            if (Form.InvokeRequired)
                return formInfo;    // another thread owns this form: report only what is safe to read from here

            formInfo.DockState = GetDockState();
            formInfo.HasGraph = Form is DockableFormEx dockableForm &&
                                null != JsonUiService.TryGetZedGraphControl(dockableForm);
            // What the form SAYS -- an alert's text -- so a caller listing the forms can see that one is in the way,
            // and why, without capturing an image of it. Only for a form that says something beyond its own title (a
            // CommonFormEx: an alert, an error); a plain form's DetailedMessage IS its title.
            formInfo.DetailedMessage = Form is CommonFormEx
                ? JsonUiService.TruncateDetail(DetailedMessage)
                : null;
            return formInfo;
        }

        protected override string GetDockState()
        {
            if (Form is DockableFormEx dockableFormEx)
            {
                return dockableFormEx.DockState.ToString();
            }

            return base.GetDockState();
        }
    }

    /// <summary>The MAIN Skyline window. It is the one form with window-level clipboard gestures: paste puts content
    /// INTO the document (the tutorial steps that say to paste a transition list, a protein list, a peptide list),
    /// and select-all selects the document's targets. No other form has anything to paste into at the window level.
    ///
    /// <para>Which is why <see cref="IClipboardElement"/> lives HERE and not on <see cref="StandaloneForm"/>. It used
    /// to be on the base, where EVERY form advertised paste / select_all through get_actions and then threw "only
    /// supported for the main Skyline window" when a caller took it up -- a capability the element claimed and did
    /// not have. Now the type tells the truth, and no other form offers them at all.</para></summary>
    public sealed class SkylineStandaloneForm : StandaloneForm, IClipboardElement
    {
        public SkylineStandaloneForm(SkylineWindow skylineWindow, IntPtr hwnd, CancellationToken cancellationToken)
            : base(skylineWindow, hwnd, cancellationToken)
        {
        }

        public SkylineWindow SkylineWindow => (SkylineWindow) Form;

        public void PasteNow(string text) => SkylineWindow.Paste(text);

        public void SelectAllNow() => SkylineWindow.SelectAll();

        public ToolStripElement MainMenuStrip
        {
            get
            {
                return (ToolStripElement) ElementFor(SkylineWindow.MainMenuStrip);
            }
        }

        public void SetUndoRedoPosition(int index)
        {
            JsonUiService.RequireMainWindow();
            if (index == 0)
                return; // Already at current state

            var undoMgr = SkylineWindow.GetUndoManager();
            if (index < 0)
            {
                // Undo: index -1 = undo top (stack index 0), -2 = undo 2 deep, etc.
                int stackIndex = -index - 1;
                if (stackIndex >= undoMgr.UndoCount)
                    throw new ArgumentOutOfRangeException(nameof(index),
                        LlmInstruction.Format(@"Undo index {0} is out of range. Only {1} undo steps available.",
                            index, undoMgr.UndoCount));
                undoMgr.UndoRestore(stackIndex);
            }
            else
            {
                // Redo: index +1 = redo top (stack index 0), +2 = redo 2 deep, etc.
                int stackIndex = index - 1;
                if (stackIndex >= undoMgr.RedoCount)
                    throw new ArgumentOutOfRangeException(nameof(index),
                        LlmInstruction.Format(@"Redo index {0} is out of range. Only {1} redo steps available.",
                            index, undoMgr.RedoCount));
                undoMgr.RedoRestore(stackIndex);
            }
        }

        protected override string GetDockState()
        {
            return @"Main";
        }
    }

    /// <summary>A button -- an IButtonControl, clicked through its own handler by the inherited gesture
    /// (PerformClick), the same way a form's accept button is clicked. Its base for the caption-bearing
    /// controls: a button, a checkbox and a radio button all carry their label in Text.</summary>
    internal class ButtonElement : ControlElement
    {
        public ButtonElement(Control control, CancellationToken cancellationToken) : base(control, cancellationToken) { }
        public override string Label => Control.Text;
    }

    /// <summary>A checkbox or radio button. Neither is an IButtonControl, so neither can be clicked the way a
    /// button is; they are clicked with BM_CLICK, which their wndproc cannot tell from a real mouse click -- it
    /// runs the Click handler and honors AutoCheck = false. (A control in the DIA-to-SRM tutorial does its job
    /// only for that message, which is why this is not a PerformClick.)
    ///
    /// <para>SENT, not posted: UiAction.Invoke has already put us on the control's own UI thread, so a click that
    /// opens a modal blocks there, inside the counted delegate, and the wait reports that dialog -- where a posted
    /// click would return as though it had finished, before its handler had even run.</para></summary>
    internal abstract class CheckableElement : ButtonElement, IValueElement
    {
        protected CheckableElement(Control control, CancellationToken cancellationToken) : base(control, cancellationToken) { }
        public override void ClickNow() =>
            User32.SendMessage(Control.Handle, User32.WinMessageType.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
        public abstract void SetValueNow(object value);
    }

    /// <summary>A checkbox: clickable (toggles through its handler) and value-settable (sets the checked state).</summary>
    internal sealed class CheckBoxElement : CheckableElement
    {
        private readonly CheckBox _checkBox;
        public CheckBoxElement(CheckBox checkBox, CancellationToken cancellationToken) : base(checkBox, cancellationToken) { _checkBox = checkBox; }
        public override object GetValueNow() => _checkBox.Checked;
        public override void SetValueNow(object value) => _checkBox.Checked = UiValue.ParseBool(value);
    }

    /// <summary>A radio button: clicking/setting it checks it (WinForms unchecks its siblings).</summary>
    internal sealed class RadioButtonElement : CheckableElement
    {
        private readonly RadioButton _radioButton;
        public RadioButtonElement(RadioButton radioButton, CancellationToken cancellationToken) : base(radioButton, cancellationToken) { _radioButton = radioButton; }
        public override object GetValueNow() => _radioButton.Checked;
        public override void SetValueNow(object value) => _radioButton.Checked = UiValue.ParseBool(value);
    }

    /// <summary>A text box -- a caption-less field named by its adjacent label.</summary>
    internal sealed class TextBoxElement : ControlElement, IValueElement, IClipboardElement
    {
        private readonly TextBoxBase _textBox;
        public TextBoxElement(TextBoxBase textBox, CancellationToken cancellationToken) : base(textBox, cancellationToken) { _textBox = textBox; }
        public override object GetValueNow() => _textBox.Text;
        // A multi-line box parses/lays out on CRLF (what Enter inserts), so normalize bare newlines.
        public void SetValueNow(object value) =>
            _textBox.Text = _textBox.Multiline ? UiValue.NormalizeNewlines(value?.ToString()) : value?.ToString();

        // Paste replaces the current selection (or inserts at the caret) with the text via SelectedText, the
        // way Ctrl+V would but without the clipboard.
        public void PasteNow(string text) =>
            _textBox.SelectedText = _textBox.Multiline ? UiValue.NormalizeNewlines(text) : text;

        public void SelectAllNow() => _textBox.SelectAll();
    }

    /// <summary>A combo box -- value set by selecting the matching item; its choices are read with get_options.</summary>
    internal sealed class ComboBoxElement : ControlElement, IValueElement, IOptionsElement
    {
        private readonly ComboBox _comboBox;
        public ComboBoxElement(ComboBox comboBox, CancellationToken cancellationToken) : base(comboBox, cancellationToken) { _comboBox = comboBox; }
        public override object GetValueNow() => _comboBox.GetItemText(_comboBox.SelectedItem);
        public IEnumerable<string> GetOptions() => ListItems.GetOptions(_comboBox);
        public void SetValueNow(object value)
        {
            var text = value?.ToString();
            int index = _comboBox.FindStringExact(text);
            if (index < 0)
                throw new ArgumentException(LlmInstruction.Format(
                    @"No item '{0}' in combo box {1}.", text, _comboBox.Name));
            _comboBox.SelectedIndex = index;
        }
    }

    /// <summary>A ListControl -- a ListBox or CheckedListBox. Select an item by index
    /// (set_selected_index) or by its text (select_item / unselect_item).</summary>
    internal class ListControlElement<T> : ControlElement<T>, ISelectItemsElement, IOptionsElement
        where T : ListControl
    {
        public ListControlElement(T control, CancellationToken cancellationToken) : base(control, cancellationToken) { }
        public void SetSelectedIndexNow(int index) => ListItems.SetSelectedIndex(Control, index);
        public void SetItemSelectedNow(string item, bool isSelected) => ListItems.SetSelected(Control, item, isSelected);
        // Every choice the list offers (get_options), regardless of selection/checked state.
        public virtual IEnumerable<string> GetOptions() => ListItems.GetOptions(Control);
    }

    /// <summary>A CheckedListBox. Besides the ListControl actions, an item is checked/unchecked by its text
    /// (check_item / uncheck_item) or toggled the way a user does it -- set_selected_index to the item, then
    /// click, which toggles the selected item's check. Its value is the checked items' text, one per line.</summary>
    internal sealed class CheckedListBoxElement : ListControlElement<CheckedListBox>,
        ICheckItemsElement
    {
        public CheckedListBoxElement(CheckedListBox control, CancellationToken cancellationToken) : base(control, cancellationToken) { }
        public override object GetValueNow() =>
            string.Join(Environment.NewLine, Control.CheckedItems.Cast<object>().Select(Control.GetItemText));
        public void SetItemCheckedNow(string item, bool isChecked) => ListItems.SetChecked(Control, item, isChecked);
        // A click toggles the checked state of the selected item, the way a user's click/space does (move to
        // the item first with set_selected_index). A CheckedListBox is not an IButtonControl, so the base gesture
        // would refuse it -- override ClickNow (not Click), so the two cannot disagree.
        public override void ClickNow()
        {
            int index = Control.SelectedIndex;
            if (index < 0)
                throw new ArgumentException(new LlmInstruction(
                    @"No item is selected -- choose one first with set_selected_index."));
            Control.SetItemChecked(index, !Control.GetItemChecked(index));
        }
    }

    /// <summary>A control whose items are checked or selected by their text -- a TreeView (a node by a
    /// '>'-separated path) or a ListView. Not a ListControl (it has no SelectedIndex); a caption-less one
    /// is reached through a path of its Type. The value is the item.</summary>
    internal class ItemContainerElement<T> : ControlElement<T>, ICheckItemsElement, ISelectItemsElement
        where T : Control
    {
        public ItemContainerElement(T control, CancellationToken cancellationToken) : base(control, cancellationToken) { }
        public void SetItemCheckedNow(string item, bool isChecked) => ListItems.SetChecked(Control, item, isChecked);
        public void SetItemSelectedNow(string item, bool isSelected) => ListItems.SetSelected(Control, item, isSelected);
        public void SetSelectedIndexNow(int index) => ListItems.SetSelectedIndex(Control, index);
    }

    /// <summary>A TreeView. Besides checking/selecting a node by text, a node is expanded or collapsed
    /// (expand/collapse) by a path: an array whose segments select a child at each level -- a string is the
    /// first child whose text matches it, an integer is the child at that index.</summary>
    internal class TreeViewElement : ItemContainerElement<TreeView>, IExpandCollapseElement
    {
        public TreeViewElement(TreeView control, CancellationToken cancellationToken) : base(control, cancellationToken) { }
        public void ExpandNow(object path) => ResolveTreePath(path).Expand();
        public void CollapseNow(object path) => ResolveTreePath(path).Collapse();

        // Resolves a tree path -- an array whose segments select a child at each level (an integer is the
        // child at that index; a string is the first child whose text matches it) -- to its TreeNode,
        // expanding each ancestor so lazily-built children are present before descending. The path value is
        // an object[] (an in-process caller) or a JSON array (over the wire). Must run on the UI thread.
        private TreeNode ResolveTreePath(object pathValue)
        {
            var path = ToTreePath(pathValue);
            if (path.Length == 0)
                throw new ArgumentException(new LlmInstruction(
                    @"The path is empty -- give an array of child names (strings) and/or indexes (integers)."));
            var nodes = Control.Nodes;
            TreeNode current = null;
            for (int i = 0; i < path.Length; i++)
            {
                current = FindChildNode(nodes, path[i]);
                if (i < path.Length - 1)
                {
                    current.Expand(); // populate lazily-built children before descending
                    nodes = current.Nodes;
                }
            }
            return current;
        }

        // One step of a tree path: an integer selects the child at that index; a string selects the first
        // child whose text matches it (the first, not the best -- ties go to document order).
        private static TreeNode FindChildNode(TreeNodeCollection nodes, object segment)
        {
            if (segment is int index)
            {
                if (index < 0 || index >= nodes.Count)
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Child index {0} is out of range; there are {1} children.", index, nodes.Count));
                return nodes[index];
            }
            var text = segment as string;
            foreach (TreeNode node in nodes)
                if (TextMatches(node.Text, text))
                    return node;
            throw new ArgumentException(LlmInstruction.Format(@"No child node matches '{0}'.", text));
        }

        // The path segments from the value a tree expand/collapse action carries: an object[] of strings
        // and integers (an in-process caller) or a JSON array (over the wire). A JSON integer is a child
        // index; a JSON string is a child's text.
        private static object[] ToTreePath(object pathValue)
        {
            switch (pathValue)
            {
                case null:
                    return new object[0];
                case string json:
                    return JArray.Parse(json).Select(ToTreePathSegment).ToArray();
                case System.Collections.IEnumerable items:
                    return items.Cast<object>().Select(ToTreePathSegment).ToArray();
                default:
                    throw new ArgumentException(new LlmInstruction(
                        @"A tree path must be an array of child names (strings) and/or indexes (integers)."));
            }
        }

        private static object ToTreePathSegment(object segment)
        {
            switch (segment)
            {
                case int i: return i;
                case long l: return (int) l;
                case JToken token:
                    return token.Type == JTokenType.Integer ? (object) (int) token : token.Value<string>();
                default: return segment as string;
            }
        }
    }

    /// <summary>The Targets tree (a <see cref="SequenceTree"/>): a TreeView with the document-owned node
    /// context menu and an in-place node rename a plain TreeView does not have.</summary>
    internal sealed class SequenceTreeElement : TreeViewElement, IRenameNodeElement, IClipboardElement
    {
        public SequenceTreeElement(SequenceTree control, CancellationToken cancellationToken) : base(control, cancellationToken) { }

        private SequenceTree SequenceTree => (SequenceTree) Control;

        // Pasting into the Targets tree pastes into the document (a transition list, peptides, or FASTA) --
        // the same as Ctrl+V with the tree focused -- without the clipboard.
        public void PasteNow(string text) => Program.MainWindow.Paste(text);

        public void SelectAllNow() => Program.MainWindow.SelectAll();

        // The Targets tree's node menu is shown manually, so it lives on the main window rather than on the
        // tree's own ContextMenuStrip; raise its Opening so item enablement reflects the current selection
        // (select the node first).
        public override ContextMenuStrip BuildContextMenu() =>
            OpenContextMenu(Program.MainWindow.ContextMenuTreeNode);

        // Renames the selected node in place the way a user typing into its label and pressing Enter would:
        // begin the in-place edit, set the text, commit it. Select the node first.
        public void RenameNodeNow(string value)
        {
            SequenceTree.BeginEdit(false);
            SequenceTree.StatementCompletionEditBox.TextBox.Text = value;
            SequenceTree.CommitEditBox(false);
        }
    }

    /// <summary>The owner-drawn ListBox on the Pick Children pop-up. It is a plain ListBox whose checkbox is
    /// painted from PickListChoice.Chosen on the PopupPickList -- not a real CheckedListBox check -- so a
    /// check routes through PopupPickList.SetItemChecked. To the connector it presents exactly as a
    /// CheckedListBox (check_item/uncheck_item, click toggles the selected item, get_value reads the checked
    /// items, plus the ListControl select actions). When PopupPickList is reworked to host a real
    /// CheckedListBox, this special case can go away.</summary>
    internal sealed class PopupPickListElement : ListControlElement<ListBox>,
        ICheckItemsElement
    {
        public PopupPickListElement(ListBox control, CancellationToken cancellationToken) : base(control, cancellationToken) { }
        private PopupPickList PickList => (PopupPickList) Control.FindForm();
        public override Type ElementType => typeof(CheckedListBox);
        public override object GetValueNow()
        {
            var pickList = PickList;
            var names = pickList.ItemNames.ToList();
            return string.Join(Environment.NewLine,
                Enumerable.Range(0, names.Count).Where(pickList.GetItemChecked).Select(i => names[i]));
        }
        // The pop-up's choices are the PickList's own item names (its ListBox items are not the display text),
        // so read them there -- and return ALL of them (get_options), not just the checked ones that Value reports.
        public override IEnumerable<string> GetOptions() => PickList.ItemNames.ToList();
        public void SetItemCheckedNow(string item, bool isChecked) => PickList.SetItemChecked(FindPickListIndex(item), isChecked);
        // A click toggles the selected item's check, like a user's click/space (move to the item first with
        // set_selected_index). Not an IButtonControl, so override ClickNow (not Click) -- see CheckedListBoxElement.
        public override void ClickNow()
        {
            int index = Control.SelectedIndex;
            if (index < 0)
                throw new ArgumentException(new LlmInstruction(
                    @"No item is selected -- choose one first with set_selected_index."));
            PickList.ToggleItem(index);
        }

        // Index of the best-matching choice (by its visible label) in this pick-list pop-up. Throws if none.
        private int FindPickListIndex(string item)
        {
            var labels = PickList.ItemNames.ToList();
            int best = ListItems.BestMatch(labels.Count, i => labels[i], item);
            if (best < 0)
                throw new ArgumentException(LlmInstruction.Format(@"Item not found in the pick list: {0}.", item));
            return best;
        }
    }

    /// <summary>The item operations the list/tree/list-view (and pick-list) elements share -- checking,
    /// selecting, and matching an item by its visible text (a TreeView node by a '>'-separated path).
    /// These were moved off JsonUiService now that only the elements drive them; they reuse the connector's
    /// text matching (<see cref="UiElement.TextMatches(string,string,bool)"/>) and run on the UI thread (the
    /// service marshals there).</summary>
    internal static class ListItems
    {
        public static void SetChecked(Control control, string item, bool isChecked)
        {
            switch (control)
            {
                case CheckedListBox checkedListBox:
                    checkedListBox.SetItemChecked(FindListItemIndex(checkedListBox, item), isChecked);
                    break;
                case TreeView treeView:
                    FindTreeNode(treeView, item).Checked = isChecked;
                    break;
                case ListView listView:
                    FindListViewItem(listView, item).Checked = isChecked;
                    break;
                default:
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Checking items is supported for a CheckedListBox, TreeView, or ListView, not {0}.", control.Name));
            }
        }

        public static void SetSelected(Control control, string item, bool selected)
        {
            switch (control)
            {
                case ListBox listBox: // CheckedListBox derives from ListBox
                    listBox.SetSelected(FindListItemIndex(listBox, item), selected);
                    break;
                case TreeView treeView:
                    var node = FindTreeNode(treeView, item);
                    if (selected)
                        treeView.SelectedNode = node;
                    else if (treeView.SelectedNode == node)
                        treeView.SelectedNode = null;
                    break;
                case ListView listView:
                    var listViewItem = FindListViewItem(listView, item);
                    listViewItem.Selected = selected;
                    if (selected)
                        listViewItem.EnsureVisible();
                    break;
                default:
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Selecting items is supported for a ListBox, TreeView, or ListView, not {0}.", control.Name));
            }
        }

        // Selects only the item at the given index, clearing any other selection -- what set_selected_index
        // does. (A control with a real SelectedIndex would already clear others, but a multi-select list or a
        // tree/list-view would not, so the selection is cleared explicitly.)
        public static void SetSelectedIndex(Control control, int index)
        {
            switch (control)
            {
                case ListBox listBox: // CheckedListBox derives from ListBox
                    RequireIndexInRange(index, listBox.Items.Count, listBox.Name);
                    listBox.ClearSelected();
                    listBox.SetSelected(index, true);
                    break;
                case TreeView treeView:
                    RequireIndexInRange(index, treeView.Nodes.Count, treeView.Name);
                    treeView.SelectedNode = treeView.Nodes[index];
                    break;
                case ListView listView:
                    RequireIndexInRange(index, listView.Items.Count, listView.Name);
                    listView.SelectedItems.Clear();
                    var listViewItem = listView.Items[index];
                    listViewItem.Selected = true;
                    listViewItem.EnsureVisible();
                    break;
                default:
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Setting the selected index is supported for a ListBox, TreeView, or ListView, not {0}.", control.Name));
            }
        }

        private static void RequireIndexInRange(int index, int count, string controlName)
        {
            if (index < 0 || index >= count)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Index {0} is out of range; {1} has {2} items.", index, controlName, count));
        }

        // The display text of EVERY item a list control offers, regardless of which are selected or checked --
        // what get_options reads. A ComboBox and a ListBox (a CheckedListBox derives from ListBox) both expose
        // their choices through Items + GetItemText.
        public static IEnumerable<string> GetOptions(Control control)
        {
            switch (control)
            {
                case ComboBox comboBox:
                    return comboBox.Items.Cast<object>().Select(comboBox.GetItemText).ToList();
                case ListBox listBox: // CheckedListBox derives from ListBox
                    return listBox.Items.Cast<object>().Select(listBox.GetItemText).ToList();
                default:
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Listing options is supported for a ComboBox or ListBox, not {0}.", control.Name));
            }
        }

        // Index of the best-matching item (by display text) in a ListBox. Throws if none.
        private static int FindListItemIndex(ListBox listBox, string item)
        {
            int best = BestMatch(listBox.Items.Count, i => listBox.GetItemText(listBox.Items[i]), item);
            if (best < 0)
                throw new ArgumentException(LlmInstruction.Format(@"Item not found in {0}: {1}.", listBox.Name, item));
            return best;
        }

        // The best-matching item (by its text) in a ListView. Throws if none.
        private static ListViewItem FindListViewItem(ListView listView, string item)
        {
            int best = BestMatch(listView.Items.Count, i => listView.Items[i].Text, item);
            if (best < 0)
                throw new ArgumentException(LlmInstruction.Format(@"Item not found in {0}: {1}.", listView.Name, item));
            return listView.Items[best];
        }

        // Walks a TreeView by a '>'-separated path of node texts, expanding each level so nodes built on
        // demand (e.g. the Customize Report field tree) are present before the next segment is matched.
        private static TreeNode FindTreeNode(TreeView treeView, string path)
        {
            // A node's text legitimately contains '|' and '/' (e.g. a UniProt name "sp|P02769|ALBU_BOVIN"),
            // so split on '>' only.
            var segments = (path ?? string.Empty)
                .Split('>').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            if (segments.Length == 0)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Empty tree path: {0}. Expected '>'-separated node texts, e.g. 'Protein > Peptide > Precursor'.",
                    path ?? string.Empty));
            var nodes = treeView.Nodes;
            TreeNode current = null;
            for (int i = 0; i < segments.Length; i++)
            {
                int best = BestMatch(nodes.Count, j => nodes[j].Text, segments[i]);
                if (best < 0)
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Tree node not found: {0} (no match for '{1}').", path, segments[i]));
                current = nodes[best];
                if (i < segments.Length - 1)
                {
                    current.Expand(); // populate lazily-built children before descending
                    nodes = current.Nodes;
                }
            }
            return current;
        }

        // The index of the best text match among count items (by the connector's label matching), or -1: the
        // first item that matches the key strictly, else the first that matches loosely (a strict match wins
        // over a loose one, the same ranking FindElement uses).
        internal static int BestMatch(int count, [InstantHandle] Func<int, string> textOf, string key)
        {
            for (int i = 0; i < count; i++)
                if (UiElement.TextMatches(textOf(i), key, true))
                    return i;
            for (int i = 0; i < count; i++)
                if (UiElement.TextMatches(textOf(i), key, false))
                    return i;
            return -1;
        }
    }

    /// <summary>A custom IButtonControl that is not a WinForms ButtonBase (e.g. a StartPage tile). The base
    /// Click already drives an IButtonControl through PerformClick; this just reports the control's own
    /// caption as the Label.</summary>
    internal sealed class ClickableControlElement : ControlElement
    {
        public ClickableControlElement(Control control, CancellationToken cancellationToken) : base(control, cancellationToken) { }
        public override string Label => Control.Text;
    }

    /// <summary>A ToolStrip (menu strip / toolbar) -- its items are its children. It owns the logic for
    /// matching a '>'-separated menu/toolbar path to an item: <see cref="ResolveMenuItem"/> walks the path,
    /// finding each segment by its visible text and opening each level's dropdown so lazily-built items are
    /// present. Both ClickMainMenuItem and ClickControlMenuItem drive it through this.</summary>
    public sealed class ToolStripElement : ControlElement
    {
        // The ToolStrip this element walks -- or null when it walks its OWNER's right-click menu instead. A context
        // menu is not a control: it does not exist until it is built (BuildContextMenu raises Opening so the
        // on-demand items appear), so there is no ToolStrip to point at, only the control that owns it. Both are
        // walked as a list of ToolStripItems, which is what lets one menu path resolve against either.
        private readonly ToolStrip _toolStrip;

        private ToolStripElement(Control control, ToolStrip toolStrip, ControlElement owner,
            CancellationToken cancellationToken) : base(control, cancellationToken)
        {
            _toolStrip = toolStrip;
            Owner = owner;
        }

        /// <summary>A real toolstrip -- a form's menu bar, a toolbar, a grid's nav bar.</summary>
        public static ToolStripElement ForToolStrip(ToolStrip toolStrip, CancellationToken cancellationToken)
        {
            return new ToolStripElement(toolStrip, toolStrip, null, cancellationToken);
        }

        /// <summary>A control's RIGHT-CLICK menu, presented as a toolstrip so a menu path resolves against it in
        /// exactly the same way -- which is what lets one verb reach a menu bar, a toolbar and a context menu.</summary>
        public static ToolStripElement ForContextMenu(ControlElement owner)
        {
            return new ToolStripElement(owner.Control, null, owner, owner.CancellationToken)
                { FormElement = owner.FormElement };
        }

        /// <summary>The control whose context menu this is; null when this is a real toolstrip.</summary>
        public ControlElement Owner { get; }

        // It IS the menu, so a menu path is matched against it directly.
        public override ToolStripElement MainToolStrip => this;

        public override IEnumerable<UiElement> EnumerateChildren()
        {
            var items = _toolStrip != null ? _toolStrip.Items : Owner.BuildContextMenu()?.Items;
            if (items == null)
                return Enumerable.Empty<UiElement>();
            return items.Cast<ToolStripItem>()
                .Select(item => (UiElement) new ToolStripItemElement(item, FormElement, CancellationToken));
        }

        /// <summary>Resolves a '>'-separated menu/toolbar path (e.g. "Reports &gt; Replicates") to its leaf
        /// item element within this toolstrip, opening each non-leaf level's dropdown so items built on demand
        /// are present before the next segment is matched; returns null when the path is not in this toolstrip
        /// (so a caller can try the next). Closes the dropdowns it opened before returning -- the leaf stays
        /// clickable while hidden. Must run on the UI thread.</summary>
        public UiElement ResolveMenuItem(string menuPath)
        {
            var segments = ParseMenuSegments(menuPath);
            UiElement current = this;
            var opened = new List<ToolStripItemElement>();
            try
            {
                for (int i = 0; i < segments.Length; i++)
                {
                    current = current.FindElementOrNull(segments[i], UiActions.Click);
                    if (current == null)
                        return null; // this toolstrip does not have the path
                    if (i < segments.Length - 1 && current is ToolStripItemElement dropDown)
                    {
                        dropDown.ShowDropDown(); // populate items built on DropDownOpening
                        opened.Add(dropDown);
                    }
                }
                return current;
            }
            finally
            {
                for (int i = opened.Count - 1; i >= 0; i--)
                    opened[i].HideDropDown();
            }
        }

        /// <summary>Resolves the menu/toolbar item the path names and clicks it. Must be called on this
        /// toolstrip's UI thread: walking the path opens each level's dropdown, so items built on demand (the
        /// Document Grid's Reports list) exist by the time the next segment is matched. Throws if the form is
        /// blocked, if the path names no item on this strip, or if the item is disabled.</summary>
        public void ClickMenuItemNow(string menuPath)
        {
            // Gate the FORM before the menu is walked at all: a modal blocking the window puts every item out of
            // reach, so the path is immaterial, and walking it would open dropdowns on a window the user could not
            // touch. (The item itself is gated by Click.InvokeNow.)
            VerifyEnabled();
            var leaf = ResolveMenuItem(menuPath) ?? throw new ArgumentException(LlmInstruction.Format(
                @"Menu item not found: {0}. Use skyline_get_controls to see what is there.", menuPath));
            UiActions.Click.InvokeNow(leaf, null);
        }

        // Splits a menu/toolbar path into its segments (separators '>', '|', '/'). Throws if empty.
        private static string[] ParseMenuSegments(string menuPath)
        {
            var segments = (menuPath ?? string.Empty)
                .Split(new[] { '>', '|', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            if (segments.Length == 0)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Empty menu path: {0}. Expected e.g. 'File > Import > Peptide Search'.",
                    menuPath ?? string.Empty));
            return segments;
        }
    }

    /// <summary>A menu or toolbar item -- clicked via PerformClick. An image-only item (no caption) is
    /// named by its tooltip, the way a user reads it (e.g. the pick-list's green-check "OK"). It carries its
    /// form so it can build the element for a hosted control (FormElement.ElementFor).</summary>
    internal sealed class ToolStripItemElement : UiComponent, IClickableElement
    {
        private readonly ToolStripItem _item;
        public ToolStripItemElement(ToolStripItem item, StandaloneForm formElement, CancellationToken cancellationToken)
            : base(cancellationToken)
        {
            _item = item;
            FormElement = formElement;
        }
        public override string Name => _item.Name;
        public override Type ElementType => _item.GetType();
        // A ToolStripControlHost reports its hosted control's Text as its own; let the hosted control
        // (this element's child) own that label, so the verbs act on the real control, not the host.
        public override string Label => _item is ToolStripControlHost ? null
            : string.IsNullOrEmpty(_item.Text) ? _item.ToolTipText : _item.Text;
        public override bool IsEnabled => _item.Enabled;
        private List<UiElement> _children;

        // A ToolStripControlHost hosts a real control: a single control the form recognizes (e.g. the Audit
        // Log "Enable audit logging" checkbox) is exposed as that control; a host whose control is a
        // transparent container (e.g. the Ion Types panel of ion-type checkbox-buttons) is flattened to its
        // children -- the way the form walk flattens a panel. A dropdown menu item is opened first, so items
        // built on demand (the Ion Types panel, the Document Grid's Reports list) are present before they are
        // listed, then reclosed; its hosted-control items are flattened the same way, so the real control(s)
        // surface as direct children rather than the ToolStripControlHost wrapper. Opening/closing the dropdown
        // is the side effect that makes this EnumerateChildren() (a method), not a property; the result is
        // cached so that side effect happens only once however often the children are enumerated.
        public override IEnumerable<UiElement> EnumerateChildren() => _children ??= BuildChildren();

        private List<UiElement> BuildChildren()
        {
            var children = new List<UiElement>();
            if (_item is ToolStripControlHost host && host.Control != null)
            {
                var hosted = FormElement.ElementFor(host.Control);
                if (hosted != null)
                    children.Add(hosted);
                else
                    children.AddRange(new ContainerElement(host.Control, CancellationToken) { FormElement = FormElement }.EnumerateChildren());
            }
            if (_item is ToolStripDropDownItem dropDownItem)
            {
                ShowDropDown();
                try
                {
                    foreach (ToolStripItem child in dropDownItem.DropDownItems)
                    {
                        var childElement = new ToolStripItemElement(child, FormElement, CancellationToken);
                        if (child is ToolStripControlHost)
                            children.AddRange(childElement.EnumerateChildren());
                        else
                            children.Add(childElement);
                    }
                }
                finally
                {
                    HideDropDown();
                }
            }
            return children;
        }
        // Marshaling through, and gating by, this item's form are inherited from UiComponent.


        public void ClickNow() => _item.PerformClick();

        // Opens / closes this item's dropdown (a menu/toolbar dropdown item), so a menu walk can populate
        // items built on DropDownOpening before matching the next path segment. A no-op for a leaf item.
        public void ShowDropDown() => (_item as ToolStripDropDownItem)?.ShowDropDown();
        public void HideDropDown() => (_item as ToolStripDropDownItem)?.HideDropDown();
    }

    /// <summary>A grid -- the DataGridView a caller reads as TSV or sets a cell on, by direct cell access.
    /// A bound grid (the inner grid of a DataboundGridControl, e.g. the Document Grid) is a
    /// <see cref="BoundGridElement"/> that overrides the read/write with the rich copy/paste path.</summary>
    internal class GridElement : ControlElement, IValueElement, IClipboardElement
    {
        private readonly DataGridView _dataGridView;
        public GridElement(DataGridView dataGridView, CancellationToken cancellationToken) : base(dataGridView, cancellationToken) { _dataGridView = dataGridView; }

        // Pasting into a grid is the same as set_grid_text: tab-separated text filled from the current cell.
        // SetGridText owns the gating/marshaling, so this just delegates to it.
        public void PasteNow(string text) => SetGridTextNow(text);

        public void SelectAllNow() => _dataGridView.SelectAll();
        public DataGridView DataGridView => _dataGridView;

        // get_value / set_value act on the single current cell (the one set_current_cell_address moved to);
        // the whole grid is read/written in bulk with get_grid_text / set_grid_text. The value is null when
        // there is no current cell. This is the cell's raw underlying value; ConvertValue normalizes it for
        // the connector at the point it is exposed (get_value, ControlInfo), not here.
        public override object GetValueNow() => _dataGridView.CurrentCell?.Value;

        public void SetValueNow(object value)
        {
            var cell = _dataGridView.CurrentCell;
            if (cell == null)
                throw new ArgumentException(new LlmInstruction(
                    @"The grid has no current cell -- move to one first with set_current_cell_address."));
            // Setting a cell's value programmatically bypasses the read-only state the user would face, so
            // refuse a read-only cell -- the connector must not do what the user could not.
            if (cell.ReadOnly || _dataGridView.ReadOnly)
                throw new ArgumentException(new LlmInstruction(
                    @"The current cell is read-only, so its value cannot be set."));
            cell.Value = value;
        }

        // A grid carries no caption, so it is addressed by its control Name -- the one place the connector
        // matches on a name rather than on visible text (an empty name picks the form's single grid, handled
        // by FindElement). The name match is the same whether strict or loose.
        public override bool MatchesText(string text, bool strict) =>
            string.Equals(Control.Name, text, StringComparison.OrdinalIgnoreCase);

        // A grid is a leaf in the walk (not a ContainerElement): its content is read/written through the
        // grid actions, not by walking into child controls. The plain path reads/writes cells directly.

        // Reads a plain DataGridView as tab-separated text: the column headers followed by every data row
        // (each cell shown as the user sees it).
        public virtual string GetGridText()
        {
            var visibleColumns = VisibleColumns();
            var lines = new List<string>
            {
                TextUtil.ToEscapedTSV(visibleColumns.Select(col => col.HeaderText))
            };
            foreach (DataGridViewRow gridRow in _dataGridView.Rows)
            {
                if (gridRow.IsNewRow)
                    continue;
                lines.Add(TextUtil.ToEscapedTSV(visibleColumns.Select(col => CellDisplayText(gridRow.Cells[col.Index]))));
            }
            return TextUtil.LineSeparate(lines);
        }

        // Pastes starting at the current cell -- the anchor a user would have clicked. Move the current cell
        // first with SetCurrentCellAddress; the text may be a multi-cell TSV block (it fills down/right).
        // DataGridViewPasteHandler sets each cell through the grid's editing control exactly as a user typing
        // would, so a value entered in the new row is committed and the grid grows (a direct cell.Value set is
        // not pushed through, leaving the row empty). A BoundGridElement overrides this to keep its document
        // in sync; this base path is for a plain DataGridView (e.g. the List Designer property grid).
        // SetGridText owns the gating + fire-and-forget post; the paste itself is SetGridTextCore (a bound grid
        // overrides it), kept separate so a caller already inside the gesture (Paste) does not post a second
        // time.
        public void SetGridTextNow(string text) => SetGridTextCore(text);

        protected virtual void SetGridTextCore(string text)
        {
            DataGridViewPasteHandler.PasteText(_dataGridView, text);
        }

        // Moves the current cell so the next SetGridText / context menu acts there. column is the
        // visible-column index, row is the row index (the same indices GetGridText's columns/rows use).
        public void SetCurrentCellAddressNow(int column, int row)
        {
            var visibleColumns = VisibleColumns();
            if (column < 0 || column >= visibleColumns.Length)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Column {0} is out of range; the grid has {1} visible columns.", column, visibleColumns.Length));
            if (row < 0 || row >= _dataGridView.Rows.Count)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Row {0} is out of range; the grid has {1} rows.", row, _dataGridView.Rows.Count));
            _dataGridView.CurrentCell = _dataGridView.Rows[row].Cells[visibleColumns[column].Index];
        }

        // A grid's menu is the one for its current cell (move there first with SetCurrentCellAddress), built
        // by raising the cell's CellContextMenuStripNeeded -- not the grid's own ContextMenuStrip.
        public override ContextMenuStrip BuildContextMenu()
        {
            var cell = _dataGridView.CurrentCell;
            if (cell == null)
                throw new ArgumentException(new LlmInstruction(
                    @"The grid has no current cell -- move to one first with set_current_cell_address."));
            var args = new DataGridViewCellContextMenuStripNeededEventArgs(cell.ColumnIndex, cell.RowIndex);
            RaiseProtectedHandler(_dataGridView, @"OnCellContextMenuStripNeeded", args);
            if (args.ContextMenuStrip == null)
                throw new ArgumentException(new LlmInstruction(@"The current cell has no context menu."));
            return OpenContextMenu(args.ContextMenuStrip);
        }

        // The visible columns of the grid in display order -- the columns a column index counts.
        private DataGridViewColumn[] VisibleColumns()
        {
            return _dataGridView.Columns.Cast<DataGridViewColumn>()
                .Where(col => col.Visible).OrderBy(col => col.DisplayIndex).ToArray();
        }

        // The display text of a grid cell: the formatted value the user sees, or empty for a null.
        private static string CellDisplayText(DataGridViewCell cell)
        {
            return (cell.FormattedValue ?? cell.Value)?.ToString() ?? string.Empty;
        }
    }

    /// <summary>A data-bound grid (a <see cref="BoundDataGridView"/>) -- its rows are a BindingListSource,
    /// reached with <c>DataSource as BindingListSource</c>. Reads with the view's rich copy (the same
    /// content as Copy All, cancellable for a large grid) and writes through the bound paste handler (so the
    /// document stays in sync), instead of the base's direct cell access.</summary>
    internal sealed class BoundGridElement : GridElement
    {
        public BoundGridElement(BoundDataGridView boundDataGridView, CancellationToken cancellationToken) : base(boundDataGridView, cancellationToken) { }
        private BindingListSource BindingListSource => DataGridView.DataSource as BindingListSource;

        public override string GetGridText()
        {
            var bindingListSource = BindingListSource;
            return bindingListSource == null
                ? base.GetGridText() // not bound yet -- read the cells directly
                : bindingListSource.ViewContext.GetCopyAllText(DataGridView, bindingListSource);
        }

        protected override void SetGridTextCore(string text)
        {
            var bindingListSource = BindingListSource;
            if (bindingListSource == null)
                base.SetGridTextCore(text);
            else
                // Pastes at the current cell exactly as Ctrl-V would, keeping the bound document in sync
                // (one undoable batch-modify -- see BoundDataGridViewPasteHandler).
                BoundDataGridViewPasteHandler.PasteText(DataGridView, bindingListSource, text);
        }
    }

    /// <summary>The right-click context menu of a control -- addressed by a path whose Type is
    /// "ContextMenu" and whose Parent is that control. Its Children are the menu's top-level items, built
    /// the way a right-click would (so items added on demand are present); drill into a submenu with
    /// get_children on its item, and invoke an item with click. A control's own get_children never returns
    /// its context menu -- you ask for it explicitly with this path. For a grid the menu is the one
    /// for the current cell (move there first with set_current_cell_address).</summary>
    internal sealed class ContextMenuElement : UiElement
    {
        public const string TypeName = "ContextMenu";
        private readonly ControlElement _owner;
        public ContextMenuElement(ControlElement owner) : base(owner.CancellationToken) { _owner = owner; }
        public override string Name => string.Empty;
        public override Type ElementType => typeof(ContextMenuStrip);
        public override bool IsEnabled => _owner.IsEnabled;
        public override IEnumerable<UiElement> EnumerateChildren() =>
            _owner.BuildContextMenu().Items.Cast<ToolStripItem>()
                .Select(item => (UiElement) new ToolStripItemElement(item, _owner.FormElement, CancellationToken));

        internal override IntPtr FormHwnd
        {
            get { return _owner.FormHwnd; }
        }
    }

    /// <summary>A TabControl -- select one of its tabs by the tab's text (select_tab). The tab pages
    /// themselves are not elements; their controls are flattened up to the form, so a control on a tab is
    /// addressed directly (select its tab first to make it visible).</summary>
    internal sealed class TabElement : ControlElement<TabControl>, ISelectTabElement
    {
        public TabElement(TabControl control, CancellationToken cancellationToken) : base(control, cancellationToken) { }
        // The tab contents are flattened to the form, so the TabControl itself has no children.
        public override IEnumerable<UiElement> EnumerateChildren() => Enumerable.Empty<UiElement>();
        public void SelectTabNow(string tabText)
            {
                var tabs = Control.TabPages.Cast<TabPage>().ToList();
                // The tab whose text matches strictly, else loosely (a strict match wins over a loose one).
                var tab = tabs.FirstOrDefault(t => TextMatches(t.Text, tabText, true))
                          ?? tabs.FirstOrDefault(t => TextMatches(t.Text, tabText, false));
                if (tab == null)
                    throw new ArgumentException(LlmInstruction.Format(@"No tab matches '{0}'.", tabText));
                Control.SelectedTab = tab;
            }
    }

    // Small value helpers shared by the value elements.
    internal static class UiValue
    {
        public static bool ParseBool(object value)
        {
            if (value is bool b)
                return b;
            var text = value?.ToString();
            return bool.TryParse(text, out var parsed) ? parsed : text == @"1";
        }

        // Converts any bare CR or LF to CRLF -- the line ending a multi-line TextBox uses for Enter.
        public static string NormalizeNewlines(string value) =>
            value == null ? null : Regex.Replace(value, @"\r\n?|\n", "\r\n");

        // The [column, row] a set_current_cell_address value carries: a two-element integer array. An
        // in-process caller passes new[] { column, row }; over the wire it is the JSON array [column, row]
        // (a JArray, or the string "[column, row]" when sent through a string-valued parameter).
        public static int[] ToColumnRow(object value)
        {
            // A string form "[0, 1]" or "0, 1" (the brackets are optional).
            if (value is string text)
            {
                var parts = text.Trim().Trim('[', ']').Split(',');
                if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out var x) && int.TryParse(parts[1].Trim(), out var y))
                    return new[] { x, y };
            }
            // An array/list (int[], object[], or a Newtonsoft JArray -- all IEnumerable).
            else if (value is System.Collections.IEnumerable sequence)
            {
                var cell = new List<int>();
                foreach (var item in sequence)
                    cell.Add(ToInt(item));
                if (cell.Count == 2)
                    return cell.ToArray();
            }
            throw new ArgumentException(new LlmInstruction(
                @"set_current_cell_address needs a two-element [column, row] array: the cell's visible-column index and its row index."));
        }

        // The int a set_selected_index value carries: a real int (an in-process caller) or its text.
        public static int ToInt(object value)
        {
            if (value is int i)
                return i;
            if (value is long l)
                return (int) l;
            if (int.TryParse(value?.ToString(), out var parsed))
                return parsed;
            throw new ArgumentException(new LlmInstruction(@"This action needs an integer value."));
        }
    }

}
