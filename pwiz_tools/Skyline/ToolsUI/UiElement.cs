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
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using SkylineTool;

namespace pwiz.Skyline.ToolsUI
{
    // The actions a UiElement can be asked to do (see UiElement.SupportsAction / PerformAction). Every
    // element supports GetActions and GetChildren; the rest depend on the kind of control.
    public enum UiAction
    {
        GetActions, GetChildren, Click, SetValue, GetValue,
        CheckItem, UncheckItem, SelectItem, UnselectItem, GetGridText, SetGridText, SetCurrentCellAddress,
        SetSelectedIndex, Expand, Collapse, SelectTab
    }

    // Converts between a UiAction and its wire name (the snake_case string the connector uses).
    internal static class UiActions
    {
        public static string ToName(UiAction action)
        {
            var name = action.ToString();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(name[i]));
            }
            return sb.ToString();
        }

        // Parses a wire name case- and underscore-insensitively, so "get_actions"/"GetActions" both work.
        public static bool TryParse(string name, out UiAction action)
        {
            var normalized = (name ?? string.Empty).Replace(@"_", string.Empty).Trim();
            foreach (UiAction candidate in Enum.GetValues(typeof(UiAction)))
            {
                if (string.Equals(candidate.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    action = candidate;
                    return true;
                }
            }
            action = default(UiAction);
            return false;
        }
    }

    /// <summary>
    /// A connector-facing view of one UI element on a form. Subclasses wrap a specific kind of control
    /// (or a ToolStrip item) and declare the actions it supports (<see cref="SupportsAction"/>) and how to
    /// perform them (<see cref="PerformAction"/>), so the verbs act polymorphically instead of switching
    /// on WinForms types. Each element also knows its own <see cref="Label"/> (the visible text that
    /// identifies it) and its <see cref="Children"/>, so matching and form enumeration are a single
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

        /// <summary>The elements nested inside this one, for the recursive form walk.</summary>
        public virtual IEnumerable<UiElement> Children => Enumerable.Empty<UiElement>();

        /// <summary>True if this element is a control a caller acts on (a button, field, list, ...) rather
        /// than a menu/list item or other pseudo-element. GetControls lists controls and descends through
        /// them; it does not list or descend into the others.</summary>
        public virtual bool IsControl => false;

        /// <summary>This element and every element under it, depth-first.</summary>
        public IEnumerable<UiElement> SelfAndDescendants()
        {
            yield return this;
            foreach (var child in Children)
            foreach (var descendant in child.SelfAndDescendants())
                yield return descendant;
        }

        /// <summary>True if this element supports an action beyond the universal get_actions/get_children
        /// -- i.e. there is something a user can do with it. Used to filter GetControls.</summary>
        public bool HasCapability =>
            SupportedActions.Any(action => action != UiAction.GetActions && action != UiAction.GetChildren);

        /// <summary>Whether this element supports the given action. The base supports only the universal
        /// GetActions and GetChildren; each kind of element overrides this to add the actions it can do.</summary>
        public virtual bool SupportsAction(UiAction action) =>
            action == UiAction.GetActions || action == UiAction.GetChildren;

        /// <summary>The actions this element supports, for discovery via GetActions / GetControls.</summary>
        public IEnumerable<UiAction> SupportedActions =>
            ((UiAction[]) Enum.GetValues(typeof(UiAction))).Where(SupportsAction);

        /// <summary>Performs an action on this element (the action determines the type of <paramref
        /// name="value"/> and of the result). Each kind of element overrides this to do its own actions,
        /// calling base for the rest. GetActions and GetChildren are handled by the service (they need the
        /// caller's path), so they do not reach here.</summary>
        public virtual object PerformAction(UiAction action, object value, CancellationToken cancellationToken)
        {
            throw new ArgumentException(LlmInstruction.Format(
                @"The action '{0}' is not supported on this control.", UiActions.ToName(action)));
        }

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
        public UiElement GetChild(UiElementPath path)
        {
            // Type "ContextMenu" addresses this control's right-click menu (never one of its real children).
            if (string.Equals(path.Type, @"ContextMenu", StringComparison.OrdinalIgnoreCase))
                return new ContextMenuElement(this);
            // No selector -> this element itself (get_children on a form lists its controls; on a control
            // walks into it).
            if (path.Text == null && path.Index == null && path.Type == null)
                return this;

            var children = Children.ToList();

            // An Index is the position among the children of its exact Type (so it is stable as other kinds
            // of control come and go), and is meaningless without that Type. Text (if set) must also match.
            if (path.Index.HasValue)
            {
                if (path.Type == null)
                    throw new ArgumentException(new LlmInstruction(
                        @"A path Index requires a Type: it is the index among the children of that exact Type."));
                var ofType = children
                    .Where(child => string.Equals(child.ElementType.Name, path.Type, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (path.Index.Value < 0 || path.Index.Value >= ofType.Count)
                    throw new ArgumentException(LlmInstruction.Format(
                        @"No {0} at index {1}; the parent has {2} of that type.", path.Type, path.Index.Value, ofType.Count));
                var indexed = ofType[path.Index.Value];
                if (path.Text != null && JsonUiService.MatchQuality(indexed.Label, path.Text) == JsonUiService.ControlMatchQuality.None)
                    throw new ArgumentException(LlmInstruction.Format(
                        @"The {0} at index {1} does not match the Text '{2}' in the path.", path.Type, path.Index.Value, path.Text));
                return indexed;
            }

            // No Index -> search the children by Text/Type, preferring the best Text match, then an
            // interactable (visible+enabled) one.
            UiElement best = null;
            var bestQuality = JsonUiService.ControlMatchQuality.None;
            var bestInteractable = false;
            foreach (var element in children)
            {
                if (!JsonUiService.MatchesPath(element, path, out var quality))
                    continue;
                var interactable = element.IsVisible && element.IsEnabled;
                if (best == null || quality > bestQuality || (quality == bestQuality && interactable && !bestInteractable))
                {
                    best = element;
                    bestQuality = quality;
                    bestInteractable = interactable;
                }
            }
            if (best == null)
                throw new ArgumentException(new LlmInstruction(
                    @"No control found matching the path. Use skyline_get_controls to list the controls."));
            return best;
        }

        /// <summary>The parentless path segment that addresses this element as the <paramref name="typeIndex"/>-th
        /// child of its parent among the siblings of this element's Type (its Text, that Index, and its Type).
        /// The caller re-parents it onto the element it listed.</summary>
        internal UiElementPath PathSegment(int typeIndex) =>
            new UiElementPath(null, JsonUiService.NullIfEmpty(JsonUiService.CleanLabel(Label)), typeIndex, ElementType.Name);
    }

    // ---- Control-backed elements ----------------------------------------------------------------

    /// <summary>Base for an element backed by a WinForms <see cref="Control"/>.</summary>
    internal abstract class ControlElement : UiElement
    {
        protected ControlElement(Control control) { Control = control; }

        public Control Control { get; }
        public override bool IsControl => true;
        public override string Name => Control.Name;
        public override Type ElementType => Control.GetType();
        public override bool IsEnabled => Control.Enabled;
        public override bool IsVisible => Control.Visible;

        // Most controls have no children -- a button, a text box, a list. Only a ContainerElement (a Form
        // or UserControl) owns children; the inherited (empty) Children is correct for everything else.

        // Every control can be clicked. A control that is itself a button -- including a custom
        // IButtonControl tile such as a StartPage action box, whose clickable surface is covered by child
        // labels and an icon -- is clicked through its handler, which is reliable regardless of those
        // children. Anything else gets a real left-click at its center, the only way to drive an owner-drawn
        // control whose click is a mouse handler. The mouse messages are POSTED (like a user click) so a
        // click that opens a modal does not block the worker thread. A subclass with a more direct gesture
        // overrides Click: a button uses BM_CLICK (to bypass PerformClick's gates), a tab selects itself.
        public virtual void Click()
        {
            if (Control is IButtonControl button)
            {
                JsonUiService.InvokeOnUiThread(button.PerformClick);
                return;
            }
            var clientSize = JsonUiService.InvokeOnUiThread(() => Control.ClientSize);
            int lParam = (clientSize.Height / 2) << 16 | (clientSize.Width / 2); // MAKELPARAM(centerX, centerY)
            User32.PostMessageA(Control.Handle, User32.WinMessageType.WM_LBUTTONDOWN, 1 /* MK_LBUTTON */, lParam);
            User32.PostMessageA(Control.Handle, User32.WinMessageType.WM_LBUTTONUP, 0, lParam);
        }
        public override bool SupportsAction(UiAction action) =>
            action == UiAction.Click || base.SupportsAction(action);
        public override object PerformAction(UiAction action, object value, CancellationToken cancellationToken)
        {
            if (action == UiAction.Click) { Click(); return null; }
            return base.PerformAction(action, value, cancellationToken);
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
    }

    /// <summary>Base for an element backed by a strongly-typed control, so subclasses use the control as
    /// its real type (e.g. a CheckedListBox) without casting.</summary>
    internal abstract class ControlElement<T> : ControlElement where T : Control
    {
        protected ControlElement(T control) : base(control) { }
        public new T Control => (T) base.Control;
    }

    /// <summary>A Form or a UserControl -- a boundary that owns its (flattened) children. It has no action
    /// of its own; it exists so the walk can list and descend into the controls it contains. Every other
    /// container (Panel, GroupBox, ...) is transparent (its controls are pulled up to the nearest Form or
    /// UserControl), so the only things that need a container element are these two.</summary>
    internal sealed class ContainerElement : ControlElement
    {
        public ContainerElement(Control control) : base(control) { }

        public override IEnumerable<UiElement> Children => FlattenChildren(Control);

        // This container's child elements for the form walk, FLATTENED. A control the factory recognizes as
        // an element is yielded as that element: a UserControl as a ContainerElement that owns its own
        // (likewise flattened) children, and a grid/list/tree/toolstrip as a leaf the caller walks into via
        // its own children. A control the factory does not recognize (a Panel, GroupBox, SplitContainer, a
        // TabPage, ...) is transparent -- its controls are pulled up so every control is a direct child of
        // the form (or of the nearest UserControl). A TabControl is kept (so its tabs can be selected via
        // select_tab) and its tab contents are flattened up to this level too. Matching the factory (rather
        // than guessing from Control.Count) keeps a complex control with internal child controls -- a
        // DataGridView, with its scroll bars -- a single element instead of dissolving it into its parts.
        private static IEnumerable<UiElement> FlattenChildren(Control container)
        {
            foreach (Control control in container.Controls)
            {
                var element = UiElementFactory.For(control);
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

    /// <summary>A checkbox: clickable (toggles via its handler) and value-settable (sets the checked state).</summary>
    internal sealed class CheckBoxElement : ButtonElement
    {
        private readonly CheckBox _checkBox;
        public CheckBoxElement(CheckBox checkBox) : base(checkBox) { _checkBox = checkBox; }
        public override string Value => _checkBox.Checked.ToString();
        public string GetValue() => Value;
        public void SetValue(string value) => _checkBox.Checked = UiValue.ParseBool(value);
        // Click is added by ButtonElement; this adds the value actions.
        public override bool SupportsAction(UiAction action) =>
            action == UiAction.SetValue || action == UiAction.GetValue || base.SupportsAction(action);
        public override object PerformAction(UiAction action, object value, CancellationToken cancellationToken)
        {
            switch (action)
            {
                case UiAction.SetValue: SetValue(value as string); return null;
                case UiAction.GetValue: return GetValue();
                default: return base.PerformAction(action, value, cancellationToken);
            }
        }
    }

    /// <summary>A radio button: clicking/setting it checks it (WinForms unchecks its siblings).</summary>
    internal sealed class RadioButtonElement : ButtonElement
    {
        private readonly RadioButton _radioButton;
        public RadioButtonElement(RadioButton radioButton) : base(radioButton) { _radioButton = radioButton; }
        public override string Value => _radioButton.Checked.ToString();
        public string GetValue() => Value;
        public void SetValue(string value) => _radioButton.Checked = UiValue.ParseBool(value);
        public override bool SupportsAction(UiAction action) =>
            action == UiAction.SetValue || action == UiAction.GetValue || base.SupportsAction(action);
        public override object PerformAction(UiAction action, object value, CancellationToken cancellationToken)
        {
            switch (action)
            {
                case UiAction.SetValue: SetValue(value as string); return null;
                case UiAction.GetValue: return GetValue();
                default: return base.PerformAction(action, value, cancellationToken);
            }
        }
    }

    /// <summary>A text box -- a caption-less field named by its adjacent label.</summary>
    internal sealed class TextBoxElement : ControlElement
    {
        private readonly TextBoxBase _textBox;
        public TextBoxElement(TextBoxBase textBox) : base(textBox) { _textBox = textBox; }
        public override string Value => _textBox.Text;
        public string GetValue() => _textBox.Text;
        // A multi-line box parses/lays out on CRLF (what Enter inserts), so normalize bare newlines.
        public void SetValue(string value) =>
            _textBox.Text = _textBox.Multiline ? UiValue.NormalizeNewlines(value) : value;
        public override bool SupportsAction(UiAction action) =>
            action == UiAction.SetValue || action == UiAction.GetValue || base.SupportsAction(action);
        public override object PerformAction(UiAction action, object value, CancellationToken cancellationToken)
        {
            switch (action)
            {
                case UiAction.SetValue: SetValue(value as string); return null;
                case UiAction.GetValue: return GetValue();
                default: return base.PerformAction(action, value, cancellationToken);
            }
        }
    }

    /// <summary>A combo box -- value set by selecting the matching item.</summary>
    internal sealed class ComboBoxElement : ControlElement
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
        public override bool SupportsAction(UiAction action) =>
            action == UiAction.SetValue || action == UiAction.GetValue || base.SupportsAction(action);
        public override object PerformAction(UiAction action, object value, CancellationToken cancellationToken)
        {
            switch (action)
            {
                case UiAction.SetValue: SetValue(value as string); return null;
                case UiAction.GetValue: return GetValue();
                default: return base.PerformAction(action, value, cancellationToken);
            }
        }
    }

    /// <summary>A ListControl -- a ListBox or CheckedListBox. Select an item by index
    /// (set_selected_index) or by its text (select_item / unselect_item).</summary>
    internal class ListControlElement<T> : ControlElement<T> where T : ListControl
    {
        public ListControlElement(T control) : base(control) { }
        public override bool SupportsAction(UiAction action)
        {
            switch (action)
            {
                case UiAction.SetSelectedIndex:
                case UiAction.SelectItem:
                case UiAction.UnselectItem:
                    return true;
                default:
                    return base.SupportsAction(action);
            }
        }
        public override object PerformAction(UiAction action, object value, CancellationToken cancellationToken)
        {
            switch (action)
            {
                case UiAction.SetSelectedIndex:
                    Control.SelectedIndex = UiValue.ToInt(value);
                    return null;
                case UiAction.SelectItem:
                    ListItems.SetSelected(Control, value as string, true);
                    return null;
                case UiAction.UnselectItem:
                    ListItems.SetSelected(Control, value as string, false);
                    return null;
                default:
                    return base.PerformAction(action, value, cancellationToken);
            }
        }
    }

    /// <summary>A CheckedListBox. Besides the ListControl actions, an item is checked/unchecked by its text
    /// (check_item / uncheck_item) or toggled the way a user does it -- set_selected_index to the item, then
    /// click, which toggles the selected item's check. Its value is the checked items' text, one per line.</summary>
    internal sealed class CheckedListBoxElement : ListControlElement<CheckedListBox>
    {
        public CheckedListBoxElement(CheckedListBox control) : base(control) { }
        public override string Value =>
            string.Join(Environment.NewLine, Control.CheckedItems.Cast<object>().Select(Control.GetItemText));
        public override bool SupportsAction(UiAction action)
        {
            switch (action)
            {
                case UiAction.CheckItem:
                case UiAction.UncheckItem:
                case UiAction.Click:
                case UiAction.GetValue:
                    return true;
                default:
                    return base.SupportsAction(action);
            }
        }
        public override object PerformAction(UiAction action, object value, CancellationToken cancellationToken)
        {
            switch (action)
            {
                case UiAction.CheckItem:
                    ListItems.SetChecked(Control, value as string, true);
                    return null;
                case UiAction.UncheckItem:
                    ListItems.SetChecked(Control, value as string, false);
                    return null;
                case UiAction.Click:
                    // A click toggles the checked state of the selected item, the way a user's click/space
                    // does (move to the item first with set_selected_index). The click runs off the UI
                    // thread inside the dialog-watch, so marshal the toggle.
                    JsonUiService.InvokeOnUiThread(() =>
                    {
                        int index = Control.SelectedIndex;
                        if (index < 0)
                            throw new ArgumentException(new LlmInstruction(
                                @"No item is selected -- choose one first with set_selected_index."));
                        Control.SetItemChecked(index, !Control.GetItemChecked(index));
                    });
                    return null;
                case UiAction.GetValue:
                    return Value;
                default:
                    return base.PerformAction(action, value, cancellationToken);
            }
        }
    }

    /// <summary>A control whose items are checked or selected by their text -- a TreeView (a node by a
    /// '>'-separated path) or a ListView. Not a ListControl (it has no SelectedIndex); a caption-less one
    /// is reached through a path of its Type. The value is the item.</summary>
    internal class ItemContainerElement<T> : ControlElement<T> where T : Control
    {
        public ItemContainerElement(T control) : base(control) { }
        public override bool SupportsAction(UiAction action)
        {
            switch (action)
            {
                case UiAction.CheckItem:
                case UiAction.UncheckItem:
                case UiAction.SelectItem:
                case UiAction.UnselectItem:
                    return true;
                default:
                    return base.SupportsAction(action);
            }
        }
        public override object PerformAction(UiAction action, object value, CancellationToken cancellationToken)
        {
            switch (action)
            {
                case UiAction.CheckItem: ListItems.SetChecked(Control, value as string, true); return null;
                case UiAction.UncheckItem: ListItems.SetChecked(Control, value as string, false); return null;
                case UiAction.SelectItem: ListItems.SetSelected(Control, value as string, true); return null;
                case UiAction.UnselectItem: ListItems.SetSelected(Control, value as string, false); return null;
                default: return base.PerformAction(action, value, cancellationToken);
            }
        }
    }

    /// <summary>A TreeView. Besides checking/selecting a node by text, a node is expanded or collapsed
    /// (expand/collapse) by a path: an array whose segments select a child at each level -- a string is the
    /// first child whose text matches it, an integer is the child at that index.</summary>
    internal sealed class TreeViewElement : ItemContainerElement<TreeView>
    {
        public TreeViewElement(TreeView control) : base(control) { }
        public override bool SupportsAction(UiAction action)
        {
            switch (action)
            {
                case UiAction.Expand:
                case UiAction.Collapse:
                    return true;
                default:
                    return base.SupportsAction(action);
            }
        }
        public override object PerformAction(UiAction action, object value, CancellationToken cancellationToken)
        {
            switch (action)
            {
                case UiAction.Expand:
                    JsonUiService.ResolveTreePath(Control, value).Expand();
                    return null;
                case UiAction.Collapse:
                    JsonUiService.ResolveTreePath(Control, value).Collapse();
                    return null;
                default:
                    return base.PerformAction(action, value, cancellationToken);
            }
        }
    }

    /// <summary>The owner-drawn ListBox on the Pick Children pop-up. It is a plain ListBox whose checkbox is
    /// painted from PickListChoice.Chosen on the PopupPickList -- not a real CheckedListBox check -- so a
    /// check routes through PopupPickList.SetItemChecked. To the connector it presents exactly as a
    /// CheckedListBox (check_item/uncheck_item, click toggles the selected item, get_value reads the checked
    /// items, plus the ListControl select actions). When PopupPickList is reworked to host a real
    /// CheckedListBox, this special case can go away.</summary>
    internal sealed class PopupPickListElement : ListControlElement<ListBox>
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
        public override bool SupportsAction(UiAction action)
        {
            switch (action)
            {
                case UiAction.CheckItem:
                case UiAction.UncheckItem:
                case UiAction.Click:
                case UiAction.GetValue:
                    return true;
                default:
                    return base.SupportsAction(action);
            }
        }
        public override object PerformAction(UiAction action, object value, CancellationToken cancellationToken)
        {
            switch (action)
            {
                case UiAction.CheckItem:
                    PickList.SetItemChecked(ListItems.FindPickListIndex(PickList, value as string), true);
                    return null;
                case UiAction.UncheckItem:
                    PickList.SetItemChecked(ListItems.FindPickListIndex(PickList, value as string), false);
                    return null;
                case UiAction.Click:
                    // A click toggles the selected item's check, like a user's click/space (move to the item
                    // first with set_selected_index). Runs off the UI thread inside the dialog-watch.
                    JsonUiService.InvokeOnUiThread(() =>
                    {
                        int index = Control.SelectedIndex;
                        if (index < 0)
                            throw new ArgumentException(new LlmInstruction(
                                @"No item is selected -- choose one first with set_selected_index."));
                        PickList.ToggleItem(index);
                    });
                    return null;
                case UiAction.GetValue:
                    return Value;
                default:
                    return base.PerformAction(action, value, cancellationToken);
            }
        }
    }

    /// <summary>The item operations the list/tree/list-view (and pick-list) elements share -- checking,
    /// selecting, and matching an item by its visible text (a TreeView node by a '>'-separated path).
    /// These were moved off JsonUiService now that only the elements drive them; they reuse the connector's
    /// text matching (JsonUiService.MatchQuality) and run on the UI thread (the service marshals there).</summary>
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

        // Index of the best-matching choice (by its visible label) in a pick-list pop-up. Throws if none.
        public static int FindPickListIndex(PopupPickList pickList, string item)
        {
            var labels = pickList.ItemNames.ToList();
            int best = BestMatch(labels.Count, i => labels[i], item);
            if (best < 0)
                throw new ArgumentException(LlmInstruction.Format(@"Item not found in the pick list: {0}.", item));
            return best;
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

        // The index of the best text match among count items (by the connector's label matching), or -1.
        private static int BestMatch(int count, Func<int, string> textOf, string key)
        {
            int best = -1;
            var bestQuality = JsonUiService.ControlMatchQuality.None;
            for (int i = 0; i < count; i++)
            {
                var quality = JsonUiService.MatchQuality(textOf(i), key);
                if (quality > bestQuality)
                {
                    best = i;
                    bestQuality = quality;
                }
            }
            return best;
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

    /// <summary>A ToolStrip (menu strip / toolbar) -- its items are its children.</summary>
    internal sealed class ToolStripElement : ControlElement
    {
        private readonly ToolStrip _toolStrip;
        public ToolStripElement(ToolStrip toolStrip) : base(toolStrip) { _toolStrip = toolStrip; }
        public override IEnumerable<UiElement> Children =>
            _toolStrip.Items.Cast<ToolStripItem>().Select(item => (UiElement) new ToolStripItemElement(item));
    }

    /// <summary>A menu or toolbar item -- clicked via PerformClick. An image-only item (no caption) is
    /// named by its tooltip, the way a user reads it (e.g. the pick-list's green-check "OK").</summary>
    internal sealed class ToolStripItemElement : UiElement
    {
        private readonly ToolStripItem _item;
        public ToolStripItemElement(ToolStripItem item) { _item = item; }
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
                    var hosted = UiElementFactory.For(host.Control);
                    if (hosted != null)
                        yield return hosted;
                }
                if (_item is ToolStripDropDownItem dropDownItem)
                    foreach (ToolStripItem child in dropDownItem.DropDownItems)
                        yield return new ToolStripItemElement(child);
            }
        }
        public void Click() => JsonUiService.InvokeOnUiThread(() => _item.PerformClick());
        public override bool SupportsAction(UiAction action) =>
            action == UiAction.Click || base.SupportsAction(action);
        public override object PerformAction(UiAction action, object value, CancellationToken cancellationToken)
        {
            if (action == UiAction.Click) { Click(); return null; }
            return base.PerformAction(action, value, cancellationToken);
        }
    }

    /// <summary>A grid -- the DataGridView a caller reads as TSV or sets a cell on, by direct cell access.
    /// A bound grid (the inner grid of a DataboundGridControl, e.g. the Document Grid) is a
    /// <see cref="BoundGridElement"/> that overrides the read/write with the rich copy/paste path.</summary>
    internal class GridElement : ControlElement
    {
        private readonly DataGridView _dataGridView;
        public GridElement(DataGridView dataGridView) : base(dataGridView) { _dataGridView = dataGridView; }
        public DataGridView DataGridView => _dataGridView;

        // A grid is a leaf in the walk (not a ContainerElement): its content is read/written through the
        // grid actions, not by walking into child controls. The plain path reads/writes cells directly.
        public virtual string GetGridText(CancellationToken cancellationToken) =>
            JsonUiService.GetDataGridViewText(_dataGridView);
        // Pastes starting at the current cell -- the anchor a user would have clicked. Move the current
        // cell first with SetCurrentCellAddress; the text may be a multi-cell TSV block (it fills down/right).
        public virtual void SetGridText(string text)
        {
            var anchor = JsonUiService.CurrentGridCell(_dataGridView);
            JsonUiService.SetDataGridViewText(_dataGridView, anchor.X, anchor.Y, text);
        }
        // Moves the current cell so the next SetGridText / context menu acts there. column is the
        // visible-column index, row is the row index (the same indices GetGridText's columns/rows use).
        public void SetCurrentCellAddress(int column, int row) =>
            JsonUiService.SetCurrentGridCell(_dataGridView, column, row);
        public override bool SupportsAction(UiAction action) =>
            action == UiAction.GetGridText || action == UiAction.SetGridText ||
            action == UiAction.SetCurrentCellAddress || base.SupportsAction(action);
        public override object PerformAction(UiAction action, object value, CancellationToken cancellationToken)
        {
            switch (action)
            {
                case UiAction.GetGridText: return GetGridText(cancellationToken);
                case UiAction.SetGridText: SetGridText(value as string); return null;
                case UiAction.SetCurrentCellAddress:
                    var cell = UiValue.ToColumnRow(value);
                    SetCurrentCellAddress(cell[0], cell[1]);
                    return null;
                default: return base.PerformAction(action, value, cancellationToken);
            }
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

        public override string GetGridText(CancellationToken cancellationToken)
        {
            var bindingListSource = BindingListSource;
            return bindingListSource == null
                ? base.GetGridText(cancellationToken) // not bound yet -- read the cells directly
                : bindingListSource.ViewContext.CopyToString(DataGridView, bindingListSource, cancellationToken);
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
        private readonly UiElement _owner;
        public ContextMenuElement(UiElement owner) { _owner = owner; }
        public override string Name => string.Empty;
        public override Type ElementType => typeof(ContextMenuStrip);
        public override bool IsEnabled => _owner.IsEnabled;
        public override bool IsVisible => _owner.IsVisible;
        public override IEnumerable<UiElement> Children =>
            JsonUiService.BuildContextMenu(_owner).Items.Cast<ToolStripItem>()
                .Select(item => (UiElement) new ToolStripItemElement(item));
    }

    /// <summary>A TabControl -- select one of its tabs by the tab's text (select_tab). The tab pages
    /// themselves are not elements; their controls are flattened up to the form, so a control on a tab is
    /// addressed directly (select its tab first to make it visible).</summary>
    internal sealed class TabElement : ControlElement<TabControl>
    {
        public TabElement(TabControl control) : base(control) { }
        // The tab contents are flattened to the form, so the TabControl itself has no children.
        public override IEnumerable<UiElement> Children => Enumerable.Empty<UiElement>();
        public override bool SupportsAction(UiAction action) =>
            action == UiAction.SelectTab || base.SupportsAction(action);
        public override object PerformAction(UiAction action, object value, CancellationToken cancellationToken)
        {
            if (action != UiAction.SelectTab)
                return base.PerformAction(action, value, cancellationToken);
            JsonUiService.InvokeOnUiThread(() =>
            {
                var text = value as string;
                var tabs = Control.TabPages.Cast<TabPage>().ToList();
                int best = -1;
                var bestQuality = JsonUiService.ControlMatchQuality.None;
                for (int i = 0; i < tabs.Count; i++)
                {
                    var quality = JsonUiService.MatchQuality(tabs[i].Text, text);
                    if (quality > bestQuality)
                    {
                        best = i;
                        bestQuality = quality;
                    }
                }
                if (best < 0)
                    throw new ArgumentException(LlmInstruction.Format(@"No tab matches '{0}'.", text));
                Control.SelectedTab = tabs[best];
            });
            return null;
        }
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

    /// <summary>
    /// Builds the <see cref="UiElement"/> for a WinForms control, choosing the subclass by its kind.
    /// </summary>
    internal static class UiElementFactory
    {
        public static UiElement For(Control control)
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
                // A Form or UserControl (including a DataboundGridControl) is a boundary that owns its
                // (flattened) children; everything else that contains controls is transparent, so these are
                // the only containers to descend into.
                case Form form: return new ContainerElement(form);
                case UserControl userControl: return new ContainerElement(userControl);
                default:
                    // A custom clickable that is not a ButtonBase (e.g. a StartPage tile).
                    if (control is IButtonControl)
                        return new ClickableControlElement(control);
                    // Anything else reaching here is a leaf with no capability (a label, a spacer, an empty
                    // panel); it is not something a caller can act on, so it is not an element.
                    return null;
            }
        }
    }
}
