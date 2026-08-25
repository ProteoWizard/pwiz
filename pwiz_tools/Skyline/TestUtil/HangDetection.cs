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
                Console.Out.WriteLine(TextUtil.LineSeparate(@"*** Hang detected.", TryGetThreadDump()));

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
        public static string TryGetThreadDump()
        {
            string threadDump = null;
            string failureReason = null;

            // Bounded on a background thread, because ClrMD offers no way to cancel an attach that
            // is going badly. Abandoning the thread leaves the rest of that work running, which is
            // the cheaper mistake - it is a background thread, so it can never hold up process
            // exit, and the caller gets an answer in seconds either way.
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

            if (!dumpThread.Join(THREAD_DUMP_TIMEOUT_MILLIS))
                return GetCallingThreadStack(string.Format(@"gave up after {0} ms", THREAD_DUMP_TIMEOUT_MILLIS));

            return threadDump ?? GetCallingThreadStack(failureReason);
        }

        /// <summary>
        /// The calling thread's own stack, for a failure whose process-wide dump could not be
        /// taken. Reading your own stack needs no attach and no debugging support on the machine,
        /// so this is what survives where <see cref="GetAllThreadsCallstacks"/> does not.
        /// </summary>
        /// <param name="reason">Why the full dump was unavailable, reported above the stack so a
        /// degraded diagnostic is never mistaken for the real one.</param>
        public static string GetCallingThreadStack(string reason)
        {
            var stackLines = new List<string>
            {
                string.Format(@"*** Thread dump unavailable: {0}", reason),
                DescribeAttachEnvironment(),
                @"*** Calling thread stack:",
                new StackTrace(1, true).ToString().TrimEnd(),
                @"*** End of calling thread stack"
            };
            return TextUtil.LineSeparate(stackLines);
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

        public static IEnumerable<string> GetAllThreadsCallstacks(int processId)
        {
            using var dataTarget = DataTarget.AttachToProcess(processId, 5000, AttachFlag.Passive);
            var clrInfo = dataTarget.ClrVersions[0];

            // Refuse before the expensive half rather than after. With no DAC on this machine
            // matching this CLR, ClrMD fetches one from a symbol server, which is slow where that
            // server is unreachable and reads garbage where the version does not match - measured
            // as 745-1035 seconds ending in "Array dimensions exceeded supported range" on the
            // TeamCity agents. Naming the missing file costs milliseconds and tells whoever
            // provisions the machine exactly what to install.
            var localMatchingDac = clrInfo.LocalMatchingDac;
            if (localMatchingDac == null)
                throw new InvalidOperationException(string.Format(
                    @"No local DAC matching CLR {0} - {1} would have to come from a symbol server",
                    clrInfo.Version, clrInfo.DacInfo.FileName));

            // Explicitly from the local file, so the symbol server can never become the fallback.
            var runtime = clrInfo.CreateRuntime(localMatchingDac);

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
    }
}
