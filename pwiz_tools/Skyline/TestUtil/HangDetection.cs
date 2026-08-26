/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using Microsoft.Diagnostics.Runtime;
using pwiz.Skyline.Util.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;

namespace pwiz.SkylineTestUtil
{
    public class HangDetection : IDisposable
    {
        /// <summary>
        /// How long the full thread dump gets before the caller settles for the degraded form. A
        /// machine that can take one does it in milliseconds, so this is not a budget - it is a
        /// bound on what a machine that CANNOT is allowed to cost. Measured 2026-08-24: the
        /// TeamCity agents spend 745-1035 seconds before failing, which is not a price a timeout
        /// diagnostic - or the test that covers it - may charge every run.
        /// </summary>
        private const int THREAD_DUMP_TIMEOUT_MILLIS = 5 * 1000;

        /// <summary>
        /// How long a dump waits for a walk already in progress. Never blocks outright: ClrMD is
        /// not thread-safe, so a second walker has to wait, but <see cref="TryGetThreadDump"/>
        /// ABANDONS a dump thread that overruns its bound and an abandoned walker never releases
        /// this lock. Waiting forever would let ONE overrun silently kill the thread dump for the
        /// rest of the process, parking a thread per later call. Giving up says so instead.
        /// </summary>
        private const int ATTACH_LOCK_TIMEOUT_MILLIS = 2 * 1000;

        /// <summary>
        /// A bound on one thread's frames. ClrMD's own documentation for
        /// <see cref="ClrThread.EnumerateStackTrace"/> says it "may loop infinitely in the case of
        /// stack corruption or other stack unwind issues which can happen in practice" and tells
        /// callers to "set a maximum loop count" - and a hung process, which is when this runs, is
        /// exactly where that is likeliest. Far above any real stack, so a truncated thread means
        /// the unwind went wrong, and the dump says so rather than never returning.
        /// </summary>
        private const int MAX_FRAMES_PER_THREAD = 512;

        /// <summary>
        /// Marks a line under a thread that is a note ABOUT the walk rather than a frame from it.
        /// Indented like a frame because it belongs to the thread above it, but distinguishable,
        /// because anything counting frames would otherwise count the notes as frames - which is
        /// how a dump that named no frames at all could still look healthy.
        /// </summary>
        public const string THREAD_NOTE_PREFIX = @"  -- ";

        /// <summary>
        /// Serializes everything that touches <see cref="_dataTarget"/> and <see cref="_runtime"/>,
        /// which ClrMD requires because it is not thread-safe. Taken only with
        /// <see cref="ATTACH_LOCK_TIMEOUT_MILLIS"/>, never unconditionally - see that field.
        /// </summary>
        private static readonly object _attachLock = new object();

        /// <summary>
        /// The attach to this process, and the runtime read through it, kept between dumps - see
        /// <see cref="GetSelfRuntime"/> for why re-attaching is not an option. Written ONLY under
        /// <see cref="_attachLock"/>, and always as a pair: either both are set and usable, or
        /// both are null and the next dump rebuilds them.
        /// </summary>
        private static DataTarget _dataTarget;
        private static ClrRuntime _runtime;

        private readonly object _lock = new object();
        private bool _disposed;
        private TimeSpan? _waitDuration;
        private Thread _callerThread;
        private readonly Thread _watchdogThread;

        public HangDetection()
        {
            _watchdogThread = new Thread(WatchdogLoop)
            {
                IsBackground = true,
                Name = nameof(HangDetection)
            };
            _watchdogThread.Start();
        }

        /// <summary>
        /// If action takes more than 30 minutes to complete, interrupt this thread.
        /// </summary>
        public static void InterruptWhenHung(Action action)
        {
            using var hangDetection = new HangDetection();
            hangDetection.InterruptAfter(action, TimeSpan.FromMinutes(30));
        }

