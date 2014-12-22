//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2014 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Automation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestStack.White;
using TestStack.White.Factory;
using TestStack.White.Configuration;
using TestStack.White.UIItems;
using TestStack.White.UIItems.WindowItems;
using TestStack.White.UIItems.WindowStripControls;
using TestStack.White.UIItems.MenuItems;
using TestStack.White.UIItems.TreeItems;
using TestStack.White.UIItems.TableItems;
using TestStack.White.UIItems.ListBoxItems;
using TestStack.White.UIItems.Finders;
using TestStack.White.UIItems.Container;
using TestStack.White.UIItems.Actions;
using TestStack.White.UIItems.Custom;

namespace Test
{
    using AppRunner = Action<Application, Stack<Window>>;

    [TestClass]
    public class ImportTests
    {
        public TestContext TestContext { get; set; }
        public bool CloseAppOnError { get { return false; } }

        #region Test initialization and cleanup
        public Application Application { get { return TestContext.Properties["Application"] as Application; } set { TestContext.Properties["Application"] = value; } }
        public string TestOutputSubdirectory { get { return (string) TestContext.Properties["TestOutputSubdirectory"]; } set { TestContext.Properties["TestOutputSubdirectory"] = value; } }

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            testContext.Properties["Application"] = null;
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            try
            {
                //Application.Attach("IDPicker").Close();
            }
            catch(WhiteException e)
            {
                if (!e.Message.Contains("Could not find process"))
                    throw e;
            }
        }

