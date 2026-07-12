/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.6) <noreply .at. anthropic.com>
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
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using SkylineTool;
using ZedGraph;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Static service for Skyline UI interactions invoked from the JSON pipe server.
    /// Provides UI thread marshaling primitives, reusable UI patterns (e.g., ImmediateWindow
    /// output tee), and complete UI operations (graph enumeration, selection, etc.).
    /// </summary>
    public static class JsonUiService
    {
        private const string EXT_PNG = @".png";
        private const string GRAPH_FILE_PREFIX = @"skyline-graph";

        // Level 1: Primitives - UI thread marshaling

        /// <summary>
        /// Executes an action on the UI thread. Exceptions propagate to the caller via wrapping to preserve
        /// the original stack trace. <paramref name="dispatcher"/> is the control to marshal through -- a
        /// form on its own thread when given (see <see cref="UiElement.InvokeOnUiThread(System.Action)"/>),
        /// otherwise (null) the main window. Most callers go through the <see cref="UiElement"/> methods. Delegates
        /// the raw <see cref="Control.Invoke(System.Delegate)"/> to <see cref="UiServiceDispatcher"/>, which owns
        /// the marshal, and adds the exception wrapping here.
        /// </summary>
        public static void InvokeOnUiThread(Action action, Control dispatcher = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            // A plain synchronous marshal: through the given control (a form on its own thread), or the UI-thread
            // window, or -- when already on that thread, or before any window exists -- run inline. The action's
            // exception is caught and re-thrown wrapped (ArgumentException preserved) so its stack survives. The
            // marshal is cancellable (not a raw Control.Invoke): it is the first UI-thread hop most verbs make, so a
            // call parked here on a UI thread that is not pumping must still be abandonable when its client goes away.
            var control = dispatcher;
            if (control == null || !control.IsHandleCreated || control.IsDisposed)
                control = DialogWatcher.UiThreadWindow;
            Exception caught = null;
            void Run()
            {
                try { action(); }
                catch (Exception ex) { caught = ex; }
            }
            if (control != null && control.InvokeRequired)
                DialogWatcher.InvokeCancelable(control, Run, cancellationToken);
            else
                Run();
            if (caught is ArgumentException argEx)
                throw new ArgumentException(argEx.Message, argEx.ParamName, argEx);
            if (caught != null)
                ExceptionUtil.WrapAndThrowException(caught);
        }

        /// <summary>
        /// Executes a function on the UI thread and returns the result. Must be called from a background thread
        /// (pipe server thread). Exceptions propagate to the caller raw.
        /// </summary>
        public static T InvokeOnUiThread<T>(Func<T> func, Control dispatcher = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            T result = default(T);
            InvokeOnUiThread(() => { result = func(); }, dispatcher, cancellationToken);
            return result;
        }

        /// <summary>
        /// Posts an action to the UI thread fire-and-forget (BeginInvoke, not Invoke): the caller returns at
        /// once and does not wait for or observe the result. Used for a void action (a click, a value set)
        /// so a gesture that opens a modal dialog does not block -- the modal is driven by later commands,
        /// the same way the main-menu-item path posts its click. Posted on the same (main-window) queue as
        /// <see cref="InvokeOnUiThread"/>, so a later synchronous read sees this action's
        /// effect (the queue is FIFO). The action is counted in <see cref="ModalNestingCount"/> from when
        /// it is posted until its delegate returns. Must be called off the UI thread.
        /// </summary>
        public static void BeginInvokeOnUiThread(Action action, Control dispatcher = null)
        {
            DialogWatcher.IncrementModalNestingCount();
            void Run()
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    // A void action is posted fire-and-forget, so there is no caller frame to catch a failure;
                    // it would otherwise reach the global handler as an "Unexpected Error". Show it as a normal
                    // MessageDlg instead -- its message and stack are then also readable by the connector's form
                    // gate (see BlockingAlertMessage), so the next command can report what went wrong.
                    MessageDlg.ShowException((IWin32Window) dispatcher ?? Program.MainWindow, exception);
                }
                finally { DialogWatcher.DecrementModalNestingCount(); }
            }
            // A form on its own thread (e.g. BackgroundThreadLongWaitDlg) is posted to through its own BeginInvoke;
            // otherwise the UI-thread window (main window, or StartPage before it). Before either exists, a
            // background thread, so a service can still act during startup.
            var control = dispatcher;
            if (control == null || !control.IsHandleCreated || control.IsDisposed)
                control = DialogWatcher.UiThreadWindow;
            try
            {
                if (control != null)
                    control.BeginInvoke((Action) Run);
                else
                    ActionUtil.RunAsync(Run, @"JsonTool command");
            }
            catch
            {
                // Posting failed, so the action will never run and is not pending after all.
                DialogWatcher.DecrementModalNestingCount();
                throw;
            }
        }

        /// <summary>
        /// Runs <paramref name="work"/> on a background thread and waits for it, but never hangs behind a
        /// MODAL dialog that blocks one of this process's windows and that it cannot get past. A
        /// <see cref="LongWaitDlg"/> is the exception: it is a progress dialog the work itself drives, so the
        /// watch keeps waiting for it. Any other blocking modal throws its message (see
        /// <see cref="BlockingAlertMessage"/>) -- a CommonAlertDlg's or ReportErrorDlg's text, or any other
        /// dialog's title (a managed caption or a native Open/Save dialog's window title) -- so the caller
        /// sees what is in the way (and can drive it: GetOpenForms / SetFormValue / ClickFormButton / accept).
        /// Used by verbs that can pop a dialog (RunCommand, the value reads, ...). Must be called off the UI thread.
        /// </summary>
        public static T RunWithDialogWatch<T>(Func<T> work,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            // Run the value-producing work on the UI-thread window and return its result, throwing a blocking modal's
            // message rather than hanging behind it -- DialogWatcher.CallFunction. IntPtr.Zero resolves to no managed
            // control, so it targets the UI-thread window (main window / StartPage).
            return DialogWatcher.CallFunction(IntPtr.Zero, work, cancellationToken);
        }

        // The wait a named/convenience method runs for its gesture: post <paramref name="postGesture"/> onto the
        // window at <paramref name="hwnd"/> (the target form's own UI thread) and wait until it finishes or leaves an
        // interactive modal open (DialogWatcher.PerformAction). Must be called off the UI thread.
        internal static ActionResult WaitForGesture(IntPtr hwnd, Action postGesture,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return DialogWatcher.PerformAction(hwnd, postGesture, cancellationToken);
        }

        // Level 2: UI patterns

        /// <summary>
        /// Creates a TextWriter that tees output to both the given capture writer
        /// and Skyline's Immediate Window. Shows the Immediate Window and writes
        /// the header before returning.
        /// </summary>
        /// <summary>Returns the main Skyline window, or throws an LLM-facing error if it does not exist yet
        /// (only the start page is showing). For verbs that genuinely need the document / main window
        /// itself, so they fail with a clear message instead of a NullReferenceException.</summary>
        public static SkylineWindow RequireMainWindow()
        {
            return Program.MainWindow ?? throw new InvalidOperationException(LlmInstruction.Format(
                @"This requires the main Skyline window, which is not available while the start page is showing."));
        }

        public static TextWriter CreateImmediateWindowTee(TextWriter capture, string header)
        {
            RequireMainWindow();
            TextWriter immediateWriter = null;
            Program.MainWindow.Invoke(new Action(() =>
            {
                Program.MainWindow.ShowImmediateWindow();
                Program.MainWindow.ImmediateWindow.WriteFresh(header);
                Program.MainWindow.ImmediateWindow.WriteLine(string.Empty);
                immediateWriter = Program.MainWindow.ImmediateWindow.Writer;
            }));
            return new TeeTextWriter(capture, immediateWriter);
        }

        // Level 3: Complete UI operations - Selection

        /// <summary>
        /// Special locator string representing the insertion point at the end of the document tree.
        /// Used by GetSelection/SetSelection to round-trip the insertion node selection.
        /// </summary>
        public const string INSERT_NODE_LOCATOR = @"/Insert";

        public static SelectionInfo GetSelection()
        {
            return InvokeOnUiThread(() =>
            {
                var skylineWindow = Program.MainWindow;
                var document = skylineWindow.DocumentUI;
                var sequenceTree = skylineWindow.SequenceTree;
                var selectedPaths = sequenceTree.SelectedPaths;
                if (selectedPaths.Count == 0)
                    return new SelectionInfo { Locators = Array.Empty<string>() };

                var elementRefs = new ElementRefs(document);
                var locators = new List<string>();
                foreach (var path in selectedPaths)
                {
                    if (path.IsRoot)
                        continue;
                    if (sequenceTree.IsInsertPath(path))
                    {
                        locators.Add(INSERT_NODE_LOCATOR);
                        continue;
                    }
                    var nodeRef = elementRefs.GetNodeRef(path);
                    if (nodeRef == null)
                        continue;
                    locators.Add(nodeRef.ToString());
                }
                return new SelectionInfo { Locators = locators.ToArray() };
            });
        }

        public static void SetSelection(string elementLocatorString, string additionalLocators)
        {
            InvokeOnUiThread(() =>
            {
                var skylineWindow = Program.MainWindow;

                // Primary selection
                if (elementLocatorString == INSERT_NODE_LOCATOR)
                {
                    skylineWindow.SequenceTree.SelectedPath =
                        new IdentityPath(SequenceTree.NODE_INSERT_ID);
                }
                else
                {
                    // Full navigation (bookmark, replicate, scroll)
                    skylineWindow.SelectElement(
                        ElementRefs.FromObjectReference(ElementLocator.Parse(elementLocatorString)));
                }

                // Secondary selections
                if (!string.IsNullOrEmpty(additionalLocators))
                {
                    var document = skylineWindow.DocumentUI;
                    var allPaths = new List<IdentityPath> { skylineWindow.SequenceTree.SelectedPath };
                    foreach (var line in additionalLocators.Split('\n'))
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed))
                            continue;
                        if (trimmed == INSERT_NODE_LOCATOR)
                        {
                            allPaths.Add(new IdentityPath(SequenceTree.NODE_INSERT_ID));
                            continue;
                        }
                        var elementRef = ElementRefs.FromObjectReference(ElementLocator.Parse(trimmed));
                        if (elementRef is NodeRef nodeRef)
                        {
                            var path = nodeRef.ToIdentityPath(document);
                            if (path != null)
                                allPaths.Add(path);
                        }
                    }
                    skylineWindow.SequenceTree.SelectedPaths = allPaths;
                }
            });
        }

        public static void SetReplicate(string replicateName)
        {
            InvokeOnUiThread(() =>
            {
                var document = Program.MainWindow.DocumentUI;
                if (!document.Settings.HasResults)
                    throw new InvalidOperationException(@"Document has no results");
                var chromatograms = document.Settings.MeasuredResults.Chromatograms;
                int index = chromatograms.IndexOf(c => c.Name == replicateName);
                if (index < 0)
                    throw new ArgumentException(@"Replicate not found: " + replicateName);
                Program.MainWindow.SelectedResultsIndex = index;
            });
        }

        public static ActionResult InvokeOnMainWindow(Action<SkylineStandaloneForm> action, CancellationToken cancellationToken)
        {
            var mainWindow = RequireMainWindow();
            return DialogWatcher.PerformAction(mainWindow, () =>
            {
                var skylineStandaloneWindow =
                    new SkylineStandaloneForm(mainWindow, mainWindow.Handle, cancellationToken);
                action(skylineStandaloneWindow);
            }, cancellationToken);
        }

        public static ActionResult InvokeOnForm(string formId, Action<StandaloneWindow> action,
            CancellationToken cancellationToken)
        {
            var window = ResolveForm(formId, cancellationToken);
            return DialogWatcher.PerformAction(window.Hwnd, () =>
            {
                action(window);
            }, cancellationToken);
        }

        // Level 3: Complete UI operations - Generic form interaction

        // Populates a ContextMenuStrip the way right-clicking the graph would: it invokes the graph's
        // ContextMenuBuilder handlers (which add the Skyline-specific items) with a point at the
        // control's center, so the correct graph pane is chosen for a multi-pane graph. The event can
        // only be raised from inside ZedGraphControl, so its backing delegate is fetched by reflection.
        internal static void PopulateGraphContextMenu(ZedGraphControl zedGraph, ContextMenuStrip menuStrip)
        {
            var field = typeof(ZedGraphControl).GetField(@"ContextMenuBuilder",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var builder = field?.GetValue(zedGraph) as ZedGraphControl.ContextMenuBuilderEventHandler;
            if (builder == null)
                throw new ArgumentException(new LlmInstruction(@"This graph has no context menu."));
            var centerPoint = new System.Drawing.Point(zedGraph.Width / 2, zedGraph.Height / 2);
            // menuStrip is the control's reused ContextMenuStrip, so it may still hold items from a previous
            // populate or a prior real right-click; empty it first (as ZedGraph's own contextMenuStrip1_Opening
            // does) before the builder repopulates it.
            menuStrip.Items.Clear();
            builder(zedGraph, menuStrip, centerPoint, default(ZedGraphControl.ContextMenuObjectState));
        }

        /// <summary>
        /// Pastes tab-separated <paramref name="text"/> into a grid on a form, starting at its current
        /// cell -- move there first with <see cref="SetCurrentCellAddress"/> (the anchor a user would click). The
        /// text may be a multi-cell TSV block (it fills down and to the right). Works for a
        /// DataboundGridControl (e.g. the Document Grid) and for a plain DataGridView (e.g. the Rule Set
        /// Editor's rules grid). See <see cref="IJsonToolService"/>.
        /// </summary>
        public static ActionResult SetGridText(string formId, string controlId, string text, CancellationToken cancellationToken = default(CancellationToken))
        {
            ValidateFormIdFormat(formId);
            // Resolve the grid on the form's own thread, then paste: SetGridText posts onto that thread and waits the
            // paste out itself (the count settling, or a type-conversion alert the paste raises appearing, which the
            // caller then drives), returning only once the paste has taken effect.
            var formElement = (StandaloneForm) FindFormById(formId, cancellationToken);
            var gridElement = formElement.InvokeOnUiThread(() => formElement.FindGrid(controlId));
            return (ActionResult) UiActions.SetGridText.Invoke(gridElement, text ?? string.Empty);
        }

        /// <summary>
        /// Moves the current cell of a grid on a form (move there before pasting with
        /// <see cref="SetGridText"/> or opening the cell's context menu). <paramref name="column"/> is the
        /// visible-column index and <paramref name="row"/> is the row index -- the same indices the grid
        /// reports columns and rows in. See <see cref="IJsonToolService"/>.
        /// </summary>
        public static ActionResult SetCurrentCellAddress(string formId, string controlId, int column, int row, CancellationToken cancellationToken = default(CancellationToken))
        {
            ValidateFormIdFormat(formId);
            // Resolve the grid on the form's own thread, then move the current cell: SetCurrentCellAddress posts onto
            // that thread and waits the move out itself (the count settling), returning only once the cell has moved.
            var formElement = (StandaloneForm) FindFormById(formId, cancellationToken);
            var gridElement = formElement.InvokeOnUiThread(() => formElement.FindGrid(controlId));
            return (ActionResult) UiActions.SetCurrentCellAddress.Invoke(gridElement, new[] { column, row });
        }

        /// <summary>
        /// Returns all the text in a grid on a form -- the column headers followed by every data row --
        /// as tab-separated columns and newline-separated rows. Works for a DataboundGridControl (the
        /// same content as Copy All) and for a plain DataGridView. See <see cref="IJsonToolService"/>.
        /// </summary>
        public static string GetGridText(string formId, string gridId)
        {
            ValidateFormIdFormat(formId);
            // A value read: run it synchronously inside the dialog-watch so it does not hang if a modal is up.
            string text = null;
            RunWithDialogWatch(() =>
            {
                text = OnFormThread(formId, formElement => formElement.FindGrid(gridId).GetGridText());
                return true;
            });
            return text;
        }

        /// <summary>
        /// The most general way to interact with a control, menu item, or list item (see
        /// <see cref="IJsonToolService"/>): resolve the element the <paramref name="path"/> refers to,
        /// then perform <paramref name="action"/> on it. The action determines the value and return types:
        /// "get_actions" -> ActionInfo[] (name + description + the value it takes); "get_children" ->
        /// ControlInfo[] (each Path already parented onto this element, so it can be used as-is); "click" ->
        /// null; "set_value" -> null; "get_value" -> the value (null, bool, double, or string).
        /// </summary>
        public static object PerformAction(UiElementPath path, string action, object value, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (path == null)
                throw new ArgumentException(new LlmInstruction(@"A path is required."));
            var uiAction = UiActions.ByName(action) ?? throw new ArgumentException(LlmInstruction.Format(
                @"Unsupported action '{0}'. Use get_actions to list the actions a control supports.", action));
            // The path's root names a form; resolve it (managed or native) and let it perform the action in its
            // own thread context (a managed form on the UI thread inside the dialog-watch; a native dialog on
            // this calling thread). get_actions/get_children are ordinary reads -- the action's Invoke returns
            // the element's SupportedActions / GetChildren(), whose child paths are parented onto the resolved
            // element (its Path was recorded in ResolvePath) so the caller can use them directly.
            return ResolveForm(path.GetRoot().Text, cancellationToken).PerformAction(path, uiAction, value);
        }

        // Runs a resolved action. There is nothing to dispatch: UiAction.Invoke owns the threading, and each KIND of
        // action already knows the threading it needs -- a gesture is gated, posted onto the element's UI thread and
        // waited out; a UiFunction runs on that thread inside the dialog-watch and returns its value; Accept runs
        // here, on the caller's thread, because it waits for a form to close. So perform_action behaves exactly like
        // the named verbs, which invoke the same actions. Must be called off the UI thread.
        internal static object ExecuteAction(UiAction action, UiElement element, object value)
        {
            return action.Invoke(element, value);
        }

        // Verifies the resolved element supports the action (it is the kind the action targets); the
        // interactable gate is applied later by UiAction.Invoke. Returns the element; throws a clear error
        // (listing the element's actions) if the action does not apply.
        internal static UiElement RequireAction(UiElement element, UiAction action)
        {
            if (!action.AppliesTo(element))
                throw new ArgumentException(LlmInstruction.Format(
                    @"The control '{0}' does not support the action '{1}'. It supports: {2}.",
                    element.Label ?? element.Name, action.SnakeCaseName,
                    string.Join(@", ", element.SupportedActions.Select(a => a.SnakeCaseName))));
            return element;
        }

        // Resolves a UiElementPath to the single element it refers to, given the already-resolved
        // <paramref name="root"/> form (the FormElement or NativeDialog the path's root segment names -- the
        // caller resolves it from that segment's Text, e.g. with path.GetRoot()). Each non-root segment names
        // a child of the element its Parent resolves to, by Index (its position in the parent's child list),
        // Text (its visible label), and/or Type -- every property that is set must match.
        internal static UiElement ResolvePath(UiElementPath path, UiElement root)
        {
            if (path == null)
                throw new ArgumentException(new LlmInstruction(@"A path is required."));

            UiElement element;
            if (path.Parent == null)
            {
                // The root segment names a form (the caller resolved it), so its Type must be "Form" or unset.
                if (!string.IsNullOrEmpty(path.Type) && !string.Equals(path.Type, @"Form", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException(LlmInstruction.Format(
                        @"The root of a path must be a form (Type 'Form' or unset), not '{0}'.", path.Type));
                element = root;
            }
            else
            {
                element = ResolvePath(path.Parent, root).GetChild(path);
            }
            // Record the resolved element's own path, so its get_children parents the controls it lists.
            element.Path = path;
            return element;
        }

        // Level 3: Complete UI operations - Graphs

        public static FormInfo[] GetOpenForms(CancellationToken cancellationToken = default(CancellationToken))
        {
            // The main window and the forms docked in it all live on the main window's thread, so they are read in
            // one trip there. Every OTHER form is read below, on its own thread -- see why there.
            var dockedForms = new HashSet<Form>();
            var results = InvokeOnUiThread(() =>
            {
                var skylineWindow = Program.MainWindow;
                var formInfos = new List<FormInfo>();
                // The main window does not exist while the StartPage is showing; skip the docked
                // forms in that case and just enumerate the open forms below (the StartPage and any
                // of its dialogs appear there).
                if (skylineWindow != null)
                {
                    // The main window itself, so its id (and thus its menus/toolbars) is discoverable.
                    formInfos.Add(new FormInfo
                    {
                        Type = skylineWindow.GetType().Name,
                        Title = GetFormTitle(skylineWindow),
                        HasGraph = false,
                        DockState = @"Main",
                        Id = GetFormId(skylineWindow),
                        ModalNestingCount = DialogWatcher.TryGetPreShowActionCount(skylineWindow.Handle),
                    });
                    foreach (var form in skylineWindow.DockPanel.Contents.OfType<DockableFormEx>())
                    {
                        dockedForms.Add(form);
                        var dockState = form.DockState;
                        if (dockState == DockState.Hidden || dockState == DockState.Unknown)
                            continue;
                        var zedGraph = TryGetZedGraphControl(form);
                        formInfos.Add(new FormInfo
                        {
                            Type = form.GetType().Name,
                            Title = GetFormTitle(form),
                            HasGraph = zedGraph != null,
                            DockState = dockState.ToString(),
                            Id = GetFormId(form),
                            ModalNestingCount = DialogWatcher.TryGetPreShowActionCount(form.Handle),
                        });
                    }
                }

                return formInfos;
            }, null, cancellationToken);

            // Every other WinForms form -- a dialog, a popup -- read through ITS OWN form, not the main window's: a
            // form that runs its own message loop on its own thread (a BackgroundThreadLongWaitDlg) owns its controls
            // there, so reading Visible / Text / Handle for it from the main window's thread is a cross-thread touch.
            // It throws only when a debugger is attached (Control.CheckForIllegalCrossThreadCalls defaults to
            // Debugger.IsAttached), which is exactly why this went unnoticed -- see ConnectorFormThreadingTest, which
            // turns the check on.
            foreach (var form in FormUtil.OpenForms)
            {
                if (form == Program.MainWindow || dockedForms.Contains(form))
                    continue;
                try
                {
                    var formInfo = InvokeOnUiThread(() => !form.Visible ? null : new FormInfo
                    {
                        Type = form.GetType().Name,
                        Title = GetFormTitle(form),
                        HasGraph = false,
                        DockState = @"Dialog",
                        Id = GetFormId(form),
                        ModalNestingCount = DialogWatcher.TryGetPreShowActionCount(form.Handle),
                    }, form, cancellationToken);
                    if (formInfo != null)
                        results.Add(formInfo);
                }
                catch (Exception)
                {
                    // The form can close between the enumeration and the read -- skip a vanishing one rather than
                    // failing the whole GetOpenForms for a caller polling during a close (as the native loop does).
                }
            }

            // Native common dialogs (e.g. the Open/Save file dialog) are not WinForms forms and
            // so never appear in FormUtil.OpenForms. Enumerate them via UI Automation. This runs
            // on the pipe thread, NOT inside InvokeOnUiThread: when such a dialog is modal the UI
            // thread is busy in the dialog's own message loop, and querying it from that thread
            // can deadlock.
            foreach (var dialog in NativeDialog.GetOpenDialogs(cancellationToken))
            {
                try
                {
                    results.Add(new FormInfo
                    {
                        Type = dialog.DialogTypeName,
                        Title = dialog.Title,
                        HasGraph = false,
                        DockState = @"Dialog",
                        Id = dialog.FormId,
                        IsNative = true,
                    });
                }
                catch (Exception)
                {
                    // The dialog can close between enumeration and reading its title/id (UI Automation) -- skip a
                    // vanishing one rather than failing the whole GetOpenForms for a caller polling during a close.
                }
            }
            return results.ToArray();
        }

        // Resolves a formId to the window it addresses -- a WinForms form (FormElement) or a native common
        // dialog (NativeDialog) -- so every verb drives both through IFormElement and none special-cases a
        // native dialog. Native dialogs are matched first, on this (pipe) thread: they are non-managed windows
        // enumerated via UI Automation cross-thread (no Control to marshal through), which runs alongside their
        // modal loop rather than on it. A managed form is then found (also off the UI thread). Throws if no open
        // window has the id.
        /// <summary>Resolves a formId to the element that drives it, built with the CANCELLATION OF THE REQUEST that
        /// asked for it: every marshal and wait the returned element (and the tree under it) performs can then be
        /// abandoned when that client disconnects. The token comes from the JsonToolServer verb that is serving the
        /// request -- it reads it on its own thread (the only thread it means anything on) and passes it in here.
        /// In-process callers, which have no client to disconnect, pass CancellationToken.None.</summary>
        public static StandaloneWindow ResolveForm(string formId, CancellationToken cancellationToken)
        {
            ValidateFormIdFormat(formId);
            // The form is the root of its path; record it so get_controls parents the controls onto it.
            var formPath = new UiElementPath(null, formId, null, @"Form");
            foreach (var dialog in NativeDialog.GetOpenDialogs(cancellationToken))
                if (dialog.FormId == formId)
                {
                    dialog.Path = formPath;
                    return dialog;
                }
            // The managed form, already built (with its handle) by GetOpenFormElements; recording its path is a
            // plain field set, so no UI-thread marshal is needed.
            var formElement = (StandaloneForm) FindFormById(formId, cancellationToken);
            formElement.Path = formPath;
            return formElement;
        }

        // Resolves the managed form named by formId and runs func against it on that form's own UI thread --
        // the correct thread even for a form created on its own background thread (e.g. a
        // BackgroundThreadLongWaitDlg), whose controls must be touched through its message loop, not the main
        // window's (see UiElement.InvokeOnUiThread). The form lookup runs on the calling thread; func then runs on
        // the form's thread, where it walks/reads the control tree. Must be called off the UI thread.
        private static T OnFormThread<T>(string formId, Func<StandaloneForm, T> func, CancellationToken cancellationToken = default(CancellationToken))
        {
            // Find the form (any thread); it is already built (with its handle). Run func on the form's OWN thread:
            // its controls must be touched through that form's Invoke.
            var formElement = (StandaloneForm) FindFormById(formId, cancellationToken);
            return InvokeOnUiThread(() => func(formElement), formElement.Form);
        }

        public static string GetGraphData(string graphId, string filePath, CancellationToken cancellationToken = default(CancellationToken))
        {
            var form = ((StandaloneForm) FindFormById(graphId, cancellationToken)).Form as DockableFormEx;
            return InvokeOnUiThread(() =>
            {
                var zedGraph = form != null ? TryGetZedGraphControl(form) : null;
                if (zedGraph == null)
                {
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Not a graph form: {0}. Use skyline_get_open_forms to find forms with HasGraph=True.",
                        graphId));
                }
                var graphData = CopyGraphDataToolStripMenuItem.GetGraphData(zedGraph.MasterPane);
                if (graphData.Panes.Count == 0)
                    return string.Empty;
                filePath = filePath ?? GetMcpTmpFilePath(
                    GRAPH_FILE_PREFIX, form.Text, TextUtil.EXT_TSV);
                DirectoryEx.CreateForFilePath(filePath);
                using (var saver = new FileSaver(filePath))
                {
                    File.WriteAllText(saver.SafeName, graphData.ToString());
                    saver.Commit();
                }
                return filePath.ToForwardSlashPath();
            });
        }

        public static string GetGraphImage(string graphId, string filePath)
        {
            string denial = CheckImageToolPreflight(graphId,
                () => EnsureGraphForm(graphId, out _, out _),
                requiresScreenCapture: false);
            if (denial != null)
                return denial;
            return InvokeOnUiThread(() =>
            {
                using (var bitmap = RenderGraphBitmap(graphId, out var form))
                {
                    filePath = filePath ?? GetMcpTmpFilePath(
                        GRAPH_FILE_PREFIX, form.Text, EXT_PNG);
                    DirectoryEx.CreateForFilePath(filePath);
                    using (var saver = new FileSaver(filePath))
                    {
                        bitmap.Save(saver.SafeName, ImageFormat.Png);
                        saver.Commit();
                    }
                }
                return filePath.ToForwardSlashPath();
            });
        }

        public static ImageBytesMetadata GetGraphImageBytes(string graphId)
        {
            string denial = CheckImageToolPreflight(graphId,
                () => EnsureGraphForm(graphId, out _, out _),
                requiresScreenCapture: false);
            if (denial != null)
                return new ImageBytesMetadata { Message = denial };
            return InvokeOnUiThread(() =>
            {
                using (var bitmap = RenderGraphBitmap(graphId, out var form))
                {
                    return new ImageBytesMetadata
                    {
                        Data = BitmapToPngBytes(bitmap),
                        FilePath = GetMcpTmpFilePath(GRAPH_FILE_PREFIX, form.Text, EXT_PNG)
                            .ToForwardSlashPath(),
                        MimeType = MIME_TYPE_PNG
                    };
                }
            });
        }

        private const string FORM_FILE_PREFIX = @"skyline-form";

        // LLM-facing instruction text for the form-image permission states.
        // Wrapped in LlmInstruction so the type makes the not-translated
        // contract explicit, and exposed as fields so tests can assert against
        // the canonical value instead of a brittle English substring
        // (see CRITICAL-RULES.md on translation-proof tests).
        public static readonly LlmInstruction LLM_MSG_SCREEN_CAPTURE_DENIED =
            new LlmInstruction(@"Screen capture denied by user.");
        public static readonly LlmInstruction LLM_MSG_SCREEN_CAPTURE_PERMISSION_REQUIRED =
            new LlmInstruction(@"Screen capture permission required. A confirmation dialog is now open in Skyline; ask the user to grant or deny it, then call this tool again. This is the documented two-phase handshake, not an error.");
        public static readonly LlmInstruction LLM_MSG_SCREEN_CAPTURE_UNAVAILABLE =
            new LlmInstruction(@"Screen capture is not available. The desktop session may be disconnected (e.g. Docker container, disconnected Remote Desktop, or locked workstation). Reconnect the desktop session and try again.");

        public static string GetFormImage(string formId, string filePath, CancellationToken cancellationToken = default(CancellationToken))
        {
            ValidateFormIdFormat(formId);
            // ResolveForm throws "form not found" before any permission prompt, so a bad id never prompts.
            var form = ResolveForm(formId, cancellationToken);
            string denial = CheckScreenCaptureAvailability();
            if (denial != null)
                return denial;
            // The form captures itself in its own thread context (a managed form on the UI thread with
            // redaction; a native dialog by window handle on this thread).
            using (var bitmap = form.CaptureImage())
            {
                filePath = filePath ?? GetMcpTmpFilePath(FORM_FILE_PREFIX, form.Title, EXT_PNG);
                DirectoryEx.CreateForFilePath(filePath);
                bitmap.Save(filePath, ImageFormat.Png);
            }
            return filePath.ToForwardSlashPath();
        }

        public static ImageBytesMetadata GetFormImageBytes(string formId, CancellationToken cancellationToken = default(CancellationToken))
        {
            ValidateFormIdFormat(formId);
            var form = ResolveForm(formId, cancellationToken);
            string denial = CheckScreenCaptureAvailability();
            if (denial != null)
            {
                // Permission denial / desktop unavailable: return a structured Message instead of bytes. The
                // wrapper emits Message as plain text content (no error flag) so the response shape matches
                // what the legacy file-based path returned for the same condition.
                return new ImageBytesMetadata { Message = denial };
            }
            using (var bitmap = form.CaptureImage())
            {
                return new ImageBytesMetadata
                {
                    Data = BitmapToPngBytes(bitmap),
                    FilePath = GetMcpTmpFilePath(FORM_FILE_PREFIX, form.Title, EXT_PNG).ToForwardSlashPath(),
                    MimeType = MIME_TYPE_PNG
                };
            }
        }

        // Captures a screenshot of a native window (e.g. the Open/Save file dialog) by its handle, on the
        // calling (pipe) thread -- the capture is a screen copy and must not marshal to the UI thread, which
        // may be blocked in the dialog's modal loop. GetWindowRect returns logical coordinates, scaled to
        // physical pixels to match the screen copy (the same convention as ScreenCapture.GetForeignWindowRects).
        internal static System.Drawing.Bitmap CaptureNativeWindow(IntPtr windowHandle)
        {
            User32.SetForegroundWindow(windowHandle);
            var rect = new User32.RECT();
            User32.GetWindowRect(windowHandle, ref rect);
            var screenRect = rect.Rectangle * ScreenCapture.GetScalingFactor();
            return ScreenCapture.CaptureScreen(screenRect);
        }

        // Validates that the given id identifies a form bearing a ZedGraph
        // control. Throws ArgumentException if the form is missing or is not
        // a graph form. Used both as the existence-check step in
        // CheckImageToolPreflight and as the first step of actual rendering.
        private static void EnsureGraphForm(string graphId, out DockableFormEx form, out ZedGraphControl graph, CancellationToken cancellationToken = default(CancellationToken))
        {
            form = ((StandaloneForm) FindFormById(graphId, cancellationToken)).Form as DockableFormEx;
            graph = form != null ? TryGetZedGraphControl(form) : null;
            if (graph == null)
            {
                throw new ArgumentException(LlmInstruction.Format(
                    @"Not a graph form: {0}. Use skyline_get_open_forms to find forms with HasGraph=True.",
                    graphId));
            }
        }

        // Renders the bitmap for a ZedGraph form, returning the bitmap and the host form.
        // Caller owns the bitmap and must dispose it.
        private static System.Drawing.Bitmap RenderGraphBitmap(string graphId, out DockableFormEx form)
        {
            EnsureGraphForm(graphId, out form, out var graph);
            return graph.MasterPane.GetImage(graph.MasterPane.IsAntiAlias);
        }

        // Shared pre-flight for the image-capture tools (form and graph variants).
        // Runs format validation on the pipe thread, guards against there being no
        // UI thread to marshal to, and runs the type-specific existence check on the
        // UI thread -- in that order so that bad input throws ArgumentException
        // regardless of environment. Optionally runs the screen-capture availability
        // check (form variants only). Returns null when the caller may proceed, or an
        // LLM-facing message the caller must surface. Throws ArgumentException
        // for bad input (id format wrong, referenced form not found, wrong form
        // type) -- those are caller-contract violations and must reach the
        // caller regardless of environment.
        private static string CheckImageToolPreflight(string id, Action ensureExistsOnUi, bool requiresScreenCapture)
        {
            ValidateFormIdFormat(id);
            // Passing an Action binds InvokeOnUiThread to the void overload,
            // which preserves ArgumentException across the thread boundary.
            InvokeOnUiThread(ensureExistsOnUi);
            return requiresScreenCapture ? CheckScreenCaptureAvailability() : null;
        }

        // Returns null when screen capture can proceed, or the LLM-facing
        // denial / pending / desktop-unavailable message that the form-image
        // tools should return to the caller without attempting capture.
        // Called from the pipe thread (no Invoke marshal) so a Pending or
        // Denied response does not pay the UI-thread round trip.
        private static string CheckScreenCaptureAvailability()
        {
            switch (ScreenCapture.EnsurePermission())
            {
                case PermissionResult.denied:
                    return LLM_MSG_SCREEN_CAPTURE_DENIED;
                case PermissionResult.pending:
                    return LLM_MSG_SCREEN_CAPTURE_PERMISSION_REQUIRED;
                case PermissionResult.unavailable:
                    return LLM_MSG_SCREEN_CAPTURE_UNAVAILABLE;
            }
            if (!ScreenCapture.IsDesktopAvailable())
            {
                return LLM_MSG_SCREEN_CAPTURE_UNAVAILABLE;
            }
            return null;
        }

        // Cheap formId well-formedness check that runs on the pipe thread,
        // before the screen-capture permission prompt fires. Catching obviously
        // bad input here avoids interrupting the user with a permission dialog
        // for a request that can never succeed.
        private static void ValidateFormIdFormat(string formId)
        {
            if (formId == null || formId.IndexOf(':') < 0)
            {
                throw new ArgumentException(LlmInstruction.Format(
                    @"Invalid form ID format: {0}. Expected 'TypeName:Title'. Use skyline_get_open_forms to get valid IDs.",
                    formId ?? string.Empty));
            }
        }

        private const string MIME_TYPE_PNG = @"image/png";

        private static byte[] BitmapToPngBytes(System.Drawing.Bitmap bitmap)
        {
            using (var memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                return memory.ToArray();
            }
        }

        // Private helpers - Graph support

        /// <summary>
        /// Returns the display title for a form, used both in GetOpenForms output
        /// and in FindFormById matching.
        /// </summary>
        internal static string GetFormTitle(Form form)
        {
            if (form is DockableFormEx dockable)
                return !string.IsNullOrEmpty(dockable.Text) ? dockable.Text
                    : !string.IsNullOrEmpty(dockable.TabText) ? dockable.TabText
                    : dockable.GetType().Name;
            return !string.IsNullOrEmpty(form.Text) ? form.Text : form.GetType().Name;
        }

        /// <summary>
        /// Builds a stable form identifier from type name and title.
        /// </summary>
        internal static string GetFormId(Form form)
        {
            return form.GetType().Name + @":" + GetFormTitle(form);
        }

        internal static ZedGraphControl TryGetZedGraphControl(DockableFormEx form)
        {
            // Use reflection to find any public property that returns ZedGraphControl,
            // so that new graph forms are automatically supported.
            foreach (var prop in form.GetType().GetProperties())
            {
                if (typeof(ZedGraphControl).IsAssignableFrom(prop.PropertyType) && prop.GetIndexParameters().Length == 0)
                    return prop.GetValue(form) as ZedGraphControl;
            }
            return null;
        }

        /// <summary>Every window a connector caller can address, each wrapped as the connector form abstraction
        /// that drives it: the top-level windows (see <see cref="NativeDialog.GetTopLevelWindows"/>) plus, when the
        /// main Skyline window is up, the forms docked inside it. Lazy -- the top-level windows are yielded first
        /// (Win32-only, no marshal), and only if the caller keeps enumerating past them is the docked-form list
        /// fetched. The docked forms are child windows (not top-level), so they are read through the main window's
        /// DockPanel, which must be on its UI thread -- hence the single Invoke, paid only when reached (a caller
        /// that finds its match among the top-level windows never triggers it).</summary>
        public static IEnumerable<StandaloneWindow> GetOpenFormElements(CancellationToken cancellationToken)
        {
            foreach (var window in NativeDialog.GetTopLevelWindows(cancellationToken))
                yield return window;

            foreach (var docked in GetDockedForms(cancellationToken))
                yield return docked;
        }

        // The forms docked in the main Skyline window, each as a FormElement, or empty when the main window is not
        // up. Callable from any thread: the docked forms are child windows read through the main window's DockPanel,
        // which must be on its UI thread, so this Invokes the read (and the FormElement construction, which captures
        // each handle) onto that thread. Hidden/unknown-state docked forms are skipped (not on screen).
        private static IList<StandaloneWindow> GetDockedForms(CancellationToken cancellationToken)
        {
            var mainWindow = Program.MainWindow;
            if (mainWindow == null)
                return new List<StandaloneWindow>();
            // Through InvokeOnUiThread (not a raw Invoke) so a client that disconnects can abandon this: it is the
            // first UI-thread hop most verbs make (form lookup), and a call parked here would hold the single-threaded
            // pipe server against every later request.
            return InvokeOnUiThread(() =>
            {
                var result = new List<StandaloneWindow>();
                foreach (var form in mainWindow.DockPanel.Contents.OfType<DockableFormEx>())
                {
                    var dockState = form.DockState;
                    if (dockState == DockState.Hidden || dockState == DockState.Unknown)
                        continue;
                    result.Add(StandaloneWindow.NewStandaloneWindow(form.Handle, cancellationToken));
                }
                return (IList<StandaloneWindow>) result;
            }, null, cancellationToken);
        }

        /// <summary>
        /// Finds a managed form by its TypeName:Title identifier -- the main window, a form docked in it, or an
        /// open dialog -- and returns it as the already-built <see cref="StandaloneForm"/> (its window handle already
        /// captured). Matches against <see cref="GetOpenFormElements"/>, which enumerates the top-level windows off
        /// any thread and reads the docked forms through the main window's own Invoke, so this may be called from
        /// any thread (the caller need not marshal it onto the UI thread).
        /// </summary>
        private static StandaloneWindow FindFormById(string formId, CancellationToken cancellationToken)
        {
            ValidateFormIdFormat(formId);
            foreach (var window in GetOpenFormElements(cancellationToken))
                if (window is StandaloneForm formElement && formElement.FormId == formId)
                    return formElement;

            throw new ArgumentException(LlmInstruction.Format(
                @"Form not found: {0}. Use skyline_get_open_forms to see available forms.",
                formId));
        }

        /// <summary>
        /// Returns the shared MCP temp directory, creating it if needed.
        /// Respects the SKYLINE_MCP_TMP_DIR environment variable.
        /// </summary>
        public static string GetMcpTmpDir()
        {
            string tmpDir = Environment.GetEnvironmentVariable(@"SKYLINE_MCP_TMP_DIR");
            if (string.IsNullOrEmpty(tmpDir))
            {
                tmpDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Skyline", @"mcp", @"tmp");
            }
            Directory.CreateDirectory(tmpDir);
            return tmpDir;
        }

        /// <summary>
        /// Generates a timestamped file path in the MCP temp directory.
        /// Format: {prefix}-{sanitized_title}-{yyyyMMdd-HHmmss}{extension}
        /// </summary>
        public static string GetMcpTmpFilePath(string prefix, string title, string extension)
        {
            string safe = Regex.Replace(title ?? string.Empty, @"[^\w\-. ]", @"_").Trim();
            if (safe.Length > 50) safe = safe.Substring(0, 50);
            string timestamp = DateTime.Now.ToString(@"yyyyMMdd-HHmmss");
            return Path.Combine(GetMcpTmpDir(),
                string.Format(@"{0}-{1}-{2}{3}", prefix, safe, timestamp, extension));
        }
    }
}
