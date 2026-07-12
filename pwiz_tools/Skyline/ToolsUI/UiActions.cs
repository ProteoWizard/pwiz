/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using System.Linq;
using pwiz.Skyline.Util.Extensions;
using SkylineTool;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// One thing the connector can ask a <see cref="UiElement"/> to do (the verbs ClickFormButton,
    /// SetFormValue, the grid verbs, and the generic perform_action all act through these). An action is a
    /// singleton object -- see <see cref="UiActions"/> for the set of them -- that knows its wire
    /// <see cref="SnakeCaseName"/> and the gates the connector must honor before performing it
    /// (<see cref="MustBeEnabled"/> -- a user could not act on a control they cannot see or that is disabled). It
    /// decides whether it <see cref="AppliesTo"/> an element (usually: the
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

        /// <summary>One line describing what the action does, surfaced by get_actions so a caller does not have
        /// to guess. LLM-facing instruction text (see <see cref="LlmInstruction"/>), not for display to users.</summary>
        public LlmInstruction Description { get; private set; }

        /// <summary>What to pass as the action's value, or null when it takes none (also surfaced by
        /// get_actions). LLM-facing instruction text (see <see cref="LlmInstruction"/>).</summary>
        public LlmInstruction ValueDescription { get; private set; }

        /// <summary>Sets the self-documentation. Called through the chaining <c>Describe</c> in
        /// <see cref="UiActions"/>, which is an extension method so that it returns the action's CONCRETE type -- an
        /// instance method could only return UiAction, and a <see cref="UiFunction{TElement,TResult}"/> would lose
        /// its result type the moment it was described. The descriptions are <see cref="LlmInstruction"/> so that
        /// every unlocalized LLM-facing string is greppable for a future localization pass.</summary>
        internal void SetDescription(LlmInstruction description, LlmInstruction valueDescription)
        {
            Description = description;
            ValueDescription = valueDescription;
        }

        /// <summary>This action as the <see cref="ActionInfo"/> get_actions reports.</summary>
        public ActionInfo ToActionInfo() => new ActionInfo
        {
            Name = SnakeCaseName,
            Description = Description,
            ValueDescription = ValueDescription,
        };

        /// <summary>Whether the element must be visible / enabled for the action to be performed -- the gates
        /// a user faces. A pure read (get_value, get_actions, get_children, get_grid_text) clears these, so a
        /// disabled or off-screen control can still be inspected; a mutation or click leaves them set.</summary>
        public bool MustBeEnabled { get; internal set; } = true;

        /// <summary>Whether this action is meaningful for the element -- usually whether the element is the
        /// kind the action targets (it implements the action's capability interface).</summary>
        public abstract bool AppliesTo(UiElement element);

        /// <summary>Whether the action can currently be performed on the element: it applies and the element
        /// passes the visible/enabled gates the action requires.</summary>
        public bool IsEnabled(UiElement element) =>
            AppliesTo(element)
            && (!MustBeEnabled || element.IsEnabled);

        /// <summary>The action ITSELF, run ON the element's UI thread: a raw gesture (a click, a value set) or a raw
        /// read. It does NO threading and NO gating -- it assumes the caller has already put it on the right thread
        /// and checked the gates. This is the one place a kind of element says what the action means; the threading
        /// belongs to <see cref="Invoke"/>, so no element decides its own.</summary>
        public abstract object InvokeNow(UiElement element, object argument);

        /// <summary>The action WITH the threading it needs, called from the connector's worker (pipe/test) thread.
        /// The base is the GESTURE case -- the common one: gate the element, post the gesture onto its UI thread and
        /// wait it out, so it returns an <see cref="ActionResult"/> saying whether it completed or left a dialog
        /// open. An action that needs different threading says so by BEING a different kind of action, rather than
        /// by setting a flag the base has to branch on: a read is a <see cref="UiFunction{T}"/>, and Accept, which
        /// waits for a form to close and so must run off that form's thread, is the one self-threaded action (see
        /// UiActions). Either way an action behaves the same reached through perform_action or a named verb. Must be
        /// called off the UI thread.</summary>
        public virtual object Invoke(UiElement element, object argument)
        {
            return element.PerformGesture(() => InvokeNow(element, argument));
        }
    }

    /// <summary>
    /// An action that PRODUCES A VALUE rather than changing anything -- get_value, get_options, get_grid_text,
    /// get_actions, get_children. Being a read is what its TYPE says, so nothing carries a "returns a value" flag
    /// and no caller branches on one.
    ///
    /// <para>It runs on the element's UI thread and returns what it read, inside the dialog-watch so that producing
    /// it cannot hang behind a modal (which is why the read is the one kind of action that can be answered while a
    /// dialog is blocking the form). A read also clears the visible/enabled gates that a gesture leaves set: a
    /// disabled or off-screen control can still be INSPECTED, as a user can read a greyed-out field.</para>
    /// </summary>
    /// <typeparam name="TResult">what the read returns -- the ONLY thing a caller needs to know about it, so it is
    /// the only thing this advertises. WHICH kind of element it reads is the implementation's business (see
    /// UiActions.SimpleFunctionImpl), so it stays out of the type: get_grid_text is a
    /// <c>UiFunction&lt;string&gt;</c>, not a UiFunction of some internal grid-element class.</typeparam>
    public abstract class UiFunction<TResult> : UiAction
    {
        protected UiFunction(string name) : base(name)
        {
            // A read clears the visible/enabled gates a gesture leaves set.
            MustBeEnabled = false;
        }

        /// <summary>The read itself, ON the element's UI thread (the caller has already marshaled). The typed
        /// counterpart of <see cref="InvokeNow"/>, which is just this boxed -- a C# override cannot narrow the
        /// return type, so the typed pair is a separate name rather than an override.</summary>
        public abstract TResult CallNow(UiElement element);

        /// <summary>The read WITH the threading it needs, from the connector's worker thread -- the typed
        /// counterpart of <see cref="Invoke"/>.
        ///
        /// <para>The nesting is load-bearing: the dialog-watch is OUTSIDE, so a read gives up rather than hanging
        /// behind a modal that is blocking the form; the hop onto the element's own UI thread is INSIDE it, because
        /// for a form running its own message loop (a BackgroundThreadLongWaitDlg) that is not the main window's
        /// thread. Reversing them would either read the wrong thread or hang.</para></summary>
        public virtual TResult Call(UiElement element)
        {
            return JsonUiService.RunWithDialogWatch(
                () => element.InvokeOnUiThread(() => CallNow(element)),
                element.CancellationToken);
        }

        public override object InvokeNow(UiElement element, object argument) => CallNow(element);

        public override object Invoke(UiElement element, object argument) => Call(element);
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

        // The element kind an action targets lives ONLY in these implementations. It is how the action finds and
        // checks its element, not something a caller needs to know -- so it is a type parameter here and never
        // appears on the public UiAction / UiFunction<TResult> a caller holds.

        /// <summary>A GESTURE that targets element kind <typeparamref name="TElement"/> (a capability interface or an
        /// element type) and whose gesture is a single call <c>func(element, argument)</c> -- so an action needs no
        /// class of its own, just an entry below. The argument is handed to the func as <typeparamref name="TArg"/>
        /// (the JSON value is already a string or the raw object; an action that wants an int or a cell parses it
        /// inside its func).</summary>
        private sealed class SimpleActionImpl<TElement, TArg> : UiAction
        {
            private readonly Func<UiElement, TElement> _toElement;
            private readonly Func<TElement, TArg, object> _invoke;

            public SimpleActionImpl(string name, Func<UiElement, TElement> toElement, Func<TElement, TArg, object> invoke) : base(name)
            {
                _toElement = toElement;
                _invoke = invoke;
            }

            public override bool AppliesTo(UiElement element) => null != _toElement(element);

            public override object InvokeNow(UiElement element, object argument)
            {
                var typed = _toElement(element);
                if (typed == null)
                    throw new ArgumentException(LlmInstruction.Format(
                        @"The action '{0}' does not apply to this control.", SnakeCaseName));
                return _invoke(typed, argument is TArg arg ? arg : default(TArg));
            }
        }

        /// <summary>A READ that targets element kind <typeparamref name="TElement"/> and whose read is a single call
        /// <c>read(element)</c>. The element kind stays here; what it RETURNS is what its
        /// <see cref="UiFunction{TResult}"/> base advertises.</summary>
        private sealed class SimpleFunctionImpl<TElement, TResult> : UiFunction<TResult>
        {
            private readonly Func<TElement, TResult> _read;

            public SimpleFunctionImpl(string name, Func<TElement, TResult> read) : base(name)
            {
                _read = read;
            }

            public override bool AppliesTo(UiElement element) => element is TElement;

            public override TResult CallNow(UiElement element)
            {
                if (!(element is TElement typed))
                    throw new ArgumentException(LlmInstruction.Format(
                        @"The action '{0}' does not apply to this control.", SnakeCaseName));
                return _read(typed);
            }
        }

        /// <summary>get_actions: the ONE action that touches no UI at all. Which actions an element supports is
        /// decided by its TYPE -- the capability interfaces it implements -- not by anything on screen, so there is
        /// nothing to marshal onto a UI thread and nothing that could hang behind a modal. Call IS CallNow.</summary>
        private sealed class GetActionsFunction : UiFunction<ActionInfo[]>
        {
            public GetActionsFunction() : base(@"GetActions")
            {
            }

            public override bool AppliesTo(UiElement element) => true;

            public override ActionInfo[] CallNow(UiElement element) =>
                element.SupportedActions.Select(a => a.ToActionInfo()).ToArray();

            // No threading: the answer is a property of the element's type, not of the UI.
            public override ActionInfo[] Call(UiElement element) => CallNow(element);
        }

        /// <summary>dismiss: the ONE action that must NOT be marshaled onto the form's UI thread. The dismiss verbs
        /// ride DialogWatcher.OkDialog, which posts the dismiss gesture onto that thread and then WAITS THERE for the
        /// form to close; run on that same thread it would be waiting on itself. So it owns its threading -- Invoke
        /// runs it exactly where it was called, on the connector's worker thread.</summary>
        private sealed class DismissAction : UiAction
        {
            public DismissAction() : base(@"Dismiss")
            {
            }

            public override bool AppliesTo(UiElement element) => element is StandaloneWindow;

            public override object InvokeNow(UiElement element, object argument)
            {
                var window = (StandaloneWindow) element;
                var button = argument as string;
                return string.IsNullOrEmpty(button)
                    ? window.DismissWithAcceptButton()
                    : window.DismissWithButton(button);
            }

            public override object Invoke(UiElement element, object argument) => InvokeNow(element, argument);
        }

        // get_actions has its own class: alone among the actions it reads no UI at all, so it does no threading
        // (see GetActionsFunction).
        public static readonly UiFunction<ActionInfo[]> GetActions = new GetActionsFunction()
            .Describe(new LlmInstruction(@"List the actions this control supports, each with a description and the value it takes."));

        public static readonly UiFunction<ControlInfo[]> GetChildren = SimpleFunction<UiElement, ControlInfo[]>(
                @"GetChildren", e => e.GetControlsNow())
            .Describe(new LlmInstruction(@"List this element's child controls; each one's path can be used directly in a later call."));

        // A gesture (click, value set, item check, ...): the func is the element's raw "...Now" method, and
        // UiAction.Invoke gates it, posts it onto the element's UI thread and waits it out. A read is a
        // UiFunction instead (above), which is what makes it run as a read.
        public static readonly UiAction Click = SimpleAction<IClickableElement>(
                @"Click", e => { e.ClickNow(); return null; })
            .Describe(new LlmInstruction(@"Click this control (a button, menu/list item, checkbox, ...)."));

        public static readonly UiFunction<object> GetValue = SimpleFunction<UiElement, object>(
                @"GetValue", e => UiElement.ConvertValue(e.Value))
            .Describe(new LlmInstruction(@"Get this control's current value (null, a bool, a number, or a string)."));

        public static readonly UiAction SetValue = SimpleAction<IValueElement, object>(
                @"SetValue", (e, value) => { e.SetValueNow(UiElement.ConvertValue(value)); return null; })
            .Describe(new LlmInstruction(@"Set this control's value."), new LlmInstruction(@"the new value -- a bool, a number, or a string"));

        public static readonly UiAction CheckItem = SimpleAction<ICheckItemsElement, string>(
                @"CheckItem", (e, item) => { e.SetItemCheckedNow(item, true); return null; })
            .Describe(new LlmInstruction(@"Check the list/tree item with the given text."), new LlmInstruction(@"the item's visible text"));

        public static readonly UiAction UncheckItem = SimpleAction<ICheckItemsElement, string>(
                @"UncheckItem", (e, item) => { e.SetItemCheckedNow(item, false); return null; })
            .Describe(new LlmInstruction(@"Uncheck the list/tree item with the given text."), new LlmInstruction(@"the item's visible text"));

        public static readonly UiAction SelectItem = SimpleAction<ISelectItemsElement, string>(
                @"SelectItem", (e, item) => { e.SetItemSelectedNow(item, true); return null; })
            .Describe(new LlmInstruction(@"Select the list/tree item with the given text."), new LlmInstruction(@"the item's visible text"));

        public static readonly UiAction UnselectItem = SimpleAction<ISelectItemsElement, string>(
                @"UnselectItem", (e, item) => { e.SetItemSelectedNow(item, false); return null; })
            .Describe(new LlmInstruction(@"Deselect the list/tree item with the given text."), new LlmInstruction(@"the item's visible text"));

        public static readonly UiAction SetSelectedIndex = SimpleAction<ISelectItemsElement, object>(
                @"SetSelectedIndex", (e, arg) => { e.SetSelectedIndexNow(UiValue.ToInt(arg)); return null; })
            .Describe(new LlmInstruction(@"Select the list item at the given index (clears any other selection)."), new LlmInstruction(@"the zero-based index"));

        public static readonly UiFunction<string[]> GetOptions = SimpleFunction<IOptionsElement, string[]>(
                @"GetOptions", e => e.GetOptions().ToArray())
            .Describe(new LlmInstruction(@"List all the choices this control offers (a combo box, list box, or checked list box), regardless of which are selected or checked."));

        public static readonly UiFunction<string> GetGridText = SimpleFunction<GridElement, string>(
                @"GetGridText", e => e.GetGridText())
            .Describe(new LlmInstruction(@"Get the whole grid as tab-separated text -- the column headers then every row."));

        public static readonly UiAction SetGridText = SimpleAction<GridElement, string>(
                @"SetGridText", (e, text) => { e.SetGridTextNow(text); return null; })
            .Describe(new LlmInstruction(@"Paste tab-separated text into the grid starting at the current cell."), new LlmInstruction(@"the tab-separated text (it fills down and to the right)"));

        public static readonly UiAction SetCurrentCellAddress = SimpleAction<GridElement, object>(
                @"SetCurrentCellAddress", (e, arg) => { var cell = UiValue.ToColumnRow(arg); e.SetCurrentCellAddressNow(cell[0], cell[1]); return null; })
            .Describe(new LlmInstruction(@"Move the grid's current cell (do this before set_grid_text or opening a cell's menu)."), new LlmInstruction(@"a [column, row] array, e.g. [0, 1]"));

        public static readonly UiAction Expand = SimpleAction<IExpandCollapseElement, object>(
                @"Expand", (e, path) => { e.ExpandNow(path); return null; })
            .Describe(new LlmInstruction(@"Expand a tree node by its path."), new LlmInstruction(@"a path array of child names/indexes, e.g. [""Peptides"", 0]"));

        public static readonly UiAction Collapse = SimpleAction<IExpandCollapseElement, object>(
                @"Collapse", (e, path) => { e.CollapseNow(path); return null; })
            .Describe(new LlmInstruction(@"Collapse a tree node by its path."), new LlmInstruction(@"a path array of child names/indexes, e.g. [""Peptides"", 0]"));

        public static readonly UiAction SelectTab = SimpleAction<ISelectTabElement, string>(
                @"SelectTab", (e, tab) => { e.SelectTabNow(tab); return null; })
            .Describe(new LlmInstruction(@"Select the tab with the given text."), new LlmInstruction(@"the tab's visible text"));

        // Dismisses a form/dialog: with no button its default (accept) button, with a caption that button -- waiting
        // for the form to close either way. Named for the DismissWith... verbs it is the generic form of. It has its
        // own class because it owns its threading (see DismissAction).
        public static readonly UiAction Dismiss = new DismissAction()
            .Describe(new LlmInstruction(@"Dismiss the dialog -- its default/OK button; pass a button's caption to click that one instead."));

        // Pastes the given text into a control that can paste (text box, grid, Targets tree, main window) --
        // for the tutorial paste steps, without touching the clipboard.
        public static readonly UiAction Paste = SimpleAction<IClipboardElement, string>(@"Paste", (e, text) => { e.PasteNow(text); return null; })
            .Describe(new LlmInstruction(@"Paste text into this element (a text box, a grid, the Targets tree, or the main Skyline window) without using the clipboard."), new LlmInstruction(@"the text to paste"));

        // Selects everything in a control that can paste -- e.g. before a paste, to replace the contents.
        public static readonly UiAction SelectAll = SimpleAction<IClipboardElement>(@"SelectAll", e => { e.SelectAllNow(); return null; })
            .Describe(new LlmInstruction(@"Select all the content of this element (a text box, a grid, the Targets tree, or the main Skyline window) -- e.g. before paste, to replace it."));

        // Renames the tree's selected node in place -- e.g. the MethodEdit tutorial's "Type 'Primary
        // Peptides' and press Enter" on a peptide group. Select the node first.
        public static readonly UiAction RenameNode = SimpleAction<IRenameNodeElement, string>(
                @"RenameNode", (e, value) => { e.RenameNodeNow(value); return null; })
            .Describe(new LlmInstruction(@"Rename the tree's selected node in place (select the node first)."), new LlmInstruction(@"the new name"));

        // Every action, in get_actions / get_children listing order (the universal ones first).
        public static readonly UiAction[] AllActions =
        {
            GetActions, GetChildren, Click, GetValue, SetValue, GetOptions, CheckItem, UncheckItem, SelectItem,
            UnselectItem, SetSelectedIndex, GetGridText, SetGridText, SetCurrentCellAddress, Expand,
            Collapse, SelectTab, Dismiss, Paste, SelectAll, RenameNode
        };

        // The action with the given wire name, matched case- and underscore-insensitively, or null.
        public static UiAction ByName(string name)
        {
            var normalized = (name ?? string.Empty).Replace(@"_", string.Empty).Trim();
            return AllActions.FirstOrDefault(action =>
                string.Equals(action.Name, normalized, StringComparison.OrdinalIgnoreCase));
        }

        // A read: it produces a value and changes nothing, so it is a UiFunction (which is what makes it run as a
        // read -- see that class), and it clears the visible/enabled gates. TElement is inferred from the lambda and
        // then disappears: what comes back advertises only what it RETURNS.
        public static UiFunction<TResult> SimpleFunction<TElement, TResult>(
            string name, Func<TElement, TResult> read)
        {
            return new SimpleFunctionImpl<TElement, TResult>(name, read);
        }

        /// <summary>Sets an action's self-documentation and returns it, so a definition below reads as one
        /// expression. An EXTENSION method, not an instance one, purely so that it gives back the action's concrete
        /// type: an instance method could only return UiAction, and a <see cref="UiFunction{TResult}"/> would lose
        /// its result type the moment it was described.</summary>
        public static TAction Describe<TAction>(this TAction action,
            LlmInstruction description, LlmInstruction valueDescription = default) where TAction : UiAction
        {
            action.SetDescription(description, valueDescription);
            return action;
        }

        // A gesture (a click, a value set, an item check): its func is the element's raw "...Now" method, which does
        // no threading of its own -- UiAction.Invoke gates it, posts it onto the element's UI thread and waits it
        // out. mustBeEnabled: unlike a read, a gesture leaves the gates set.
        public static UiAction SimpleAction<T>(string name, Func<T, object> action)
        {
            return SimpleAction<T, object>(name, (element, _) => action(element));
        }
        public static UiAction SimpleAction<TElement, TArg>(string name, Func<TElement, TArg, object> action)
        {
            return new SimpleActionImpl<TElement, TArg>(name, element => element is TElement typed  ? typed : default, action);
        }
    }
}
