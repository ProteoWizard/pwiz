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
            // A main-menu item cannot be clicked while a modal dialog is blocking the main window
            // (the window, and so its menu, is disabled). Fail fast rather than silently no-op.
            var mainHandle = InvokeOnUiThread(() => Program.MainWindow.Handle);
            if (!User32.IsWindowEnabled(mainHandle))
                throw new InvalidOperationException(
                    @"Cannot invoke a menu item: a modal dialog is blocking the main Skyline window. Handle the open dialog first (see skyline_get_open_forms).");
            var item = InvokeOnUiThread(() => FindMenuItem(menuPath));
            Program.MainWindow.BeginInvoke((Action)item.PerformClick);
        }

        /// <summary>
        /// Invokes an item on a graph's right-click context menu by its visible path (e.g.
        /// "Normalize To > None"). The menu is built the way a right-click would build it -- the
        /// graph's ContextMenuBuilder handlers populate a fresh ContextMenuStrip -- then the item is
        /// located by path and clicked. Runs on the UI thread. Use for forms with HasGraph=True.
        /// </summary>
        public static void InvokeContextMenuItem(string formId, string menuPath)
        {
            InvokeOnUiThread(() =>
            {
                var graph = FindFormById(formId) as DockableFormEx;
                var zedGraph = graph != null ? TryGetZedGraphControl(graph) : null;
                if (zedGraph == null)
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Not a graph form: {0}. Context menus are available on forms with HasGraph=True (see skyline_get_open_forms).",
                        formId));
                using (var menuStrip = new ContextMenuStrip())
                {
                    PopulateGraphContextMenu(zedGraph, menuStrip);
                    FindMenuItemIn(menuStrip.Items, menuPath).PerformClick();
                }
            });
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
                if (IsCancelAction(button))
                    dialog.Cancel();
                else
                    dialog.Accept();
                return;
            }
            // Resolve the click target on the UI thread, then act inside the dialog-watch so a dialog
            // the click pops is observed: a resulting alert is surfaced (throws its text) and a native
            // dialog (e.g. Save/Open) returns immediately rather than blocking on its modal.
            var target = InvokeOnUiThread(() => FindClickTarget(formId, button));
            RunWithDialogWatch(() =>
            {
                if (target.ButtonHandle != IntPtr.Zero)
                {
                    // A Win32 button (Button / CheckBox / RadioButton): BM_CLICK clicks like a mouse --
                    // it fires the Click handler (so an AutoCheck=false checkbox toggles) and bypasses
                    // PerformClick's CanSelect / validation gates (which can silently no-op).
                    User32.SendMessage(target.ButtonHandle, User32.WinMessageType.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                }
                else if (target.Clickable != null)
                {
                    // A custom IButtonControl (e.g. a StartPage tile) -- PerformClick on the UI thread.
                    InvokeOnUiThread(() => target.Clickable.PerformClick());
                }
                else if (target.ToolStripItem != null)
                {
                    // A menu / toolbar item (ToolStripButton, dropdown item, ...) -- PerformClick.
                    InvokeOnUiThread(() => target.ToolStripItem.PerformClick());
                }
                else if (target.TabPage != null)
                {
                    // A tab: select it on its TabControl (a mouse click would hit the page content,
                    // not the tab header).
                    InvokeOnUiThread(() => SelectTabPage(target.TabPage));
                }
                else if (target.CheckedList != null)
                {
                    // An item in a CheckedListBox (e.g. "Applies to > Replicates") -- toggle its check,
                    // the way a CheckOnClick item toggles when a user clicks it.
                    InvokeOnUiThread(() => ToggleCheckedListItem(target.CheckedList, target.CheckedListItemIndex));
                }
                else
                {
                    // Any other control -- synthesize a mouse click at its center on the UI thread.
                    InvokeOnUiThread(() => ClickControlWithMouse(target.Control));
                }
                return true;
            });
        }

        // Toggles the checked state of one item in a CheckedListBox. SetItemChecked raises ItemCheck --
        // the same event a user's click raises -- so any handler keyed on the selection stays in sync.
        // Must run on the UI thread.
        private static void ToggleCheckedListItem(CheckedListBox checkedList, int index)
        {
            checkedList.SetItemChecked(index, !checkedList.GetItemChecked(index));
        }

        // Selects the tab a TabPage belongs to. Must run on the UI thread.
        private static void SelectTabPage(TabPage tabPage)
        {
            if (!(tabPage.Parent is TabControl tabControl))
                throw new ArgumentException(LlmInstruction.Format(
                    @"Tab '{0}' is not on a tab control.", tabPage.Text));
            tabControl.SelectedTab = tabPage;
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
                InvokeOnUiThread(() => FindAndClickToolStripItem(formId, menuPath));
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
                current.PerformClick();
            }
            finally
            {
                // Close any dropdowns this opened (PerformClick usually closes them; be sure).
                for (int i = opened.Count - 1; i >= 0; i--)
                    opened[i].HideDropDown();
            }
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
                var quality = MatchQuality(item.Name, item.Text, key);
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
                    if (!(dialog is FileDialogAutomation fileDialog))
                        throw new ArgumentException(LlmInstruction.Format(
                            @"Setting values is not supported for native dialog {0}.", formId));
                    fileDialog.EnterPath(value);
                    return;
                }
                InvokeOnUiThread(() =>
                {
                    var form = FindFormById(formId);
                    var control = FindControl<Control>(form, controlId);
                    if (control == null)
                        throw new ArgumentException(LlmInstruction.Format(
                            @"Control not found on form {0}: {1}.", formId, controlId));
                    // A user edits a field, not its caption. When the match is a label (e.g. "Name:"),
                    // set the editable control it labels -- the next one in tab order -- instead, since
                    // changing a label's text is not something a user could do through the UI.
                    if (control is System.Windows.Forms.Label)
                    {
                        control = NextEditableInTabOrder(form, control)
                            ?? throw new ArgumentException(LlmInstruction.Format(
                                @"'{0}' on form {1} is a label with no editable field after it in tab order.",
                                controlId, formId));
                    }
                    SetControlValue(control, value);
                });
            });
        }

        /// <summary>
        /// Pastes tab-separated <paramref name="text"/> into a grid on a form, exactly as a Ctrl-V of
        /// that text would, starting at the anchor cell (<paramref name="column"/>, <paramref name="row"/>).
        /// Currently supports <see cref="DataboundGridControl"/> grids (see <see cref="IJsonToolService"/>).
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
                    var form = FindFormById(formId);
                    var grid = FindGrid(form, controlId);
                    PasteGridText(grid, column, row, text ?? string.Empty);
                });
                return true;
            });
        }

        // Finds the grid to paste into: the DataboundGridControl named controlId, or -- when controlId
        // is null/empty -- the single grid on the form. Throws if there is no grid, or more than one
        // and no name was given to disambiguate.
        private static DataboundGridControl FindGrid(Form form, string controlId)
        {
            if (!string.IsNullOrEmpty(controlId))
            {
                var named = FindControl<DataboundGridControl>(form, controlId);
                if (named == null)
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Grid not found on form {0}: {1}.", GetFormId(form), controlId));
                return named;
            }
            var grids = EnumerateControls(form).OfType<DataboundGridControl>().ToList();
            if (grids.Count == 0)
                throw new ArgumentException(LlmInstruction.Format(
                    @"No grid found on form {0}.", GetFormId(form)));
            if (grids.Count > 1)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Form {0} has more than one grid; pass a controlId to choose one.", GetFormId(form)));
            return grids[0];
        }

        // Sets the anchor cell (column/row are zero-based indices into the grid's visible columns and
        // its rows) and pastes the tab-separated text there.
        private static void PasteGridText(DataboundGridControl grid, int column, int row, string text)
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

        /// <summary>
        /// Returns all the text in a grid on a form -- the column headers followed by every data row --
        /// as tab-separated columns and newline-separated rows, the same content as Copy All
        /// (see <see cref="IJsonToolService"/>). Currently supports DataboundGridControl grids.
        /// </summary>
        public static string GetGridText(string formId, string gridId)
        {
            ValidateFormIdFormat(formId);
            // Capture the disconnect check on this (server) thread before any marshaling, so a large
            // copy is abandoned if the client goes away rather than pinning the single-instance server.
            using (var cancellation = new ClientDisconnectCancellation(_clientConnectedCheck))
            {
                var text = InvokeOnUiThread(() =>
                    FindGrid(FindFormById(formId), gridId).GetCopyText(cancellation.Token));
                if (text == null)
                    throw new OperationCanceledException(LlmInstruction.Format(
                        @"Reading the grid {0} was cancelled.", formId));
                return text;
            }
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

        // --- Generic form interaction helpers ---

        // Walks the main menu by path (segments split on '>', '|', '/'), matching each segment by
        // normalized text or by control name. Must run on the UI thread. Throws if no item matches.
        private static ToolStripMenuItem FindMenuItem(string menuPath)
        {
            return FindMenuItemIn(Program.MainWindow.MainMenuStrip.Items, menuPath);
        }

        // Walks a menu by path (segments split on '>', '|', '/') starting from the given items,
        // matching each segment by normalized text or control name. Must run on the UI thread (and,
        // for a context menu, after the menu has been populated). Throws if no item matches.
        private static ToolStripMenuItem FindMenuItemIn(ToolStripItemCollection rootItems, string menuPath)
        {
            var segments = ParseMenuSegments(menuPath);
            var items = rootItems;
            ToolStripMenuItem current = null;
            foreach (var segment in segments)
            {
                current = items.OfType<ToolStripMenuItem>().FirstOrDefault(i => MenuItemMatches(i, segment));
                if (current == null)
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Menu item not found: {0} (no match for '{1}').", menuPath, segment));
                items = current.DropDownItems;
            }
            return current;
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

        // A resolved click target. Exactly one member is set, classified in FindClickTarget /
        // MakeControlTarget: a Win32 button (Button/CheckBox/RadioButton) clicked via BM_CLICK; a
        // custom IButtonControl (e.g. a StartPage tile) or a ToolStripItem (menu/toolbar item) clicked
        // via PerformClick; an item in a CheckedListBox (e.g. "Applies to") toggled like a click; or any
        // other control clicked with a synthesized mouse click.
        private class ClickTarget
        {
            public IntPtr ButtonHandle;
            public IButtonControl Clickable;
            public ToolStripItem ToolStripItem;
            public TabPage TabPage;
            public CheckedListBox CheckedList;
            public int CheckedListItemIndex;
            public Control Control;
        }

        // Resolves a click target on a form by control/item name or visible text. Searches the whole
        // WinForms control tree (any control -- the same breadth as SetFormValue) AND the ToolStrip
        // items (menus, toolbars, dropdowns), which are not in the control tree. Ranks by match quality
        // (exact over symbol-stripped), then prefers a visible+enabled target so a hidden duplicate
        // does not win. Must run on the UI thread. Throws if nothing matches.
        private static ClickTarget FindClickTarget(string formId, string button)
        {
            var form = FindFormById(formId);
            ClickTarget best = null;
            var bestQuality = ControlMatchQuality.None;
            var bestInteractable = false;

            void Consider(ControlMatchQuality quality, bool interactable, Func<ClickTarget> make)
            {
                if (quality == ControlMatchQuality.None)
                    return;
                if (quality > bestQuality || (quality == bestQuality && interactable && !bestInteractable))
                {
                    best = make();
                    bestQuality = quality;
                    bestInteractable = interactable;
                }
            }

            foreach (var control in EnumerateControls(form))
                Consider(ControlMatches(control, button), control.Visible && control.Enabled,
                    () => MakeControlTarget(control));
            foreach (var item in EnumerateToolStripItems(form))
                Consider(MatchQuality(item.Name, item.Text, button), item.Visible && item.Enabled,
                    () => new ClickTarget { ToolStripItem = item });
            // Items inside a CheckedListBox (e.g. the "Applies to" list) are not controls, so match them
            // by their display text and treat a hit as a click that toggles the item's check.
            foreach (var checkedList in EnumerateControls(form).OfType<CheckedListBox>())
            {
                var interactable = checkedList.Visible && checkedList.Enabled;
                for (int i = 0; i < checkedList.Items.Count; i++)
                {
                    int index = i; // capture a stable value for the closure
                    Consider(MatchQuality(null, checkedList.GetItemText(checkedList.Items[index]), button),
                        interactable,
                        () => new ClickTarget { CheckedList = checkedList, CheckedListItemIndex = index });
                }
            }

            if (best == null)
                throw new ArgumentException(LlmInstruction.Format(
                    @"No clickable control found on form {0}: {1}.", formId, button));
            return best;
        }

        // Classifies a control into a click target: a Win32 button (Button/CheckBox/RadioButton) is
        // clicked via BM_CLICK (which fires the Click handler even for an AutoCheck=false checkbox); a
        // custom IButtonControl (e.g. a StartPage tile) via PerformClick; anything else via a mouse click.
        private static ClickTarget MakeControlTarget(Control control)
        {
            if (control is ButtonBase)
                return new ClickTarget { ButtonHandle = control.Handle };
            if (control is IButtonControl buttonControl)
                return new ClickTarget { Clickable = buttonControl };
            if (control is TabPage tabPage)
                return new ClickTarget { TabPage = tabPage };
            return new ClickTarget { Control = control };
        }

        // Enumerates every ToolStripItem reachable from the form's ToolStrips (MenuStrip, ToolStrip,
        // BindingNavigator, ...), descending into dropdown items so submenu/dropdown items are included.
        private static IEnumerable<ToolStripItem> EnumerateToolStripItems(Control form)
        {
            return EnumerateControls(form).OfType<ToolStrip>().SelectMany(s => EnumerateToolStripItems(s.Items));
        }

        private static IEnumerable<ToolStripItem> EnumerateToolStripItems(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                yield return item;
                if (item is ToolStripDropDownItem dropDownItem)
                    foreach (var child in EnumerateToolStripItems(dropDownItem.DropDownItems))
                        yield return child;
            }
        }

        // Synthesizes a left mouse click at the center of a control that is neither a button nor a
        // ToolStripItem (e.g. a link or a custom control). Must run on the UI thread.
        private static void ClickControlWithMouse(Control control)
        {
            var lParam = (IntPtr)(((control.Height / 2) << 16) | ((control.Width / 2) & 0xFFFF));
            User32.SendMessage(control.Handle, User32.WinMessageType.WM_LBUTTONDOWN, (IntPtr)MK_LBUTTON, lParam);
            User32.SendMessage(control.Handle, User32.WinMessageType.WM_LBUTTONUP, IntPtr.Zero, lParam);
        }

        private const int MK_LBUTTON = 0x0001;

        // Searches the form for the control of type T that best matches key.
        private static T FindControl<T>(Control parent, string key) where T : Control
        {
            return (T)FindBestMatch(parent, key, c => c is T);
        }

        // Finds the control under parent that best matches key among controls passing the filter.
        // Ranking: highest text-match quality first (exact over symbol-stripped), then a
        // visible+enabled control over a hidden/disabled one of the same quality. The latter matters
        // when two controls share a caption but only one is live -- e.g. a wizard's last page has both
        // the visible "Finish" nav button and a hidden early-"Finish" button. Returns null if none.
        private static Control FindBestMatch(Control parent, string key, Func<Control, bool> filter)
        {
            Control best = null;
            var bestQuality = ControlMatchQuality.None;
            var bestInteractable = false;
            foreach (var control in EnumerateControls(parent))
            {
                if (!filter(control))
                    continue;
                var quality = ControlMatches(control, key);
                if (quality == ControlMatchQuality.None)
                    continue;
                var interactable = control.Visible && control.Enabled;
                if (quality > bestQuality || (quality == bestQuality && interactable && !bestInteractable))
                {
                    best = control;
                    bestQuality = quality;
                    bestInteractable = interactable;
                    if (quality == ControlMatchQuality.Exact && interactable)
                        break; // best possible -- exact text on a live control
                }
            }
            return best;
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

        // How well a control matches a requested name/label. Higher is better; callers prefer the
        // best match in the form and accept a weaker one only when nothing matches better.
        private enum ControlMatchQuality
        {
            None = 0,     // no match
            Stripped = 1, // matched only after ignoring non-alphanumeric symbols ("Next" == "Next >")
            Exact = 2,    // matched on control name, or on visible text after light normalization
        }

        private static ControlMatchQuality ControlMatches(Control control, string key)
        {
            return MatchQuality(control.Name, control.Text, key);
        }

        // Match quality of a control/item with the given name and visible text against a requested key.
        // Used for both WinForms controls and ToolStripItems (which are not Controls but have the same
        // Name/Text pair).
        private static ControlMatchQuality MatchQuality(string name, string text, string key)
        {
            // Best: the stable control name, or the visible text after light normalization (mnemonic
            // '&' and a trailing ellipsis/period removed -- see NormalizeLabel).
            if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(NormalizeLabel(text), NormalizeLabel(key), StringComparison.CurrentCultureIgnoreCase))
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

        private static void SetControlValue(Control control, string value)
        {
            switch (control)
            {
                case CheckBox checkBox:
                    checkBox.Checked = bool.TryParse(value, out var parsed) ? parsed : value == @"1";
                    break;
                case RadioButton radioButton:
                    // Selecting a radio button checks it; WinForms auto-unchecks its siblings and
                    // raises CheckedChanged so any UI logic keyed on the selection stays in sync.
                    radioButton.Checked = bool.TryParse(value, out var radioParsed) ? radioParsed : value == @"1";
                    break;
                case ComboBox comboBox:
                    int index = comboBox.FindStringExact(value);
                    if (index < 0)
                        throw new ArgumentException(LlmInstruction.Format(
                            @"No item '{0}' in combo box {1}.", value, control.Name));
                    comboBox.SelectedIndex = index;
                    break;
                case TextBoxBase textBox:
                    textBox.Text = value;
                    break;
                case DataGridView grid:
                    if (grid.CurrentCell == null)
                    {
                        // Try to find first editable cell if no current cell
                        foreach (DataGridViewRow row in grid.Rows)
                        {
                            foreach (DataGridViewCell cell in row.Cells)
                            {
                                if (!cell.ReadOnly && cell.Visible)
                                {
                                    grid.CurrentCell = cell;
                                    break;
                                }
                            }
                            if (grid.CurrentCell != null) break;
                        }
                    }
                    if (grid.CurrentCell == null)
                        throw new ArgumentException(LlmInstruction.Format(
                            @"No editable cell found in grid {0}.", control.Name));
                    grid.CurrentCell.Value = value;
                    break;
                case System.Windows.Forms.Label _:
                    // A user cannot edit a label's caption, so setting it would not mirror the UI.
                    // SetFormValue resolves a matched label to its field before reaching here; this
                    // guard keeps the invariant if SetControlValue is ever called with one directly.
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Cannot set a value on the label {0}.", control.Name));
                default:
                    control.Text = value;
                    break;
            }
        }

        // Finds the first editable control after the given control in tab order -- the input a "Name:"
        // label labels, for example. Used to resolve a matched label to the field it names. Returns
        // null if no editable control follows. Must run on the UI thread.
        private static Control NextEditableInTabOrder(Control container, Control afterControl)
        {
            for (var next = container.GetNextControl(afterControl, true);
                 next != null;
                 next = container.GetNextControl(next, true))
            {
                if (next.Visible && next.Enabled && IsEditableValueControl(next))
                    return next;
            }
            return null;
        }

        // True for controls a user can type into or pick a value from -- the kinds a label labels.
        private static bool IsEditableValueControl(Control control)
        {
            return control is TextBoxBase
                || control is ComboBox
                || control is NumericUpDown
                || control is DateTimePicker
                || control is CheckBox
                || control is RadioButton
                || control is ListBox
                || control is TrackBar;
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
