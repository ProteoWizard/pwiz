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
using SkylineTool;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Runs one unit of connector work on a window's own UI thread and watches, from the caller (pipe) thread, for
    /// it to finish or for a modal dialog to appear. Three entry points, all keyed on a window handle:
    /// <list type="bullet">
    /// <item><see cref="PerformGesture"/> -- post an action (a click, a set-value) and return when it finishes or
    /// leaves an interactive modal open.</item>
    /// <item><see cref="OkDialog"/> -- accept/cancel a dialog: post its dismiss gesture, then wait until that
    /// window has closed AND the nesting count has drained back to the level its opener left.</item>
    /// <item><see cref="CallFunction{T}"/> -- run a value-producing read and return its result, throwing if a modal
    /// got in the way.</item>
    /// </list>
    /// All three funnel through <see cref="PerformActionAndWait"/>, which BeginInvokes the action onto the window's
    /// thread (capturing the sync context, the nesting count, and the open windows there, before running it) and
    /// then waits: it stops and records a new interactive modal, rides a busy <c>ILongWaitForm</c>, completes when
    /// the action has finished and the caller's wait condition holds, and trips a message-loop-progress watchdog.
    /// The action is counted in <see cref="ModalNestingCount"/> while it runs, so one that blocks in a modal loop
    /// keeps the count elevated. Every method must be called off the UI thread.
    /// </summary>
    internal static class DialogWatcher
    {
        // The wait loop's poll interval, and how many message-loop pumps with no progress (no LongWaitDlg, no
        // completion) trip the watchdog.
        private const int POLL_MILLIS = 30;
        private const int NO_PROGRESS_LIMIT = 10;

        // ===== Pending-action count =====

        private static int _modalNestingCount;

        /// <summary>The number of posted actions currently executing (including those blocked in a modal loop) --
        /// the connector's live modal-nesting depth. See <see cref="JsonUiService.ModalNestingCount"/>.</summary>
        public static int ModalNestingCount => Volatile.Read(ref _modalNestingCount);

        internal static void IncrementModalNestingCount() => Interlocked.Increment(ref _modalNestingCount);
        internal static void DecrementModalNestingCount() => Interlocked.Decrement(ref _modalNestingCount);

        // ===== The UI-thread window =====

        /// <summary>
        /// The window used to marshal onto the UI thread when a handle resolves to no managed control (a native
        /// dialog) or none is given: the main window, or the StartPage while it is showing. Null only very early in
        /// startup before either exists. No disposed/handle checks: the tool service is shut down before the main
        /// window closes, so it is never asked to marshal through a dead window.
        /// </summary>
        internal static Control UiThreadWindow => (Control) Program.MainWindow ?? Program.StartWindow;

        // ===== The three entry points =====

        /// <summary>Posts <paramref name="action"/> onto <paramref name="hwnd"/>'s UI thread and waits until it
        /// finishes, or until it opens/leaves an interactive modal (Completed = false, with that modal's message).</summary>
        public static ActionResult PerformGesture(IntPtr hwnd, Action action)
        {
            return PerformActionAndWait(hwnd, action, () => true);
        }

        /// <summary>Accepts or cancels the dialog at <paramref name="hwndDlg"/>: posts <paramref name="dismissAction"/>
        /// (a post-only gesture), then waits until that window has closed AND the nesting count has dropped back to
        /// the level recorded when the dialog was shown -- i.e. the action that opened it has returned -- stopping
        /// early on a new modal or the watchdog.</summary>
        public static ActionResult OkDialog(IntPtr hwndDlg, Action dismissAction)
        {
            // The count to wait back to: the level in effect when the dialog first appeared (recorded then), or the
            // current level if it was never tracked (then this reduces to "wait for the window to close").
            int preShowCount = TryGetPreShowActionCount(hwndDlg) ?? ModalNestingCount;
            return PerformActionAndWait(hwndDlg, dismissAction,
                () => !User32.IsWindowVisible(hwndDlg) && ModalNestingCount <= preShowCount);
        }

        /// <summary>Runs <paramref name="function"/> on <paramref name="hwnd"/>'s UI thread and returns its result;
        /// throws if a modal got in the way (the read did not complete).</summary>
        public static T CallFunction<T>(IntPtr hwnd, Func<T> function)
        {
            T result = default(T);
            var actionResult = PerformActionAndWait(hwnd, () => { result = function(); }, () => true);
            if (!actionResult.Completed)
                throw new InvalidOperationException(actionResult.Message);
            return result;
        }

        // ===== The shared wait =====

        /// <summary>
        /// BeginInvokes <paramref name="action"/> onto <paramref name="hwnd"/>'s UI thread and waits it out on the
        /// caller thread. On the UI thread, before running the action, it snapshots the sync context, the nesting
        /// count, and the open top-level windows (and prunes the pre-show tracker to them). The wait then: returns
        /// Completed = false on a new interactive modal (recording its pre-show count); re-throws an exception the
        /// action raised; returns Completed = true once the action has finished and <paramref name="waitCondition"/>
        /// holds; and trips the watchdog after <see cref="NO_PROGRESS_LIMIT"/> message-loop pumps with no LongWaitDlg
        /// and no completion. Must be called off the UI thread.
        /// </summary>
        private static ActionResult PerformActionAndWait(IntPtr hwnd, Action action, Func<bool> waitCondition)
        {
            var control = Control.FromHandle(hwnd) ?? UiThreadWindow;

            WindowsFormsSynchronizationContext syncContext = null;
            HashSet<IntPtr> startWindows = null;
            int startCount = 0;
            Exception actionError = null;
            bool actionDone = false;

            control.BeginInvoke((Action) (() =>
            {
                // On the window's own UI thread: snapshot everything the wait compares against, then run the action
                // (which may block here in a modal loop it opens -- the snapshot is already taken either way). The
                // action is counted while it runs, so a blocked one keeps the nesting count elevated.
                startCount = ModalNestingCount;
                startWindows = new HashSet<IntPtr>(NativeDialog.GetTopLevelWindows().Select(w => w.Hwnd));
                PruneModalsNotOpen(startWindows);
                Volatile.Write(ref syncContext,
                    SynchronizationContext.Current as WindowsFormsSynchronizationContext ?? new WindowsFormsSynchronizationContext());
                IncrementModalNestingCount();
                try { action(); }
                catch (Exception ex) { actionError = ex; }
                finally { DecrementModalNestingCount(); Volatile.Write(ref actionDone, true); }
            }));

            int pumpCount = 0, lastPump = 0, noProgress = 0;
            bool pumpOutstanding = false;
            while (true)
            {
                // Nothing to compare until the posted delegate has taken its snapshot (it does so before running the
                // action, even one that then blocks). Once the sync context is published, watch.
                var ctx = Volatile.Read(ref syncContext);
                if (ctx != null)
                {
                    // A new interactive modal (one not open at the start) means the action opened, or left open, a
                    // dialog: record its pre-show count so a later OkDialog can wait the count back to it, then stop.
                    var newModal = GetOpenModals()
                        .FirstOrDefault(m => m.IsInteractiveModal && !startWindows.Contains(m.Hwnd));
                    if (newModal != null)
                    {
                        RecordModalPreShowCount(newModal, startCount);
                        return new ActionResult { Completed = false, Message = newModal.DetailedMessage };
                    }

                    if (Volatile.Read(ref actionDone))
                    {
                        if (actionError != null)
                            ExceptionUtil.WrapAndThrowException(actionError); // the gesture failed on the UI thread
                        if (waitCondition())
                            return new ActionResult { Completed = true };
                    }

                    // Watchdog: post a no-op and count message-loop pumps. While a pump is outstanding (the loop has
                    // not run it yet) we make no judgement -- the loop may simply be busy. When one comes back with no
                    // LongWaitDlg showing and no completion, that is a pump with no progress; abort after a run of them.
                    if (!pumpOutstanding)
                    {
                        lastPump = Volatile.Read(ref pumpCount);
                        ctx.Post(_ => Interlocked.Increment(ref pumpCount), null);
                        pumpOutstanding = true;
                    }
                    else if (Volatile.Read(ref pumpCount) != lastPump)
                    {
                        pumpOutstanding = false;
                        if (GetOpenModals().Any(m => m.IsBusy))
                            noProgress = 0; // a progress dialog is up: the operation is advancing, keep waiting
                        else if (++noProgress >= NO_PROGRESS_LIMIT)
                            throw new InvalidOperationException(new LlmInstruction(GestureTimeoutMessage));
                    }
                }
                Thread.Sleep(POLL_MILLIS);
            }
        }

        // The message the watchdog throws: about NO_PROGRESS_LIMIT message-loop pumps with no completion and no
        // progress dialog, which points at a slow Skyline operation not showing a LongWaitDlg.
        private static string GestureTimeoutMessage =>
            LlmInstruction.Format(
                @"Timed out after an actively-pumping Skyline message loop made no progress with no long-wait (progress) dialog showing. This usually means a Skyline operation is slow but is not surfacing a LongWaitDlg. If a dialog is open, drive it with skyline_get_open_forms and the accept/cancel/click verbs.");

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
        // existed just before the modal was shown. Recorded ONLY in the primary path -- when a wait detects that the
        // gesture it posted opened a new interactive modal, it records that modal here. Keyed by WINDOW HANDLE, so a
        // native dialog is tracked exactly like a managed form (both expose Hwnd). The key is a value, so a stale
        // entry never keeps a closed dialog -- and through it the SkylineWindow / document -- alive. There is no
        // close hook; entries are pruned against the open windows when a wait snapshots them. Guarded by its own lock.
        private static readonly List<KeyValuePair<IntPtr, int>> _modalPreShowCounts = new List<KeyValuePair<IntPtr, int>>();

        // Drops entries whose window is no longer among the currently-open top-level windows (a closed dialog).
        // Called once per wait, right after that window set is captured -- there is no other time the tracker needs
        // pruning, and doing it against the fresh set avoids a stale-handle race with handle reuse.
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
    }
}
