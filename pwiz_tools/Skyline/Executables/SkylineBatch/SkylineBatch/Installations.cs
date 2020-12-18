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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using SkylineBatch.Properties;

namespace SkylineBatch
{



    public class Installations
    {
        public const string SkylineCmdExe = "SkylineCmd.exe";
        public const string Skyline = "Skyline";
        public const string SkylineDaily = "Skyline-daily";
        public const string SkylineRunnerExe = "SkylineRunner.exe";
        public const string SkylineDailyRunnerExe = "SkylineDailyRunner.exe";
        private const string RegistryLocationR = @"SOFTWARE\R-core\R\";

        public static bool HasLocalSkylineCmd => !string.IsNullOrEmpty(Settings.Default.SkylineLocalCommandPath);

        public static bool HasSkyline => !string.IsNullOrEmpty(Settings.Default.SkylineAdminCmdPath) || !string.IsNullOrEmpty(Settings.Default.SkylineRunnerPath);

        public static bool HasSkylineDaily => !string.IsNullOrEmpty(Settings.Default.SkylineDailyAdminCmdPath) || !string.IsNullOrEmpty(Settings.Default.SkylineDailyRunnerPath);

        #region Skyline

        public static bool FindSkyline()
        {
            FindLocalSkyline();
            FindClickOnceInstallations();
            FindAdministrativeInstallations();
            return HasSkyline || HasSkylineDaily || HasLocalSkylineCmd;
        }

        private static void FindLocalSkyline()
        {
            var skylineCmdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SkylineCmdExe);
            Settings.Default.SkylineLocalCommandPath = File.Exists(skylineCmdPath) ? skylineCmdPath : null;
        }

        private static void FindClickOnceInstallations()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var skylineInstallExists = ClickOnceInstallExists(Skyline);
            Settings.Default.SkylineRunnerPath =
                skylineInstallExists ? Path.Combine(baseDirectory, SkylineRunnerExe) : null;

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

            var skylinePath = Path.Combine(programFilesPath, Skyline);
            var skylineDailyPath = Path.Combine(programFilesPath, SkylineDaily);
            var adminInstallSkyline = Directory.Exists(skylinePath) &&
                                      Directory.Exists(Path.Combine(skylinePath, SkylineCmdExe));
            Settings.Default.SkylineAdminCmdPath =
                adminInstallSkyline ? Path.Combine(skylinePath, SkylineCmdExe) : null;

            var adminInstallSkylineDaily = Directory.Exists(skylineDailyPath) &&
                                           Directory.Exists(Path.Combine(skylineDailyPath, SkylineCmdExe));
            Settings.Default.SkylineDailyAdminCmdPath =
                adminInstallSkylineDaily ? Path.Combine(skylineDailyPath, SkylineCmdExe) : null;

        }

        #endregion



        #region R

        public static bool FindRDirectory()
        {
            if (string.IsNullOrWhiteSpace(Settings.Default.RDir))
            {
                RegistryKey rKey = null;
                try
                {
                    rKey = Registry.LocalMachine.OpenSubKey(RegistryLocationR);
                }
                catch (Exception)
                {
                }
                if (rKey == null)
                    return false;
                var latestRPath = rKey.GetValue(@"InstallPath") as string;
                Settings.Default.RDir = Path.GetDirectoryName(latestRPath);
            }

            InitRscriptExeList();
            return Settings.Default.RVersions.Count > 0;
        }

        private static void InitRscriptExeList()
        {
            var rPaths = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(Settings.Default.RDir))
            {
                return;
            }

            var rVersions = Directory.GetDirectories(Settings.Default.RDir);
            foreach (var rVersion in rVersions)
            {
                var folderName = Path.GetFileName(rVersion);
                if (folderName.StartsWith("R-"))
                {
                    var rScriptPath = rVersion + "\\bin\\Rscript.exe";
                    if (File.Exists(rScriptPath))
                    {
                        rPaths.Add(folderName.Substring(2), rScriptPath);
                    }
                }

            }

            Settings.Default.RVersions = rPaths;
        }

        #endregion





    }
}
