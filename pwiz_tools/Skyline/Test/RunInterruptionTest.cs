/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;
using SkylineNightly;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Tests the classification SkylineNightly uses to explain a run that ended without finishing,
    /// from synthetic Windows event records shaped like the real ones (insertion strings, not
    /// formatted messages).
    /// </summary>
    [TestClass]
    public class RunInterruptionTest : AbstractUnitTest
    {
        private const string RUNTIMES_13 = "Microsoft.NETCore.App 10.0.13, Microsoft.WindowsDesktop.App 10.0.13";
        private const string RUNTIMES_14 = "Microsoft.NETCore.App 10.0.14, Microsoft.WindowsDesktop.App 10.0.14";
        private const string TRUSTED_INSTALLER = @"C:\WINDOWS\servicing\TrustedInstaller.exe";
        private const string WINLOGON = @"C:\WINDOWS\system32\winlogon.exe";
        private const string CUMULATIVE_UPDATE = "2026-10 Cumulative Update for Windows 10 Version 22H2 for x64-based Systems (KB5066791)";
        private const string DOTNET_UPDATE = "2026-10 .NET 10.0.14 Security Update for x64 Client (KB5068000)";
        private const string DEFENDER_UPDATE = "Security Intelligence Update for Microsoft Defender Antivirus - KB2267602 (Version 1.461.209.0)";
        private const string STORE_UPDATE = "9PLM9XGG6VKS-OpenAI.Codex";

        private static readonly DateTime RUN_START = new DateTime(2026, 10, 25, 10, 30, 0);
        private static readonly DateTime LAST_WRITE = new DateTime(2026, 10, 26, 3, 10, 0);

        [TestMethod]
        public void TestRunInterruption()
        {
            TestWindowsUpdateRestart();
            TestOtherRestart();
            TestUnexpectedShutdown();
            TestNightlyCrash();
            TestStillRunning();
            TestRuntimeChangeOnly();
            TestParseRuntimes();
            TestReadMachineState();
        }

        private static void TestWindowsUpdateRestart()
        {
            // The insertion string may or may not carry the computer name after the process path
            foreach (var process in new[] { TRUSTED_INSTALLER, TRUSTED_INSTALLER + @" (BSPRATT-UW2)" })
            {
                var events = new[]
                {
                    Update(RUN_START.AddHours(2), DEFENDER_UPDATE),
                    Update(RUN_START.AddHours(3), STORE_UPDATE),
                    Update(LAST_WRITE.AddMinutes(-30), CUMULATIVE_UPDATE),
                    Update(LAST_WRITE.AddMinutes(-20), DOTNET_UPDATE),
                    Restart(LAST_WRITE.AddMinutes(2), process),
                    // After the restart: not part of the interrupted run
                    Update(LAST_WRITE.AddHours(1), "2026-10 Update after the restart (KB0000001)")
                };
                var interruption = RunInterruption.Diagnose(events, LAST_WRITE, false, RUNTIMES_13, RUNTIMES_14);
                Assert.AreEqual(InterruptionCause.windows_update_restart, interruption.Cause);
                Assert.AreEqual(LAST_WRITE.AddMinutes(2), interruption.Time);
                AssertEx.Contains(interruption.Detail, TRUSTED_INSTALLER, @"NT AUTHORITY\SYSTEM", @"restart");
                CollectionAssert.AreEqual(new[] { CUMULATIVE_UPDATE, DOTNET_UPDATE }, interruption.UpdatesInstalled.ToArray());
                AssertEx.Contains(interruption.RuntimeChange, RUNTIMES_13, RUNTIMES_14);
                AssertEx.Contains(interruption.ToString(), CUMULATIVE_UPDATE, DOTNET_UPDATE, RUNTIMES_14);
            }
        }

        private static void TestOtherRestart()
        {
            // A restart well before the log's last write cannot have ended the run
            var events = new[]
            {
                Restart(RUN_START.AddHours(-1), TRUSTED_INSTALLER),
                Restart(LAST_WRITE.AddMinutes(5), WINLOGON)
            };
            var interruption = RunInterruption.Diagnose(events, LAST_WRITE, false, RUNTIMES_13, RUNTIMES_13);
            Assert.AreEqual(InterruptionCause.restart, interruption.Cause);
            AssertEx.Contains(interruption.Detail, WINLOGON);
            Assert.IsNull(interruption.RuntimeChange);
            Assert.AreEqual(0, interruption.UpdatesInstalled.Count);
        }

        private static void TestUnexpectedShutdown()
        {
            foreach (var shutdown in new[]
                     {
                         new SystemEventRecord(LAST_WRITE.AddMinutes(30), RunInterruption.PROVIDER_EVENT_LOG, RunInterruption.ID_UNEXPECTED_SHUTDOWN, @"3:12:00 AM", @"10/26/2026"),
                         new SystemEventRecord(LAST_WRITE.AddMinutes(30), RunInterruption.PROVIDER_KERNEL_POWER, RunInterruption.ID_POWER_LOSS)
                     })
            {
                var interruption = RunInterruption.Diagnose(new[] { shutdown }, LAST_WRITE, false, null, RUNTIMES_14);
                Assert.AreEqual(InterruptionCause.unexpected_shutdown, interruption.Cause);
                // No runtimes line in a log from before the line existed, so nothing to compare
                Assert.IsNull(interruption.RuntimeChange);
            }

            // The same ids from other providers are not shutdowns
            var unrelated = new SystemEventRecord(LAST_WRITE.AddMinutes(30), @"Some-Other-Provider", RunInterruption.ID_POWER_LOSS);
            Assert.AreEqual(InterruptionCause.unknown, RunInterruption.Diagnose(new[] { unrelated }, LAST_WRITE, false, null, RUNTIMES_14).Cause);
        }

        private static void TestNightlyCrash()
        {
            // The BSPRATT-UW2 failure: the runtime was serviced underneath SkylineNightly, which then
            // failed to load a framework assembly on its finalizer thread
            const string exceptionInfo = @"Exception Info: System.IO.FileNotFoundException: Could not load file or assembly 'System.Diagnostics.TraceSource, Version=6.0.0.0'";
            var crashText = string.Join(Environment.NewLine,
                @"Application: SkylineNightly.exe",
                @"CoreCLR Version: 10.0.1326.50504",
                @".NET Version: 10.0.13",
                @"Description: The process was terminated due to an unhandled exception.",
                exceptionInfo,
                @"   at Microsoft.Diagnostics.Runtime.WindowsProcessDataReader.Finalize()");
            var events = new[]
            {
                // A TestRunner crash does not end SkylineNightly
                new SystemEventRecord(LAST_WRITE, RunInterruption.PROVIDER_DOTNET_RUNTIME, RunInterruption.ID_DOTNET_CRASH,
                    @"Application: TestRunner.exe" + Environment.NewLine + @"Exception Info: System.InvalidOperationException"),
                new SystemEventRecord(LAST_WRITE.AddSeconds(20), RunInterruption.PROVIDER_DOTNET_RUNTIME, RunInterruption.ID_DOTNET_CRASH, crashText),
                // The process stays alive behind its crash dialog until the next run stops it
                Restart(LAST_WRITE.AddHours(10), WINLOGON)
            };
            var interruption = RunInterruption.Diagnose(events, LAST_WRITE, true, RUNTIMES_13, RUNTIMES_14);
            Assert.AreEqual(InterruptionCause.crash, interruption.Cause);
            AssertEx.Contains(interruption.Detail, exceptionInfo);
            Assert.IsNotNull(interruption.RuntimeChange);

            var nativeCrash = new SystemEventRecord(LAST_WRITE.AddSeconds(20), RunInterruption.PROVIDER_APPLICATION_ERROR, RunInterruption.ID_APPLICATION_CRASH,
                @"SkylineNightly.exe", @"3.0.26299.0", @"6a1b2c3d", @"coreclr.dll", @"10.0.1326.50504", @"6a1b2c3d", @"c0000005", @"0000000000176ec2");
            interruption = RunInterruption.Diagnose(new[] { nativeCrash }, LAST_WRITE, false, RUNTIMES_13, RUNTIMES_13);
            Assert.AreEqual(InterruptionCause.crash, interruption.Cause);
            AssertEx.Contains(interruption.Detail, @"coreclr.dll", @"c0000005");
        }

        private static void TestStillRunning()
        {
            var interruption = RunInterruption.Diagnose(Array.Empty<SystemEventRecord>(), LAST_WRITE, true, RUNTIMES_13, RUNTIMES_13);
            Assert.AreEqual(InterruptionCause.still_running, interruption.Cause);
            Assert.IsNull(interruption.Time);
        }

        private static void TestRuntimeChangeOnly()
        {
            // With no cause event, only updates before the log's last write belong to the run; the
            // machine kept updating after the run was dead, and that must not be blamed on it
            var events = new[]
            {
                Update(LAST_WRITE.AddMinutes(-5), DOTNET_UPDATE),
                Update(LAST_WRITE.AddHours(15), CUMULATIVE_UPDATE)
            };
            var interruption = RunInterruption.Diagnose(events, LAST_WRITE, false, RUNTIMES_13, RUNTIMES_14);
            Assert.AreEqual(InterruptionCause.unknown, interruption.Cause);
            AssertEx.Contains(interruption.RuntimeChange, RUNTIMES_14);
            CollectionAssert.AreEqual(new[] { DOTNET_UPDATE }, interruption.UpdatesInstalled.ToArray());
        }

        private static void TestParseRuntimes()
        {
            var log = string.Join("\r\n", @"[10:30:03] 10/25/2026", @"[10:30:03] " + RunInterruption.RUNTIMES_PREFIX + RUNTIMES_13,
                @"[10:30:04] buildRoot is D:\Nightly\SkylineTesterForNightly_perf\pwiz", string.Empty);
            Assert.AreEqual(RUNTIMES_13, RunInterruption.ParseRuntimes(log));
            Assert.IsNull(RunInterruption.ParseRuntimes(@"[10:30:03] 10/25/2026" + "\r\n"));
        }

        /// <summary>
        /// Reads this machine's real event logs and runtimes. What they contain varies by machine, so
        /// this checks only that the queries are well formed and return what was asked for.
        /// </summary>
        private static void TestReadMachineState()
        {
            var since = DateTime.Now.AddDays(-7);
            var requestedIds = RunInterruption.SYSTEM_EVENT_IDS.Concat(RunInterruption.APPLICATION_EVENT_IDS).ToArray();
            foreach (var record in MachineState.ReadEvents(since))
            {
                CollectionAssert.Contains(requestedIds, record.Id);
                Assert.IsTrue(record.Time >= since.AddMinutes(-1));
            }
            // Any machine that builds these tests has the .NET SDK, which installs a shared runtime
            AssertEx.Contains(MachineState.DescribeInstalledRuntimes(), @"Microsoft.NETCore.App ");
            MachineState.IsRestartPending();
        }

        private static SystemEventRecord Update(DateTime time, string title)
        {
            return new SystemEventRecord(time, RunInterruption.PROVIDER_WINDOWS_UPDATE, RunInterruption.ID_UPDATE_INSTALLED,
                title, Guid.Empty.ToString(), @"1");
        }

        private static SystemEventRecord Restart(DateTime time, string process)
        {
            // Insertion strings: process, computer, reason, reason code, shutdown type, comment, user
            return new SystemEventRecord(time, RunInterruption.PROVIDER_USER32, RunInterruption.ID_RESTART_INITIATED,
                process, @"BSPRATT-UW2", @"Operating System: Service pack (Planned)", @"0x80020010", @"restart",
                string.Empty, @"NT AUTHORITY\SYSTEM");
        }
    }
}
