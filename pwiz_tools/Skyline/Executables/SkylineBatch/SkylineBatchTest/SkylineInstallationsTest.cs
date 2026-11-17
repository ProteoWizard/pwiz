/*
 * Original author: Vagisha Sharma <vsharma .at. uw.edu>,
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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using SharedBatch;
using SharedBatchTest;
using SharedSettings = SharedBatch.Properties.Settings;

namespace SkylineBatchTest
{
    [TestClass]
    public class SkylineInstallationsTest : AbstractSkylineBatchUnitTest
    {
        private const string TEST_REGISTRY_KEY = @"Software\MacCossLabUW-SkylineInstallationsTest";

        /// <summary>
        /// Tests that <see cref="SkylineInstallations.FindSkyline"/> finds an Inno Setup install from the InstallDir
        /// recorded in the registry. A per-user install comes before an all-users one, and an unusable InstallDir is
        /// ignored. <see cref="SkylineSettings"/> uses the Inno Setup install before a ClickOnce or administrative
        /// install.
        /// </summary>
        [TestMethod]
        public void TestFindInnoSkyline()
        {
            var testDir = GetTestResultsPath();
            if (Directory.Exists(testDir))
                Helpers.TryTwice(() => Directory.Delete(testDir, true));
            Registry.CurrentUser.DeleteSubKeyTree(TEST_REGISTRY_KEY, false);
            var suiteRegistryKey = SkylineInstallations.TestInnoRegistryKey;
            SkylineInstallations.TestInnoRegistryKey = TEST_REGISTRY_KEY;
            try
            {
                try
                {
                    SkylineInstallations.FindSkyline();
                    AssertEx.IsNull(SharedSettings.Default.SkylineInnoCmdPath,
                        "Expected no Inno Setup Skyline since the registry key " + TEST_REGISTRY_KEY +
                        @"\Skyline does not exist");
                    AssertEx.IsNull(SharedSettings.Default.SkylineDailyInnoCmdPath,
                        "Expected no Inno Setup Skyline-daily since the registry key " + TEST_REGISTRY_KEY +
                        @"\Skyline-daily does not exist");

                    ValidateRegistryInstalls(testDir);
                    ValidateAllUsersInstall(testDir);
                    ValidateUnusableInstallDirs(testDir);
                    ValidateInnoPreferredOverClickOnce();
                }
                finally
                {
                    Registry.CurrentUser.DeleteSubKeyTree(TEST_REGISTRY_KEY, false);
                }
            }
            finally
            {
                SkylineInstallations.TestInnoRegistryKey = suiteRegistryKey;
                SkylineInstallations.FindSkyline();
            }
        }

        private static void ValidateRegistryInstalls(string testDir)
        {
            var skylineCmd = MakeSkylineCmd(Path.Combine(testDir, SkylineInstallations.Skyline));
            var dailyCmd = MakeSkylineCmd(Path.Combine(testDir, SkylineInstallations.SkylineDaily));
            SetInstallDir(SkylineInstallations.Skyline, Path.GetDirectoryName(skylineCmd));
            SetInstallDir(SkylineInstallations.SkylineDaily, Path.GetDirectoryName(dailyCmd));

            SkylineInstallations.FindSkyline();
            AssertEx.AreEqual(skylineCmd, SharedSettings.Default.SkylineInnoCmdPath,
                "Expected the Skyline install folder recorded in the registry");
            AssertEx.AreEqual(dailyCmd, SharedSettings.Default.SkylineDailyInnoCmdPath,
                "Expected the Skyline-daily install folder recorded in the registry");

            // HasSkyline is true for any kind of install. Clear the ClickOnce and administrative paths, which this
            // machine or the suite's checkout build may have set, so only the Inno Setup path can make it true.
            var settings = SharedSettings.Default;
            settings.SkylineRunnerPath = settings.SkylineAdminCmdPath = null;
            settings.SkylineDailyRunnerPath = settings.SkylineDailyAdminCmdPath = null;
            AssertEx.IsTrue(SkylineInstallations.HasSkyline, "Expected HasSkyline for an Inno Setup install alone");
            AssertEx.IsTrue(SkylineInstallations.HasSkylineDaily, "Expected HasSkylineDaily for an Inno Setup install alone");
            settings.SkylineInnoCmdPath = settings.SkylineDailyInnoCmdPath = null;
            AssertEx.IsFalse(SkylineInstallations.HasSkyline, "Expected no HasSkyline with no install");
            AssertEx.IsFalse(SkylineInstallations.HasSkylineDaily, "Expected no HasSkylineDaily with no install");
        }

        private static void ValidateAllUsersInstall(string testDir)
        {
            Registry.CurrentUser.DeleteSubKeyTree(TEST_REGISTRY_KEY, false);
            var allUsersCmd = MakeSkylineCmd(Path.Combine(testDir, @"AllUsers"));
            var perUserCmd = MakeSkylineCmd(Path.Combine(testDir, @"PerUser"));
            var allUsersDir = Path.GetDirectoryName(allUsersCmd);
            var perUserDir = Path.GetDirectoryName(perUserCmd);
            try
            {
                SkylineInstallations.TestReadInnoInstallDir = (hive, channel) =>
                    !Equals(SkylineInstallations.Skyline, channel) ? null :
                    hive == RegistryHive.LocalMachine ? allUsersDir : null;
                SkylineInstallations.FindSkyline();
                AssertEx.AreEqual(allUsersCmd, SharedSettings.Default.SkylineInnoCmdPath,
                    "Expected the all-users install folder recorded in HKLM when HKCU records none");
                AssertEx.IsNull(SharedSettings.Default.SkylineDailyInnoCmdPath,
                    "Expected no Inno Setup Skyline-daily since neither hive records one");

                SkylineInstallations.TestReadInnoInstallDir = (hive, channel) =>
                    !Equals(SkylineInstallations.Skyline, channel) ? null :
                    hive == RegistryHive.CurrentUser ? perUserDir :
                    hive == RegistryHive.LocalMachine ? allUsersDir : null;
                SkylineInstallations.FindSkyline();
                AssertEx.AreEqual(perUserCmd, SharedSettings.Default.SkylineInnoCmdPath,
                    "Expected the per-user install folder recorded in HKCU before the all-users one in HKLM");
            }
            finally
            {
                SkylineInstallations.TestReadInnoInstallDir = null;
            }
        }

        private static void ValidateUnusableInstallDirs(string testDir)
        {
            var noSkylineCmdDir = Path.Combine(testDir, @"NoSkylineCmd");
            Directory.CreateDirectory(noSkylineCmdDir);
            // A root-relative install folder: "\Users\...\RootRelative", the test folder without "C:". Windows resolves it
            // against the current drive, so File.Exists finds the SkylineCmd.exe created here even though the path is relative.
            var fullInstallDir = Path.GetDirectoryName(MakeSkylineCmd(Path.Combine(testDir, @"RootRelative")));
            Assert.IsNotNull(fullInstallDir);
            var rootRelativeDir = fullInstallDir.Substring(Path.GetPathRoot(fullInstallDir).Length - 1);
            foreach (var installDir in new[] { noSkylineCmdDir, SkylineInstallations.Skyline, @"C:\Skyline|""", rootRelativeDir })
            {
                SetInstallDir(SkylineInstallations.Skyline, installDir);
                SkylineInstallations.FindSkyline();
                AssertEx.IsNull(SharedSettings.Default.SkylineInnoCmdPath,
                    "Expected no Inno Setup Skyline for the recorded install folder " + installDir);
            }
        }

        private static void ValidateInnoPreferredOverClickOnce()
        {
            var settings = SharedSettings.Default;
            settings.SkylineInnoCmdPath = @"C:\Inno\Skyline\SkylineCmd.exe";
            settings.SkylineRunnerPath = @"C:\ClickOnce\SkylineRunner.exe";
            settings.SkylineAdminCmdPath = @"C:\Admin\Skyline\SkylineCmd.exe";
            settings.SkylineDailyInnoCmdPath = @"C:\Inno\Skyline-daily\SkylineCmd.exe";
            settings.SkylineDailyRunnerPath = @"C:\ClickOnce\SkylineDailyRunner.exe";
            settings.SkylineDailyAdminCmdPath = @"C:\Admin\Skyline-daily\SkylineCmd.exe";

            AssertEx.AreEqual(settings.SkylineInnoCmdPath, new SkylineSettings(SkylineType.Skyline, null).CmdPath,
                "Expected an Inno Setup Skyline to come before ClickOnce and administrative installs");
            AssertEx.AreEqual(settings.SkylineDailyInnoCmdPath, new SkylineSettings(SkylineType.SkylineDaily, null).CmdPath,
                "Expected an Inno Setup Skyline-daily to come before ClickOnce and administrative installs");

            settings.SkylineInnoCmdPath = null;
            settings.SkylineDailyInnoCmdPath = null;
            AssertEx.AreEqual(settings.SkylineRunnerPath, new SkylineSettings(SkylineType.Skyline, null).CmdPath,
                "Expected the ClickOnce Skyline without an Inno Setup install");
            AssertEx.AreEqual(settings.SkylineDailyRunnerPath, new SkylineSettings(SkylineType.SkylineDaily, null).CmdPath,
                "Expected the ClickOnce Skyline-daily without an Inno Setup install");
        }

        private static string MakeSkylineCmd(string installDir)
        {
            Directory.CreateDirectory(installDir);
            var cmdPath = Path.Combine(installDir, SkylineInstallations.SkylineCmdExe);
            File.WriteAllText(cmdPath, string.Empty);
            return cmdPath;
        }

        private static void SetInstallDir(string channel, string installDir)
        {
            using (var channelKey = Registry.CurrentUser.CreateSubKey(TEST_REGISTRY_KEY + @"\" + channel))
            {
                Assert.IsNotNull(channelKey, "Expected the test registry key to be created for " + channel);
                channelKey.SetValue(@"InstallDir", installDir);
            }
        }
    }
}
