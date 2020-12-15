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
using System.Text;
using Microsoft.Win32;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public class SkylineSettings
    {
        private const string SkylineCmdExe = "SkylineCmd.exe";
        public const string Skyline = "Skyline";
        public const string SkylineDaily = "Skyline-daily";
        public const string SkylineRunnerExe = "SkylineRunner.exe";
        public const string SkylineDailyRunnerExe = "SkylineDailyRunner.exe";
        private const string SkylineExe = Skyline + ".exe";
        private const string SkylineDailyExe = SkylineDaily + ".exe";
        private const string RegistryLocationR = @"SOFTWARE\R-core\R\";

        public static bool IsInitialized()
        {
            // Check for the new settings.  If they are empty it means that this is a new installation
            // OR the first time the program is being run after the upgrade that added the settings.
            return !string.IsNullOrWhiteSpace(Settings.Default.SkylineCommandPath);
        }

        public static bool FindLocalSkylineCmd()
        {
            var skylineCmdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SkylineCmdExe);
            if (!File.Exists(skylineCmdPath))
            {
                Settings.Default.AdminInstallation = false;
                return false;
            }
            Settings.Default.SkylineCommandPath = skylineCmdPath;
            Settings.Default.AdminInstallation = true;
            return true;
        }

        public static bool FindRDirectory()
        {
            if (string.IsNullOrWhiteSpace(Settings.Default.RDir))
            {
                var rKey = Registry.LocalMachine.OpenSubKey(RegistryLocationR) ?? Registry.CurrentUser.OpenSubKey(RegistryLocationR);
                if (rKey == null)
                    return false;
                var latestRPath = rKey.GetValue(@"InstallPath") as string;
                Settings.Default.RDir = Path.GetDirectoryName(latestRPath);
            }

            InitRscriptExeList();
            return Settings.Default.RVersions.Count > 0;
        }

        public static void InitRscriptExeList()
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

        public static bool SavedInstallationsEquals(Tuple<bool,bool> installations)
        {
            return Settings.Default.SkylineClickOnceInstalled == installations.Item1 &&
                   Settings.Default.SkylineDailyClickOnceInstalled == installations.Item2;
        }

        public static void UpdateSavedInstallations(Tuple<bool, bool> installations)
        {
            Settings.Default.SkylineClickOnceInstalled = installations.Item1;
            Settings.Default.SkylineDailyClickOnceInstalled = installations.Item2;
        }

        public static Tuple<bool,bool> ClickOnceInstallExists()
        {
            var pathsChecked = new List<string>();
            var skylineInstallExists = ClickOnceInstallExists(Skyline, pathsChecked);
            var skylineDailyInstallExists = ClickOnceInstallExists(SkylineDaily, pathsChecked);
            return new Tuple<bool, bool>(skylineInstallExists, skylineDailyInstallExists);
        }

        private static bool ClickOnceInstallExists(string skylineType, ICollection<string> pathsChecked)
        {
            var paths = ListPossibleSkylineShortcutPaths(skylineType);
            Array.ForEach(paths, pathsChecked.Add);
            return paths.Any(File.Exists);
        }

        private static bool AdminInstallExists(ref string skylineInstallDir, ref string skylineType, ICollection<string> pathsChecked)
        {
            var programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var installDirs = new[]
            {
                Path.Combine(programFilesPath, Skyline),
                Path.Combine(programFilesPath, SkylineDaily)
            };

            foreach (var installDir in installDirs)
            {
                pathsChecked.Add(installDir);
                if (!Directory.Exists(installDir))
                {
                    continue;
                }
                var skyCmdExe = Path.Combine(installDir, SkylineCmdExe);
                var skyType = File.Exists(Path.Combine(installDir, SkylineExe))
                    ? Skyline
                    : (File.Exists(Path.Combine(installDir, SkylineDailyExe)) ? SkylineDaily : null);

                if (File.Exists(skyCmdExe) && skyType != null)
                {
                    skylineInstallDir = installDir;
                    skylineType = skyType;
                    return true;
                }
            }

            return false;
        }

        public static bool UseClickOnceInstall => Settings.Default.SkylineCommandPath.EndsWith("Runner.exe");

        public static string GetSkylineCmdLineExePath => Settings.Default.SkylineCommandPath;
        

        public static string GetSkylineSettingsStr()
        {
            var str = new StringBuilder($"Skyline type: {Settings.Default.SkylineType}; ");
            str.Append(UseClickOnceInstall ? "Using web-based Skyline install" : $"Skyline installation directory {Settings.Default.SkylineCommandPath}");
            return str.ToString();
        }

        public static bool UpdateSettings(bool useSkyline, bool useSkylineDaily, string skylineFolderDir,
            out string errors)
        {
            errors = string.Empty;
            if (useSkyline || useSkylineDaily)
            {
                var runnerExe = useSkyline ? SkylineRunnerExe : SkylineDailyRunnerExe;
                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                var skylineRunnerPath = Path.Combine(baseDirectory, runnerExe);
                if (!File.Exists(skylineRunnerPath))
                {
                    errors = $"{skylineRunnerPath} does not exist.";
                    return false;
                }
                Settings.Default.SkylineCommandPath = skylineRunnerPath;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(skylineFolderDir))
                {
                    errors = "Path to Skyline installation directory cannot be empty.";
                    return false;
                }
                if (!Directory.Exists(skylineFolderDir))
                {
                    errors = $"Directory '{skylineFolderDir}' does not exist.";
                    return false;
                }

                var skylineCmdExePath = Path.Combine(skylineFolderDir, SkylineCmdExe);
                if (!File.Exists(skylineCmdExePath))
                {
                    errors = $"{SkylineCmdExe} was not found in '{skylineFolderDir}'.";
                    return false;
                }

                Settings.Default.SkylineCommandPath = skylineCmdExePath;
            }
            Settings.Default.Save();
            return true;
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
    }
}
