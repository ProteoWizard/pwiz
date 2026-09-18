/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using pwiz.Common.Controls;
using pwiz.Skyline.Util.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

                ControlUtil.SafeBeginInvoke(dialog, () =>
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
        /// <para>On this line the dump is taken from a SNAPSHOT of the process, so a thread that
        /// is running comes out with frames like any other. That was not true of the passive
        /// attach used where only the older ClrMD is available, which listed every running thread
        /// - usually including the UI thread - with no frames at all, so an empty stack there
        /// meant "could not read" rather than "idle". Snapshots removed that blind spot.</para>
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
            // never hold the process open.
            var dumpThread = new Thread(() =>
            {
                try
                {
                    var threadDumpLines = new List<string> { @"*** Thread dump:" };
                    threadDumpLines.AddRange(GetAllThreadsCallstacks(Process.GetCurrentProcess().Id));
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

            // Not describing the runtime on this path. Doing so needs another attach, and the
            // attach is what just proved too slow to finish - so asking again would spend the
            // bound a second time, per timeout, which is the cost this bound exists to stop.
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
        /// What ClrMD can see of this machine's runtime, so a failed dump says which machine it
        /// failed on rather than leaving the next reader to guess.
        /// <para>Metadata only - deliberately does NOT create the runtime, which is the expensive
        /// half and the half that was already failing when this is reached.</para>
        /// </summary>
        private static string DescribeAttachEnvironment()
        {
            try
            {
                using var dataTarget = DataTarget.CreateSnapshotAndAttach(Process.GetCurrentProcess().Id);
                dataTarget.FileLocator = null;   // No symbol server, and no symbols cache left behind
                var clrInfo = dataTarget.ClrVersions.FirstOrDefault();
                if (clrInfo == null)
                    return @"*** No CLR found in this process";

                return string.Format(@"*** CLR {0}", clrInfo.Version);
            }
            catch (Exception ex)
            {
                return string.Format(@"*** Runtime description unavailable: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Where ClrMD writes its symbols cache. Under the TEMP directory, not the working
        /// directory - which matters here because AbstractUnitTest redirects TEMP per test, so
        /// the cache lands in that test's own temp folder and is reported as a file it left
        /// behind.
        /// </summary>
        private static string SymbolsCacheDir
        {
            get { return Path.Combine(Path.GetTempPath(), @"symbols"); }
        }

        /// <summary>
        /// Removes the symbols cache a snapshot wrote, unless it was already there.
        /// <para>Retried, because the snapshot can still hold the directory for a moment after the
        /// DataTarget is disposed, and a first delete then throws. Left behind, it is reported as a
        /// temp file the test failed to clean up.</para>
        /// </summary>
        private static void DeleteSymbolsCache(bool existedBefore)
        {
            if (existedBefore)
                return;   // Somebody else owns it

            for (int i = 0; i < SYMBOLS_DELETE_ATTEMPTS; i++)
            {
                try
                {
                    if (!Directory.Exists(SymbolsCacheDir))
                        return;
                    Directory.Delete(SymbolsCacheDir, true);
                    return;
                }
                catch (Exception)
                {
                    // Cleaning up after a diagnostic must never become the failure it reports
                    Thread.Sleep(SYMBOLS_DELETE_RETRY_MILLIS);
                }
            }
        }

        private const int SYMBOLS_DELETE_ATTEMPTS = 10;
        private const int SYMBOLS_DELETE_RETRY_MILLIS = 100;

        public static IEnumerable<string> GetAllThreadsCallstacks(int processId)
        {
            // A SNAPSHOT, not a passive attach. This line has ClrMD 3.x, where the snapshot both
            // avoids the DAC resolution that made a passive attach cost 745-1035 seconds on the
            // TeamCity agents, and can walk a thread that is RUNNING - which the passive attach on
            // the older ClrMD could not, and which was the known blind spot documented above.
            // Taking a snapshot writes a symbols cache into the working directory, before there is
            // any object to configure - so it cannot be prevented, only cleaned up. Left alone it
            // is reported as a temp file the test failed to remove.
            var symbolsExisted = Directory.Exists(SymbolsCacheDir);
            try
            {
                using var dataTarget = DataTarget.CreateSnapshotAndAttach(processId);
                dataTarget.FileLocator = null;   // Nothing here needs symbols
                // Disposed, not just left to the data target. ClrMD 3.x makes ClrRuntime
                // IDisposable and it owns the DAC library loaded to answer these questions;
                // dropping it on the floor holds that per call, which is a leak the pass-1
                // check reports against whatever test happened to ask for a dump.
                using var runtime = dataTarget.ClrVersions[0].CreateRuntime();

                foreach (var thread in runtime.Threads)
                {
                    if (!thread.IsAlive) continue;

                    yield return $"Thread {thread.OSThreadId:X} (Managed ID: {thread.ManagedThreadId})";

                    foreach (var frame in thread.EnumerateStackTrace())
                    {
                        yield return $"  {frame.Method?.Type?.Name}.{frame.Method?.Name ?? "[Unknown]"}";
                    }

                    yield return string.Empty;
                }
            }
            finally
            {
                DeleteSymbolsCache(symbolsExisted);
            }
        }
    }
}