        /// <summary>
        /// If action takes longer than <paramref name="duration"/> then
        /// interrupt this thread and dump callstacks to the console.
        /// </summary>
        public void InterruptAfter(Action action, TimeSpan duration)
        {
            lock (_lock)
            {
                _waitDuration = duration;
                _callerThread = Thread.CurrentThread;
                Monitor.Pulse(_lock);
            }

            try
            {
                action();
            }
            catch (ThreadInterruptedException)
            {
                try
                {
                    Console.Out.WriteLine(TextUtil.LineSeparate(@"*** Hang detected.", TryGetThreadDump()));
                }
                catch (Exception ex)
                {
                    // TryGetThreadDump swallows its own failures, but it still has to START a thread
                    // to bound them - and this runs on a process that is already hung, which is
                    // exactly where that fails. Letting it throw here would replace the hang being
                    // reported with an unrelated exception.
                    Console.Out.WriteLine(@"Unable to get thread dump: {0}", ex);
                }

                try
                {
                    foreach (var form in FormUtil.OpenForms)
                    {
                        Console.Out.WriteLine("Open Form: {0}", AbstractFunctionalTest.GetTextForForm(form));
                    }
                }
                catch (Exception ex)
                {
                    Console.Out.WriteLine("Unable to get open forms string: {0}", ex);
                }
                throw;
            }
            finally
            {
                lock (_lock)
                {
                    _waitDuration = null;
                    Monitor.Pulse(_lock);
                }
            }
        }

        private void WatchdogLoop()
        {
            lock (_lock)
            {
                while (!_disposed)
                {
                    if (!_waitDuration.HasValue)
                    {
                        Monitor.Wait(_lock);
                        continue;
                    }

                    var duration = _waitDuration.Value;
                    var stopWatch = Stopwatch.StartNew();
                    TimeSpan cycleDuration = TimeSpan.FromTicks(100);
                    long minCycleCount = duration.Ticks / cycleDuration.Ticks;

                    // While blocked waiting for the action to complete, also watch for a stray
                    // ThreadExceptionDialog. If WinForms catches an exception inside a reentrant
                    // WndProc (e.g. EventWaitHandle.Set on a disposed SafeWaitHandle during
                    // teardown) it can bypass our Application.ThreadException handler and pop the
                    // default dialog. The UI thread is then wedged in the dialog's nested message
                    // loop, so the caller's Invoke never returns and this wait would otherwise
                    // time out only at the full duration. Dismissing the dialog releases the UI
                    // thread; recording the exception ensures the test fails loudly so the
                    // underlying bug is investigated rather than masked.
                    var handledDialogs = new HashSet<ThreadExceptionDialog>();

                    for (long cycleIndex = 0; ; cycleIndex++)
                    {
                        if (!_waitDuration.HasValue || _disposed)
                        {
                            break;
                        }

                        if (cycleIndex > minCycleCount && stopWatch.Elapsed > duration)
                        {
                            _callerThread.Interrupt();
                            break;
                        }

                        try
                        {
                            DismissThreadExceptionDialogs(handledDialogs);
                        }
                        catch (Exception ex)
                        {
                            Console.Out.WriteLine(
                                @"HangDetection: error checking for ThreadExceptionDialog: {0}", ex);
                        }

                        Monitor.Wait(_lock, cycleDuration);
                    }
                }
            }
        }

