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
                var decision = ClassifyNewModals(newModals);
                if (decision.KeepWaiting)
                {
                    // Every new modal is a LongWaitDlg progress dialog; ignore them and keep waiting.
                    knownModals.UnionWith(newModals);
                    continue;
                }
                if (decision.AlertText != null)
                    throw new InvalidOperationException(decision.AlertText);
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

        // Classifies newly-appeared modal dialogs on the UI thread. Returns KeepWaiting when every new
        // modal is a LongWaitDlg (progress), an alert's text when one is a CommonAlertDlg (the caller
        // throws it), or neither (the caller returns and lets the model drive the dialog).
        private static ModalDecision ClassifyNewModals(IList<IntPtr> newModals)
        {
            return InvokeOnUiThread(() =>
            {
                foreach (var hwnd in newModals)
                {
                    var form = FormUtil.OpenForms.OfType<Form>()
                        .FirstOrDefault(f => f.IsHandleCreated && f.Handle == hwnd);
                    if (form is LongWaitDlg)
                        continue; // progress dialog -- not a blocker
                    if (form is CommonAlertDlg alert)
                        return new ModalDecision { AlertText = GetAlertText(alert) };
                    return new ModalDecision(); // native / other modal -- caller drives it
                }
                return new ModalDecision { KeepWaiting = true };
            });
        }

        private class ModalDecision
        {
            public bool KeepWaiting;
            public string AlertText;
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
            var item = InvokeOnUiThread(() =>
            {
                // The main menu lives on the main window; gate it for a modal block / disabled state,
                // then for the item's own enablement -- the same things that stop a user clicking it.
                VerifyFormInteractable(Program.MainWindow);
                var menuItem = FindMenuItem(menuPath);
                VerifyEnabled(menuItem);
                return menuItem;
            });
            Program.MainWindow.BeginInvoke((Action)item.PerformClick);
        }

        /// <summary>
        /// Invokes an item on a right-click context menu by its visible path (e.g. "Normalize To &gt;
        /// None"). The target is given by <paramref name="controlId"/>: empty/null for the form's graph,
        /// or a grid cell locator "<c>grid[column,row]</c>" to right-click that cell (the grid name may
        /// be empty for the form's single grid; row may be -1 for a column header). The menu is built
        /// the way a right-click would build it, then the item is located by path and clicked. Runs on
        /// the UI thread. See <see cref="IJsonToolService"/>.
        /// </summary>
        public static void InvokeContextMenuItem(string formId, string controlId, string menuPath)
        {
            ValidateFormIdFormat(formId);
            RunWithDialogWatch(() =>
            {
                InvokeOnUiThread(() =>
                {
                    var form = FindFormById(formId);

                    if (TryParseGridCell(controlId, out var gridName, out var column, out var row))
                    {
                        VerifyInteractable(GetGridView(form, gridName));
                        InvokeGridCellContextMenuItem(form, gridName, column, row, menuPath);
                    }
                    else if (EnumerateControls(form).OfType<SequenceTree>().FirstOrDefault() is SequenceTree tree)
                    {
                        // The Targets tree has no caption to match; it is identified as the form's tree
                        // (controlId is unused -- pass empty). Used for "Pick Children" etc.
                        VerifyInteractable(tree);
                        InvokeTreeNodeContextMenuItem(menuPath);
                    }
                    else
                    {
                        var zedGraph = form is DockableFormEx graph ? TryGetZedGraphControl(graph) : null;
                        // Gate the graph control when there is one; otherwise still gate the form so a
                        // modal block is reported before the "not a graph form" error.
                        if (zedGraph != null)
                            VerifyInteractable(zedGraph);
                        else
                            VerifyFormInteractable(form);
                        InvokeGraphContextMenuItem(form, menuPath);
                    }
                });
                return true;
            });
        }

        // Invokes an item on the graph's context menu, built the way a right-click would (the graph's
        // ContextMenuBuilder populates a fresh menu). Used when controlId does not name a grid cell.
        private static void InvokeGraphContextMenuItem(Form form, string menuPath)
        {
            var zedGraph = form is DockableFormEx graph ? TryGetZedGraphControl(graph) : null;
            if (zedGraph == null)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Not a graph form: {0}. A graph context menu needs HasGraph=True; for a grid cell pass a controlId like ""grid[col,row]"" (see skyline_get_open_forms).",
                    GetFormId(form)));
            using (var menuStrip = new ContextMenuStrip())
            {
                PopulateGraphContextMenu(zedGraph, menuStrip);
                var item = FindMenuItemIn(menuStrip.Items, menuPath);
                VerifyEnabled(item);
                item.PerformClick();
            }
        }

        // Invokes an item on a grid cell's right-click context menu. Raises the grid's
        // CellContextMenuStripNeeded for the target cell (as a right-click does) to obtain the menu,
        // fires its Opening so on-demand items are built, then clicks the item by path.
        private static void InvokeGridCellContextMenuItem(Form form, string gridName, int column, int row, string menuPath)
        {
            var dataGridView = GetGridView(form, gridName);
            var visibleColumns = VisibleGridColumns(dataGridView);
            if (column < 0 || column >= visibleColumns.Length)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Column {0} is out of range; the grid has {1} visible columns.", column, visibleColumns.Length));
            int columnIndex = visibleColumns[column].Index;
            if (row >= 0 && row < dataGridView.Rows.Count)
                dataGridView.CurrentCell = dataGridView.Rows[row].Cells[columnIndex];
            var args = new DataGridViewCellContextMenuStripNeededEventArgs(columnIndex, row);
            RaiseProtectedHandler(dataGridView, @"OnCellContextMenuStripNeeded", args);
            var menuStrip = args.ContextMenuStrip;
            if (menuStrip == null)
                throw new ArgumentException(LlmInstruction.Format(
                    @"The cell at column {0}, row {1} has no context menu.", column, row));
            RaiseProtectedHandler(menuStrip, @"OnOpening", new CancelEventArgs());
            var item = FindMenuItemIn(menuStrip.Items, menuPath);
            VerifyEnabled(item);
            item.PerformClick();
        }

        // Invokes an item on the Targets tree's right-click context menu (e.g. "Pick Children", which
        // opens the pick-list popup). The tree's ContextMenuStrip is shown manually rather than wired to
        // the control, so it is fetched from the main window; its Opening is fired (the way a right-click
        // would) so item enablement is computed for the currently selected node. Select the node first
        // (skyline_set_item_selected on the tree) -- items like Pick Children act on the selection.
        private static void InvokeTreeNodeContextMenuItem(string menuPath)
        {
            var menuStrip = Program.MainWindow.ContextMenuTreeNode;
            RaiseProtectedHandler(menuStrip, @"OnOpening", new CancelEventArgs());
            var item = FindMenuItemIn(menuStrip.Items, menuPath);
            if (!item.Enabled)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Tree context-menu item '{0}' is disabled for the current selection. Select the target node first (skyline_set_item_selected on the tree).", menuPath));
            item.PerformClick();
        }

        // The DataGridView of the grid named gridName on the form (its DataboundGridControl's inner
        // grid, or a standalone DataGridView), or the form's single grid when gridName is empty.
        private static DataGridView GetGridView(Form form, string gridName)
        {
            return FindGrid(form, gridName).DataGridView;
        }

        // Parses a grid-cell locator "name[column,row]" (the name is optional -> the form's single
        // grid). Returns false for a plain control id (no "[col,row]" suffix). column/row are
        // zero-based indices into the grid's visible columns and its rows; row may be -1 for a header.
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

        // Raises a protected On&lt;Event&gt; method (e.g. DataGridView.OnCellContextMenuStripNeeded,
        // ContextMenuStrip.OnOpening) by reflection, walking up to where it is declared, so the wired
        // handlers run the way the real UI event would.
        private static void RaiseProtectedHandler(object target, string methodName, object eventArgs)
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
        /// IButtonControl (e.g. a StartPage tile), a ToolStrip/menu/toolbar item, an item in a
        /// CheckedListBox (its check is toggled), or any other control (matched by control name or
        /// visible text) -- or accepts/cancels a native dialog (see <see cref="IJsonToolService"/>).
        /// </summary>
        public static void ClickFormButton(string formId, string button)
        {
            ValidateFormIdFormat(formId);
            if (TryGetNativeDialog(formId, out var dialog))
            {
                if (!User32.IsWindowEnabled(dialog.WindowHandle))
                    throw new InvalidOperationException(LlmInstruction.Format(
                        @"Cannot interact with native dialog '{0}' because it is blocked.", formId));

                if (IsCancelAction(button))
                    dialog.Cancel();
                else
                    dialog.Accept();
                return;
            }
            // Resolve the click target on the UI thread, then act inside the dialog-watch so a dialog
            // the click pops is observed: a resulting alert is surfaced (throws its text) and a native
            // dialog (e.g. Save/Open) returns immediately rather than blocking on its modal. Each element
            // knows how to click itself (a button via BM_CLICK so an AutoCheck=false checkbox still
            // toggles; a menu/toolbar item or tile via PerformClick; a tab by selecting it; a
            // CheckedListBox item by toggling its check).
            var element = InvokeOnUiThread(() =>
            {
                var form = FindFormById(formId);
                VerifyFormInteractable(form);
                var clickable = FindElement(form, button, e => e is IClickable, @"clickable control");
                VerifyInteractable(clickable);
                return clickable;
            });
            RunWithDialogWatch(() =>
            {
                // The element clicks itself; each handles its own threading (a Win32 BM_CLICK is sent
                // cross-thread so a click that opens a modal does not block here, while a managed
                // PerformClick / property-set marshals to the UI thread internally).
                ((IClickable)element).Click();
                return true;
            });
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
            // Resolve + click on the UI thread, inside the dialog-watch so a dialog the click opens is
            // surfaced/returned rather than blocking.
            RunWithDialogWatch(() =>
            {
                InvokeOnUiThread(() =>
                {
                    var form = FindFormById(formId);
                    VerifyFormInteractable(form);
                    FindAndClickToolStripItem(formId, menuPath);
                });
                return true;
            });
        }

        // Walks a form's ToolStrip items by path, opening each level's dropdown so dynamically-built
        // items are populated, then clicks the final item. Must run on the UI thread. Throws if a
        // segment does not match.
        private static void FindAndClickToolStripItem(string formId, string menuPath)
        {
            var form = FindFormById(formId);
            var segments = ParseMenuSegments(menuPath);
            var opened = new List<ToolStripDropDownItem>();
            try
            {
                // The first segment is a top-level item on one of the form's ToolStrips.
                var candidates = EnumerateControls(form).OfType<ToolStrip>()
                    .SelectMany(strip => strip.Items.Cast<ToolStripItem>());
                ToolStripItem current = null;
                for (int i = 0; i < segments.Length; i++)
                {
                    if (i > 0)
                    {
                        if (!(current is ToolStripDropDownItem dropDownItem))
                            throw new ArgumentException(LlmInstruction.Format(
                                @"Toolbar item '{0}' on form {1} is not a dropdown.", segments[i - 1], formId));
                        VerifyEnabled(dropDownItem);
                        // Show the dropdown so items built on DropDownOpening are present.
                        dropDownItem.ShowDropDown();
                        opened.Add(dropDownItem);
                        candidates = dropDownItem.DropDownItems.Cast<ToolStripItem>();
                    }
                    current = BestToolStripItem(candidates, segments[i]);
                    if (current == null)
                        throw new ArgumentException(LlmInstruction.Format(
                            @"Toolbar item not found on form {0}: {1} (no match for '{2}').",
                            formId, menuPath, segments[i]));
                }

                VerifyEnabled(current);
                current.PerformClick();
            }
            finally
            {
                // Close any dropdowns this opened (PerformClick usually closes them; be sure).
                for (int i = opened.Count - 1; i >= 0; i--)
                    opened[i].HideDropDown();
            }
        }

        // Matches a ToolStrip item by its name or caption; for an image-only item (no caption, e.g. the
        // pick-list's green-check OK button) it falls back to the tooltip, which is how a user reads it.
        private static ControlMatchQuality ToolStripMatchQuality(ToolStripItem item, string key)
        {
            var quality = MatchQuality(item.Text, key);
            if (string.IsNullOrEmpty(item.Text))
            {
                var tipQuality = MatchQuality(item.ToolTipText, key);
                if (tipQuality > quality)
                    quality = tipQuality;
            }
            return quality;
        }

        // Picks the ToolStripItem that best matches key (same ranking as FindClickTarget: highest
        // text-match quality, then prefer a visible+enabled item). Returns null if none matches.
        private static ToolStripItem BestToolStripItem(IEnumerable<ToolStripItem> items, string key)
        {
            ToolStripItem best = null;
            var bestQuality = ControlMatchQuality.None;
            var bestInteractable = false;
            foreach (var item in items)
            {
                var quality = ToolStripMatchQuality(item, key);
                if (quality == ControlMatchQuality.None)
                    continue;
                var interactable = item.Visible && item.Enabled;
                if (quality > bestQuality || (quality == bestQuality && interactable && !bestInteractable))
                {
                    best = item;
                    bestQuality = quality;
                    bestInteractable = interactable;
                }
            }
            return best;
        }

        /// <summary>
        /// Sets a control's value on an open form, or the file name(s) on a native file dialog
        /// (see <see cref="IJsonToolService"/>).
        /// </summary>
        public static void SetFormValue(string formId, string controlId, string value)
        {
            // Watch for a dialog so a setter that triggers a validation/confirmation alert fails fast
            // with its text, or that opens a native dialog returns immediately, instead of blocking.
            RunWithDialogWatch(() =>
            {
                ValidateFormIdFormat(formId);
                if (TryGetNativeDialog(formId, out var dialog))
                {
                    if (!User32.IsWindowEnabled(dialog.WindowHandle))
                        throw new InvalidOperationException(LlmInstruction.Format(
                            @"Cannot interact with native dialog '{0}' because it is blocked.", formId));

                    if (!(dialog is FileDialogAutomation fileDialog))
                        throw new ArgumentException(LlmInstruction.Format(
                            @"Setting values is not supported for native dialog {0}.", formId));
                    fileDialog.EnterPath(value);
                    return;
                }
                InvokeOnUiThread(() =>
                {
                    var form = FindFormById(formId);

                    // controlId can name a grid cell ("grid[column,row]") -- set that cell's value.
                    if (TryParseGridCell(controlId, out var gridName, out var column, out var row))
                    {
                        VerifyInteractable(GetGridView(form, gridName));
                        SetGridCellValue(form, gridName, column, row, value);
                        return;
                    }
                    // A field is matched by its own Label (its caption, or the label that names a
                    // caption-less box -- see UiElement.Label), so a label never has to be matched and
                    // resolved forward to its field.
                    var element = FindElement(form, controlId, e => e is IValueControl, @"settable control");
                    VerifyInteractable(element);
                    ((IValueControl)element).SetValue(value);
                });
            });
        }

        /// <summary>
        /// Pastes tab-separated <paramref name="text"/> into a grid on a form, starting at the anchor
        /// cell (<paramref name="column"/>, <paramref name="row"/>), the way typing/pasting there would.
        /// Works for a DataboundGridControl (e.g. the Document Grid) and for a plain DataGridView (e.g.
        /// the Rule Set Editor's rules grid). See <see cref="IJsonToolService"/>.
        /// </summary>
        public static void SetGridText(string formId, string controlId, int column, int row, string text)
        {
            ValidateFormIdFormat(formId);
            // A type conversion error during the paste raises an alert; watch for it so it is surfaced
            // rather than left blocking the main window.
            RunWithDialogWatch(() =>
            {
                InvokeOnUiThread(() =>
                {
                    var grid = FindGrid(FindFormById(formId), controlId);
                    VerifyInteractable(grid);
                    grid.SetGridText(column, row, text ?? string.Empty);
                });
                return true;
            });
        }

        // A grid the connector can drive: a DataboundGridControl (driven through its rich paste/copy
        // path, which keeps the bound document in sync) or a standalone DataGridView (driven by direct
        // cell access, the way a user types into the cells).
        private class GridTarget
        {
            public DataboundGridControl Databound;
            public DataGridView DataGridView;
            public string Name;
        }

        // Finds the grid to act on: the one named controlId, or -- when controlId is null/empty -- the
        // single grid on the form. Throws if there is no grid, or more than one and no name was given.
        private static GridElement FindGrid(Form form, string controlId)
        {
            var targets = EnumerateGridTargets(form).ToList();
            GridTarget target;
            if (!string.IsNullOrEmpty(controlId))
            {
                // A grid carries no visible caption, so on a form with several grids its control name is
                // the only stable way to choose one. This is the lone place the connector matches by
                // name (MatchQuality otherwise matches visible text only); pass an empty controlId when
                // the form has a single grid.
                target = targets.FirstOrDefault(t =>
                    string.Equals(t.Name, controlId, StringComparison.OrdinalIgnoreCase));
                if (target == null)
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Grid not found on form {0}: {1}. Pass an empty controlId for the form's single grid.",
                        GetFormId(form), controlId));
            }
            else if (targets.Count == 0)
                throw new ArgumentException(LlmInstruction.Format(
                    @"No grid found on form {0}.", GetFormId(form)));
            else if (targets.Count > 1)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Form {0} has more than one grid; pass a controlId to choose one.", GetFormId(form)));
            else
                target = targets[0];
            return target.Databound != null ? new GridElement(target.Databound) : new GridElement(target.DataGridView);
        }

        // The grids on a form: each DataboundGridControl, plus each DataGridView that is not inside a
        // DataboundGridControl (those inner grids are driven through the DataboundGridControl instead).
        private static IEnumerable<GridTarget> EnumerateGridTargets(Form form)
        {
            foreach (var databound in EnumerateControls(form).OfType<DataboundGridControl>())
                yield return new GridTarget { Databound = databound, Name = databound.Name };
            foreach (var dataGridView in EnumerateControls(form).OfType<DataGridView>())
                if (!HasDataboundAncestor(dataGridView))
                    yield return new GridTarget { DataGridView = dataGridView, Name = dataGridView.Name };
        }

        private static bool HasDataboundAncestor(Control control)
        {
            for (var parent = control.Parent; parent != null; parent = parent.Parent)
                if (parent is DataboundGridControl)
                    return true;
            return false;
        }

        // Sets the anchor cell (column/row are zero-based indices into the grid's visible columns and
        // its rows) and pastes the tab-separated text there.
        internal static void PasteGridText(DataboundGridControl grid, int column, int row, string text)
        {
            var dataGridView = grid.DataGridView;
            var visibleColumns = VisibleGridColumns(dataGridView);
            if (column < 0 || column >= visibleColumns.Length)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Column {0} is out of range; the grid has {1} visible columns.", column, visibleColumns.Length));
            if (row < 0 || row >= dataGridView.Rows.Count)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Row {0} is out of range; the grid has {1} rows.", row, dataGridView.Rows.Count));
            dataGridView.CurrentCell = dataGridView.Rows[row].Cells[visibleColumns[column].Index];
            grid.PasteText(text);
        }

        // Pastes tab/newline-separated text into a plain DataGridView by setting cell values from the
        // anchor cell, the way a user typing cell-by-cell would. Skips read-only cells. Throws if the
        // anchor (or a row a multi-row paste reaches) is out of range -- pasting cannot add rows beyond
        // the grid's new row, just as a user cannot type into a row that is not there.
        internal static void SetDataGridViewText(DataGridView dataGridView, int column, int row, string text)
        {
            var visibleColumns = VisibleGridColumns(dataGridView);
            if (column < 0 || column >= visibleColumns.Length)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Column {0} is out of range; the grid has {1} visible columns.", column, visibleColumns.Length));
            if (row < 0)
                throw new ArgumentException(LlmInstruction.Format(@"Row {0} is out of range.", row));
            var lines = SplitPasteLines(text);
            for (int iLine = 0; iLine < lines.Length; iLine++)
            {
                int rowIndex = row + iLine;
                if (rowIndex >= dataGridView.Rows.Count)
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Row {0} is past the end of the grid ({1} rows).", rowIndex, dataGridView.Rows.Count));
                var cellValues = lines[iLine].Split('\t');
                for (int iCol = 0; iCol < cellValues.Length && column + iCol < visibleColumns.Length; iCol++)
                {
                    var cell = dataGridView.Rows[rowIndex].Cells[visibleColumns[column + iCol].Index];
                    if (cell.ReadOnly)
                        continue;
                    // Go through the cell's edit lifecycle so the value is committed (and pushed to any
                    // data source) the way ending a cell edit does. A grid that validates whole rows
                    // governs whether a partially-filled new row is kept -- the same as for a user.
                    dataGridView.CurrentCell = cell;
                    dataGridView.BeginEdit(true);
                    cell.Value = cellValues[iCol];
                    dataGridView.EndEdit();
                }
            }
        }

        // Sets a single grid cell (column/row are zero-based indices into the grid's visible columns
        // and its rows) to value, reusing the grid paste path so a DataboundGridControl stays in sync.
        private static void SetGridCellValue(Form form, string gridName, int column, int row, string value)
        {
            FindGrid(form, gridName).SetGridText(column, row, value);
        }

        // Splits pasted text into row lines, normalizing newlines and dropping a single trailing empty
        // line (from text that ends with a newline).
        private static string[] SplitPasteLines(string text)
        {
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (lines.Length > 1 && lines[lines.Length - 1].Length == 0)
                lines = lines.Take(lines.Length - 1).ToArray();
            return lines;
        }

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
                var text = InvokeOnUiThread(() =>
                    FindGrid(FindFormById(formId), gridId).GetGridText(cancellation.Token));
                if (text == null)
                    throw new OperationCanceledException(LlmInstruction.Format(
                        @"Reading the grid {0} was cancelled.", formId));
                return text;
            }
        }

        // Reads a plain DataGridView as tab-separated text: the column headers followed by every data
        // row (each cell shown as the user sees it).
        internal static string GetDataGridViewText(DataGridView dataGridView)
        {
            var visibleColumns = VisibleGridColumns(dataGridView);
            var lines = new List<string>
            {
                TextUtil.ToEscapedTSV(visibleColumns.Select(col => col.HeaderText))
            };
            foreach (DataGridViewRow gridRow in dataGridView.Rows)
            {
                if (gridRow.IsNewRow)
                    continue;
                lines.Add(TextUtil.ToEscapedTSV(visibleColumns.Select(col => CellText(gridRow.Cells[col.Index]))));
            }
            return TextUtil.LineSeparate(lines);
        }

        // The display text of a grid cell: the formatted value the user sees, or empty for a null.
        private static string CellText(DataGridViewCell cell)
        {
            return (cell.FormattedValue ?? cell.Value)?.ToString() ?? string.Empty;
        }

        // The visible columns of a grid in display order -- the columns that
        // SetGridText's column index counts.
        private static DataGridViewColumn[] VisibleGridColumns(DataGridView dataGridView)
        {
            return dataGridView.Columns.Cast<DataGridViewColumn>()
                .Where(col => col.Visible).OrderBy(col => col.DisplayIndex).ToArray();
        }

        /// <summary>
        /// Closes an open form: a dialog, a docked or floating tool window (e.g. the Document Grid or
        /// Audit Log), or a native dialog (which is cancelled) -- see <see cref="IJsonToolService"/>.
        /// </summary>
        public static void CloseForm(string formId)
        {
            ValidateFormIdFormat(formId);
            // Closing may itself raise a confirmation (e.g. "save changes?"); watch for it so it is
            // surfaced rather than left blocking the main window.
            RunWithDialogWatch(() =>
            {
                if (TryGetNativeDialog(formId, out var dialog))
                {
                    dialog.Cancel();
                    return true;
                }
                InvokeOnUiThread(() =>
                {
                    var form = FindFormById(formId);
                    if (form == null)
                        throw new ArgumentException(LlmInstruction.Format(
                            @"Form not found: {0}. Use skyline_get_open_forms to list open forms.", formId));
                    form.Close();
                });
                return true;
            });
        }

        /// <summary>
        /// Checks or unchecks an item in a CheckedListBox or a TreeView on a form (see
        /// <see cref="IJsonToolService"/>). For a CheckedListBox the item is matched by its display
        /// text; for a TreeView <paramref name="item"/> is a '&gt;'-separated path of node texts.
        /// </summary>
        public static void SetItemChecked(string formId, string controlId, string item, bool isChecked)
        {
            ValidateFormIdFormat(formId);
            RunWithDialogWatch(() =>
            {
                InvokeOnUiThread(() =>
                {
                    var form = FindFormById(formId);
                    // The pick-list popup (Pick Children) is an owner-drawn ListBox whose checkboxes are
                    // PickListChoice.Chosen, not a CheckedListBox; match the item by its visible label.
                    if (form is PopupPickList pickList)
                    {
                        VerifyInteractable(pickList);
                        pickList.SetItemChecked(FindPickListIndex(pickList, item), isChecked);
                        return;
                    }
                    var element = FindElement(form, controlId, e => e is IItemContainer, @"list, tree, or list-view control");
                    VerifyInteractable(element);
                    ((IItemContainer)element).SetItemChecked(item, isChecked);
                });
                return true;
            });
        }

        // Checks/unchecks an item on a list-like control by its text (a TreeView item by a '>'-separated
        // path). Shared by the SetItemChecked verb and ListContainerElement so both drive it identically.
        internal static void SetListItemChecked(Control control, string item, bool isChecked)
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

        /// <summary>
        /// Selects or deselects an item in a ListBox/CheckedListBox or a TreeView on a form (see
        /// <see cref="IJsonToolService"/>). For a list the item is matched by its display text; for a
        /// TreeView <paramref name="item"/> is a '&gt;'-separated path of node texts.
        /// </summary>
        public static void SetItemSelected(string formId, string controlId, string item, bool selected)
        {
            ValidateFormIdFormat(formId);
            RunWithDialogWatch(() =>
            {
                InvokeOnUiThread(() =>
                {
                    var form = FindFormById(formId);
                    var element = FindElement(form, controlId, e => e is IItemContainer, @"list, tree, or list-view control");
                    VerifyInteractable(element);
                    ((IItemContainer)element).SetItemSelected(item, selected);
                });
                return true;
            });
        }

        // Selects/deselects an item on a list-like control by its text (a TreeView item by a '>'-separated
        // path). Shared by the SetItemSelected verb and ListContainerElement.
        internal static void SetListItemSelected(Control control, string item, bool selected)
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

        // Finds the index of the best-matching choice (by its visible label) in a pick-list popup.
        // Throws if none matches. The index matches PopupPickList.SetItemChecked's item ordering.
        private static int FindPickListIndex(PopupPickList pickList, string item)
        {
            var labels = pickList.ItemNames.ToList();
            int best = -1;
            var bestQuality = ControlMatchQuality.None;
            for (int i = 0; i < labels.Count; i++)
            {
                var quality = MatchQuality(labels[i], item);
                if (quality > bestQuality)
                {
                    best = i;
                    bestQuality = quality;
                }
            }
            if (best < 0)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Item not found in the pick list: {0}.", item));
            return best;
        }

        // Finds the index of the best-matching item (by display text) in a ListBox. Throws if none.
        private static int FindListItemIndex(ListBox listBox, string item)
        {
            int best = -1;
            var bestQuality = ControlMatchQuality.None;
            for (int i = 0; i < listBox.Items.Count; i++)
            {
                var quality = MatchQuality(listBox.GetItemText(listBox.Items[i]), item);
                if (quality > bestQuality)
                {
                    best = i;
                    bestQuality = quality;
                }
            }
            if (best < 0)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Item not found in {0}: {1}.", listBox.Name, item));
            return best;
        }

        // Finds the best-matching item (by its text or name) in a ListView. Throws if none.
        private static ListViewItem FindListViewItem(ListView listView, string item)
        {
            ListViewItem best = null;
            var bestQuality = ControlMatchQuality.None;
            foreach (ListViewItem listViewItem in listView.Items)
            {
                var quality = MatchQuality(listViewItem.Text, item);
                if (quality > bestQuality)
                {
                    best = listViewItem;
                    bestQuality = quality;
                }
            }
            if (best == null)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Item not found in {0}: {1}.", listView.Name, item));
            return best;
        }

        // Walks a TreeView by a '>'-separated path of node texts, expanding each level so nodes built
        // on demand (e.g. the Customize Report field tree) are present before the next segment is
        // matched. Must run on the UI thread. Throws if a segment does not match.
        private static TreeNode FindTreeNode(TreeView treeView, string path)
        {
            var segments = ParseTreePath(path);
            var nodes = treeView.Nodes;
            TreeNode current = null;
            for (int i = 0; i < segments.Length; i++)
            {
                current = BestTreeNode(nodes, segments[i]);
                if (current == null)
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Tree node not found: {0} (no match for '{1}').", path, segments[i]));
                if (i < segments.Length - 1)
                {
                    current.Expand(); // populate lazily-built children before descending
                    nodes = current.Nodes;
                }
            }
            return current;
        }

        // Picks the child node that best matches key by node name or visible text. Returns null if none.
        private static TreeNode BestTreeNode(TreeNodeCollection nodes, string key)
        {
            TreeNode best = null;
            var bestQuality = ControlMatchQuality.None;
            foreach (TreeNode node in nodes)
            {
                var quality = MatchQuality(node.Text, key);
                if (quality > bestQuality)
                {
                    best = node;
                    bestQuality = quality;
                }
            }
            return best;
        }

        /// <summary>
        /// Lists the interactive controls on a form so a caller can discover what is there -- and how to
        /// address it -- without reading the source. Each control reports its Name (informational), Type,
        /// the visible Label that identifies it, current Value, enabled/visible state, and the connector
        /// actions it supports. Only controls a user can act on are returned (buttons, fields, lists, ...).
        /// </summary>
        public static ControlInfo[] GetControls(string formId)
        {
            ValidateFormIdFormat(formId);
            return InvokeOnUiThread(() =>
            {
                var form = FindFormById(formId);
                // Report form-level controls; menu and list items (clickable pseudo-elements) are an
                // internal detail of the walk, not controls to list here.
                return UiElementFactory.For(form).SelfAndDescendants()
                    .Where(element => element is ControlElement && element.HasCapability)
                    .Select(element => new ControlInfo
                    {
                        Id = ToControlId(element, formId),
                        Value = element.Value,
                        Enabled = element.IsEnabled,
                        Visible = element.IsVisible,
                        Actions = element.Actions.ToArray(),
                    })
                    .ToArray();
            });
        }

        private static string NullIfEmpty(string text) => string.IsNullOrEmpty(text) ? null : text;

        // The label as a caller would type it: mnemonic '&' and a trailing colon/ellipsis removed, so a
        // "Name:" label is reported (and addressable) as "Name". Matching tolerates either form anyway.
        private static string CleanLabel(string text) =>
            string.IsNullOrEmpty(text) ? text : NormalizeLabel(text).TrimEnd(' ', ':');

        // The locator a caller can pass back to refer to this element (e.g. to PerformAction). The owning
        // form is the Parent -- a ControlId of Type "Form" naming the form id from GetOpenForms.
        private static ControlId ToControlId(UiElement element, string formId) =>
            ControlIdFor(element, FormControlId(formId));

        // The locator for an element whose container is given by parentId (so the chain is preserved).
        private static ControlId ControlIdFor(UiElement element, ControlId parentId) => new ControlId
        {
            Parent = parentId,
            Name = NullIfEmpty(element.Name),
            Label = NullIfEmpty(CleanLabel(element.Label)),
            Type = element.ElementType.Name,
        };

        private static ControlId FormControlId(string formId) => new ControlId { Type = @"Form", Name = formId };

        /// <summary>
        /// The most general way to interact with a control, menu item, or list item (see
        /// <see cref="IJsonToolService"/>): resolve the element the <paramref name="controlId"/> refers to,
        /// then perform <paramref name="action"/> on it. The action determines the value and return types:
        /// "get_actions" -> string[]; "get_children" -> ControlId[]; "click" -> null; "set_value" (string
        /// value) -> null; "get_value" -> string.
        /// </summary>
        public static object PerformAction(ControlId controlId, string action, object value)
        {
            // Accept any case / underscore style, so "get_actions", "GetActions", "getActions" all match.
            var normalized = (action ?? string.Empty).Trim().ToLowerInvariant().Replace(@"_", string.Empty);
            switch (normalized)
            {
                case @"getactions":
                    return InvokeOnUiThread(() => ResolveControlId(controlId).Actions.ToArray());
                case @"getchildren":
                    return InvokeOnUiThread(() => ResolveControlId(controlId).Children
                        .Select(child => ControlIdFor(child, controlId)).ToArray());
                case @"click":
                    var clickable = InvokeOnUiThread(() => RequireCapability<IClickable>(ResolveControlId(controlId), action));
                    RunWithDialogWatch(() =>
                    {
                        // The element clicks itself (each handles its own threading -- see ClickFormButton).
                        clickable.Click();
                        return true;
                    });
                    return null;
                case @"setvalue":
                    var stringValue = value as string ?? value?.ToString();
                    RunWithDialogWatch(() =>
                    {
                        InvokeOnUiThread(() =>
                            RequireCapability<IValueControl>(ResolveControlId(controlId), action).SetValue(stringValue));
                        return true;
                    });
                    return null;
                case @"getvalue":
                    return InvokeOnUiThread(() =>
                        RequireCapability<IValueControl>(ResolveControlId(controlId), action).GetValue());
                default:
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Unsupported action '{0}'. Use get_actions to list the actions a control supports.", action));
            }
        }

        // Verifies the resolved element is interactable and supports the capability the action needs,
        // returning it cast to that capability. Throws a clear error (listing the element's actions) if not.
        private static T RequireCapability<T>(UiElement element, string action) where T : class
        {
            VerifyInteractable(element);
            if (element is T capability)
                return capability;
            throw new ArgumentException(LlmInstruction.Format(
                @"The control '{0}' does not support the action '{1}'. It supports: {2}.",
                element.Label ?? element.Name, action, string.Join(@", ", element.Actions)));
        }

        // Resolves a ControlId to the single element it refers to, using only the properties that are set.
        // A Parent narrows the search to within that element; otherwise the search is the whole form. Among
        // matches, prefers the best Label match, then a visible+enabled one. Must run on the UI thread.
        private static UiElement ResolveControlId(ControlId controlId)
        {
            if (controlId == null)
                throw new ArgumentException(new LlmInstruction(@"A controlId is required."));

            // The chain bottoms out at a form: a root ControlId (no Parent) names the form -- its Name is
            // the form id from GetOpenForms (its Type is "Form"). Everything else is found within a Parent.
            if (controlId.Parent == null)
            {
                if (string.IsNullOrEmpty(controlId.Name))
                    throw new ArgumentException(new LlmInstruction(
                        @"The root of a controlId must name a form: Type 'Form' and Name set to the form id from skyline_get_open_forms."));
                return UiElementFactory.For(FindFormById(controlId.Name));
            }
            if (controlId.Name == null && controlId.Label == null && controlId.Type == null)
                throw new ArgumentException(new LlmInstruction(
                    @"A controlId needs at least a Name, Label, or Type to identify a control."));

            var scope = ResolveControlId(controlId.Parent).SelfAndDescendants().Skip(1);
            UiElement best = null;
            var bestQuality = ControlMatchQuality.None;
            var bestInteractable = false;
            foreach (var element in scope)
            {
                if (!MatchesControlId(element, controlId, out var quality))
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
                    @"No control found matching the controlId. Use skyline_get_controls to list the controls."));
            return best;
        }

        // True if every set property of the controlId matches the element; quality ranks Label matches
        // (an exact label beats a symbol-stripped one) so the closest match wins among several.
        private static bool MatchesControlId(UiElement element, ControlId controlId, out ControlMatchQuality quality)
        {
            quality = ControlMatchQuality.None;
            if (controlId.Name != null && !string.Equals(element.Name, controlId.Name, StringComparison.OrdinalIgnoreCase))
                return false;
            if (controlId.Type != null && !MatchesControlType(element.ElementType, controlId.Type))
                return false;
            if (controlId.Label != null)
            {
                quality = MatchQuality(element.Label, controlId.Label);
                if (quality == ControlMatchQuality.None)
                    return false;
            }
            else if (controlId.Name != null)
                quality = ControlMatchQuality.Exact; // a name is an exact identity
            else
                quality = ControlMatchQuality.Type; // a type-only match is the weakest
            return true;
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
            foreach (var dialog in NativeDialogAutomation.GetOpenDialogs())
            {
                results.Add(new FormInfo
                {
                    Type = dialog.DialogTypeName,
                    Title = dialog.Title,
                    HasGraph = false,
                    DockState = @"Dialog",
                    Id = GetNativeDialogId(dialog),
                    IsNative = true,
                });
            }
            return results.ToArray();
        }

        private static string GetNativeDialogId(NativeDialogAutomation dialog)
        {
            return dialog.DialogTypeName + @":" + dialog.Title;
        }

        private static bool TryGetNativeDialog(string formId, out NativeDialogAutomation dialog)
        {
            foreach (var openDialog in NativeDialogAutomation.GetOpenDialogs())
            {
                if (GetNativeDialogId(openDialog) == formId)
                {
                    dialog = openDialog;
                    return true;
                }
            }
            dialog = null;
            return false;
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
            if (TryGetNativeDialog(formId, out var nativeDialog))
                return GetNativeDialogImage(nativeDialog, filePath);
            string denial = CheckImageToolPreflight(formId,
                () => FindFormById(formId),
                requiresScreenCapture: true);
            if (denial != null)
                return denial;
            return InvokeOnUiThread(() =>
            {
                // Re-resolve the form on the UI thread so a close during the
                // pipe-thread work in CheckScreenCaptureAvailability surfaces
                // as the same "form not found" ArgumentException as a bad
                // formId, not as ObjectDisposedException during capture.
                var form = FindFormById(formId);
                using (var bitmap = CaptureGrantedForm(form))
                {
                    filePath = filePath ?? GetMcpTmpFilePath(
                        FORM_FILE_PREFIX, GetFormTitle(form), EXT_PNG);
                    DirectoryEx.CreateForFilePath(filePath);
                    bitmap.Save(filePath, ImageFormat.Png);
                }
                return filePath.ToForwardSlashPath();
            });
        }

        public static ImageBytesMetadata GetFormImageBytes(string formId)
        {
            ValidateFormIdFormat(formId);
            if (TryGetNativeDialog(formId, out var nativeDialog))
                return GetNativeDialogImageBytes(nativeDialog);
            string denial = CheckImageToolPreflight(formId,
                () => FindFormById(formId),
                requiresScreenCapture: true);
            if (denial != null)
            {
                // Permission denial / desktop unavailable: return a structured
                // Message instead of bytes. The wrapper emits Message as plain
                // text content (no error flag) so the response shape matches
                // what the legacy file-based path returned for the same condition.
                return new ImageBytesMetadata { Message = denial };
            }
            return InvokeOnUiThread(() =>
            {
                var form = FindFormById(formId);
                using (var bitmap = CaptureGrantedForm(form))
                {
                    return new ImageBytesMetadata
                    {
                        Data = BitmapToPngBytes(bitmap),
                        FilePath = GetMcpTmpFilePath(FORM_FILE_PREFIX, GetFormTitle(form), EXT_PNG)
                            .ToForwardSlashPath(),
                        MimeType = MIME_TYPE_PNG
                    };
                }
            });
        }

        // Captures a native dialog (e.g. the Open/Save file dialog) to a PNG file. Unlike the
        // WinForms path this runs entirely on the calling (pipe) thread: the capture is a screen
        // copy by window handle and must not marshal to the UI thread, which may be blocked in a
        // modal dialog's message loop.
        private static string GetNativeDialogImage(NativeDialogAutomation dialog, string filePath)
        {
            if (!HasUiDispatch())
                return LLM_MSG_SCREEN_CAPTURE_UNAVAILABLE;
            string denial = CheckScreenCaptureAvailability();
            if (denial != null)
                return denial;
            using (var bitmap = CaptureNativeWindow(dialog.WindowHandle))
            {
                filePath = filePath ?? GetMcpTmpFilePath(FORM_FILE_PREFIX, dialog.Title, EXT_PNG);
                DirectoryEx.CreateForFilePath(filePath);
                bitmap.Save(filePath, ImageFormat.Png);
            }
            return filePath.ToForwardSlashPath();
        }

        private static ImageBytesMetadata GetNativeDialogImageBytes(NativeDialogAutomation dialog)
        {
            if (!HasUiDispatch())
                return new ImageBytesMetadata { Message = LLM_MSG_SCREEN_CAPTURE_UNAVAILABLE };
            string denial = CheckScreenCaptureAvailability();
            if (denial != null)
                return new ImageBytesMetadata { Message = denial };
            using (var bitmap = CaptureNativeWindow(dialog.WindowHandle))
            {
                return new ImageBytesMetadata
                {
                    Data = BitmapToPngBytes(bitmap),
                    FilePath = GetMcpTmpFilePath(FORM_FILE_PREFIX, dialog.Title, EXT_PNG)
                        .ToForwardSlashPath(),
                    MimeType = MIME_TYPE_PNG
                };
            }
        }

        // Captures a screenshot of a native window by its handle. GetWindowRect returns logical
        // coordinates, scaled to physical pixels to match the screen copy (the same convention as
        // ScreenCapture.GetForeignWindowRects).
        private static System.Drawing.Bitmap CaptureNativeWindow(IntPtr windowHandle)
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

        // Captures a screenshot of an open form (with redaction). Caller owns
        // the bitmap. Must be called on the UI thread, and only after
        // CheckScreenCaptureAvailability has returned null.
        private static System.Drawing.Bitmap CaptureGrantedForm(Form form)
        {
            ScreenCapture.ActivateForm(form);
            var screenRect = ScreenCapture.GetWindowRectangle(form);
            return ScreenCapture.CaptureAndRedact(screenRect, form);
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
        private static string GetFormTitle(Form form)
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
        private static string GetFormId(Form form)
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

        // Finds the one element on the form with the requested capability that the caller asked for: the
        // best match of key against each candidate's visible Label (or, failing that, its type), the same
        // ranking FindBestMatch uses (best quality, then prefer a visible+enabled element). An empty key
        // means "the form's single element with this capability". Throws if none (or, for an empty key,
        // more than one) matches. This is the single finder the action verbs share -- see GetControls for
        // what a caller can discover. Must run on the UI thread.
        private static UiElement FindElement(Form form, string key, Func<UiElement, bool> hasCapability, string capabilityNoun)
        {
            var candidates = UiElementFactory.For(form).SelfAndDescendants().Where(hasCapability).ToList();
            if (string.IsNullOrEmpty(key))
            {
                if (candidates.Count == 0)
                    throw new ArgumentException(LlmInstruction.Format(
                        @"No {0} found on form {1}.", capabilityNoun, GetFormId(form)));
                if (candidates.Count > 1)
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Form {0} has more than one {1}; pass a Label or type to choose one (see skyline_get_controls).",
                        GetFormId(form), capabilityNoun));
                return candidates[0];
            }
            UiElement best = null;
            var bestQuality = ControlMatchQuality.None;
            var bestInteractable = false;
            foreach (var element in candidates)
            {
                var quality = ElementMatches(element, key);
                if (quality == ControlMatchQuality.None)
                    continue;
                var interactable = element.IsVisible && element.IsEnabled;
                if (quality > bestQuality || (quality == bestQuality && interactable && !bestInteractable))
                {
                    best = element;
                    bestQuality = quality;
                    bestInteractable = interactable;
                }
            }
            if (best == null)
                throw new ArgumentException(LlmInstruction.Format(
                    @"No {0} found on form {1} matching '{2}'. Use skyline_get_controls to list the controls.",
                    capabilityNoun, GetFormId(form), key));
            return best;
        }

        // The element gate: the same modal-block + enabled check as VerifyInteractable(Control), for a
        // resolved element. Control-backed elements reuse the control path; others check their own state.
        private static void VerifyInteractable(UiElement element)
        {
            if (element is ControlElement controlElement)
            {
                VerifyInteractable(controlElement.Control);
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
        private static void VerifyFormInteractable(Form form)
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

        // The single chokepoint for a verb that acts on one resolved control: gate the control's form
        // (modal/disabled) AND the control itself (Control.Enabled also reflects a disabled ancestor).
        private static void VerifyInteractable(Control target)
        {
            var form = target.FindForm();
            if (form != null)
                VerifyFormInteractable(form);
            VerifyEnabled(target);
        }

        // The target gate for a control: throws if it (or, via Control.Enabled, an ancestor) is disabled.
        private static void VerifyEnabled(Control control)
        {
            if (!control.Enabled)
                throw new InvalidOperationException(LlmInstruction.Format(
                    @"Control '{0}' is disabled.", control.Name));
        }

        // The target gate for a menu/toolbar item (a ToolStripItem is not a Control).
        private static void VerifyEnabled(ToolStripItem item)
        {
            if (!item.Enabled)
                throw new InvalidOperationException(LlmInstruction.Format(
                    @"Menu or toolbar item '{0}' is disabled.",
                    string.IsNullOrEmpty(item.Text) ? item.Name : item.Text));
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

        // --- Generic form interaction helpers ---

        // Walks the main menu by path (segments split on '>', '|', '/'), matching each segment by
        // normalized text or by control name. Must run on the UI thread. Throws if no item matches.
        private static ToolStripMenuItem FindMenuItem(string menuPath)
        {
            return FindMenuItemIn(Program.MainWindow.MainMenuStrip.Items, menuPath);
        }

        // Walks a menu by path (segments split on '>', '|', '/') starting from the given items,
        // matching each segment by normalized text or control name. Opens each submenu level so items
        // built on DropDownOpening (e.g. the View > Live Reports > Group Comparisons list, recent
        // files) are present before the next segment is matched. Must run on the UI thread. Throws if
        // no segment matches.
        private static ToolStripMenuItem FindMenuItemIn(ToolStripItemCollection rootItems, string menuPath)
        {
            var segments = ParseMenuSegments(menuPath);
            var items = rootItems;
            var opened = new List<ToolStripDropDownItem>();
            ToolStripMenuItem current = null;
            try
            {
                for (int i = 0; i < segments.Length; i++)
                {
                    current = items.OfType<ToolStripMenuItem>().FirstOrDefault(item => MenuItemMatches(item, segments[i]));
                    if (current == null)
                        throw new ArgumentException(LlmInstruction.Format(
                            @"Menu item not found: {0} (no match for '{1}').", menuPath, segments[i]));
                    if (i < segments.Length - 1)
                    {
                        current.ShowDropDown(); // populate items built on DropDownOpening
                        opened.Add(current);
                        items = current.DropDownItems;
                    }
                }
                return current;
            }
            finally
            {
                // Close the dropdowns this opened (the found item is still clickable while hidden).
                for (int i = opened.Count - 1; i >= 0; i--)
                    opened[i].HideDropDown();
            }
        }

        private static bool MenuItemMatches(ToolStripMenuItem item, string label)
        {
            return string.Equals(NormalizeLabel(item.Text), NormalizeLabel(label), StringComparison.CurrentCultureIgnoreCase)
                || string.Equals(item.Name, label, StringComparison.OrdinalIgnoreCase);
        }

        // Splits a tree-node path into its segments on '>' ONLY -- unlike a menu path, a node's text
        // legitimately contains '|' and '/' (e.g. a UniProt protein name "sp|P02769|ALBU_BOVIN", or a
        // small-molecule formula), so those must not be treated as separators. Throws if empty.
        private static string[] ParseTreePath(string path)
        {
            var segments = (path ?? string.Empty)
                .Split('>').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            if (segments.Length == 0)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Empty tree path: {0}. Expected '>'-separated node texts, e.g. 'Protein > Peptide > Precursor'.",
                    path ?? string.Empty));
            return segments;
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

        // Depth-first (pre-order) enumeration of all controls under the given parent.
        private static IEnumerable<Control> EnumerateControls(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                yield return child;
                foreach (var descendant in EnumerateControls(child))
                    yield return descendant;
            }
        }

        // How well a control matches a requested label. Higher is better; callers prefer the best match
        // in the form and accept a weaker one only when nothing matches better.
        private enum ControlMatchQuality
        {
            None = 0,     // no match
            Type = 1,     // matched the control's type name ("ListView"/"TreeView") -- a last resort for a
                          // caption-less control that no visible text identifies; any text match wins.
            Stripped = 2, // matched the visible text after ignoring non-alphanumeric symbols ("Next" == "Next >")
            Exact = 3,    // matched the visible text after light normalization
        }

        // How well a UiElement matches a requested key: by its visible Label, else (the weakest match) by
        // its kind. The single ranking the verbs use to find the element to act on.
        private static ControlMatchQuality ElementMatches(UiElement element, string key)
        {
            var quality = MatchQuality(element.Label, key);
            if (quality == ControlMatchQuality.None && MatchesControlType(element.ElementType, key))
                return ControlMatchQuality.Type;
            return quality;
        }

        // True if key names the type or any of its base types -- so "ListView" matches a ColumnListView
        // and "TreeView" an AvailableFieldsTree. Lets a caption-less control be referred to by its kind.
        // Stops at Control: a key like "Control"/"UserControl" is too broad to be useful.
        private static bool MatchesControlType(Type controlType, string key)
        {
            for (var type = controlType; type != null && type != typeof(Control); type = type.BaseType)
                if (string.Equals(type.Name, key, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        // Match quality of a control/item against a requested key, by VISIBLE TEXT only -- the connector
        // deliberately does not match on the internal control Name, so a step refers to a control the way
        // a user sees it (its caption, or the label that names it -- see the label->field resolution in
        // SetFormValue/FindListOrTreeControl). A control with no caption (e.g. a text box) is reached via
        // its label; a grid, which has no caption at all, is the lone exception (matched by name in
        // FindGrid). Used for WinForms controls, ToolStripItems, list items, and tree nodes alike.
        private static ControlMatchQuality MatchQuality(string text, string key)
        {
            // Best: the visible text after light normalization (mnemonic '&' and a trailing
            // ellipsis/period removed -- see NormalizeLabel).
            if (string.Equals(NormalizeLabel(text), NormalizeLabel(key), StringComparison.CurrentCultureIgnoreCase))
                return ControlMatchQuality.Exact;
            // Fallback: ignore every non-alphanumeric character, so a tutorial's "Next" matches a
            // button captioned "Next >" (and "Back" a "< Back"). Used only when nothing matches exactly.
            var keyStripped = StripToAlphanumeric(key);
            if (keyStripped.Length > 0
                && string.Equals(StripToAlphanumeric(text), keyStripped, StringComparison.CurrentCultureIgnoreCase))
                return ControlMatchQuality.Stripped;
            return ControlMatchQuality.None;
        }

        // Removes every character that is not a letter or digit (spaces and punctuation included), for
        // the loose, symbol-insensitive comparison tier.
        private static string StripToAlphanumeric(string text)
        {
            return string.IsNullOrEmpty(text)
                ? string.Empty
                : new string(text.Where(char.IsLetterOrDigit).ToArray());
        }

        // True when the requested button names the cancel/close action of a native dialog. Locale
        // sensitive by nature; callers that key on the visible label inherit that.
        private static bool IsCancelAction(string button)
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
