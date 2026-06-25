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
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
        /// otherwise (null) the main window. Most callers go through the <see cref="UiElement"/> methods.
        /// </summary>
        public static void InvokeOnUiThread(Action action, Control dispatcher = null)
        {
            Exception caught = null;
            DispatchToUiThread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            }, dispatcher);
            if (caught is ArgumentException argEx)
                throw new ArgumentException(argEx.Message, argEx.ParamName, argEx);
            if (caught != null)
                ExceptionUtil.WrapAndThrowException(caught);
        }

        /// <summary>
        /// Executes a function on the UI thread and returns the result.
        /// Must be called from a background thread (pipe server thread).
        /// Exceptions propagate to the caller.
        /// </summary>
        public static T InvokeOnUiThread<T>(Func<T> func, Control dispatcher = null)
        {
            T result = default(T);
            DispatchToUiThread(() => result = func(), dispatcher);
            return result;
        }

        /// <summary>
        /// Marshals an action onto the UI thread. When a <paramref name="dispatcher"/> control with a live
        /// handle is given, it is used (so a form on its own thread is driven through its own message loop);
        /// otherwise <see cref="Program.InvokeOnUiThread"/> targets the main window, or -- before it exists --
        /// the StartPage, so the form-introspection verbs work while only the StartPage is showing. Must be
        /// called off the UI thread.
        /// </summary>
        private static void DispatchToUiThread(Action action, Control dispatcher = null)
        {
            if (dispatcher != null && dispatcher.IsHandleCreated && !dispatcher.IsDisposed)
            {
                dispatcher.Invoke(action);
                return;
            }
            Program.InvokeOnUiThread(action);
        }

        private static int _unfinishedActionCount;

        /// <summary>
        /// The number of fire-and-forget actions (posted by <see cref="BeginInvokeOnUiThread"/>) that have
        /// been posted but have not yet finished running. Incremented when an action is posted and
        /// decremented when its delegate returns -- so an action that opens a modal dialog stays counted
        /// until that modal closes (its delegate is blocked in the modal's message loop). It is therefore
        /// usually equal to the number of modal dialogs the connector's actions have raised and left open,
        /// and a caller can poll it to wait until pending sets/clicks have actually been applied.
        /// </summary>
        public static int UnfinishedActionCount => Volatile.Read(ref _unfinishedActionCount);

        /// <summary>
        /// Posts an action to the UI thread fire-and-forget (BeginInvoke, not Invoke): the caller returns at
        /// once and does not wait for or observe the result. Used for a void action (a click, a value set)
        /// so a gesture that opens a modal dialog does not block -- the modal is driven by later commands,
        /// the same way the main-menu-item path posts its click. Posted on the same (main-window) queue as
        /// <see cref="InvokeOnUiThread"/>, so a later synchronous read sees this action's
        /// effect (the queue is FIFO). The action is counted in <see cref="UnfinishedActionCount"/> from when
        /// it is posted until its delegate returns. Must be called off the UI thread.
        /// </summary>
        public static void BeginInvokeOnUiThread(Action action, Control dispatcher = null)
        {
            Interlocked.Increment(ref _unfinishedActionCount);
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
                finally { Interlocked.Decrement(ref _unfinishedActionCount); }
            }
            try
            {
                // A form on its own thread (e.g. BackgroundThreadLongWaitDlg) is posted to through its own
                // BeginInvoke; otherwise Program.BeginInvokeOnUiThread targets the main window, or the
                // StartPage before it exists (and throws if neither is up).
                if (dispatcher != null && dispatcher.IsHandleCreated && !dispatcher.IsDisposed)
                    dispatcher.BeginInvoke((Action) Run);
                else
                    Program.BeginInvokeOnUiThread(Run);
            }
            catch
            {
                // Posting failed, so the action will never run and is not pending after all.
                Interlocked.Decrement(ref _unfinishedActionCount);
                throw;
            }
        }

        private const int DIALOG_POLL_INTERVAL_MILLIS = 100;

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
        public static T RunWithDialogWatch<T>(Func<T> work)
        {
            var knownModals = new HashSet<IntPtr>(FindModalDialogWindows());

            T result = default(T);
            Exception workError = null;
            // Capture the worker's exception here rather than letting it escape, so the RunAsync
            // reporter does not surface its own error dialog.
            var worker = ActionUtil.RunAsync(() =>
            {
                try { result = work(); }
                catch (Exception ex) { workError = ex; }
            }, @"JsonTool command");

            while (!worker.Join(DIALOG_POLL_INTERVAL_MILLIS))
            {
                var newModals = FindModalDialogWindows().Where(h => !knownModals.Contains(h)).ToList();
                if (newModals.Count == 0)
                    continue;
                var blockingMessage = InvokeOnUiThread(() => FirstBlockingDialogMessage(newModals));
                if (blockingMessage == null)
                {
                    // Every new modal is a LongWaitDlg progress dialog; ignore them and keep waiting.
                    knownModals.UnionWith(newModals);
                    continue;
                }
                // A modal is blocking (an alert/error, or any other dialog including a native one); surface its
                // message so the caller sees what is in the way instead of hanging.
                throw new InvalidOperationException(blockingMessage);
            }

            if (workError != null)
            {
                // Preserve ArgumentException (maps to an invalid-params error) like InvokeOnUiThread.
                if (workError is ArgumentException argEx)
                    throw new ArgumentException(argEx.Message, argEx.ParamName, argEx);
                ExceptionUtil.WrapAndThrowException(workError);
            }
            return result;
        }

        public static void RunWithDialogWatch(Action work)
        {
            RunWithDialogWatch(() => { work(); return true; });
        }

        // The message a blocking modal dialog shows, or null for a LongWaitDlg (a progress dialog that does not
        // block): a CommonAlertDlg's or ReportErrorDlg's text, or any other dialog's title -- a managed Form's
        // caption, or, for a native window (no managed form), its window title.
        // The message a blocking modal dialog shows: a CommonFormEx's DetailedMessage (a CommonAlertDlg's
        // composed text, or the ReportErrorDlg's exception message), else any other managed dialog's title,
        // else (a native window with no managed form) its window title.
        private static string ModalDialogMessage(IntPtr hwnd, Form form)
        {
            return (form as CommonFormEx)?.DetailedMessage ?? form?.Text ?? GetWindowTitle(hwnd);
        }

        // The message of the first of the given modal windows that is actually blocking, or null if none
        // blocks (every one is a LongWaitDlg, a progress dialog the work itself drives). The shared core of
        // BlockingAlertMessage (the form gate) and the dialog-watch. Reads the open forms, so it must run on
        // the UI thread.
        private static string FirstBlockingDialogMessage(IEnumerable<IntPtr> modalHandles)
        {
            var forms = FormUtil.OpenForms.Where(f => f.IsHandleCreated).ToDictionary(f => f.Handle);
            foreach (var hwnd in modalHandles)
            {
                forms.TryGetValue(hwnd, out var form);
                if (form is LongWaitDlg)
                    continue; // a progress dialog -- not a blocker; keep waiting
                return ModalDialogMessage(hwnd, form);
            }
            return null;
        }

        // The title of a window (used for a native dialog, which has no managed Form), or empty if it has none.
        private static string GetWindowTitle(IntPtr hwnd)
        {
            var buffer = new StringBuilder(256);
            return User32.GetWindowText(hwnd, buffer, buffer.Capacity) > 0 ? buffer.ToString() : string.Empty;
        }

        // The message of a dialog currently blocking the UI -- a CommonAlertDlg's or ReportErrorDlg's text, or
        // any other modal dialog's title (a managed caption or a native window's title), but never a LongWaitDlg
        // progress dialog. Null if nothing is blocking, so the form gate can tell the caller what is in the way.
        // Must run on the UI thread (it reads the open forms).
        internal static string BlockingAlertMessage()
        {
            return FirstBlockingDialogMessage(FindModalDialogWindows());
        }

        // Returns the handles of this process's modal dialogs: visible, enabled, top-level windows
        // whose owner window is disabled -- the signature of a modal dialog blocking its owner.
        private static IList<IntPtr> FindModalDialogWindows()
        {
            var processId = (uint)Process.GetCurrentProcess().Id;
            var result = new List<IntPtr>();
            User32.EnumWindows((hwnd, lparam) =>
            {
                User32.GetWindowThreadProcessId(hwnd, out var windowProcessId);
                if (windowProcessId != processId || !User32.IsWindowVisible(hwnd) || !User32.IsWindowEnabled(hwnd))
                    return true;
                var owner = User32.GetWindow(hwnd, User32.GW_OWNER);
                if (owner != IntPtr.Zero && !User32.IsWindowEnabled(owner))
                    result.Add(hwnd);
                return true;
            }, IntPtr.Zero);
            return result;
        }

        // Level 2: UI patterns

        /// <summary>
        /// Creates a TextWriter that tees output to both the given capture writer
        /// and Skyline's Immediate Window. Shows the Immediate Window and writes
        /// the header before returning.
        /// </summary>
        public static TextWriter CreateImmediateWindowTee(TextWriter capture, string header)
        {
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

        // Level 3: Complete UI operations - Generic form interaction

        /// <summary>
        /// Invokes a main-menu item by its visible path (see <see cref="IJsonToolService"/>). The
        /// item is located on the UI thread (throwing if absent), then its click is posted with
        /// BeginInvoke so a menu item that opens a modal dialog does not block the caller.
        /// </summary>
        public static void InvokeMenuItem(string menuPath)
        {
            // There is no main menu while the StartPage is showing (the main window does not exist
            // yet). Fail with a clear message rather than dereferencing a null main window.
            if (Program.MainWindow == null)
                throw new InvalidOperationException(
                    @"Cannot invoke a menu item: the main Skyline window is not open yet (the StartPage may be showing).");
            // The main menu is the main window's; drive it through that form's element model.
            new FormElement(Program.MainWindow).InvokeMenuItem(menuPath);
        }

        // Fires a context menu's Opening (so items added on demand are present) and returns it, for a
        // ControlElement that surfaces an already-built menu (its own ContextMenuStrip, the Targets-tree
        // menu). The menu is owned elsewhere -- do not dispose it. Runs on the UI thread.
        internal static ContextMenuStrip OpenContextMenu(ContextMenuStrip menu)
        {
            RaiseProtectedHandler(menu, @"OnOpening", new CancelEventArgs());
            return menu;
        }

        // Builds a fresh context menu for a graph through its ContextMenuBuilder, or returns null when the
        // control is not a graph. The graph can be addressed as its ZedGraphControl, or -- since a graph form
        // is just its graph -- as the form itself. Runs on the UI thread.
        internal static ContextMenuStrip TryBuildGraphContextMenu(Control control)
        {
            var zedGraph = control as ZedGraphControl
                ?? (control as DockableFormEx != null ? TryGetZedGraphControl((DockableFormEx) control) : null);
            if (zedGraph == null)
                return null;
            var graphMenu = new ContextMenuStrip();
            PopulateGraphContextMenu(zedGraph, graphMenu);
            return graphMenu;
        }

        // Raises a protected On&lt;Event&gt; method (e.g. DataGridView.OnCellContextMenuStripNeeded,
        // ContextMenuStrip.OnOpening) by reflection, walking up to where it is declared, so the wired
        // handlers run the way the real UI event would.
        internal static void RaiseProtectedHandler(object target, string methodName, object eventArgs)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var method = type.GetMethod(methodName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                    null, new[] { eventArgs.GetType() }, null);
                if (method != null)
                {
                    method.Invoke(target, new[] { eventArgs });
                    return;
                }
            }
            throw new ArgumentException(LlmInstruction.Format(
                @"Could not raise {0} on {1}.", methodName, target.GetType().Name));
        }

        // Populates a ContextMenuStrip the way right-clicking the graph would: it invokes the graph's
        // ContextMenuBuilder handlers (which add the Skyline-specific items) with a point at the
        // control's center, so the correct graph pane is chosen for a multi-pane graph. The event can
        // only be raised from inside ZedGraphControl, so its backing delegate is fetched by reflection.
        private static void PopulateGraphContextMenu(ZedGraphControl zedGraph, ContextMenuStrip menuStrip)
        {
            var field = typeof(ZedGraphControl).GetField(@"ContextMenuBuilder",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var builder = field?.GetValue(zedGraph) as ZedGraphControl.ContextMenuBuilderEventHandler;
            if (builder == null)
                throw new ArgumentException(new LlmInstruction(@"This graph has no context menu."));
            var centerPoint = new System.Drawing.Point(zedGraph.Width / 2, zedGraph.Height / 2);
            builder(zedGraph, menuStrip, centerPoint, default(ZedGraphControl.ContextMenuObjectState));
        }

        /// <summary>
        /// Clicks a control on an open form -- a Button, a CheckBox or RadioButton, a custom
        /// IButtonControl (e.g. a StartPage tile), a ToolStrip/menu/toolbar item, or any other clickable
        /// control (matched by its visible label) -- or accepts/cancels a native dialog (see
        /// <see cref="IJsonToolService"/>).
        /// </summary>
        public static void ClickFormButton(string formId, string button)
        {
            ResolveForm(formId).ClickButton(button);
        }

        /// <summary>
        /// Clicks an item on a form's ToolStrip (toolbar / menu strip) by its path, e.g.
        /// "Reports &gt; Replicates" -- the toolbar button "Reports" then the "Replicates" item in its
        /// dropdown. Each level's dropdown is opened first so that items built on demand (which are not
        /// in the static DropDownItems, e.g. the Document Grid's Reports list) are present before the
        /// item is matched. Each segment is matched by item name or visible text, like InvokeMenuItem.
        /// </summary>
        public static void ClickToolStripItem(string formId, string menuPath)
        {
            ValidateFormIdFormat(formId);
            // The path-walk, matching, gating, and click all live in ToolStripElement.ClickMenuItem; the
            // first segment is a top-level item on one of the form's toolstrips, so try each until one has it.
            var toolStrips = InvokeOnUiThread(() =>
                new FormElement(FindFormById(formId))
                    .SelfAndDescendants().OfType<ToolStripElement>().ToList());
            if (!toolStrips.Any(toolStrip => toolStrip.ClickMenuItem(menuPath)))
                throw new ArgumentException(LlmInstruction.Format(
                    @"Toolbar item not found on form {0}: {1}.", formId, menuPath));
        }

        /// <summary>
        /// Sets a control's value on an open form, or the file name(s) on a native file dialog
        /// (see <see cref="IJsonToolService"/>).
        /// </summary>
        public static void SetFormValue(string formId, string controlId, string value)
        {
            ResolveForm(formId).SetValue(controlId, value);
        }

        /// <summary>
        /// Returns the current value of a control on a form, found by its Label: a text box's text, a combo
        /// box's selected item, a check/radio's checked state, or a CheckedListBox's checked items (their
        /// text, one per line). See <see cref="IJsonToolService"/>.
        /// </summary>
        public static string GetFormValue(string formId, string controlId)
        {
            ValidateFormIdFormat(formId);
            // A value read: run it synchronously inside the dialog-watch so it does not hang if a modal is up.
            string result = null;
            RunWithDialogWatch(() =>
            {
                result = OnFormThread(formId,
                    formElement => formElement.FindElement(controlId, UiActions.GetValue).Value?.ToString());
                return true;
            });
            return result;
        }

        /// <summary>
        /// Pastes tab-separated <paramref name="text"/> into a grid on a form, starting at its current
        /// cell -- move there first with <see cref="SetCurrentCellAddress"/> (the anchor a user would click). The
        /// text may be a multi-cell TSV block (it fills down and to the right). Works for a
        /// DataboundGridControl (e.g. the Document Grid) and for a plain DataGridView (e.g. the Rule Set
        /// Editor's rules grid). See <see cref="IJsonToolService"/>.
        /// </summary>
        public static void SetGridText(string formId, string controlId, string text)
        {
            ValidateFormIdFormat(formId);
            // Resolve the grid synchronously; the action's Invoke gates it and pastes fire-and-forget, so a
            // type conversion alert the paste raises becomes a form the caller drives rather than blocking here.
            var grid = OnFormThread(formId, formElement => formElement.FindGrid(controlId));
            UiActions.SetGridText.Invoke(grid, text ?? string.Empty);
        }

        /// <summary>
        /// Moves the current cell of a grid on a form (move there before pasting with
        /// <see cref="SetGridText"/> or opening the cell's context menu). <paramref name="column"/> is the
        /// visible-column index and <paramref name="row"/> is the row index -- the same indices the grid
        /// reports columns and rows in. See <see cref="IJsonToolService"/>.
        /// </summary>
        public static void SetCurrentCellAddress(string formId, string controlId, int column, int row)
        {
            ValidateFormIdFormat(formId);
            // Resolve the grid synchronously; the action's Invoke gates it and moves the cell fire-and-forget.
            var grid = OnFormThread(formId, formElement => formElement.FindGrid(controlId));
            UiActions.SetCurrentCellAddress.Invoke(grid, new[] { column, row });
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
        /// Closes an open form: a dialog, a docked or floating tool window (e.g. the Document Grid or
        /// Audit Log), or a native dialog (which is cancelled) -- see <see cref="IJsonToolService"/>.
        /// </summary>
        public static void CloseForm(string formId)
        {
            ResolveForm(formId).Close();
        }

        /// <summary>
        /// Lists the controls directly on a form so a caller can discover what is there -- and how to
        /// address it -- without reading the source: this is the form element's <see cref="UiElement.GetChildren"/>.
        /// Each control reports its Name (informational), Type, the visible Label that identifies it, and
        /// its enabled/visible state, with a parentless Path the caller re-parents to act on it (and walks
        /// deeper with the get_children action). Use the get_value / get_actions actions for a control's
        /// value and the actions it supports.
        /// </summary>
        public static ControlInfo[] GetControls(string formId)
        {
            return ResolveForm(formId).GetControls();
        }

        /// <summary>
        /// The most general way to interact with a control, menu item, or list item (see
        /// <see cref="IJsonToolService"/>): resolve the element the <paramref name="path"/> refers to,
        /// then perform <paramref name="action"/> on it. The action determines the value and return types:
        /// "get_actions" -> string[]; "get_children" -> ControlInfo[] (parentless paths the caller
        /// re-parents); "click" -> null; "set_value" (string value) -> null; "get_value" -> string.
        /// </summary>
        public static object PerformAction(UiElementPath path, string action, object value)
        {
            var uiAction = UiActions.ByName(action) ?? throw new ArgumentException(LlmInstruction.Format(
                @"Unsupported action '{0}'. Use get_actions to list the actions a control supports.", action));
            // The path's root names a form; resolve it (managed or native) and let it perform the action in its
            // own thread context (a managed form on the UI thread inside the dialog-watch; a native dialog on
            // this calling thread). get_actions/get_children are ordinary reads -- the action's Invoke returns
            // the element's SupportedActions / GetChildren(), whose child paths are parentless (the caller
            // knows the parent it asked about).
            return ResolveForm(RootFormText(path)).PerformAction(path, uiAction, value);
        }

        // Runs a resolved action, which gates and marshals itself (see UiAction.Invoke): a void action posts
        // fire-and-forget; a value action (get_value, get_grid_text) is run synchronously and its result
        // returned. The value action is run inside the dialog-watch so that if producing the value brings up a
        // modal, the watch surfaces it (or leaves it open) rather than the server blocking on it; a void action
        // is posted and returns at once, so it needs no watch. Must be called off the UI thread.
        internal static object ExecuteAction(UiAction action, UiElement element, object value)
        {
            if (!action.ReturnsValue)
                return action.Invoke(element, value);
            return RunWithDialogWatch(() => action.Invoke(element, value));
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

        // The form id at the root of a path -- the Text of its deepest (Parent-less) segment, or null.
        private static string RootFormText(UiElementPath path)
        {
            while (path?.Parent != null)
                path = path.Parent;
            return path?.Text;
        }

        // Resolves a UiElementPath against an already-resolved root form (the FormElement or NativeDialog the
        // verb is acting on), so the path's root segment maps to that form and the rest walks into it.
        internal static UiElement ResolvePathFrom(UiElementPath path, UiElement root) =>
            ResolvePath(path, _ => root);

        // Resolves a UiElementPath to the single element it refers to. The path is matched segment by
        // segment: each segment names a child of the element its Parent resolves to, by Index (its position
        // in the parent's child list), Text (its visible label), and/or Type -- every property that is set
        // must match. The chain bottoms out at a form, resolved by <paramref name="rootResolver"/> (a
        // WinForms form, or a native dialog) from its id.
        private static UiElement ResolvePath(UiElementPath path, Func<string, UiElement> rootResolver)
        {
            if (path == null)
                throw new ArgumentException(new LlmInstruction(@"A path is required."));

            // The root of a path (no Parent) names a form: its Text is the form id from GetOpenForms (its
            // Type is "Form"). Everything else is a child of the element its Parent resolves to -- the parent
            // resolves the chain, then GetChild picks the element this segment names.
            if (path.Parent == null)
            {
                if (string.IsNullOrEmpty(path.Text))
                    throw new ArgumentException(new LlmInstruction(
                        @"The root of a path must name a form: Text set to the form id from skyline_get_open_forms (Type 'Form')."));
                return rootResolver(path.Text);
            }
            return ResolvePath(path.Parent, rootResolver).GetChild(path);
        }

        // Level 3: Complete UI operations - Graphs

        public static FormInfo[] GetOpenForms()
        {
            var results = InvokeOnUiThread(() =>
            {
                var skylineWindow = Program.MainWindow;
                var formInfos = new List<FormInfo>();
                var dockedForms = new HashSet<Form>();
                // The main window does not exist while the StartPage is showing; skip the docked
                // forms in that case and just enumerate the open forms below (the StartPage and any
                // of its dialogs appear there).
                if (skylineWindow != null)
                {
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
                        });
                    }
                }

                // Enumerate non-docked forms (dialogs, popups)
                foreach (var form in FormUtil.OpenForms)
                {
                    if (form == skylineWindow || dockedForms.Contains(form))
                        continue;
                    if (!form.Visible)
                        continue;
                    formInfos.Add(new FormInfo
                    {
                        Type = form.GetType().Name,
                        Title = GetFormTitle(form),
                        HasGraph = false,
                        DockState = @"Dialog",
                        Id = GetFormId(form),
                    });
                }
                return formInfos;
            });

            // Native common dialogs (e.g. the Open/Save file dialog) are not WinForms forms and
            // so never appear in FormUtil.OpenForms. Enumerate them via UI Automation. This runs
            // on the pipe thread, NOT inside InvokeOnUiThread: when such a dialog is modal the UI
            // thread is busy in the dialog's own message loop, and querying it from that thread
            // can deadlock.
            foreach (var dialog in NativeDialog.GetOpenDialogs())
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
            return results.ToArray();
        }

        // Resolves a formId to the window it addresses -- a WinForms form (FormElement) or a native common
        // dialog (NativeDialog) -- so every verb drives both through IFormElement and none special-cases a
        // native dialog. Native dialogs are matched first, on this (pipe) thread: they are non-managed windows
        // enumerated via UI Automation cross-thread (no Control to marshal through), which runs alongside their
        // modal loop rather than on it. A managed form is then found on the UI thread. Throws if no open window
        // has the id.
        private static IFormElement ResolveForm(string formId)
        {
            ValidateFormIdFormat(formId);
            foreach (var dialog in NativeDialog.GetOpenDialogs())
                if (dialog.FormId == formId)
                    return dialog;
            return InvokeOnUiThread(() => new FormElement(FindFormById(formId)));
        }

        // Resolves the managed form named by formId and runs func against it on that form's own UI thread --
        // the correct thread even for a form created on its own background thread (e.g. a
        // BackgroundThreadLongWaitDlg), whose controls must be touched through its message loop, not the main
        // window's (see UiElement.InvokeOnUiThread). The form lookup runs on the main thread; func then runs on
        // the form's thread, where it walks/reads the control tree. Must be called off the UI thread.
        private static T OnFormThread<T>(string formId, Func<FormElement, T> func)
        {
            return InvokeOnUiThread(() =>
            {
                var formElement = new FormElement(FindFormById(formId));
                return formElement.InvokeOnUiThread(() => func(formElement));
            });
        }

        public static string GetGraphData(string graphId, string filePath)
        {
            return InvokeOnUiThread(() =>
            {
                var form = FindFormById(graphId) as DockableFormEx;
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

        public static string GetFormImage(string formId, string filePath)
        {
            ValidateFormIdFormat(formId);
            // ResolveForm throws "form not found" before any permission prompt, so a bad id never prompts.
            var form = ResolveForm(formId);
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

        public static ImageBytesMetadata GetFormImageBytes(string formId)
        {
            ValidateFormIdFormat(formId);
            var form = ResolveForm(formId);
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
        private static void EnsureGraphForm(string graphId, out DockableFormEx form, out ZedGraphControl graph)
        {
            form = FindFormById(graphId) as DockableFormEx;
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

        private static ZedGraphControl TryGetZedGraphControl(DockableFormEx form)
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

        /// <summary>
        /// Finds a form by its TypeName:Title identifier from GetOpenForms.
        /// Searches docked forms first, then non-docked forms (dialogs).
        /// </summary>
        private static Form FindFormById(string formId)
        {
            ValidateFormIdFormat(formId);
            int colonIndex = formId.IndexOf(':');
            string typeName = formId.Substring(0, colonIndex);
            string title = formId.Substring(colonIndex + 1);

            var skylineWindow = Program.MainWindow;

            // Search docked forms (none while the StartPage is showing -- no main window yet)
            if (skylineWindow != null)
            {
                foreach (var form in skylineWindow.DockPanel.Contents.OfType<DockableFormEx>())
                {
                    var dockState = form.DockState;
                    if (dockState == DockState.Hidden || dockState == DockState.Unknown)
                        continue;
                    if (form.GetType().Name == typeName && GetFormTitle(form) == title)
                        return form;
                }
            }

            // Search non-docked forms (dialogs)
            var dockedForms = skylineWindow != null
                ? new HashSet<Form>(skylineWindow.DockPanel.Contents.OfType<DockableFormEx>())
                : new HashSet<Form>();
            foreach (var form in FormUtil.OpenForms)
            {
                if (form == skylineWindow || dockedForms.Contains(form))
                    continue;
                if (!form.Visible)
                    continue;
                if (form.GetType().Name == typeName && GetFormTitle(form) == title)
                    return form;
            }

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