        private static void DismissThreadExceptionDialogs(HashSet<ThreadExceptionDialog> handled)
        {
            var dialogs = FormUtil.OpenForms.OfType<ThreadExceptionDialog>().ToList();
            foreach (var dialog in dialogs)
            {
                if (dialog.IsDisposed || !dialog.IsHandleCreated)
                {
                    continue;
                }

                // Track by reference: prevents redundant log entries and queued BeginInvoke
                // callbacks if the UI thread is slow to process the dismissal. New dialogs
                // that appear later are still handled.
                if (!handled.Add(dialog))
                {
                    continue;
                }

                Console.Out.WriteLine(
                    @"*** ThreadExceptionDialog detected during InterruptAfter wait - dismissing");
                Program.AddTestException(new InvalidOperationException(
                    string.Format(@"ThreadExceptionDialog appeared while waiting for UI action: {0}",
                        TryGetDialogText(dialog))));

                CommonActionUtil.SafeBeginInvoke(dialog, () =>
                {
                    try
                    {
                        // Setting DialogResult posts WM_CLOSE asynchronously (PostMessage),
                        // avoiding the synchronous Form.Close -> WmClose path that triggered
                        // the original SafeWaitHandle race during teardown.
                        if (!dialog.IsDisposed)
                        {
                            dialog.DialogResult = DialogResult.Cancel;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Out.WriteLine(@"Failed to dismiss ThreadExceptionDialog: {0}", ex);
                    }
                });
            }
        }

        private static string TryGetDialogText(Form dialog)
        {
            // Best-effort diagnostic: this runs on a background thread, so any property access
            // can throw if the dialog is disposed mid-call. Wrap so a throw never breaks the
            // watchdog loop.
            try
            {
                var textBox = dialog.Controls.OfType<TextBox>().FirstOrDefault(tb => tb.Multiline);
                if (textBox != null && !string.IsNullOrEmpty(textBox.Text))
                {
                    return textBox.Text;
                }
                return dialog.Text ?? @"<no text>";
            }
            catch
            {
                return @"<unavailable>";
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _disposed = true;
                Monitor.Pulse(_lock);
            }

            _watchdogThread.Join();
        }

        /// <summary>
        /// Every managed thread's call stack right now, for a failure that means something never
        /// finished. A wait that times out can say what it was waiting for but not what was - or
        /// was not - working on it, and that is usually the only fact worth having.
        /// <para>KNOWN BLIND SPOT, measured rather than assumed: this attaches to its own process
        /// passively, which cannot walk a stack that is in motion. Blocked and sleeping threads
        /// come out complete, but the thread calling this - and any other thread running at that
        /// instant - is listed with NO frames at all. An empty stack here therefore means "could
        /// not read", NOT "idle", and the UI thread is usually one of the empty ones. When the
        /// question is specifically what a RUNNING thread was doing, this cannot answer it, and
        /// the fix is to attach from a separate process rather than from this one.</para>
        /// <para>Never throws, and never comes back with nothing. Where the process-wide dump
        /// cannot be taken it degrades to the calling thread's own stack, which is one thread
        /// instead of all of them but is the thread that gave up waiting, and always reads. A
        /// diagnostic that replaces the failure it exists to explain is worse than no diagnostic;
        /// one that reports only that it is unavailable is barely better.</para>
        /// </summary>
        /// <param name="timeoutMillis">How long to allow, for a caller whose question is worth more
        /// than the default bound. JsonToolServerTest asks what is holding a modal dialog on a loaded
        /// agent, where the walk legitimately takes longer than a wait-timeout diagnostic should.</param>
        public static string TryGetThreadDump(int timeoutMillis = THREAD_DUMP_TIMEOUT_MILLIS)
        {
            string threadDump = null;
            string failureReason = null;

            // Reading the call stacks attaches ClrMD to this very process, which can block on
            // locating the DAC or on walking a live runtime, and ClrMD offers no way to cancel an
            // attach that is going badly. Left unbounded it could turn a reported failure into a
            // wedged test run, which costs a whole nightly pass - so it gets its own background
            // thread and a deadline. Abandoning that thread leaves the rest of the work running,
            // which is the cheaper mistake: the thread is a background one, so a wedged attach can
            // never hold the process open. It does hold the shared runtime, though, which is why
            // every later dump waits only ATTACH_LOCK_TIMEOUT_MILLIS for it and then reports that
            // rather than queueing up behind it.
            var dumpThread = new Thread(() =>
            {
                try
                {
                    var threadDumpLines = new List<string> { @"*** Thread dump:" };
                    threadDumpLines.AddRange(GetAllThreadsCallstacks());
                    threadDumpLines.Add(@"*** End of thread dump");
                    threadDump = TextUtil.LineSeparate(threadDumpLines);
                }
                catch (Exception ex)
                {
                    failureReason = ex.Message;
                }
            })
            {
                IsBackground = true,
                Name = nameof(TryGetThreadDump)
            };
            dumpThread.Start();

            // Not describing the runtime on this path. Doing so needs the attach, and the attach
            // is what just proved too slow to finish - the abandoned thread still holds it - so
            // asking would spend the bound a second time, per timeout, which is the cost this
            // bound exists to stop.
            if (!dumpThread.Join(timeoutMillis))
            {
                return TextUtil.LineSeparate(
                    string.Format(@"*** Thread dump unavailable: gave up after {0} ms", timeoutMillis),
                    GetCallingThreadStack());
            }

            if (threadDump != null)
                return threadDump;

            // A dump that failed FAST can afford the metadata read: whatever went wrong, attaching
            // itself returned, so describing the CLR and DAC is what says why to whoever reads the log.
            return TextUtil.LineSeparate(
                string.Format(@"*** Thread dump unavailable: {0}", failureReason),
                DescribeAttachEnvironment(),
                GetCallingThreadStack());
        }

        /// <summary>
        /// The calling thread's own stack, for a failure whose process-wide dump could not be
        /// taken. Reading your own stack needs no attach and no debugging support on the machine,
        /// so this is what survives where <see cref="GetAllThreadsCallstacks"/> does not.
        /// <para>Only the stack: why the caller wants it, and anything else worth saying about the
        /// machine, belong to whoever is composing the report.</para>
        /// </summary>
        public static string GetCallingThreadStack()
        {
            return TextUtil.LineSeparate(
                @"*** Calling thread stack:",
                new StackTrace(1, true).ToString().TrimEnd(),
                @"*** End of calling thread stack");
        }

        /// <summary>
        /// What ClrMD can see of this machine's runtime. Whether the attach can work at all is
        /// decided by the CLR build and the matching DAC, so a failed dump carries both out to the
        /// log rather than leaving the next reader to guess which machines differ and how. A DAC
        /// that does not resolve locally is fetched from a symbol server instead, which is slow
        /// where that server is unreachable and yields misreads where the version does not match.
        /// <para>Metadata only - deliberately does NOT call CreateRuntime, which is the expensive
        /// half and the half that was already failing when this is reached.</para>
        /// </summary>
        private static string DescribeAttachEnvironment()
        {
            try
            {
                // Its own attach, disposed, deliberately NOT the shared one. Three reasons, all of
                // which the shared runtime gets wrong here. It answers from the first attach, so on
                // an agent whose DAC was installed mid-run every later report would repeat the
                // original "local matching DAC: none" - the exact question this exists to answer,
                // frozen at the wrong moment. It would need the lock, on the CALLER's thread, which
                // is the one path with no Join around it to bound the wait. And without
                // CreateRuntime no DAC is ever loaded, so there is no unreleasable COM reference
                // and Dispose really does release everything, including the process handle.
                using var dataTarget = DataTarget.AttachToProcess(Process.GetCurrentProcess().Id, 5000, AttachFlag.Passive);
                var clrInfo = dataTarget.ClrVersions.FirstOrDefault();
                if (clrInfo == null)
                    return @"*** No CLR found in this process";

                return string.Format(@"*** CLR {0}, DAC {1}, local matching DAC: {2}",
                    clrInfo.Version, clrInfo.DacInfo.FileName,
                    clrInfo.LocalMatchingDac ?? @"none - a symbol server would have to supply it");
            }
            catch (Exception ex)
            {
                return string.Format(@"*** Runtime description unavailable: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Every managed thread in THIS process, one header line per thread followed by its frames.
        /// <para>Materialized rather than lazy: the walk holds <see cref="_attachLock"/>, and a
        /// deferred iterator would hold it for however long the caller took to enumerate.</para>
        /// </summary>
        public static IEnumerable<string> GetAllThreadsCallstacks()
        {
            if (!Monitor.TryEnter(_attachLock, ATTACH_LOCK_TIMEOUT_MILLIS))
            {
                // Named, not silent. Waiting forever behind an abandoned walker is how one overrun
                // would take the diagnostic down for the rest of the process; reporting a timeout
                // with no cause is how the reader gets sent after a DAC that is fine.
                throw new InvalidOperationException(string.Format(
                    @"Another thread dump is still running after {0} ms and holds the shared runtime",
                    ATTACH_LOCK_TIMEOUT_MILLIS));
            }

            try
            {
                var lines = new List<string>();
                foreach (var thread in GetSelfRuntime().Threads)
                {
                    if (!thread.IsAlive)
                    {
                        continue;
                    }

                    lines.Add(string.Format(@"Thread {0:X} (Managed ID: {1})",
                        thread.OSThreadId, thread.ManagedThreadId));

                    // Listed but not walked, rather than dropped. The DAC cannot read the stack of
                    // the thread asking - it is running, the blind spot documented on
                    // TryGetThreadDump - and asking does not merely come back empty, it can never
                    // come back at all: measured, a background walker asking for its own stack
                    // hangs indefinitely. Since TryGetThreadDump ABANDONS this thread at its
                    // deadline, that hang would strand it holding the runtime for the life of the
                    // process. Saying so keeps "every managed thread" true and keeps an empty stack
                    // meaning what the class doc says it means - could not read, not idle.
                    if (thread.ManagedThreadId == Thread.CurrentThread.ManagedThreadId)
                    {
                        lines.Add(THREAD_NOTE_PREFIX + @"thread taking this dump - its own stack cannot be read");
                        lines.Add(string.Empty);
                        continue;
                    }

                    lines.AddRange(GetFrames(thread));
                    lines.Add(string.Empty);
                }
                return lines;
            }
            finally
            {
                Monitor.Exit(_attachLock);
            }
        }

        /// <summary>
        /// One thread's frames, capped at <see cref="MAX_FRAMES_PER_THREAD"/> because ClrMD
        /// documents this enumeration as able to loop forever on a stack it cannot unwind.
        /// </summary>
        private static IEnumerable<string> GetFrames(ClrThread thread)
        {
            var frames = new List<string>();
            foreach (var frame in thread.EnumerateStackTrace())
            {
                if (frames.Count >= MAX_FRAMES_PER_THREAD)
                {
                    frames.Add(string.Format(@"{0}stopped after {1} frames - the stack unwind is not terminating",
                        THREAD_NOTE_PREFIX, MAX_FRAMES_PER_THREAD));
                    break;
                }

                frames.Add(@"  " + GetFrameName(frame));
            }
            return frames;
        }

        /// <summary>
        /// One frame as "Type.Method", degrading to whichever half ClrMD could resolve. Built
        /// rather than formatted because a null type used to render as nothing at all, leaving a
        /// bare leading dot - ".[Unknown]" - which reads as a parse error rather than as the
        /// unresolved frame it is.
        /// </summary>
        private static string GetFrameName(ClrStackFrame frame)
        {
            var methodName = frame.Method?.Name;
            if (methodName == null)
            {
                return @"[Unknown]";
            }

            var typeName = frame.Method?.Type?.Name;
            return typeName == null ? methodName : typeName + @"." + methodName;
        }

        /// <summary>
        /// The runtime to read stacks out of, attached once and then reused.
        /// <para>Reused because ONCE THE DAC IS LOADED the attach can no longer be released. In
        /// this ClrMD (0.8.31, the checked-in prerelease) CreateRuntime hands the DAC a COM
        /// reference back to the data target that nothing releases, leaving the graph rooted by a
        /// ref-counted handle: dotMemory shows DacDataTarget -> DataTargetImpl -> ClrInfo[] ->
        /// ModuleInfo[]. Measured on a fresh attach per call, in a bare console harness, 9 KB
        /// managed and 3.2 MB private leaked EVERY time; the same leak inside a Debug TestRunner
        /// costs 15.5 KB managed and 7.6 MB per run, the process being bigger. Releasing the DAC's
        /// COM objects by hand was tried and changes neither number.</para>
        /// <para>Disposing is NOT a no-op, which is why the attach above is kept local until the
        /// runtime exists: DataTargetImpl.Dispose closes its reader, and for a passive attach that
        /// reader holds an OpenProcess handle - measured, ten undisposed attaches cost ten handles
        /// and disposing them returns every one. So an attach that never reaches CreateRuntime is
        /// fully releasable and gets released; only one that did is kept, because by then keeping
        /// it is the only thing the leak can be traded for.</para>
        /// <para><see cref="ClrRuntime.Flush"/> is what makes reuse correct rather than merely
        /// cheap: without it a live-process runtime answers from its snapshot. With it, this
        /// returns exactly what re-attaching returns - verified against a fresh attach per call
        /// while threads were being added between reads, identical thread and frame counts.</para>
        /// </summary>
        private static ClrRuntime GetSelfRuntime()
        {
            if (_runtime != null)
            {
                try
                {
                    _runtime.Flush();
                    return _runtime;
                }
                catch (Exception)
                {
                    // Drop the pair so the next dump rebuilds it. Flush calls into the DAC of a
                    // process that is already unwell, and it discards its caches only AFTER that
                    // call returns - so a throw leaves the runtime holding stale data that every
                    // later Flush would throw on identically. Keeping it would trade a leak for a
                    // diagnostic permanently stuck on "unavailable"; re-attaching costs one attach.
                    DiscardAttach();
                    throw;
                }
            }

            // Deliberately local until the runtime exists. Where this machine has no matching DAC
            // the guard below throws and CreateRuntime is never reached, so no DAC is ever loaded,
            // nothing holds an unreleasable COM reference to this target, and disposing it really
            // does give the process handle back. Caching it before the guard would leave the
            // agents this code was written for holding a permanent attach that can never produce
            // a dump - a leak introduced by the leak fix.
            DataTarget dataTarget = null;
            try
            {
                using (var process = Process.GetCurrentProcess())
                {
                    dataTarget = DataTarget.AttachToProcess(process.Id, 5000, AttachFlag.Passive);
                }

                // FirstOrDefault, not [0]: an attach that succeeds where ClrMD recognizes no CLR
                // would otherwise surface "Index was outside the bounds of the array" as the
                // reason the dump failed, which tells the reader nothing about the DAC.
                var clrInfo = dataTarget.ClrVersions.FirstOrDefault();
                if (clrInfo == null)
                {
                    throw new InvalidOperationException(@"No CLR found in this process");
                }

                // Refuse before the expensive half rather than after. With no DAC on this machine
                // matching this CLR, ClrMD fetches one from a symbol server, which is slow where
                // that server is unreachable and reads garbage where the version does not match -
                // measured as 745-1035 seconds ending in "Array dimensions exceeded supported
                // range" on the TeamCity agents. Naming the missing file costs milliseconds and
                // tells whoever provisions the machine exactly what to install.
                var localMatchingDac = clrInfo.LocalMatchingDac;
                if (localMatchingDac == null)
                {
                    throw new InvalidOperationException(string.Format(
                        @"No local DAC matching CLR {0} - {1} would have to come from a symbol server",
                        clrInfo.Version, clrInfo.DacInfo.FileName));
                }

                // Explicitly from the local file, so the symbol server can never become the
                // fallback. Publishing the pair only now is what makes the leak one-time: past
                // this point the DAC is loaded and the attach can no longer be released, so it
                // has to be worth keeping.
                _runtime = clrInfo.CreateRuntime(localMatchingDac);
                _dataTarget = dataTarget;
                dataTarget = null;
                return _runtime;
            }
            finally
            {
                // Still local means it never became the shared attach, so nothing was kept and
                // this is the disposal that hands the process handle back.
                dataTarget?.Dispose();
            }
        }

        /// <summary>
        /// Forgets the shared attach, as a pair. The DAC that was loaded through it cannot be
        /// unloaded - that is the leak this class works around, not one it can undo - so this
        /// gives up the memory it was reusing in exchange for a diagnostic that still works.
        /// </summary>
        private static void DiscardAttach()
        {
            _runtime = null;
            _dataTarget?.Dispose();
            _dataTarget = null;
        }
    }
}
