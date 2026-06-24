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

        // Builds the right-click context menu for owner the way a right-click would (so items added on
        // demand are present), for ContextMenuElement to list or invoke. The Targets-tree menu and a
        // control's own ContextMenuStrip are owned elsewhere (do not dispose); the grid-cell and graph
        // menus are freshly built. Runs on the UI thread.
        internal static ContextMenuStrip BuildContextMenu(UiElement owner)
        {
            var control = (owner as ControlElement)?.Control;
            // The Targets tree's node menu is shown manually, so it lives on the main window. Its Opening
            // is fired so item enablement reflects the current selection (select the node first).
            if (control is SequenceTree)
            {
                var treeMenu = Program.MainWindow.ContextMenuTreeNode;
                RaiseProtectedHandler(treeMenu, @"OnOpening", new CancelEventArgs());
                return treeMenu;
            }
            // A grid's menu is the one for its current cell (move there first with set_current_cell_address).
            if (owner is GridElement gridElement)
                return BuildGridCellContextMenu(gridElement.DataGridView);
            // A graph builds a fresh menu through its ContextMenuBuilder. The graph can be addressed as
            // its ZedGraphControl, or -- since a graph form is just its graph -- as the form itself.
            var zedGraph = control as ZedGraphControl
                ?? (control as DockableFormEx != null ? TryGetZedGraphControl((DockableFormEx) control) : null);
            if (zedGraph != null)
            {
                var graphMenu = new ContextMenuStrip();
                PopulateGraphContextMenu(zedGraph, graphMenu);
                return graphMenu;
            }
            // Otherwise the control's own ContextMenuStrip, if it has one.
            if (control?.ContextMenuStrip != null)
            {
                RaiseProtectedHandler(control.ContextMenuStrip, @"OnOpening", new CancelEventArgs());
                return control.ContextMenuStrip;
            }
            throw new ArgumentException(LlmInstruction.Format(
                @"{0} has no context menu.", owner.Label ?? NullIfEmpty(owner.Name) ?? owner.ElementType.Name));
        }

        // Raises the grid's CellContextMenuStripNeeded for its current cell (as a right-click there does)
        // to obtain the menu, then fires its Opening so on-demand items are built.
        private static ContextMenuStrip BuildGridCellContextMenu(DataGridView dataGridView)
        {
            var cell = dataGridView.CurrentCell;
            if (cell == null)
                throw new ArgumentException(new LlmInstruction(
                    @"The grid has no current cell -- move to one first with set_current_cell_address."));
            var args = new DataGridViewCellContextMenuStripNeededEventArgs(cell.ColumnIndex, cell.RowIndex);
            RaiseProtectedHandler(dataGridView, @"OnCellContextMenuStripNeeded", args);
            var menuStrip = args.ContextMenuStrip;
            if (menuStrip == null)
                throw new ArgumentException(new LlmInstruction(@"The current cell has no context menu."));
            RaiseProtectedHandler(menuStrip, @"OnOpening", new CancelEventArgs());
            return menuStrip;
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
        /// IButtonControl (e.g. a StartPage tile), a ToolStrip/menu/toolbar item, or any other clickable
        /// control (matched by its visible label) -- or accepts/cancels a native dialog (see
        /// <see cref="IJsonToolService"/>).
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
            // toggles; a menu/toolbar item or tile via PerformClick; a tab by selecting it).
            var element = InvokeOnUiThread(() =>
            {
                var form = FindFormById(formId);
                VerifyFormInteractable(form);
                var clickable = UiElementFactory.For(form).FindElement(button, UiAction.Click);
                VerifyInteractable(clickable);
                return clickable;
            });
            RunWithDialogWatch(() =>
            {
                // The element clicks itself; each handles its own threading (a Win32 BM_CLICK is sent
                // cross-thread so a click that opens a modal does not block here, while a managed
                // PerformClick / property-set marshals to the UI thread internally).
                element.PerformAction(UiAction.Click, null, CancellationToken.None);
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

                    // controlId can name a grid cell ("grid[column,row]") -- set that cell's value: move the
                    // current cell there and paste, reusing the grid path so a DataboundGridControl stays in
                    // sync. The grid is resolved through the same finder as every other control.
                    if (TryParseGridCell(controlId, out var gridName, out var column, out var row))
                    {
                        var gridElement = FindGrid(form, gridName);
                        VerifyInteractable(gridElement);
                        gridElement.SetCurrentCellAddress(column, row);
                        gridElement.SetGridText(value);
                        return;
                    }
                    // A field is matched by its own Label (its caption, or the label that names a
                    // caption-less box -- see UiElement.Label), so a label never has to be matched and
                    // resolved forward to its field.
                    var element = UiElementFactory.For(form).FindElement(controlId, UiAction.SetValue);
                    VerifyInteractable(element);
                    element.PerformAction(UiAction.SetValue, value, CancellationToken.None);
                });
            });
        }

        /// <summary>
        /// Returns the current value of a control on a form, found by its Label: a text box's text, a combo
        /// box's selected item, a check/radio's checked state, or a CheckedListBox's checked items (their
        /// text, one per line). See <see cref="IJsonToolService"/>.
        /// </summary>
        public static string GetFormValue(string formId, string controlId)
        {
            ValidateFormIdFormat(formId);
            return InvokeOnUiThread(() =>
                UiElementFactory.For(FindFormById(formId)).FindElement(controlId, UiAction.GetValue).Value);
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
            // A type conversion error during the paste raises an alert; watch for it so it is surfaced
            // rather than left blocking the main window.
            RunWithDialogWatch(() =>
            {
                InvokeOnUiThread(() =>
                {
                    var grid = FindGrid(FindFormById(formId), controlId);
                    VerifyInteractable(grid);
                    grid.SetGridText(text ?? string.Empty);
                });
                return true;
            });
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
            InvokeOnUiThread(() =>
            {
                var grid = FindGrid(FindFormById(formId), controlId);
                VerifyInteractable(grid);
                grid.SetCurrentCellAddress(column, row);
            });
        }

        // Finds the grid to act on: the one named controlId, or -- when controlId is null/empty -- the
        // single grid on the form, resolved through the shared element finder. A grid supports the grid
        // actions and is matched by its control name (it has no caption -- see GridElement.MatchesText), so
        // FindElement picks it out of the form's controls just like any other control. The factory wraps a
        // bound inner grid (a DataboundGridControl's BoundDataGridView) as a BoundGridElement with the rich
        // copy/paste path, and a standalone DataGridView as a plain GridElement; the DataboundGridControl
        // itself is a transparent container the walk descends through to reach the inner grid.
        private static GridElement FindGrid(Form form, string controlId)
        {
            return (GridElement) UiElementFactory.For(form)
                .FindElement(controlId ?? string.Empty, UiAction.SetGridText);
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

        // The grid's current cell as a point (X = visible-column index, Y = row index), or (0,0) when the
        // grid has no current cell -- the anchor SetGridText pastes from.
        internal static System.Drawing.Point CurrentGridCell(DataGridView dataGridView)
        {
            var cell = dataGridView.CurrentCell;
            if (cell == null)
                return new System.Drawing.Point(0, 0);
            var visibleColumns = VisibleGridColumns(dataGridView);
            int column = Array.FindIndex(visibleColumns, col => col.Index == cell.ColumnIndex);
            return new System.Drawing.Point(Math.Max(0, column), cell.RowIndex);
        }

        // Moves the grid's current cell to the visible-column index x and row index y, validating both.
        internal static void SetCurrentGridCell(DataGridView dataGridView, int x, int y)
        {
            var visibleColumns = VisibleGridColumns(dataGridView);
            if (x < 0 || x >= visibleColumns.Length)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Column {0} is out of range; the grid has {1} visible columns.", x, visibleColumns.Length));
            if (y < 0 || y >= dataGridView.Rows.Count)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Row {0} is out of range; the grid has {1} rows.", y, dataGridView.Rows.Count));
            dataGridView.CurrentCell = dataGridView.Rows[y].Cells[visibleColumns[x].Index];
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


        // Resolves a tree path -- an array whose segments select a child at each level (an integer is the
        // child at that index; a string is the first child whose text matches it) -- to its TreeNode,
        // expanding each ancestor so lazily-built children are present before descending. The path value is
        // an object[] (an in-process caller) or a JSON array (over the wire). Must run on the UI thread.
        internal static TreeNode ResolveTreePath(TreeView treeView, object pathValue)
        {
            var path = ToTreePath(pathValue);
            if (path.Length == 0)
                throw new ArgumentException(new LlmInstruction(
                    @"The path is empty -- give an array of child names (strings) and/or indexes (integers)."));
            var nodes = treeView.Nodes;
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
                if (MatchQuality(node.Text, text) != ControlMatchQuality.None)
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
            ValidateFormIdFormat(formId);
            // A native dialog is enumerated on this (pipe) thread via UI Automation, never inside
            // InvokeOnUiThread: while it is modal the UI thread is busy in its own message loop, so a
            // marshalled call would deadlock.
            if (TryGetNativeDialog(formId, out var dialog))
                return dialog.GetChildren();
            return InvokeOnUiThread(() => UiElementFactory.For(FindFormById(formId)).GetChildren());
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
            if (!UiActions.TryParse(action, out var uiAction))
                throw new ArgumentException(LlmInstruction.Format(
                    @"Unsupported action '{0}'. Use get_actions to list the actions a control supports.", action));
            // Capture the disconnect check on this (server) thread before any marshaling, so a long action
            // (e.g. reading a large grid) is abandoned if the client goes away rather than pinning the
            // single-instance server. The element receives the token through its PerformAction.
            using (var cancellation = new ClientDisconnectCancellation(_clientConnectedCheck))
            {
                var token = cancellation.Token;
                // A path rooted at a native dialog is resolved and acted on this (pipe) thread: the dialog
                // runs its own modal loop on the UI thread, so marshalling to it (or to RunWithDialogWatch's
                // background work) would deadlock. The dialog's elements post Win32 messages, which is safe
                // from any thread.
                if (TryGetNativeDialog(RootFormText(path), out var nativeDialog))
                    return PerformActionOnNativeDialog(nativeDialog, path, uiAction, value, token);
                switch (uiAction)
                {
                    // GetActions/GetChildren are answered by the service; the returned child paths have a null
                    // Parent (the caller knows the parent it asked about). The element handles the operational
                    // actions through its PerformAction.
                    case UiAction.GetActions:
                        return InvokeOnUiThread(() => ResolvePath(path).SupportedActions.Select(UiActions.ToName).ToArray());
                    case UiAction.GetChildren:
                        return InvokeOnUiThread(() => ResolvePath(path).GetChildren());
                    case UiAction.Click:
                        // A click runs inside the dialog-watch (the element handles its own threading); resolve
                        // and verify first on the UI thread.
                        var clickElement = InvokeOnUiThread(() => RequireAction(ResolvePath(path), uiAction));
                        RunWithDialogWatch(() =>
                        {
                            clickElement.PerformAction(uiAction, value, token);
                            return true;
                        });
                        return null;
                    case UiAction.SetValue:
                    case UiAction.SetGridText:
                    case UiAction.SetCurrentCellAddress:
                    case UiAction.SetSelectedIndex:
                    case UiAction.CheckItem:
                    case UiAction.UncheckItem:
                    case UiAction.SelectItem:
                    case UiAction.UnselectItem:
                    case UiAction.SelectTab:
                    case UiAction.Expand:
                    case UiAction.Collapse:
                        // Mutating actions: run inside the dialog-watch (a paste can raise a conversion
                        // alert) and on the UI thread.
                        object setResult = null;
                        RunWithDialogWatch(() =>
                        {
                            setResult = InvokeOnUiThread(() => RequireAction(ResolvePath(path), uiAction).PerformAction(uiAction, value, token));
                            return true;
                        });
                        return setResult;
                    default:
                        // Read-only / other actions: resolve, verify, perform on the UI thread.
                        return InvokeOnUiThread(() => RequireAction(ResolvePath(path), uiAction).PerformAction(uiAction, value, token));
                }
            }
        }

        // Dispatches an action on a native dialog (or one of its elements). Everything runs on the calling
        // (pipe) thread -- the dialog is modal on the UI thread, so it must not be marshalled there -- and
        // the path is resolved against the dialog as its root. The dialog's elements dispatch to UI
        // Automation / Win32 messages, which are safe from any thread.
        private static object PerformActionOnNativeDialog(NativeDialog dialog, UiElementPath path,
            UiAction uiAction, object value, CancellationToken token)
        {
            var element = ResolvePath(path, _ => dialog);
            switch (uiAction)
            {
                case UiAction.GetActions:
                    return element.SupportedActions.Select(UiActions.ToName).ToArray();
                case UiAction.GetChildren:
                    return element.GetChildren();
                default:
                    return RequireAction(element, uiAction).PerformAction(uiAction, value, token);
            }
        }

        // Verifies the resolved element is interactable and supports the action, returning it. Throws a
        // clear error (listing the element's actions) if it does not.
        private static UiElement RequireAction(UiElement element, UiAction action)
        {
            VerifyInteractable(element);
            if (element.SupportsAction(action))
                return element;
            throw new ArgumentException(LlmInstruction.Format(
                @"The control '{0}' does not support the action '{1}'. It supports: {2}.",
                element.Label ?? element.Name, UiActions.ToName(action),
                string.Join(@", ", element.SupportedActions.Select(UiActions.ToName))));
        }

        // The form id at the root of a path -- the Text of its deepest (Parent-less) segment, or null.
        private static string RootFormText(UiElementPath path)
        {
            while (path?.Parent != null)
                path = path.Parent;
            return path?.Text;
        }

        // Resolves a managed path: its root form is found in FormUtil.OpenForms. Must run on the UI thread.
        private static UiElement ResolvePath(UiElementPath path) =>
            ResolvePath(path, formId => UiElementFactory.For(FindFormById(formId)));

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
                    Id = GetNativeDialogId(dialog),
                    IsNative = true,
                });
            }
            return results.ToArray();
        }

        private static string GetNativeDialogId(NativeDialog dialog)
        {
            return dialog.DialogTypeName + @":" + dialog.Title;
        }

        private static bool TryGetNativeDialog(string formId, out NativeDialog dialog)
        {
            foreach (var openDialog in NativeDialog.GetOpenDialogs())
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
        private static string GetNativeDialogImage(NativeDialog dialog, string filePath)
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

        private static ImageBytesMetadata GetNativeDialogImageBytes(NativeDialog dialog)
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

        // The element gate: gate the modal-block + enabled state of a resolved element before a verb acts
        // on it. A control-backed element gates its form (modal/disabled) AND the control itself
        // (Control.Enabled also reflects a disabled ancestor); a non-control element checks its own state.
        private static void VerifyInteractable(UiElement element)
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
        internal enum ControlMatchQuality
        {
            None = 0,     // no match
            Type = 1,     // matched the control's type name ("ListView"/"TreeView") -- a last resort for a
                          // caption-less control that no visible text identifies; any text match wins.
            Stripped = 2, // matched the visible text after ignoring non-alphanumeric symbols ("Next" == "Next >")
            Exact = 3,    // matched the visible text after light normalization
        }

        // Match quality of a control/item against a requested key, by VISIBLE TEXT only -- the connector
        // deliberately does not match on the internal control Name, so a step refers to a control the way
        // a user sees it (its caption, or the label that names it -- see the label->field resolution in
        // SetFormValue/FindListOrTreeControl). A control with no caption (e.g. a text box) is reached via
        // its label; a grid, which has no caption at all, is the lone exception (matched by name in
        // FindGrid). Used for WinForms controls, ToolStripItems, list items, and tree nodes alike.
        internal static ControlMatchQuality MatchQuality(string text, string key)
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
