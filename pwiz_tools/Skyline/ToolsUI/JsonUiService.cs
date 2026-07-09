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
                // StartPage before it exists (and falls back to a background thread if neither is up).
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

        // ===== Interactive-modal tracking and convenience-method waits (connector) =====

        // Whether a modal is one the caller would drive (true) rather than a progress/wait dialog the work drives
        // itself (false). Excludes the LongWaitDlg family (its BackgroundThreadLongWaitDlg subclass included), the
        // same way the dialog-watch already skips them.
        internal static bool IsInteractiveModal(Form form)
        {
            return form != null && form.Modal && !(form is LongWaitDlg);
        }

        // The pre-show count for each interactive modal the connector has SEEN APPEAR: the UnfinishedActionCount
        // that existed just before the modal was shown. Recorded ONLY in the primary path -- when WaitForGesture
        // detects that the gesture it posted opened a new interactive modal, it records that modal here. There is
        // no process-wide hook and no close hook, so entries are pruned lazily whenever the tracker is read or
        // written. The Form is held by a WEAK reference so a stale entry (this static list outlives any single
        // wait) never keeps a closed dialog -- and through it the SkylineWindow / document -- alive. A modal
        // opened another way simply has no entry, and Accept/Cancel then wait for its disappearance only. Kept
        // LIFO (modals stack) but searched by Form. Guarded by its own lock.
        private static readonly List<KeyValuePair<WeakReference<Form>, int>> _modalPreShowCounts =
            new List<KeyValuePair<WeakReference<Form>, int>>();

        // Drops entries whose Form has been collected or disposed. Called under the lock, in place of a close hook.
        private static void PruneDeadModals()
        {
            _modalPreShowCounts.RemoveAll(e => !e.Key.TryGetTarget(out var form) || form.IsDisposed);
        }

        // Records a newly appeared interactive modal's pre-show count, unless one is already recorded for it.
        private static void RecordModalPreShowCount(Form form, int preShowCount)
        {
            lock (_modalPreShowCounts)
            {
                PruneDeadModals();
                if (!_modalPreShowCounts.Any(e => e.Key.TryGetTarget(out var f) && ReferenceEquals(f, form)))
                    _modalPreShowCounts.Add(new KeyValuePair<WeakReference<Form>, int>(new WeakReference<Form>(form), preShowCount));
            }
        }

        // The recorded pre-show count for a modal, or the given default when none is recorded.
        private static int PeekModalPreShowCount(Form form, int defaultCount)
        {
            return TryGetPreShowActionCount(form) ?? defaultCount;
        }

        // The recorded pre-show count for a form, or null when it is not a tracked interactive modal -- surfaced
        // in each FormInfo (get_open_forms) so a caller can confirm the tracker's state.
        internal static int? TryGetPreShowActionCount(Form form)
        {
            lock (_modalPreShowCounts)
            {
                PruneDeadModals();
                foreach (var entry in _modalPreShowCounts)
                    if (entry.Key.TryGetTarget(out var f) && ReferenceEquals(f, form))
                        return entry.Value;
            }
            return null;
        }

        // The interactive modal most recently seen appear and still alive (the LIFO top of the tracker), or null.
        private static Form CurrentTopInteractiveModal()
        {
            lock (_modalPreShowCounts)
            {
                PruneDeadModals();
                for (int i = _modalPreShowCounts.Count - 1; i >= 0; i--)
                    if (_modalPreShowCounts[i].Key.TryGetTarget(out var form))
                        return form;
                return null;
            }
        }

        // How long the message-loop-progress watchdog waits between probes, and how many consecutive
        // actively-pumping probes with no completion and no LongWaitDlg it tolerates before giving up. Each probe
        // is a SYNCHRONOUS marshaled read, so the counter advances only while the UI message loop is pumping (a
        // non-pumping/blocked UI thread simply parks the probe and never trips the watchdog).
        private const int PROGRESS_POLL_MILLIS = 1000;
        private const int NO_PROGRESS_LIMIT = 10;

        // What a single synchronous, UI-thread probe of the modal/count state reports back to the waiting worker.
        private class ModalProbeResult
        {
            public bool NewInteractiveModal;   // a modal not open at the start appeared and is not a LongWaitDlg
            public Form NewModalForm;          // the managed interactive modal to record (null for a native one)
            public bool TopModalDismissed;     // the interactive modal open at the start is gone
            public bool LongWaitPresent;       // a busy progress form (LongWaitDlg or other ILongWaitForm) is open
            public int UnfinishedCount;        // UnfinishedActionCount sampled in the same UI-thread pass
        }

        // Samples the modal/count state on the UI thread in one pass, relative to the state captured at the start
        // of the wait. Runs synchronously (the caller marshals it), so returning proves the message loop pumped.
        private static ModalProbeResult ProbeModals(HashSet<IntPtr> startModalHandles, IntPtr topModalHandle)
        {
            var currentHandles = FindModalDialogWindows();
            var forms = FormUtil.OpenForms.Where(f => f.IsHandleCreated).ToDictionary(f => f.Handle);
            bool IsLongWait(IntPtr h) => forms.TryGetValue(h, out var f) && f is LongWaitDlg;
            // A form that is currently driving long-running work in its own progress display (a LongWaitDlg,
            // which is always busy, or e.g. the Import Peptide Search wizard while its search is running). The
            // watchdog rides through any of these, not just a LongWaitDlg.
            bool IsBusyProgressForm(IntPtr h) => forms.TryGetValue(h, out var f) && f is ILongWaitForm { IsBusy: true };

            var result = new ModalProbeResult { UnfinishedCount = UnfinishedActionCount };
            // A new modal that is not a LongWaitDlg progress dialog is an interactive stop (managed OR native --
            // a native window has no managed form, so it is not a LongWaitDlg either). Mirrors the dialog-watch.
            foreach (var hwnd in currentHandles)
            {
                if (startModalHandles.Contains(hwnd) || IsLongWait(hwnd))
                    continue;
                result.NewInteractiveModal = true;
                if (forms.TryGetValue(hwnd, out var form) && IsInteractiveModal(form))
                {
                    result.NewModalForm = form;
                    break; // prefer a managed form to record
                }
            }
            // "Dismissed" means the top modal's window is actually GONE, not merely disabled. A modal that has
            // itself opened a nested child modal is disabled (so it drops out of FindModalDialogWindows, whose
            // set only holds enabled modal windows) yet is still visible -- it has NOT been dismissed and its
            // opener's work has not resumed. Testing the window's visibility (rather than modal-set membership)
            // keeps a still-open parent from being mistaken for a dismissed one -- which would otherwise drop the
            // ride-through target to its pre-show count while its opener delegate is still legitimately blocked.
            result.TopModalDismissed = topModalHandle != IntPtr.Zero && !User32.IsWindowVisible(topModalHandle);
            result.LongWaitPresent = currentHandles.Any(IsBusyProgressForm);
            return result;
        }

        // The wait a named/convenience method runs after posting its (fire-and-forget) gesture. Unifies three
        // cases: a plain mutating action returns when UnfinishedActionCount falls back to where it started; an
        // action that opens an interactive modal returns as soon as the modal appears (recording its pre-show
        // count -- the ONLY place a pre-show count is recorded); an action that dismisses the top modal waits
        // until the count falls to the pre-show level its opener left, riding through any LongWaitDlg the resumed
        // work shows. Progress is gauged by a synchronous marshaled probe each cycle (see ProbeModals): the wait
        // aborts only after NO_PROGRESS_LIMIT consecutive actively-pumping probes show neither completion nor a
        // LongWaitDlg. Must be called off the UI thread.
        internal static void WaitForGesture(Action postGesture)
        {
            int initialCount = UnfinishedActionCount;
            var startModalHandles = new HashSet<IntPtr>(FindModalDialogWindows());
            var topModalAtStart = CurrentTopInteractiveModal();
            // Capture the opener's pre-show count and the modal's window handle now, before it (and its tracker
            // entry) can close, so the ride-through target and the "still open" test do not race the close.
            int topModalPreShowCount = topModalAtStart != null
                ? PeekModalPreShowCount(topModalAtStart, initialCount) : initialCount;
            IntPtr topModalHandle = topModalAtStart != null
                ? InvokeOnUiThread(() => topModalAtStart.IsHandleCreated ? topModalAtStart.Handle : IntPtr.Zero)
                : IntPtr.Zero;

            postGesture();

            int target = initialCount;
            int noProgress = 0;
            while (true)
            {
                // Probe first (before any sleep), so a fast gesture -- which the UI thread runs before this
                // synchronously-queued probe, FIFO -- is seen complete without waiting a whole cycle.
                var probe = InvokeOnUiThread(() => ProbeModals(startModalHandles, topModalHandle));

                if (probe.NewInteractiveModal)
                {
                    // A new interactive modal (managed or native) appeared -- the caller will drive it. Record its
                    // pre-show count if it is a managed form we can track (a native dialog has none). Primary path.
                    if (probe.NewModalForm != null)
                        RecordModalPreShowCount(probe.NewModalForm, initialCount);
                    return;
                }

                if (topModalAtStart != null && target == initialCount && probe.TopModalDismissed)
                {
                    // The top modal open at the start was dismissed; drop the target to the level its opener left,
                    // so the work the dismissal resumes (which may show a LongWaitDlg) is waited out.
                    target = topModalPreShowCount;
                }

                // <= (not ==): the count can settle BELOW target and be missed by an exact match. A dismissed
                // modal whose pre-show count was never tracked falls back to an upper-bound target (initialCount,
                // which still counts the modal's blocked opener); after the dismissal that opener completes and
                // the count drops past it. It stays above target while the gesture is in flight, so once at or
                // below the pre-show level the gesture -- and any work its dismissal resumed -- is done.
                if (probe.UnfinishedCount <= target)
                    return;

                if (probe.LongWaitPresent)
                    noProgress = 0; // a progress dialog is up: the operation is advancing, keep waiting
                else if (++noProgress >= NO_PROGRESS_LIMIT)
                {
                    string openForms = InvokeOnUiThread(() =>
                        string.Join(@", ", FormUtil.OpenForms.Select(f => f.GetType().Name)));
                    throw new InvalidOperationException(new LlmInstruction(GestureTimeoutMessage +
                        string.Format(@" [initialCount={0}, target={1}, current={2}, topModalDismissed={3}, openForms=[{4}]]",
                            initialCount, target, probe.UnfinishedCount, probe.TopModalDismissed, openForms)));
                }

                Thread.Sleep(PROGRESS_POLL_MILLIS);
            }
        }

        // The message the watchdog throws: about NO_PROGRESS_LIMIT seconds of an actively-pumping message loop
        // with no completion and no progress dialog, which points at a slow Skyline operation not showing one.
        private static string GestureTimeoutMessage =>
            LlmInstruction.Format(
                @"Timed out after about {0} seconds of an active Skyline message loop with no long-wait (progress) dialog showing. A wait this long with no progress dialog usually means a Skyline operation is slow but is not surfacing a LongWaitDlg. If a dialog is open, drive it with skyline_get_open_forms and the accept/cancel/click verbs.",
                NO_PROGRESS_LIMIT.ToString());

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
        /// Accepts the dialog named by <paramref name="formId"/> -- presses its default (accept) button, fire and
        /// forget -- then WAITS until the dialog is gone (inspecting the form object's own Visible / handle state,
        /// not <see cref="System.Windows.Forms.Application.OpenForms"/>) and, when its pre-show count is known,
        /// until <see cref="UnfinishedActionCount"/> has fallen back to it (so the work the accept resumes is
        /// waited out). See <see cref="IJsonToolService"/>.
        /// </summary>
        public static void Accept(string formId)
        {
            ValidateFormIdFormat(formId);
            AcceptOrCancel(ResolveForm(formId), true);
        }

        /// <summary>
        /// Cancels the dialog named by <paramref name="formId"/> -- presses its cancel button (or closes it when
        /// it has none), fire and forget -- then waits for it to disappear the same way <see cref="Accept"/> does.
        /// See <see cref="IJsonToolService"/>.
        /// </summary>
        public static void Cancel(string formId)
        {
            ValidateFormIdFormat(formId);
            AcceptOrCancel(ResolveForm(formId), false);
        }

        // Posts the accept/cancel gesture and waits for the form to go (and, if known, for the action count to
        // return to the form's pre-show level). Must be called off the UI thread.
        private static void AcceptOrCancel(IFormElement formElement, bool accept)
        {
            var managedForm = (formElement as FormElement)?.Form;
            int? preShowCount = managedForm != null ? TryGetPreShowActionCount(managedForm) : null;

            // Post the gesture fire-and-forget (a managed form through its post-only helper, so the wait is here
            // and not doubled inside the helper; a native dialog's Accept/Close are already fire-and-forget).
            if (formElement is FormElement form)
            {
                if (accept) form.PostAccept(); else form.PostCancel();
            }
            else if (accept)
                formElement.Accept();
            else
                formElement.Close();

            // Wait on the same message-loop-progress watchdog WaitForGesture uses: each cycle a SYNCHRONOUS
            // marshaled probe samples whether the form is gone (and the count has returned) and whether a busy
            // progress form (LongWaitDlg or other ILongWaitForm) is up, so the watchdog advances only while the
            // loop is actually pumping.
            int noProgress = 0;
            while (true)
            {
                var probe = InvokeOnUiThread(() => new
                {
                    StillOpen = IsFormStillOpenOnUi(formElement, managedForm),
                    LongWaitPresent = FormUtil.OpenForms.Any(f => f is ILongWaitForm { IsBusy: true }),
                    Count = UnfinishedActionCount
                });
                bool countReached = !preShowCount.HasValue || probe.Count == preShowCount.Value;
                if (!probe.StillOpen && countReached)
                    return;

                if (probe.LongWaitPresent)
                    noProgress = 0;
                else if (++noProgress >= NO_PROGRESS_LIMIT)
                    throw new InvalidOperationException(LlmInstruction.Format(
                        @"Timed out after about {0} seconds waiting for form {1} to close, with an active message loop and no long-wait (progress) dialog showing.",
                        NO_PROGRESS_LIMIT.ToString(), formElement.FormId));

                Thread.Sleep(PROGRESS_POLL_MILLIS);
            }
        }

        // Whether the resolved form is still on screen, inspecting the form INSTANCE directly (its own
        // Visible/handle, or a native window's visibility) rather than Application.OpenForms / IsFormOpen. Must
        // run on the UI thread for a managed form (it reads control state).
        private static bool IsFormStillOpenOnUi(IFormElement formElement, Form managedForm)
        {
            if (managedForm != null)
                return !managedForm.IsDisposed && managedForm.IsHandleCreated && managedForm.Visible;
            if (formElement is NativeDialog nativeDialog)
                return User32.IsWindowVisible(nativeDialog.WindowHandle);
            return false;
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
            // through the same named-action wait ClickToolStripItem uses.
            var formRoot = (UiElement) ResolveForm(formId);
            var pathToClick = itemPath;
            var element = formRoot.InvokeOnUiThread(() =>
                RequireAction(ResolvePath(pathToClick, formRoot), UiActions.Click));
            WaitForGesture(() => UiActions.Click.Invoke(element, null));
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
        public static void SetGridText(string formId, string controlId, string text)
        {
            ValidateFormIdFormat(formId);
            // Resolve the grid synchronously; the action's Invoke gates it and pastes fire-and-forget. This
            // named/convenience verb then waits out the posted paste (the count settling, or a type-conversion
            // alert the paste raises appearing, which the caller then drives) so it returns only once the paste
            // has taken effect. The PerformAction escape-hatch path (UiActions.SetGridText) stays fire-and-forget.
            var grid = OnFormThread(formId, formElement => formElement.FindGrid(controlId));
            WaitForGesture(() => UiActions.SetGridText.Invoke(grid, text ?? string.Empty));
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
            // This named/convenience verb then waits out the posted move (the count settling) so it returns only
            // once the cell has moved. The PerformAction escape-hatch path (UiActions.SetCurrentCellAddress) stays
            // fire-and-forget.
            var grid = OnFormThread(formId, formElement => formElement.FindGrid(controlId));
            WaitForGesture(() => UiActions.SetCurrentCellAddress.Invoke(grid, new[] { column, row }));
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
                        PreShowActionCount = TryGetPreShowActionCount(skylineWindow),
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
                            PreShowActionCount = TryGetPreShowActionCount(form),
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
                        PreShowActionCount = TryGetPreShowActionCount(form),
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
            return InvokeOnUiThread(() => (IFormElement) new FormElement(FindFormById(formId)) { Path = formPath });
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

            // The main Skyline window itself -- it is not in DockPanel.Contents, but it owns the main menu
            // and toolbars, so it must be resolvable to walk/drive them (e.g. View > Libraries > Ion Types).
            if (skylineWindow != null
                && skylineWindow.GetType().Name == typeName && GetFormTitle(skylineWindow) == title)
                return skylineWindow;

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
