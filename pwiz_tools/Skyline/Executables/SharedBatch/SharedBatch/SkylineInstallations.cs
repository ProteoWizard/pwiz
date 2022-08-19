/*
 * Original author: Ali Marsh <alimarsh .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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

        public static bool HasLocalSkylineCmd => !string.IsNullOrEmpty(Settings.Default.SkylineLocalCommandPath);

        public static bool HasCustomSkylineCmd => !string.IsNullOrEmpty(Settings.Default.SkylineCustomCmdPath) && File.Exists(Settings.Default.SkylineCustomCmdPath);

        public static bool HasSkyline => !string.IsNullOrEmpty(Settings.Default.SkylineAdminCmdPath) || !string.IsNullOrEmpty(Settings.Default.SkylineRunnerPath);

        public static bool HasSkylineDaily => !string.IsNullOrEmpty(Settings.Default.SkylineDailyAdminCmdPath) || !string.IsNullOrEmpty(Settings.Default.SkylineDailyRunnerPath);

        #region Skyline

        public static bool FindSkyline()
        {
            FindLocalSkyline();
            FindClickOnceInstallations();
            FindAdministrativeInstallations();
            return HasSkyline || HasSkylineDaily || HasLocalSkylineCmd || HasCustomSkylineCmd;
        }

        private static void FindLocalSkyline()
        {
            var skylineCmdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SkylineCmdExe);
            Settings.Default.SkylineLocalCommandPath = File.Exists(skylineCmdPath) ? skylineCmdPath : null;
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
