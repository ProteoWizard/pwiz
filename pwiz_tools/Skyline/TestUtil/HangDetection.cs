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
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using TestRunnerLib;

namespace pwiz.SkylineTestUtil
{
    public class HangDetection : IDisposable
    {
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
        /// question is what the UI thread was doing, take a minidump as well; see
        /// <see cref="TryWriteMiniDump"/>.</para>
        /// <para>Never throws. A diagnostic that replaces the failure it exists to explain is worse
        /// than no diagnostic, so a dump that cannot be taken reports only that.</para>
        /// </summary>
        public static string TryGetThreadDump()
        {
            try
            {
                var threadDumpLines = new List<string> { @"*** Thread dump:" };
                threadDumpLines.AddRange(GetAllThreadsCallstacks(Process.GetCurrentProcess().Id));
                threadDumpLines.Add(@"*** End of thread dump");
                return TextUtil.LineSeparate(threadDumpLines);
            }
            catch (Exception ex)
            {
                return string.Format(@"*** Thread dump unavailable: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Environment variable that turns on a minidump beside every wait timeout. Off by default
        /// because the dumps are large; turn it on for a long soak that is hunting a stall.
        /// </summary>
        public const string ENV_DUMP_ON_TIMEOUT = "SKYLINE_DUMP_ON_WAIT_TIMEOUT";

        /// <summary>
        /// Most minidumps one process will write. A dump of a Skyline test process runs to a few
        /// hundred MB, and a long soak can time out many times, so an uncapped switch fills the
        /// disk and takes the run down with it. The first few carry the same information.
        /// </summary>
        private const int MAX_MINI_DUMPS = 3;

        private static int _miniDumpsWritten;

        /// <summary>
        /// Writes a minidump of this process when <see cref="ENV_DUMP_ON_TIMEOUT"/> is set, and
        /// returns a line naming it. Unlike <see cref="TryGetThreadDump"/> a minidump captures
        /// every thread faithfully, including the ones that were running, so it can answer what
        /// the UI thread was doing when nothing finished.
        /// <para>Never throws, and returns an empty string when disabled so it costs a caller
        /// nothing to ask.</para>
        /// </summary>
        public static string TryWriteMiniDump(string reason)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ENV_DUMP_ON_TIMEOUT)))
                return string.Empty;
            if (Interlocked.Increment(ref _miniDumpsWritten) > MAX_MINI_DUMPS)
                return TextUtil.LineSeparate(string.Empty,
                    string.Format(@"*** Minidump skipped: already wrote {0} in this process", MAX_MINI_DUMPS));

            try
            {
                var process = Process.GetCurrentProcess();
                var path = Path.Combine(Path.GetDirectoryName(process.MainModule?.FileName) ?? Path.GetTempPath(),
                    string.Format(@"{0}-{1}-{2}.dmp", reason, process.Id, DateTime.UtcNow.ToString(@"HHmmss")));
                return MiniDump.WriteMiniDump(path)
                    ? TextUtil.LineSeparate(string.Empty, string.Format(@"*** Minidump written to {0}", path))
                    : TextUtil.LineSeparate(string.Empty, string.Format(@"*** Minidump could not be written to {0}", path));
            }
            catch (Exception ex)
            {
                return TextUtil.LineSeparate(string.Empty, string.Format(@"*** Minidump unavailable: {0}", ex.Message));
            }
        }

        public static IEnumerable<string> GetAllThreadsCallstacks(int processId)
        {
            using var dataTarget = DataTarget.AttachToProcess(processId, 5000, AttachFlag.Passive);
            var runtime = dataTarget.ClrVersions[0].CreateRuntime();

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
