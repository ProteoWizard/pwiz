using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AutoQC.Properties;

namespace AutoQC
{
    public class Installations
    {
        public const string SkylineCmdExe = "SkylineCmd.exe";
        public const string Skyline = "Skyline";
        public const string SkylineDaily = "Skyline-daily";
        public const string SkylineRunnerExe = "SkylineRunner.exe";
        public const string SkylineDailyRunnerExe = "SkylineDailyRunner.exe";

        public static bool HasCustomSkylineCmd => !string.IsNullOrEmpty(Settings.Default.SkylineCustomCmdPath) && File.Exists(Settings.Default.SkylineCustomCmdPath);

        public static bool HasSkyline => !string.IsNullOrEmpty(Settings.Default.SkylineAdminCmdPath) || !string.IsNullOrEmpty(Settings.Default.SkylineRunnerPath);

        public static bool HasSkylineDaily => !string.IsNullOrEmpty(Settings.Default.SkylineDailyAdminCmdPath) || !string.IsNullOrEmpty(Settings.Default.SkylineDailyRunnerPath);

        #region Skyline

        public static bool FindSkyline()
        {
            FindClickOnceInstallations();
            FindAdministrativeInstallations();
            return HasSkyline || HasSkylineDaily || HasCustomSkylineCmd;
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
                                      File.Exists(Path.Combine(skylinePath, SkylineCmdExe));
            Settings.Default.SkylineAdminCmdPath =
                adminInstallSkyline ? Path.Combine(skylinePath, SkylineCmdExe) : null;

            var adminInstallSkylineDaily = Directory.Exists(skylineDailyPath) &&
                                           File.Exists(Path.Combine(skylineDailyPath, SkylineCmdExe));
            Settings.Default.SkylineDailyAdminCmdPath =
                adminInstallSkylineDaily ? Path.Combine(skylineDailyPath, SkylineCmdExe) : null;

        }

        #endregion
    }
}
