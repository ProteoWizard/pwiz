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
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using Newtonsoft.Json.Linq;
using pwiz.Common.GUI;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
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
        /// Executes an action on the UI thread.
        /// Exceptions propagate to the caller via wrapping to preserve the original stack trace.
        /// </summary>
        public static void InvokeOnUiThread(Action action)
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
            });
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
        public static T InvokeOnUiThread<T>(Func<T> func)
        {
            T result = default(T);
            DispatchToUiThread(() => result = func());
            return result;
        }

        /// <summary>
        /// Marshals an action onto the UI thread. Uses the main window when it exists; otherwise the
        /// UI-thread synchronization context captured at startup (see
        /// <see cref="Program.UiSynchronizationContext"/>), so the form-introspection verbs work
        /// while only the StartPage is showing, before the main window has been created. Must be
        /// called off the UI thread.
        /// </summary>
        private static void DispatchToUiThread(Action action)
        {
            var mainWindow = Program.MainWindow;
            if (mainWindow != null && !mainWindow.IsDisposed)
            {
                mainWindow.Invoke(action);
                return;
            }
            var syncContext = Program.UiSynchronizationContext;
            if (syncContext == null)
                throw new InvalidOperationException(
                    @"No UI thread is available to handle this request.");
            syncContext.Send(_ => action(), null);
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
        /// <see cref="InvokeOnUiThread(System.Action)"/>, so a later synchronous read sees this action's
        /// effect (the queue is FIFO). The action is counted in <see cref="UnfinishedActionCount"/> from when
        /// it is posted until its delegate returns. Must be called off the UI thread.
        /// </summary>
        public static void BeginInvokeOnUiThread(Action action)
        {
            Interlocked.Increment(ref _unfinishedActionCount);
            void Run()
            {
                try { action(); }
                finally { Interlocked.Decrement(ref _unfinishedActionCount); }
            }
            var mainWindow = Program.MainWindow;
            if (mainWindow != null && !mainWindow.IsDisposed)
            {
                mainWindow.BeginInvoke((Action) Run);
                return;
            }
            var syncContext = Program.UiSynchronizationContext;
            if (syncContext == null)
            {
                // The action will never run, so it is not pending after all.
                Interlocked.Decrement(ref _unfinishedActionCount);
                throw new InvalidOperationException(
                    @"No UI thread is available to handle this request.");
            }
            syncContext.Post(_ => Run(), null);
        }

        /// <summary>
        /// Whether there is a UI thread to marshal to -- either a live main window or the captured
        /// synchronization context (the StartPage case, before the main window exists).
        /// </summary>
        private static bool HasUiDispatch()
        {
            var mainWindow = Program.MainWindow;
            return (mainWindow != null && !mainWindow.IsDisposed) || Program.UiSynchronizationContext != null;
        }

        private const int DIALOG_POLL_INTERVAL_MILLIS = 100;

        // Ambient "is the calling client still connected?" check, set per request by the pipe server.
        // ThreadStatic because RunWithDialogWatch's poll loop runs on the request's server thread.
        // Lets a long-running verb bail when the client disconnects, so the single-instance server
        // can move on to the next connection instead of staying blocked.
        [ThreadStatic] private static Func<bool> _clientConnectedCheck;

        public static void SetClientConnectedCheck(Func<bool> check)
        {
            _clientConnectedCheck = check;
        }

        // A CancellationTokenSource that fires when the calling client disconnects, so a long copy/read
        // abandons instead of pinning the single-instance server. It polls the connected-check the pipe
        // server installs; the caller passes that check (it is ThreadStatic to the server thread, so it
        // must be read before any thread hop) and it is null in non-pipe contexts (tests), in which
        // case the token simply never fires from a disconnect.
        private sealed class ClientDisconnectCancellation : IDisposable
        {
            private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
            private readonly Thread _pollThread;

            public ClientDisconnectCancellation(Func<bool> connectedCheck)
            {
                if (connectedCheck == null)
                    return;
                _pollThread = new Thread(() =>
                {
                    // WaitOne returns true once the token is cancelled (the work finished, in Dispose);
                    // false on timeout, when we re-check whether the client is still connected.
                    while (!_cancellationTokenSource.Token.WaitHandle.WaitOne(DIALOG_POLL_INTERVAL_MILLIS))
                    {
                        if (!connectedCheck())
                        {
                            _cancellationTokenSource.Cancel();
                            return;
                        }
                    }
                })
                {
                    IsBackground = true,
                    Name = @"AI Connector disconnect poll"
                };
                _pollThread.Start();
            }

            public CancellationToken Token => _cancellationTokenSource.Token;

            public void Dispose()
            {
                _cancellationTokenSource.Cancel(); // stop the poll thread, then wait for it to exit
                _pollThread?.Join();
                _cancellationTokenSource.Dispose();
            }
        }

        /// <summary>
        /// Runs <paramref name="work"/> on a background thread and waits for it, but returns
        /// immediately if a MODAL dialog appears that blocks one of this process's windows -- so the
        /// call never hangs behind a modal it cannot get past. A <see cref="LongWaitDlg"/> is the
        /// exception: it is a progress dialog the work itself drives, so the watch keeps waiting for
        /// it. When a blocking modal appears, a <see cref="CommonAlertDlg"/> throws its text (so the
        /// caller sees the message); any other modal (native Open/Save dialog, custom dialog) returns,
        /// leaving it open for the caller to drive (GetOpenForms / SetFormValue / ClickFormButton).
        /// Used by verbs that can pop a dialog (RunCommand, SetFormValue, ClickFormButton, ...). Must
        /// be called off the UI thread.
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
                if (_clientConnectedCheck != null && !_clientConnectedCheck())
                    return result; // client disconnected -- abandon the (possibly blocked) work so the server can move on
                var newModals = FindModalDialogWindows().Where(h => !knownModals.Contains(h)).ToList();
                if (newModals.Count == 0)
                    continue;
                if (ShouldKeepWaiting(newModals, out var alertText))
                {
                    // Every new modal is a LongWaitDlg progress dialog; ignore them and keep waiting.
                    knownModals.UnionWith(newModals);
                    continue;
                }
                if (alertText != null)
                    throw new InvalidOperationException(alertText);
                return result; // a blocking modal (e.g. a file dialog) is up; return rather than block
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

        // Classifies newly-appeared modal dialogs on the UI thread. Returns true when every new modal is a
        // LongWaitDlg (progress) and the watch should keep waiting; otherwise false, with alertText set to a
        // CommonAlertDlg's text (the caller throws it) or null for a native / other modal (the caller returns
        // and lets the model drive the dialog).
        private static bool ShouldKeepWaiting(IList<IntPtr> newModals, out string alertText)
        {
            var decision = InvokeOnUiThread(() =>
            {
                foreach (var hwnd in newModals)
                {
                    var form = FormUtil.OpenForms.OfType<Form>()
                        .FirstOrDefault(f => f.IsHandleCreated && f.Handle == hwnd);
                    if (form is LongWaitDlg)
                        continue; // progress dialog -- not a blocker
                    if (form is CommonAlertDlg alert)
                        return (keepWaiting: false, alertText: GetAlertText(alert));
                    return (keepWaiting: false, alertText: (string) null); // native / other modal -- caller drives it
                }
                return (keepWaiting: true, alertText: (string) null);
            });
            alertText = decision.alertText;
            return decision.keepWaiting;
        }

        private static string GetAlertText(CommonAlertDlg alert)
        {
            return string.IsNullOrEmpty(alert.DetailMessage)
                ? alert.Message
                : alert.Message + Environment.NewLine + alert.DetailMessage;
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
            new FormElement(Program.MainWindow, CancellationToken.None).InvokeMenuItem(menuPath);
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
            ResolveForm(formId, CancellationToken.None).ClickButton(button);
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
                new FormElement(FindFormById(formId), CancellationToken.None)
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
            ResolveForm(formId, CancellationToken.None).SetValue(controlId, value);
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
                result = InvokeOnUiThread(() =>
                    new FormElement(FindFormById(formId), CancellationToken.None)
                        .FindElement(controlId, UiActions.GetValue).Value);
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
            // Resolve + gate the grid synchronously, then paste fire-and-forget: a void action, so a type
            // conversion alert the paste raises becomes a form the caller drives rather than blocking here.
            var grid = InvokeOnUiThread(() =>
            {
                var g = new FormElement(FindFormById(formId), CancellationToken.None).FindGrid(controlId);
                VerifyInteractable(g);
                return g;
            });
            BeginInvokeOnUiThread(() => grid.SetGridText(text ?? string.Empty));
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
            // Resolve + gate synchronously, then move the cell fire-and-forget (a void action).
            var grid = InvokeOnUiThread(() =>
            {
                var g = new FormElement(FindFormById(formId), CancellationToken.None).FindGrid(controlId);
                VerifyInteractable(g);
                return g;
            });
            BeginInvokeOnUiThread(() => grid.SetCurrentCellAddress(column, row));
        }

        // Finds the grid to act on: the one named controlId, or -- when controlId is null/empty -- the
        // single grid on the form, resolved through the shared element finder. A grid supports the grid
        // actions and is matched by its control name (it has no caption -- see GridElement.MatchesText), so
        // FindElement picks it out of the form's controls just like any other control. The factory wraps a
        // bound inner grid (a DataboundGridControl's BoundDataGridView) as a BoundGridElement with the rich
        // copy/paste path, and a standalone DataGridView as a plain GridElement; the DataboundGridControl
        // itself is a transparent container the walk descends through to reach the inner grid.
        /// <summary>
        /// Returns all the text in a grid on a form -- the column headers followed by every data row --
        /// as tab-separated columns and newline-separated rows. Works for a DataboundGridControl (the
        /// same content as Copy All) and for a plain DataGridView. See <see cref="IJsonToolService"/>.
        /// </summary>
        public static string GetGridText(string formId, string gridId)
        {
            ValidateFormIdFormat(formId);
            // Capture the disconnect check on this (server) thread before any marshaling, so a large
            // copy is abandoned if the client goes away rather than pinning the single-instance server.
            using (var cancellation = new ClientDisconnectCancellation(_clientConnectedCheck))
            {
                // A value read: run it synchronously inside the dialog-watch so it does not hang if a modal
                // is up. The FormElement carries the disconnect token; the grid it produces reads with it.
                string text = null;
                RunWithDialogWatch(() =>
                {
                    text = InvokeOnUiThread(() =>
                        new FormElement(FindFormById(formId), cancellation.Token).FindGrid(gridId).GetGridText());
                    return true;
                });
                if (text == null)
                    throw new OperationCanceledException(LlmInstruction.Format(
                        @"Reading the grid {0} was cancelled.", formId));
                return text;
            }
        }

        /// <summary>
        /// Closes an open form: a dialog, a docked or floating tool window (e.g. the Document Grid or
        /// Audit Log), or a native dialog (which is cancelled) -- see <see cref="IJsonToolService"/>.
        /// </summary>
        public static void CloseForm(string formId)
        {
            ResolveForm(formId, CancellationToken.None).Close();
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
            return ResolveForm(formId, CancellationToken.None).GetControls();
        }

        internal static string NullIfEmpty(string text) => string.IsNullOrEmpty(text) ? null : text;

        // The label as a caller would type it: mnemonic '&' and a trailing colon/ellipsis removed, so a
        // "Name:" label is reported (and addressable) as "Name". Matching tolerates either form anyway.
        internal static string CleanLabel(string text) =>
            string.IsNullOrEmpty(text) ? text : NormalizeLabel(text).TrimEnd(' ', ':');

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
            // Capture the disconnect check on this (server) thread before any marshaling, so a long action
            // (e.g. reading a large grid) is abandoned if the client goes away rather than pinning the
            // single-instance server. The token is carried by the FormElement, which hands it to every
            // element in its tree, so the action reaches it without a parameter.
            using (var cancellation = new ClientDisconnectCancellation(_clientConnectedCheck))
            {
                // The path's root names a form; resolve it (managed or native) and let it perform the action
                // in its own thread context (a managed form on the UI thread inside the dialog-watch; a native
                // dialog on this calling thread). get_actions/get_children are ordinary reads -- the action's
                // Invoke returns the element's SupportedActions / GetChildren(), whose child paths are
                // parentless (the caller knows the parent it asked about).
                return ResolveForm(RootFormText(path), cancellation.Token).PerformAction(path, uiAction, value);
            }
        }

        // Runs a resolved action with the threading its ReturnsValue declares. A void action (a click, a
        // value set) is posted to the UI thread fire-and-forget: the verb returns at once, and a gesture that
        // opens a modal does not block -- the modal is driven by later commands, exactly like the
        // main-menu-item path. A value action (get_value, get_grid_text) must be waited for, so it is run
        // synchronously on the UI thread inside the dialog-watch (which returns rather than hanging if a modal
        // is up). The element resolution / gating has already happened synchronously, so a bad path or a
        // disabled control still fails before this. Must be called off the UI thread.
        internal static object ExecuteAction(UiAction action, UiElement element, object value)
        {
            if (!action.ReturnsValue)
            {
                BeginInvokeOnUiThread(() => action.Invoke(element, value));
                return null;
            }
            object result = null;
            RunWithDialogWatch(() =>
            {
                result = InvokeOnUiThread(() => action.Invoke(element, value));
                return true;
            });
            return result;
        }

        // Verifies the resolved element supports the action (it is the kind the action targets) and, for an
        // action that requires it, that the element is interactable (not blocked by a modal, and enabled).
        // Returns the element; throws a clear error (listing the element's actions) if it does not apply.
        internal static UiElement RequireAction(UiElement element, UiAction action)
        {
            if (!action.AppliesTo(element))
                throw new ArgumentException(LlmInstruction.Format(
                    @"The control '{0}' does not support the action '{1}'. It supports: {2}.",
                    element.Label ?? element.Name, action.SnakeCaseName,
                    string.Join(@", ", element.SupportedActions.Select(a => a.SnakeCaseName))));
            if (action.MustBeEnabled)
                VerifyInteractable(element);
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
        // native dialog. Native dialogs are matched first, on this (pipe) thread (their UI thread is busy in
        // the modal loop, so they are enumerated via UI Automation, never marshalled); a managed form is then
        // found on the UI thread. Throws if no open window has the id.
        private static IFormElement ResolveForm(string formId, CancellationToken cancellationToken)
        {
            ValidateFormIdFormat(formId);
            foreach (var dialog in NativeDialog.GetOpenDialogs())
                if (dialog.FormId == formId)
                    return dialog;
            return InvokeOnUiThread(() => new FormElement(FindFormById(formId), cancellationToken));
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
            // Guard against there being no desktop session before resolving the form, so a missing UI thread
            // returns the "try again" message rather than failing to find the form.
            if (!HasUiDispatch())
                return LLM_MSG_SCREEN_CAPTURE_UNAVAILABLE;
            // ResolveForm throws "form not found" before any permission prompt, so a bad id never prompts.
            var form = ResolveForm(formId, CancellationToken.None);
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
            if (!HasUiDispatch())
                return new ImageBytesMetadata { Message = LLM_MSG_SCREEN_CAPTURE_UNAVAILABLE };
            var form = ResolveForm(formId, CancellationToken.None);
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
        //
        // Note: when there is no UI dispatch yet we use LLM_MSG_SCREEN_CAPTURE_UNAVAILABLE
        // for all variants, including graphs. The wording is slightly off for
        // graphs (which do not capture from the screen), but the user-facing
        // intent ("try again momentarily") is correct.
        private static string CheckImageToolPreflight(string id, Action ensureExistsOnUi, bool requiresScreenCapture)
        {
            ValidateFormIdFormat(id);
            if (!HasUiDispatch())
                return LLM_MSG_SCREEN_CAPTURE_UNAVAILABLE;
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

        // The element gate: gate the modal-block + enabled state of a resolved element before a verb acts
        // on it. A control-backed element gates its form (modal/disabled) AND the control itself
        // (Control.Enabled also reflects a disabled ancestor); a non-control element checks its own state.
        internal static void VerifyInteractable(UiElement element)
        {
            if (element is ControlElement controlElement)
            {
                var control = controlElement.Control;
                var form = control.FindForm();
                if (form != null)
                    VerifyFormInteractable(form);
                VerifyEnabled(control);
                return;
            }
            if (!element.IsEnabled)
                throw new InvalidOperationException(LlmInstruction.Format(
                    @"'{0}' is disabled.", element.Label ?? element.Name));
        }

        // Guards that the connector never does anything a user could not. Two concerns make a target
        // interactable: (1) no modal dialog is blocking its window, and (2) the target itself is enabled.
        // They are split into VerifyFormInteractable (the form/modal gate) and VerifyEnabled (the target
        // gate); VerifyInteractable(control) combines them for the common single-control case so a verb
        // has one obvious call and cannot enforce only half. Every verb routes through these helpers
        // rather than re-checking inline. Must run on the UI thread (the modal check reads a window handle).

        // The form gate: throws if a modal dialog is blocking the form's window, or the form is disabled.
        // The modal check must use the Win32 enabled state of the TOP-LEVEL window (e.g. the main window
        // for a docked form): showing a modal dialog calls EnableWindow(false) on the other windows
        // WITHOUT flipping their managed Control.Enabled, so a managed-only check would miss it.
        internal static void VerifyFormInteractable(Form form)
        {
            var topLevel = TopLevelFormOf(form);
            if (!User32.IsWindowEnabled(topLevel.Handle))
                throw new InvalidOperationException(LlmInstruction.Format(
                    @"Cannot interact with form '{0}': a modal dialog is blocking it. Handle the open dialog first (see skyline_get_open_forms).",
                    GetFormId(form)));

            if (!form.Enabled)
                throw new InvalidOperationException(LlmInstruction.Format(
                    @"Form '{0}' is disabled.", GetFormId(form)));
        }

        // The target gate for a control: throws if it (or, via Control.Enabled, an ancestor) is disabled.
        private static void VerifyEnabled(Control control)
        {
            if (!control.Enabled)
                throw new InvalidOperationException(LlmInstruction.Format(
                    @"Control '{0}' is disabled.", control.Name));
        }

        // The top-level form hosting a control (e.g. the main window for a docked form). FindForm on a
        // form returns itself, so each step goes through Parent first to climb past the current form.
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

        // True when the requested button names the cancel/close action of a native dialog. Locale
        // sensitive by nature; callers that key on the visible label inherit that.
        internal static bool IsCancelAction(string button)
        {
            var normalized = NormalizeLabel(button);
            return string.Equals(normalized, @"Cancel", StringComparison.CurrentCultureIgnoreCase)
                || string.Equals(normalized, @"Close", StringComparison.OrdinalIgnoreCase);
        }

        // Strips the mnemonic '&' and a trailing ellipsis/period so menu and button captions compare
        // equal to the plain label a tutorial uses ("Peptide Search" == "&Peptide Search...").
        private static string NormalizeLabel(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            return text.Replace(@"&", string.Empty).Trim().TrimEnd('.', '…', ' ').Trim();
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
