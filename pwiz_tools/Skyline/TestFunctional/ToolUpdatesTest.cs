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
using pwiz.Common.SystemUtil;
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
    public class ToolUpdatesTest : AbstractFunctionalTestEx
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
        private void TestDeselectAll()
        {
            using var context = new ToolUpdateTestContext(new[] { SAMPLE_TOOL }, CreateToolStoreClient(TestFilesDir.FullPath));
            var toolUpdatesDlg = context.ShowToolUpdatesDlg(itemsSelected: false);

            TestMessageDlgShown(toolUpdatesDlg.OkDialog, 
                Resources.ToolUpdatesDlg_btnUpdate_Click_Please_select_at_least_one_tool_to_update_);

            OkDialog(toolUpdatesDlg, toolUpdatesDlg.CancelDialog);
        }

        private void TestDownload()
        {
            TestDownloadFailure();
            TestDownloadCancel();
            TestDownloadSuccess();
            TestMultipleToolDownloadFailures();
        }

        /// <summary>
        /// Tests for failing to download updates for a tool.
        /// </summary>
        private static void TestDownloadFailure()
        {
            using var helper = HttpClientTestHelper.SimulateNoNetworkInterface();
            using var context = new ToolUpdateTestContext(new[] { SAMPLE_TOOL }, ToolStoreUtil.ToolStoreClient);
            var toolUpdatesDlg = context.ShowToolUpdatesDlg(testingDownloadOnly: true);

            TestMessageDlgShown(toolUpdatesDlg.OkDialog, 
                ToolUpdatesDlg.FormatDownloadFailureSummary(SAMPLE_TOOL.PackageName, helper.GetExpectedMessage()));
        }

        /// <summary>
        /// Tests for user canceling during download of tool updates.
        /// </summary>
        private static void TestDownloadCancel()
        {
            using var context = new ToolUpdateTestContext(new[] { SAMPLE_TOOL }, ToolStoreUtil.ToolStoreClient);
            var toolUpdatesDlg = context.ShowToolUpdatesDlg(testingDownloadOnly: true);
            
            // Should cancel silently - no MessageDlg shown
            TestHttpClientCancellation(toolUpdatesDlg.OkDialog);
        }

        /// <summary>
        /// Tests for successfully downloading an update for a tool.
        /// </summary>
        private void TestDownloadSuccess()
        {
            using var context = new ToolUpdateTestContext(new[] { SAMPLE_TOOL }, CreateToolStoreClient(TestFilesDir.FullPath));
            var toolUpdatesDlg = context.ShowToolUpdatesDlg(testingDownloadOnly: true);
            OkDialog(toolUpdatesDlg, toolUpdatesDlg.OkDialog);
        }

        /// <summary>
        /// Tests downloading multiple tools when all fail with the same network error.
        /// Verifies that error message is grouped (tool names listed, common error at bottom).
        /// </summary>
        private static void TestMultipleToolDownloadFailures()
        {
            var multipleTools = new[] { SAMPLE_TOOL, NESTED_TOOL_A, NESTED_TOOL_B };
            using var helper = HttpClientTestHelper.SimulateNoNetworkInterface();
            using var context = new ToolUpdateTestContext(multipleTools, ToolStoreUtil.ToolStoreClient);
            var toolUpdatesDlg = context.ShowToolUpdatesDlg(testingDownloadOnly: true);
            
            // All tools should fail with same network error - message should be grouped
            TestMessageDlgShown(toolUpdatesDlg.OkDialog, 
                ToolUpdatesDlg.FormatDownloadFailureSummary(
                    multipleTools.Select(t => t.PackageName).Distinct(),
                    helper.GetExpectedMessage()));
        }

        private void TestUpdate()
        {
            TestUpdateFailure();
            TestUpdateSuccess();
        }

        private void TestUpdateFailure()
        {
            TestUpdateFailureIOException();
            TestUpdateFailureMessageException();
            TestUpdateFailureUserCancel();
        }

        private const string EXCEPTION_MESSAGE = "Message"; // Not L10N

        /// <summary>
        /// Test for failing to update a tool due to an IOException during installation.
        /// </summary>
        private void TestUpdateFailureIOException()
        {
            var expectedErrorMessage = TextUtil.LineSeparate(
                Resources.ConfigureToolsDlg_UnpackZipTool_Failed_attempting_to_extract_the_tool, 
                EXCEPTION_MESSAGE);
            
            TestSingleToolInstall(
                CreateTestInstallFunction(new IOException(EXCEPTION_MESSAGE), false),
                FormatFailureMessage(SAMPLE_TOOL, expectedErrorMessage),
                isSuccess: false);
        }

        /// <summary>
        /// Test for failing to update a tool due to a ToolExecutionException during installation.
        /// </summary>
        private void TestUpdateFailureMessageException()
        {
            TestSingleToolInstall(
                CreateTestInstallFunction(new ToolExecutionException(EXCEPTION_MESSAGE), false),
                FormatFailureMessage(SAMPLE_TOOL, EXCEPTION_MESSAGE),
                isSuccess: false);
        }

        /// <summary>
        /// Tests for failing to update a tool due to the user cancelling.
        /// </summary>
        private void TestUpdateFailureUserCancel()
        {
            TestSingleToolInstall(
                CreateTestInstallFunction(null, true),
                FormatFailureMessage(SAMPLE_TOOL, Resources.ToolUpdatesDlg_InstallUpdates_User_cancelled_installation),
                isSuccess: false);
        }

        /// <summary>
        /// Tests for successfully updating a tool.
        /// </summary>
        private void TestUpdateSuccess()
        {
            TestSingleToolInstall(
                CreateTestInstallFunction(null, false),
                SAMPLE_TOOL.PackageName,
                isSuccess: true);
        }

        /// <summary>
        /// Helper method to test single tool installation with various outcomes.
        /// Eliminates repetitive setup/teardown code.
        /// </summary>
        /// <param name="unpackZipTool">Function to control install behavior (success, failure, cancellation)</param>
        /// <param name="expectedMessageContent">Expected content in the final message (tool name for success, formatted error for failure)</param>
        /// <param name="isSuccess">True for success message, false for failure message</param>
        private void TestSingleToolInstall(
            Func<string, IUnpackZipToolSupport, ToolInstaller.UnzipToolReturnAccumulator> unpackZipTool,
            string expectedMessageContent,
            bool isSuccess)
        {
            using var context = new ToolUpdateTestContext(new[] { SAMPLE_TOOL }, CreateToolStoreClient(TestFilesDir.FullPath), unpackZipTool: unpackZipTool);
            var toolUpdatesDlg = context.ShowToolUpdatesDlg();
            
            var expectedMessage = isSuccess
                ? ToolUpdatesDlg.FormatInstallSuccessSummary(expectedMessageContent)
                : ToolUpdatesDlg.FormatInstallFailureSummary(expectedMessageContent);
            
            TestMessageDlgShown(toolUpdatesDlg.OkDialog, expectedMessage);
        }

        /// <summary>
        /// IDisposable helper for ToolUpdatesTest that handles common setup/teardown.
        /// Ensures tool list is cleared before and after test, properly disposes resources,
        /// and waits for ToolUpdatesDlg to close.
        /// </summary>
        private class ToolUpdateTestContext : IDisposable
        {
            private readonly IToolUpdateHelper _updateHelper;
            
            public ToolUpdateTestContext(
                ToolDescription[] toolsToAdd,
                IToolStoreClient toolStoreClient,
                Func<string, IUnpackZipToolSupport, ToolInstaller.UnzipToolReturnAccumulator> unpackZipTool = null)
            {
                // Clear tool list BEFORE test (prevents cascading failures from previous test failures)
                Settings.Default.ToolList.Clear();
                
                // Add tools for this test
                if (toolsToAdd != null && toolsToAdd.Length > 0)
                    Settings.Default.ToolList.AddRange(toolsToAdd);
                
                // Create update helper - toolStoreClient is now required (not optional)
                _updateHelper = CreateUpdateHelper(toolStoreClient, unpackZipTool);
            }
            
            /// <summary>
            /// Shows the ToolUpdatesDlg with the configured update helper.
            /// </summary>
            /// <param name="itemsSelected">If true, all tools are selected. If false, all tools are deselected.</param>
            /// <param name="testingDownloadOnly">If true, only tests download phase (skips installation).</param>
            public ToolUpdatesDlg ShowToolUpdatesDlg(bool itemsSelected = true, bool testingDownloadOnly = false)
            {
                return ShowAndInitToolUpdatesDlg(itemsSelected, _updateHelper, testingDownloadOnly);
            }
            
            public void Dispose()
            {
                // Wait for ToolUpdatesDlg to close (no-op if already closed)
                WaitForClosedForm<ToolUpdatesDlg>();
                
                // Dispose the update helper (which disposes the tool store client)
                (_updateHelper as IDisposable)?.Dispose();
                
                // Clear tool list AFTER test (cleanup)
                Settings.Default.ToolList.Clear();
            }
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
            using var context = new ToolUpdateTestContext(new[] { SAMPLE_TOOL }, CreateToolStoreClient(TestFilesDir.FullPath));
            var toolUpdatesDlg = context.ShowToolUpdatesDlg();

            var multiBtnMsgDlg = ShowDialog<MultiButtonMsgDlg>(toolUpdatesDlg.OkDialog);

            TestMessageDlgShown(multiBtnMsgDlg.Btn0Click, 
                ToolUpdatesDlg.FormatInstallSuccessSummary(SAMPLE_TOOL.PackageName));

            AssertUpdated(SAMPLE_TOOL);
        }

        /// <summary>
        /// Tests the start to finish installation of a nested tool. 
        /// </summary>
        private void TestUpdateNestedTool()
        {
            using var context = new ToolUpdateTestContext(new[] { NESTED_TOOL_A, NESTED_TOOL_B }, CreateToolStoreClient(TestFilesDir.FullPath));
            var toolUpdatesDlg = context.ShowToolUpdatesDlg();
            Assert.AreEqual(1, toolUpdatesDlg.ItemCount);

            var multiBtnMsgDlg = ShowDialog<MultiButtonMsgDlg>(toolUpdatesDlg.OkDialog);

            TestMessageDlgShown(multiBtnMsgDlg.Btn0Click, 
                ToolUpdatesDlg.FormatInstallSuccessSummary(NESTED_TOOL_A.PackageName));

            AssertUpdated(NESTED_TOOL_A);
        }

        private void AssertUpdated(ToolDescription toolToCheck)
        {
            foreach (var tool in Settings.Default.ToolList.Where(tool => Equals(tool.PackageIdentifier, toolToCheck.PackageIdentifier)))
            {
                Assert.IsFalse(tool.UpdateAvailable);
            }

        }

        /// <summary>
        /// Tests proper execution when multiple tools are not successfully updated.
        /// </summary>
        private void TestMultipleFailures()
        {
            TestInstallSummaryMessageDlg(false, false,
                ToolUpdatesDlg.FormatInstallFailureSummary(new[]
                {
                    FormatFailureMessage(NESTED_TOOL_A, Resources.ToolUpdatesDlg_InstallUpdates_User_cancelled_installation),
                    FormatFailureMessage(SAMPLE_TOOL, Resources.ToolUpdatesDlg_InstallUpdates_User_cancelled_installation)
                }));
        }

        /// <summary>
        /// Tests proper execution when one tool is updated successfully but the other is not.
        /// </summary>
        private void TestOneSuccessOneFailure()
        {
            TestInstallSummaryMessageDlg(false, true,
                ToolUpdatesDlg.FormatMixedInstallSummary(
                    new[] { SAMPLE_TOOL.PackageName },
                    new[] { FormatFailureMessage(NESTED_TOOL_A, Resources.ToolUpdatesDlg_InstallUpdates_User_cancelled_installation) }));
        }

        /// <summary>
        /// Tests proper execution when multiple tools are updated successfully.
        /// </summary>
        private void TestMultipleSuccesses()
        {
            TestInstallSummaryMessageDlg(true, true,
                ToolUpdatesDlg.FormatInstallSuccessSummary(new[] { NESTED_TOOL_A.PackageName, SAMPLE_TOOL.PackageName }));
        }

        /// <summary>
        /// Used for testing successfully installing multiple tools, failing to install multiple tools, or successfully installing one tool while
        /// failing to install the other. This method formats the summary message dialog created by the Tool Updater.
        /// </summary>
        /// <param name="nestedSuccess">If true, the nested tool will be installed successfully. If false, it will not be installed successfully, because
        /// of a user cancellation.</param>
        /// <param name="sampleSuccess">If true, the sample tool will be installed successfully. If false, it will not be installed successfully, because
        /// of a user cancellation.</param>
        /// <param name="message">The message expected to be showing in the summary message dialog</param>
        private void TestInstallSummaryMessageDlg(bool nestedSuccess, bool sampleSuccess, string message)
        {
            using var context = new ToolUpdateTestContext(new[] { NESTED_TOOL_A, NESTED_TOOL_B, SAMPLE_TOOL },
                CreateToolStoreClient(TestFilesDir.GetTestPath("TestOneSuccessOneFailure")));
            var toolUpdatesDlg = context.ShowToolUpdatesDlg();
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
            var messageDlg = WaitForOpenForm<MessageDlg>();
            Assert.AreEqual(message, messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm<ToolUpdatesDlg>();
            AssertUpdateAvailability(Settings.Default.ToolList, !nestedSuccess, !sampleSuccess);
        }

        /// <summary>
        /// Asserts that updates are available or not for the sample tool and nested tool, from a given <see cref="ToolList"/>>.
        /// </summary>
        private static void AssertUpdateAvailability(ToolList toolList, bool nestedToolUpdateAvailable, bool sampleToolUpdateAvailable)
        {
            var sampleTool =
                toolList.First(description => description.PackageIdentifier.Equals(SAMPLE_TOOL.PackageIdentifier));
            var nestedTools =
                toolList.Where(description => description.PackageIdentifier.Equals(NESTED_TOOL_A.PackageIdentifier));

            Assert.IsNotNull(sampleTool);
            Assert.IsNotNull(nestedTools);

            Assert.AreEqual(sampleToolUpdateAvailable, sampleTool.UpdateAvailable);
            foreach (var nestedTool in nestedTools)
            {
                Assert.AreEqual(nestedToolUpdateAvailable, nestedTool.UpdateAvailable);
            }
        }

        private static string FormatFailureMessage(ToolDescription toolDescription, string message)
        {
            return ToolUpdatesDlg.FormatFailureMessage(toolDescription.PackageName, message);
        }

        /// <summary>
        /// Shows and initializes a <see cref="ToolUpdatesDlg"/> form for use in testing.
        /// </summary>
        /// <param name="itemsSelected">If true, this will check all the tools listed in the <see cref="ToolUpdatesDlg"/>. If false, it will uncheck all of them.</param>
        /// <param name="updateHelper">The update helper for the <see cref="ToolUpdatesDlg"/>.</param>
        /// <param name="testingDownloadOnly">If true, it will only test the DownloadTools function of the <see cref="ToolUpdatesDlg"/>.</param>
        private static ToolUpdatesDlg ShowAndInitToolUpdatesDlg(bool itemsSelected, IToolUpdateHelper updateHelper, bool testingDownloadOnly)
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
        /// Creates a <see cref="TestToolStoreClient"/> for use in testing.
        /// </summary>
        /// <param name="filePath">Path to the folder containing test tool zip files.</param>
        private static TestToolStoreClient CreateToolStoreClient(string filePath = null)
        {
            return new TestToolStoreClient(filePath ?? Path.GetTempPath());
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
        /// Creates a ToolUpdateHelper for testing. If no unpackZipTool function is specified, it uses the default ToolInstaller one.
        /// </summary>
        private static TestToolUpdateHelper CreateUpdateHelper(IToolStoreClient client,
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

    public class TestToolUpdateHelper : IToolUpdateHelper, IDisposable
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

        public void GetToolZipFile(IProgressMonitor progressMonitor, IProgressStatus progressStatus, string packageIdentifier, FileSaver fileSaver)
        {
            _client.GetToolZipFile(progressMonitor, progressStatus, packageIdentifier, fileSaver);
        }

        public void Dispose()
        {
            (_client as IDisposable)?.Dispose();
        }
    }
}
