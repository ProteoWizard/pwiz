/*
 * Original author: Daniel Broudy <daniel.broudy .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;
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
        public void TestInstallTools()
        {
            TestFilesZip = @"TestFunctional\InstallToolsTest.zip"; //Not L10N
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            Settings.Default.ToolList.Clear();

            /* Running in a loop want to move the toolDir out of the way.
             */
            using (new MovedDirectory(ToolDescriptionHelpers.GetToolsDirectory(), Skyline.Program.StressTest))
            {
                //WaitForCondition(2*1000, () => !Directory.Exists(ToolDescriptionHelpers.GetToolsDirectory()));
                ClearAllTools();
                AssertCleared();
                ZipTestInvalidPropertiesFiles();

                ZipTestAllCommandTypes();

                ZipTestSkylineReports();

                ZipTestAnnotations();

                TestToolDirMacro();

                if (!Skyline.Program.StressTest)
                {
                    /* This test creates TestArgsCollector.Dll a file that cannot be deleted in this instance of skyline
                     * after running the tool. */
                    TestArgCollector();
                }

                TestNotInstalledTool();

                TestLocateFileDlg();

                TestToolVersioning();

                TestPackageVersioning();

                ClearAllTools();
                if (Skyline.Program.StressTest)
                {
                    // If TestArgsCollector is run this will fail. Only run when looping.
                    AssertCleared();
                }
            }
        }

        private class UnpackZipToolTestSupport: IUnpackZipToolSupport
        {
            public bool? ShouldOverwriteAnnotations(List<AnnotationDef> annotations)
            {
                return false;
            }

            public bool? ShouldOverwrite(string toolCollectionName, string toolCollectionVersion, List<ReportOrViewSpec> reportList, string foundVersion,
                                         string newCollectionName)
            {
                return true;
            }

            public string InstallProgram(ProgramPathContainer ppc, ICollection<ToolPackage> packages, string pathToInstallScript)
            {
                return string.Empty;
            }
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

        private void TestLocateFileDlg()
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            string path = TestFilesDir.GetTestPath("TestLocateFileDlg.zip");
            RunUI(configureToolsDlg.RemoveAllTools);
            var locateFileDlg1 = ShowDialog<LocateFileDlg>(() => configureToolsDlg.InstallZipTool(path));
            RunUI(() =>
            {
                AssertEx.AreComparableStrings(TextUtil.LineSeparate(
                    Resources.LocateFileDlg_LocateFileDlg_This_tool_requires_0_version_1,
                    Resources.LocateFileDlg_LocateFileDlg_If_you_have_it_installed_please_provide_the_path_below,
                    Resources.LocateFileDlg_LocateFileDlg_Otherwise__please_cancel_and_install__0__version__1__first,
                    Resources.LocateFileDlg_LocateFileDlg_then_run_the_tool_again), locateFileDlg1.Message, 4);
                Assert.AreEqual(String.Empty, locateFileDlg1.Path);
            });
            OkDialog(locateFileDlg1, locateFileDlg1.OkDialog);
            // ReSharper disable LocalizableElement
            WaitForConditionUI(2 * 1000, () => configureToolsDlg.textTitle.Text == "TestTool1"); //Not L10N                                      
            // ReSharper restore LocalizableElement
            // CONSIDER(brendanx): Not sure why, but this was causing a failure in my laptop
            var messageDlgNotFound = FindOpenForm<MessageDlg>();
            if (messageDlgNotFound != null)
            {
                RunUI(() => AssertEx.AreComparableStrings(TextUtil.LineSeparate(
                        Resources.ToolDescription_RunTool_File_not_found_,
                        "{0}",
                        Resources.ToolDescription_RunTool_Please_check_the_command_location_is_correct_for_this_tool_),
                    messageDlgNotFound.Message, 1));
                OkDialog(messageDlgNotFound, messageDlgNotFound.OkDialog);
            }
            RunUI(() =>
            {
                Assert.AreEqual("TestTool1", configureToolsDlg.textTitle.Text);
                Assert.AreEqual("$(ProgramPath(TESTPROGRAM,1))", configureToolsDlg.textCommand.Text);
                Assert.AreEqual("TestArgs", configureToolsDlg.textArguments.Text);
                Assert.AreEqual(CheckState.Checked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                Assert.AreEqual(string.Empty, configureToolsDlg.comboReport.SelectedItem);
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
            RunUI(() => SkylineWindow.PopulateToolsMenu());
            string validpath = TestFilesDir.GetTestPath("ShortStdinToStdout.exe");
            RunDlg<LocateFileDlg>(() => SkylineWindow.RunTool(0), lfd =>
            {
                Assert.AreEqual(String.Empty, lfd.Path);
                lfd.Path = validpath;
                lfd.OkDialog();
            });

            var ctd = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunDlg<LocateFileDlg>(ctd.EditMacro, locate =>
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

        private void TestToolVersioning()
        {
            string version1 = TestFilesDir.GetTestPath("TestToolVersioning\\1.0\\Counter.zip");
            {
                RunDlg<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg, configureToolsDlg =>
                {
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.SaveTools();
                    configureToolsDlg.InstallZipTool(version1);
                    Assert.AreEqual("Counter", configureToolsDlg.textTitle.Text);
                    Assert.AreEqual("$(ToolDir)\\NumberWriter.exe", configureToolsDlg.textCommand.Text);
                    Assert.AreEqual("100 100", configureToolsDlg.textArguments.Text);
                    Assert.AreEqual(string.Empty, configureToolsDlg.textInitialDirectory.Text);
                    Assert.AreEqual(CheckState.Checked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                    configureToolsDlg.OkDialog();
                });
                ToolDescription newtool = Settings.Default.ToolList[0];
                Assert.AreEqual("Counter", newtool.PackageName);
                Assert.AreEqual("uw.genomesciences.macosslabs.skyline.externaltools.test.countertool", newtool.PackageIdentifier);
                Assert.AreEqual("1.0", newtool.PackageVersion);
            }
            {
                var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
                RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.InstallZipTool(version1), messageDlg =>
                {
                    string messageForm =
                        TextUtil.LineSeparate(
                            Resources.ConfigureToolsDlg_OverwriteOrInParallel_The_tool__0__is_already_installed_,
                            string.Empty,
                            Resources
                                .ConfigureToolsDlg_OverwriteOrInParallel_Do_you_wish_to_reinstall_or_install_in_parallel_);
                    AssertEx.AreComparableStrings(messageForm, messageDlg.Message, 1);

                    messageDlg.Btn1Click(); // In Parallel
                });
                WaitForConditionUI(3 * 1000, () => configureToolsDlg.ToolList.Count == 2);
                string version2 = TestFilesDir.GetTestPath("TestToolVersioning\\1.0.2\\Counter.zip");
                RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.InstallZipTool(version2), messageDlg =>
                {

                    string messageForm = TextUtil.LineSeparate(Resources.ConfigureToolsDlg_OverwriteOrInParallel_The_tool__0__is_currently_installed_, string.Empty,
                        Resources.ConfigureToolsDlg_OverwriteOrInParallel_Do_you_wish_to_upgrade_to__0__or_install_in_parallel_);
                    AssertEx.AreComparableStrings(messageForm, messageDlg.Message, 2);

                    messageDlg.Btn0Click(); // Update/Overwrite
                });
                WaitForConditionUI(3 * 1000, () => configureToolsDlg.ToolList.Count == 2);
                OkDialog(configureToolsDlg, configureToolsDlg.OkDialog);
                ToolDescription newtool = Settings.Default.ToolList[1];
                Assert.AreEqual("Counter", newtool.PackageName);
                Assert.AreEqual("uw.genomesciences.macosslabs.skyline.externaltools.test.countertool", newtool.PackageIdentifier);
                Assert.AreEqual("1.0.2", newtool.PackageVersion);
            }
            {
                var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
                RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.InstallZipTool(version1), messageDlg =>
                {
                    string messageForm = TextUtil.LineSeparate(Resources.ConfigureToolsDlg_OverwriteOrInParallel_This_is_an_older_installation_v_0__of_the_tool__1_,
                                string.Empty, Resources.ConfigureToolsDlg_OverwriteOrInParallel_Do_you_wish_to_overwrite_with_the_older_version__0__or_install_in_parallel_);
                    AssertEx.AreComparableStrings(messageForm, messageDlg.Message, 3);

                    messageDlg.Btn1Click(); // In Parallel
                });
                WaitForConditionUI(3 * 1000, () => configureToolsDlg.ToolList.Count == 3);
                string versionDifferent = TestFilesDir.GetTestPath("TestToolVersioning\\Differentidentifier\\Counter.zip");

                //Testing recognition of a different unique identifier when zip has the same name
                RunUI(() => configureToolsDlg.InstallZipTool(versionDifferent));
                WaitForConditionUI(3 * 1000, () => configureToolsDlg.ToolList.Count == 4);
                string version3 = TestFilesDir.GetTestPath("TestToolVersioning\\1.2.0\\Counter.zip");
                RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.InstallZipTool(version3), messageDlg =>
                {
                    string messageForm = TextUtil.LineSeparate(Resources.ConfigureToolsDlg_OverwriteOrInParallel_The_tool__0__is_currently_installed_, string.Empty,
                            Resources.ConfigureToolsDlg_OverwriteOrInParallel_Do_you_wish_to_upgrade_to__0__or_install_in_parallel_);
                    AssertEx.AreComparableStrings(messageForm, messageDlg.Message, 2);
                    messageDlg.Btn0Click(); // Upgrade
                });
                WaitForConditionUI(3 * 1000, () => configureToolsDlg.ToolList.Count == 4);
                RunUI(configureToolsDlg.RemoveAllTools);
                WaitForConditionUI(3 * 1000, () => configureToolsDlg.ToolList.Count == 0);
                OkDialog(configureToolsDlg, configureToolsDlg.OkDialog);
            }
            //Settings.Default.ToolList.Clear();
        }


        private void TestPackageVersioning()
        {
            IUnpackZipToolSupport support = new UnpackZipToolTestSupport();
            string version1 = TestFilesDir.GetTestPath("TestPackageVersioning.zip");
            var retval = ToolInstaller.UnpackZipTool(version1, support);
            Assert.AreEqual(1, retval.Installations.Count);
            ProgramPathContainer ppc = new ProgramPathContainer("BogusProgram","3.0.0");
            var ListPackages = retval.Installations[ppc];
            Assert.AreEqual(3, ListPackages.Count);
            Assert.AreEqual("TestPAckages", ListPackages[0].Name);
            Assert.AreEqual("7.3-28",ListPackages[0].Version);
            Assert.AreEqual("TestOtherPAckage",ListPackages[1].Name);
            Assert.AreEqual("3.2.3",ListPackages[1].Version);
            Assert.AreEqual("noVersionPackage", ListPackages[2].Name);
            Assert.AreEqual(null,ListPackages[2].Version);
        }


        private void AssertCleared()
        {
            Assert.AreEqual(0, Settings.Default.ToolList.Count);
            string toolsDir = ToolDescriptionHelpers.GetToolsDirectory();

            WaitForCondition(5*1000, () => !Directory.Exists(toolsDir), $@"Directory ""{toolsDir}"" should not exist");
        }

        private void ClearAllTools()
        {
            Settings.Default.ToolList.Clear();
            string toolsDir = ToolDescriptionHelpers.GetToolsDirectory();
            DirectoryEx.SafeDelete(toolsDir);
        }


        private void ZipTestInvalidPropertiesFiles()
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            string toolNoCommandPath = TestFilesDir.GetTestPath("ToolNoCommand.zip");
            RunUI(() =>
                {
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.SaveTools();
                });

            RunDlg<MessageDlg>(() => configureToolsDlg.InstallZipTool(toolNoCommandPath), messageDlg =>
                {
                    AssertEx.AreComparableStrings(TextUtil.LineSeparate(Resources.ConfigureToolsDlg_unpackZipTool_Invalid_Tool_Description_in_file__0__,
                                                                        Resources.ConfigureToolsDlg_unpackZipTool_Title_and_Command_are_required,
                                                                        Resources.ConfigureToolsDlg_unpackZipTool_skipping_that_tool_), messageDlg.Message, 1);
                    messageDlg.OkDialog();
                });
            string toolNoTitlePath = TestFilesDir.GetTestPath("ToolNoTitle.zip");
            RunDlg<MessageDlg>(() => configureToolsDlg.InstallZipTool(toolNoTitlePath), messageDlg =>
                {
                    AssertEx.AreComparableStrings(TextUtil.LineSeparate(Resources.ConfigureToolsDlg_unpackZipTool_Invalid_Tool_Description_in_file__0__,
                                                                        Resources.ConfigureToolsDlg_unpackZipTool_Title_and_Command_are_required,
                                                                        Resources.ConfigureToolsDlg_unpackZipTool_skipping_that_tool_), messageDlg.Message, 1);
                    messageDlg.OkDialog();
                });
            string toolNoName = TestFilesDir.GetTestPath("NoName.zip");
            RunDlg<MessageDlg>(() => configureToolsDlg.InstallZipTool(toolNoName), messageDlg =>
            {
                AssertEx.AreComparableStrings(TextUtil.LineSeparate(Resources.ToolInstaller_UnpackZipTool_The_selected_zip_file_is_not_a_valid_installable_tool_, 
                            Resources.ToolInstaller_UnpackZipTool_Error__The__0__does_not_contain_a_valid__1__attribute_), messageDlg.Message,2);
                messageDlg.OkDialog();
            });
            string toolNoVersion = TestFilesDir.GetTestPath("NoVersion.zip");
            RunDlg<MessageDlg>(() => configureToolsDlg.InstallZipTool(toolNoVersion), messageDlg =>
            {
                AssertEx.AreComparableStrings(TextUtil.LineSeparate(Resources.ToolInstaller_UnpackZipTool_The_selected_zip_file_is_not_a_valid_installable_tool_,
                            Resources.ToolInstaller_UnpackZipTool_Error__The__0__does_not_contain_a_valid__1__attribute_), messageDlg.Message, 2);
                messageDlg.OkDialog();
            });
            string toolNoIdentifier = TestFilesDir.GetTestPath("NoIdentifier.zip");
            RunDlg<MessageDlg>(() => configureToolsDlg.InstallZipTool(toolNoIdentifier), messageDlg =>
            {
                AssertEx.AreComparableStrings(TextUtil.LineSeparate(Resources.ToolInstaller_UnpackZipTool_The_selected_zip_file_is_not_a_valid_installable_tool_,
                            Resources.ToolInstaller_UnpackZipTool_Error__The__0__does_not_contain_a_valid__1__attribute_), messageDlg.Message, 2);
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
            RunDlg<LocateFileDlg>(() => configureToolsDlg.InstallZipTool(allCommandTypesPath), dlg => dlg.OkDialog());
            WaitForConditionUI(1*1000, ()=>configureToolsDlg.listTools.Items.Count == 3);
            RunUI(()=>
                {
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
                    Assert.IsTrue(configureToolsDlg.textArguments.Enabled);
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
            RunUI(() =>
                {
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.SaveTools();                    
                });

            string testSkylineReportsPath = TestFilesDir.GetTestPath("TestSkylineReports.zip");
            RunDlg<MessageDlg>(() => configureToolsDlg.InstallZipTool(testSkylineReportsPath), messageDlgReportNotProvided =>
                {
                    AssertEx.AreComparableStrings(
                        Resources.UnpackZipToolHelper_UnpackZipTool_The_tool___0___requires_report_type_titled___1___and_it_is_not_provided__Import_canceled_,
                        messageDlgReportNotProvided.Message, 2);
                    messageDlgReportNotProvided.OkDialog();
                });
            CheckUniqueReportImport(configureToolsDlg, "HelloWorld", 1, false);

            string uniqueReportPath = TestFilesDir.GetTestPath("UniqueReport.zip");
            RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.InstallZipTool(uniqueReportPath), resolveReportConflict =>
                {
                    string messageForm =
                        TextUtil.LineSeparate(
                            Resources
                                .ConfigureToolsDlg_OverwriteOrInParallel_This_installation_would_modify_the_report_titled__0_,
                            string.Empty, Resources.ConfigureToolsDlg_OverwriteOrInParallel_Do_you_wish_to_overwrite_or_install_in_parallel_);
                    AssertEx.AreComparableStrings(messageForm, resolveReportConflict.Message,1);
                    resolveReportConflict.CancelDialog();
                });
            CheckUniqueReportImport(configureToolsDlg, "HelloWorld", 1, false);

            RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.InstallZipTool(uniqueReportPath), resolveReportConflict2 =>
                {
                    string messageForm =
                        TextUtil.LineSeparate(
                            Resources
                                .ConfigureToolsDlg_OverwriteOrInParallel_This_installation_would_modify_the_report_titled__0_,
                            string.Empty, Resources.ConfigureToolsDlg_OverwriteOrInParallel_Do_you_wish_to_overwrite_or_install_in_parallel_);
                    AssertEx.AreComparableStrings(messageForm, resolveReportConflict2.Message, 1);                       
                    resolveReportConflict2.Btn0Click(); //Overwrite report
                });
            CheckUniqueReportImport(configureToolsDlg, "HelloWorld1", 2, false);

            RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.InstallZipTool(uniqueReportPath), resolveReportConflict3 =>
                {
                    string messageForm =
                            TextUtil.LineSeparate(
                                Resources.ConfigureToolsDlg_OverwriteOrInParallel_The_tool__0__is_already_installed_,
                                string.Empty,
                                Resources
                                    .ConfigureToolsDlg_OverwriteOrInParallel_Do_you_wish_to_reinstall_or_install_in_parallel_);
                    AssertEx.AreComparableStrings(messageForm, resolveReportConflict3.Message, 1);
                    resolveReportConflict3.BtnCancelClick(); //Cancel
                });
            CheckUniqueReportImport(configureToolsDlg, "HelloWorld1", 2, false);

            RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.InstallZipTool(uniqueReportPath),
                                      resolveReportConflict4 => resolveReportConflict4.Btn1Click());
            CheckUniqueReportImport(configureToolsDlg, "HelloWorld2", 3, true);

            string reportFromSkyPath = TestFilesDir.GetTestPath("TestSkylineReportSky.zip");
            RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.InstallZipTool(reportFromSkyPath), resolveReportConflict5 =>
            {
                string messageForm =
                    TextUtil.LineSeparate(
                        Resources
                            .ConfigureToolsDlg_OverwriteOrInParallel_This_installation_would_modify_the_report_titled__0_,
                        string.Empty, Resources.ConfigureToolsDlg_OverwriteOrInParallel_Do_you_wish_to_overwrite_or_install_in_parallel_);
                AssertEx.AreComparableStrings(messageForm, resolveReportConflict5.Message, 1);
                resolveReportConflict5.Btn0Click(); //Overwrite report
            });
            CheckUniqueReportImport(configureToolsDlg, "HelloWorld", 1, true);

            string reportFromBothPath = TestFilesDir.GetTestPath("TestSkylineReportBoth.zip");
            RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.InstallZipTool(reportFromBothPath), resolveReportConflict6 =>
            {
                string messageForm =
                    TextUtil.LineSeparate(
                        Resources
                            .ConfigureToolsDlg_OverwriteOrInParallel_This_installation_would_modify_the_report_titled__0_,
                        string.Empty, Resources.ConfigureToolsDlg_OverwriteOrInParallel_Do_you_wish_to_overwrite_or_install_in_parallel_);
                AssertEx.AreComparableStrings(messageForm, resolveReportConflict6.Message, 1);
                resolveReportConflict6.Btn0Click(); //Overwrite report
            });
            CheckUniqueReportImport(configureToolsDlg, "HelloWorld",1, true);

            OkDialog(configureToolsDlg, configureToolsDlg.OkDialog);
        }
        

        private void CheckUniqueReportImport(ConfigureToolsDlg configureToolsDlg, string toolName, int expectedToolCount, bool deleteToolsWhenDone)
        {
            WaitForConditionUI(3 * 1000, () => configureToolsDlg.listTools.Items.Count == expectedToolCount);
            RunUI(() =>
            {
                Assert.AreEqual(expectedToolCount - 1, configureToolsDlg.listTools.SelectedIndex);
                Assert.AreEqual(toolName, configureToolsDlg.textTitle.Text);
                Assert.AreEqual("$(ToolDir)\\HelloWorld.exe", configureToolsDlg.textCommand.Text);
                Assert.AreEqual(string.Empty, configureToolsDlg.textArguments.Text);
                Assert.AreEqual(string.Empty, configureToolsDlg.textInitialDirectory.Text);
                Assert.AreEqual(CheckState.Checked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                Assert.AreEqual("UniqueReport", configureToolsDlg.comboReport.SelectedItem);
                var toolDir = configureToolsDlg.ToolDir;
                Assert.IsTrue(Directory.Exists(toolDir));
                if (deleteToolsWhenDone)
                {
                    configureToolsDlg.RemoveAllTools();
                }
                configureToolsDlg.SaveTools();
            });
        }

        private void ZipTestAnnotations()
        {
            var sampleId = new AnnotationDef("SampleID", // Not L10N
                                             AnnotationDef.AnnotationTargetSet.Singleton(
                                                 AnnotationDef.AnnotationTarget.replicate),
                                              AnnotationDef.AnnotationType.text,
                                             new List<string>());

            var isConc = new AnnotationDef("IS Conc", // Not L10N
                                           AnnotationDef.AnnotationTargetSet.Singleton(
                                               AnnotationDef.AnnotationTarget.replicate),
                                           AnnotationDef.AnnotationType.text,
                                           new List<string>());

            var analyteConcentration = new AnnotationDef("Analyte Concentration", // Not L10N
                                                         AnnotationDef.AnnotationTargetSet.Singleton(
                                                             AnnotationDef.AnnotationTarget.replicate),
                                                         AnnotationDef.AnnotationType.text,
                                                         new List<string>());

            var sampleIdTransition = new AnnotationDef("SampleID", // Not L10N
                                                       AnnotationDef.AnnotationTargetSet.Singleton(
                                                           AnnotationDef.AnnotationTarget.transition),
                                                       AnnotationDef.AnnotationType.text,
                                                       new List<string>());

            // Test proper loading of annotations
            string testAnnotationsPath = TestFilesDir.GetTestPath("TestAnnotations.zip"); // Not L10N
            RunDlg<ConfigureToolsDlg>(() => SkylineWindow.ShowConfigureToolsDlg(), dlg =>
            {
                dlg.RemoveAllTools();
                dlg.InstallZipTool(testAnnotationsPath);
                WaitForConditionUI(3 * 1000, () => dlg.ToolList.Count == 4);

                ToolDescription t0 = dlg.ToolList[0];
                ToolDescription t1 = dlg.ToolList[1];
                ToolDescription t2 = dlg.ToolList[2];
                ToolDescription t3 = dlg.ToolList[3];

                AssertEx.AreEqualDeep(t0.Annotations, new List<AnnotationDef>());
                AssertEx.AreEqualDeep(t1.Annotations,
                                      new List<AnnotationDef>
                                              {
                                                  sampleId,
                                                  isConc,
                                                  analyteConcentration
                                              });
                AssertEx.AreEqualDeep(t2.Annotations, new List<AnnotationDef> { sampleId });
                AssertEx.AreEqualDeep(t3.Annotations, new List<AnnotationDef>());
                Assert.IsTrue(Settings.Default.AnnotationDefList.Contains(sampleId));
                Assert.IsTrue(Settings.Default.AnnotationDefList.Contains(isConc));
                Assert.IsTrue(Settings.Default.AnnotationDefList.Contains(analyteConcentration));

                dlg.OkDialog();
            });

            // Test conflicting annotations
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            string conflictAnnotationsPath = TestFilesDir.GetTestPath("ConflictAnnotations.zip"); // Not L10N
            RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.InstallZipTool(conflictAnnotationsPath),
                                      messageDlg => messageDlg.Btn1Click()); // keep existing annotations
            WaitForConditionUI(3 * 1000, () => configureToolsDlg.ToolList.Count == 5);
            Assert.IsTrue(Settings.Default.AnnotationDefList.Contains(sampleId));
            RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.InstallZipTool(conflictAnnotationsPath),
                                      dlg => dlg.Btn0Click());
            {
                var messageDlg = WaitForOpenForm<MultiButtonMsgDlg>();
                OkDialog(messageDlg, messageDlg.Btn0Click);
            }
            // overwrite existing annotations
            Assert.IsTrue(Settings.Default.AnnotationDefList.Contains(sampleIdTransition));

            OkDialog(configureToolsDlg, configureToolsDlg.OkDialog);
            RunUI(() => SkylineWindow.PopulateToolsMenu());

            // Test running the tool with an unchecked annotation
            RunDlg<MessageDlg>(() => SkylineWindow.RunTool(4), dlg =>
            {
                Assert.AreEqual(TextUtil.LineSeparate(Resources.ToolDescription_VerifyAnnotations_This_tool_requires_the_use_of_the_following_annotations_which_are_not_enabled_for_this_document,
                                                             string.Empty,
                                                             TextUtil.LineSeparate(new Collection<string> { sampleId.GetKey() }),
                                                             string.Empty,
                                                             Resources.ToolDescription_VerifyAnnotations_Please_enable_these_annotations_and_fill_in_the_appropriate_data_in_order_to_use_the_tool_), dlg.Message);
                dlg.OkDialog();
            });

            // Test running the tool with a missing annotation
            Settings.Default.AnnotationDefList = new AnnotationDefList();
            WaitForCondition(3 * 1000, () => Settings.Default.AnnotationDefList.Count == 0);
            RunDlg<MessageDlg>(() => SkylineWindow.RunTool(4), dlg =>
            {
                Assert.AreEqual(TextUtil.LineSeparate(Resources.ToolDescription_VerifyAnnotations_This_tool_requires_the_use_of_the_following_annotations_which_are_missing_or_improperly_formatted,
                                                              string.Empty,
                                                              TextUtil.LineSeparate(new Collection<string> { sampleId.GetKey() }),
                                                              string.Empty,
                                                              Resources.ToolDescription_VerifyAnnotations_Please_re_install_the_tool_and_try_again_), dlg.Message);
                dlg.OkDialog();
            });

            // Clean-up
            RunDlg<ConfigureToolsDlg>(() => SkylineWindow.ShowConfigureToolsDlg(), dlg =>
            {
                dlg.RemoveAllTools();
                dlg.OkDialog();
            });
        }

        private void TestToolDirMacro()
        {
            string path1 = TestFilesDir.GetTestPath("TestToolDirMacro.zip");
            RunDlg<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg, configureToolsDlg =>
                {
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.InstallZipTool(path1);
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
            string argsCollector = string.Empty;
            RunDlg<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg, configureToolsDlg =>
                {
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.InstallZipTool(path1);
                    Assert.AreEqual("TestArgCollector", configureToolsDlg.textTitle.Text);
                    Assert.AreEqual("$(ToolDir)\\ArgstoOut.exe", configureToolsDlg.textCommand.Text);
                    Assert.AreEqual("SomeArgs $(CollectedArgs) SomeMoreArgs", configureToolsDlg.textArguments.Text);
                    Assert.AreEqual(string.Empty, configureToolsDlg.textInitialDirectory.Text);
                    Assert.AreEqual(CheckState.Checked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                    Assert.AreEqual(string.Empty, configureToolsDlg.comboReport.SelectedItem);
                    string toolDir = configureToolsDlg.ToolDir;
                    Assert.IsTrue(Directory.Exists(toolDir));
                    argsCollector = configureToolsDlg.ArgsCollectorPath;
                    string argscollectorCall = configureToolsDlg.ArgsCollectorType;
                    Assert.IsTrue(File.Exists(Path.Combine(toolDir, argsCollector)));
                    Assert.IsTrue(File.Exists(Path.Combine(toolDir, "ArgstoOut.exe")));
                    Assert.AreEqual("TestArgCollector.dll", Path.GetFileName(argsCollector));
                    Assert.AreEqual("TestArgCollector.ArgCollector", argscollectorCall);
                    configureToolsDlg.OkDialog();
                });
            WaitForCondition(3*1000, () => File.Exists(argsCollector));
            RunUI(() =>
                {
                    SkylineWindow.PopulateToolsMenu();
                    string toolText = SkylineWindow.GetToolText(0);
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