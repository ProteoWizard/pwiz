/*
 * Original author: Ali Marsh <alimarsh .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using Microsoft.Win32;
using SharedBatch.Properties;

namespace SharedBatch
{

    public class SkylineInstallations
    {
        // Finds and saves information about the computer's Skyline and R installation locations

        public const string SkylineExe = "Skyline.exe";
        public const string SkylineDailyExe = "Skyline-daily.exe";
        public const string SkylineCmdExe = "SkylineCmd.exe";
        public const string Skyline = "Skyline";
        public const string SkylineDaily = "Skyline-daily";
        public const string SkylineRunnerExe = "SkylineRunner.exe";
        public const string SkylineDailyRunnerExe = "SkylineDailyRunner.exe";

        /// <summary>
        /// Test seam: a SkylineCmd.exe to stand in for an administrative Skyline installation
        /// when the machine has none. Mirrors <c>RInstallations.TestRVersions</c>.
        ///
        /// The functional tests drive the real app, and the app refuses to start - and then
        /// refuses to save a configuration - without a Skyline installation. Pointing this at a
        /// SkylineCmd.exe built in the checkout makes that run work without installing Skyline.
        ///
        /// It deliberately stands in for the ADMINISTRATIVE install rather than the local one.
        /// A local SkylineCmd.exe makes <c>SkylineSettings.ReadXml</c> retype every configuration
        /// to <see cref="SkylineType.Local"/>, which changes what an import/export round trip
        /// writes and breaks the BcfgTestFiles baselines that expect type="Skyline". Standing in
        /// as an administrative install keeps the type the configuration files already record.
        /// </summary>
        public static string TestAdminSkylineCmdPath { get; set; }

        /// <summary>
        /// Test seam: the registry key path used in place of <c>Software\MacCossLabUW</c>. The Inno Setup
        /// installer writes each channel's install folder to the InstallDir value of
        /// <c>Software\MacCossLabUW\&lt;channel&gt;</c>.
        /// </summary>
        public static string TestInnoRegistryKey { get; set; }

        /// <summary>
        /// Test seam: replaces the registry read of an Inno Setup install folder. Takes the hive and the channel and
        /// returns the InstallDir recorded there, or null. A test cannot write HKLM without running elevated.
        /// When this is set, <see cref="TestInnoRegistryKey"/> is not used.
        /// </summary>
        public static Func<RegistryHive, string, string> TestReadInnoInstallDir { get; set; }

        public static bool HasLocalSkylineCmd => !string.IsNullOrEmpty(Settings.Default.SkylineLocalCommandPath);

        public static bool HasCustomSkylineCmd => !string.IsNullOrEmpty(Settings.Default.SkylineCustomCmdPath) && File.Exists(Settings.Default.SkylineCustomCmdPath);

        public static bool HasSkyline => !string.IsNullOrEmpty(Settings.Default.SkylineInnoCmdPath) || !string.IsNullOrEmpty(Settings.Default.SkylineAdminCmdPath) || !string.IsNullOrEmpty(Settings.Default.SkylineRunnerPath);

        public static bool HasSkylineDaily => !string.IsNullOrEmpty(Settings.Default.SkylineDailyInnoCmdPath) || !string.IsNullOrEmpty(Settings.Default.SkylineDailyAdminCmdPath) || !string.IsNullOrEmpty(Settings.Default.SkylineDailyRunnerPath);

        #region Skyline

        public static bool FindSkyline()
        {
            FindLocalSkyline();
            FindInnoInstallations();
            FindClickOnceInstallations();
            FindAdministrativeInstallations();
            return HasSkyline || HasSkylineDaily || HasLocalSkylineCmd || HasCustomSkylineCmd;
        }

        private static void FindLocalSkyline()
        {
            var skylineCmdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SkylineCmdExe);
            Settings.Default.SkylineLocalCommandPath = File.Exists(skylineCmdPath) ? skylineCmdPath : null;
        }

        private static void FindInnoInstallations()
        {
            Settings.Default.SkylineInnoCmdPath = FindInnoSkylineCmd(Skyline);
            Settings.Default.SkylineDailyInnoCmdPath = FindInnoSkylineCmd(SkylineDaily);
        }

        /// <summary>
        /// Returns the SkylineCmd.exe of an Inno Setup install of the channel, or null. Looks in the install
        /// folder the installer records in HKCU, then in HKLM.
        /// </summary>
        private static string FindInnoSkylineCmd(string channel)
        {
            return GetSkylineCmdInDir(GetInnoInstallDir(RegistryHive.CurrentUser, channel)) ??
                   GetSkylineCmdInDir(GetInnoInstallDir(RegistryHive.LocalMachine, channel));
        }

        private static string GetSkylineCmdInDir(string installDir)
        {
            // A registry value can hold anything, and Path.Combine throws on characters not allowed in a path.
            if (string.IsNullOrEmpty(installDir) || installDir.IndexOfAny(Path.GetInvalidPathChars()) >= 0 ||
                !Path.IsPathRooted(installDir))
            {
                return null;
            }
            var cmdPath = Path.Combine(installDir, SkylineCmdExe);
            return File.Exists(cmdPath) ? cmdPath : null;
        }

        private static string GetInnoInstallDir(RegistryHive hive, string channel)
        {
            if (TestReadInnoInstallDir != null)
                return TestReadInnoInstallDir(hive, channel);
            var channelKeyPath = (TestInnoRegistryKey ?? @"Software\MacCossLabUW") + @"\" + channel;
            try
            {
                // The installer runs in 64-bit mode, so an all-users install writes to the 64-bit view of HKLM.
                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var channelKey = baseKey.OpenSubKey(channelKeyPath))
                {
                    return channelKey?.GetValue(@"InstallDir") as string;
                }
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException || e is IOException)
            {
                return null;
            }
        }

        private static void FindClickOnceInstallations()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            // Skyline click-once install
            var skylineInstallExists = ClickOnceInstallExists(Skyline);
            Settings.Default.SkylineRunnerPath =
                skylineInstallExists ? Path.Combine(baseDirectory, SkylineRunnerExe) : null;
            // Skyline-daily click-once install
            var skylineDailyInstallExists = ClickOnceInstallExists(SkylineDaily);
            Settings.Default.SkylineDailyRunnerPath =
                skylineDailyInstallExists ? Path.Combine(baseDirectory, SkylineDailyRunnerExe) : null;
        }

        private static bool ClickOnceInstallExists(string skylineType)
        {
            var paths = ListPossibleSkylineShortcutPaths(skylineType);
            return paths.Any(File.Exists);
        }

        private static string[] ListPossibleSkylineShortcutPaths(string skylineAppName)
        {
            var programsFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            var shortcutFilename = skylineAppName + ".appref-ms"; // Not L10N
            return new[]
            {
                Path.Combine(Path.Combine(programsFolderPath, "MacCoss Lab, UW"), shortcutFilename), // Not L10N
                Path.Combine(Path.Combine(programsFolderPath, skylineAppName), shortcutFilename),
            };
        }

        private static void FindAdministrativeInstallations()
        {
            var programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            // Skyline administrative install
            var skylinePath = Path.Combine(programFilesPath, Skyline);
            var adminInstallSkyline = Directory.Exists(skylinePath) &&
                                      File.Exists(Path.Combine(skylinePath, SkylineCmdExe));
            Settings.Default.SkylineAdminCmdPath =
                adminInstallSkyline ? Path.Combine(skylinePath, SkylineCmdExe) : null;
            // Applied here rather than assigned once by the test, because this runs again on every
            // FindSkyline() - including the one in the application's own startup and the one after
            // Settings.Reset() - and any value assigned outside would be overwritten.
            if (Settings.Default.SkylineAdminCmdPath == null && File.Exists(TestAdminSkylineCmdPath))
                Settings.Default.SkylineAdminCmdPath = TestAdminSkylineCmdPath;
            // Skyline-daily administrative install
            var skylineDailyPath = Path.Combine(programFilesPath, SkylineDaily);
            var adminInstallSkylineDaily = Directory.Exists(skylineDailyPath) &&
                                           File.Exists(Path.Combine(skylineDailyPath, SkylineCmdExe));
            Settings.Default.SkylineDailyAdminCmdPath =
                adminInstallSkylineDaily ? Path.Combine(skylineDailyPath, SkylineCmdExe) : null;
        }

        // Opens a Skyline file using the Skyline installation selected in SkylineSettings
        public static void OpenSkylineFile(string filePath, SkylineSettings skylineSettings)
        {
            var hasSkylineExe = skylineSettings.CmdPath.EndsWith(SkylineCmdExe, StringComparison.CurrentCultureIgnoreCase);
            string skylinePath;
            if (hasSkylineExe)
            {
                skylinePath = Path.Combine(FileUtil.GetDirectory(skylineSettings.CmdPath), SkylineExe);
                if (!File.Exists(skylinePath))
                    skylinePath = Path.Combine(FileUtil.GetDirectory(skylineSettings.CmdPath), SkylineDailyExe);
            }
            else if (skylineSettings.Type == SkylineType.Skyline)
            {
                var possiblePaths = ListPossibleSkylineShortcutPaths(Skyline);
                skylinePath = possiblePaths.FirstOrDefault(File.Exists);
            } else
            {
                var possiblePaths = ListPossibleSkylineShortcutPaths(SkylineDaily);
                skylinePath = possiblePaths.FirstOrDefault(File.Exists);
            }

            var args = hasSkylineExe ? $"--opendoc \"{filePath}\"" : $"\"{filePath}\""; // only use --opendoc for .exe
            Process.Start(skylinePath, args);
        }


        #endregion
    }
}
