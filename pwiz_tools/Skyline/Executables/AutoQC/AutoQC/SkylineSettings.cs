using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AutoQC.Properties;

namespace AutoQC
{
    public class SkylineSettings
    {
        private const string SKYLINE_CMD_EXE = "SkylineCmd.exe";
        public const string Skyline = "Skyline";
        public const string SkylineDaily = "Skyline-daily";
        private const string SKYLINE_RUNNER_EXE = "SkylineRunner.exe";
        private const string SKYLINE_DAILY_RUNNER_EXE = "SkylineDailyRunner.exe";
        private const string SKYLINE_EXE = Skyline + ".exe";
        private const string SKYLINE_DAILY_EXE = SkylineDaily + ".exe";

        public static bool IsInitialized()
        {
            // Check for the new settings.  If they are empty it means that this is a new installation
            // OR the first time the program is being run after the upgrade that added the settings.
            return !string.IsNullOrWhiteSpace(Settings.Default.SkylineType) &&
                   !string.IsNullOrWhiteSpace(Settings.Default.SkylineCmdLineExePath);
        }

        public static bool FindSkyline(out IList<string> pathsChecked)
        {
            pathsChecked = new List<string>();
            string skylineCmdLineExePath = null;
            string skylineType = null;
            if (AdminInstallExists(ref skylineCmdLineExePath, ref skylineType, pathsChecked) ||
                ClickOnceInstallExists(ref skylineCmdLineExePath, ref skylineType, pathsChecked))
            {
                ChangeSettings(skylineType, skylineCmdLineExePath);
                return true;
            }

            return false;
        }

        private static bool ClickOnceInstallExists(ref string skylineCmdLineExePath, ref string skylineType, ICollection<string> pathsChecked)
        {
            // ClickOnce Skyline installation
            if (ClickOnceInstallExists(Skyline, pathsChecked))
            {
                skylineType = Skyline;
                skylineCmdLineExePath = GetSkylineRunnerPath(true);
                return true;
            }

            // ClickOnce Skyline-daily installation
            if (ClickOnceInstallExists(SkylineDaily, pathsChecked))
            {
                skylineType = SkylineDaily;
                skylineCmdLineExePath = GetSkylineRunnerPath(false);
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

        private static bool AdminInstallExists(ref string skylineCmdLineExePath, ref string skylineType, ICollection<string> pathsChecked)
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
                var skyCmdExe = Path.Combine(installDir, SKYLINE_CMD_EXE);
                var skyType = File.Exists(Path.Combine(installDir, SKYLINE_EXE))
                    ? Skyline
                    : (File.Exists(Path.Combine(installDir, SKYLINE_DAILY_EXE)) ? SkylineDaily : null);

                if (File.Exists(skyCmdExe) && skyType != null)
                {
                    skylineCmdLineExePath = skyCmdExe;
                    skylineType = skyType;
                    return true;
                }
            }

            return false;
        }

        public static bool UseSkyline => Settings.Default.SkylineType.Equals(Skyline);

        public static bool UseClickOnceInstaller => GetSkylineRunnerPath().Equals(GetSkylineCmdExePath);

        public static string GetSkylineCmdExePath => Settings.Default.SkylineCmdLineExePath;

        public static string GetSkylineCmdExeDir
        {
            get
            {
                var exePath = GetSkylineCmdExePath;
                return string.IsNullOrWhiteSpace(exePath) ? "" : Path.GetDirectoryName(exePath);
            }
        }

        private static string GetSkylineRunnerPath()
        {
            return GetSkylineRunnerPath(UseSkyline);
        }

        private static string GetSkylineRunnerPath(bool useSkyline)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, useSkyline ? SKYLINE_RUNNER_EXE : SKYLINE_DAILY_RUNNER_EXE);
        }

        private static void ChangeSettings(string skylineType, string skylineCmdLineExePath)
        {
            Settings.Default.SkylineType = skylineType;
            Settings.Default.SkylineCmdLineExePath = skylineCmdLineExePath;
            Settings.Default.Save();
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
                
                ChangeSettings(skylineTypeClicked, skylineRunnerPath);
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

                var skylineCmdExePath = Path.Combine(installDir, SKYLINE_CMD_EXE);
                if (!File.Exists(skylineCmdExePath))
                {
                    errors = $"{SKYLINE_CMD_EXE} was not found in '{installDir}'.";
                    return false;
                }

                var skylineExe = useSkyline ? SKYLINE_EXE : SKYLINE_DAILY_EXE;
                var skylineExePath = Path.Combine(installDir, skylineExe);
                if (!File.Exists(skylineExePath))
                {
                    errors = $"{skylineExe} was not found in '{installDir}'.";
                    return false;
                }

                ChangeSettings(skylineTypeClicked, skylineCmdExePath);
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
                   || UseClickOnceInstaller != clickOnceInstallSelected
                   || (!clickOnceInstallSelected && !GetSkylineCmdExeDir.Equals(installDirEntered));
        }
    }
}
