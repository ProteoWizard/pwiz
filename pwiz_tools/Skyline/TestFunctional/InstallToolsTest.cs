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
    public class InstallToolsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestConfigureToolsDlg()
        {
            TestFilesZip = @"TestFunctional\InstallToolsTest.zip"; //Not L10N
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            Settings.Default.ToolList.Clear();

            //ZipTestGoodImport(); //Imports MSstats report. //Trys to install R which will be tested somewhere else.

            ZipTestInvalidToolDescription();

            ZipTestAllCommandTypes();

            ZipTestSkylineReports();

            TestToolDirMacro();

            TestArgCollector();

            TestNotInstalledTool();

            TestLocateFileDlg();
        }

        private void TestLocateFileDlg()
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            string path = TestFilesDir.GetTestPath("TestLocateFileDlg.zip");
            RunUI(configureToolsDlg.RemoveAllTools);
            RunDlg<LocateFileDlg>(() => configureToolsDlg.UnpackZipTool(path), locateFileDlg =>
                {
                    AssertEx.AreComparableStrings(TextUtil.LineSeparate(
                        Resources.LocateFileDlg_LocateFileDlg_This_tool_requires_0_version_1,
                        Resources.LocateFileDlg_LocateFileDlg_If_you_have_it_installed_please_provide_the_path_below,
                        Resources.LocateFileDlg_LocateFileDlg_Otherwise__please_cancel_and_install__0__version__1__first,
                        Resources.LocateFileDlg_LocateFileDlg_then_run_the_tool_again), locateFileDlg.Message, 4);
                    Assert.AreEqual(String.Empty, locateFileDlg.Path);
                    locateFileDlg.OkDialog();
                });
// ReSharper disable LocalizableElement
            WaitForConditionUI(2 * 1000, ()=> configureToolsDlg.textTitle.Text == "TestTool1"); //Not L10N            