        [TestInitialize]
        public void TestInitialize()
        {
            TestOutputSubdirectory = TestContext.TestName;
            Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));
        }
        #endregion

        public static void WaitForReady(TextBox statusTextBox, int maxMillisecondsToWait = 5000)
        {
            const int msToWait = 200;

            // FIXME: depends on English text
            for (int i = 0; i < maxMillisecondsToWait; i += msToWait)
            {
                if (statusTextBox.Text == "Ready")
                    return;

                Thread.Sleep(msToWait);
            }

            throw new TimeoutException("timeout waiting for status to return to 'Ready'");
        }

        [TestMethod]
        public void OpenWithoutFileArguments()
        {
            TestContext.LaunchAppTest("IDPicker.exe", "--test-ui-layout",

            (app, windowStack) =>
            {
                var window = app.GetWindow(SearchCriteria.ByAutomationId("IDPickerForm"), InitializeOption.NoCache);
                windowStack.Push(window);

                var statusBar = window.Get<StatusStrip>();
                var statusText = statusBar.Get<TextBox>();
                WaitForReady(statusText);
            });
        }

        [TestMethod]
        public void PwizBindings()
        {
            TestContext.LaunchAppTest("IDPicker.exe", "--test", // closes automatically after calling a pwiz function to make sure pwiz bindings load properly

            (app, windowStack) =>
            {
            });
        }


        public class IDPickerAllSettings
        {
            public IDPickerAllSettings()
            {
                GeneralSettings = new IDPicker.Properties.Settings();
                GUISettings = new IDPicker.Properties.GUI.Settings();
            }

            public IDPicker.Properties.Settings GeneralSettings { get; private set; }
            public IDPicker.Properties.GUI.Settings GUISettings { get; private set; }
        }


        /// <summary>
        /// Get the values from the options menu of an existing IDPicker Application instance (the Settings are not directly accessible from a separate process)
        /// </summary>
        public IDPickerAllSettings GetSettings(Application app, Stack<Window> windowStack)
        {
            var settings = new IDPickerAllSettings();

            var window = app.GetWindow(SearchCriteria.ByAutomationId("IDPickerForm"), InitializeOption.NoCache);
            windowStack.Push(window);

            var menu = window.RawGet<MenuBar>(SearchCriteria.ByAutomationId("menuStrip1"), 2);
            //menu.MenuItemBy(SearchCriteria.ByAutomationId("toolsToolStripMenuItem"), SearchCriteria.ByAutomationId("optionsToolStripMenuItem")).Click();
            menu.MenuItem("Tools", "Options...").RaiseClickEvent(); // FIXME: not localized, but the AutomationIds aren't being set properly so the above line won't work

            var options = window.ModalWindow(SearchCriteria.ByAutomationId("DefaultSettingsManagerForm"), InitializeOption.WithCache);
            windowStack.Push(options);

            settings.GeneralSettings.DefaultMinSpectraPerDistinctMatch = Convert.ToInt32(options.Get<TextBox>("minSpectraPerMatchTextBox").Text);
            settings.GeneralSettings.DefaultMinSpectraPerDistinctPeptide = Convert.ToInt32(options.Get<TextBox>("minSpectraPerPeptideTextBox").Text);
            settings.GeneralSettings.DefaultMaxProteinGroupsPerPeptide = Convert.ToInt32(options.Get<TextBox>("maxProteinGroupsTextBox").Text);
            settings.GeneralSettings.DefaultMinSpectra = Convert.ToInt32(options.Get<TextBox>("minSpectraTextBox").Text);
            settings.GeneralSettings.DefaultMinDistinctPeptides = Convert.ToInt32(options.Get<TextBox>("minDistinctPeptidesTextBox").Text);
            settings.GeneralSettings.DefaultMinAdditionalPeptides = Convert.ToInt32(options.Get<TextBox>("minAdditionalPeptidesTextBox").Text);
            settings.GeneralSettings.DefaultMaxRank = Convert.ToInt32(options.Get<TextBox>("maxImportRankTextBox").Text);

            settings.GeneralSettings.DefaultMaxFDR = Convert.ToDouble(options.Get<ComboBox>("maxQValueComboBox").EditableText) / 100;
            settings.GeneralSettings.DefaultMaxImportFDR = Convert.ToDouble(options.Get<ComboBox>("maxImportFdrComboBox").EditableText) / 100;

            settings.GeneralSettings.DefaultDecoyPrefix = options.Get<TextBox>("defaultDecoyPrefixTextBox").Text;
            settings.GeneralSettings.DefaultIgnoreUnmappedPeptides = options.Get<CheckBox>("ignoreUnmappedPeptidesCheckBox").Checked;

            settings.GeneralSettings.DefaultGeneLevelFiltering = options.Get<CheckBox>("filterByGeneCheckBox").Checked;
            settings.GeneralSettings.DefaultChargeIsDistinct = options.Get<CheckBox>("chargeIsDistinctCheckBox").Checked;
            settings.GeneralSettings.DefaultAnalysisIsDistinct = options.Get<CheckBox>("analysisIsDistinctCheckBox").Checked;
            settings.GeneralSettings.DefaultModificationsAreDistinct = options.Get<CheckBox>("modificationsAreDistinctCheckbox").Checked;
            settings.GeneralSettings.DefaultModificationRoundToNearest = Convert.ToDecimal(options.Get<TextBox>("modificationRoundToMassTextBox").Text);

            //settings.GeneralSettings.FastaPaths.Clear(); settings.GeneralSettings.FastaPaths.AddRange(lbFastaPaths.Items.OfType<string>().ToArray());
            //settings.GeneralSettings.SourcePaths.Clear(); settings.GeneralSettings.SourcePaths.AddRange(lbSourcePaths.Items.OfType<string>().ToArray());

            settings.GeneralSettings.SourceExtensions = options.Get<TextBox>("sourceExtensionsTextBox").Text;

            settings.GUISettings.WarnAboutNonFixedDrive = options.Get<CheckBox>("nonFixedDriveWarningCheckBox").Checked;
            settings.GUISettings.WarnAboutNoGeneMetadata = options.Get<CheckBox>("embedGeneMetadataWarningCheckBox").Checked;

            options.Get<Button>("btnOk").RaiseClickEvent();
            windowStack.Pop();

            return settings;
        }

        public void SetSettings(Application app, Stack<Window> windowStack, IDPickerAllSettings settings)
        {
            var window = app.GetWindow(SearchCriteria.ByAutomationId("IDPickerForm"), InitializeOption.NoCache);
            windowStack.Push(window);

            var menu = window.RawGet<MenuBar>(SearchCriteria.ByAutomationId("menuStrip1"), 2);
            //menu.MenuItemBy(SearchCriteria.ByAutomationId("toolsToolStripMenuItem"), SearchCriteria.ByAutomationId("optionsToolStripMenuItem")).Click();
            menu.MenuItem("Tools", "Options...").Click(); // FIXME: not localized, but the AutomationIds aren't being set properly so the above line won't work

            var options = window.ModalWindow(SearchCriteria.ByAutomationId("DefaultSettingsManagerForm"), InitializeOption.WithCache);
            windowStack.Push(options);

            options.Get<TextBox>("minSpectraPerMatchTextBox").Text = settings.GeneralSettings.DefaultMinSpectraPerDistinctMatch.ToString();
            options.Get<TextBox>("minSpectraPerPeptideTextBox").Text = settings.GeneralSettings.DefaultMinSpectraPerDistinctPeptide.ToString();
            options.Get<TextBox>("maxProteinGroupsTextBox").Text = settings.GeneralSettings.DefaultMaxProteinGroupsPerPeptide.ToString();
            options.Get<TextBox>("minSpectraTextBox").Text = settings.GeneralSettings.DefaultMinSpectra.ToString();
            options.Get<TextBox>("minDistinctPeptidesTextBox").Text = settings.GeneralSettings.DefaultMinDistinctPeptides.ToString();
            options.Get<TextBox>("minAdditionalPeptidesTextBox").Text = settings.GeneralSettings.DefaultMinAdditionalPeptides.ToString();
            options.Get<TextBox>("maxImportRankTextBox").Text = settings.GeneralSettings.DefaultMaxRank.ToString();

            options.Get<ComboBox>("maxQValueComboBox").EditableText = (settings.GeneralSettings.DefaultMaxFDR * 100).ToString();
            options.Get<ComboBox>("maxImportFdrComboBox").EditableText = (settings.GeneralSettings.DefaultMaxImportFDR * 100).ToString();

            options.Get<TextBox>("defaultDecoyPrefixTextBox").Text = settings.GeneralSettings.DefaultDecoyPrefix;
            options.Get<CheckBox>("ignoreUnmappedPeptidesCheckBox").Checked = settings.GeneralSettings.DefaultIgnoreUnmappedPeptides;

            options.Get<CheckBox>("filterByGeneCheckBox").Checked = settings.GeneralSettings.DefaultGeneLevelFiltering;
            options.Get<CheckBox>("chargeIsDistinctCheckBox").Checked = settings.GeneralSettings.DefaultChargeIsDistinct;
            options.Get<CheckBox>("analysisIsDistinctCheckBox").Checked = settings.GeneralSettings.DefaultAnalysisIsDistinct;
            options.Get<CheckBox>("modificationsAreDistinctCheckbox").Checked = settings.GeneralSettings.DefaultModificationsAreDistinct;
            options.Get<TextBox>("modificationRoundToMassTextBox").Text = settings.GeneralSettings.DefaultModificationRoundToNearest.ToString();

            //settings.GeneralSettings.FastaPaths.Clear(); settings.GeneralSettings.FastaPaths.AddRange(lbFastaPaths.Items.OfType<string>().ToArray());
            //settings.GeneralSettings.SourcePaths.Clear(); settings.GeneralSettings.SourcePaths.AddRange(lbSourcePaths.Items.OfType<string>().ToArray());

            options.Get<TextBox>("sourceExtensionsTextBox").Text = settings.GeneralSettings.SourceExtensions;

            options.Get<CheckBox>("nonFixedDriveWarningCheckBox").Checked = settings.GUISettings.WarnAboutNonFixedDrive;
            options.Get<CheckBox>("embedGeneMetadataWarningCheckBox").Checked = settings.GUISettings.WarnAboutNoGeneMetadata;

            options.Get<Button>("btnOk").RaiseClickEvent();
            windowStack.Pop();
        }

        /// <summary>
        /// Launch IDPicker without any arguments and return the values from the options menu (the Settings are not directly accessible from a separate process)
        /// </summary>
        public IDPickerAllSettings GetSettings()
        {
            var settings = new IDPickerAllSettings();

            TestContext.LaunchAppTest("IDPicker.exe", "", (app, windowStack) => { settings = GetSettings(app, windowStack); });

            return settings;
        }

        /// <summary>
        /// Launch IDPicker without any arguments and set up the options menu according to the given settings parameter (the Settings are not directly accessible from a separate process)
        /// </summary>
        public void SetSettings(IDPickerAllSettings settings)
        {
            TestContext.LaunchAppTest("IDPicker.exe", "", (app, windowStack) => { SetSettings(app, windowStack, settings); });
        }

        public IDPickerAllSettings GetAndSetTestSettings(Application app, Stack<Window> windowStack)
        {
            var settings = new IDPickerAllSettings();
            SetSettings(app, windowStack, settings);
            return settings;
        }

        public IDPickerAllSettings GetAndSetTestSettings()
        {
            IDPickerAllSettings settings = null;
            TestContext.LaunchAppTest("IDPicker.exe", "", (app, windowStack) => { settings = GetAndSetTestSettings(app, windowStack); });
            return settings;
        }

        [TestMethod]
        public void ImportSingleFileOnOpen()
        {
            // get settings in a separate invocation because import starts immediately when a file is passed on the command-line
            var settings = GetAndSetTestSettings();

            // this lambda allow reusing of the testing code; we can change only UI parameters to create a specific AppRunner
            Func<AppRunner> createTestCase = () =>
            {
                return (app, windowStack) =>
                {
                    var window = app.GetWindow(SearchCriteria.ByAutomationId("IDPickerForm"), InitializeOption.NoCache);
                    var statusBar = window.Get<StatusStrip>();
                    var statusText = statusBar.Get<TextBox>();
                    windowStack.Push(window);

                    var progressForm = window.ModalWindow(SearchCriteria.ByAutomationId("ProgressForm"), InitializeOption.NoCache);
                    windowStack.Push(progressForm);

                    var importSettings = window.ModalWindow("Import Settings"); // TODO: don't use Text
                    windowStack.Push(importSettings);

                    var settingsTable = importSettings.Get<Table>("dataGridView");
                    Assert.IsNotNull(settingsTable);
                    Assert.AreEqual(1, settingsTable.Rows.Count);

                    var firstRow = settingsTable.Rows[0];
                    Assert.AreEqual(7, settingsTable.Rows[0].Cells.Count);
                    UnitTestExtensions.AssertSequenceEquals(new Object[] { "MyriMatch 2.2.140", "cow.protein.PRG2012-subset.fasta", "XXX_", "2", "0.1", "False", "MyriMatch optimized" }, settingsTable.Rows[0].Cells.Select(o => o.Value).ToArray());

                    // HACK: for some reason White's TableCell.Value property isn't sending keyboard input correctly;
                    // with this workaround, be careful while debugging around this code because the keyboard input might be sent to the debugger!
                    settingsTable.Rows[0].Cells[1].Click();
                    importSettings.Keyboard.Enter(TestContext.TestDataFilePath("cow.protein.PRG2012-subset.fasta"));

                    // TODO: add interface testing
                    // test qonverter settings
                    // test error conditions (bad values for max rank and max FDR score)
                    // test invalid decoy prefix
                    // test analysis parameters tree

                    var ok = importSettings.Get<Button>("okButton");
                    ok.Click();
                    windowStack.Pop();

                    while (!progressForm.IsClosed)
                        Thread.Sleep(500);

                    windowStack.Pop();

                    bool willEmbedGeneMetadata = settings.GUISettings.WarnAboutNoGeneMetadata;

                    // handle prompt for gene metadata embedding if necessary
                    if (settings.GUISettings.WarnAboutNoGeneMetadata)
                    {
                        var prompt = window.ModalWindow(SearchCriteria.ByAutomationId("EmbedGeneMetadataWarningForm"), InitializeOption.NoCache);
                        windowStack.Push(prompt);

                        prompt.Get<CheckBox>("doNotShowCheckBox").Click();
                        prompt.Get<Button>("embedButton").RaiseClickEvent();

                        while (!prompt.IsClosed)
                            Thread.Sleep(500);

                        // refresh settings
                        settings = GetSettings(app, windowStack);
                        Assert.AreEqual(false, settings.GUISettings.WarnAboutNoGeneMetadata);

                        // reset to original state
                        settings.GUISettings.WarnAboutNoGeneMetadata = true;
                        SetSettings(app, windowStack, settings);
                    }

                    WaitForReady(statusText);

                    window = app.GetWindow(SearchCriteria.ByAutomationId("IDPickerForm"), InitializeOption.NoCache);
                    var dockableForms = window.GetDockableForms();

                    var proteinTableForm = dockableForms.Single(o => o.Id == "ProteinTableForm");
                    if (willEmbedGeneMetadata)
                        Assert.AreEqual("Protein View: 9 clusters, 9 protein groups, 19 proteins, 0% protein FDR, 9 gene groups, 19 genes", proteinTableForm.Name);
                    else
                        Assert.AreEqual("Protein View: 9 clusters, 9 protein groups, 19 proteins, 0% protein FDR", proteinTableForm.Name);

                    var peptideTableForm = dockableForms.Single(o => o.Id == "PeptideTableForm");
                    Assert.AreEqual("Peptide View: 129 distinct peptides, 191 distinct matches", peptideTableForm.Name);

                    var spectrumTableForm = dockableForms.Single(o => o.Id == "SpectrumTableForm");
                    Assert.AreEqual("Spectrum View: 1 groups, 1 sources, 207 spectra", spectrumTableForm.Name);

                    var modificationTableForm = dockableForms.Single(o => o.Id == "ModificationTableForm");
                    Assert.AreEqual("Modification View: 82 modified spectra", modificationTableForm.Name);

                    var analysisTableForm = dockableForms.Single(o => o.Id == "AnalysisTableForm");
                    Assert.AreEqual("Analysis View", analysisTableForm.Name);

                    var filterHistoryForm = dockableForms.Single(o => o.Id == "FilterHistoryForm");
                    Assert.AreEqual("Filter History", filterHistoryForm.Name);
                };
            };

            // make sure embed gene metadata warning is turned on for first tests
            if (!settings.GUISettings.WarnAboutNoGeneMetadata)
            {
                settings.GUISettings.WarnAboutNoGeneMetadata = true;
                SetSettings(settings);
            }

            var inputFiles = new string[] { "201203-624176-12-mm.pepXML" };

            TestOutputSubdirectory = TestContext.TestName + "-EmbedGeneMetadata";
            TestContext.CopyTestInputFiles(inputFiles);
            TestContext.LaunchAppTest("IDPicker.exe", TestContext.TestOutputPath("*.*").QuotePathWithSpaces() + " --test-ui-layout", createTestCase(), closeAppOnError: true);

            settings.GUISettings.WarnAboutNoGeneMetadata = false;
            SetSettings(settings);

            TestOutputSubdirectory = TestContext.TestName;
            TestContext.CopyTestInputFiles(inputFiles);
            TestContext.LaunchAppTest("IDPicker.exe", TestContext.TestOutputPath("*.*").QuotePathWithSpaces() + " --test-ui-layout", createTestCase(), closeAppOnError: true);
        }

        [TestMethod]
        public void ImportMultipleFilesOnOpen()
        {
            // get settings in a separate invocation because import starts immediately when a file is passed on the command-line
            var settings = GetAndSetTestSettings();

            // this lambda allow reusing of the testing code; we can change only UI parameters to create a specific AppRunner
            Func<string, AppRunner> createTestCase = (mergedOutputFilepath) =>
            {
                return (app, windowStack) =>
                {
                    var window = app.GetWindow(SearchCriteria.ByAutomationId("IDPickerForm"), InitializeOption.NoCache);
                    var statusBar = window.Get<StatusStrip>();
                    var statusText = statusBar.Get<TextBox>();
                    windowStack.Push(window);

                    if (String.IsNullOrEmpty(mergedOutputFilepath))
                    {
                        // check:
                        // - trying to save to a read only location prompts for a new location
                        // - choosing an existing filepath asks user to confirm overwriting
                        // - that the automatically generated merged filepath is correct
                        Window saveDialog = null;
                        IDPicker.Util.TryRepeatedly(() => saveDialog = window.ModalWindows()[0]);
                        windowStack.Push(saveDialog);

                        // HACK: saveDialog.Get<TextBox>() won't work because of some unsupported control types in the Save Dialog (at least on Windows 7); I'm not sure if the 1001 id is stable
                        var saveTarget = new TextBox(saveDialog.AutomationElement.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "1001")), new NullActionListener());
                        Assert.AreEqual(TestContext.TestOutputPath("201208-378803.idpDB"), saveTarget.Text);

                        var saveButton = new Button(saveDialog.AutomationElement.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "1")), new NullActionListener());
                        saveButton.Click();
                    }

                    var progressForm = window.ModalWindow(SearchCriteria.ByAutomationId("ProgressForm"), InitializeOption.NoCache);
                    windowStack.Push(progressForm);

                    var importSettings = window.ModalWindow(SearchCriteria.ByAutomationId("UserDialog"));
                    windowStack.Push(importSettings);

                    var settingsTable = importSettings.Get<Table>("dataGridView");
                    Assert.IsNotNull(settingsTable);
                    Assert.AreEqual(4, settingsTable.Rows.Count);

                    Assert.AreEqual(7, settingsTable.Rows[0].Cells.Count);
                    UnitTestExtensions.AssertSequenceEquals(new Object[] { "Comet 2014.02", "cow.protein.PRG2012-subset.fasta", "XXX_", "2", "0.1", "False", "Comet optimized" }, settingsTable.Rows[0].Cells.Select(o => o.Value).ToArray());
                    UnitTestExtensions.AssertSequenceEquals(new Object[] { "MyriMatch 2.2.140", "cow.protein.PRG2012-subset.fasta", "XXX_", "2", "0.1", "False", "MyriMatch optimized" }, settingsTable.Rows[1].Cells.Select(o => o.Value).ToArray());
                    UnitTestExtensions.AssertSequenceEquals(new Object[] { "MS-GF+ Beta (v10072)", "cow.protein.PRG2012-subset.fasta", "XXX", "2", "0.1", "False", "MS-GF+" }, settingsTable.Rows[2].Cells.Select(o => o.Value).ToArray());
                    UnitTestExtensions.AssertSequenceEquals(new Object[] { "Mascot 2.2.06", "cow.protein.PRG2012-subset.fasta", "DECOY_", "2", "0.1", "False", "Mascot ionscore" }, settingsTable.Rows[3].Cells.Select(o => o.Value).ToArray());

                    // HACK: for some reason White's TableCell.Value property isn't sending keyboard input correctly;
                    // with this workaround, be careful while debugging around this code because the keyboard input might be sent to the debugger!
                    settingsTable.Rows[0].Cells[1].Click();
                    importSettings.Keyboard.Enter(TestContext.TestDataFilePath("cow.protein.PRG2012-subset.fasta"));

                    // TODO: add interface testing
                    // test qonverter settings
                    // test error conditions (bad values for max rank and max FDR score)
                    // test invalid decoy prefix
                    // test analysis parameters tree

                    var ok = importSettings.Get<Button>("okButton");
                    ok.RaiseClickEvent();
                    windowStack.Pop();

                    while (!progressForm.IsClosed)
                        Thread.Sleep(500);

                    windowStack.Pop();

                    bool willEmbedGeneMetadata = settings.GUISettings.WarnAboutNoGeneMetadata;

                    // handle prompt for gene metadata embedding if necessary
                    if (willEmbedGeneMetadata)
                    {
                        var prompt = window.ModalWindow(SearchCriteria.ByAutomationId("EmbedGeneMetadataWarningForm"), InitializeOption.NoCache);
                        windowStack.Push(prompt);

                        prompt.Get<CheckBox>("doNotShowCheckBox").Click();
                        prompt.Get<Button>("embedButton").RaiseClickEvent();

                        while (!prompt.IsClosed)
                            Thread.Sleep(500);

                        // refresh settings
                        settings = GetSettings(app, windowStack);
                        Assert.AreEqual(false, settings.GUISettings.WarnAboutNoGeneMetadata);

                        // reset to original state
                        settings.GUISettings.WarnAboutNoGeneMetadata = true;
                        SetSettings(app, windowStack, settings);
                    }

                    WaitForReady(statusText);

                    window = app.GetWindow(SearchCriteria.ByAutomationId("IDPickerForm"), InitializeOption.NoCache);
                    var dockableForms = window.GetDockableForms();

                    var proteinTableForm = dockableForms.Single(o => o.Id == "ProteinTableForm");
                    if (willEmbedGeneMetadata)
                        Assert.AreEqual("Protein View: 6 clusters, 8 protein groups, 13 proteins, 0% protein FDR, 8 gene groups, 13 genes", proteinTableForm.Name);
                    else
                        Assert.AreEqual("Protein View: 6 clusters, 8 protein groups, 13 proteins, 0% protein FDR", proteinTableForm.Name);

                    var peptideTableForm = dockableForms.Single(o => o.Id == "PeptideTableForm");
                    Assert.AreEqual("Peptide View: 43 distinct peptides, 47 distinct matches", peptideTableForm.Name);

                    var spectrumTableForm = dockableForms.Single(o => o.Id == "SpectrumTableForm");
                    Assert.AreEqual("Spectrum View: 1 groups, 1 sources, 42 spectra", spectrumTableForm.Name);

                    var modificationTableForm = dockableForms.Single(o => o.Id == "ModificationTableForm");
                    Assert.AreEqual("Modification View: 24 modified spectra", modificationTableForm.Name);

                    var analysisTableForm = dockableForms.Single(o => o.Id == "AnalysisTableForm");
                    Assert.AreEqual("Analysis View", analysisTableForm.Name);

                    var filterHistoryForm = dockableForms.Single(o => o.Id == "FilterHistoryForm");
                    Assert.AreEqual("Filter History", filterHistoryForm.Name);
                };
            };

            // make sure embed gene metadata warning is turned on for first tests
            if (!settings.GUISettings.WarnAboutNoGeneMetadata)
            {
                settings.GUISettings.WarnAboutNoGeneMetadata = true;
                SetSettings(settings);
            }

            var inputFiles = new string[] { "201208-378803-*.mzid", "201208-378803-*xml", "F003098.dat" };

            TestOutputSubdirectory = TestContext.TestName + "-EmbedGeneMetadata";
            TestContext.CopyTestInputFiles(inputFiles);
            TestContext.LaunchAppTest("IDPicker.exe", TestContext.TestOutputPath("*.*").QuotePathWithSpaces() + " --test-ui-layout", createTestCase(""), closeAppOnError: true);

            TestOutputSubdirectory = TestContext.TestName + "-EmbedGeneMetadata-MergedOutputFilepath";
            TestContext.CopyTestInputFiles(inputFiles);
            TestContext.LaunchAppTest("IDPicker.exe", TestContext.TestOutputPath("*.*").QuotePathWithSpaces() + " -MergedOutputFilepath foobar.idpDB --test-ui-layout", createTestCase("foobar.idpDB"), closeAppOnError: true);

            settings.GUISettings.WarnAboutNoGeneMetadata = false;
            SetSettings(settings);

            TestOutputSubdirectory = TestContext.TestName + "-MergedOutputFilepath";
            TestContext.CopyTestInputFiles(inputFiles);
            TestContext.LaunchAppTest("IDPicker.exe", TestContext.TestOutputPath("*.*").QuotePathWithSpaces() + " -MergedOutputFilepath foobar.idpDB --test-ui-layout", createTestCase("foobar.idpDB"), closeAppOnError: true);
        }

        [TestMethod]
        public void ImportMultipleFilesFromMenu()
        {
            IDPickerAllSettings settings = null;

            Func<AppRunner> createTestCase = () =>
            {
                return (app, windowStack) =>
                {
                    var window = app.GetWindow(SearchCriteria.ByAutomationId("IDPickerForm"), InitializeOption.NoCache);
                    var statusBar = window.Get<StatusStrip>();
                    var statusText = statusBar.Get<TextBox>();
                    windowStack.Push(window);

                    settings = GetAndSetTestSettings(app, windowStack);

                    var menu = window.RawGet<MenuBar>(SearchCriteria.ByAutomationId("menuStrip1"), 2);
                    //menu.MenuItemBy(SearchCriteria.ByAutomationId("toolsToolStripMenuItem"), SearchCriteria.ByAutomationId("optionsToolStripMenuItem")).Click();
                    menu.MenuItem("File", "Import files").Click(); // FIXME: not localized, but the AutomationIds aren't being set properly so the above line won't work

                    var importFilesForm = window.ModalWindow(SearchCriteria.ByAutomationId("IDPOpenDialog"), InitializeOption.WithCache);
                    windowStack.Push(importFilesForm);

                    // make sure "all importable IDPicker formats" is selected, should be the first option
                    var sourceType = importFilesForm.Get<ComboBox>("sourceTypeComboBox");
                    sourceType.Select(0);

                    var fileTree = importFilesForm.Get<Tree>(SearchCriteria.ByAutomationId("FileTree"));
                    List<string> pathSegments = TestContext.TestOutputPath().Split('\\').ToList();
                    pathSegments[0] += '\\';
                    var node = fileTree.Node(pathSegments.ToArray());
                    node.Select();
                    importFilesForm.Get<Button>("AddNode").Click(); // TODO: fix variable names in Jay's code to be consistent with my code
                    Thread.Sleep(500);
                    importFilesForm.Get<Button>("openButton").RaiseClickEvent();

                    // check:
                    // - trying to save to a read only location prompts for a new location
                    // - choosing an existing filepath asks user to confirm overwriting
                    // - that the automatically generated merged filepath is correct
                    Window saveDialog = null;
                    IDPicker.Util.TryRepeatedly(() => saveDialog = window.ModalWindows()[0]);
                    windowStack.Push(saveDialog);

                    // HACK: saveDialog.Get<TextBox>() won't work because of some unsupported control types in the Save Dialog (at least on Windows 7); I'm not sure if the 1001 id is stable
                    var saveTarget = new TextBox(saveDialog.AutomationElement.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "1001")), new NullActionListener());
                    Assert.AreEqual(TestContext.TestOutputPath("201208-378803.idpDB"), saveTarget.Text);

                    var saveButton = new Button(saveDialog.AutomationElement.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "1")), new NullActionListener());
                    saveButton.Click();

                    var progressForm = window.ModalWindow(SearchCriteria.ByAutomationId("ProgressForm"), InitializeOption.NoCache);
                    windowStack.Push(progressForm);

                    var importSettings = window.ModalWindow(SearchCriteria.ByAutomationId("UserDialog"));
                    windowStack.Push(importSettings);

                    var settingsTable = importSettings.Get<Table>("dataGridView");
                    Assert.IsNotNull(settingsTable);
                    Assert.AreEqual(4, settingsTable.Rows.Count);

                    Assert.AreEqual(7, settingsTable.Rows[0].Cells.Count);
                    UnitTestExtensions.AssertSequenceEquals(new Object[] { "Comet 2014.02", "cow.protein.PRG2012-subset.fasta", "XXX_", "2", "0.1", "False", "Comet optimized" }, settingsTable.Rows[0].Cells.Select(o => o.Value).ToArray());
                    UnitTestExtensions.AssertSequenceEquals(new Object[] { "MyriMatch 2.2.140", "cow.protein.PRG2012-subset.fasta", "XXX_", "2", "0.1", "False", "MyriMatch optimized" }, settingsTable.Rows[1].Cells.Select(o => o.Value).ToArray());
                    UnitTestExtensions.AssertSequenceEquals(new Object[] { "MS-GF+ Beta (v10072)", "cow.protein.PRG2012-subset.fasta", "XXX", "2", "0.1", "False", "MS-GF+" }, settingsTable.Rows[2].Cells.Select(o => o.Value).ToArray());
                    UnitTestExtensions.AssertSequenceEquals(new Object[] { "Mascot 2.2.06", "cow.protein.PRG2012-subset.fasta", "DECOY_", "2", "0.1", "False", "Mascot ionscore" }, settingsTable.Rows[3].Cells.Select(o => o.Value).ToArray());

                    // HACK: for some reason White's TableCell.Value property isn't sending keyboard input correctly;
                    // with this workaround, be careful while debugging around this code because the keyboard input might be sent to the debugger!
                    settingsTable.Rows[0].Cells[1].Click();
                    importSettings.Keyboard.Enter(TestContext.TestDataFilePath("cow.protein.PRG2012-subset.fasta"));

                    // TODO: add interface testing
                    // test qonverter settings
                    // test error conditions (bad values for max rank and max FDR score)
                    // test invalid decoy prefix
                    // test analysis parameters tree

                    var ok = importSettings.Get<Button>("okButton");
                    ok.Click();
                    windowStack.Pop();

                    while (!progressForm.IsClosed)
                        Thread.Sleep(1000);

                    windowStack.Pop();

                    if (settings.GUISettings.WarnAboutNoGeneMetadata)
                    {
                        // handle prompt for gene metadata embedding
                        var prompt = window.ModalWindow(SearchCriteria.ByAutomationId("EmbedGeneMetadataWarningForm"), InitializeOption.NoCache);
                        prompt.Get<Button>("embedButton").RaiseClickEvent();
                    }

                    WaitForReady(statusText);

                    window = app.GetWindow(SearchCriteria.ByAutomationId("IDPickerForm"), InitializeOption.NoCache);
                    var dockableForms = window.GetDockableForms();

                    var proteinTableForm = dockableForms.Single(o => o.Id == "ProteinTableForm");
                    if (settings.GUISettings.WarnAboutNoGeneMetadata)
                        Assert.AreEqual("Protein View: 6 clusters, 8 protein groups, 13 proteins, 0% protein FDR, 8 gene groups, 13 genes", proteinTableForm.Name);
                    else
                        Assert.AreEqual("Protein View: 6 clusters, 8 protein groups, 13 proteins, 0% protein FDR", proteinTableForm.Name);

                    var peptideTableForm = dockableForms.Single(o => o.Id == "PeptideTableForm");
                    Assert.AreEqual("Peptide View: 43 distinct peptides, 47 distinct matches", peptideTableForm.Name);

                    var spectrumTableForm = dockableForms.Single(o => o.Id == "SpectrumTableForm");
                    Assert.AreEqual("Spectrum View: 2 groups, 1 sources, 42 spectra", spectrumTableForm.Name);

                    var modificationTableForm = dockableForms.Single(o => o.Id == "ModificationTableForm");
                    Assert.AreEqual("Modification View: 24 modified spectra", modificationTableForm.Name);

                    var analysisTableForm = dockableForms.Single(o => o.Id == "AnalysisTableForm");
                    Assert.AreEqual("Analysis View", analysisTableForm.Name);

                    var filterHistoryForm = dockableForms.Single(o => o.Id == "FilterHistoryForm");
                    Assert.AreEqual("Filter History", filterHistoryForm.Name);
                };
            };

            

            TestContext.CopyTestInputFiles("201208-378803*.pepXML", "201208-378803*.pep.xml", "201208-378803*.mzid", "F003098.dat");

            TestContext.LaunchAppTest("IDPicker.exe", "--test-ui-layout", createTestCase(), closeAppOnError: true);

            // toggle embed gene metadata warning
            settings.GUISettings.WarnAboutNoGeneMetadata = !settings.GUISettings.WarnAboutNoGeneMetadata;
            SetSettings(settings);

            // delete the idpDB files between tests
            Directory.GetFiles(TestContext.TestOutputPath(), "*.idpDB").ToList().ForEach(o => File.Delete(o));

            TestContext.LaunchAppTest("IDPicker.exe", "--test-ui-layout", createTestCase(), closeAppOnError: true);

            // test with default hierarchy when input is a flat hierarchy, e.g. /source1, /source2; should be no groups in output
            // test with default hierarchy when input is a multi-level hierarchy, e.g. /A/1/source1, /B/2/source2; the hierarchy should be preserved in the output
            // test that sources are combined together when they are in separate places in the filesystem, even Mascot DAT files
            // test that files are combined properly when 
        }

        [TestMethod]
        public void OpenExistingFileOnOpen()
        {
            TestContext.CopyTestInputFiles("201203-624176-12-mm-gui-test.idpDB");

            TestContext.LaunchAppTest("IDPicker.exe", TestContext.TestOutputPath("201203-624176-12-mm-gui-test.idpDB").QuotePathWithSpaces() + " --test-ui-layout",

            (app, windowStack) =>
            {
                var window = app.GetWindow(SearchCriteria.ByAutomationId("IDPickerForm"), InitializeOption.NoCache);
                windowStack.Push(window);

                var statusBar = window.Get<StatusStrip>();
                var statusText = statusBar.Get<TextBox>();
                WaitForReady(statusText);

                window = app.GetWindow(SearchCriteria.ByAutomationId("IDPickerForm"), InitializeOption.NoCache);
                var dockableForms = window.GetDockableForms();

                var proteinTableForm = dockableForms.Single(o => o.Id == "ProteinTableForm");
                Assert.AreEqual("Protein View: 9 clusters, 9 protein groups, 19 proteins, 0% protein FDR, 9 gene groups, 19 genes", proteinTableForm.Name);

                var peptideTableForm = dockableForms.Single(o => o.Id == "PeptideTableForm");
                Assert.AreEqual("Peptide View: 129 distinct peptides, 191 distinct matches", peptideTableForm.Name);

                var spectrumTableForm = dockableForms.Single(o => o.Id == "SpectrumTableForm");
                Assert.AreEqual("Spectrum View: 1 groups, 1 sources, 207 spectra", spectrumTableForm.Name);

                var modificationTableForm = dockableForms.Single(o => o.Id == "ModificationTableForm");
                Assert.AreEqual("Modification View: 82 modified spectra", modificationTableForm.Name);

                var analysisTableForm = dockableForms.Single(o => o.Id == "AnalysisTableForm");
                Assert.AreEqual("Analysis View", analysisTableForm.Name);

                var filterHistoryForm = dockableForms.Single(o => o.Id == "FilterHistoryForm");
                Assert.AreEqual("Filter History", filterHistoryForm.Name);
            });
        }
    }
}
