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
        /// otherwise (null) the main window. Most callers go through the <see cref="UiElement"/> methods. Delegates
        /// the raw <see cref="Control.Invoke(System.Delegate)"/> to <see cref="UiServiceDispatcher"/>, which owns
        /// the marshal, and adds the exception wrapping here.
        /// </summary>
        public static void InvokeOnUiThread(Action action, Control dispatcher = null)
        {
            // Route through the InvokeOnUiThread factory + Run (a synchronous Control.Invoke); the wrapping stays
            // here around it -- the action's exception is caught inside, then re-thrown wrapped (ArgumentException
            // preserved) so the original stack trace survives, exactly as before.
            Exception caught = null;
            UiServiceDispatcher.ForInvoke(dispatcher).Run(() =>
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
        /// Executes a function on the UI thread and returns the result, through the InvokeOnUiThread factory + Run
        /// (a synchronous <see cref="Control.Invoke(System.Delegate)"/>). Must be called from a background thread
        /// (pipe server thread). Exceptions propagate to the caller raw.
        /// </summary>
        public static T InvokeOnUiThread<T>(Func<T> func, Control dispatcher = null)
        {
            return UiServiceDispatcher.ForInvoke(dispatcher).Run(func);
        }

        /// <summary>
        /// The number of fire-and-forget actions (posted by <see cref="BeginInvokeOnUiThread"/>, or wrapped by
        /// <see cref="UiServiceDispatcher"/>) that have been posted but have not yet finished running -- the public
        /// (IJsonToolService) accessor, forwarding to the count state that now lives on
        /// <see cref="UiServiceDispatcher"/>. Incremented when an action is posted and decremented when its delegate
        /// returns -- so an action that opens a modal dialog stays counted until that modal closes (its delegate is
        /// blocked in the modal's message loop). It is therefore usually equal to the number of modal dialogs the
        /// connector's actions have raised and left open, and a caller can poll it to wait until pending
        /// sets/clicks have actually been applied.
        /// </summary>
        public static int ModalNestingCount => UiServiceDispatcher.ModalNestingCount;

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
            UiServiceDispatcher.IncrementModalNestingCount();
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
                finally { UiServiceDispatcher.DecrementModalNestingCount(); }
            }
            try
            {
                // A form on its own thread (e.g. BackgroundThreadLongWaitDlg) is posted to through its own
                // BeginInvoke; otherwise UiServiceDispatcher.PostToUiThreadWindow targets the main window, or the
                // StartPage before it exists (and falls back to a background thread if neither is up).
                if (dispatcher != null && dispatcher.IsHandleCreated && !dispatcher.IsDisposed)
                    dispatcher.BeginInvoke((Action) Run);
                else
                    UiServiceDispatcher.PostToUiThreadWindow(Run);
            }
            catch
            {
                // Posting failed, so the action will never run and is not pending after all.
                UiServiceDispatcher.DecrementModalNestingCount();
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
        public static T RunWithDialogWatch<T>(Func<T> work)
        {
            // Run the value-producing work on a background thread and THROW a blocking modal's message rather than
            // hang, with no timeout -- the shared wait/modal machinery, configured for the dialog-watch. See
            // <see cref="UiServiceDispatcher"/>.
            return UiServiceDispatcher.ForDialogWatch().Run(work);
        }

        public static void RunWithDialogWatch(Action work)
        {
            RunWithDialogWatch(() => { work(); return true; });
        }

        // The wait a named/convenience method runs for its gesture. The gesture (<paramref name="postGesture"/>) is
        // posted onto <paramref name="dispatcher"/> -- the TARGET FORM's own UI thread -- as one delegate that
        // resolves the control, gates it, and does the gesture; UiServiceDispatcher waits it out. Unifies three
        // cases: a plain mutating action returns when ModalNestingCount falls back to where it started; an
        // action that opens an interactive modal returns as soon as the modal appears (recording its pre-show
        // count -- the ONLY place a pre-show count is recorded); an action that dismisses the top modal waits
        // until the count falls to the pre-show level its opener left, riding through any LongWaitDlg the resumed
        // work shows. Progress is gauged by a synchronous marshaled probe each cycle (see ProbeModals): the wait
        // aborts only after NO_PROGRESS_LIMIT consecutive actively-pumping probes show neither completion nor a
        // LongWaitDlg. Must be called off the UI thread.
        internal static ActionResult WaitForGesture(Control dispatcher, Action postGesture)
        {
            // BeginInvoke the gesture onto the target form's own thread, do NOT throw on a modal but STOP and record
            // it, and ride the ~10s message-loop-progress watchdog -- the shared wait/modal machinery, configured
            // for the posted-gesture wait. Falls back to the main/start window only if no form is given. See
            // UiServiceDispatcher.
            var dispatch = UiServiceDispatcher.ForGesture(dispatcher ?? UiServiceDispatcher.UiThreadWindow);
            dispatch.Run(postGesture);
            // The gesture completed only if the count drained; if it opened (or left open) a modal, report that
            // dialog's message so the caller knows what to drive next.
            return dispatch.StoppedOnModal
                ? new ActionResult { Completed = false, Message = dispatch.BlockingMessage }
                : new ActionResult { Completed = true };
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

        // Level 3: Complete UI operations - Generic form interaction

        /// <summary>
        /// Invokes a main-menu item by its visible path (see <see cref="IJsonToolService"/>). The
        /// item is located on the UI thread (throwing if absent), then its click is posted with
        /// BeginInvoke so a menu item that opens a modal dialog does not block the caller.
        /// </summary>
        public static ActionResult InvokeMenuItem(string menuPath)
        {
            // There is no main menu while the StartPage is showing (the main window does not exist
            // yet). Fail with a clear message rather than dereferencing a null main window.
            if (Program.MainWindow == null)
                throw new InvalidOperationException(
                    @"Cannot invoke a menu item: the main Skyline window is not open yet (the StartPage may be showing).");
            // The main menu is the main window's; drive it through that form's element model. Build the element on
            // the UI thread (it reads the window handle), then InvokeMenuItem drives it from this thread.
            var mainWindow = InvokeOnUiThread(() => new FormElement(Program.MainWindow));
            return mainWindow.InvokeMenuItem(menuPath);
        }

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
        /// Clicks an item on a form's ToolStrip (toolbar / menu strip) by its path, e.g.
        /// "Reports &gt; Replicates" -- the toolbar button "Reports" then the "Replicates" item in its
        /// dropdown. Each level's dropdown is opened first so that items built on demand (which are not
        /// in the static DropDownItems, e.g. the Document Grid's Reports list) are present before the
        /// item is matched. Each segment is matched by item name or visible text, like InvokeMenuItem.
        /// </summary>
        public static ActionResult ClickToolStripItem(string formId, string menuPath)
        {
            ValidateFormIdFormat(formId);
            // The path-walk, matching, gating, and click all live in ToolStripElement.ClickMenuItem; the first
            // segment is a top-level item on one of the form's toolstrips, so try each until one has it (a null
            // result means that toolstrip did not have it -- move on). Stops at the first that clicks it.
            var formElement = (FormElement) FindFormById(formId);
            var toolStrips = InvokeOnUiThread(() =>
                formElement.SelfAndDescendants().OfType<ToolStripElement>().ToList());
            var result = toolStrips.Select(toolStrip => toolStrip.ClickMenuItem(menuPath)).FirstOrDefault(r => r != null);
            return result ?? throw new ArgumentException(LlmInstruction.Format(
                @"Toolbar item not found on form {0}: {1}.", formId, menuPath));
        }

        /// <summary>
        /// Accepts the dialog named by <paramref name="formId"/> -- the resolved form knows how to accept itself
        /// (a managed form posts its default button and waits the gesture out; a native dialog does its OK gesture
        /// and waits for the window to close) and reports whether it completed. See <see cref="IJsonToolService"/>.
        /// </summary>
        public static ActionResult Accept(string formId)
        {
            ValidateFormIdFormat(formId);
            return ResolveForm(formId).Accept();
        }

        /// <summary>
        /// Cancels the dialog named by <paramref name="formId"/> -- the dismissing counterpart of
        /// <see cref="Accept"/>, likewise delegated to the resolved form. See <see cref="IJsonToolService"/>.
        /// </summary>
        public static ActionResult Cancel(string formId)
        {
            ValidateFormIdFormat(formId);
            return ResolveForm(formId).Cancel();
        }

        /// <summary>
        /// Clicks an item on a control's right-click context menu. The control is addressed the way get_controls
        /// addresses one (by its <paramref name="controlSelector"/> label or type); <paramref name="itemText"/>
        /// is the item's visible text, or a '&gt;'-separated path into a submenu. A string-friendly wrapper over
        /// the ContextMenu <see cref="UiElementPath"/> a caller would otherwise hand-build for
        /// <see cref="PerformAction"/>. See <see cref="IJsonToolService"/>.
        /// </summary>
        public static void InvokeContextMenuItem(string formId, string controlSelector, string itemText)
        {
            ValidateFormIdFormat(formId);
            if (string.IsNullOrEmpty(itemText))
                throw new ArgumentException(new LlmInstruction(@"An item text is required."));

            var formPath = new UiElementPath(null, formId, null, @"Form");
            // An empty control selector means the form's own context menu -- e.g. a graph, whose right-click menu
            // is built on demand and is not owned by a named child control. Otherwise address the named control
            // (the way get_controls does), and take its context menu.
            var menuOwnerPath = string.IsNullOrEmpty(controlSelector)
                ? formPath
                : new UiElementPath(formPath, controlSelector, null, null);
            UiElementPath itemPath = new UiElementPath(menuOwnerPath, null, null, ContextMenuElement.TypeName);
            foreach (var segment in itemText.Split(new[] { '>', '|', '/' }, StringSplitOptions.RemoveEmptyEntries))
                itemPath = new UiElementPath(itemPath, segment.Trim(), null, null);

            // Resolve the item on the form's UI thread (building the context menu has side effects), then click it
            // by posting the click onto that form's own thread and waiting it out.
            var formRoot = (UiElement) ResolveForm(formId);
            var pathToClick = itemPath;
            var element = formRoot.InvokeOnUiThread(() =>
                RequireAction(ResolvePath(pathToClick, formRoot), UiActions.Click));
            WaitForGesture((formRoot as FormElement)?.Form, () => UiActions.Click.Invoke(element, null));
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
        /// Returns all the choices a list control on a form offers (a combo box, list box, or checked list
        /// box), found by its Label/name, as their visible text -- every option regardless of selection or
        /// checked state (unlike <see cref="GetFormValue"/>, which reports the current selection). See
        /// <see cref="IJsonToolService"/>.
        /// </summary>
        public static string[] GetOptions(string formId, string controlId)
        {
            ValidateFormIdFormat(formId);
            // A value read: run it synchronously inside the dialog-watch so it does not hang if a modal is up.
            string[] result = null;
            RunWithDialogWatch(() =>
            {
                result = OnFormThread(formId, formElement =>
                    ((IOptionsElement) formElement.FindElement(controlId, UiActions.GetOptions))
                    .GetOptions().ToArray());
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
        public static ActionResult SetGridText(string formId, string controlId, string text)
        {
            ValidateFormIdFormat(formId);
            // Identify the form (its own thread) synchronously, then post the whole paste onto that thread: resolve
            // the grid there, and SetGridText gates it and pastes. This named/convenience verb waits out the posted
            // paste (the count settling, or a type-conversion alert the paste raises appearing, which the caller
            // then drives) so it returns only once the paste has taken effect. The PerformAction escape-hatch path
            // (UiActions.SetGridText) stays fire-and-forget.
            var formElement = (FormElement) FindFormById(formId);
            return WaitForGesture(formElement.Form, () => UiActions.SetGridText.Invoke(formElement.FindGrid(controlId), text ?? string.Empty));
        }

        /// <summary>
        /// Moves the current cell of a grid on a form (move there before pasting with
        /// <see cref="SetGridText"/> or opening the cell's context menu). <paramref name="column"/> is the
        /// visible-column index and <paramref name="row"/> is the row index -- the same indices the grid
        /// reports columns and rows in. See <see cref="IJsonToolService"/>.
        /// </summary>
        public static ActionResult SetCurrentCellAddress(string formId, string controlId, int column, int row)
        {
            ValidateFormIdFormat(formId);
            // Identify the form (its own thread) synchronously, then post the whole move onto that thread: resolve
            // the grid there, and SetCurrentCellAddress gates it and moves the cell. This named/convenience verb
            // waits out the posted move (the count settling) so it returns only once the cell has moved. The
            // PerformAction escape-hatch path (UiActions.SetCurrentCellAddress) stays fire-and-forget.
            var formElement = (FormElement) FindFormById(formId);
            return WaitForGesture(formElement.Form, () => UiActions.SetCurrentCellAddress.Invoke(formElement.FindGrid(controlId), new[] { column, row }));
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
        public static object PerformAction(UiElementPath path, string action, object value)
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
            return ResolveForm(path.GetRoot().Text).PerformAction(path, uiAction, value);
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
                    // The main window itself, so its id (and thus its menus/toolbars) is discoverable.
                    formInfos.Add(new FormInfo
                    {
                        Type = skylineWindow.GetType().Name,
                        Title = GetFormTitle(skylineWindow),
                        HasGraph = false,
                        DockState = @"Main",
                        Id = GetFormId(skylineWindow),
                        ModalNestingCount = UiServiceDispatcher.TryGetPreShowActionCount(skylineWindow),
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
                            ModalNestingCount = UiServiceDispatcher.TryGetPreShowActionCount(form),
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
                        ModalNestingCount = UiServiceDispatcher.TryGetPreShowActionCount(form),
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
        // modal loop rather than on it. A managed form is then found (also off the UI thread). Throws if no open
        // window has the id.
        public static IFormElement ResolveForm(string formId)
        {
            ValidateFormIdFormat(formId);
            // The form is the root of its path; record it so get_controls parents the controls onto it.
            var formPath = new UiElementPath(null, formId, null, @"Form");
            foreach (var dialog in NativeDialog.GetOpenDialogs())
                if (dialog.FormId == formId)
                {
                    dialog.Path = formPath;
                    return dialog;
                }
            // The managed form, already built (with its handle) by GetOpenFormElements; recording its path is a
            // plain field set, so no UI-thread marshal is needed.
            var formElement = (FormElement) FindFormById(formId);
            formElement.Path = formPath;
            return formElement;
        }

        // Resolves the managed form named by formId and runs func against it on that form's own UI thread --
        // the correct thread even for a form created on its own background thread (e.g. a
        // BackgroundThreadLongWaitDlg), whose controls must be touched through its message loop, not the main
        // window's (see UiElement.InvokeOnUiThread). The form lookup runs on the calling thread; func then runs on
        // the form's thread, where it walks/reads the control tree. Must be called off the UI thread.
        private static T OnFormThread<T>(string formId, Func<FormElement, T> func)
        {
            // Find the form (any thread); it is already built (with its handle). Run func on the form's OWN thread:
            // its controls must be touched through that form's Invoke.
            var formElement = (FormElement) FindFormById(formId);
            return InvokeOnUiThread(() => func(formElement), formElement.Form);
        }

        public static string GetGraphData(string graphId, string filePath)
        {
            var form = ((FormElement) FindFormById(graphId)).Form as DockableFormEx;
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
            form = ((FormElement) FindFormById(graphId)).Form as DockableFormEx;
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
        public static IEnumerable<IFormElement> GetOpenFormElements()
        {
            foreach (var window in NativeDialog.GetTopLevelWindows())
                yield return window;

            foreach (var docked in GetDockedForms())
                yield return docked;
        }

        // The forms docked in the main Skyline window, each as a FormElement, or empty when the main window is not
        // up. Callable from any thread: the docked forms are child windows read through the main window's DockPanel,
        // which must be on its UI thread, so this Invokes the read (and the FormElement construction, which captures
        // each handle) onto that thread. Hidden/unknown-state docked forms are skipped (not on screen).
        private static IList<IFormElement> GetDockedForms()
        {
            var mainWindow = Program.MainWindow;
            if (mainWindow == null)
                return new List<IFormElement>();
            return (IList<IFormElement>) mainWindow.Invoke((Func<IList<IFormElement>>) (() =>
            {
                var result = new List<IFormElement>();
                foreach (var form in mainWindow.DockPanel.Contents.OfType<DockableFormEx>())
                {
                    var dockState = form.DockState;
                    if (dockState == DockState.Hidden || dockState == DockState.Unknown)
                        continue;
                    result.Add(new FormElement(form));
                }
                return result;
            }));
        }

        /// <summary>
        /// Finds a managed form by its TypeName:Title identifier -- the main window, a form docked in it, or an
        /// open dialog -- and returns it as the already-built <see cref="FormElement"/> (its window handle already
        /// captured). Matches against <see cref="GetOpenFormElements"/>, which enumerates the top-level windows off
        /// any thread and reads the docked forms through the main window's own Invoke, so this may be called from
        /// any thread (the caller need not marshal it onto the UI thread).
        /// </summary>
        private static IFormElement FindFormById(string formId)
        {
            ValidateFormIdFormat(formId);
            foreach (var window in GetOpenFormElements())
                if (window is FormElement formElement && formElement.FormId == formId)
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
