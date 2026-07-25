/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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

// Does creating and destroying Win32 windows leak native heap on THIS machine?
//
// Nightly reports heap leaks on the native-dialog / connector test family, but only on some
// machines -- RITACH-DSK and KAIPOT-PC1 ran the same commit with zero leaks while every other
// agent reported 2-6. The leading explanation is that window create/destroy leaks native heap
// in a Terminal Services (remoted display) session, and that the leaking tests are simply the
// ones that create an unusual number of windows per run:
//
//   TestMcpConnectorBackgroundDialog   ~100,000 short-lived grid editing controls (before the fix)
//   TestNativeMessageBox               3 native Save dialogs + 2 message boxes
//   TestNativeFileDialog / TestPrmMcpConnector    1 native file dialog each
//   every other *McpConnector* test    almost none -- and all are dead flat in nightly
//
// This probe settles that with no Skyline code involved. Run it on a machine that reports the
// leak and on one that does not, and compare bytes/iteration.
//
//   csc.exe /platform:x64 /target:exe /out:WindowChurnProbe.exe /r:System.Windows.Forms.dll ^
//           /r:System.Drawing.dll WindowChurnProbe.cs
//
//   WindowChurnProbe.exe child  20000     // child windows, as a grid editing control is
//   WindowChurnProbe.exe form    2000     // top-level forms
//   WindowChurnProbe.exe dialog   500     // modal dialogs
//   WindowChurnProbe.exe idle   20000     // control: no windows at all
//
// Committed heap is measured exactly the way TestRunner does it (GetProcessHeaps + HeapWalk,
// summing BUSY blocks -- see TestRunnerLib/RunTests.cs MemoryManagement.GetProcessHeapSizes), so
// the numbers are directly comparable to the "heap" column in a nightly log.
//
// Reading the result: a flat or plateauing delta means window churn is free on this machine. A
// dead-linear delta with no plateau means it is not, and the per-iteration rate times the number
// of windows a test creates should account for that test's nightly heap number.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace WindowChurnProbe
{
    internal static class Program
    {
        // ---- heap accounting, same as TestRunnerLib RunTests.MemoryManagement ----

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int GetProcessHeaps(int count, IntPtr[] heaps);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool HeapLock(IntPtr h);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool HeapUnlock(IntPtr h);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool HeapWalk(IntPtr h, ref PROCESS_HEAP_ENTRY e);

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_HEAP_ENTRY
        {
            public IntPtr lpData;
            public int cbData;
            public byte cbOverhead;
            public byte iRegionIndex;
            public short wFlags;
            public IntPtr dwCommittedSize_hMem;   // union member
            public int dwUnCommittedSize;
            public int lpFirstBlock;
            public int lpLastBlock;
        }

        private const short PROCESS_HEAP_ENTRY_BUSY = 0x0004;

        private static long CommittedBytes()
        {
            int count = GetProcessHeaps(0, null);
            var buffer = new IntPtr[count];
            GetProcessHeaps(count, buffer);
            long total = 0;
            for (int i = 0; i < count; i++)
            {
                var h = buffer[i];
                HeapLock(h);
                var e = new PROCESS_HEAP_ENTRY();
                while (HeapWalk(h, ref e))
                {
                    if ((e.wFlags & PROCESS_HEAP_ENTRY_BUSY) != 0)
                        total += e.cbData + e.cbOverhead;
                }
                HeapUnlock(h);
            }
            return total;
        }

        // ---- the churn itself ----

        private static Form _host;

        private static void OneIteration(string mode)
        {
            switch (mode)
            {
                case "child":
                    // What a DataGridView does for every cell it edits: create an editing control,
                    // force its handle, then destroy it.
                    using (var box = new TextBox())
                    {
                        _host.Controls.Add(box);
                        var unused = box.Handle;
                        _host.Controls.Remove(box);
                    }
                    break;

                case "form":
                    using (var form = new Form { Text = @"churn", Width = 200, Height = 100 })
                    {
                        form.Show();
                        Application.DoEvents();
                        form.Close();
                    }
                    break;

                case "dialog":
                    // A modal dialog, closed from a timer so the modal loop actually runs.
                    using (var dlg = new Form { Text = @"churn", Width = 200, Height = 100 })
                    using (var timer = new System.Windows.Forms.Timer { Interval = 1 })
                    {
                        timer.Tick += (s, e) => { timer.Stop(); dlg.Close(); };
                        timer.Start();
                        dlg.ShowDialog(_host);
                    }
                    break;

                case "idle":
                    Thread.SpinWait(100);
                    break;

                default:
                    throw new ArgumentException(@"unknown mode " + mode);
            }
        }

        [STAThread]
        private static int Main(string[] args)
        {
            string mode = args.Length > 0 ? args[0] : @"child";
            int n = args.Length > 1 ? int.Parse(args[1]) : 20000;

            Application.EnableVisualStyles();
            Console.WriteLine(@"TerminalServerSession={0}  SESSIONNAME={1}  MonitorCount={2}",
                SystemInformation.TerminalServerSession,
                Environment.GetEnvironmentVariable(@"SESSIONNAME") ?? @"(unset)",
                SystemInformation.MonitorCount);

            _host = new Form { Text = @"WindowChurnProbe", Width = 400, Height = 200 };
            _host.Show();
            Application.DoEvents();

            // Settle, and pay any one-time cost of the first iteration before taking the baseline.
            for (int i = 0; i < 100; i++) { Application.DoEvents(); Thread.Sleep(2); }
            OneIteration(mode);
            for (int i = 0; i < 100; i++) { Application.DoEvents(); Thread.Sleep(2); }
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();

            long baseline = CommittedBytes();
            Console.WriteLine(@"mode={0} iterations={1} baseline={2:N0} bytes", mode, n, baseline);
            Console.WriteLine(@"{0,10} {1,16} {2,14} {3,12}", @"iter", @"committed", @"delta", @"bytes/iter");

            var sw = Stopwatch.StartNew();
            int report = Math.Max(1, n / 20);
            for (int i = 1; i <= n; i++)
            {
                OneIteration(mode);
                if (i % report != 0)
                    continue;
                Application.DoEvents();
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                long now = CommittedBytes();
                long delta = now - baseline;
                Console.WriteLine(@"{0,10:N0} {1,16:N0} {2,14:N0} {3,12:F2}", i, now, delta, (double) delta / i);
            }
            sw.Stop();
            Console.WriteLine(@"elapsed {0:F1} s", sw.Elapsed.TotalSeconds);
            return 0;
        }
    }
}
