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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Runs one unit of connector work off the caller (pipe) thread and watches for modal dialogs. It unifies the
    /// two waits <see cref="JsonUiService"/> used to have -- the dialog-watch (a value-producing read run on a
    /// background thread, throwing a blocking modal's message, no timeout) and the posted-gesture wait (a
    /// fire-and-forget UI-thread gesture, riding a busy <c>ILongWaitForm</c>, returning on a new interactive modal,
    /// timing out via a message-loop-progress watchdog). Construct it, set the properties, then call
    /// <see cref="Run{T}"/> (or the void <see cref="Run(System.Action)"/>).
    ///
    /// The work is counted in <see cref="JsonUiService.ModalNestingCount"/> from before it is dispatched until
    /// its delegate returns, so an action that opens a modal stays counted while its delegate blocks in the modal
    /// loop. The shared wait loop then: polls the modals; rides through a busy progress form; on a new
    /// blocking/interactive modal either throws its message or stops and records it (per <see cref="ThrowOnDialog"/>);
    /// completes when the count has drained to its target (lowering the target when the top modal open at the start
    /// is dismissed -- inert for a read that dismisses nothing); and, if <see cref="TimeOut"/>, trips the watchdog.
    /// Must be called off the UI thread.
    /// </summary>
    internal class UiServiceDispatcher
    {
        /// <summary>
        /// How the work is launched (set by the constructor). Null runs it on a BACKGROUND thread
        /// (<see cref="ActionUtil.RunAsync"/>) -- the dialog-watch, whose value-producing work may block on a modal,
        /// so it must run off the caller thread for the wait loop to watch. A target FORM (with <see cref="TimeOut"/>)
        /// posts the WHOLE gesture with <see cref="Control.BeginInvoke(System.Delegate)"/> onto that form's OWN UI
        /// thread. The synchronous-invoke config (see <see cref="Synchronous"/>) marshals straight through this
        /// control with <see cref="Control.Invoke(System.Delegate)"/>. This is a <see cref="Control"/> so a form on
        /// its own thread is driven through the thread that owns it, not a global window.
        /// </summary>
        public Control Dispatcher { get; }

        /// <summary>
        /// What to do when a blocking/interactive modal appears: THROW its message (the dialog-watch) when true, or
        /// STOP the wait and record it (the posted-gesture wait) when false.
        /// </summary>
        public bool ThrowOnDialog { get; set; }

        /// <summary>
        /// When true, apply the ~10s message-loop-progress watchdog (the posted-gesture wait); when false, wait
        /// indefinitely for the work to drain (the dialog-watch).
        /// </summary>
        public bool TimeOut { get; set; }

        /// <summary>
        /// When true (the InvokeOnUiThread config), <see cref="Run{T}"/> is a plain SYNCHRONOUS UI-thread marshal
        /// (a <see cref="Control.Invoke(System.Delegate)"/> that blocks until the work returns its result): no
        /// pending-action count, no modal wait loop. It skips the count deliberately -- a synchronous read must not
        /// move <see cref="ModalNestingCount"/>, because callers read that count from INSIDE such an invoke
        /// (e.g. the Accept/Cancel wait) and an increment would corrupt it -- and a read cannot afford the
        /// posted-and-polled wait a gesture uses (that would add a poll interval per read). Exceptions propagate raw.
        /// </summary>
        public bool Synchronous { get; set; }

        /// <summary>
        /// After <see cref="Run{T}"/> returns for a posted-gesture wait: true when the wait ended because a new
        /// interactive modal appeared (the gesture opened, or left open, a dialog) rather than because the work
        /// drained. False when the work completed normally. Meaningless for the synchronous / dialog-watch configs.
        /// </summary>
        public bool StoppedOnModal { get; private set; }

        /// <summary>The message of the modal that stopped the wait (see <see cref="StoppedOnModal"/>), else null.</summary>
        public string BlockingMessage { get; private set; }

        /// <summary>
        /// The window this wait is dismissing (an accept or cancel), or null for a plain gesture. When set it IS the
        /// top modal <see cref="Run{T}"/> waits on -- we know exactly which window we expect to disappear -- and Run
        /// completes only once it is actually GONE and the count has drained to its pre-show level, not on the count
        /// alone. A native accept needs this: its Win32 gesture is UNCOUNTED, so the count returns to initialCount
        /// the instant the gesture is posted, before the window has closed. The same wait is correct for a managed
        /// form, so both go through it.
        /// </summary>
        public IFormElement AwaitedDismissal { get; set; }

        /// <summary>
        /// When true (the accept/cancel wait), <see cref="Run{T}"/> runs the gesture on the CALLER's thread instead
        /// of dispatching it: a managed accept's <c>PostAccept</c> marshals to the UI thread itself, and a native
        /// dialog's UI Automation must NOT touch the UI thread that owns the dialog's modal loop. The gesture only
        /// posts (it returns at once); the wait loop then runs on the caller's thread as usual.
        /// </summary>
        public bool WorkOnCallerThread { get; set; }

        /// <summary>Constructs a dispatcher for the given launch control (null = background/dialog-watch).</summary>
        public UiServiceDispatcher(Control dispatcher)
        {
            Dispatcher = dispatcher;
        }

        // ===== Factories: one per use, each constructing + configuring an instance (callers use these, not new) =====

        /// <summary>The posted-gesture wait (WaitForGesture): BeginInvoke the whole gesture onto <paramref name="form"/>'s
        /// own thread, stop-and-record on a new modal, ride the ~10s watchdog.</summary>
        internal static UiServiceDispatcher ForGesture(Control form) =>
            new UiServiceDispatcher(form) { ThrowOnDialog = false, TimeOut = true };

        /// <summary>The dialog-watch (RunWithDialogWatch): run the value work on a background thread, throw a
        /// blocking modal's message, no timeout.</summary>
        internal static UiServiceDispatcher ForDialogWatch() =>
            new UiServiceDispatcher(null) { ThrowOnDialog = true, TimeOut = false };

        /// <summary>The accept/cancel wait shared by a managed form and a native dialog (WaitForOkDialog): run the
        /// dismiss gesture on the caller's thread, stop-and-record on a new modal, ride the ~10s watchdog, and
        /// complete only once <paramref name="dialog"/>'s window has closed AND the count has drained to the level
        /// its opener left (the action that showed the dialog returned).</summary>
        internal static UiServiceDispatcher ForOkDialog(IFormElement dialog) =>
            new UiServiceDispatcher(null)
                { ThrowOnDialog = false, TimeOut = true, AwaitedDismissal = dialog, WorkOnCallerThread = true };

        /// <summary>The synchronous UI-thread invoke (InvokeOnUiThread): a direct <see cref="Control.Invoke(System.Delegate)"/>
        /// against <paramref name="dispatcher"/> (or the UI-thread window when null), no count, no wait loop.</summary>
        internal static UiServiceDispatcher ForInvoke(Control dispatcher) =>
            new UiServiceDispatcher(dispatcher) { Synchronous = true };

        // The process-wide count of fire-and-forget connector actions posted but not yet finished. It lives here,
        // with the wait machinery that reads it: Run wraps its dispatched work in it (increment before dispatch,
        // decrement in the delegate's finally), and JsonUiService.BeginInvokeOnUiThread counts its posted gestures
        // the same way. An action that opens a modal stays counted while its delegate blocks in the modal loop, so
        // the count is usually the number of open connector-raised modals, and the wait loop polls it to know when
        // a gesture's effect has drained. Exposed read-only through JsonUiService.ModalNestingCount (the
        // IJsonToolService accessor).
        private static int _modalNestingCount;

        /// <summary>The pending fire-and-forget action count (see <see cref="JsonUiService.ModalNestingCount"/>).</summary>
        public static int ModalNestingCount => Volatile.Read(ref _modalNestingCount);

        // Increment/decrement the pending-action count: Run wraps its own dispatched work, and
        // JsonUiService.BeginInvokeOnUiThread wraps its posted gestures, with these.
        internal static void IncrementModalNestingCount() => Interlocked.Increment(ref _modalNestingCount);
        internal static void DecrementModalNestingCount() => Interlocked.Decrement(ref _modalNestingCount);

        // ===== UI-thread marshaling (owned here; JsonUiService.InvokeOnUiThread delegates to these) =====

        /// <summary>
        /// The window used to marshal onto the UI thread (and to capture that thread's synchronization context):
        /// the main window once it exists, otherwise the StartPage while it is showing. Null only very early in
        /// startup before either exists. No disposed/handle checks: the tool service is shut down before the main
        /// window closes, so it is never asked to marshal through a dead window.
        /// </summary>
        internal static Control UiThreadWindow => (Control) Program.MainWindow ?? Program.StartWindow;

        // The UI thread's synchronization context, captured on this dispatcher's FIRST marshal (see FirstMarshal)
        // and used for every marshal after that. Null until then.
        private WindowsFormsSynchronizationContext _synchronizationContext;

        /// <summary>
        /// Marshals <paramref name="action"/> onto the UI thread and BLOCKS until it has run (a synchronous invoke).
        /// The first marshal goes through the Control -- assumed to have a valid handle at construction -- and, while
        /// running there, captures that thread's WinForms synchronization context; every marshal after that goes
        /// through the captured context instead, so a form this dispatcher is in the middle of closing is never
        /// touched again. Must be called off the UI thread.
        /// </summary>
        private void Send(Action action)
        {
            if (_synchronizationContext != null)
                _synchronizationContext.Send(_ => action(), null);
            else
                FirstMarshal(action, synchronous: true);
        }

        /// <summary>Marshals <paramref name="action"/> onto the UI thread fire-and-forget (an async post); otherwise
        /// like <see cref="Send"/>. Must be called off the UI thread.</summary>
        private void Post(Action action)
        {
            if (_synchronizationContext != null)
                _synchronizationContext.Post(_ => action(), null);
            else
                FirstMarshal(action, synchronous: false);
        }

        // The first marshal for this dispatcher: hop onto the UI thread through the Control, capture that thread's
        // synchronization context (so later marshals bypass the Control), then run the action. Before any window
        // exists (very early startup) there is nothing to marshal through, so the action runs on the calling thread.
        private void FirstMarshal(Action action, bool synchronous)
        {
            var control = Dispatcher ?? UiThreadWindow;
            if (control == null)
            {
                action();
                return;
            }
            void OnUiThread()
            {
                _synchronizationContext ??= SynchronizationContext.Current as WindowsFormsSynchronizationContext
                                            ?? new WindowsFormsSynchronizationContext();
                action();
            }
            if (synchronous)
                control.Invoke((Action) OnUiThread);
            else
                control.BeginInvoke((Action) OnUiThread);
        }

        /// <summary>
        /// Posts <paramref name="action"/> to the UI thread fire-and-forget, through <see cref="UiThreadWindow"/>
        /// (or a background thread if neither window is up yet). The RAW post that does NOT count in
        /// <see cref="ModalNestingCount"/> (unlike the connector's counted
        /// <see cref="JsonUiService.BeginInvokeOnUiThread(System.Action,System.Windows.Forms.Control)"/>). Off the UI thread.
        /// </summary>
        internal static void PostToUiThreadWindow(Action action)
        {
            var window = UiThreadWindow;
            if (window != null)
                window.BeginInvoke(action);
            else
                CommonActionUtil.RunAsync(action);
        }

        // ===== Modal-dialog detection and messages =====

        // The modal dialogs currently open in this process, each wrapped as the connector form abstraction that
        // drives it -- managed modals as a FormElement, truly-native ones as a NativeDialog. A single Win32
        // enumeration of the modal windows is the sole source (see NativeDialog.GetModalDialogs), so there is no
        // Form<->handle pairing and no Form.Handle read that would trip the cross-thread check; safe off the poll
        // thread.
        internal static IList<IFormElement> GetOpenModals()
        {
            return NativeDialog.GetModalDialogs().ToList();
        }

        // The message of the first interactive modal currently blocking the UI (an alert/error text or a dialog's
        // body/caption), or null if none blocks (every open modal is a progress dialog). Used by the form gate
        // (UiElement.VerifyFormInteractable).
        internal static string BlockingAlertMessage()
        {
            return GetOpenModals().Where(m => m.IsInteractiveModal).Select(m => m.DetailedMessage).FirstOrDefault();
        }

        // ===== Interactive-modal pre-show-count tracker =====

        // The pre-show count for each interactive modal the connector has SEEN APPEAR: the ModalNestingCount that
        // existed just before the modal was shown. Recorded ONLY in the primary path -- when Run detects that the
        // gesture it posted opened a new interactive modal, it records that modal here. Keyed by WINDOW HANDLE, so a
        // native dialog can be tracked exactly like a managed form (both expose Hwnd). The key is a value, so a
        // stale entry never keeps a closed dialog -- and through it the SkylineWindow / document -- alive. There is
        // no close hook, so entries are pruned lazily (a closed dialog's window is destroyed) whenever the tracker
        // is read or written. Kept LIFO (modals stack). Guarded by its own lock.
        private static readonly List<KeyValuePair<IntPtr, int>> _modalPreShowCounts = new List<KeyValuePair<IntPtr, int>>();

        // Drops entries whose window is no longer among the currently-open top-level windows (a closed dialog).
        // Called once per wait, right after that window set is captured (see Run) -- there is no other time the
        // tracker needs pruning, and doing it against the fresh set avoids a stale-handle race with handle reuse.
        internal static void PruneModalsNotOpen(HashSet<IntPtr> openWindows)
        {
            lock (_modalPreShowCounts)
                _modalPreShowCounts.RemoveAll(e => !openWindows.Contains(e.Key));
        }

        // Records a newly appeared interactive modal's pre-show count, unless one is already recorded for it.
        internal static void RecordModalPreShowCount(IFormElement modal, int preShowCount)
        {
            lock (_modalPreShowCounts)
                if (_modalPreShowCounts.All(e => e.Key != modal.Hwnd))
                    _modalPreShowCounts.Add(new KeyValuePair<IntPtr, int>(modal.Hwnd, preShowCount));
        }

        // The recorded pre-show count for a modal, or the given default when none is recorded.
        internal static int PeekModalPreShowCount(IFormElement modal, int defaultCount)
        {
            return TryGetPreShowActionCount(modal) ?? defaultCount;
        }

        // The recorded pre-show count for a modal (by its window handle), or null when it is not a tracked
        // interactive modal -- surfaced in each FormInfo (get_open_forms) so a caller can confirm the tracker state.
        internal static int? TryGetPreShowActionCount(IFormElement modal) => TryGetPreShowActionCount(modal.Hwnd);

        // The recorded pre-show count for a window handle, or null when it is untracked.
        internal static int? TryGetPreShowActionCount(IntPtr hwnd)
        {
            lock (_modalPreShowCounts)
                foreach (var entry in _modalPreShowCounts)
                    if (entry.Key == hwnd)
                        return entry.Value;
            return null;
        }

        // The interactive modal most recently seen appear and still alive (the LIFO top of the tracker), wrapped as
        // the connector form abstraction that drives it -- a managed FormElement or a native NativeDialog -- or
        // null. Built from the recorded handle, so it is safe on this poll thread.
        internal static IFormElement CurrentTopInteractiveModal()
        {
            lock (_modalPreShowCounts)
                return _modalPreShowCounts.Count > 0
                    ? NativeDialog.WrapWindow(_modalPreShowCounts[_modalPreShowCounts.Count - 1].Key)
                    : null;
        }

        // ===== The modal/count probe =====

        // How long the message-loop-progress watchdog waits between probes, and how many consecutive
        // actively-pumping probes with no completion and no LongWaitDlg it tolerates before giving up. Each probe
        // is a SYNCHRONOUS marshaled read, so the counter advances only while the UI message loop is pumping (a
        // non-pumping/blocked UI thread simply parks the probe and never trips the watchdog). DIALOG_POLL is the
        // faster cadence the no-timeout dialog-watch polls at.
        internal const int DIALOG_POLL_INTERVAL_MILLIS = 100;
        internal const int PROGRESS_POLL_MILLIS = 1000;
        internal const int NO_PROGRESS_LIMIT = 10;

        // What a single synchronous, UI-thread probe of the modal/count state reports back to the waiting worker.
        internal class ModalProbeResult
        {
            public bool NewInteractiveModal;   // a modal not open at the start appeared and is not a progress dialog
            public IFormElement NewModal;      // the new interactive modal to record a pre-show count for (managed or native)
            public string BlockingMessage;     // the first new blocking modal's message (throw text / record); null if none
            public bool TopModalDismissed;     // the interactive modal open at the start is gone
            public bool LongWaitPresent;       // a busy progress form (LongWaitDlg or other ILongWaitForm) is open
            public int ModalNestingCount;        // ModalNestingCount sampled in the same UI-thread pass
        }

        // Samples the modal/count state in one pass, relative to the state captured at the start of the wait. Runs
        // on the caller's poll thread (FormUtil.OpenForms is thread-safe), asking each modal (a FormElement or
        // NativeDialog) about itself -- no window handles, no UI-thread marshal.
        internal static ModalProbeResult ProbeModals(HashSet<IntPtr> startModals, IFormElement topModal)
        {
            var currentModals = GetOpenModals();
            var result = new ModalProbeResult { ModalNestingCount = ModalNestingCount };
            // A new modal that is not a progress dialog is an interactive stop (a managed modal, or a native
            // dialog). Mirrors the dialog-watch.
            foreach (var modal in currentModals)
            {
                if (startModals.Contains(modal.Hwnd) || !modal.IsInteractiveModal)
                    continue; // open at the start (by window handle), or a progress dialog -- not a new stop
                // The FIRST new interactive modal (in window-enumeration order): the message the dialog-watch throws
                // or the posted-gesture wait records, and the modal whose pre-show count is recorded. Managed and
                // native modals are both tracked (by window handle), so there is no need to prefer a managed one.
                result.NewInteractiveModal = true;
                result.BlockingMessage = modal.DetailedMessage;
                result.NewModal = modal;
                break;
            }
            // "Dismissed" means the top modal open at the start is no longer on screen (gone, not merely disabled
            // by a nested child modal it opened). IsOpen tests visibility, so a still-visible parent whose child
            // disabled it is NOT mistaken for dismissed -- which would otherwise drop the ride-through target to
            // its pre-show count while its opener delegate is still legitimately blocked.
            result.TopModalDismissed = topModal != null && !topModal.IsOpen;
            result.LongWaitPresent = currentModals.Any(m => m.IsBusy);
            return result;
        }

        /// <summary>Runs a void unit of work (see <see cref="Run{T}"/>).</summary>
        public void Run(Action work)
        {
            Run(() => { work(); return (object) null; });
        }

        /// <summary>
        /// Dispatches <paramref name="work"/> (per <see cref="Dispatcher"/>), waits it out through the shared
        /// wait/modal loop, and returns its result (re-throwing its exception, preserving an ArgumentException).
        /// </summary>
        public T Run<T>(Func<T> work)
        {
            if (Synchronous)
            {
                // A synchronous UI-thread read: marshal straight through (Control.Invoke blocks until the read
                // returns), with NO pending-action count and NO modal wait loop -- see Synchronous. Exceptions
                // propagate raw (the InvokeOnUiThread Action overload adds its own wrapping). This is the whole of
                // Run for this config; the count/dispatch/wait machinery below is only for the async configs.
                T syncResult = default(T);
                Send(() => { syncResult = work(); });
                return syncResult;
            }

            int initialCount = ModalNestingCount;
            // The windows open at the start, keyed by window handle -- the identity the wait loop uses to tell a
            // NEW modal from one already open. Snapshotted from ALL top-level windows, not just the currently-active
            // modals (GetOpenModals): a parent modal disabled by a nested child (a prompt on top of a wizard) is
            // EXCLUDED from GetOpenModals while the child is up (it filters on IsWindowEnabled), so keying on that
            // would misread the parent as a NEW modal when the child closes and re-enables it. GetTopLevelWindows
            // keys on visibility, not enabled state, so the disabled parent is present and recognized as already
            // open. Read off the poll thread (Win32 + Control.FromHandle; no marshal).
            var startModals = new HashSet<IntPtr>(NativeDialog.GetTopLevelWindows().Select(form => form.Hwnd));
            // Prune the pre-show tracker against the windows open right now: any tracked modal whose window is not
            // among them has closed. This is the ONLY place the tracker is pruned -- done against the fresh set, so
            // no separate close hook or per-access sweep is needed.
            PruneModalsNotOpen(startModals);
            // The top interactive modal open at the start, as a form abstraction, and the opener's pre-show count --
            // captured now, before it (and its tracker entry) can close, so the ride-through target does not race
            // the close. For an accept/cancel this is exactly the window being dismissed (AwaitedDismissal); for a
            // plain gesture it is whatever modal is on top (only to ride a dismissal it happens to cause). The probe
            // later asks this modal itself whether it is still open. Inert for a value read that opens nothing.
            var topModalAtStart = AwaitedDismissal ?? CurrentTopInteractiveModal();
            // Only a modal actually OPEN at the start is a dismissal candidate. A stale tracker entry -- e.g. the
            // StartPage, recorded when the connector first gestured before the main window came up and since closed
            // -- can still be the LIFO top; treating its !IsOpen as "dismissed" would wrongly lower the target and
            // hang the wait. (The old handle-based check guarded this: a closed form had no window handle, so it was
            // never seen as dismissed.) Drop it, so the dismissal machinery stays inert unless a real top modal is
            // open here.
            if (topModalAtStart != null && !topModalAtStart.IsOpen)
                topModalAtStart = null;
            // The smaller of initialCount or the modal's recorded pre-show count: an accept waits for the count to
            // fall back to this. (Peek returns initialCount when the pre-show count is unknown, so the Min is a no-op
            // then; when known, the pre-show count is at or below initialCount anyway.)
            int topModalPreShowCount = topModalAtStart != null
                ? Math.Min(initialCount, PeekModalPreShowCount(topModalAtStart, initialCount)) : initialCount;

            T result = default(T);
            Exception workError = null;
            // Count the work from BEFORE it is dispatched (so a background thread that has not started yet, or a
            // gesture posted onto the form thread, cannot let the drain check complete prematurely) until its
            // delegate returns -- an action that opens a modal stays counted while its delegate blocks in the modal
            // loop. Capture the worker's exception (on whichever thread it runs) rather than let it escape: the
            // dialog-watch avoids a RunAsync error dialog, and the posted gesture surfaces a not-interactable gate
            // failure to the caller (re-thrown below) instead of leaving it on the form thread.
            IncrementModalNestingCount();
            void RunWork()
            {
                try { result = work(); }
                catch (Exception ex) { workError = ex; }
                finally { DecrementModalNestingCount(); }
            }
            try
            {
                if (WorkOnCallerThread)
                    // The native-dialog gesture: run it right here on the caller's (pipe) thread. Its Win32 posts /
                    // UI Automation must NOT marshal onto the UI thread that owns the dialog's modal loop (that would
                    // deadlock), and the AutomationElement must stay on the thread it was obtained on -- so no
                    // dispatch. RunWork returns at once (it only posts a gesture); the wait loop below does the rest.
                    RunWork();
                else if (Dispatcher == null)
                    // The dialog-watch: run the (possibly blocking) value work on a background thread and watch here.
                    ActionUtil.RunAsync(RunWork, @"JsonTool command");
                else
                    // The posted-gesture wait: post the whole gesture (resolve control, gate, do it) onto the form's
                    // own UI thread; RunWork captures its exception (including a not-interactable gate failure),
                    // which is re-thrown below after the wait, just as the dialog-watch re-throws its worker's. This
                    // first Post also captures the UI thread's context, so the later watchdog Sends never touch the
                    // form (which the gesture may be closing).
                    Post(RunWork);
            }
            catch
            {
                // Dispatch failed, so the work will never run and is not pending after all.
                DecrementModalNestingCount();
                throw;
            }

            int target = initialCount;
            int noProgress = 0;
            // Stop the moment the work has errored: a caller-thread gesture (WorkOnCallerThread) runs synchronously
            // above, so a throw during it (e.g. no accept button) is already visible here -- do not wait for a
            // dismissal that will never come; fall through to re-throw its exception below.
            while (workError == null)
            {
                // Read the modal/count state on this poll thread -- no marshal (FormUtil.OpenForms is thread-safe,
                // and each modal answers about itself).
                var probe = ProbeModals(startModals, topModalAtStart);
                int count = probe.ModalNestingCount;

                if (probe.NewInteractiveModal)
                {
                    if (ThrowOnDialog)
                        // A modal is blocking (an alert/error, or any other dialog including a native one); surface
                        // its message so the caller sees what is in the way instead of hanging.
                        throw new InvalidOperationException(probe.BlockingMessage);
                    // A new interactive modal (managed or native) appeared -- the caller will drive it. Record its
                    // pre-show count (the ONLY place one is recorded), so a later accept of it can wait for the count
                    // to fall back to this level. Note the stop-on-modal outcome (and its message), then stop.
                    if (probe.NewModal != null)
                        RecordModalPreShowCount(probe.NewModal, initialCount);
                    StoppedOnModal = true;
                    BlockingMessage = probe.BlockingMessage;
                    break;
                }

                if (topModalAtStart != null && target == initialCount && probe.TopModalDismissed)
                {
                    // The top modal open at the start was dismissed; drop the target to the level its opener left,
                    // so the work the dismissal resumes (which may show a LongWaitDlg) is waited out.
                    target = topModalPreShowCount;
                }

                // <= (not ==): the count can settle BELOW target and be missed by an exact match. A dismissed modal
                // whose pre-show count was never tracked falls back to an upper-bound target (initialCount, which
                // still counts the modal's blocked opener); after the dismissal that opener completes and the count
                // drops past it. It stays above target while the work is in flight, so once at or below the pre-show
                // level the work -- and any work a dismissal resumed -- is done.
                //
                // AwaitedDismissal (an accept/cancel): also require its window to be actually GONE, not just the
                // count drained. A native accept posts an UNCOUNTED Win32 gesture, so the count returns to
                // initialCount the instant the gesture is posted -- before the window has closed; without this it
                // would complete early. Bypassed for a plain gesture (no AwaitedDismissal) and when the awaited
                // window was already closed at the start (topModalAtStart nulled), which nothing then waits on.
                if (count <= target &&
                    (AwaitedDismissal == null || topModalAtStart == null || probe.TopModalDismissed))
                    break;

                if (TimeOut)
                {
                    // The watchdog needs proof the UI message loop is actually PUMPING (its signal is a slow op that
                    // pumps but shows no progress dialog) -- a pure off-thread read cannot show that. So ping the UI
                    // thread with a minimal synchronous no-op: it returns only if the loop pumped, and parks here
                    // (so noProgress cannot advance) if the loop is blocked, exactly as the old marshaled probe did.
                    Send(() => { });
                    if (probe.LongWaitPresent)
                        noProgress = 0; // a progress dialog is up: the operation is advancing, keep waiting
                    else if (++noProgress >= NO_PROGRESS_LIMIT)
                    {
                        string openForms = string.Join(@", ", FormUtil.OpenForms.Select(f => f.GetType().Name));
                        throw new InvalidOperationException(new LlmInstruction(GestureTimeoutMessage +
                            string.Format(@" [initialCount={0}, target={1}, current={2}, topModalDismissed={3}, openForms=[{4}]]",
                                initialCount, target, count, probe.TopModalDismissed, openForms)));
                    }
                }

                Thread.Sleep(TimeOut ? PROGRESS_POLL_MILLIS : DIALOG_POLL_INTERVAL_MILLIS);
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

        // The message the watchdog throws: about NO_PROGRESS_LIMIT seconds of an actively-pumping message loop
        // with no completion and no progress dialog, which points at a slow Skyline operation not showing one.
        private static string GestureTimeoutMessage =>
            LlmInstruction.Format(
                @"Timed out after about {0} seconds of an active Skyline message loop with no long-wait (progress) dialog showing. A wait this long with no progress dialog usually means a Skyline operation is slow but is not surfacing a LongWaitDlg. If a dialog is open, drive it with skyline_get_open_forms and the accept/cancel/click verbs.",
                NO_PROGRESS_LIMIT.ToString());
    }
}
