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
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.ToolsUI
{
    // The actions a UiElement can be asked to do (see UiElement.SupportsAction / PerformAction). Every
    // element supports GetActions and GetChildren; the rest depend on the kind of control.
    internal enum UiAction
    {
        GetActions, GetChildren, Click, SetValue, GetValue,
        SetItemChecked, SetItemSelected, GetGridText, SetGridText, SetCurrentCellAddress, SetSelectedIndex
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
    /// </summary>
    internal abstract class UiElement
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
        /// caller's ControlId), so they do not reach here.</summary>
        public virtual object PerformAction(UiAction action, object value, CancellationToken cancellationToken)
        {
            throw new ArgumentException(LlmInstruction.Format(
                @"The action '{0}' is not supported on this control.", UiActions.ToName(action)));
        }
    }

    // ---- Control-backed elements ----------------------------------------------------------------

    /// <summary>Base for an element backed by a WinForms <see cref="Control"/>.</summary>
    internal abstract class ControlElement : UiElement
    {
        protected ControlElement(Control control) { Control = control; }

        public Control Control { get; }
        public override string Name => Control.Name;
        public override Type ElementType => Control.GetType();
        public override bool IsEnabled => Control.Enabled;
        public override bool IsVisible => Control.Visible;
        public override IEnumerable<UiElement> Children =>
            Control.Controls.Cast<Control>().Select(UiElementFactory.For);

        // Default: a caption-less field is named by the Label immediately before it in tab order
        // (e.g. "Name:" -> the name textbox, "Use only scans within" -> the RT box). Caption-bearing
        // elements (button, checkbox, tab) override this to return their own text.
        public override string Label
        {
            get
            {
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

    /// <summary>A control with no special capability (panel, group box, label, ...) -- kept so the walk
    /// recurses through it to its children.</summary>
    internal sealed class GenericControlElement : ControlElement
    {
        public GenericControlElement(Control control) : base(control) { }
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
        public virtual void Click() =>
            User32.PostMessageA(Control.Handle, User32.WinMessageType.BM_CLICK, 0, 0);
        public override bool SupportsAction(UiAction action) =>
            action == UiAction.Click || base.SupportsAction(action);
        public override object PerformAction(UiAction action, object value, CancellationToken cancellationToken)
        {
            if (action == UiAction.Click) { Click(); return null; }
            return base.PerformAction(action, value, cancellationToken);
        }
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
    /// (set_selected_index) or by its text (set_item_selected).</summary>
    internal class ListControlElement<T> : ControlElement<T> where T : ListControl
    {
        public ListControlElement(T control) : base(control) { }
        public override bool SupportsAction(UiAction action)
        {
            switch (action)
            {
                case UiAction.SetSelectedIndex:
                case UiAction.SetItemSelected:
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
                    JsonUiService.InvokeOnUiThread(() => Control.SelectedIndex = UiValue.ToInt(value));
                    return null;
                // The value is the item text; the action selects it (the typed verb also deselects).
                case UiAction.SetItemSelected:
                    JsonUiService.SetListItemSelected(Control, value as string, true);
                    return null;
                default:
                    return base.PerformAction(action, value, cancellationToken);
            }
        }
    }

    /// <summary>A CheckedListBox. Besides the ListControl actions, an item is checked by its text
    /// (set_item_checked) or toggled the way a user does it -- set_selected_index to the item, then click,
    /// which toggles the selected item's check. Its value is the checked items' text, one per line.</summary>
    internal sealed class CheckedListBoxElement : ListControlElement<CheckedListBox>
    {
        public CheckedListBoxElement(CheckedListBox control) : base(control) { }
        public override string Value =>
            string.Join(Environment.NewLine, Control.CheckedItems.Cast<object>().Select(Control.GetItemText));
        public override bool SupportsAction(UiAction action)
        {
            switch (action)
            {
                case UiAction.SetItemChecked:
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
                // The value is the item text; the action checks it (the typed verb also unchecks).
                case UiAction.SetItemChecked:
                    JsonUiService.SetListItemChecked(Control, value as string, true);
                    return null;
                case UiAction.Click:
                    // A click toggles the checked state of the selected item, the way a user's click/space
                    // does. Move to the item first with set_selected_index.
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
    /// is reached through a ControlId of its Type. The value is the item; the typed verbs also
    /// uncheck/deselect.</summary>
    internal class ItemContainerElement<T> : ControlElement<T> where T : Control
    {
        public ItemContainerElement(T control) : base(control) { }
        public override bool SupportsAction(UiAction action) =>
            action == UiAction.SetItemChecked || action == UiAction.SetItemSelected || base.SupportsAction(action);
        public override object PerformAction(UiAction action, object value, CancellationToken cancellationToken)
        {
            switch (action)
            {
                case UiAction.SetItemChecked:
                    JsonUiService.SetListItemChecked(Control, value as string, true);
                    return null;
                case UiAction.SetItemSelected:
                    JsonUiService.SetListItemSelected(Control, value as string, true);
                    return null;
                default:
                    return base.PerformAction(action, value, cancellationToken);
            }
        }
    }

    /// <summary>A custom IButtonControl that is not a WinForms ButtonBase (e.g. a StartPage tile) --
    /// clicked via PerformClick.</summary>
    internal sealed class ClickableControlElement : ControlElement
    {
        private readonly IButtonControl _button;
        public ClickableControlElement(Control control) : base(control) { _button = (IButtonControl) control; }
        public override string Label => Control.Text;
        public void Click() => JsonUiService.InvokeOnUiThread(() => _button.PerformClick());
        public override bool SupportsAction(UiAction action) =>
            action == UiAction.Click || base.SupportsAction(action);
        public override object PerformAction(UiAction action, object value, CancellationToken cancellationToken)
        {
            if (action == UiAction.Click) { Click(); return null; }
            return base.PerformAction(action, value, cancellationToken);
        }
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
                    yield return UiElementFactory.For(host.Control);
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

    /// <summary>A grid -- a DataboundGridControl (e.g. the Document Grid) driven through its rich paste
    /// path, or a standalone DataGridView driven by direct cell access. Read as TSV, or set a cell.</summary>
    internal sealed class GridElement : ControlElement
    {
        private readonly DataboundGridControl _databound; // null for a plain DataGridView
        private readonly DataGridView _dataGridView;
        public GridElement(DataboundGridControl databound) : base(databound)
        {
            _databound = databound;
            _dataGridView = databound.DataGridView;
        }
        public GridElement(DataGridView dataGridView) : base(dataGridView) { _dataGridView = dataGridView; }
        public DataGridView DataGridView => _dataGridView;
        // Cells are content, not child controls, so the inherited Children (the grid's child controls --
        // e.g. a DataboundGridControl's nav bar, which can host other controls) is what to enumerate.
        // A DataboundGridControl uses its rich copy path (the same content as Copy All, and cancellable
        // for a large grid); a plain DataGridView is read cell by cell.
        public string GetGridText(CancellationToken cancellationToken) =>
            _databound != null ? _databound.GetCopyText(cancellationToken) : JsonUiService.GetDataGridViewText(_dataGridView);
        // Pastes starting at the current cell -- the anchor a user would have clicked. Move the current
        // cell first with SetCurrentCellAddress; the text may be a multi-cell TSV block (it fills down/right).
        public void SetGridText(string text)
        {
            var anchor = JsonUiService.CurrentGridCell(_dataGridView);
            if (_databound != null)
                JsonUiService.PasteGridText(_databound, anchor.X, anchor.Y, text);
            else
                JsonUiService.SetDataGridViewText(_dataGridView, anchor.X, anchor.Y, text);
        }
        // Moves the current cell so the next SetGridText / context menu acts there. X is the visible-column
        // index, Y is the row index (the same indices GetGridText's columns/rows are reported in).
        public void SetCurrentCellAddress(System.Drawing.Point cell) =>
            JsonUiService.SetCurrentGridCell(_dataGridView, cell.X, cell.Y);
        public override bool SupportsAction(UiAction action) =>
            action == UiAction.GetGridText || action == UiAction.SetGridText ||
            action == UiAction.SetCurrentCellAddress || base.SupportsAction(action);
        public override object PerformAction(UiAction action, object value, CancellationToken cancellationToken)
        {
            switch (action)
            {
                case UiAction.GetGridText: return GetGridText(cancellationToken);
                case UiAction.SetGridText: SetGridText(value as string); return null;
                case UiAction.SetCurrentCellAddress: SetCurrentCellAddress(UiValue.ToPoint(value)); return null;
                default: return base.PerformAction(action, value, cancellationToken);
            }
        }
    }

    /// <summary>The right-click context menu of a control -- addressed by a ControlId whose Type is
    /// "ContextMenu" and whose Parent is that control. Its Children are the menu's top-level items, built
    /// the way a right-click would (so items added on demand are present); drill into a submenu with
    /// get_children on its item, and invoke an item with click. A control's own get_children never returns
    /// its context menu -- you ask for it explicitly with this ControlId. For a grid the menu is the one
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

    /// <summary>A tab page -- "clicking" it selects its tab on the parent TabControl.</summary>
    internal sealed class TabPageElement : ControlElement
    {
        private readonly TabPage _tabPage;
        public TabPageElement(TabPage tabPage) : base(tabPage) { _tabPage = tabPage; }
        public override string Label => _tabPage.Text;
        public void Click() => JsonUiService.InvokeOnUiThread(() =>
        {
            if (!(_tabPage.Parent is TabControl tabControl))
                throw new ArgumentException(LlmInstruction.Format(
                    @"Tab '{0}' is not on a tab control.", _tabPage.Text));
            tabControl.SelectedTab = _tabPage;
        });
        public override bool SupportsAction(UiAction action) =>
            action == UiAction.Click || base.SupportsAction(action);
        public override object PerformAction(UiAction action, object value, CancellationToken cancellationToken)
        {
            if (action == UiAction.Click) { Click(); return null; }
            return base.PerformAction(action, value, cancellationToken);
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

        // The Point a set_current_cell_address value carries: a real Point (an in-process caller), or "x,y" text.
        // (Over the wire the server has already turned the JSON object into a Point before it reaches here.)
        public static System.Drawing.Point ToPoint(object value)
        {
            if (value is System.Drawing.Point point)
                return point;
            var parts = (value as string)?.Split(',');
            if (parts?.Length == 2 && int.TryParse(parts[0].Trim(), out var x) && int.TryParse(parts[1].Trim(), out var y))
                return new System.Drawing.Point(x, y);
            throw new ArgumentException(new LlmInstruction(
                @"set_current_cell_address needs a point: the cell's column index as X and row index as Y."));
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
                case TabPage tabPage: return new TabPageElement(tabPage);
                // CheckedListBox before ListBox -- it derives from ListBox, so its case must win.
                case CheckedListBox checkedListBox: return new CheckedListBoxElement(checkedListBox);
                case ListBox listBox: return new ListControlElement<ListBox>(listBox);
                case TreeView treeView: return new ItemContainerElement<TreeView>(treeView);
                case ListView listView: return new ItemContainerElement<ListView>(listView);
                case DataboundGridControl databound: return new GridElement(databound);
                // A standalone grid; the inner grid of a DataboundGridControl is driven through it, so
                // that one is left as a plain element.
                case DataGridView dataGridView when !HasDataboundAncestor(dataGridView):
                    return new GridElement(dataGridView);
                case ToolStrip toolStrip: return new ToolStripElement(toolStrip);
                default:
                    // A custom clickable that is not a ButtonBase (e.g. a StartPage tile).
                    if (control is IButtonControl)
                        return new ClickableControlElement(control);
                    return new GenericControlElement(control);
            }
        }

        private static bool HasDataboundAncestor(Control control)
        {
            for (var parent = control.Parent; parent != null; parent = parent.Parent)
                if (parent is DataboundGridControl)
                    return true;
            return false;
        }
    }
}
