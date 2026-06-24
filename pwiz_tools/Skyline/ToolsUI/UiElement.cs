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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using SkylineTool;

namespace pwiz.Skyline.ToolsUI
{
    // ---- Capability interfaces ------------------------------------------------------------------
    // The "kind" a UiAction targets: an action applies to an element when the element implements the
    // action's interface (UiAction<T>.AppliesTo is `element is T`), and the action drives the element
    // through that interface's method. A control's element class declares the capabilities it has by
    // implementing these, instead of a per-element switch over an action enum.

    /// <summary>An element a click acts on (a button, a field, a menu item, a native dialog button). The
    /// element marshals its own gesture -- a posted BM_CLICK, a PerformClick on the UI thread, a native
    /// Accept/Cancel -- so Click may be called from the connector's worker thread.</summary>
    public interface IClickableElement { void Click(); }

    /// <summary>An element whose value can be read (a text box, a combo box, a check state, a list of
    /// checked items).</summary>
    public interface IReadValueElement { string GetValue(); }

    /// <summary>An element whose value can be set (a text box, a combo box, a check state, a file name).</summary>
    public interface IWriteValueElement { void SetValue(string value); }

    /// <summary>An element whose items are checked/unchecked by their visible text (a CheckedListBox, a
    /// TreeView, a ListView, the pick-list pop-up).</summary>
    public interface ICheckItemsElement { void SetItemChecked(string item, bool isChecked); }

    /// <summary>An element whose items are selected/unselected by their visible text (a list box, a tree,
    /// a list view).</summary>
    public interface ISelectItemsElement { void SetItemSelected(string item, bool isSelected); }

    /// <summary>A list whose selection can be moved to a given index.</summary>
    public interface ISelectIndexElement { void SetSelectedIndex(int index); }

    /// <summary>A tree whose nodes can be expanded/collapsed by a path (an array of child names/indexes).</summary>
    public interface IExpandCollapseElement { void Expand(object path); void Collapse(object path); }

    /// <summary>A tab control whose tab can be selected by the tab's visible text.</summary>
    public interface ISelectTabElement { void SelectTab(string tabText); }

    // ---- Actions --------------------------------------------------------------------------------

    /// <summary>
    /// One thing the connector can ask a <see cref="UiElement"/> to do (the verbs ClickFormButton,
    /// SetFormValue, the grid verbs, and the generic perform_action all act through these). An action is a
    /// singleton object -- see <see cref="UiActions"/> for the set of them -- that knows its wire
    /// <see cref="SnakeCaseName"/>, the gates the connector must honor before performing it
    /// (<see cref="MustBeVisible"/> / <see cref="MustBeEnabled"/> -- a user could not act on a control they
    /// cannot see or that is disabled), and whether it <see cref="ReturnsValue"/> (a value action is run
    /// synchronously; a void action is posted fire-and-forget). It decides whether it
    /// <see cref="AppliesTo"/> an element (usually: the
    /// element is the right kind -- it implements the action's capability interface) and how to
    /// <see cref="Invoke"/> it (by calling that interface's method). This inverts the old per-element switch
    /// over an action enum: a new control kind declares its capabilities by implementing interfaces, and a
    /// new action is usually just one <see cref="UiActions"/> entry.
    /// </summary>
    public abstract class UiAction
    {
        protected UiAction(string name)
        {
            Name = name;
            SnakeCaseName = UiActions.ToSnakeCase(name);
        }

        /// <summary>The PascalCase action name; <see cref="SnakeCaseName"/> is the snake_case wire form.</summary>
        public string Name { get; }
        public string SnakeCaseName { get; }

        /// <summary>Whether the element must be visible / enabled for the action to be performed -- the gates
        /// a user faces. A pure read (get_value, get_actions, get_children, get_grid_text) clears these, so a
        /// disabled or off-screen control can still be inspected; a mutation or click leaves them set.</summary>
        public bool MustBeVisible { get; internal set; } = true;
        public bool MustBeEnabled { get; internal set; } = true;

        /// <summary>Whether the action produces a result the caller waits for (get_value, get_grid_text,
        /// get_actions, get_children). Drives the threading: a value action is run synchronously, inside the
        /// dialog-watch so it does not hang if a modal is up; a void action (a click, a value set) is posted
        /// fire-and-forget, so a gesture that opens a modal does not block and the modal is driven by later
        /// commands -- the same way clicking a main-menu item already works.</summary>
        public bool ReturnsValue { get; internal set; }

        /// <summary>Whether this action is meaningful for the element -- usually whether the element is the
        /// kind the action targets (it implements the action's capability interface).</summary>
        public abstract bool AppliesTo(UiElement element);

        /// <summary>Whether the action can currently be performed on the element: it applies and the element
        /// passes the visible/enabled gates the action requires.</summary>
        public bool IsEnabled(UiElement element) =>
            AppliesTo(element)
            && (!MustBeVisible || element.IsVisible)
            && (!MustBeEnabled || element.IsEnabled);

        /// <summary>Performs the action on the element (the action determines the argument and result types).
        /// An action that needs the request's cancellation token (a large grid read) gets it from the
        /// element -- every UiElement reaches its form's token through its FormElement -- not a parameter.</summary>
        public abstract object Invoke(UiElement element, object argument);
    }

