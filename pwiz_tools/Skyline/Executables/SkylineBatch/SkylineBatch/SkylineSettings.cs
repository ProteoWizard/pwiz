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
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public class SkylineSettings
    {
        private const string SkylineCmdExe = "SkylineCmd.exe";
        public const string Skyline = "Skyline";
        public const string SkylineDaily = "Skyline-daily";
        private const string SkylineRunnerExe = "SkylineRunner.exe";
        private const string SkylineDailyRunnerExe = "SkylineDailyRunner.exe";
        private const string SkylineExe = Skyline + ".exe";
        private const string SkylineDailyExe = SkylineDaily + ".exe";

        public static bool IsInitialized()
        {
            // Check for the new settings.  If they are empty it means that this is a new installation
            // OR the first time the program is being run after the upgrade that added the settings.
            return !string.IsNullOrWhiteSpace(Settings.Default.SkylineInstallDir);
        }

        public static bool HasRPath()
        {
            return !string.IsNullOrWhiteSpace(Settings.Default.RDir);
        }

        public static bool FindSkyline(out IList<string> pathsChecked)
        {
            pathsChecked = new List<string>();
            string skyineInstallDir = null;
            string skylineType = null;
            if (AdminInstallExists(ref skyineInstallDir, ref skylineType, pathsChecked))
            {
                SaveSettings(skylineType, skyineInstallDir);
                return true;
            }
            if (ClickOnceInstallExists(ref skylineType, pathsChecked))
            {
                SaveSettings(skylineType);
                return true;
            }

            return false;
        }

        public static bool FindR(out IList<string> pathsChecked)
        {
            pathsChecked = new List<string>();
            string rDir = null;

            
            if (TryGetR(ref rDir, pathsChecked))
            {
                Settings.Default.RDir = rDir;
                Settings.Default.Save();
                Program.LogInfo(new StringBuilder("Skyline settings changed. ").Append(GetSkylineSettingsStr()).ToString());
                return true;
            }

            return false;
        }

        private static bool ClickOnceInstallExists(ref string skylineType, ICollection<string> pathsChecked)
        {
            // ClickOnce Skyline installation
            if (ClickOnceInstallExists(Skyline, pathsChecked))
            {
                skylineType = Skyline;
                return true;
            }

            // ClickOnce Skyline-daily installation
            if (ClickOnceInstallExists(SkylineDaily, pathsChecked))
            {
                skylineType = SkylineDaily;
                return true;
            }

            return false;
        }

        private static bool ClickOnceInstallExists(string skylineType, ICollection<string> pathsChecked)
        {
            var paths = ListPossibleSkylineShortcutPaths(skylineType);
            Array.ForEach(paths, pathsChecked.Add);
            return paths.Any(File.Exists);
        }

        private static bool TryGetR(ref string rPath, ICollection<string> pathsChecked)
        {
            var rDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "\\R";
            if (!Directory.Exists(rDirectory))
                return false;

            var rVersions = Directory.GetDirectories(rDirectory);
            int i = rVersions.Length - 1;
            while (i >= 0)
            {
                if (Path.GetFileName(rVersions[i]).StartsWith("R-"))
                {
                    var possiblePath = rVersions[i] + "\\bin\\Rscript.exe";
                    pathsChecked.Add(possiblePath);
                    if (File.Exists(possiblePath))
                    {
                        rPath = possiblePath;
                        return true;
                    }
                }
                i--;
            }
            return false;
            
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

        public static bool UseSkyline => Settings.Default.SkylineType.Equals(Skyline);

        public static bool UseClickOnceInstall => string.IsNullOrWhiteSpace(Settings.Default.SkylineInstallDir);

        public static string SkylineInstallDir => Settings.Default.SkylineInstallDir;

        public static string GetSkylineCmdLineExePath => UseClickOnceInstall
            ? GetSkylineRunnerPath()
            : Path.Combine(SkylineInstallDir, SkylineCmdExe);

        public static string GetRscriptExeLocation => Settings.Default.RDir;

        private static string GetSkylineRunnerPath()
        {
            return GetSkylineRunnerPath(UseSkyline);
        }

        private static string GetSkylineRunnerPath(bool useSkyline)
        {
            var runnerName = useSkyline ? SkylineRunnerExe : SkylineDailyRunnerExe;
            var runnerDirectory = Path.GetDirectoryName(typeof(SkylineSettings).Assembly.Location);
            if (runnerDirectory == null)
                throw new ArgumentNullException(nameof(runnerDirectory), @"Could not find skyline runner directory.");
            return Path.Combine(runnerDirectory, runnerName);
        }

        private static void SaveSettings(string skylineType)
        {
            SaveSettings(skylineType, string.Empty);
        }

        private static void SaveSettings(string skylineType, string skylineInstallPath)
        {
            Settings.Default.SkylineType = skylineType;
            Settings.Default.SkylineInstallDir = skylineInstallPath;
            Settings.Default.Save();
            Program.LogInfo(new StringBuilder("Skyline settings changed. ").Append(GetSkylineSettingsStr()).ToString());
        }

        public static string GetSkylineSettingsStr()
        {
            var str = new StringBuilder($"Skyline type: {Settings.Default.SkylineType}; ");
            str.Append(UseClickOnceInstall ? "Using web-based Skyline install" : $"Skyline installation directory {Settings.Default.SkylineInstallDir}");
            return str.ToString();
        }

        public static bool UpdateSettings(bool useSkyline, bool useClickOnceInstaller, string installDir,
            out string errors)
        {
            errors = string.Empty;
            var skylineTypeClicked = useSkyline ? Skyline : SkylineDaily;

            if (useClickOnceInstaller)
            {
                var pathsChecked = new List<string>();
                var skylineRunnerPath = GetSkylineRunnerPath(useSkyline);
                if (!File.Exists(skylineRunnerPath))
                {
                    errors = $"{skylineRunnerPath} does not exist.";
                    return false;
                }

                if (!ClickOnceInstallExists(skylineTypeClicked, pathsChecked))
                {
                    var err = new StringBuilder(
                            $"Could not find a web-based install of {skylineTypeClicked}. Checked in the following locations:")
                        .AppendLine()
                        .Append(string.Join(Environment.NewLine, pathsChecked));
                    errors = err.ToString();
                    return false;
                }

                SaveSettings(skylineTypeClicked);
            }

            else
            {
                if (string.IsNullOrWhiteSpace(installDir))
                {
                    errors = "Path to Skyline installation directory cannot be empty.";
                    return false;
                }
                if (!Directory.Exists(installDir))
                {
                    errors = $"Directory '{installDir}' does not exist.";
                    return false;
                }

                var skylineCmdExePath = Path.Combine(installDir, SkylineCmdExe);
                if (!File.Exists(skylineCmdExePath))
                {
                    errors = $"{SkylineCmdExe} was not found in '{installDir}'.";
                    return false;
                }

                var skylineExe = useSkyline ? SkylineExe : SkylineDailyExe;
                var skylineExePath = Path.Combine(installDir, skylineExe);
                if (!File.Exists(skylineExePath))
                {
                    errors = $"{skylineExe} was not found in '{installDir}'.";
                    return false;
                }

                SaveSettings(skylineTypeClicked, installDir);
            }

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

        public static bool SettingsChanged(bool useSkylineSelected, bool clickOnceInstallSelected, string installDirEntered)
        {
            return UseSkyline != useSkylineSelected
                   || UseClickOnceInstall != clickOnceInstallSelected
                   || (!clickOnceInstallSelected && !SkylineInstallDir.Equals(installDirEntered));
        }
    }
}
