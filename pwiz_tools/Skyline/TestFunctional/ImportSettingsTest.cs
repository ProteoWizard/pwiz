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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests Tools > Options > Miscellaneous > Import Settings, which replaces the settings
    /// with those of another installed Skyline, and the import at the first start after an
    /// installation (<see cref="FirstLaunchImport"/>).
    /// </summary>
    [TestClass]
    public class ImportSettingsTest : AbstractFunctionalTest
    {
        private const string OWN_INSTALLATION_ID = @"own-installation";
        private const string OTHER_INSTALLATION_ID = @"other-installation";
        private const string UNINSTALL_COMMAND = @"uninstall the other installation";
        private const string HANDOFF_UNINSTALL_COMMAND = @"uninstall the installing Skyline";
        private const string TOOL_TITLE = @"Imported Tool";
        private const string TOOL_FOLDER = @"ImportedTool";
        private const string TOOL_FILE = @"tool.bat";
        private const int OTHER_ANNOTATION_COLOR = 7;
        private const int OWN_ANNOTATION_COLOR = 2;
        private const int SOURCE_ANNOTATION_COLOR = 3;
        private const string OWN_TOOL_TITLE = @"Own Tool";
        private const string SOURCE_TOOL_TITLE = @"Tool Added To Source";
        private const string OWN_LIBRARY_DIRECTORY = @"C:\OwnLibraries";
        private const string SOURCE_LIBRARY_DIRECTORY = @"C:\SourceLibraries";

        private string _toolsDirectory;

        [TestMethod]
        public void TestImportSettings()
        {
            TestFilesZip = @"TestFunctional\ImportSettingsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            _toolsDirectory = ToolDescriptionHelpers.GetToolsDirectory();
            DirectoryEx.SafeDelete(_toolsDirectory);
            try
            {
                var other = WriteOtherInstallation();
                // Also an installation that is no longer in Programs and Features, whose
                // settings can be imported but which cannot be uninstalled
                var stale = new SkylineInstallation
                {
                    ProductName = @"ExampleProductName",
                    Version = @"25.1.1.100",
                    ExecutableFolder = Path.GetDirectoryName(other.ExecutableFolder),
                    UserConfigFile = other.UserConfigFile
                };
                WriteOwnSettings();

                TestImportReplacesSettingsAndCopiesTools(other, stale);
                TestCanceledImportIsUndone(other);
                TestAdminChangesAreMergedIn(other);
                TestImportAndUninstallTakesOverTheOtherInstallation(other);
                TestInstallingSkylineIsImportedSilently(other);
                TestOlderInstallationsAreOfferedOnce(other);
            }
            finally
            {
                DirectoryEx.SafeDelete(_toolsDirectory);
                Registry.CurrentUser.DeleteSubKeyTree(HandoffKeyPath, false);
            }
        }

        /// <summary>
        /// A registry key of this test's own, standing in for the one an older Skyline writes
        /// before it starts the installer of this one.
        /// </summary>
        private static string HandoffKeyPath => @"Software\MacCossLabUW\" + nameof(ImportSettingsTest);

        /// <summary>
        /// The first start after an older Skyline installed this one: the settings file it named
        /// in the registry is imported without asking, this installation takes over its id, the
        /// uninstall command it recorded in that file is run, and the registry value is used up.
        /// </summary>
        private void TestInstallingSkylineIsImportedSilently(SkylineInstallation other)
        {
            var handoffConfigFile = TestFilesDir.GetTestPath(@"handoff.user.config");
            var document = XDocument.Load(other.UserConfigFile);
            document.Descendants(@"pwiz.Skyline.Properties.Settings").First().Add(
                new XElement(@"setting",
                    new XAttribute(@"name", SettingsImporter.UNINSTALL_COMMAND_SETTING),
                    new XAttribute(@"serializeAs", @"String"),
                    new XElement(@"value", HANDOFF_UNINSTALL_COMMAND)));
            document.Save(handoffConfigFile);
            using (var key = Registry.CurrentUser.CreateSubKey(HandoffKeyPath))
                key.SetValue(FirstLaunchImport.IMPORT_SETTINGS_FROM, handoffConfigFile);

            var uninstallsRun = new List<string>();
            var firstLaunchImport = new FirstLaunchImport
            {
                HandoffKeyPath = HandoffKeyPath,
                FindInstallations = () => throw new AssertFailedException(@"Nothing to look for after a handoff"),
                RunUninstall = uninstallsRun.Add
            };
            RunUI(() =>
            {
                Settings.Default.InstallationId = OWN_INSTALLATION_ID;
                Settings.Default.AnnotationColor = OWN_ANNOTATION_COLOR;
                Settings.Default.CheckedForSettingsToImport = false;
                firstLaunchImport.Run(SkylineWindow);
            });

            Assert.AreEqual(ReadAnnotationColor(handoffConfigFile), Settings.Default.AnnotationColor);
            Assert.AreEqual(OTHER_INSTALLATION_ID, Settings.Default.InstallationId);
            CollectionAssert.AreEqual(new[] { HANDOFF_UNINSTALL_COMMAND }, uninstallsRun);
            Assert.IsTrue(Settings.Default.CheckedForSettingsToImport);
            using (var key = Registry.CurrentUser.OpenSubKey(HandoffKeyPath))
                Assert.IsNull(key?.GetValue(FirstLaunchImport.IMPORT_SETTINGS_FROM));
        }

        /// <summary>
        /// The first start with no handoff offers the older installations found, in the Import
        /// Settings dialog. A later start offers nothing, even when the user chose nothing.
        /// </summary>
        private void TestOlderInstallationsAreOfferedOnce(SkylineInstallation other)
        {
            int searches = 0;
            var firstLaunchImport = new FirstLaunchImport
            {
                HandoffKeyPath = HandoffKeyPath,
                FindInstallations = () =>
                {
                    searches++;
                    return new[] { other };
                },
                RunUninstall = command => throw new AssertFailedException(@"Nothing was to be uninstalled")
            };
            RunUI(() =>
            {
                Settings.Default.AnnotationColor = OWN_ANNOTATION_COLOR;
                Settings.Default.CheckedForSettingsToImport = false;
            });
            RunDlg<ImportSettingsDlg>(() => firstLaunchImport.Run(SkylineWindow), importDlg =>
            {
                Assert.AreSame(other, importDlg.SelectedInstallation);
                importDlg.OkDialog();
            });
            Assert.AreEqual(ReadAnnotationColor(other.UserConfigFile), Settings.Default.AnnotationColor);
            Assert.IsTrue(Settings.Default.CheckedForSettingsToImport);

            RunUI(() => firstLaunchImport.Run(SkylineWindow));
            Assert.AreEqual(1, searches);
        }

        /// <summary>
        /// The default import: the other installation's settings replace this one's, its tools
        /// are copied into this installation's Tools folder, and this installation keeps its own
        /// id.
        /// </summary>
        private void TestImportReplacesSettingsAndCopiesTools(SkylineInstallation other, SkylineInstallation stale)
        {
            var uninstallsRun = ImportFromToolOptions(new[] { other, stale }, importDlg =>
            {
                Assert.AreEqual(2, importDlg.Installations.Count);
                Assert.AreSame(other, importDlg.SelectedInstallation);
                Assert.IsTrue(importDlg.UninstallEnabled);

                // Nothing to uninstall for an installation Programs and Features no longer lists
                importDlg.SelectedInstallation = stale;
                Assert.IsFalse(importDlg.UninstallEnabled);

                importDlg.SelectedInstallation = other;
            });

            Assert.AreEqual(0, uninstallsRun.Count);
            Assert.AreEqual(OTHER_ANNOTATION_COLOR, Settings.Default.AnnotationColor);
            Assert.AreEqual(OWN_INSTALLATION_ID, Settings.Default.InstallationId);
            Assert.AreEqual(OWN_INSTALLATION_ID, ReadSavedInstallationId());

            // The tool now lives under this installation's Tools folder, and the settings say so
            var tool = Settings.Default.ToolList.Single(t => t.Title == TOOL_TITLE);
            string expectedToolDir = Path.Combine(_toolsDirectory, TOOL_FOLDER);
            Assert.AreEqual(expectedToolDir, tool.ToolDirPath);
            AssertEx.FileExists(Path.Combine(expectedToolDir, TOOL_FILE));
        }

        /// <summary>
        /// An import canceled or failed partway is undone, down to a change made in this session
        /// and not yet saved.
        /// </summary>
        private void TestCanceledImportIsUndone(SkylineInstallation other)
        {
            RunUI(() =>
            {
                Settings.Default.AnnotationColor = OWN_ANNOTATION_COLOR;
                var importer = new SettingsImporter(other.UserConfigFile);
                importer.ImportSettingsFile();
                Assert.AreEqual(OTHER_ANNOTATION_COLOR, Settings.Default.AnnotationColor);
                importer.RevertImport();
                Assert.AreEqual(OWN_ANNOTATION_COLOR, Settings.Default.AnnotationColor);

                // Back to what was imported, for the merge that follows
                Settings.Default.AnnotationColor = OTHER_ANNOTATION_COLOR;
            });
        }

        /// <summary>
        /// What an ordinary user of an installation made for all users gets at startup, with the
        /// other installation's user.config standing in for the administrator's shared one, and
        /// the settings just imported from it for the user's own. Changes the administrator made
        /// since the last merge are brought across without undoing the user's: a setting changed
        /// only by the administrator takes the new value, one changed by both keeps the user's,
        /// the tool list gains the tools added on each side, and the user keeps their own
        /// installation id.
        /// </summary>
        private void TestAdminChangesAreMergedIn(SkylineInstallation other)
        {
            var baseConfigFile = TestFilesDir.GetTestPath(SharedSettingsMerger.SHARED_BASE_CONFIG_FILE_NAME);
            File.Copy(other.UserConfigFile, baseConfigFile, true);
            var merger = new SharedSettingsMerger
            {
                SourcePath = other.UserConfigFile,
                BaseConfigFilePath = baseConfigFile
            };
            Assert.IsFalse(merger.HasSourceChanged());

            RunUI(() =>
            {
                Settings.Default.LibraryDirectory = OWN_LIBRARY_DIRECTORY;
                Settings.Default.ToolList = ToolList.CopyTools(Settings.Default.ToolList.Append(
                    new ToolDescription(OWN_TOOL_TITLE, @"own.exe", string.Empty)));

                var source = new Settings();
                source.UserConfigProvider.ConfigFilePath = other.UserConfigFile;
                source.AnnotationColor = SOURCE_ANNOTATION_COLOR;
                source.LibraryDirectory = SOURCE_LIBRARY_DIRECTORY;
                source.ToolList = ToolList.CopyTools(source.ToolList.Append(
                    new ToolDescription(SOURCE_TOOL_TITLE, @"source.exe", string.Empty)));
                source.Save();
            });
            Assert.IsTrue(merger.HasSourceChanged());

            RunUI(merger.MergeIfChanged);

            Assert.AreEqual(SOURCE_ANNOTATION_COLOR, Settings.Default.AnnotationColor);
            Assert.AreEqual(OWN_LIBRARY_DIRECTORY, Settings.Default.LibraryDirectory);
            Assert.AreEqual(OWN_INSTALLATION_ID, Settings.Default.InstallationId);
            CollectionAssert.AreEquivalent(new[] { TOOL_TITLE, OWN_TOOL_TITLE, SOURCE_TOOL_TITLE },
                Settings.Default.ToolList.Select(tool => tool.Title).ToArray());
            // The copy brought into this installation's Tools folder is still the one used
            Assert.AreEqual(Path.Combine(_toolsDirectory, TOOL_FOLDER),
                Settings.Default.ToolList.Single(tool => tool.Title == TOOL_TITLE).ToolDirPath);
            // The base copy was refreshed, so the next start has nothing to bring across
            Assert.IsFalse(merger.HasSourceChanged());
        }

        /// <summary>
        /// Importing and uninstalling the other installation: this installation takes over the
        /// other's id, and the uninstall command is run.
        /// </summary>
        private void TestImportAndUninstallTakesOverTheOtherInstallation(SkylineInstallation other)
        {
            var uninstallsRun = ImportFromToolOptions(new[] { other },
                importDlg => importDlg.UninstallSelected = true);

            Assert.AreEqual(1, uninstallsRun.Count);
            Assert.AreEqual(UNINSTALL_COMMAND, uninstallsRun[0]);
            Assert.AreEqual(OTHER_INSTALLATION_ID, Settings.Default.InstallationId);
            Assert.AreEqual(OTHER_INSTALLATION_ID, ReadSavedInstallationId());
        }

        /// <summary>
        /// Clicks Import Settings in Tools > Options, offering the given installations, makes the
        /// choices in the Import Settings dialog, which runs on the UI thread, and accepts both
        /// dialogs. Returns once the whole import is done. The uninstall commands it would have
        /// run are returned instead of being run.
        /// </summary>
        private List<string> ImportFromToolOptions(SkylineInstallation[] installations, Action<ImportSettingsDlg> chooseImport)
        {
            var uninstallsRun = new List<string>();
            RunLongDlg<ToolOptionsUI>(SkylineWindow.ShowToolOptionsUI, toolsOptions =>
            {
                RunUI(() =>
                {
                    toolsOptions.SelectedTab = ToolOptionsUI.TABS.Miscellaneous;
                    toolsOptions.FindInstallations = () => installations;
                    toolsOptions.RunUninstall = uninstallsRun.Add;
                });
                RunDlg<ImportSettingsDlg>(toolsOptions.ImportSettings, importDlg =>
                {
                    chooseImport(importDlg);
                    importDlg.OkDialog();
                });
            }, toolsOptions => toolsOptions.OkDialog());
            return uninstallsRun;
        }

        /// <summary>
        /// Writes the settings file of the other installation, along with the external tool it
        /// names under its own Tools folder.
        /// </summary>
        private SkylineInstallation WriteOtherInstallation()
        {
            string otherFolder = TestFilesDir.GetTestPath(@"Other");
            string toolDir = Path.Combine(otherFolder, @"Tools", TOOL_FOLDER);
            Directory.CreateDirectory(toolDir);
            string toolPath = Path.Combine(toolDir, TOOL_FILE);
            File.WriteAllText(toolPath, @"@echo off");

            var otherSettings = new Settings();
            otherSettings.UserConfigProvider.ConfigFilePath = Path.Combine(otherFolder, UserConfigSettingsProvider.CONFIG_FILE_NAME);
            otherSettings.InstallationId = OTHER_INSTALLATION_ID;
            otherSettings.AnnotationColor = OTHER_ANNOTATION_COLOR;
            var tool = new ToolDescription(TOOL_TITLE, toolPath, string.Empty) { ToolDirPath = toolDir };
            otherSettings.ToolList = ToolList.CopyTools(new[] { tool });
            otherSettings.Save();

            return new SkylineInstallation
            {
                ProductName = @"ExampleProductName",
                Version = @"26.1.1.209",
                ExecutableFolder = otherFolder,
                UserConfigFile = otherSettings.SettingsFilePath,
                IsCurrentlyInstalled = true,
                UninstallCommand = UNINSTALL_COMMAND
            };
        }

        private static void WriteOwnSettings()
        {
            Settings.Default.InstallationId = OWN_INSTALLATION_ID;
            Settings.Default.AnnotationColor = 0;
            Settings.Default.ToolList = new ToolList();
            Settings.Default.Save();
        }

        /// <summary>
        /// The annotation color in a settings file, which the steps above leave different from
        /// <see cref="OWN_ANNOTATION_COLOR"/>, so that taking it on shows the file was imported.
        /// </summary>
        private static int ReadAnnotationColor(string configFile)
        {
            var settings = new Settings();
            settings.UserConfigProvider.ConfigFilePath = configFile;
            Assert.AreNotEqual(OWN_ANNOTATION_COLOR, settings.AnnotationColor);
            return settings.AnnotationColor;
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