    /// <summary>
    /// The set of <see cref="UiAction"/> singletons (these replace the former enum members) and the lookup
    /// over them. Every action's Invoke is just a method call on a capability interface, so they are all
    /// built from the generic <see cref="SimpleActionImpl{T,TArg}"/> -- no per-action class.
    /// </summary>
    public static class UiActions
    {
        // PascalCase -> snake_case ("GetActions" -> "get_actions").
        public static string ToSnakeCase(string name)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(name[i]));
            }

            return sb.ToString();
        }

        /// <summary>An action that targets element kind <typeparamref name="T"/> (a capability interface or
        /// an element type) and whose Invoke is a single call <c>func(element, argument)</c> -- so an action
        /// needs no class of its own, just an entry below. The argument is handed to the func as
        /// <typeparamref name="TArg"/> (the JSON value is already a string or the raw object; an action that
        /// wants an int or a cell parses it inside its func). <paramref name="mustBeEnabled"/> is false for a
        /// pure read (it may also be inspected off-screen), which also passes <c>returnsValue: true</c> so the
        /// dispatch waits for its result instead of posting it fire-and-forget.</summary>
        private sealed class SimpleActionImpl<T, TArg> : UiAction
        {
            private readonly Func<T, TArg, object> _invoke;

            public SimpleActionImpl(string name, Func<T, TArg, object> invoke, bool mustBeEnabled = true) : base(name)
            {
                _invoke = invoke;
                // Visible and enabled gates move together: a read clears both, a mutation/click leaves both set.
                MustBeEnabled = mustBeEnabled;
                MustBeVisible = mustBeEnabled;
            }

            public override bool AppliesTo(UiElement element) => element is T;

            public override object Invoke(UiElement element, object argument)
            {
                if (!(element is T typed))
                    throw new ArgumentException(LlmInstruction.Format(
                        @"The action '{0}' does not apply to this control.", SnakeCaseName));
                return _invoke(typed, argument is TArg arg ? arg : default(TArg));
            }
        }

        // get_actions / get_children apply to every element (not one capability kind), so they target the
        // base UiElement type; they are reads (mustBeEnabled: false, returnsValue: true).
        public static readonly UiAction GetActions = SimpleAction<UiElement>(
            @"GetActions", e => e.SupportedActions.Select(a => a.SnakeCaseName).ToArray(),
            mustBeEnabled: false, returnsValue: true);

        public static readonly UiAction GetChildren = SimpleAction<UiElement>(
            @"GetChildren", e => e.GetChildren(), mustBeEnabled: false, returnsValue: true);

        // A void action (click, value set, item check, ...) is posted fire-and-forget; only a value action
        // (get_value, get_grid_text) is run synchronously inside the dialog-watch -- see UiAction.ReturnsValue.
        public static readonly UiAction Click = SimpleAction<IClickableElement>(
            @"Click", e =>
            {
                e.Click();
                return null;
            });

        public static readonly UiAction GetValue = SimpleAction<IReadValueElement>(
            @"GetValue", e => e.GetValue(), mustBeEnabled: false, returnsValue: true);

        public static readonly UiAction SetValue = SimpleAction<IWriteValueElement, string>(
            @"SetValue", (e, value) =>
            {
                e.SetValue(value);
                return null;
            });

        public static readonly UiAction CheckItem = SimpleAction<ICheckItemsElement, string>(
            @"CheckItem", (e, item) =>
            {
                e.SetItemChecked(item, true);
                return null;
            });

        public static readonly UiAction UncheckItem = SimpleAction<ICheckItemsElement, string>(
            @"UncheckItem", (e, item) =>
            {
                e.SetItemChecked(item, false);
                return null;
            });

        public static readonly UiAction SelectItem = SimpleAction<ISelectItemsElement, string>(
            @"SelectItem", (e, item) =>
            {
                e.SetItemSelected(item, true);
                return null;
            });

        public static readonly UiAction UnselectItem = SimpleAction<ISelectItemsElement, string>(
            @"UnselectItem", (e, item) =>
            {
                e.SetItemSelected(item, false);
                return null;
            });

        public static readonly UiAction SetSelectedIndex = SimpleAction<ISelectIndexElement, object>(
            @"SetSelectedIndex", (e, arg) =>
            {
                e.SetSelectedIndex(UiValue.ToInt(arg));
                return null;
            });

        public static readonly UiAction GetGridText = SimpleAction<GridElement>(
            @"GetGridText", e => e.GetGridText(), mustBeEnabled: false, returnsValue: true);

        public static readonly UiAction SetGridText = SimpleAction<GridElement, string>(
            @"SetGridText", (e, text) =>
            {
                e.SetGridText(text);
                return null;
            });

        public static readonly UiAction SetCurrentCellAddress = SimpleAction<GridElement, object>(
            @"SetCurrentCellAddress", (e, arg) =>
            {
                var cell = UiValue.ToColumnRow(arg);
                e.SetCurrentCellAddress(cell[0], cell[1]);
                return null;
            });

        public static readonly UiAction Expand = SimpleAction<IExpandCollapseElement, object>(
            @"Expand", (e, path) =>
            {
                e.Expand(path);
                return null;
            });

        public static readonly UiAction Collapse = SimpleAction<IExpandCollapseElement, object>(
            @"Collapse", (e, path) =>
            {
                e.Collapse(path);
                return null;
            });

        public static readonly UiAction SelectTab = SimpleAction<ISelectTabElement, string>(
            @"SelectTab", (e, tab) =>
            {
                e.SelectTab(tab);
                return null;
            });

        // Every action, in get_actions / get_children listing order (the universal ones first).
        public static readonly UiAction[] AllActions =
        {
            GetActions, GetChildren, Click, GetValue, SetValue, CheckItem, UncheckItem, SelectItem,
            UnselectItem, SetSelectedIndex, GetGridText, SetGridText, SetCurrentCellAddress, Expand,
            Collapse, SelectTab
        };

        // The action with the given wire name, matched case- and underscore-insensitively, or null.
        public static UiAction ByName(string name)
        {
            var normalized = (name ?? string.Empty).Replace(@"_", string.Empty).Trim();
            return AllActions.FirstOrDefault(action =>
                string.Equals(action.Name, normalized, StringComparison.OrdinalIgnoreCase));
        }

        public static UiAction SimpleAction<T>(string name, Func<T, object> action, bool mustBeEnabled = true, bool returnsValue = false)
        {
            return SimpleAction<T, object>(name, (element, arg) => action(element), mustBeEnabled, returnsValue);
        }

        public static UiAction SimpleAction<T, TArg>(string name, Func<T, TArg, object> action,
            bool mustBeEnabled = true, bool returnsValue = false)
        {
            return new SimpleActionImpl<T, TArg>(name, action, mustBeEnabled) { ReturnsValue = returnsValue };
        }
}

    /// <summary>
    /// A connector-facing view of one UI element on a form. Subclasses wrap a specific kind of control
    /// (or a ToolStrip item) and declare the actions they support by implementing the matching capability
    /// interfaces (<see cref="IClickableElement"/>, <see cref="IWriteValueElement"/>, ...); each
    /// <see cref="UiAction"/> targets one of those interfaces, so the verbs act polymorphically instead of
    /// switching on WinForms types. Each element also knows its own <see cref="Label"/> (the visible text
    /// that identifies it) and its <see cref="Children"/>, so matching and form enumeration are a single
    /// recursive walk.
    ///
    /// Most elements wrap a WinForms <see cref="Control"/> (see <see cref="ControlElement"/>), but the base
    /// is public and control-agnostic so a non-WinForms surface can present itself the same way -- a native
    /// dialog (<see cref="NativeDialog"/>) is itself a UiElement whose children dispatch to UI
    /// Automation rather than to a Control.
    /// </summary>
    public abstract class UiElement
    {
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
        public abstract bool IsVisible { get; }

        /// <summary>The element's current value, for a value control (else null) -- informational.</summary>
        public virtual string Value => null;

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
                return FindElementOrNull(text, action)
                    ?? throw new ArgumentException(LlmInstruction.Format(
                        @"No control matching '{0}' supports the action '{1}'. Use skyline_get_controls to list the controls.",
                        text, action.SnakeCaseName));
            // Empty text means "the single element that supports the action".
            var candidates = SelfAndDescendants().Where(action.AppliesTo).ToList();
            if (candidates.Count == 0)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Nothing here supports the action '{0}'.", action.SnakeCaseName));
            if (candidates.Count == 1)
                return candidates[0];
            // Several support it: take the single interactable (visible + enabled) one, so a hidden sibling
            // never makes "the form's single grid/control" ambiguous -- e.g. the Document Grid's
            // DataboundGridControl hosts a bound grid AND a collapsed replicate-pivot grid; only the bound one
            // is visible. If more than one is still interactable, the caller must name one.
            var interactable = candidates.Where(IsInteractable).ToList();
            if (interactable.Count == 1)
                return interactable[0];
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

        // The best of the candidates whose text matches at the given strictness: prefer an interactable
        // (visible + enabled) one so a hidden duplicate never shadows the control the user sees. Returns
        // null when none matches at this strictness (the caller then falls back to a looser match).
        private static UiElement BestMatch(IEnumerable<UiElement> candidates, string text, bool strict)
        {
            UiElement best = null;
            foreach (var element in candidates)
            {
                if (!element.MatchesText(text, strict))
                    continue;
                if (best == null || (!IsInteractable(best) && IsInteractable(element)))
                    best = element;
            }
            return best;
        }

        private static bool IsInteractable(UiElement element) => element.IsVisible && element.IsEnabled;

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
                return string.Equals(candidate, key, StringComparison.Ordinal);
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
            return Children.Where(child => child.MatchesType(typeName));
        }

        /// <summary>The elements nested inside this one, for the recursive form walk.</summary>
        public virtual IEnumerable<UiElement> Children => Enumerable.Empty<UiElement>();

        /// <summary>This element and every element under it, depth-first.</summary>
        public IEnumerable<UiElement> SelfAndDescendants()
        {
            yield return this;
            foreach (var child in Children)
            foreach (var descendant in child.SelfAndDescendants())
                yield return descendant;
        }

        /// <summary>The actions this element supports, for discovery via GetActions / GetControls: every
        /// action that <see cref="UiAction.AppliesTo"/> this element (it is the kind the action targets).
        /// The capability is declared by the interfaces the element implements, not by an override here.</summary>
        public IEnumerable<UiAction> SupportedActions =>
            UiActions.AllActions.Where(action => action.AppliesTo(this));

        /// <summary>This element's children as <see cref="ControlInfo"/>, each with a parentless,
        /// single-segment Path: the caller re-parents it (onto the path of the element it listed) before
        /// acting. The form walk is one level at a time -- GetControls and the get_children action both
        /// return this; descend by calling GetChildren on a child. Each child's Index is its position among
        /// the siblings of its same Type, so adding a control of another Type never shifts it.</summary>
        public virtual ControlInfo[] GetChildren()
        {
            var indexByType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var result = new List<ControlInfo>();
            foreach (var child in Children)
            {
                indexByType.TryGetValue(child.ElementType.Name, out var typeIndex);
                indexByType[child.ElementType.Name] = typeIndex + 1;
                result.Add(new ControlInfo
                {
                    Path = child.PathSegment(typeIndex),
                    Name = JsonUiService.NullIfEmpty(child.Name),
                    Enabled = child.IsEnabled,
                    Visible = child.IsVisible,
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
                candidates = Children.ToList();
            }

            // An Index pins the index-th child of that Type; Text, if also set, must match it.
            if (path.Index.HasValue)
            {
                if (path.Index < 0 || path.Index >= candidates.Count)
                    throw new ArgumentException(LlmInstruction.Format(
                        @"No {0} at index {1}; the parent has {2} of that type.", path.Type, path.Index.Value, candidates.Count));
                var indexed = candidates[path.Index.Value];
                if (path.Text != null && !indexed.MatchesText(path.Text, false))
                    throw new ArgumentException(LlmInstruction.Format(
                        @"The {0} at index {1} does not match the Text '{2}' in the path.", path.Type, path.Index.Value, path.Text));
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
            return match ?? throw new ArgumentException(new LlmInstruction(
                @"No control found matching the path. Use skyline_get_controls to list the controls."));
        }

        // The first interactable (visible + enabled) element, or -- when none is interactable -- the first
        // one (so a legitimately disabled target still resolves). Null only for an empty sequence. Lets a
        // Type/Text match prefer the control the user sees over a hidden duplicate.
        private static UiElement PreferInteractable(IEnumerable<UiElement> elements)
        {
            UiElement first = null;
            foreach (var element in elements)
            {
                if (IsInteractable(element))
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
        /// The caller re-parents it onto the element it listed.</summary>
        internal UiElementPath PathSegment(int typeIndex) =>
            new UiElementPath(null, JsonUiService.NullIfEmpty(JsonUiService.CleanLabel(Label)), typeIndex, ElementType.Name);
    }

    // ---- Control-backed elements ----------------------------------------------------------------

    /// <summary>Base for an element backed by a WinForms <see cref="Control"/>. Every control is clickable
    /// (see <see cref="Click"/>); a subclass adds value/list/grid capabilities by implementing the matching
    /// capability interface.</summary>
    internal abstract class ControlElement : UiElement, IClickableElement
    {
        protected ControlElement(Control control) { Control = control; }

        public Control Control { get; }

        /// <summary>The form this control belongs to -- the root of the element tree it was built in. Set
        /// once by <see cref="FormElement.ElementFor"/> when the element is created (the FormElement sets its
        /// own to itself). Every control reaches its form's <see cref="CancellationToken"/> through it.</summary>
        public FormElement FormElement { get; internal set; }

        /// <summary>The token that fires when the connector client disconnects, so a long read (a large grid
        /// copy) is abandoned. It belongs to the <see cref="FormElement"/>; every control shares it.</summary>
        public virtual CancellationToken CancellationToken => FormElement.CancellationToken;

        public override string Name => Control.Name;
        public override Type ElementType => Control.GetType();

        public override bool IsEnabled
        {
            get
            {
                if (!IsVisible)
                {
                    return false;
                }
                if (true != Control.FindForm()?.Enabled)
                {
                    return false;
                }

                return Control.Enabled;
            }
        }
        public override bool IsVisible => Control.Visible;

        // Most controls have no children -- a button, a text box, a list. Only a ContainerElement (a Form
        // or UserControl) owns children; the inherited (empty) Children is correct for everything else.

        // A control is clicked through its IButtonControl handler -- a real button, or a custom tile such as
        // a StartPage action box or recent-file row, whose clickable surface is covered by child labels and
        // an icon; PerformClick is reliable regardless of those children. A control that is not an
        // IButtonControl has no programmatic click (clicking it would have to synthesize a mouse gesture at a
        // guessed point, which is fragile), so it reports that it cannot be clicked. A subclass with a more
        // direct gesture overrides Click: a button uses BM_CLICK (to bypass PerformClick's gates), a
        // ToolStripItem / native dialog button drives its own.
        public virtual void Click()
        {
            if (!(Control is IButtonControl button))
                throw new ArgumentException(LlmInstruction.Format(
                    @"The control '{0}' cannot be clicked: it is not a button. Set its value, or act on the button/menu item that triggers it.",
                    Label ?? JsonUiService.NullIfEmpty(Name) ?? ElementType.Name));
            JsonUiService.InvokeOnUiThread(button.PerformClick);
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
                return previous is System.Windows.Forms.Label label ? label.Text : null;
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
        /// control's own <see cref="System.Windows.Forms.Control.ContextMenuStrip"/> (a graph builds a fresh
        /// menu); a grid and the Targets tree, whose menus are built/owned elsewhere, override this. Runs on
        /// the UI thread.</summary>
        public virtual ContextMenuStrip BuildContextMenu()
        {
            // A graph (this control is a ZedGraphControl, or a graph form is just its graph) builds a fresh menu.
            var graphMenu = JsonUiService.TryBuildGraphContextMenu(Control);
            if (graphMenu != null)
                return graphMenu;
            // Otherwise the control's own ContextMenuStrip, with its Opening raised so on-demand items appear.
            if (Control.ContextMenuStrip != null)
                return JsonUiService.OpenContextMenu(Control.ContextMenuStrip);
            throw new ArgumentException(LlmInstruction.Format(
                @"{0} has no context menu.", Label ?? JsonUiService.NullIfEmpty(Name) ?? ElementType.Name));
        }
    }

    /// <summary>Base for an element backed by a strongly-typed control, so subclasses use the control as
    /// its real type (e.g. a CheckedListBox) without casting.</summary>
    internal abstract class ControlElement<T> : ControlElement where T : Control
    {
        protected ControlElement(T control) : base(control) { }
        public new T Control => (T) base.Control;
    }

    /// <summary>A top-level window the connector addresses by a formId: a WinForms form
    /// (<see cref="FormElement"/>) or a native common dialog (<see cref="NativeDialog"/>). JsonUiService
    /// resolves a formId to one of these and drives it entirely through this interface, so no verb
    /// special-cases a native dialog. Each implementation runs its work in its own thread context: a managed
    /// form marshals to the UI thread and can watch for a dialog the work pops; a native dialog -- whose UI
    /// thread is busy in its own modal loop -- runs on the calling (pipe) thread and cannot be watched.</summary>
    internal interface IFormElement
    {
        /// <summary>The "TypeName:Title" id this form is addressed by (matches skyline_get_open_forms).</summary>
        string FormId { get; }
        /// <summary>The form's visible title, for naming a captured-image file.</summary>
        string Title { get; }
        /// <summary>The form's controls as parentless ControlInfo (the get_controls verb).</summary>
        ControlInfo[] GetControls();
        /// <summary>Clicks a control on the form by its visible label, or accepts/cancels a native dialog.</summary>
        void ClickButton(string button);
        /// <summary>Sets a control's value (or a grid cell, or a native dialog's file name).</summary>
        void SetValue(string controlId, string value);
        /// <summary>Closes (a form) or cancels (a native dialog).</summary>
        void Close();
        /// <summary>Resolves the path against this form and performs the action in the form's thread context.
        /// The action gets its cancellation token from the resolved element (its FormElement), not a parameter.</summary>
        object PerformAction(UiElementPath path, UiAction action, object value);
        /// <summary>Captures the form's image to a bitmap the caller disposes (no permission/format checks --
        /// the caller has done the screen-capture pre-flight).</summary>
        System.Drawing.Bitmap CaptureImage();
    }

    /// <summary>A Form or a UserControl -- a boundary that owns its (flattened) children. It has no action
    /// of its own; it exists so the walk can list and descend into the controls it contains. Every other
    /// container (Panel, GroupBox, ...) is transparent (its controls are pulled up to the nearest Form or
    /// UserControl), so the only things that need a container element are these two. A Form is a
    /// <see cref="FormElement"/> (a container that is also an addressable <see cref="IFormElement"/>).</summary>
    internal class ContainerElement : ControlElement
    {
        public ContainerElement(Control control) : base(control) { }

        public override IEnumerable<UiElement> Children => FlattenChildren(Control);

        // This container's child elements for the form walk, FLATTENED. A control the form recognizes as an
        // element (FormElement.ElementFor) is yielded as that element -- and tagged with the same FormElement,
        // so it shares the form's cancellation token: a UserControl as a ContainerElement that owns its own
        // (likewise flattened) children, and a grid/list/tree/toolstrip as a leaf the caller walks into via
        // its own children. A control ElementFor does not recognize (a Panel, GroupBox, SplitContainer, a
        // TabPage, ...) is transparent -- its controls are pulled up so every control is a direct child of
        // the form (or of the nearest UserControl). A TabControl is kept (so its tabs can be selected via
        // select_tab) and its tab contents are flattened up to this level too. Recognizing the kind (rather
        // than guessing from Control.Count) keeps a complex control with internal child controls -- a
        // DataGridView, with its scroll bars -- a single element instead of dissolving it into its parts.
        private IEnumerable<UiElement> FlattenChildren(Control container)
        {
            foreach (Control control in container.Controls)
            {
                var element = FormElement.ElementFor(control);
                if (element != null)
                    yield return element;
                // Recurse through a transparent container (no element of its own), and through a TabControl
                // (kept above) to flatten its tab contents up alongside it.
                if (element == null || control is TabControl)
                    foreach (var inner in FlattenChildren(control))
                        yield return inner;
            }
        }
    }

    /// <summary>A WinForms top-level form, addressed by a formId. It is a <see cref="ContainerElement"/> (it
    /// owns the form's flattened controls) that also implements <see cref="IFormElement"/>: the verbs resolve
    /// a managed formId to this and call its methods, which marshal to the UI thread (and watch for a dialog
    /// a mutation pops) so the connector drives a form the same way whether or not it is native. It carries
    /// the request's <see cref="CancellationToken"/> and is the factory (<see cref="ElementFor"/>) for the
    /// elements in its tree, tagging each with itself so every control can reach that token.</summary>
    internal sealed class FormElement : ContainerElement, IFormElement
    {
        private readonly CancellationToken _cancellationToken;
        public FormElement(Form form, CancellationToken cancellationToken) : base(form)
        {
            _cancellationToken = cancellationToken;
            FormElement = this;
        }
        internal Form Form => (Form) Control;
        public override CancellationToken CancellationToken => _cancellationToken;

        /// <summary>Builds the <see cref="UiElement"/> for a control in this form's tree, choosing the
        /// subclass by the control's kind and tagging it with this form (so it shares the cancellation
        /// token). Returns null for a control that is not an element (a label, a spacer, a transparent
        /// panel) -- the caller treats it as transparent and recurses into it.</summary>
        public UiElement ElementFor(Control control)
        {
            var element = CreateElement(control);
            if (element != null)
                element.FormElement = this;
            return element;
        }

        private ControlElement CreateElement(Control control)
        {
            switch (control)
            {
                case CheckBox checkBox: return new CheckBoxElement(checkBox);
                case RadioButton radioButton: return new RadioButtonElement(radioButton);
                case ButtonBase button: return new ButtonElement(button);
                case ComboBox comboBox: return new ComboBoxElement(comboBox);
                case TextBoxBase textBox: return new TextBoxElement(textBox);
                case TabControl tabControl: return new TabElement(tabControl);
                // CheckedListBox before ListBox -- it derives from ListBox, so its case must win.
                case CheckedListBox checkedListBox: return new CheckedListBoxElement(checkedListBox);
                // The Pick Children pop-up's owner-drawn ListBox presents as a CheckedListBox.
                case ListBox listBox when listBox.FindForm() is PopupPickList: return new PopupPickListElement(listBox);
                case ListBox listBox: return new ListControlElement<ListBox>(listBox);
                case TreeView treeView: return new TreeViewElement(treeView);
                case ListView listView: return new ItemContainerElement<ListView>(listView);
                // The grid itself -- the inner grid of a DataboundGridControl (a BoundDataGridView, driven
                // through its rich copy/paste path) or a standalone DataGridView (direct cell access). The
                // DataboundGridControl is a UserControl you walk into to reach this grid and its nav bar.
                case BoundDataGridView boundDataGridView: return new BoundGridElement(boundDataGridView);
                case DataGridView dataGridView: return new GridElement(dataGridView);
                case ToolStrip toolStrip: return new ToolStripElement(toolStrip);
                // A UserControl (including a DataboundGridControl) is a boundary that owns its (flattened)
                // children; everything else that contains controls is transparent. A nested Form (rare as a
                // child) is treated as a plain container under this form's token.
                case UserControl userControl: return new ContainerElement(userControl);
                case Form form: return new ContainerElement(form);
                default:
                    // A custom clickable that is not a ButtonBase (e.g. a StartPage tile).
                    if (control is IButtonControl)
                        return new ClickableControlElement(control);
                    // Anything else reaching here is a leaf with no capability (a label, a spacer, an empty
                    // panel); it is not something a caller can act on, so it is not an element.
                    return null;
            }
        }

        public string FormId => JsonUiService.GetFormId(Form);
        public string Title => JsonUiService.GetFormTitle(Form);

        // A value read: run synchronously inside the dialog-watch so it does not hang if a modal is up.
        public ControlInfo[] GetControls()
        {
            ControlInfo[] result = null;
            JsonUiService.RunWithDialogWatch(() =>
            {
                result = JsonUiService.InvokeOnUiThread(GetChildren);
                return true;
            });
            return result;
        }

        // Clicks a button (or any clickable) on the form by its caption. Resolve + verify synchronously on
        // the UI thread (so a control not found / disabled fails here), then post the click fire-and-forget
        // so a click that opens a modal does not block; the modal is driven by later commands. Each element
        // knows how to click itself (a button via BM_CLICK so an AutoCheck=false checkbox still toggles; a
        // menu/toolbar item or tile via PerformClick; a tab by selecting it).
        public void ClickButton(string button)
        {
            var element = JsonUiService.InvokeOnUiThread(() =>
            {
                JsonUiService.VerifyFormInteractable(Form);
                var clickable = FindElement(button, UiActions.Click);
                JsonUiService.VerifyInteractable(clickable);
                return clickable;
            });
            JsonUiService.BeginInvokeOnUiThread(() => UiActions.Click.Invoke(element, null));
        }

        // Sets a control's value (or a grid cell) on the form. The control is resolved + gated synchronously
        // on the UI thread (so a control not found / disabled fails here), then the value is applied
        // fire-and-forget -- a void action, so a setter that pops a validation or confirmation alert does not
        // block; the alert becomes a form the caller drives next.
        public void SetValue(string controlId, string value)
        {
            var apply = JsonUiService.InvokeOnUiThread(() =>
            {
                // controlId can name a grid cell ("grid[column,row]") -- set that cell's value: move the
                // current cell there and paste, reusing the grid path so a DataboundGridControl stays in
                // sync. The grid is resolved through the same finder as every other control.
                if (TryParseGridCell(controlId, out var gridName, out var column, out var row))
                {
                    var gridElement = FindGrid(gridName);
                    JsonUiService.VerifyInteractable(gridElement);
                    return (Action) (() =>
                    {
                        gridElement.SetCurrentCellAddress(column, row);
                        gridElement.SetGridText(value);
                    });
                }
                // A field is matched by its own Label (its caption, or the label that names a caption-less
                // box -- see UiElement.Label), so a label never has to be matched and resolved to its field.
                var element = FindElement(controlId, UiActions.SetValue);
                JsonUiService.VerifyInteractable(element);
                return (Action) (() => UiActions.SetValue.Invoke(element, value));
            });
            JsonUiService.BeginInvokeOnUiThread(apply);
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

        // Closing is a void action -- post it fire-and-forget so a "save changes?" confirmation it raises
        // becomes a form the caller drives next rather than blocking.
        public void Close() => JsonUiService.BeginInvokeOnUiThread(() => Form.Close());

        public object PerformAction(UiElementPath path, UiAction action, object value)
        {
            // Resolve + verify on the UI thread (a control's gates read window handles), then run the action
            // with the thread/dialog policy it declares.
            var element = JsonUiService.InvokeOnUiThread(() =>
                JsonUiService.RequireAction(JsonUiService.ResolvePathFrom(path, this), action));
            return JsonUiService.ExecuteAction(action, element, value);
        }

        // Activates the form and captures it (redacting any sensitive regions), as a right-click "capture
        // screenshot" would. Runs on the UI thread.
        public System.Drawing.Bitmap CaptureImage() => JsonUiService.InvokeOnUiThread(() =>
        {
            ScreenCapture.ActivateForm(Form);
            return ScreenCapture.CaptureAndRedact(ScreenCapture.GetWindowRectangle(Form), Form);
        });

        /// <summary>Invokes a menu item on this form's main menu by its '>'-separated path (e.g. "File &gt;
        /// Import &gt; Peptide Search"). The path is resolved through the element model -- the menu is a
        /// <see cref="ToolStripElement"/> over <see cref="System.Windows.Forms.Form.MainMenuStrip"/>, and each
        /// segment is found by <see cref="FindElement"/> matching its visible text -- opening each level's
        /// dropdown so items built on demand are present, then the leaf's click is posted fire-and-forget so a
        /// menu item that opens a modal does not block.</summary>
        public void InvokeMenuItem(string menuPath)
        {
            if (Form.MainMenuStrip == null)
                throw new ArgumentException(new LlmInstruction(@"This form has no main menu."));
            if (!new ToolStripElement(Form.MainMenuStrip) { FormElement = this }.ClickMenuItem(menuPath))
                throw new ArgumentException(LlmInstruction.Format(@"Menu item not found: {0}.", menuPath));
        }
    }

    /// <summary>A push button (or any ButtonBase) -- clicked with BM_CLICK, which fires the Click handler
    /// like a real mouse click (even for an AutoCheck=false checkbox) and bypasses PerformClick's gates.</summary>
    internal class ButtonElement : ControlElement
    {
        public ButtonElement(Control control) : base(control) { }
        public override string Label => Control.Text;
        // BM_CLICK is POSTED (not sent) cross-thread: like a real user click it returns to the message
        // loop at once. A click that opens a modal dialog must not block the worker thread until that
        // modal closes -- SendMessage would, because the button's WndProc runs the modal loop before it
        // returns, which pins the worker and wedges the single-threaded JsonTool server behind the modal.
        // The main thread runs the posted click when it next pumps; the resulting dialog is then driven by
        // later commands, exactly like the asynchronous main-menu path.
        public override void Click() =>
            User32.PostMessageA(Control.Handle, User32.WinMessageType.BM_CLICK, 0, 0);
    }

    /// <summary>A checkbox: clickable (toggles via its handler -- from ButtonElement) and value-settable
    /// (sets the checked state).</summary>
    internal sealed class CheckBoxElement : ButtonElement, IReadValueElement, IWriteValueElement
    {
        private readonly CheckBox _checkBox;
        public CheckBoxElement(CheckBox checkBox) : base(checkBox) { _checkBox = checkBox; }
        public override string Value => _checkBox.Checked.ToString();
        public string GetValue() => Value;
        public void SetValue(string value) => _checkBox.Checked = UiValue.ParseBool(value);
    }

    /// <summary>A radio button: clicking/setting it checks it (WinForms unchecks its siblings).</summary>
    internal sealed class RadioButtonElement : ButtonElement, IReadValueElement, IWriteValueElement
    {
        private readonly RadioButton _radioButton;
        public RadioButtonElement(RadioButton radioButton) : base(radioButton) { _radioButton = radioButton; }
        public override string Value => _radioButton.Checked.ToString();
        public string GetValue() => Value;
        public void SetValue(string value) => _radioButton.Checked = UiValue.ParseBool(value);
    }

    /// <summary>A text box -- a caption-less field named by its adjacent label.</summary>
    internal sealed class TextBoxElement : ControlElement, IReadValueElement, IWriteValueElement
    {
        private readonly TextBoxBase _textBox;
        public TextBoxElement(TextBoxBase textBox) : base(textBox) { _textBox = textBox; }
        public override string Value => _textBox.Text;
        public string GetValue() => _textBox.Text;
        // A multi-line box parses/lays out on CRLF (what Enter inserts), so normalize bare newlines.
        public void SetValue(string value) =>
            _textBox.Text = _textBox.Multiline ? UiValue.NormalizeNewlines(value) : value;
    }

    /// <summary>A combo box -- value set by selecting the matching item.</summary>
    internal sealed class ComboBoxElement : ControlElement, IReadValueElement, IWriteValueElement
    {
        private readonly ComboBox _comboBox;
        public ComboBoxElement(ComboBox comboBox) : base(comboBox) { _comboBox = comboBox; }
        public override string Value => _comboBox.GetItemText(_comboBox.SelectedItem);
        public string GetValue() => Value;
        public void SetValue(string value)
        {
            int index = _comboBox.FindStringExact(value);
            if (index < 0)
                throw new ArgumentException(LlmInstruction.Format(
                    @"No item '{0}' in combo box {1}.", value, _comboBox.Name));
            _comboBox.SelectedIndex = index;
        }
    }

    /// <summary>A ListControl -- a ListBox or CheckedListBox. Select an item by index
    /// (set_selected_index) or by its text (select_item / unselect_item).</summary>
    internal class ListControlElement<T> : ControlElement<T>, ISelectIndexElement, ISelectItemsElement
        where T : ListControl
    {
        public ListControlElement(T control) : base(control) { }
        public void SetSelectedIndex(int index) => Control.SelectedIndex = index;
        public void SetItemSelected(string item, bool isSelected) =>
            ListItems.SetSelected(Control, item, isSelected);
    }

    /// <summary>A CheckedListBox. Besides the ListControl actions, an item is checked/unchecked by its text
    /// (check_item / uncheck_item) or toggled the way a user does it -- set_selected_index to the item, then
    /// click, which toggles the selected item's check. Its value is the checked items' text, one per line.</summary>
    internal sealed class CheckedListBoxElement : ListControlElement<CheckedListBox>,
        ICheckItemsElement, IReadValueElement
    {
        public CheckedListBoxElement(CheckedListBox control) : base(control) { }
        public override string Value =>
            string.Join(Environment.NewLine, Control.CheckedItems.Cast<object>().Select(Control.GetItemText));
        public string GetValue() => Value;
        public void SetItemChecked(string item, bool isChecked) =>
            ListItems.SetChecked(Control, item, isChecked);
        // A click toggles the checked state of the selected item, the way a user's click/space does (move to
        // the item first with set_selected_index). The click runs off the UI thread inside the dialog-watch,
        // so marshal the toggle.
        public override void Click() =>
            JsonUiService.InvokeOnUiThread(() =>
            {
                int index = Control.SelectedIndex;
                if (index < 0)
                    throw new ArgumentException(new LlmInstruction(
                        @"No item is selected -- choose one first with set_selected_index."));
                Control.SetItemChecked(index, !Control.GetItemChecked(index));
            });
    }

    /// <summary>A control whose items are checked or selected by their text -- a TreeView (a node by a
    /// '>'-separated path) or a ListView. Not a ListControl (it has no SelectedIndex); a caption-less one
    /// is reached through a path of its Type. The value is the item.</summary>
    internal class ItemContainerElement<T> : ControlElement<T>, ICheckItemsElement, ISelectItemsElement
        where T : Control
    {
        public ItemContainerElement(T control) : base(control) { }
        public void SetItemChecked(string item, bool isChecked) =>
            ListItems.SetChecked(Control, item, isChecked);
        public void SetItemSelected(string item, bool isSelected) =>
            ListItems.SetSelected(Control, item, isSelected);
    }

    /// <summary>A TreeView. Besides checking/selecting a node by text, a node is expanded or collapsed
    /// (expand/collapse) by a path: an array whose segments select a child at each level -- a string is the
    /// first child whose text matches it, an integer is the child at that index.</summary>
    internal sealed class TreeViewElement : ItemContainerElement<TreeView>, IExpandCollapseElement
    {
        public TreeViewElement(TreeView control) : base(control) { }
        public void Expand(object path) => ResolveTreePath(path).Expand();
        public void Collapse(object path) => ResolveTreePath(path).Collapse();

        // The Targets tree's node menu is shown manually, so it lives on the main window rather than on the
        // tree's own ContextMenuStrip; raise its Opening so item enablement reflects the current selection
        // (select the node first). Any other TreeView falls back to the default.
        public override ContextMenuStrip BuildContextMenu() =>
            Control is SequenceTree
                ? JsonUiService.OpenContextMenu(Program.MainWindow.ContextMenuTreeNode)
                : base.BuildContextMenu();

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

    /// <summary>The owner-drawn ListBox on the Pick Children pop-up. It is a plain ListBox whose checkbox is
    /// painted from PickListChoice.Chosen on the PopupPickList -- not a real CheckedListBox check -- so a
    /// check routes through PopupPickList.SetItemChecked. To the connector it presents exactly as a
    /// CheckedListBox (check_item/uncheck_item, click toggles the selected item, get_value reads the checked
    /// items, plus the ListControl select actions). When PopupPickList is reworked to host a real
    /// CheckedListBox, this special case can go away.</summary>
    internal sealed class PopupPickListElement : ListControlElement<ListBox>,
        ICheckItemsElement, IReadValueElement
    {
        public PopupPickListElement(ListBox control) : base(control) { }
        private PopupPickList PickList => (PopupPickList) Control.FindForm();
        public override Type ElementType => typeof(CheckedListBox);
        public override string Value
        {
            get
            {
                var pickList = PickList;
                var names = pickList.ItemNames.ToList();
                return string.Join(Environment.NewLine,
                    Enumerable.Range(0, names.Count).Where(pickList.GetItemChecked).Select(i => names[i]));
            }
        }
        public string GetValue() => Value;
        public void SetItemChecked(string item, bool isChecked) =>
            PickList.SetItemChecked(FindPickListIndex(item), isChecked);
        // A click toggles the selected item's check, like a user's click/space (move to the item first with
        // set_selected_index). Runs off the UI thread inside the dialog-watch.
        public override void Click() =>
            JsonUiService.InvokeOnUiThread(() =>
            {
                int index = Control.SelectedIndex;
                if (index < 0)
                    throw new ArgumentException(new LlmInstruction(
                        @"No item is selected -- choose one first with set_selected_index."));
                PickList.ToggleItem(index);
            });

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
        internal static int BestMatch(int count, Func<int, string> textOf, string key)
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
        public ClickableControlElement(Control control) : base(control) { }
        public override string Label => Control.Text;
    }

    /// <summary>A ToolStrip (menu strip / toolbar) -- its items are its children. It owns the logic for
    /// matching a '>'-separated menu/toolbar path to an item: <see cref="ResolveMenuItem"/> walks the path,
    /// finding each segment by its visible text and opening each level's dropdown so lazily-built items are
    /// present. Both InvokeMenuItem (over the main menu strip) and ClickToolStripItem (over a form's
    /// toolbars) drive it through this.</summary>
    internal sealed class ToolStripElement : ControlElement
    {
        private readonly ToolStrip _toolStrip;
        public ToolStripElement(ToolStrip toolStrip) : base(toolStrip) { _toolStrip = toolStrip; }
        public override IEnumerable<UiElement> Children =>
            _toolStrip.Items.Cast<ToolStripItem>().Select(item => (UiElement) new ToolStripItemElement(item, FormElement));

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

        /// <summary>Resolves and clicks the menu/toolbar item the path names: finds it (and gates the form and
        /// item) synchronously on the UI thread, then posts its click fire-and-forget so an item that opens a
        /// modal does not block. Returns false (clicking nothing) when the path is not in this toolstrip, so a
        /// caller can try the next toolstrip on the form. Throws if the form is blocked or the item disabled.</summary>
        public bool ClickMenuItem(string menuPath)
        {
            var leaf = JsonUiService.InvokeOnUiThread(() =>
            {
                JsonUiService.VerifyFormInteractable(FormElement.Form);
                var item = ResolveMenuItem(menuPath);
                if (item == null)
                    return null;
                JsonUiService.VerifyInteractable(item);
                return item;
            });
            if (leaf == null)
                return false;
            JsonUiService.BeginInvokeOnUiThread(() => UiActions.Click.Invoke(leaf, null));
            return true;
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
    internal sealed class ToolStripItemElement : UiElement, IClickableElement
    {
        private readonly ToolStripItem _item;
        private readonly FormElement _formElement;
        public ToolStripItemElement(ToolStripItem item, FormElement formElement)
        {
            _item = item;
            _formElement = formElement;
        }
        public override string Name => _item.Name;
        public override Type ElementType => _item.GetType();
        // A ToolStripControlHost reports its hosted control's Text as its own; let the hosted control
        // (this element's child) own that label, so the verbs act on the real control, not the host.
        public override string Label => _item is ToolStripControlHost ? null
            : string.IsNullOrEmpty(_item.Text) ? _item.ToolTipText : _item.Text;
        public override bool IsEnabled => _item.Enabled;
        // Available, not Visible: an item on a context menu that was built but never shown reports
        // Visible=false (its strip is not displayed), yet it is a legitimate, actionable item.
        public override bool IsVisible => _item.Available;
        // A ToolStripControlHost hosts a real control (e.g. the Audit Log "Enable audit logging" checkbox);
        // expose it so the verbs reach it as the control it is. Otherwise descend into dropdown items.
        public override IEnumerable<UiElement> Children
        {
            get
            {
                if (_item is ToolStripControlHost host && host.Control != null)
                {
                    var hosted = _formElement.ElementFor(host.Control);
                    if (hosted != null)
                        yield return hosted;
                }
                if (_item is ToolStripDropDownItem dropDownItem)
                    foreach (ToolStripItem child in dropDownItem.DropDownItems)
                        yield return new ToolStripItemElement(child, _formElement);
            }
        }
        public void Click() => JsonUiService.InvokeOnUiThread(() => _item.PerformClick());

        // Opens / closes this item's dropdown (a menu/toolbar dropdown item), so a menu walk can populate
        // items built on DropDownOpening before matching the next path segment. A no-op for a leaf item.
        public void ShowDropDown() => (_item as ToolStripDropDownItem)?.ShowDropDown();
        public void HideDropDown() => (_item as ToolStripDropDownItem)?.HideDropDown();
    }

    /// <summary>A grid -- the DataGridView a caller reads as TSV or sets a cell on, by direct cell access.
    /// A bound grid (the inner grid of a DataboundGridControl, e.g. the Document Grid) is a
    /// <see cref="BoundGridElement"/> that overrides the read/write with the rich copy/paste path.</summary>
    internal class GridElement : ControlElement
    {
        private readonly DataGridView _dataGridView;
        public GridElement(DataGridView dataGridView) : base(dataGridView) { _dataGridView = dataGridView; }
        public DataGridView DataGridView => _dataGridView;

        // A grid carries no caption, so it is addressed by its control Name -- the one place the connector
        // matches on a name rather than on visible text (an empty name picks the form's single grid, handled
        // by FindElement). The name match is the same whether strict or loose.
        public override bool MatchesText(string text, bool strict) =>
            string.Equals(Control.Name, text, StringComparison.OrdinalIgnoreCase);

        // A grid is a leaf in the walk (not a ContainerElement): its content is read/written through the
        // grid actions, not by walking into child controls. The plain path reads/writes cells directly. A
        // large read is cancelled through the form's CancellationToken (a bound grid honors it; the plain
        // cell read is synchronous).

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

        // Pastes starting at the current cell -- the anchor a user would have clicked. Move the current
        // cell first with SetCurrentCellAddress; the text may be a multi-cell TSV block (it fills down/right).
        public virtual void SetGridText(string text)
        {
            var anchor = CurrentCellPoint();
            PasteCellText(anchor.X, anchor.Y, text);
        }

        // Moves the current cell so the next SetGridText / context menu acts there. column is the
        // visible-column index, row is the row index (the same indices GetGridText's columns/rows use).
        public void SetCurrentCellAddress(int column, int row)
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
            JsonUiService.RaiseProtectedHandler(_dataGridView, @"OnCellContextMenuStripNeeded", args);
            if (args.ContextMenuStrip == null)
                throw new ArgumentException(new LlmInstruction(@"The current cell has no context menu."));
            return JsonUiService.OpenContextMenu(args.ContextMenuStrip);
        }

        // The current cell as a point (X = visible-column index, Y = row index), or (0,0) when the grid has
        // no current cell -- the anchor SetGridText pastes from.
        private System.Drawing.Point CurrentCellPoint()
        {
            var cell = _dataGridView.CurrentCell;
            if (cell == null)
                return new System.Drawing.Point(0, 0);
            int column = Array.FindIndex(VisibleColumns(), col => col.Index == cell.ColumnIndex);
            return new System.Drawing.Point(Math.Max(0, column), cell.RowIndex);
        }

        // Pastes tab/newline-separated text into the grid by setting cell values from the anchor cell, the
        // way a user typing cell-by-cell would. Skips read-only cells. Throws if the anchor (or a row a
        // multi-row paste reaches) is out of range -- pasting cannot add rows beyond the grid's new row.
        private void PasteCellText(int column, int row, string text)
        {
            var visibleColumns = VisibleColumns();
            if (column < 0 || column >= visibleColumns.Length)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Column {0} is out of range; the grid has {1} visible columns.", column, visibleColumns.Length));
            if (row < 0)
                throw new ArgumentException(LlmInstruction.Format(@"Row {0} is out of range.", row));
            var lines = SplitPasteLines(text);
            for (int iLine = 0; iLine < lines.Length; iLine++)
            {
                int rowIndex = row + iLine;
                if (rowIndex >= _dataGridView.Rows.Count)
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Row {0} is past the end of the grid ({1} rows).", rowIndex, _dataGridView.Rows.Count));
                var cellValues = lines[iLine].Split('\t');
                for (int iCol = 0; iCol < cellValues.Length && column + iCol < visibleColumns.Length; iCol++)
                {
                    var cell = _dataGridView.Rows[rowIndex].Cells[visibleColumns[column + iCol].Index];
                    if (cell.ReadOnly)
                        continue;
                    // Go through the cell's edit lifecycle so the value is committed (and pushed to any data
                    // source) the way ending a cell edit does. A grid that validates whole rows governs
                    // whether a partially-filled new row is kept -- the same as for a user.
                    _dataGridView.CurrentCell = cell;
                    _dataGridView.BeginEdit(true);
                    cell.Value = cellValues[iCol];
                    _dataGridView.EndEdit();
                }
            }
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

        // Splits pasted text into row lines, normalizing newlines and dropping a single trailing empty line
        // (from text that ends with a newline).
        private static string[] SplitPasteLines(string text)
        {
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (lines.Length > 1 && lines[lines.Length - 1].Length == 0)
                lines = lines.Take(lines.Length - 1).ToArray();
            return lines;
        }
    }

    /// <summary>A data-bound grid (a <see cref="BoundDataGridView"/>) -- its rows are a BindingListSource,
    /// reached with <c>DataSource as BindingListSource</c>. Reads with the view's rich copy (the same
    /// content as Copy All, cancellable for a large grid) and writes through the bound paste handler (so the
    /// document stays in sync), instead of the base's direct cell access.</summary>
    internal sealed class BoundGridElement : GridElement
    {
        public BoundGridElement(BoundDataGridView boundDataGridView) : base(boundDataGridView) { }
        private BindingListSource BindingListSource => DataGridView.DataSource as BindingListSource;

        public override string GetGridText()
        {
            var bindingListSource = BindingListSource;
            return bindingListSource == null
                ? base.GetGridText() // not bound yet -- read the cells directly
                : bindingListSource.ViewContext.CopyToString(DataGridView, bindingListSource, CancellationToken);
        }

        public override void SetGridText(string text)
        {
            var bindingListSource = BindingListSource;
            if (bindingListSource == null)
                base.SetGridText(text);
            else
                // Pastes at the current cell exactly as Ctrl-V would, keeping the bound document in sync.
                DataGridViewPasteHandler.PasteText(DataGridView, bindingListSource, text);
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
        public ContextMenuElement(ControlElement owner) { _owner = owner; }
        public override string Name => string.Empty;
        public override Type ElementType => typeof(ContextMenuStrip);
        public override bool IsEnabled => _owner.IsEnabled;
        public override bool IsVisible => _owner.IsVisible;
        public override IEnumerable<UiElement> Children =>
            _owner.BuildContextMenu().Items.Cast<ToolStripItem>()
                .Select(item => (UiElement) new ToolStripItemElement(item, _owner.FormElement));
    }

    /// <summary>A TabControl -- select one of its tabs by the tab's text (select_tab). The tab pages
    /// themselves are not elements; their controls are flattened up to the form, so a control on a tab is
    /// addressed directly (select its tab first to make it visible).</summary>
    internal sealed class TabElement : ControlElement<TabControl>, ISelectTabElement
    {
        public TabElement(TabControl control) : base(control) { }
        // The tab contents are flattened to the form, so the TabControl itself has no children.
        public override IEnumerable<UiElement> Children => Enumerable.Empty<UiElement>();
        public void SelectTab(string tabText) =>
            JsonUiService.InvokeOnUiThread(() =>
            {
                var tabs = Control.TabPages.Cast<TabPage>().ToList();
                // The tab whose text matches strictly, else loosely (a strict match wins over a loose one).
                var tab = tabs.FirstOrDefault(t => TextMatches(t.Text, tabText, true))
                          ?? tabs.FirstOrDefault(t => TextMatches(t.Text, tabText, false));
                if (tab == null)
                    throw new ArgumentException(LlmInstruction.Format(@"No tab matches '{0}'.", tabText));
                Control.SelectedTab = tab;
            });
    }

    // Small value helpers shared by the value elements.
    internal static class UiValue
    {
        public static bool ParseBool(string value) =>
            bool.TryParse(value, out var parsed) ? parsed : value == @"1";

        // Converts any bare CR or LF to CRLF -- the line ending a multi-line TextBox uses for Enter.
        public static string NormalizeNewlines(string value) =>
            value == null ? null : System.Text.RegularExpressions.Regex.Replace(value, @"\r\n?|\n", "\r\n");

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
            else if (value is System.Collections.IEnumerable sequence && !(value is string))
            {
                var cell = new System.Collections.Generic.List<int>();
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
