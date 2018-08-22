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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
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
            Settings.Default.ToolList.Clear();

            TestToolDescErrors();

            TestQuasarDeprecated();

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

        }

        private void TestToolDescErrors()
        {
            RunErrorCase(
                "Missing arg collector",    // Not L10N  
                @"C:\NotHere\Args.dll",     // Not L10N
                "NoClass",  // Not L10N
                Resources.ToolDescription_RunExecutableBackground_Error_running_the_installed_tool_0_It_seems_to_be_missing_a_file__Please_reinstall_the_tool_and_try_again_);

            string dllPath = TestFilesDir.GetTestPath("ExampleArgCollector.dll"); // Not L10N
            // Copy to bin directory rather than loading an assembly in the TestFilesDir, because we can't
            // unlock the file once we load the assembly, and the unit test base class tries to delete
            // TestFilesDir when the test is done.
            var skylineDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            string dllCopyPath = Path.Combine(skylineDir, Path.GetFileName(dllPath) ?? string.Empty);
            try
            {
                File.Copy(dllPath, dllCopyPath, true);
            }
// ReSharper disable once EmptyGeneralCatchClause
            catch (Exception)
            {
            }

            RunErrorCase(
                "Missing arg collector class",  // Not L10N
                dllCopyPath,
                "NoClass",  // Not L10N
                Resources.ToolDescription_RunExecutableBackground_Error_running_the_installed_tool__0___It_seems_to_have_an_error_in_one_of_its_files__Please_reinstall_the_tool_and_try_again);
            
            RunErrorCase(
                "Wrong arg collector class",  // Not L10N
                dllCopyPath,
                "ExampleArgCollector.ExampleToolUI",  // Not L10N
                Resources.ToolDescription_RunExecutableBackground_The_tool__0__had_an_error__it_returned_the_message_);
            
            // Clean up.
            Settings.Default.ToolList.Clear();
            RunUI(SkylineWindow.PopulateToolsMenu);
        }

        private void RunErrorCase(string toolName, string argCollector, string argCollectorClass, string errorMessage)
        {
            Settings.Default.ToolList.Add(new ToolDescription(
                toolName,
                "Not.here",
                string.Empty,
                string.Empty,
                true,
                null,
                argCollector,
                argCollectorClass,
                "NotHere",
                new List<AnnotationDef>(),
                "1.0",
                "urn:not:here",
                "NotHere"));
            RunUI(SkylineWindow.PopulateToolsMenu);
            RunDlg<MessageDlg>(() => SkylineWindow.RunTool(Settings.Default.ToolList.Count - 1),
                dlg =>
                {
                    AssertEx.Contains(dlg.Message, string.Format(errorMessage, toolName));
                    dlg.OkDialog();
                });
        }

        public class FakeWebHelper : IWebHelpers
        {
            public FakeWebHelper()
            {
                OpenLinkCalled = false;
                HttpPostCalled = false;
            }

            public string LastLinkUsed { get; private set; }
            public bool OpenLinkCalled { get; private set; }
            public bool HttpPostCalled { get; private set; }
            
            #region Implementation of IWebHelpers

            public void OpenLink(string link)
            {
                LastLinkUsed = link;
                OpenLinkCalled = true;
            }

            public void PostToLink(string link, string postData)
            {
                LastLinkUsed = link;
                HttpPostCalled = true;
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

        private void TestQuasarDeprecated()
        {
            Settings.Default.ToolList.Add(ToolList.DEPRECATED_QUASAR);

            RunUI(SkylineWindow.PopulateToolsMenu);
            RunDlg<MessageDlg>(() => SkylineWindow.RunTool(0), messageDlg =>
            {
                Assert.AreEqual(messageDlg.Message, TextUtil.LineSeparate(
                            Resources.ToolDescription_RunTool_Support_for_the_GenePattern_version_of_QuaSAR_has_been_discontinued_,
                            Resources.ToolDescription_RunTool_Please_check_the_External_Tools_Store_on_the_Skyline_web_site_for_the_most_recent_version_of_the_QuaSAR_external_tool_));
                messageDlg.OkDialog();
            });

            Settings.Default.ToolList.Clear();
        }

        private void TestHttpPost()
        {            
            var fakeWebHelper = new FakeWebHelper();
            const string googleUrl = "http://www.google.com"; // Not L10N
            const string param1 = "param1=test";    // Not L10N
            const string param2 = "param2=test";    // Not L10N
            const string querySep = "?";    // Not L10N
            const string paramSep = "&";    // Not L10N
            Settings.Default.PersistedViews.SetViewSpecList(PersistedViews.ExternalToolsGroup.Id, 
                Settings.Default.PersistedViews.GetViewSpecList(PersistedViews.MainGroup.Id));
            RunDlg<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg, configureToolsDlg =>
                {
                    //Remove all tools.
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.AddDialog("OpenLinkTest", googleUrl, _empty, _empty, false, _empty); // Not L10N
                    configureToolsDlg.AddDialog("OpenLinkParamsTest", googleUrl, param1, _empty, false, _empty); // Not L10N);
                    configureToolsDlg.AddDialog("HttpPostTest", googleUrl, _empty, _empty, false, // Not L10N
                                                Resources.ReportSpecList_GetDefaults_Transition_Results);
                    configureToolsDlg.AddDialog("HttpPostParamsTest", googleUrl + "?" + param1, param2, _empty, false, // Not L10N
                                                Resources.ReportSpecList_GetDefaults_Transition_Results);
                    Assert.AreEqual(4, configureToolsDlg.ToolList.Count);
                    configureToolsDlg.OkDialog();
                });

            RunUI(() =>
                {
                    Settings.Default.ToolList[0].WebHelpers = fakeWebHelper;
                    Settings.Default.ToolList[1].WebHelpers = fakeWebHelper;
                    Settings.Default.ToolList[2].WebHelpers = fakeWebHelper;

                    SkylineWindow.PopulateToolsMenu();
                    Assert.IsFalse(fakeWebHelper.OpenLinkCalled);
                    SkylineWindow.RunTool(0);
                    Assert.IsFalse(fakeWebHelper.LastLinkUsed.Contains(querySep));
                    Assert.IsTrue(fakeWebHelper.OpenLinkCalled);
                    Assert.IsFalse(fakeWebHelper.HttpPostCalled);
                    SkylineWindow.RunTool(1);
                    Assert.AreEqual(googleUrl + querySep + param1, fakeWebHelper.LastLinkUsed);
                    Assert.IsTrue(fakeWebHelper.OpenLinkCalled);
                    Assert.IsFalse(fakeWebHelper.HttpPostCalled);
                    SkylineWindow.RunTool(2);
                });
            // The post now happens on a background thread, since report export can take a long time
            WaitForCondition(() => fakeWebHelper.HttpPostCalled);
            Assert.IsFalse(fakeWebHelper.LastLinkUsed.Contains(querySep));
            var fakeWebHelper3 = new FakeWebHelper();
            RunUI(() =>
            {
                Settings.Default.ToolList[3].WebHelpers = fakeWebHelper3;

                SkylineWindow.PopulateToolsMenu();
                SkylineWindow.RunTool(3);
            });
            WaitForCondition(() => fakeWebHelper3.HttpPostCalled);
            Assert.AreEqual(googleUrl + querySep + param1 + paramSep + param2, fakeWebHelper3.LastLinkUsed);
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
            string exePath = TestFilesDir.GetTestPath("ShortStdinToStdout.exe"); // Not L10N
            // ShortStdinToStdout outputs the string provided to it. the string can come either as an argument or from stdin.
            WaitForCondition(10*60*1000, () => File.Exists(exePath));
            RunDlg<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg, configureToolsDlg =>
                {
                    Assert.IsTrue(File.Exists(exePath));
                    //Remove all tools.
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.AddDialog("ImWindowTest", exePath, _empty, _empty, true,
                                                Resources.ReportSpecList_GetDefaults_Peptide_RT_Results); // Report passed via stdin. // Not L10N
                    configureToolsDlg.AddDialog("ImWindowTestWithMacro", exePath, "\"" + ToolMacros.INPUT_REPORT_TEMP_PATH + "\"",
                                                _empty, true, Resources.ReportSpecList_GetDefaults_Transition_Results);
                    // Report passed as an argument. // Not L10N
                    Assert.AreEqual(2, configureToolsDlg.ToolList.Count);
                    configureToolsDlg.OkDialog();
                });
            RunUI(() =>
                {
                    SkylineWindow.PopulateToolsMenu();
                    Assert.AreEqual("ImWindowTest", SkylineWindow.GetToolText(0)); // Not L10N
                    Assert.AreEqual("ImWindowTestWithMacro", SkylineWindow.GetToolText(1)); // Not L10N
                    SkylineWindow.RunTool(0);                                               
                });
            string reportText =
                "Peptide,Protein,Replicate,PredictedRetentionTime,PeptideRetentionTime,PeptidePeakFoundRatio"; // Not L10N
            WaitForConditionUI(30*1000, () => SkylineWindow.ImmediateWindow != null);
            WaitForConditionUI(() => SkylineWindow.ImmediateWindow.TextContent.Contains(reportText));
            RunUI(() =>
            {
                SkylineWindow.ImmediateWindow.Clear();
                SkylineWindow.RunTool(1);
            });
            string reportText1 =
                "Peptide,Protein,Replicate,PrecursorMz,PrecursorCharge,ProductMz,ProductCharge,FragmentIon,RetentionTime,Area,Background,PeakRank"; // Not L10N
            string actualText=String.Empty;
            if (!TryWaitForConditionUI(() => (actualText=SkylineWindow.ImmediateWindow.TextContent).Contains(reportText1)))
            {
                Assert.Fail(@"ImmediateWindow.TextContent ""{0}"" does not contain expected string ""{1}""", actualText??String.Empty, reportText1);
            }
            // Make sure the running EXE does not cause test to fail, because it is locked.
            WaitForCondition(() =>
            {
                try
                {
                    File.Delete(exePath);
                    return !File.Exists(exePath);
                }
                catch (Exception)
                {
                    return false;
                }
            });
            RunUI(() => SkylineWindow.ImmediateWindow.Clear());            
        }

        private void TestURL()
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
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
                          Assert.IsTrue(configureToolsDlg.textArguments.Enabled);
                          Assert.IsFalse(configureToolsDlg.textInitialDirectory.Enabled);
                          Assert.IsFalse(configureToolsDlg.cbOutputImmediateWindow.Enabled);
                          Assert.IsTrue(configureToolsDlg.comboReport.Enabled);
                          Assert.AreEqual(_empty,configureToolsDlg.comboReport.SelectedItem);
                          Assert.IsFalse(configureToolsDlg.btnFindCommand.Enabled);
                          Assert.IsTrue(configureToolsDlg.btnArguments.Enabled);
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
                      });
            OkDialog(configureToolsDlg, configureToolsDlg.OkDialog);
        }

        private void TestMacros()
        {
            RunDlg<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg, configureToolsDlg =>
            {
                configureToolsDlg.RemoveAllTools();
                configureToolsDlg.AddDialog(EXAMPLE, EXAMPLE_EXE, _empty, _empty);
                Assert.AreEqual(_empty, configureToolsDlg.textArguments.Text);
                configureToolsDlg.ClickMacro(configureToolsDlg.MacroListArguments, 0);
                string shortText = configureToolsDlg.MacroListArguments[0].ShortText; 
                Assert.AreEqual(shortText, configureToolsDlg.textArguments.Text);
                Assert.AreEqual(_empty, configureToolsDlg.textInitialDirectory.Text); 
                string shortText2 = configureToolsDlg.MacroListInitialDirectory[0].ShortText; 
                configureToolsDlg.ClickMacro(configureToolsDlg.MacroListInitialDirectory, 0);
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
                    Assert.AreEqual(string.Format(Resources.ConfigureToolsDlg_GetTitle__New_Tool_0__, 1), configureToolsDlg.ToolList[0].Title); // Not L10N
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
                var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
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
                        SkylineWindow.ToolMenuItem example3 = (SkylineWindow.ToolMenuItem) mi2.DropDownItems[0];
                        Assert.AreEqual(EXAMPLE3_EXE, example3.Command);

                        Assert.AreEqual(EXAMPLE2, mi.DropDownItems[2].Text);
                        SkylineWindow.ToolMenuItem example2 = (SkylineWindow.ToolMenuItem) mi.DropDownItems[2];
                        Assert.AreEqual(EXAMPLE2_EXE, example2.Command);

                        Assert.IsTrue(SkylineWindow.ConfigMenuPresent());

                        configureToolsDlg.RemoveAllTools();
                        // Now the tool list is empty.
                        Assert.IsFalse(configureToolsDlg.btnRemove.Enabled);
                    });
                OkDialog(configureToolsDlg, configureToolsDlg.OkDialog);
                RunUI(() =>
                    {
                        SkylineWindow.PopulateToolsMenu();
                        Assert.IsTrue(SkylineWindow.ConfigMenuPresent());
                    });
            }
        }

        private void TestButtons()
        {
            {
                var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
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
                WaitForConditionUI(3*1000, () => configureToolsDlg.listTools.SelectedIndex == 2 );
                RunUI(()=>
                {
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
                WaitForClosedForm(configureToolsDlg);
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
                WaitForClosedForm(configureToolsDlg);
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
                });
                OkDialog(configureToolsDlg, configureToolsDlg.OkDialog);
                RunUI(()=>
                {
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
            WaitForClosedForm(configureToolsDlg);
//            RunUI(() => Assert.IsFalse(configureToolsDlg.btnApply.Enabled));
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
            RunUI(configureToolsDlg.Remove);
            OkDialog(configureToolsDlg, configureToolsDlg.OkDialog);
        }

        private void TestSaveDialogNo()
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunUI((() =>
                {
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.Add();
                    configureToolsDlg.textTitle.Text = EXAMPLE1; 
                    configureToolsDlg.Remove();
                }));
            
            RunDlg<MultiButtonMsgDlg>(configureToolsDlg.Cancel, messageDlg =>
            {
                AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_Cancel_Do_you_wish_to_Save_changes_, messageDlg.Message, 0);
                messageDlg.Btn1Click();
            }); 

            WaitForClosedForm(configureToolsDlg);
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
            WaitForClosedForm(configureToolsDlg);
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
            });
            OkDialog(configureToolsDlg, configureToolsDlg.OkDialog);
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
            WaitForClosedForm(configureToolsDlg);
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
            RunUI(() => configureToolsDlg.AddDialog(string.Format(Resources.ConfigureToolsDlg_GetTitle__New_Tool_0__, 1), EXAMPLE1_EXE, _empty, _empty));
            RunDlg<MultiButtonMsgDlg>(configureToolsDlg.Add, messageDlg =>
            {
                AssertEx.AreComparableStrings(Resources.ConfigureToolsDlg_CheckPassTool__The_command_for__0__may_not_exist_in_that_location__Would_you_like_to_edit_it__, messageDlg.Message, 1);
                messageDlg.BtnCancelClick();
            });
            RunUI(configureToolsDlg.RemoveAllTools);
            OkDialog(configureToolsDlg, configureToolsDlg.OkDialog);
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
            WaitForClosedForm(configureToolsDlg);
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
                Settings.Default.ToolList.Add(new ToolDescription(EXAMPLE2, EXAMPLE2_EXE, "$(DocumentPath)", "$(DocumentDir)", false, string.Empty)); // Not L10N

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
