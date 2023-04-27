/*
 * Original author: Trevor Killeen <killeent .at. u.washington.edu>,
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
using System.IO;
using System.Linq;
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
    public class ToolUpdatesTest : AbstractFunctionalTest 
    {
        /// <summary>
        /// Functional test for the tool updates dlg.
        /// </summary>
        [TestMethod]
        public void TestToolUpdates()
        {
            TestFilesZip = @"TestFunctional\ToolUpdatesTest.zip"; // Not L10N
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // clear out any existing tools
            Settings.Default.ToolList.Clear();
            
            TestMenuItemAvailability();
            TestDeselectAll();
            TestDownload();
            TestUpdate();
            TestStartToFinish();
        }

        /// <summary>
        /// Tests that the Updates... tool menu item is only enabled when there are updates available
        /// </summary>
        private static void TestMenuItemAvailability()
        {
            // add a sample tool to the list
            Settings.Default.ToolList.Add(FULLY_UPDATED_TOOL);
            // test the menu item is not enabled if there are no updates available
            RunUI(() =>
                {
                    SkylineWindow.PopulateToolsMenu();
                    Assert.IsFalse(SkylineWindow.UpdatesMenuEnabled());
                });

            // test the menu item is enabled if there are updates available
            Settings.Default.ToolList.Add(SAMPLE_TOOL);
            RunUI(() =>
                {
                    SkylineWindow.PopulateToolsMenu();
                    Assert.IsTrue(SkylineWindow.UpdatesMenuEnabled());
                });

            Settings.Default.ToolList.Clear();
        }

        /// <summary>
        /// Tests for the message dialog that appears if you click the install button with no tools selected
        /// </summary>
        private static void TestDeselectAll()
        {
            Settings.Default.ToolList.Add(SAMPLE_TOOL);
            var toolUpdatesDlg = FormatToolUpdatesDlg(false, FormatUpdateHelper(FormatToolStoreClient(false)), false);
            var messageDlg = ShowDialog<MessageDlg>(toolUpdatesDlg.OkDialog);
            Assert.AreEqual(Resources.ToolUpdatesDlg_btnUpdate_Click_Please_select_at_least_one_tool_to_update_, messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            OkDialog(toolUpdatesDlg, toolUpdatesDlg.CancelDialog);
            Settings.Default.ToolList.Clear();
        }

        private static void TestDownload()
        {
            TestDownloadFailure();
            TestDownloadSuccess();
        }

        /// <summary>
        /// Tests for failing to download updates for a tool.
        /// </summary>
        private static void TestDownloadFailure()
        {
            Settings.Default.ToolList.Add(SAMPLE_TOOL);
            var toolUpdatesDlg = FormatToolUpdatesDlg(true, FormatUpdateHelper(FormatToolStoreClient(false)) , true);
            var messageDlg = ShowDialog<MessageDlg>(toolUpdatesDlg.OkDialog);
            Assert.AreEqual(
                TextUtil.LineSeparate(
                    Resources
                        .ToolUpdatesDlg_DisplayDownloadSummary_Failed_to_download_updates_for_the_following_packages,
                    string.Empty, SAMPLE_TOOL.PackageName), messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm(toolUpdatesDlg);
            Settings.Default.ToolList.Clear();
        }

        /// <summary>
        /// Tests for successfully downloading an update for a tool.
        /// </summary>
        private static void TestDownloadSuccess()
        {
            Settings.Default.ToolList.Add(SAMPLE_TOOL);
            var toolUpdatesDlg = FormatToolUpdatesDlg(true, FormatUpdateHelper(FormatToolStoreClient(true)), true);
            OkDialog(toolUpdatesDlg, toolUpdatesDlg.OkDialog);
            Settings.Default.ToolList.Clear();
        }

        private static void TestUpdate()
        {
            TestUpdateFailure();
            TestUpdateSuccess();
        }

        private static void TestUpdateFailure()
        {
            TestUpdateFailureIOException();
            TestUpdateFailureMessageException();
            TestUpdateFailureUserCancel();
        }

        private const string EXCEPTION_MESSAGE = "Message"; // Not L10N

        /// <summary>
        /// Test for failing to update a tool due to an IOException during installation.
        /// </summary>
        private static void TestUpdateFailureIOException()
        {
            Settings.Default.ToolList.Add(SAMPLE_TOOL);
            var toolUpdatesDlg = FormatToolUpdatesDlg(true,
                                                      FormatUpdateHelper(FormatToolStoreClient(true),
                                                                         CreateTestInstallFunction(new IOException(EXCEPTION_MESSAGE), false)),
                                                      false);
            var messageDlg = ShowDialog<MessageDlg>(toolUpdatesDlg.OkDialog);
            Assert.AreEqual(
                TextUtil.LineSeparate(
                    Resources.ToolUpdatesDlg_DisplayInstallSummary_Failed_to_update_the_following_tool, string.Empty,
                    ToolUpdatesDlg.FormatFailureMessage(SAMPLE_TOOL.PackageName, TextUtil.LineSeparate(
                        string.Format(
                            Resources
                                .ConfigureToolsDlg_UnpackZipTool_Failed_attempting_to_extract_the_tool_from__0_,
                            string.Empty), EXCEPTION_MESSAGE))), messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm(toolUpdatesDlg);
            Settings.Default.ToolList.Clear();
        }

        /// <summary>
        /// Test for failing to update a tool due to a ToolExecutionException during installation.
        /// </summary>
        private static void TestUpdateFailureMessageException()
        {
            Settings.Default.ToolList.Add(SAMPLE_TOOL);
            var toolUpdatesDlg = FormatToolUpdatesDlg(true,
                                                      FormatUpdateHelper(FormatToolStoreClient(true),
                                                                         CreateTestInstallFunction(new ToolExecutionException(EXCEPTION_MESSAGE), false)),
                                                      false);
            var messageDlg = ShowDialog<MessageDlg>(toolUpdatesDlg.OkDialog);
            Assert.AreEqual(
                TextUtil.LineSeparate(
                    Resources.ToolUpdatesDlg_DisplayInstallSummary_Failed_to_update_the_following_tool, string.Empty,
                    ToolUpdatesDlg.FormatFailureMessage(SAMPLE_TOOL.PackageName, EXCEPTION_MESSAGE)), messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm(toolUpdatesDlg);
            Settings.Default.ToolList.Clear();
        }

        /// <summary>
        /// Tests for failing to update a tool due to the user cancelling.
        /// </summary>
        private static void TestUpdateFailureUserCancel()
        {
            Settings.Default.ToolList.Add(SAMPLE_TOOL);
            var toolUpdatesDlg = FormatToolUpdatesDlg(true,
                                                      FormatUpdateHelper(FormatToolStoreClient(true),
                                                                         CreateTestInstallFunction(null, true)),
                                                      false);
            var messageDlg = ShowDialog<MessageDlg>(toolUpdatesDlg.OkDialog);
            Assert.AreEqual(
                TextUtil.LineSeparate(
                    Resources.ToolUpdatesDlg_DisplayInstallSummary_Failed_to_update_the_following_tool, string.Empty,
                    ToolUpdatesDlg.FormatFailureMessage(SAMPLE_TOOL.PackageName,
                                                        Resources
                                                            .ToolUpdatesDlg_InstallUpdates_User_cancelled_installation)),
                messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm(toolUpdatesDlg);
            Settings.Default.ToolList.Clear();
        }

        /// <summary>
        /// Tests for successfully updating a tool.
        /// </summary>
        private static void TestUpdateSuccess()
        {
            Settings.Default.ToolList.Add(SAMPLE_TOOL);
            var toolUpdatesDlg = FormatToolUpdatesDlg(true,
                                                      FormatUpdateHelper(FormatToolStoreClient(true),
                                                                         CreateTestInstallFunction(null, false)),
                                                      false);
            var messageDlg = ShowDialog<MessageDlg>(toolUpdatesDlg.OkDialog);
            Assert.AreEqual(
                TextUtil.LineSeparate(
                    Resources.ToolUpdatesDlg_DisplayInstallSummary_Successfully_updated_the_following_tool,
                    string.Empty, SAMPLE_TOOL.PackageName), messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm(toolUpdatesDlg);
            Settings.Default.ToolList.Clear();
        }

        /// <summary>
        /// Tests installing tools using actual zip files from start to finish. Tool downloading is still "faked," but
        /// the update process uses the actual behavior of Skyline, illustrating the ability to interact with messages
        /// shown by the tool installation process.
        /// 
        /// Perhaps most importantly, this also tests that tools that are updated have their "UpdateAvailable" field
        /// set to true if there are updates available, and then set to false if updated successfully.
        /// </summary>
        private void TestStartToFinish()
        {
            TestUpdateSingleTool();
            TestUpdateNestedTool();
            TestMultipleFailures();
            TestOneSuccessOneFailure();
            TestMultipleSuccesses();
        }

        /// <summary>
        /// Tests the start to finish update of a single tool.
        /// </summary>
        private void TestUpdateSingleTool()
        {
            Settings.Default.ToolList.Add(SAMPLE_TOOL);
            var toolUpdatesDlg = FormatToolUpdatesDlg(true, FormatUpdateHelper(FormatToolStoreClient(true, TestFilesDir.FullPath)), false);
            var multiBtnMsgDlg = ShowDialog<MultiButtonMsgDlg>(toolUpdatesDlg.OkDialog);
            var messageDlg = ShowDialog<MessageDlg>(multiBtnMsgDlg.Btn0Click);
            Assert.AreEqual(
                TextUtil.LineSeparate(
                    Resources.ToolUpdatesDlg_DisplayInstallSummary_Successfully_updated_the_following_tool,
                    string.Empty, SAMPLE_TOOL.PackageName), messageDlg.Message);
            foreach (var tool in Settings.Default.ToolList.Where(tool => Equals(tool.PackageIdentifier, SAMPLE_TOOL.PackageIdentifier)))
            {
                Assert.IsFalse(tool.UpdateAvailable);
            }
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm(toolUpdatesDlg);
            Settings.Default.ToolList.Clear();
        }

        /// <summary>
        /// Tests the start to finish installation of a nested tool. 
        /// </summary>
        private void TestUpdateNestedTool()
        {
            Settings.Default.ToolList.AddRange(new [] {NESTED_TOOL_A, NESTED_TOOL_B});
            var toolUpdatesDlg = FormatToolUpdatesDlg(true, FormatUpdateHelper(FormatToolStoreClient(true, TestFilesDir.FullPath)), false);
            Assert.AreEqual(1, toolUpdatesDlg.ItemCount);
            var multiBtnMsgDlg = ShowDialog<MultiButtonMsgDlg>(toolUpdatesDlg.OkDialog);
            var messageDlg = ShowDialog<MessageDlg>(multiBtnMsgDlg.Btn0Click);
            Assert.AreEqual(
                TextUtil.LineSeparate(
                    Resources.ToolUpdatesDlg_DisplayInstallSummary_Successfully_updated_the_following_tool,
                    string.Empty, NESTED_TOOL_A.PackageName), messageDlg.Message);
            foreach (var tool in Settings.Default.ToolList.Where(tool => Equals(tool.PackageIdentifier, NESTED_TOOL_A.PackageIdentifier)))
            {
                Assert.IsFalse(tool.UpdateAvailable);
            }
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm(toolUpdatesDlg);
            Settings.Default.ToolList.Clear();
        }

        /// <summary>
        /// Tests proper execution when multiple tools are not successfully updated.
        /// </summary>
        private void TestMultipleFailures()
        {
            var messageDlg = FormatInstallSummaryMessageDlg(false, false);
            Assert.AreEqual(
                TextUtil.LineSeparate(
                    Resources.ToolUpdatesDlg_DisplayInstallSummary_Failed_to_update_the_following_tools, string.Empty,
                    TextUtil.LineSeparate(
                        ToolUpdatesDlg.FormatFailureMessage(NESTED_TOOL_A.PackageName, Resources.ToolUpdatesDlg_InstallUpdates_User_cancelled_installation),
                        ToolUpdatesDlg.FormatFailureMessage(SAMPLE_TOOL.PackageName, Resources.ToolUpdatesDlg_InstallUpdates_User_cancelled_installation))),
                messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm<ToolUpdatesDlg>();
            AssertUpdateAvailability(Settings.Default.ToolList, true, true);
            Settings.Default.ToolList.Clear();
        }

        /// <summary>
        /// Tests proper execution when one tool is updated successfully but the other is not.
        /// </summary>
        private void TestOneSuccessOneFailure()
        {
            var messageDlg = FormatInstallSummaryMessageDlg(false, true);
            Assert.AreEqual(
                TextUtil.LineSeparate(
                    Resources.ToolUpdatesDlg_DisplayInstallSummary_Successfully_updated_the_following_tool, string.Empty,
                    SAMPLE_TOOL.PackageName, string.Empty,
                    Resources.ToolUpdatesDlg_DisplayInstallSummary_Failed_to_update_the_following_tool, string.Empty,
                    ToolUpdatesDlg.FormatFailureMessage(NESTED_TOOL_A.PackageName,
                                                        Resources.ToolUpdatesDlg_InstallUpdates_User_cancelled_installation)), messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm<ToolUpdatesDlg>();
            AssertUpdateAvailability(Settings.Default.ToolList, false, true);
            Settings.Default.ToolList.Clear();
        }

        /// <summary>
        /// Tests proper execution when multiple tools are updated successfully.
        /// </summary>
        private void TestMultipleSuccesses()
        {
            var messageDlg = FormatInstallSummaryMessageDlg(true, true);
            Assert.AreEqual(
                TextUtil.LineSeparate(
                    Resources.ToolUpdatesDlg_DisplayInstallSummary_Successfully_updated_the_following_tools,
                    string.Empty, TextUtil.LineSeparate(NESTED_TOOL_A.PackageName, SAMPLE_TOOL.PackageName)), messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm<ToolUpdatesDlg>();
            AssertUpdateAvailability(Settings.Default.ToolList, false, false);
            Settings.Default.ToolList.Clear();
        }

        /// <summary>
        /// Used for testing successfully installing multiple tools, failing to install multiple tools, or successfully installing one tool while
        /// failing to install the other. This method formats the summary message dialog created by the Tool Updater.
        /// </summary>
        /// <param name="nestedSuccess">If true, the nested tool will be installed successfully. If false, it will not be installed successfully, because
        /// of a user cancellation.</param>
        /// <param name="sampleSuccess">If true, the sample tool will be installed successfully. If false, it will not be installed successfully, because
        /// of a user cancellation.</param>
        private MessageDlg FormatInstallSummaryMessageDlg(bool nestedSuccess, bool sampleSuccess)
        {
            Settings.Default.ToolList.AddRange(new[] { NESTED_TOOL_A, NESTED_TOOL_B, SAMPLE_TOOL });
            var toolUpdatesDlg = FormatToolUpdatesDlg(true, FormatUpdateHelper(FormatToolStoreClient(true, TestFilesDir.GetTestPath("TestOneSuccessOneFailure"))), false); // Not L10N
            Assert.AreEqual(2, toolUpdatesDlg.ItemCount);
            var multiBtnMsgDlgNested = ShowDialog<MultiButtonMsgDlg>(toolUpdatesDlg.OkDialog);
            Assert.IsTrue(multiBtnMsgDlgNested.Message.Contains(NESTED_TOOL_A.PackageName));
            if (nestedSuccess)
            {
                OkDialog(multiBtnMsgDlgNested, multiBtnMsgDlgNested.Btn0Click);
            }
            else
            {
                OkDialog(multiBtnMsgDlgNested, multiBtnMsgDlgNested.BtnCancelClick);
            }
            var multiBtnMsgDlgSample = WaitForOpenForm<MultiButtonMsgDlg>();
            Assert.IsTrue(multiBtnMsgDlgSample.Message.Contains(SAMPLE_TOOL.PackageName));
            if (sampleSuccess)
            {
                OkDialog(multiBtnMsgDlgNested, multiBtnMsgDlgSample.Btn0Click);
            }
            else
            {
                OkDialog(multiBtnMsgDlgNested, multiBtnMsgDlgSample.BtnCancelClick);
            }
            return WaitForOpenForm<MessageDlg>();
        }

        /// <summary>
        /// Asserts that updates are available or not for the sample tool and nested tool, from a given toollist.
        /// </summary>
        private static void AssertUpdateAvailability(ToolList toolList, bool sampleToolUpdateAvailable, bool nestedToolUpdateAvailable)
        {
            var sampleTool =
                 Settings.Default.ToolList.First(description => description.PackageIdentifier.Equals(SAMPLE_TOOL.PackageIdentifier));
            var nestedTools =
                Settings.Default.ToolList.Where(description => description.PackageIdentifier.Equals(NESTED_TOOL_A.PackageIdentifier));

            Assert.IsNotNull(sampleTool);
            Assert.IsNotNull(nestedTools);

            Assert.AreEqual(sampleToolUpdateAvailable, sampleTool.UpdateAvailable);
            foreach (var nestedTool in nestedTools)
            {
                Assert.AreEqual(nestedToolUpdateAvailable, nestedTool.UpdateAvailable);
            }
        }

        /// <summary>
        /// Formats a ToolUpdatesDlg form for use in testing.
        /// </summary>
        /// <param name="itemsSelected">If true, this will check all the tools listed in the ToolUpdatesDlg. If false, it will uncheck all of them.</param>
        /// <param name="updateHelper">The update helper for the ToolUpdatesDlg.</param>
        /// <param name="testingDownloadOnly">If true, it will only test the DownloadTools function of the ToolUpdatesDlg.</param>
        private static ToolUpdatesDlg FormatToolUpdatesDlg(bool itemsSelected, IToolUpdateHelper updateHelper, bool testingDownloadOnly)
        {
            var toolUpdatesDlg = ShowDialog<ToolUpdatesDlg>(() => SkylineWindow.ShowToolUpdatesDlg(updateHelper));
            if (itemsSelected)
                RunUI(toolUpdatesDlg.SelectAll);
            else
                RunUI(toolUpdatesDlg.DeselectAll);
            toolUpdatesDlg.TestingDownloadOnly = testingDownloadOnly;
            return toolUpdatesDlg;
        }

        /// <summary>
        /// Formats a TestToolStoreClient for use in testing.
        /// </summary>
        /// <param name="downloadSuccess">If true, the "fake" download process will successfully download tool zips. If false, it emulate failing to download zips.</param>
        /// <param name="filePath">When using a folder on the local machine as the source for package updates, set this value to the path to that folder. If this value is null,
        /// when the tool calls the GetToolZipFile function of the the test client, it will an empty string.</param>
        private static IToolStoreClient FormatToolStoreClient(bool downloadSuccess, string filePath = null)
        {
            return new TestToolStoreClient(filePath ?? Path.GetTempPath())
                {
                    FailDownload = !downloadSuccess,
                    TestDownloadPath = filePath == null ? string.Empty : null
                };
        }

        /// <summary>
        /// Because tool installation is tested elsewhere (InstallToolsTest), we want to mimic the installation behavior
        /// of tools. Specifically, we mimic the failure of installations by supporting exception throwing, and by
        /// user cancellation. If the exception is null, and userCancelled is false, returns an empty UnzipToolReturnAccumulator.
        /// </summary>
        /// <param name="exception">If non-null, this function will throw this exception when called.</param>
        /// <param name="userCancelled">If true, and no exception is specifed, this function will return null,
        /// indicating the user cancelled the installation.</param>
        /// <returns>A function that implements the above behavior.</returns>
        private static Func<string, IUnpackZipToolSupport, ToolInstaller.UnzipToolReturnAccumulator>
            CreateTestInstallFunction(Exception exception, bool userCancelled)
        {
            return (s, support) =>
            {
                if (exception != null)
                    throw exception;

                if (userCancelled)
                    return null;

                return new ToolInstaller.UnzipToolReturnAccumulator();
            };
        }
        
        /// <summary>
        /// Formats a ToolUpdateHelper for testing. If no unpackZipTool function is specified, it uses the default ToolInstaller one.
        /// </summary>
        private static IToolUpdateHelper FormatUpdateHelper(IToolStoreClient client,
                                                            Func<string, IUnpackZipToolSupport, ToolInstaller.UnzipToolReturnAccumulator> unpackZipTool = null)
        {
            return new TestToolUpdateHelper(client, unpackZipTool ?? ToolInstaller.UnpackZipTool);
        }

        private static readonly ToolDescription FULLY_UPDATED_TOOL = new ToolDescription("Fully Updated", "http://fullyupdated.com", string.Empty)  // Not L10N
            {
                PackageIdentifier = "FullyUpdated",     // Not L10N
                PackageName = "Fully Updated",          // Not L10N
                PackageVersion = "0.1",                 // Not L10N
                UpdateAvailable = false
            };

        private static readonly ToolDescription SAMPLE_TOOL = new ToolDescription("Sample Tool", "http://test.com", string.Empty)                   // Not L10N
            {
                PackageIdentifier = "SampleTool",       // Not L10N
                PackageName = "Sample Tool",            // Not L10N
                PackageVersion = "0.1",                 // Not L10N
                UpdateAvailable = true
            };

        private static readonly ToolDescription NESTED_TOOL_A = new ToolDescription("NestedTool\\A", "http://testA.com", string.Empty)
            {
                PackageIdentifier = "NestedTool",
                PackageName = "Nested Tool",
                PackageVersion = "0.1",
                UpdateAvailable = true
            };

        private static readonly ToolDescription NESTED_TOOL_B = new ToolDescription("NestedTool\\B", "http://testB.com", string.Empty)
            {
                PackageIdentifier = "NestedTool",
                PackageName = "Nested Tool",
                PackageVersion = "0.1",
                UpdateAvailable = true,
            };
    }

    public class TestToolUpdateHelper : IToolUpdateHelper
    {
        private readonly IToolStoreClient _client;
        private readonly Func<string, IUnpackZipToolSupport, ToolInstaller.UnzipToolReturnAccumulator> _unpackZipTool; 

        public TestToolUpdateHelper(IToolStoreClient client, Func<string, IUnpackZipToolSupport, ToolInstaller.UnzipToolReturnAccumulator> unpackZipTool)
        {
            _client = client;
            _unpackZipTool = unpackZipTool;
        }
        
        public ToolInstaller.UnzipToolReturnAccumulator UnpackZipTool(string pathToZip, IUnpackZipToolSupport unpackSupport)
        {
            return _unpackZipTool.Invoke(pathToZip, unpackSupport);
        }

        public string GetToolZipFile(ILongWaitBroker waitBroker, string packageIdentifier, string directory)
        {
            return _client.GetToolZipFile(waitBroker, packageIdentifier, directory);
        }
    }

}
