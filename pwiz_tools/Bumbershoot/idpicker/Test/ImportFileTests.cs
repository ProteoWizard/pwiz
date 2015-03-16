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

        [TestMethod]
        [TestCategory("GUI")]
        public void OpenWithoutFileArguments()
        {
            TestContext.LaunchAppTest("IDPicker.exe", "--test-ui-layout",

            (app, windowStack) =>
            {
                var window = app.GetWindow(SearchCriteria.ByAutomationId("IDPickerForm"), InitializeOption.NoCache);
                windowStack.Push(window);

                var statusBar = window.Get<StatusStrip>();
                var statusText = statusBar.Get<TextBox>();
                statusText.WaitForReady();
            });
        }

        [TestMethod]
        [TestCategory("GUI")]
        public void PwizBindings()
        {
            TestContext.LaunchAppTest("IDPicker.exe", "--test", // closes automatically after calling a pwiz function to make sure pwiz bindings load properly

            (app, windowStack) =>
            {
            });
        }

        /// <summary>
        /// If originalFilepath exists, returns it unchanged; otherwise, returns only the filename part of the filepath.
        /// </summary>
        private string FilepathOrFilename(string originalFilepath)
        {
            if (File.Exists(originalFilepath))
                return originalFilepath;
            else
                return Path.GetFileName(originalFilepath);
        }

        [TestMethod]
        [TestCategory("GUI")]
        public void ImportSingleFileOnOpen()
        {
            // get settings in a separate invocation because import starts immediately when a file is passed on the command-line
            var settings = TestContext.GetAndSetTestSettings();

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

                    var settingsTable = importSettings.GetFastTable("dataGridView");
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
                        settings = app.GetSettings(windowStack);
                        Assert.AreEqual(false, settings.GUISettings.WarnAboutNoGeneMetadata);

                        // reset to original state
                        settings.GUISettings.WarnAboutNoGeneMetadata = true;
                        app.SetSettings(windowStack, settings);
                    }

                    statusText.WaitForReady();

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
                TestContext.SetSettings(settings);
            }

            var inputFiles = new string[] { "201203-624176-12-mm.pepXML" };

            TestOutputSubdirectory = TestContext.TestName + "-EmbedGeneMetadata";
            TestContext.CopyTestInputFiles(inputFiles);
            TestContext.LaunchAppTest("IDPicker.exe", TestContext.TestOutputPath("*.*").QuotePathWithSpaces() + " --test-ui-layout", createTestCase(), closeAppOnError: true);

            settings.GUISettings.WarnAboutNoGeneMetadata = false;
            TestContext.SetSettings(settings);

            TestOutputSubdirectory = TestContext.TestName;
            TestContext.CopyTestInputFiles(inputFiles);
            TestContext.LaunchAppTest("IDPicker.exe", TestContext.TestOutputPath("*.*").QuotePathWithSpaces() + " --test-ui-layout", createTestCase(), closeAppOnError: true);
        }

        [TestMethod]
        [TestCategory("GUI")]
        public void ImportMultipleFilesOnOpen()
        {
            // get settings in a separate invocation because import starts immediately when a file is passed on the command-line
            var settings = TestContext.GetAndSetTestSettings();

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

                    var settingsTable = importSettings.GetFastTable("dataGridView");
                    Assert.IsNotNull(settingsTable);
                    Assert.AreEqual(4, settingsTable.Rows.Count);

                    Assert.AreEqual(7, settingsTable.Rows[0].Cells.Count);
                    UnitTestExtensions.AssertSequenceEquals(new Object[] { "Comet 2014.02", FilepathOrFilename(settingsTable.Rows[0].Cells[1].Value.ToString()), "XXX_", "2", "0.1", "False", "Comet optimized" }, settingsTable.Rows[0].Cells.Select(o => o.Value).ToArray());
                    UnitTestExtensions.AssertSequenceEquals(new Object[] { "MyriMatch 2.2.140", FilepathOrFilename(settingsTable.Rows[1].Cells[1].Value.ToString()), "XXX_", "2", "0.1", "False", "MyriMatch optimized" }, settingsTable.Rows[1].Cells.Select(o => o.Value).ToArray());
                    UnitTestExtensions.AssertSequenceEquals(new Object[] { "MS-GF+ Beta (v10072)", FilepathOrFilename(settingsTable.Rows[2].Cells[1].Value.ToString()), "XXX", "2", "0.1", "False", "MS-GF+" }, settingsTable.Rows[2].Cells.Select(o => o.Value).ToArray());
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
                        settings = app.GetSettings(windowStack);
                        Assert.AreEqual(false, settings.GUISettings.WarnAboutNoGeneMetadata);

                        // reset to original state
                        settings.GUISettings.WarnAboutNoGeneMetadata = true;
                        app.SetSettings(windowStack, settings);
                    }

                    statusText.WaitForReady();

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
                TestContext.SetSettings(settings);
            }

            var inputFiles = new string[] { "201208-378803-*.mzid", "201208-378803-*xml", "F003098.dat" };

            TestOutputSubdirectory = TestContext.TestName + "-EmbedGeneMetadata";
            TestContext.CopyTestInputFiles(inputFiles);
            TestContext.LaunchAppTest("IDPicker.exe", TestContext.TestOutputPath("*.*").QuotePathWithSpaces() + " --test-ui-layout", createTestCase(""), closeAppOnError: true);

            TestOutputSubdirectory = TestContext.TestName + "-EmbedGeneMetadata-MergedOutputFilepath";
            TestContext.CopyTestInputFiles(inputFiles);
            TestContext.LaunchAppTest("IDPicker.exe", TestContext.TestOutputPath("*.*").QuotePathWithSpaces() + " -MergedOutputFilepath foobar.idpDB --test-ui-layout", createTestCase("foobar.idpDB"), closeAppOnError: true);

            settings.GUISettings.WarnAboutNoGeneMetadata = false;
            TestContext.SetSettings(settings);

            TestOutputSubdirectory = TestContext.TestName + "-MergedOutputFilepath";
            TestContext.CopyTestInputFiles(inputFiles);
            TestContext.LaunchAppTest("IDPicker.exe", TestContext.TestOutputPath("*.*").QuotePathWithSpaces() + " -MergedOutputFilepath foobar.idpDB --test-ui-layout", createTestCase("foobar.idpDB"), closeAppOnError: true);
        }

        [TestMethod]
        [TestCategory("GUI")]
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

                    settings = app.GetAndSetTestSettings(windowStack);

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
                    IDPicker.Util.TryRepeatedly<IndexOutOfRangeException>(() => saveDialog = window.ModalWindows()[0], 10, 500);
                    windowStack.Push(saveDialog);

                    // HACK: saveDialog.Get<TextBox>() won't work because of some unsupported control types in the Save Dialog (at least on Windows 7); I'm not sure if the 1001 id is stable
                    var saveTarget = new TextBox(saveDialog.AutomationElement.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "1001")), new NullActionListener());
                    Assert.AreEqual(TestContext.TestOutputPath("201208-378803.idpDB"), saveTarget.Text);

                    var saveButton = new Button(saveDialog.AutomationElement.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "1")), new NullActionListener());
                    saveButton.Click();
                    windowStack.Pop();

                    var progressForm = window.ModalWindow(SearchCriteria.ByAutomationId("ProgressForm"), InitializeOption.NoCache);
                    windowStack.Push(progressForm);

                    var importSettings = window.ModalWindow(SearchCriteria.ByAutomationId("UserDialog"));
                    windowStack.Push(importSettings);

                    var settingsTable = importSettings.GetFastTable("dataGridView");
                    Assert.IsNotNull(settingsTable);
                    Assert.AreEqual(4, settingsTable.Rows.Count);

                    Assert.AreEqual(7, settingsTable.Rows[0].Cells.Count);
                    UnitTestExtensions.AssertSequenceEquals(new Object[] { "Comet 2014.02", FilepathOrFilename(settingsTable.Rows[0].Cells[1].Value.ToString()), "XXX_", "2", "0.1", "False", "Comet optimized" }, settingsTable.Rows[0].Cells.Select(o => o.Value).ToArray());
                    UnitTestExtensions.AssertSequenceEquals(new Object[] { "MyriMatch 2.2.140", FilepathOrFilename(settingsTable.Rows[1].Cells[1].Value.ToString()), "XXX_", "2", "0.1", "False", "MyriMatch optimized" }, settingsTable.Rows[1].Cells.Select(o => o.Value).ToArray());
                    UnitTestExtensions.AssertSequenceEquals(new Object[] { "MS-GF+ Beta (v10072)", FilepathOrFilename(settingsTable.Rows[2].Cells[1].Value.ToString()), "XXX", "2", "0.1", "False", "MS-GF+" }, settingsTable.Rows[2].Cells.Select(o => o.Value).ToArray());
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

                    statusText.WaitForReady();

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
            TestContext.SetSettings(settings);

            // delete the idpDB files between tests
            Directory.GetFiles(TestContext.TestOutputPath(), "*.idpDB").ToList().ForEach(o => File.Delete(o));

            TestContext.LaunchAppTest("IDPicker.exe", "--test-ui-layout", createTestCase(), closeAppOnError: true);

            // test with default hierarchy when input is a flat hierarchy, e.g. /source1, /source2; should be no groups in output
            // test with default hierarchy when input is a multi-level hierarchy, e.g. /A/1/source1, /B/2/source2; the hierarchy should be preserved in the output
            // test that sources are combined together when they are in separate places in the filesystem, even Mascot DAT files
            // test that files are combined properly when 
        }

        [TestMethod]
        [TestCategory("GUI")]
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
                statusText.WaitForReady();

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
