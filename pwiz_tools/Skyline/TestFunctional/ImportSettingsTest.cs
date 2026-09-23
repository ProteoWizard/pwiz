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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests Tools > Options > Miscellaneous > Import Settings, which replaces the settings
    /// with those of another installed Skyline.
    /// </summary>
    [TestClass]
    public class ImportSettingsTest : AbstractFunctionalTest
    {
        private const string OWN_INSTALLATION_ID = @"own-installation";
        private const string OTHER_INSTALLATION_ID = @"other-installation";
        private const string UNINSTALL_COMMAND = @"uninstall the other installation";
        private const string TOOL_TITLE = @"Imported Tool";
        private const string TOOL_FOLDER = @"ImportedTool";
        private const string TOOL_FILE = @"tool.bat";
        private const int OTHER_ANNOTATION_COLOR = 7;

        private string _toolsDirectory;

        [TestMethod]
        public void TestImportSettings()
        {
            TestFilesZip = @"TestFunctional\ImportSettingsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var provider = Settings.Default.UserConfigProvider;
            string originalConfigFolder = provider.ConfigFolder;
            _toolsDirectory = ToolDescriptionHelpers.GetToolsDirectory();
            DirectoryEx.SafeDelete(_toolsDirectory);
            try
            {
                var other = WriteOtherInstallation(provider);
                // Also an installation that is no longer in Programs and Features, whose
                // settings can be imported but which cannot be uninstalled
                var stale = new SkylineInstallation
                {
                    ProductName = @"Skyline-daily",
                    Version = @"25.1.1.100",
                    ExecutableFolder = Path.GetDirectoryName(other.ExecutableFolder),
                    UserConfigFile = other.UserConfigFile
                };
                WriteOwnSettings(provider, TestFilesDir.GetTestPath(@"Own"));

                TestImportKeepingOwnIdentity(other, stale);
                TestSourceChangeDetection(other);
                TestImportWithUninstall(other);
            }
            finally
            {
                provider.ConfigFolder = originalConfigFolder;
                DirectoryEx.SafeDelete(_toolsDirectory);
            }
        }

        /// <summary>
        /// The default import: the other installation's settings replace this one's, its tools
        /// are copied into this installation's Tools folder, this installation keeps its own id,
        /// and, on request, a copy of the imported file is kept so that later changes to it can
        /// be noticed.
        /// </summary>
        private void TestImportKeepingOwnIdentity(SkylineInstallation other, SkylineInstallation stale)
        {
            var toolsOptions = ShowToolOptions(new[] { other, stale }, out var uninstallsRun);
            var importDlg = ShowDialog<ImportSettingsDlg>(toolsOptions.ImportSettings);
            RunUI(() =>
            {
                Assert.AreEqual(2, importDlg.Installations.Count);
                Assert.AreSame(other, importDlg.SelectedInstallation);
                Assert.IsTrue(importDlg.UninstallEnabled);
                Assert.IsTrue(importDlg.TrackChangesEnabled);

                // Nothing to uninstall for an installation Programs and Features no longer lists
                importDlg.SelectedInstallation = stale;
                Assert.IsFalse(importDlg.UninstallEnabled);
                Assert.IsTrue(importDlg.TrackChangesEnabled);

                importDlg.SelectedInstallation = other;
                importDlg.TrackChanges = true;
            });
            OkDialog(importDlg, importDlg.OkDialog);
            WaitForImport(toolsOptions);
            OkDialog(toolsOptions, toolsOptions.OkDialog);

            Assert.AreEqual(0, uninstallsRun.Count);
            Assert.AreEqual(OTHER_ANNOTATION_COLOR, Settings.Default.AnnotationColor);
            Assert.AreEqual(OWN_INSTALLATION_ID, Settings.Default.InstallationId);
            Assert.AreEqual(OWN_INSTALLATION_ID, ReadSavedInstallationId());

            // The tool now lives under this installation's Tools folder, and the settings say so
            var tool = Settings.Default.ToolList.Single(t => t.Title == TOOL_TITLE);
            string expectedToolDir = Path.Combine(_toolsDirectory, TOOL_FOLDER);
            Assert.AreEqual(expectedToolDir, tool.ToolDirPath);
            AssertEx.FileExists(Path.Combine(expectedToolDir, TOOL_FILE));

            // Tracking: the source is remembered, along with a copy of what it held
            Assert.AreEqual(other.UserConfigFile, Settings.Default.ImportedSettingsPath);
            string baseConfigFile = SettingsImporter.GetBaseConfigPath(Settings.Default.SettingsFilePath);
            AssertEx.FileExists(baseConfigFile);
            Assert.IsTrue(File.ReadAllBytes(baseConfigFile).SequenceEqual(File.ReadAllBytes(other.UserConfigFile)));
        }

        /// <summary>
        /// The startup check notices when the tracked file changes, and does not when it has not.
        /// </summary>
        private void TestSourceChangeDetection(SkylineInstallation other)
        {
            var updater = new ImportedSettingsUpdater();
            Assert.IsTrue(updater.IsTracking);
            Assert.AreEqual(other.UserConfigFile, updater.SourcePath);
            Assert.IsFalse(updater.HasSourceChanged());
            updater.UpdateIfChanged();

            File.AppendAllText(other.UserConfigFile, @"<!-- changed since the import -->");
            Assert.IsTrue(updater.HasSourceChanged());
            // The merge is not written yet, so this only has to leave things as they are
            updater.UpdateIfChanged();
            Assert.IsTrue(updater.HasSourceChanged());
        }

        /// <summary>
        /// Importing and uninstalling the other installation: this installation takes over the
        /// other's id, the uninstall command is run, and there is no source left to track.
        /// </summary>
        private void TestImportWithUninstall(SkylineInstallation other)
        {
            var toolsOptions = ShowToolOptions(new[] { other }, out var uninstallsRun);
            var importDlg = ShowDialog<ImportSettingsDlg>(toolsOptions.ImportSettings);
            RunUI(() =>
            {
                importDlg.TrackChanges = true;
                importDlg.UninstallSelected = true;
                // Nothing to keep up with once the source is gone
                Assert.IsFalse(importDlg.TrackChangesEnabled);
                Assert.IsFalse(importDlg.TrackChanges);
            });
            OkDialog(importDlg, importDlg.OkDialog);
            WaitForImport(toolsOptions);
            OkDialog(toolsOptions, toolsOptions.OkDialog);

            Assert.AreEqual(1, uninstallsRun.Count);
            Assert.AreEqual(UNINSTALL_COMMAND, uninstallsRun[0]);
            Assert.AreEqual(OTHER_INSTALLATION_ID, Settings.Default.InstallationId);
            Assert.AreEqual(OTHER_INSTALLATION_ID, ReadSavedInstallationId());
            Assert.AreEqual(string.Empty, Settings.Default.ImportedSettingsPath);
            Assert.IsFalse(File.Exists(SettingsImporter.GetBaseConfigPath(Settings.Default.SettingsFilePath)));
        }

        private ToolOptionsUI ShowToolOptions(SkylineInstallation[] installations, out List<string> uninstallsRun)
        {
            var commandsRun = new List<string>();
            uninstallsRun = commandsRun;
            var toolsOptions = ShowDialog<ToolOptionsUI>(SkylineWindow.ShowToolOptionsUI);
            RunUI(() =>
            {
                toolsOptions.SelectedTab = ToolOptionsUI.TABS.Miscellaneous;
                toolsOptions.FindInstallations = () => installations;
                toolsOptions.RunUninstall = commandsRun.Add;
            });
            return toolsOptions;
        }

        /// <summary>
        /// The import pumps messages while it runs, so dismissing the dialog is not the end of
        /// it. The options dialog says when it is done.
        /// </summary>
        private static void WaitForImport(ToolOptionsUI toolsOptions)
        {
            WaitForConditionUI(() => !toolsOptions.IsImportingSettings);
        }

        /// <summary>
        /// Writes the settings file of the other installation, along with the external tool it
        /// names under its own Tools folder, by pointing the settings provider at that folder
        /// for the duration.
        /// </summary>
        private SkylineInstallation WriteOtherInstallation(UserConfigSettingsProvider provider)
        {
            string otherFolder = TestFilesDir.GetTestPath(@"Other");
            string toolDir = Path.Combine(otherFolder, @"Tools", TOOL_FOLDER);
            Directory.CreateDirectory(toolDir);
            string toolPath = Path.Combine(toolDir, TOOL_FILE);
            File.WriteAllText(toolPath, @"@echo off");

            provider.ConfigFolder = otherFolder;
            Settings.Default.InstallationId = OTHER_INSTALLATION_ID;
            Settings.Default.AnnotationColor = OTHER_ANNOTATION_COLOR;
            var tool = new ToolDescription(TOOL_TITLE, toolPath, string.Empty) { ToolDirPath = toolDir };
            Settings.Default.ToolList = ToolList.CopyTools(new[] { tool });
            Settings.Default.Save();

            return new SkylineInstallation
            {
                ProductName = @"Skyline-daily",
                Version = @"26.1.1.209",
                ExecutableFolder = otherFolder,
                UserConfigFile = provider.ConfigFilePath,
                IsCurrentlyInstalled = true,
                UninstallCommand = UNINSTALL_COMMAND
            };
        }

        private static void WriteOwnSettings(UserConfigSettingsProvider provider, string ownFolder)
        {
            provider.ConfigFolder = ownFolder;
            Settings.Default.InstallationId = OWN_INSTALLATION_ID;
            Settings.Default.AnnotationColor = 0;
            Settings.Default.ToolList = new ToolList();
            Settings.Default.Save();
        }

        /// <summary>
        /// The installation id as written to this installation's settings file, which is what
        /// the next run would read.
        /// </summary>
        private static string ReadSavedInstallationId()
        {
            var document = XDocument.Load(Settings.Default.SettingsFilePath);
            return document.Descendants(@"setting")
                .Where(el => (string) el.Attribute(@"name") == @"InstallationId")
                .Select(el => el.Element(@"value")?.Value)
                .FirstOrDefault();
        }
    }
}
