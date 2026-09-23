/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using System.Diagnostics;
using System.IO;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Replaces the running program's user settings with those of another Skyline installation.
    ///
    /// The settings file is taken whole, and then the parts of it that must not simply be copied
    /// are put right: the external tools it names live under the other installation's Tools
    /// folder and are brought across, and the installation id stays this program's own unless
    /// the other installation is being uninstalled, in which case this one carries on its
    /// identity.
    /// </summary>
    public class SettingsImporter
    {
        /// <summary>
        /// Name of the copy of the imported file kept beside user.config when the import is to
        /// be kept up to date. <see cref="ImportedSettingsUpdater"/> compares the source
        /// against it to see whether anything changed.
        /// </summary>
        public const string BASE_CONFIG_FILE_NAME = @"base.user.config";

        private const string INSTALLING_SUFFIX = @"_installing";

        public static string GetBaseConfigPath(string configFilePath)
        {
            return Path.Combine(Path.GetDirectoryName(configFilePath) ?? string.Empty, BASE_CONFIG_FILE_NAME);
        }

        /// <summary>
        /// Runs a command line the way Programs and Features would, through the shell so that an
        /// uninstaller needing elevation gets to ask for it.
        /// </summary>
        public static void RunCommand(string commandLine)
        {
            SplitCommandLine(commandLine, out var fileName, out var arguments);
            Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = true });
        }

        public SettingsImporter(string sourceConfigFile)
        {
            SourceConfigFile = sourceConfigFile;
            RunUninstall = RunCommand;
        }

        public string SourceConfigFile { get; set; }

        /// <summary>
        /// Whether this program keeps its own installation id, which is the default, rather
        /// than taking over the one in the imported file. The id identifies an installation
        /// in usage and error reports, so it should only be taken over when the other
        /// installation is going away.
        /// </summary>
        public bool KeepInstallationId { get; set; } = true;

        /// <summary>
        /// Whether to remember where the settings came from, so that later changes to that file
        /// can be brought across at startup.
        /// </summary>
        public bool TrackChanges { get; set; }

        /// <summary>
        /// Command to run after importing to uninstall the other installation, or null to leave
        /// it in place.
        /// </summary>
        public string UninstallCommand { get; set; }

        /// <summary>
        /// How <see cref="UninstallCommand"/> gets run. A test replaces this to see the command
        /// without running anything.
        /// </summary>
        public Action<string> RunUninstall { get; set; }

        /// <summary>
        /// The whole import, for a caller with no UI to show progress in. A caller with one runs
        /// the three steps itself, with <see cref="CopyTools"/> in the background and the other
        /// two on the UI thread: reloading the settings raises PropertyChanged for every setting,
        /// and the graphs that listen for those expect to hear about them on the UI thread.
        /// </summary>
        public void Import(ILongWaitBroker broker)
        {
            ImportSettingsFile();
            CopyTools(broker);
            FinishImport();
        }

        /// <summary>
        /// Replaces the settings file with the source and reads it in, keeping this program's
        /// own installation id unless the source's is to be taken over.
        /// </summary>
        public void ImportSettingsFile()
        {
            var settings = Settings.Default;
            string ownInstallationId = settings.InstallationId;
            string configFile = settings.SettingsFilePath;
            var configFolder = Path.GetDirectoryName(configFile);
            if (!string.IsNullOrEmpty(configFolder))
                Directory.CreateDirectory(configFolder);
            File.Copy(SourceConfigFile, configFile, true);
            settings.Reload();

            // An imported file with no id of its own has nothing to take over.
            if (!string.IsNullOrEmpty(ownInstallationId) &&
                (KeepInstallationId || string.IsNullOrEmpty(settings.InstallationId)))
            {
                settings.InstallationId = ownInstallationId;
            }
        }

        /// <summary>
        /// Records where the settings came from, saves everything, and uninstalls the other
        /// installation when that was asked for.
        /// </summary>
        public void FinishImport()
        {
            var settings = Settings.Default;
            var baseConfigFile = GetBaseConfigPath(settings.SettingsFilePath);
            if (TrackChanges)
            {
                File.Copy(SourceConfigFile, baseConfigFile, true);
                settings.ImportedSettingsPath = SourceConfigFile;
            }
            else
            {
                // Whatever the imported file itself was tracking is not this program's business.
                settings.ImportedSettingsPath = string.Empty;
                FileEx.SafeDelete(baseConfigFile, true);
            }
            settings.Save();

            if (!string.IsNullOrEmpty(UninstallCommand))
                RunUninstall(UninstallCommand);
        }

        /// <summary>
        /// Brings the external tools the current settings name into this installation's Tools
        /// folder, and points the settings at the copies. Tools already there are left alone.
        /// Public because the ClickOnce migration at startup copies the settings file itself,
        /// before anything has read the settings, and then needs just this part.
        /// </summary>
        public void CopyTools(ILongWaitBroker broker)
        {
            var toolsDirectory = ToolDescriptionHelpers.GetToolsDirectory();
            var toolList = Settings.Default.ToolList;
            var searchToolList = Settings.Default.SearchToolList;
            int numTools = toolList.Count + searchToolList.Count;
            if (numTools == 0)
                return;
            if (broker != null)
                broker.Message = SkylineResources.Program_Main_Copying_external_tools_from_a_previous_installation;
            int increment = 100 / (numTools + 1);

            bool canceled = false;
            foreach (var tool in toolList)
            {
                string oldDir = tool.ToolDirPath;
                string newDir = CopyToolFolder(oldDir, toolsDirectory);
                if (newDir != null)
                {
                    tool.ToolDirPath = newDir;
                    tool.ArgsCollectorDllPath = tool.ArgsCollectorDllPath?.Replace(oldDir, newDir);
                }
                if (!AdvanceProgress(broker, increment))
                {
                    canceled = true;
                    break;
                }
            }
            // Assigning the lists is what marks them changed, so that the new paths get saved.
            Settings.Default.ToolList = ToolList.CopyTools(toolList);
            if (canceled)
                return;

            foreach (var tool in searchToolList)
            {
                // Only a tool Skyline installed itself is under a Tools folder. One the user
                // pointed at stays where the user put it.
                string oldDir = tool.InstallPath;
                string newDir = tool.AutoInstalled ? CopyToolFolder(oldDir, toolsDirectory) : null;
                if (newDir != null)
                {
                    tool.InstallPath = newDir;
                    tool.Path = tool.Path?.Replace(oldDir, newDir);
                }
                if (!AdvanceProgress(broker, increment))
                    break;
            }
            Settings.Default.SearchToolList = SearchToolList.CopyTools(searchToolList);
        }

        /// <summary>
        /// The folder the tool was copied to, or null when there was nothing to copy: no folder,
        /// or one already under the Tools folder. A tool whose folder is already there is not
        /// overwritten, since it may be the same tool at a newer version.
        /// </summary>
        private static string CopyToolFolder(string toolDirPath, string toolsDirectory)
        {
            if (string.IsNullOrEmpty(toolDirPath) || !Directory.Exists(toolDirPath))
                return null;
            string folderName = Path.GetFileName(
                toolDirPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(folderName))
                return null;
            string newDir = Path.Combine(toolsDirectory, folderName);
            if (AreSameFolder(toolDirPath, newDir))
                return null;
            if (!Directory.Exists(newDir))
            {
                // Copy beside the destination and move into place, so that a copy which fails
                // partway through does not leave what looks like an installed tool.
                string tempDir = newDir + INSTALLING_SUFFIX;
                DirectoryEx.SafeDelete(tempDir);
                Directory.CreateDirectory(toolsDirectory);
                DirectoryEx.DirectoryCopy(toolDirPath, tempDir, true);
                Directory.Move(tempDir, newDir);
            }
            return newDir;
        }

        private static bool AreSameFolder(string folder1, string folder2)
        {
            return string.Equals(
                Path.GetFullPath(folder1).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(folder2).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// False when the user canceled.
        /// </summary>
        private static bool AdvanceProgress(ILongWaitBroker broker, int increment)
        {
            if (broker == null)
                return true;
            if (broker.IsCanceled)
                return false;
            broker.ProgressValue += increment;
            return true;
        }

        /// <summary>
        /// Separates the executable from its arguments in a registered uninstall command, which
        /// is a full command line such as
        /// <c>"C:\Program Files\Skyline\unins000.exe" /SILENT</c> or
        /// <c>rundll32.exe dfshim.dll,ShArpMaintain Skyline-daily.application, Culture=neutral, ...</c>.
        /// </summary>
        private static void SplitCommandLine(string commandLine, out string fileName, out string arguments)
        {
            commandLine = commandLine.Trim();
            int fileNameEnd;
            if (commandLine.StartsWith(@""""))
            {
                int closingQuote = commandLine.IndexOf('"', 1);
                fileNameEnd = closingQuote < 0 ? commandLine.Length : closingQuote + 1;
            }
            else
            {
                int space = commandLine.IndexOf(' ');
                fileNameEnd = space < 0 ? commandLine.Length : space;
            }
            fileName = commandLine.Substring(0, fileNameEnd).Trim('"');
            arguments = commandLine.Substring(fileNameEnd).Trim();
        }
    }
}
