/*
 * Original author: Daniel Broudy <daniel.broudy .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.IO;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ConfigureToolsDlgTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestConfigureToolsDlg()
        {
            TestFilesZip = @"TestFunctional\ConfigureToolsDlgTest.zip"; //Not L10N
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            #region OtherTests
            TestHttpPost();

            TestEmptyOpen();

            TestButtons();

            TestPopups();

            TestEmptyCommand();

            TestSaveDialogNo();

            TestSavedCancel();

            TestValidNo();

            TestIndexChange();

            TestProcessStart();

            TestNewToolName();

            TestMacros();

            TestMacroComplaint();

            TestMacroReplacement();

            TestURL();

            TestImmediateWindow();

            TestCascadingMenuItems();

            //ZipTestGoodImport(); //Imports MSstats report.

            ZipTestInvalidToolDescription();

            //ZipTestAllCommandTypes();

            ZipTestSkylineReports();

            TestToolDirMacro();
            #endregion

            //TestArgCollector(); //Still Fails

            //TestNotInstalledTool();

            //TestLocateFileDlg();
        }

        private void TestLocateFileDlg()
        {
            ConfigureToolsDlg configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            string path = TestFilesDir.GetTestPath("TestLocateFileDlg.zip");
            RunUI(() =>
                {
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.UnpackZipTool(path);
                    Assert.AreEqual("TestTool1", configureToolsDlg.textTitle.Text);
                    Assert.AreEqual("$(ProgramPath(R,2.15.2))", configureToolsDlg.textCommand.Text);
                    Assert.AreEqual("TestArgs", configureToolsDlg.textArguments.Text);
                    Assert.AreEqual("DocDir", configureToolsDlg.textInitialDirectory.Text);
                    Assert.AreEqual(CheckState.Checked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                    Assert.AreEqual("QuaSAR Input", configureToolsDlg.comboReport.SelectedItem);
                });
            
            LocateFileDlg locateFileDlg = ShowDialog<LocateFileDlg>(configureToolsDlg.EditMacro);
            RunUI(()=>
                {
                    AssertEx.AreComparableStrings(TextUtil.LineSeparate(
                        Resources.LocateFileDlg_LocateFileDlg_This_tool_requires_0_version_1,
                        Resources.LocateFileDlg_LocateFileDlg_If_you_have_it_installed_please_provide_the_path_below,
                        Resources.LocateFileDlg_LocateFileDlg_Otherwise__please_cancel_and_install__0__version__1__first,
                        Resources.LocateFileDlg_LocateFileDlg_then_run_the_tool_again), locateFileDlg.Message, 4);
                Assert.AreEqual(String.Empty, locateFileDlg.Path);
                locateFileDlg.Path = "invalidPath";
            });
            RunDlg<MessageDlg>(locateFileDlg.OkDialog, messageDlg =>
            {
                AssertEx.AreComparableStrings(Resources.LocateFileDlg_PathPasses_You_have_not_provided_a_valid_path_, messageDlg.Message);
                messageDlg.OkDialog();
            });
            RunUI(() =>
            {
                locateFileDlg.CancelButton.PerformClick();     
                configureToolsDlg.OkDialog();
                SkylineWindow.PopulateToolsMenu();
            });
            LocateFileDlg lfd = ShowDialog<LocateFileDlg>(() => SkylineWindow.RunTool(0));
            RunUI(() =>
            {
                Assert.AreEqual(String.Empty, lfd.Path);
                lfd.Path = TestFilesDir.GetTestPath("ShortStdinToStdout.exe");
                lfd.OkDialog();
            });
            

            ConfigureToolsDlg ctd = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            LocateFileDlg locate = ShowDialog<LocateFileDlg>(configureToolsDlg.EditMacro);
            RunUI(() =>
            {
                AssertEx.AreComparableStrings(TextUtil.LineSeparate(
                                              Resources.LocateFileDlg_LocateFileDlg_This_tool_requires_0_version_1,
                                              Resources.LocateFileDlg_LocateFileDlg_Below_is_the_saved_value_for_the_path_to_the_executable,
                                              Resources.LocateFileDlg_LocateFileDlg_Please_verify_and_update_if_incorrect), locate.Message, 2);
                Assert.AreEqual(TestFilesDir.GetTestPath("ShortStdinToStdout.exe"), locate.Path);
                locate.OkDialog();
                ctd.RemoveAllTools();
                ctd.OkDialog();
            });
        }

        private static void TestNotInstalledTool()
        {            
            ConfigureToolsDlg configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunUI(() =>
            {
                configureToolsDlg.RemoveAllTools();                
                configureToolsDlg.AddDialog("NEwTool", "$(ToolDir)\\Test.exe", string.Empty, string.Empty);
                Assert.AreEqual("NEwTool", configureToolsDlg.textTitle.Text);
                Assert.AreEqual("$(ToolDir)\\Test.exe", configureToolsDlg.textCommand.Text);                
            });
            MessageDlg dlg = ShowDialog<MessageDlg>(configureToolsDlg.Add);
            RunUI(() =>
            {
                AssertEx.AreComparableStrings(
                    Resources.ConfigureToolsDlg_CheckPassToolInternal__ToolDir__is_not_a_valid_macro_for_a_tool_that_was_not_installed_and_therefore_does_not_have_a_Tool_Directory_,
                    dlg.Message, 0);
                dlg.OkDialog();
                configureToolsDlg.Remove();
                configureToolsDlg.OkDialog();
            });

            
        }        

        private void ZipTestGoodImport()
        {
            ConfigureToolsDlg configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            string path1 = TestFilesDir.GetTestPath("MSstats.zip");
            string exepath = TestFilesDir.GetTestPath("ShortStdinToStdout.exe");
            RunUI(() =>
                {
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.SaveTools();
                    //configureToolsDlg.UnpackZipTool(path1);
                });
            LocateFileDlg locateFileDlg = ShowDialog<LocateFileDlg>(() =>configureToolsDlg.UnpackZipTool(path1));
            RunUI(() =>
            {                
                Assert.AreEqual(String.Empty, locateFileDlg.Path);
                locateFileDlg.Path = exepath;
                locateFileDlg.OkDialog();
            });


            RunUI(() =>
            {
                Assert.AreEqual(3,configureToolsDlg.listTools.Items.Count);
                //MSstats\\Design Sample Size
                configureToolsDlg.listTools.SelectedIndex = 0;
                Assert.AreEqual("MSstats\\Design Sample Size", configureToolsDlg.textTitle.Text);
                Assert.AreEqual("$(ProgramPath(R,2.15.2))", configureToolsDlg.textCommand.Text);
                Assert.AreEqual("-f $(ToolDir)\\MSStatsDSS.r --slave --args $(InputReportTempPath)", configureToolsDlg.textArguments.Text);
                Assert.AreEqual("$(DocumentDir)", configureToolsDlg.textInitialDirectory.Text);
                Assert.AreEqual(CheckState.Unchecked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                Assert.AreEqual("MSstats", configureToolsDlg.comboReport.SelectedItem);
                string tooldir = configureToolsDlg.ToolDir;
                string argscollectorpath = Path.Combine(tooldir, "MSStatArgsCollector.dll");                
                Assert.AreEqual(configureToolsDlg.ArgsCollectorPath, argscollectorpath );
                Assert.AreEqual(configureToolsDlg.ArgsCollectorType, "MSStatArgsCollector.MSstatsSampleSizeCollector");

                // MSstats\\Group Comparison
                configureToolsDlg.listTools.SelectedIndex = 1;
                Assert.AreEqual("MSstats\\Group Comparison", configureToolsDlg.textTitle.Text);
                Assert.AreEqual("$(ProgramPath(R,2.15.2))", configureToolsDlg.textCommand.Text);
                Assert.AreEqual("-f $(ToolDir)\\MSStatsGC.r --slave --args $(InputReportTempPath)", configureToolsDlg.textArguments.Text);
                Assert.AreEqual("$(DocumentDir)", configureToolsDlg.textInitialDirectory.Text);
                Assert.AreEqual(CheckState.Unchecked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                Assert.AreEqual("MSstats", configureToolsDlg.comboReport.SelectedItem);
                Assert.AreEqual(tooldir,configureToolsDlg.ToolDir);
                Assert.AreEqual(configureToolsDlg.ArgsCollectorPath, argscollectorpath);
                Assert.AreEqual(configureToolsDlg.ArgsCollectorType, "MSStatArgsCollector.MSstatsGroupComparisonCollector");

                // MSstats\\QC
                configureToolsDlg.listTools.SelectedIndex = 2;
                Assert.AreEqual("MSstats\\QC", configureToolsDlg.textTitle.Text);
                Assert.AreEqual("$(ProgramPath(R,2.15.2))", configureToolsDlg.textCommand.Text);
                Assert.AreEqual("-f $(ToolDir)\\MSStatsQC.r --slave --args $(InputReportTempPath)", configureToolsDlg.textArguments.Text);
                Assert.AreEqual("$(DocumentDir)", configureToolsDlg.textInitialDirectory.Text);
                Assert.AreEqual(CheckState.Unchecked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                Assert.AreEqual("MSstats", configureToolsDlg.comboReport.SelectedItem);
                Assert.AreEqual(tooldir, configureToolsDlg.ToolDir);
                Assert.AreNotEqual(configureToolsDlg.ArgsCollectorPath, argscollectorpath);
                Assert.AreEqual(configureToolsDlg.ArgsCollectorType, string.Empty);
                
                
                configureToolsDlg.RemoveAllTools();
                configureToolsDlg.OkDialog();
            });
        }

        private void ZipTestInvalidToolDescription()
        {
            ConfigureToolsDlg configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            string toolNoCommandPath = TestFilesDir.GetTestPath("ToolNoCommand.zip");
            RunUI(() =>
                {
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.SaveTools();
                });

            RunDlg<MessageDlg>(() => configureToolsDlg.UnpackZipTool(toolNoCommandPath), messageDlg =>
            {
                AssertEx.AreComparableStrings(TextUtil.LineSeparate(Resources.ConfigureToolsDlg_unpackZipTool_Invalid_Tool_Description_in_file__0__,
                        Resources.ConfigureToolsDlg_unpackZipTool_Title_and_Command_are_required,
                        Resources.ConfigureToolsDlg_unpackZipTool_skipping_that_tool_), messageDlg.Message, 1);
                messageDlg.OkDialog();
            });
            string toolNoTitlePath = TestFilesDir.GetTestPath("ToolNoTitle.zip");
            RunDlg<MessageDlg>(() => configureToolsDlg.UnpackZipTool(toolNoTitlePath), messageDlg =>
            {
                AssertEx.AreComparableStrings(TextUtil.LineSeparate(Resources.ConfigureToolsDlg_unpackZipTool_Invalid_Tool_Description_in_file__0__,
                        Resources.ConfigureToolsDlg_unpackZipTool_Title_and_Command_are_required,
                        Resources.ConfigureToolsDlg_unpackZipTool_skipping_that_tool_), messageDlg.Message, 1);
                messageDlg.OkDialog();
            });
            RunUI(configureToolsDlg.OkDialog);
        }

        private void ZipTestAllCommandTypes()
        {
            ConfigureToolsDlg configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            string allCommandTypesPath = TestFilesDir.GetTestPath("AllCommandTypes.zip");
            RunUI(() =>
            {
                configureToolsDlg.RemoveAllTools();
                configureToolsDlg.SaveTools();
                configureToolsDlg.UnpackZipTool(allCommandTypesPath);
                Assert.AreEqual(3, configureToolsDlg.listTools.Items.Count);

                // ExecutableType
                configureToolsDlg.listTools.SelectedIndex = 0;
                Assert.AreEqual("ExecutableType", configureToolsDlg.textTitle.Text);
                Assert.AreEqual("$(ToolDir)\\HelloWorld.exe", configureToolsDlg.textCommand.Text);
                Assert.AreEqual(string.Empty, configureToolsDlg.textArguments.Text);
                Assert.AreEqual(string.Empty, configureToolsDlg.textInitialDirectory.Text);
                Assert.AreEqual(CheckState.Unchecked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                Assert.AreEqual(string.Empty, configureToolsDlg.comboReport.SelectedItem);
                string tooldir = configureToolsDlg.ToolDir;
                Assert.AreEqual(configureToolsDlg.ArgsCollectorPath, string.Empty);
                Assert.AreEqual(configureToolsDlg.ArgsCollectorType, string.Empty);

                // ProgramPathType
                configureToolsDlg.listTools.SelectedIndex = 1;
                Assert.AreEqual("ProgramPathType", configureToolsDlg.textTitle.Text);
                Assert.AreEqual("$(ProgramPath(R,2.15.2))", configureToolsDlg.textCommand.Text);
                Assert.AreEqual(string.Empty, configureToolsDlg.textArguments.Text);
                Assert.AreEqual(string.Empty, configureToolsDlg.textInitialDirectory.Text);
                Assert.AreEqual(CheckState.Unchecked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                Assert.AreEqual(string.Empty, configureToolsDlg.comboReport.SelectedItem);
                Assert.AreEqual(tooldir, configureToolsDlg.ToolDir);
                Assert.AreEqual(configureToolsDlg.ArgsCollectorPath, string.Empty);
                Assert.AreEqual(configureToolsDlg.ArgsCollectorType, string.Empty);

                // WebPageType
                configureToolsDlg.listTools.SelectedIndex = 2;
                Assert.AreEqual("WebPageType", configureToolsDlg.textTitle.Text);
                Assert.AreEqual("http://www.google.com", configureToolsDlg.textCommand.Text);
                Assert.IsFalse(configureToolsDlg.textArguments.Enabled);
                Assert.IsFalse(configureToolsDlg.textInitialDirectory.Enabled);
                Assert.IsFalse(configureToolsDlg.cbOutputImmediateWindow.Enabled);
                Assert.AreEqual(string.Empty, configureToolsDlg.comboReport.SelectedItem);
                Assert.AreEqual(tooldir, configureToolsDlg.ToolDir);
                Assert.AreEqual(configureToolsDlg.ArgsCollectorPath, string.Empty);
                Assert.AreEqual(configureToolsDlg.ArgsCollectorType, string.Empty);

                configureToolsDlg.RemoveAllTools();
                configureToolsDlg.OkDialog();
            });
        }

        private void ZipTestSkylineReports()
        {
            ConfigureToolsDlg configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            string testSkylineReportsPath = TestFilesDir.GetTestPath("TestSkylineReports.zip");
            RunUI(() =>
                {
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.SaveTools();                    
                });
            MessageDlg messageDlgReportNotProvided = ShowDialog<MessageDlg>(()=>configureToolsDlg.UnpackZipTool(testSkylineReportsPath));
            RunUI(()=>
                    {
                        AssertEx.AreComparableStrings(
                            Resources.UnpackZipToolHelper_UnpackZipTool_The_tool___0___requires_report_type_titled___1___and_it_is_not_provided__Import_canceled_,
                            messageDlgReportNotProvided.Message, 2);
                        messageDlgReportNotProvided.OkDialog();
                    });
            RunUI(()=>
            {
                Assert.AreEqual(1, configureToolsDlg.listTools.Items.Count);
                Assert.AreEqual("HelloWorld", configureToolsDlg.textTitle.Text);
                Assert.AreEqual("$(ToolDir)\\HelloWorld.exe", configureToolsDlg.textCommand.Text);
                Assert.AreEqual(string.Empty, configureToolsDlg.textArguments.Text);
                Assert.AreEqual(string.Empty, configureToolsDlg.textInitialDirectory.Text);
                Assert.AreEqual(CheckState.Checked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                Assert.AreEqual("UniqueReport", configureToolsDlg.comboReport.SelectedItem);
            });
            string uniqueReportPath = TestFilesDir.GetTestPath("UniqueReport.zip");
            RunUI(() => configureToolsDlg.SaveTools());
            RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.UnpackZipTool(uniqueReportPath), resolveReportConflict =>
                    {
                        Assert.IsTrue(resolveReportConflict.Message.Contains("A Report with the name UniqueReport already exists."));
                        Assert.IsTrue(resolveReportConflict.Message.Contains("Do you wish to overwrite or install in parallel?"));
                        resolveReportConflict.CancelDialog();
                    });
            WaitForConditionUI(() => configureToolsDlg.listTools.Items.Count == 1);
            RunUI(() =>
            {
                Assert.AreEqual("HelloWorld", configureToolsDlg.textTitle.Text);
                Assert.AreEqual("$(ToolDir)\\HelloWorld.exe", configureToolsDlg.textCommand.Text);
                Assert.AreEqual(string.Empty, configureToolsDlg.textArguments.Text);
                Assert.AreEqual(string.Empty, configureToolsDlg.textInitialDirectory.Text);
                Assert.AreEqual(CheckState.Checked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                Assert.AreEqual("UniqueReport", configureToolsDlg.comboReport.SelectedItem);
                configureToolsDlg.SaveTools();
            });
            RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.UnpackZipTool(uniqueReportPath), resolveReportConflict2 =>
                    {
                        Assert.IsTrue(resolveReportConflict2.Message.Contains("A Report with the name UniqueReport already exists."));
                        Assert.IsTrue(resolveReportConflict2.Message.Contains("Do you wish to overwrite or install in parallel?"));                        
                        resolveReportConflict2.Btn0Click(); //Overwrite report
                    });
            WaitForConditionUI(() => configureToolsDlg.listTools.Items.Count == 2);
            string toolDir = string.Empty;
            RunUI(()=>
            {
                Assert.AreEqual(1, configureToolsDlg.listTools.SelectedIndex);
                Assert.AreEqual("HelloWorld1", configureToolsDlg.textTitle.Text);
                Assert.AreEqual("$(ToolDir)\\HelloWorld.exe", configureToolsDlg.textCommand.Text);
                Assert.AreEqual(string.Empty, configureToolsDlg.textArguments.Text);
                Assert.AreEqual(string.Empty, configureToolsDlg.textInitialDirectory.Text);
                Assert.AreEqual(CheckState.Checked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                Assert.AreEqual("UniqueReport", configureToolsDlg.comboReport.SelectedItem);
                toolDir = configureToolsDlg.ToolDir;
                Assert.IsTrue(Directory.Exists(toolDir));
            });
            RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.UnpackZipTool(uniqueReportPath), resolveReportConflict3 =>
            {
                Assert.IsTrue(resolveReportConflict3.Message.Contains("The tool HelloWorld1 is in conflict with the new installation"));
                Assert.IsTrue(resolveReportConflict3.Message.Contains("A Report with the name UniqueReport already exists."));
                Assert.IsTrue(resolveReportConflict3.Message.Contains("Do you wish to overwrite or install in parallel?"));
                resolveReportConflict3.Btn0Click(); //Overwrite
            });
            WaitForConditionUI(() => configureToolsDlg.listTools.Items.Count == 2);
            RunUI(() =>
            {
                Assert.AreEqual(1, configureToolsDlg.listTools.SelectedIndex);
                Assert.AreEqual("HelloWorld1", configureToolsDlg.textTitle.Text);
                Assert.AreEqual("$(ToolDir)\\HelloWorld.exe", configureToolsDlg.textCommand.Text);
                Assert.AreEqual(string.Empty, configureToolsDlg.textArguments.Text);
                Assert.AreEqual(string.Empty, configureToolsDlg.textInitialDirectory.Text);
                Assert.AreEqual(toolDir, configureToolsDlg.ToolDir);
                Assert.AreEqual(CheckState.Checked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                Assert.AreEqual("UniqueReport", configureToolsDlg.comboReport.SelectedItem);
            });
            RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.UnpackZipTool(uniqueReportPath),
                resolveReportConflict4 => resolveReportConflict4.Btn1Click());
            WaitForConditionUI(() => configureToolsDlg.listTools.Items.Count == 3);
            RunUI(() =>
            {
                Assert.AreEqual(2, configureToolsDlg.listTools.SelectedIndex);
                Assert.AreEqual("HelloWorld2", configureToolsDlg.textTitle.Text);
                Assert.AreEqual("$(ToolDir)\\HelloWorld.exe", configureToolsDlg.textCommand.Text);
                Assert.AreEqual(string.Empty, configureToolsDlg.textArguments.Text);
                Assert.AreEqual(string.Empty, configureToolsDlg.textInitialDirectory.Text);
                Assert.AreNotEqual(toolDir, configureToolsDlg.ToolDir);
                Assert.AreEqual(CheckState.Checked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                Assert.AreNotEqual("UniqueReport", configureToolsDlg.comboReport.SelectedItem); //Could asser
                configureToolsDlg.RemoveAllTools();
                configureToolsDlg.OkDialog();
            });

        }

        private void TestToolDirMacro()
        {
            ConfigureToolsDlg configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            string path1 = TestFilesDir.GetTestPath("TestToolDirMacro.zip");
            RunUI(() =>
                {
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.UnpackZipTool(path1);
                    Assert.AreEqual("TestToolDirMacro", configureToolsDlg.textTitle.Text);
                    Assert.AreEqual("$(ToolDir)\\HelloWorld.exe", configureToolsDlg.textCommand.Text);
                    Assert.AreEqual("$(ToolDir)\\MSStatsDSS.r", configureToolsDlg.textArguments.Text);
                    Assert.AreEqual(string.Empty, configureToolsDlg.textInitialDirectory.Text);
                    Assert.AreEqual(CheckState.Unchecked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                    Assert.AreEqual(string.Empty, configureToolsDlg.comboReport.SelectedItem);
                    configureToolsDlg.PopulateListMacroArguments();
                    string toolDir = configureToolsDlg.ToolDir;
                    string macroToolDir =
                        configureToolsDlg.GetMacroArgumentToolTip(Resources.ToolMacros__listArguments_Tool_Directory);
                    Assert.AreEqual(toolDir,macroToolDir);
                    //string argsCollected =
                    //    configureToolsDlg.GetMacroArgumentToolTip(
                    //        Resources.ToolMacros__listArguments_Collected_Arguments);
                    //Assert.AreEqual(Resources.ConfigureToolsDlg_PopulateMacroDropdown_Arguments_collected_at_run_time,argsCollected);
                    string inputReportTempPath =
                        configureToolsDlg.GetMacroArgumentToolTip(
                            Resources.ToolMacros__listArguments_Input_Report_Temp_Path);
                    Assert.AreEqual(inputReportTempPath, Resources.ConfigureToolsDlg_PopulateMacroDropdown_File_path_to_a_temporary_report);
                    configureToolsDlg.SaveTools();
                    ToolDescription tool = Settings.Default.ToolList[0];
                    Assert.AreEqual(tool.Title,configureToolsDlg.textTitle.Text);
                    Assert.AreEqual(tool.ToolDirPath, toolDir);
                    string expectedcommand = configureToolsDlg.textCommand.Text.Replace("$(ToolDir)", toolDir);

                    string command = ToolMacros.ReplaceMacrosCommand(SkylineWindow.Document, SkylineWindow, tool, SkylineWindow);
                    Assert.AreEqual(expectedcommand,command);

                    string expectedArgument = configureToolsDlg.textArguments.Text.Replace("$(ToolDir)", toolDir);

                    string arguments = ToolMacros.ReplaceMacrosArguments(SkylineWindow.Document, SkylineWindow, tool, SkylineWindow);
                    Assert.AreEqual(expectedArgument, arguments);

                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.OkDialog();
                });
        }

        private void TestArgCollector()
        {
            //TestArgCollector.zip
            ConfigureToolsDlg configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            string path1 = TestFilesDir.GetTestPath("TestArgCollector.zip");
            RunUI(() =>
                {
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.UnpackZipTool(path1);
                    Assert.AreEqual("TestArgCollector", configureToolsDlg.textTitle.Text);
                    Assert.AreEqual("$(ToolDir)\\ArgstoOut.exe", configureToolsDlg.textCommand.Text);
                    Assert.AreEqual("SomeArgs $(CollectedArgs) SomeMoreArgs", configureToolsDlg.textArguments.Text);
                    Assert.AreEqual(string.Empty, configureToolsDlg.textInitialDirectory.Text);
                    Assert.AreEqual(CheckState.Checked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                    Assert.AreEqual(string.Empty, configureToolsDlg.comboReport.SelectedItem);
                    string toolDir = configureToolsDlg.ToolDir;
                    Assert.IsTrue(Directory.Exists(toolDir));
                    string argsCollector = configureToolsDlg.ArgsCollectorPath;
                    string argscollectorCall = configureToolsDlg.ArgsCollectorType;
                    Assert.IsTrue(File.Exists(Path.Combine(toolDir,argsCollector)));
                    Assert.IsTrue(File.Exists(Path.Combine(toolDir, "ArgstoOut.exe")));
                    Assert.AreEqual("TestArgCollector.dll", Path.GetFileName(argsCollector));
                    Assert.AreEqual("TestArgCollector.ArgCollector", argscollectorCall);
                    configureToolsDlg.OkDialog();

                    SkylineWindow.PopulateToolsMenu();
                    string toolText = SkylineWindow.GetToolText(0);// Somehow the SRM collider and Quasar are still on the list!

                    Assert.AreEqual("TestArgCollector", toolText); // Not L10N                    
                    SkylineWindow.RunTool(0);                   
                });
            const string toolOutput = "SomeArgs test args collector SomeMoreArgs"; // Not L10N
            //WaitForConditionUI(() => SkylineWindow.ImmediateWindow != null && SkylineWindow.ImmediateWindow.Visible);
            WaitForConditionUI(() => SkylineWindow.ImmediateWindow.TextContent.Contains(toolOutput));
            RunUI(() =>
            {
                Assert.IsTrue(SkylineWindow.ImmediateWindow.TextContent.Contains(toolOutput));
                SkylineWindow.ImmediateWindow.Clear();
                SkylineWindow.Close();
            });

        }


        public class FakeWebHelper : IWebHelpers
        {
            public FakeWebHelper()
            {
                _openLinkCalled = false;
                _httpPostCalled = false;
            }

            public bool _openLinkCalled { get; set; }
            public bool _httpPostCalled { get; set; }
            
            #region Implementation of IWebHelpers

            public void OpenLink(string link)
            {
                _openLinkCalled = true;
            }

            public void PostToLink(string link, string postData)
            {
                _httpPostCalled = true;
            }

            #endregion
        }

        // Instead of putting Not L10N every time i use these i factored them out.
        private readonly string _empty = String.Empty; // Not L10N 
        private const string EXAMPLE = "example"; // Not L10N
        private const string EXAMPLE_EXE = "example.exe"; //Not L10N
        private const string EXAMPLE1 = "example1"; // Not L10N
        private const string EXAMPLE1_EXE = "_example1.exe"; //Not L10N
        private const string EXAMPLE2 = "example2"; // Not L10N
        private const string EXAMPLE2_EXE = "example2.exe"; //Not L10N
        private const string EXAMPLE2_ARGUMENT = "2Arguments"; //Not L10N
        private const string EXAMPLE2_INTLDIR = "2InitialDir"; //Not L10N
        private const string EXAMPLE3 = "example3"; // Not L10N
        private const string EXAMPLE3_EXE = "example3.exe"; // Not L10N
        private const string FOLDEREXAMPLE1 = @"Test folder\example1"; // Not L10N
        private const string FOLDEREXAMPLE2 = @"Test folder\example2"; // Not L10N
        private const string FOLDEREXAMPLE3 = @"Test folder\further\example3"; // Not L10N
        private const string FOLDER_NAME = "Test folder";
        private const string FURTHER = "further";

        private void TestHttpPost()
        {            
            ConfigureToolsDlg configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            FakeWebHelper fakeWebHelper = new FakeWebHelper();
            RunUI(() =>
                {
                    //Remove all tools.
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.AddDialog("OpenLinkTest", "http://www.google.com", _empty, _empty, false, _empty);
                    // Not L10N
                    configureToolsDlg.AddDialog("HttpPostTest", "http://www.google.com", _empty, _empty, false,
                                                "Transition Results"); // Not L10N
                    Assert.AreEqual(2, configureToolsDlg.ToolList.Count);
                    configureToolsDlg.OkDialog();

                    Settings.Default.ToolList[0].WebHelpers = fakeWebHelper;
                    Settings.Default.ToolList[1].WebHelpers = fakeWebHelper;

                    SkylineWindow.PopulateToolsMenu();
                    Assert.IsFalse(fakeWebHelper._openLinkCalled);
                    SkylineWindow.RunTool(0);
                    Assert.IsTrue(fakeWebHelper._openLinkCalled);
                    Assert.IsFalse((fakeWebHelper._httpPostCalled));
                    SkylineWindow.RunTool(1);
                });
            // The post now happens on a background thread, since report export can take a long time
            WaitForCondition(() => fakeWebHelper._httpPostCalled);
            RunUI(() =>
                {
                    // Remove all tools
                    while (Settings.Default.ToolList.Count > 0)
                    {
                        Settings.Default.ToolList.RemoveAt(0);
                    }
                    SkylineWindow.PopulateToolsMenu();

                });
        }

        private void TestImmediateWindow()
        {            
            ConfigureToolsDlg configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            string exePath = TestFilesDir.GetTestPath("ShortStdinToStdout.exe"); // Not L10N
            // ShortStdinToStdout outputs the string provided to it. the string can come either as an argument or from stdin.
            WaitForCondition(10*60*1000, () => File.Exists(exePath));
            RunUI(() =>
            {
                Assert.IsTrue(File.Exists(exePath));
                //Remove all tools.
                configureToolsDlg.RemoveAllTools();
                configureToolsDlg.AddDialog("ImWindowTest", exePath, _empty, _empty, true,
                                            "Peptide RT Results"); // Report passed via stdin. // Not L10N
                configureToolsDlg.AddDialog("ImWindowTestWithMacro", exePath, ToolMacros.INPUT_REPORT_TEMP_PATH,
                                            _empty, true, "Transition Results");
                // Report passed as an argument. // Not L10N
                Assert.AreEqual(2, configureToolsDlg.ToolList.Count);
                configureToolsDlg.OkDialog();
                SkylineWindow.PopulateToolsMenu();
                Assert.AreEqual("ImWindowTest", SkylineWindow.GetToolText(0)); // Not L10N
                Assert.AreEqual("ImWindowTestWithMacro", SkylineWindow.GetToolText(1)); // Not L10N
                SkylineWindow.RunTool(0);                                               
            });
            string reportText = "PeptideSequence,ProteinName,ReplicateName,PredictedRetentionTime,PeptideRetentionTime,PeptidePeakFoundRatio" // Not L10N
                .Replace(TextUtil.SEPARATOR_CSV, TextUtil.CsvSeparator);
            WaitForConditionUI(30*1000, () => SkylineWindow.ImmediateWindow != null);
            WaitForConditionUI(() => SkylineWindow.ImmediateWindow.TextContent.Contains(reportText));
            RunUI(() =>
            {
                SkylineWindow.ImmediateWindow.Clear();
                SkylineWindow.RunTool(1);
            });
            string reportText1 = "PeptideSequence,ProteinName,ReplicateName,PrecursorMz,PrecursorCharge,ProductMz,ProductCharge,FragmentIon,RetentionTime,Area,Background,PeakRank" //Not L10N
                .Replace(TextUtil.SEPARATOR_CSV, TextUtil.CsvSeparator);
            WaitForConditionUI(() => SkylineWindow.ImmediateWindow.TextContent.Contains(reportText1));
            RunUI(() =>
            {
                SkylineWindow.ImmediateWindow.Clear();
                //SkylineWindow.ImmediateWindow.Close();
            });            
    }

        private void TestURL()
        {
            ConfigureToolsDlg configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunUI(() =>
            {
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.AddDialog("ExampleWebsiteTool", "https://skyline.gs.washington.edu/labkey/project/home/begin.view?", _empty, _empty, false, _empty); // Not L10N
                    configureToolsDlg.AddDialog(EXAMPLE1, EXAMPLE1_EXE, _empty, _empty);
                    Assert.IsTrue(configureToolsDlg.btnRemove.Enabled);
                    Assert.AreEqual(2, configureToolsDlg.ToolList.Count);
                    Assert.AreEqual(1, configureToolsDlg.listTools.SelectedIndex);

                    Assert.AreEqual(EXAMPLE1, configureToolsDlg.textTitle.Text); 
                    Assert.AreEqual(EXAMPLE1_EXE, configureToolsDlg.textCommand.Text); 

                    Assert.IsTrue(configureToolsDlg.textTitle.Enabled);
                    Assert.IsTrue(configureToolsDlg.textCommand.Enabled);
                    Assert.IsTrue(configureToolsDlg.textArguments.Enabled);
                    Assert.IsTrue(configureToolsDlg.textInitialDirectory.Enabled);
                    Assert.IsTrue(configureToolsDlg.cbOutputImmediateWindow.Enabled);
                    Assert.IsTrue(configureToolsDlg.comboReport.Enabled);
                    Assert.IsTrue(configureToolsDlg.btnFindCommand.Enabled);
                    Assert.IsTrue(configureToolsDlg.btnArguments.Enabled);
                    Assert.IsTrue(configureToolsDlg.btnInitialDirectoryMacros.Enabled);
                    Assert.IsTrue(configureToolsDlg.btnInitialDirectory.Enabled);
             });

            RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.TestHelperIndexChange(0), messageDlg =>
            {
                AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_CheckPassTool__The_command_for__0__may_not_exist_in_that_location__Would_you_like_to_edit_it__, messageDlg.Message, 1);
                // Because of the way MultiButtonMsgDlg is written CancelClick is actually no when only yes/no options are avalible. 
                messageDlg.BtnCancelClick(); // Dialog response no.
            });
            RunUI(()=>
                      {
                          Assert.AreEqual(0, configureToolsDlg.listTools.SelectedIndex);
                          Assert.IsTrue(configureToolsDlg.textTitle.Enabled);
                          Assert.AreEqual("ExampleWebsiteTool", configureToolsDlg.textTitle.Text); // Not L10N
                          Assert.IsTrue(configureToolsDlg.textCommand.Enabled);
                          Assert.AreEqual("https://skyline.gs.washington.edu/labkey/project/home/begin.view?", configureToolsDlg.textCommand.Text); // Not L10N
                          Assert.IsFalse(configureToolsDlg.textArguments.Enabled);
                          Assert.IsFalse(configureToolsDlg.textInitialDirectory.Enabled);
                          Assert.IsFalse(configureToolsDlg.cbOutputImmediateWindow.Enabled);
                          Assert.IsTrue(configureToolsDlg.comboReport.Enabled);
                          Assert.AreEqual(_empty,configureToolsDlg.comboReport.SelectedItem);
                          Assert.IsFalse(configureToolsDlg.btnFindCommand.Enabled);
                          Assert.IsFalse(configureToolsDlg.btnArguments.Enabled);
                          Assert.IsFalse(configureToolsDlg.btnInitialDirectoryMacros.Enabled);
                          Assert.IsFalse(configureToolsDlg.btnInitialDirectory.Enabled);                        
                          Assert.AreEqual(CheckState.Unchecked, configureToolsDlg.cbOutputImmediateWindow.CheckState);

                          configureToolsDlg.TestHelperIndexChange(1);
                          Assert.AreEqual(EXAMPLE1, configureToolsDlg.textTitle.Text); 
                          Assert.AreEqual(EXAMPLE1_EXE, configureToolsDlg.textCommand.Text);
                          Assert.IsTrue(configureToolsDlg.textTitle.Enabled);
                          Assert.IsTrue(configureToolsDlg.textCommand.Enabled);
                          Assert.IsTrue(configureToolsDlg.textArguments.Enabled);
                          Assert.IsTrue(configureToolsDlg.textInitialDirectory.Enabled);
                          Assert.IsTrue(configureToolsDlg.cbOutputImmediateWindow.Enabled);
                          Assert.IsTrue(configureToolsDlg.comboReport.Enabled);
                          Assert.IsTrue(configureToolsDlg.btnFindCommand.Enabled);
                          Assert.IsTrue(configureToolsDlg.btnArguments.Enabled);
                          Assert.IsTrue(configureToolsDlg.btnInitialDirectoryMacros.Enabled);
                          Assert.IsTrue(configureToolsDlg.btnInitialDirectory.Enabled);

                          //delete both and exit
                          configureToolsDlg.RemoveAllTools();
                          configureToolsDlg.OkDialog();
                      });
        }

        private void TestMacros()
        {
            RunDlg<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg, configureToolsDlg =>
            {
                configureToolsDlg.RemoveAllTools();
                configureToolsDlg.AddDialog(EXAMPLE, EXAMPLE_EXE, _empty, _empty);
                Assert.AreEqual(_empty, configureToolsDlg.textArguments.Text);
                configureToolsDlg.ClickMacro(configureToolsDlg._macroListArguments, 0);
                string shortText = configureToolsDlg._macroListArguments[0].ShortText; 
                Assert.AreEqual(shortText, configureToolsDlg.textArguments.Text);
                Assert.AreEqual(_empty, configureToolsDlg.textInitialDirectory.Text); 
                string shortText2 = configureToolsDlg._macroListInitialDirectory[0].ShortText; 
                configureToolsDlg.ClickMacro(configureToolsDlg._macroListInitialDirectory, 0);
                Assert.AreEqual(shortText2, configureToolsDlg.textInitialDirectory.Text);
                configureToolsDlg.Remove();
                configureToolsDlg.OkDialog();
            });
        }

        private void TestEmptyOpen()
        {
            // Empty the tools list just in case.
            RunDlg<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg, configureToolsDlg =>
                {                    
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.Add();
                    Assert.AreEqual("[New Tool1]", configureToolsDlg.ToolList[0].Title); // Not L10N
                    Assert.AreEqual(1, configureToolsDlg.ToolList.Count);
                    Assert.IsFalse(configureToolsDlg.btnMoveUp.Enabled);
                    Assert.IsFalse(configureToolsDlg.btnMoveDown.Enabled);
                    configureToolsDlg.Remove();
                    configureToolsDlg.SaveTools();
                    Assert.IsFalse(configureToolsDlg.btnApply.Enabled);
                    Assert.AreEqual(0, configureToolsDlg.ToolList.Count);
                    Assert.AreEqual(-1, configureToolsDlg.listTools.SelectedIndex);
                    Assert.AreEqual(-1, configureToolsDlg.PreviouslySelectedIndex);
                    configureToolsDlg.OkDialog();
                });
        }

        private void TestCascadingMenuItems()
        {
            {
                ConfigureToolsDlg configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
                RunUI(() =>
                {
                    configureToolsDlg.RemoveAllTools();
                    
                    Assert.AreEqual(0, configureToolsDlg.ToolList.Count);
                    configureToolsDlg.AddDialog(EXAMPLE3, EXAMPLE3_EXE, _empty, _empty, true, _empty);
                    configureToolsDlg.AddDialog(FOLDEREXAMPLE1, EXAMPLE1_EXE, _empty, _empty, true, _empty);
                    configureToolsDlg.AddDialog(EXAMPLE1, EXAMPLE1_EXE, _empty, _empty, true, _empty);
                    configureToolsDlg.AddDialog(FOLDEREXAMPLE3, EXAMPLE3_EXE, _empty, _empty, true, _empty);
                    configureToolsDlg.AddDialog(FOLDEREXAMPLE2, EXAMPLE2_EXE, _empty, _empty, true, _empty);                    
                });

                // Save and return to skylinewindow.
                RunDlg<MultiButtonMsgDlg>(configureToolsDlg.OkDialog, messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_CheckPassTool__The_command_for__0__may_not_exist_in_that_location__Would_you_like_to_edit_it__, messageDlg.Message, 1);
                    messageDlg.BtnCancelClick(); // Dialog response no.
                });
                RunUI(() =>
                {
                    SkylineWindow.PopulateToolsMenu();
                    Assert.AreEqual(EXAMPLE3, SkylineWindow.GetTextByIndex(0));
                    Assert.AreEqual(FOLDER_NAME, SkylineWindow.GetTextByIndex(1));
                    Assert.AreEqual(EXAMPLE1, SkylineWindow.GetTextByIndex(2));
                    
                    ToolStripMenuItem mi = SkylineWindow.GetMenuItem(1);
                    Assert.AreEqual(EXAMPLE1, mi.DropDownItems[0].Text);
                    SkylineWindow.ToolMenuItem example1 = (SkylineWindow.ToolMenuItem) mi.DropDownItems[0];
                    Assert.AreEqual(EXAMPLE1_EXE, example1.Command);
                    
                    Assert.AreEqual(FURTHER, mi.DropDownItems[1].Text);
                    ToolStripMenuItem mi2 = (ToolStripMenuItem) mi.DropDownItems[1];
                    Assert.AreEqual(EXAMPLE3, mi2.DropDownItems[0].Text);
                    SkylineWindow.ToolMenuItem example3 = (SkylineWindow.ToolMenuItem)mi2.DropDownItems[0];
                    Assert.AreEqual(EXAMPLE3_EXE, example3.Command);

                    Assert.AreEqual(EXAMPLE2, mi.DropDownItems[2].Text);
                    SkylineWindow.ToolMenuItem example2 = (SkylineWindow.ToolMenuItem)mi.DropDownItems[2];
                    Assert.AreEqual(EXAMPLE2_EXE, example2.Command);

                    Assert.IsTrue(SkylineWindow.ConfigMenuPresent());
                    configureToolsDlg.RemoveAllTools();
                });
                RunUI(() =>
                {
                    configureToolsDlg.RemoveAllTools();
                    // Now the tool list is empty.
                    Assert.IsFalse(configureToolsDlg.btnRemove.Enabled);
                    configureToolsDlg.OkDialog();
                    SkylineWindow.PopulateToolsMenu();
                    Assert.IsTrue(SkylineWindow.ConfigMenuPresent());
                });
            }
        }

        private void TestButtons()
        {
            {
                
                ConfigureToolsDlg configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
                RunUI(() =>
                {
                    configureToolsDlg.RemoveAllTools();
                    Assert.AreEqual(_empty, configureToolsDlg.textTitle.Text); 
                    Assert.AreEqual(_empty, configureToolsDlg.textCommand.Text);
                    Assert.AreEqual(_empty, configureToolsDlg.textArguments.Text);  
                    Assert.AreEqual(_empty, configureToolsDlg.textInitialDirectory.Text);
                    Assert.AreEqual(CheckState.Unchecked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                    Assert.AreEqual(_empty, configureToolsDlg.comboReport.SelectedItem);
                    //All buttons and fields disabled
                    Assert.IsFalse(configureToolsDlg.textTitle.Enabled);
                    Assert.IsFalse(configureToolsDlg.textCommand.Enabled);
                    Assert.IsFalse(configureToolsDlg.textArguments.Enabled);
                    Assert.IsFalse(configureToolsDlg.textInitialDirectory.Enabled);
                    Assert.IsFalse(configureToolsDlg.cbOutputImmediateWindow.Enabled);
                    Assert.IsFalse(configureToolsDlg.comboReport.Enabled);
                    Assert.IsFalse(configureToolsDlg.btnFindCommand.Enabled);
                    Assert.IsFalse(configureToolsDlg.btnArguments.Enabled);
                    Assert.IsFalse(configureToolsDlg.btnInitialDirectoryMacros.Enabled);
                    Assert.IsFalse(configureToolsDlg.btnInitialDirectory.Enabled);

                    Assert.AreEqual(0, configureToolsDlg.ToolList.Count);
                    Assert.IsFalse(configureToolsDlg.btnRemove.Enabled);
                    // Now add an example.                    
                    configureToolsDlg.AddDialog(EXAMPLE1, EXAMPLE1_EXE, _empty, _empty, true, _empty); 
                    Assert.AreEqual(1, configureToolsDlg.ToolList.Count);
                    Assert.AreEqual(0, configureToolsDlg.listTools.SelectedIndex);
                    //All buttons and fields enabled
                    Assert.IsTrue(configureToolsDlg.btnRemove.Enabled);
                    Assert.IsTrue(configureToolsDlg.textTitle.Enabled);
                    Assert.IsTrue(configureToolsDlg.textCommand.Enabled);
                    Assert.IsTrue(configureToolsDlg.textArguments.Enabled);
                    Assert.IsTrue(configureToolsDlg.textInitialDirectory.Enabled);
                    Assert.IsTrue(configureToolsDlg.cbOutputImmediateWindow.Enabled);
                    Assert.IsTrue(configureToolsDlg.comboReport.Enabled);
                    Assert.IsTrue(configureToolsDlg.btnFindCommand.Enabled);
                    Assert.IsTrue(configureToolsDlg.btnArguments.Enabled);
                    Assert.IsTrue(configureToolsDlg.btnInitialDirectoryMacros.Enabled);
                    Assert.IsTrue(configureToolsDlg.btnInitialDirectory.Enabled);


                    Assert.AreEqual(EXAMPLE1, configureToolsDlg.textTitle.Text);
                    Assert.AreEqual(EXAMPLE1_EXE, configureToolsDlg.textCommand.Text);
                    Assert.AreEqual(CheckState.Checked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                    Assert.AreEqual(_empty, configureToolsDlg.comboReport.SelectedItem); 
                    Assert.IsFalse(configureToolsDlg.btnMoveUp.Enabled);
                    Assert.IsFalse(configureToolsDlg.btnMoveDown.Enabled);
                    // Now add an example2.
                    configureToolsDlg.comboReport.Items.Add("ExampleReport"); // Not L10N
                    configureToolsDlg.AddDialog(EXAMPLE2, EXAMPLE2_EXE, EXAMPLE2_ARGUMENT, EXAMPLE2_INTLDIR, false, "ExampleReport"); // Not L10N
                    Assert.AreEqual(2, configureToolsDlg.ToolList.Count);
                    Assert.AreEqual(1, configureToolsDlg.listTools.SelectedIndex);
                    Assert.AreEqual(EXAMPLE2, configureToolsDlg.textTitle.Text); 
                    Assert.AreEqual(EXAMPLE2_EXE, configureToolsDlg.textCommand.Text);
                    Assert.AreEqual(EXAMPLE2_ARGUMENT, configureToolsDlg.textArguments.Text); 
                    Assert.AreEqual(configureToolsDlg.textInitialDirectory.Text, EXAMPLE2_INTLDIR); 
                    Assert.AreEqual(CheckState.Unchecked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                    Assert.AreEqual("ExampleReport", configureToolsDlg.comboReport.SelectedItem); // Not L10N

                    Assert.IsTrue(configureToolsDlg.btnMoveUp.Enabled);
                    Assert.IsFalse(configureToolsDlg.btnMoveDown.Enabled);
                    // Test move up/down with only 2 tools.
                    configureToolsDlg.MoveUp();
                    Assert.AreEqual(2, configureToolsDlg.ToolList.Count);
                    Assert.AreEqual(0, configureToolsDlg.listTools.SelectedIndex);
                    Assert.AreEqual(EXAMPLE2, configureToolsDlg.textTitle.Text); 
                    Assert.AreEqual(EXAMPLE2_EXE, configureToolsDlg.textCommand.Text); 
                    Assert.AreEqual(EXAMPLE2_ARGUMENT, configureToolsDlg.textArguments.Text); 
                    Assert.AreEqual(configureToolsDlg.textInitialDirectory.Text, EXAMPLE2_INTLDIR);
                    Assert.AreEqual(CheckState.Unchecked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                    Assert.AreEqual("ExampleReport", configureToolsDlg.comboReport.SelectedItem); // Not L10N

                    Assert.IsFalse(configureToolsDlg.btnMoveUp.Enabled);
                    Assert.IsTrue(configureToolsDlg.btnMoveDown.Enabled);
                    Assert.AreEqual(EXAMPLE2, configureToolsDlg.ToolList[0].Title); 
                    Assert.AreEqual(EXAMPLE1, configureToolsDlg.ToolList[1].Title); 

                    configureToolsDlg.MoveDown();
                    Assert.AreEqual(EXAMPLE1, configureToolsDlg.ToolList[0].Title); 
                    Assert.AreEqual(EXAMPLE2, configureToolsDlg.ToolList[1].Title); 
                    // Now add an example 3.
                    configureToolsDlg.AddDialog(EXAMPLE3, EXAMPLE3_EXE, _empty, _empty); 
                    Assert.AreEqual(3, configureToolsDlg.ToolList.Count);
                    Assert.AreEqual(2, configureToolsDlg.listTools.SelectedIndex);
                    Assert.AreEqual(EXAMPLE3, configureToolsDlg.textTitle.Text); 
                    Assert.AreEqual(EXAMPLE3_EXE, configureToolsDlg.textCommand.Text);
                    Assert.AreEqual(_empty, configureToolsDlg.textArguments.Text);
                    Assert.AreEqual(_empty, configureToolsDlg.textInitialDirectory.Text);
                    Assert.AreEqual(CheckState.Unchecked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                    Assert.AreEqual(_empty, configureToolsDlg.comboReport.SelectedItem);
                    Assert.IsTrue(configureToolsDlg.btnMoveUp.Enabled);
                    Assert.IsFalse(configureToolsDlg.btnMoveDown.Enabled);
                    // Test btnMoveUp.
                    configureToolsDlg.MoveUp();
                    Assert.AreEqual(3, configureToolsDlg.ToolList.Count);
                    Assert.AreEqual(1, configureToolsDlg.listTools.SelectedIndex);
                    Assert.AreEqual(EXAMPLE3, configureToolsDlg.textTitle.Text);
                    Assert.AreEqual(EXAMPLE3_EXE, configureToolsDlg.textCommand.Text);
                    Assert.AreEqual(_empty, configureToolsDlg.textArguments.Text); 
                    Assert.AreEqual(_empty, configureToolsDlg.textInitialDirectory.Text); 
                    Assert.AreEqual(CheckState.Unchecked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                    Assert.AreEqual(_empty, configureToolsDlg.comboReport.SelectedItem); 

                    Assert.IsTrue(configureToolsDlg.btnMoveUp.Enabled);
                    Assert.IsTrue(configureToolsDlg.btnMoveDown.Enabled);
                    Assert.AreEqual(EXAMPLE2, configureToolsDlg.ToolList[2].Title); 
                    Assert.AreEqual(EXAMPLE3, configureToolsDlg.ToolList[1].Title); 
                    Assert.AreEqual(EXAMPLE1, configureToolsDlg.ToolList[0].Title); 
                });
                // Test response to selected index changing.
                RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.TestHelperIndexChange(0), messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_CheckPassTool__The_command_for__0__may_not_exist_in_that_location__Would_you_like_to_edit_it__, messageDlg.Message, 1);
                    // Because of the way MultiButtonMsgDlg is written CancelClick is actually no when only yes/no options are avalible. 
                    messageDlg.BtnCancelClick(); // Dialog response no.
                });
                RunUI(() =>
                {
                    Assert.AreEqual(EXAMPLE1, configureToolsDlg.textTitle.Text); 
                    Assert.AreEqual(EXAMPLE1_EXE, configureToolsDlg.textCommand.Text); 
                    Assert.AreEqual(_empty, configureToolsDlg.textArguments.Text); 
                    Assert.AreEqual(_empty, configureToolsDlg.textInitialDirectory.Text);
                    Assert.AreEqual(CheckState.Checked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                    Assert.AreEqual(_empty, configureToolsDlg.comboReport.SelectedItem);

                    Assert.IsFalse(configureToolsDlg.btnMoveUp.Enabled);
                    Assert.IsTrue(configureToolsDlg.btnMoveDown.Enabled);
                    // Test update of previously selected index.
                    configureToolsDlg.listTools.SelectedIndex = 0;
                    Assert.AreEqual(configureToolsDlg.PreviouslySelectedIndex, 0);
                    // Test btnMoveDown.
                    configureToolsDlg.MoveDown();
                    Assert.AreEqual(1, configureToolsDlg.listTools.SelectedIndex);
                    Assert.AreEqual(EXAMPLE1, configureToolsDlg.textTitle.Text);
                    Assert.AreEqual(EXAMPLE1_EXE, configureToolsDlg.textCommand.Text); 
                    Assert.AreEqual(_empty, configureToolsDlg.textArguments.Text);  
                    Assert.AreEqual(_empty, configureToolsDlg.textInitialDirectory.Text); 
                    Assert.AreEqual(CheckState.Checked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                    Assert.AreEqual(_empty, configureToolsDlg.comboReport.SelectedItem);

                    Assert.IsTrue(configureToolsDlg.btnMoveUp.Enabled);
                    Assert.IsTrue(configureToolsDlg.btnMoveDown.Enabled);
                    Assert.AreEqual(EXAMPLE3, configureToolsDlg.ToolList[0].Title);
                    Assert.AreEqual(EXAMPLE1, configureToolsDlg.ToolList[1].Title); 
                    Assert.AreEqual(EXAMPLE2, configureToolsDlg.ToolList[2].Title); 
            });
                RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.TestHelperIndexChange(2), messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_CheckPassTool__The_command_for__0__may_not_exist_in_that_location__Would_you_like_to_edit_it__, messageDlg.Message, 1);
                    // Because of the way MultiButtonMsgDlg is written CancelClick is actually no when only yes/no options are avalible. 
                    messageDlg.BtnCancelClick(); // Dialog response no.
                });
                RunUI(()=>
                {
                    Assert.AreEqual(2, configureToolsDlg.listTools.SelectedIndex);
                    Assert.AreEqual(EXAMPLE2, configureToolsDlg.textTitle.Text);
                    Assert.AreEqual(EXAMPLE2_EXE, configureToolsDlg.textCommand.Text);
                    Assert.AreEqual(EXAMPLE2_ARGUMENT, configureToolsDlg.textArguments.Text);
                    Assert.AreEqual(configureToolsDlg.textInitialDirectory.Text, EXAMPLE2_INTLDIR);
                    Assert.AreEqual(CheckState.Unchecked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                    Assert.AreEqual("ExampleReport", configureToolsDlg.comboReport.SelectedItem); // Not L10N
                });
                RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.TestHelperIndexChange(1), messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_CheckPassTool__The_command_for__0__may_not_exist_in_that_location__Would_you_like_to_edit_it__, messageDlg.Message, 1);
                    // Because of the way MultiButtonMsgDlg is written CancelClick is actually no when only yes/no options are avalible. 
                    messageDlg.BtnCancelClick(); // Dialog response no.
                });
                RunUI(()=>
                {
                    Assert.AreEqual(EXAMPLE1, configureToolsDlg.textTitle.Text);
                    Assert.AreEqual(EXAMPLE1_EXE, configureToolsDlg.textCommand.Text);
                    Assert.AreEqual(CheckState.Checked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                    Assert.AreEqual(_empty, configureToolsDlg.comboReport.SelectedItem);
                });

                // Save and return to skylinewindow.
                RunDlg<MultiButtonMsgDlg>(configureToolsDlg.OkDialog, messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_CheckPassTool__The_command_for__0__may_not_exist_in_that_location__Would_you_like_to_edit_it__, messageDlg.Message, 1);
                    messageDlg.BtnCancelClick(); // Dialog response no.
                });
                RunUI(() =>
                {
                    SkylineWindow.PopulateToolsMenu();
                    Assert.AreEqual(EXAMPLE3, SkylineWindow.GetToolText(0));
                    Assert.AreEqual(EXAMPLE1, SkylineWindow.GetToolText(1));
                    Assert.AreEqual(EXAMPLE2, SkylineWindow.GetToolText(2));
                    Assert.IsTrue(SkylineWindow.ConfigMenuPresent());
                });
            }
            {
                // Reopen menu to swap, save, close, and check changes showed up.
                var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
                RunUI(() =>
                {
                    Assert.AreEqual(3, configureToolsDlg.ToolList.Count);
                    Assert.AreEqual(EXAMPLE3, configureToolsDlg.ToolList[0].Title);
                    Assert.AreEqual(EXAMPLE1, configureToolsDlg.ToolList[1].Title);
                    Assert.AreEqual(EXAMPLE2, configureToolsDlg.ToolList[2].Title);
                });
                RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.TestHelperIndexChange(1), messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_CheckPassTool__The_command_for__0__may_not_exist_in_that_location__Would_you_like_to_edit_it__, messageDlg.Message, 1);
                    messageDlg.BtnCancelClick(); // Dialog response no.
                });
                RunUI(() =>
                {
                    configureToolsDlg.MoveDown();
                    Assert.AreEqual(EXAMPLE3, configureToolsDlg.ToolList[0].Title);
                    Assert.AreEqual(EXAMPLE2, configureToolsDlg.ToolList[1].Title);
                    Assert.AreEqual(EXAMPLE1, configureToolsDlg.ToolList[2].Title);
                });
                RunDlg<MultiButtonMsgDlg>(configureToolsDlg.OkDialog, messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_CheckPassTool__The_command_for__0__may_not_exist_in_that_location__Would_you_like_to_edit_it__, messageDlg.Message, 1);
                    messageDlg.BtnCancelClick(); // Dialog response no.
                });
                RunUI(() =>
                {
                    SkylineWindow.PopulateToolsMenu();
                    Assert.AreEqual(EXAMPLE3, SkylineWindow.GetToolText(0));
                    Assert.AreEqual(EXAMPLE2, SkylineWindow.GetToolText(1));
                    Assert.AreEqual(EXAMPLE1, SkylineWindow.GetToolText(2));
                    Assert.IsTrue(SkylineWindow.ConfigMenuPresent());
                });
            }
            {
                // First empty the tool list then return to skyline to check dropdown is correct.
                var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
                // Change selected index to test deleting from the middle.
                RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.TestHelperIndexChange(1), messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_CheckPassTool__The_command_for__0__may_not_exist_in_that_location__Would_you_like_to_edit_it__, messageDlg.Message, 1);
                    messageDlg.BtnCancelClick(); // Dialog response no.
                });
                RunUI(() =>
                {
                    configureToolsDlg.RemoveAllTools();
                    // Now the tool list is empty.
                    Assert.IsFalse(configureToolsDlg.btnRemove.Enabled);
                    configureToolsDlg.OkDialog();
                    SkylineWindow.PopulateToolsMenu();
                    Assert.IsTrue(SkylineWindow.ConfigMenuPresent());
                });
            }
        }

        private void TestPopups()
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunUI(configureToolsDlg.Add);
            RunDlg<MessageDlg>(configureToolsDlg.OkDialog, messageDlg =>
            {
                AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_CheckPassTool_The_command_cannot_be_blank__please_enter_a_valid_command_for__0_, messageDlg.Message, 1);
                messageDlg.OkDialog();
            });
            RunUI(() =>
            {          
                configureToolsDlg.Remove();
                configureToolsDlg.AddDialog(EXAMPLE1, EXAMPLE1, _empty, _empty);
            });
            RunDlg<MessageDlg>(configureToolsDlg.OkDialog, messageDlg =>
            {
                string supportedTypes = String.Join("; ", ConfigureToolsDlg.EXTENSIONS); // Not L10N
                supportedTypes = supportedTypes.Replace(".", "*."); // Not L10N
                AssertEx.Contains(messageDlg.Message, string.Format(TextUtil.LineSeparate(
                            Resources.ConfigureToolsDlg_CheckPassTool_The_command_for__0__must_be_of_a_supported_type,
                            Resources.ConfigureToolsDlg_CheckPassTool_Supported_Types___1_,
                            Resources.ConfigureToolsDlg_CheckPassTool_if_you_would_like_the_command_to_launch_a_link__make_sure_to_include_http____or_https___),
                            EXAMPLE1, supportedTypes));
                messageDlg.OkDialog();
            });
            RunUI(() =>
            {
                configureToolsDlg.Remove();
                configureToolsDlg.AddDialog(EXAMPLE1, EXAMPLE1_EXE, _empty, _empty);
            });
            RunDlg<MultiButtonMsgDlg>(configureToolsDlg.OkDialog, messageDlg =>
            {
                AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_CheckPassTool__The_command_for__0__may_not_exist_in_that_location__Would_you_like_to_edit_it__, messageDlg.Message, 1);
                messageDlg.Btn1Click(); //Dialog response yes.
            });
            RunUI(() =>
            {
                configureToolsDlg.Remove();
                configureToolsDlg.AddDialog(_empty, EXAMPLE1_EXE, _empty, _empty);      
            });              
            RunDlg<MessageDlg>(configureToolsDlg.OkDialog, messageDlg =>
            {
                AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_CheckPassTool_You_must_enter_a_valid_title_for_the_tool, messageDlg.Message, 0);
                messageDlg.OkDialog();
            });
            // Replace the value, save, delete it then cancel to test the "Do you wish to Save changes" dlg.
            RunUI(() =>
            {
                configureToolsDlg.Remove();
                configureToolsDlg.AddDialog(EXAMPLE, EXAMPLE1_EXE, _empty, _empty);
             });
            RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.SaveTools(), messageDlg =>
            {
                AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_CheckPassTool__The_command_for__0__may_not_exist_in_that_location__Would_you_like_to_edit_it__, messageDlg.Message, 1);
                messageDlg.BtnCancelClick(); // Dialog response no.
            });
            RunUI(configureToolsDlg.Remove);
            RunDlg<MultiButtonMsgDlg>(configureToolsDlg.Cancel, messageDlg =>
            {
                AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_Cancel_Do_you_wish_to_Save_changes_, messageDlg.Message, 0);
                messageDlg.CancelDialog();
            });
            RunUI(() => Assert.IsTrue(configureToolsDlg.btnApply.Enabled));
            RunDlg<MultiButtonMsgDlg>(configureToolsDlg.Cancel, messageDlg =>
            {
                AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_Cancel_Do_you_wish_to_Save_changes_, messageDlg.Message, 0);
                messageDlg.Btn0Click();
            });
            RunUI(() => Assert.IsFalse(configureToolsDlg.btnApply.Enabled));
        }

        private void TestEmptyCommand()
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunUI(configureToolsDlg.Add);
            RunDlg<MessageDlg>(configureToolsDlg.Add, messageDlg =>
            {
                AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_CheckPassTool_The_command_cannot_be_blank__please_enter_a_valid_command_for__0_, messageDlg.Message, 1);
                messageDlg.OkDialog();
            });
            RunUI(() =>
            {
                configureToolsDlg.Remove();
                configureToolsDlg.OkDialog();
            });
        }

        private void TestSaveDialogNo()
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunUI((() =>
                {
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.Add();
                    configureToolsDlg.Remove();
                }));
            
            RunDlg<MultiButtonMsgDlg>(configureToolsDlg.Cancel, messageDlg =>
            {
                AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_Cancel_Do_you_wish_to_Save_changes_, messageDlg.Message, 0);
                messageDlg.Btn1Click();
            }); 
        }

        private void TestSavedCancel()
        {
            // Test to show Cancel when saved has no dlg.
            RunDlg<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg, configureToolsDlg =>
            {
                configureToolsDlg.Add();
                configureToolsDlg.Remove();
                configureToolsDlg.SaveTools();
                configureToolsDlg.Cancel();
            });
        }

        private void TestValidNo()
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunUI(() => configureToolsDlg.AddDialog(EXAMPLE, EXAMPLE_EXE, _empty, _empty));
            RunDlg<MultiButtonMsgDlg>(configureToolsDlg.OkDialog, messageDlg =>
            {
                AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_CheckPassTool__The_command_for__0__may_not_exist_in_that_location__Would_you_like_to_edit_it__, messageDlg.Message, 1);
                messageDlg.BtnCancelClick();
            });
        }

        private void TestIndexChange()
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunUI(() =>
            {
                configureToolsDlg.Remove();
                configureToolsDlg.AddDialog(EXAMPLE,EXAMPLE_EXE,_empty,_empty);
                configureToolsDlg.AddDialog(EXAMPLE1, EXAMPLE1_EXE, _empty, _empty);
                Assert.AreEqual(1, configureToolsDlg.PreviouslySelectedIndex);
                configureToolsDlg.listTools.SelectedIndex = 1;
                Assert.AreEqual(1, configureToolsDlg.listTools.SelectedIndex);
            });
           RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.TestHelperIndexChange(0), messageDlg =>
            {
                AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_CheckPassTool__The_command_for__0__may_not_exist_in_that_location__Would_you_like_to_edit_it__, messageDlg.Message, 1);
                messageDlg.Btn1Click();
            });
            RunUI(() =>
            {
                configureToolsDlg.Remove();
                configureToolsDlg.Remove();
                configureToolsDlg.SaveTools();
                configureToolsDlg.OkDialog();
            });
        }
        
        private void TestProcessStart()
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunUI(() =>
            {
                configureToolsDlg.RemoveAllTools();
                configureToolsDlg.AddDialog(EXAMPLE, EXAMPLE_EXE, _empty, _empty);
            });
            RunDlg<MultiButtonMsgDlg>(configureToolsDlg.OkDialog, messageDlg =>
            {
                AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_CheckPassTool__The_command_for__0__may_not_exist_in_that_location__Would_you_like_to_edit_it__, messageDlg.Message, 1);
                messageDlg.BtnCancelClick();
            }); 
            RunUI(() =>
            {            
                SkylineWindow.PopulateToolsMenu();
                Assert.AreEqual(EXAMPLE, SkylineWindow.GetToolText(0));
                Assert.IsTrue(SkylineWindow.ConfigMenuPresent());
            });
            RunDlg<MessageDlg>(() => SkylineWindow.RunTool(0), messageDlg =>
            {
                AssertEx.AreComparableStrings(TextUtil.LineSeparate(
                        Resources.ToolDescription_RunTool_File_not_found_,
                        Resources.ToolDescription_RunTool_Please_check_the_command_location_is_correct_for_this_tool_), 
                        messageDlg.Message, 0);
                messageDlg.OkDialog();
            });
          }

        private void TestNewToolName()
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunUI(() => configureToolsDlg.AddDialog("[New Tool1]", EXAMPLE1_EXE, _empty, _empty));
            RunDlg<MultiButtonMsgDlg>(configureToolsDlg.Add, messageDlg =>
            {
                AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_CheckPassTool__The_command_for__0__may_not_exist_in_that_location__Would_you_like_to_edit_it__, messageDlg.Message, 1);
                messageDlg.BtnCancelClick();
            });
            RunUI(() =>
            {
                configureToolsDlg.RemoveAllTools();
                configureToolsDlg.OkDialog();
            });
        }

        // Check that a complaint dialog is displayed when the user tries to run a tool missing a macro. 
        private void TestMacroComplaint()
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunUI(() =>
            {
                configureToolsDlg.RemoveAllTools();
                configureToolsDlg.AddDialog(EXAMPLE3, EXAMPLE_EXE, "$(DocumentPath)", _empty); // Not L10N
            });
            RunDlg<MultiButtonMsgDlg>(configureToolsDlg.OkDialog, messageDlg =>
            {
                AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_CheckPassTool__The_command_for__0__may_not_exist_in_that_location__Would_you_like_to_edit_it__, messageDlg.Message, 1);
                messageDlg.BtnCancelClick();
            });
            RunUI(() =>
            {
                ToolDescription toolMenuItem = Settings.Default.ToolList[0];
                Assert.AreEqual("$(DocumentPath)", toolMenuItem.Arguments); // Not L10N
                SkylineWindow.PopulateToolsMenu();
            });
            RunDlg<MessageDlg>(() => SkylineWindow.RunTool(0), messageDlg =>
            {
                AssertEx.AreComparableStrings(Resources.ToolMacros__listArguments_This_tool_requires_a_Document_Path_to_run, messageDlg.Message, 0);
                messageDlg.OkDialog();
            });

        }

        // Checks macro replacement method GetArguments works.
        private void TestMacroReplacement()
        {
            RunUI(() =>
            {
                while (Settings.Default.ToolList.Count > 0)
                {
                    Settings.Default.ToolList.RemoveAt(0);
                }
                Settings.Default.ToolList.Add(new ToolDescription(EXAMPLE2, EXAMPLE2_EXE, "$(DocumentPath)", "$(DocumentDir)")); // Not L10N

                SkylineWindow.Paste("PEPTIDER"); // Not L10N
                bool saved = SkylineWindow.SaveDocument(TestContext.GetTestPath("ConfigureToolsTest.sky")); // Not L10N
                // dotCover can cause trouble with saving
                Assert.IsTrue(saved);
                ToolDescription toolMenuItem = Settings.Default.ToolList[0];
                Assert.AreEqual("$(DocumentPath)", toolMenuItem.Arguments); // Not L10N
                Assert.AreEqual("$(DocumentDir)", toolMenuItem.InitialDirectory); // Not L10N
                string args = toolMenuItem.GetArguments(SkylineWindow.Document, SkylineWindow, SkylineWindow);
                string initDir = toolMenuItem.GetInitialDirectory(SkylineWindow.Document, SkylineWindow, SkylineWindow);
                Assert.AreEqual(Path.GetDirectoryName(args), initDir);
                string path = TestContext.GetTestPath("ConfigureToolsTest.sky"); // Not L10N
                Assert.AreEqual(args, path);
            });
        }

    }
}