// ReSharper restore LocalizableElement

            RunUI(() =>
                {
                    Assert.AreEqual("TestTool1", configureToolsDlg.textTitle.Text);
                    Assert.AreEqual("$(ProgramPath(TESTPROGRAM,1))", configureToolsDlg.textCommand.Text);
                    Assert.AreEqual("TestArgs", configureToolsDlg.textArguments.Text);
                    Assert.AreEqual(CheckState.Checked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                    Assert.AreEqual("QuaSAR Input", configureToolsDlg.comboReport.SelectedItem);
                });

            {
                LocateFileDlg locateFileDlg = ShowDialog<LocateFileDlg>(configureToolsDlg.EditMacro);
                RunUI(() =>
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
                RunUI(() => locateFileDlg.CancelButton.PerformClick());
                WaitForClosedForm(locateFileDlg);
            }
            OkDialog(configureToolsDlg, configureToolsDlg.OkDialog);
            RunUI(()=> SkylineWindow.PopulateToolsMenu());
            string validpath = TestFilesDir.GetTestPath("ShortStdinToStdout.exe"); 
            RunDlg<LocateFileDlg>(() => SkylineWindow.RunTool(0), lfd =>
                {
                    Assert.AreEqual(String.Empty, lfd.Path);
                    lfd.Path = validpath;
                    lfd.OkDialog();
                });            

            var ctd = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);            
            RunDlg<LocateFileDlg>(configureToolsDlg.EditMacro, locate =>
                {
                    AssertEx.AreComparableStrings(TextUtil.LineSeparate(
                        Resources.LocateFileDlg_LocateFileDlg_This_tool_requires_0_version_1,
                        Resources.LocateFileDlg_LocateFileDlg_Below_is_the_saved_value_for_the_path_to_the_executable,
                        Resources.LocateFileDlg_LocateFileDlg_Please_verify_and_update_if_incorrect), locate.Message, 2);
                    Assert.AreEqual(validpath, locate.Path);
                    locate.OkDialog();
                });
            RunUI(ctd.RemoveAllTools);
            OkDialog(ctd, ctd.OkDialog);
        }

        private static void TestNotInstalledTool()
        {            
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunUI(() =>
                {
                    configureToolsDlg.RemoveAllTools();                
                    configureToolsDlg.AddDialog("NEwTool", "$(ToolDir)\\Test.exe", string.Empty, string.Empty);
                    Assert.AreEqual("NEwTool", configureToolsDlg.textTitle.Text);
                    Assert.AreEqual("$(ToolDir)\\Test.exe", configureToolsDlg.textCommand.Text);                
                });
            RunDlg<MessageDlg>(configureToolsDlg.Add, dlg =>
                {
                    AssertEx.AreComparableStrings(
                        Resources.ConfigureToolsDlg_CheckPassToolInternal__ToolDir__is_not_a_valid_macro_for_a_tool_that_was_not_installed_and_therefore_does_not_have_a_Tool_Directory_,
                        dlg.Message, 0);
                    dlg.OkDialog();
                });
            RunUI(configureToolsDlg.Remove);
            OkDialog(configureToolsDlg, configureToolsDlg.OkDialog);
        }        

        private void ZipTestGoodImport()
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            string path1 = TestFilesDir.GetTestPath("MSstats.zip");
            string exepath = TestFilesDir.GetTestPath("ShortStdinToStdout.exe");
            RunUI(() =>
                {
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.SaveTools();
                    //configureToolsDlg.UnpackZipTool(path1);
                });
            RunDlg<LocateFileDlg>(() =>configureToolsDlg.UnpackZipTool(path1), locateFileDlg =>
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
                });
            OkDialog(configureToolsDlg, configureToolsDlg.OkDialog);
        }

        private void ZipTestInvalidToolDescription()
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
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
            OkDialog(configureToolsDlg, configureToolsDlg.OkDialog);
        }

        private void ZipTestAllCommandTypes()
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            string allCommandTypesPath = TestFilesDir.GetTestPath("AllCommandTypes.zip");
            RunUI(() =>
                {
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.SaveTools();
                });
            RunDlg<LocateFileDlg>(() => configureToolsDlg.UnpackZipTool(allCommandTypesPath), dlg => dlg.OkDialog());
            WaitForConditionUI(1*1000, ()=>configureToolsDlg.listTools.Items.Count == 3);
            RunUI(()=>
                {
                    //configureToolsDlg.UnpackZipTool(allCommandTypesPath);
                    //Assert.AreEqual(3, configureToolsDlg.listTools.Items.Count);

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
                    Assert.AreEqual("$(ProgramPath(TESTPROGRAM))", configureToolsDlg.textCommand.Text);
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
                });
            OkDialog(configureToolsDlg, configureToolsDlg.OkDialog);
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
            RunDlg<MessageDlg>(() => configureToolsDlg.UnpackZipTool(testSkylineReportsPath), messageDlgReportNotProvided =>
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
                });
            OkDialog(configureToolsDlg, configureToolsDlg.OkDialog);
        }

        private void TestToolDirMacro()
        {
            string path1 = TestFilesDir.GetTestPath("TestToolDirMacro.zip");
            RunDlg<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg, configureToolsDlg =>
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
            string path1 = TestFilesDir.GetTestPath("TestArgCollector.zip");
            RunDlg<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg, configureToolsDlg =>
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
                    Assert.IsTrue(File.Exists(Path.Combine(toolDir, argsCollector)));
                    Assert.IsTrue(File.Exists(Path.Combine(toolDir, "ArgstoOut.exe")));
                    Assert.AreEqual("TestArgCollector.dll", Path.GetFileName(argsCollector));
                    Assert.AreEqual("TestArgCollector.ArgCollector", argscollectorCall);
                    configureToolsDlg.OkDialog();
                });
            RunUI(() =>
                {
                    SkylineWindow.PopulateToolsMenu();
                    string toolText = SkylineWindow.GetToolText(0);
                    // Somehow the SRM collider and Quasar are still on the list!

                    Assert.AreEqual("TestArgCollector", toolText); // Not L10N                    
                    SkylineWindow.RunTool(0);                    
                });
            const string toolOutput = "SomeArgs test args collector SomeMoreArgs"; // Not L10N
            WaitForConditionUI(3*1000, () => SkylineWindow.ImmediateWindow.TextContent.Contains(toolOutput));
            RunUI(() =>
                {
                    Assert.IsTrue(SkylineWindow.ImmediateWindow.TextContent.Contains(toolOutput));
                    SkylineWindow.ImmediateWindow.Clear();
                    SkylineWindow.ImmediateWindow.Cleanup();
                    SkylineWindow.ImmediateWindow.Close();
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
    }
}