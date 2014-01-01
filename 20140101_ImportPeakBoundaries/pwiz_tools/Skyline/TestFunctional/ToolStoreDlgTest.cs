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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI; 
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ToolStoreDlgTest : AbstractFunctionalTest 
    {
        /// <summary>
        /// Functional test for the Tool Store Dlg
        /// </summary>
        [TestMethod]
        public void TestToolStore()
        {
            TestFilesZip = @"TestFunctional\ToolStoreDlgTest.zip"; // Not L10N
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // clear out the list of tools
            Settings.Default.ToolList.Clear();
            
            TestStoreLoad();
            TestToolDownload();
            TestInstallation();
            TestReinstall();
        }

        private void TestStoreLoad()
        {
            TestServerConnectionFailure();
            TestProperPopulation();
        }

        private static void TestServerConnectionFailure()
        {
            const string errorMessage = "error message";    // Not L10N
           
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            ToolStoreUtil.ToolStoreClient = new TestToolStoreClient(Path.GetTempPath())
                {
                    FailToConnect = true,
                    FailToConnectMessage = errorMessage
                };
            var messageDlg = ShowDialog<MessageDlg>(configureToolsDlg.AddFromWeb);
            Assert.AreEqual(string.Format(Resources.ConfigureToolsDlg_GetZipFromWeb_Error_connecting_to_the_Tool_Store___0_, errorMessage), messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            OkDialog(configureToolsDlg, configureToolsDlg.OkDialog);
        }

        private void TestProperPopulation()
        {
            TestToolNotInstalled();
            TestToolOldVersion();
            TestToolFullyUpdated();
            TestMultipleTools();
            TestImageLoad();
        }

        // tool is not installed
        private void TestToolNotInstalled()
        {
            var toolStoreDlg = ShowDialog<ToolStoreDlg>(() => ShowToolStore(TestFilesDir.GetTestPath("TestBasicPopulation"), false)); // Not L10N
            Assert.AreEqual(1, toolStoreDlg.ToolCount);
            var toolStoreItem = toolStoreDlg.GetTools().First();
            AssertToolItemEquality(GetSampleTool(), toolStoreItem);
            Assert.IsFalse(toolStoreItem.Installed);
            Assert.IsFalse(toolStoreItem.IsMostRecentVersion);
            OkDialog(toolStoreDlg, toolStoreDlg.CancelButton.PerformClick);
        }

        // tool is installed but not the most recent version
        private void TestToolOldVersion()
        {
            Settings.Default.ToolList.Add(GetSampleToolDescription(true));
            var toolStoreDlg = ShowDialog<ToolStoreDlg>(() => ShowToolStore(TestFilesDir.GetTestPath("TestBasicPopulation"), false)); // Not L10N
            Assert.AreEqual(1, toolStoreDlg.ToolCount);
            var toolStoreItem = toolStoreDlg.GetTools().First();
            AssertToolItemEquality(GetSampleTool(), toolStoreItem);
            Assert.IsTrue(toolStoreItem.Installed);
            Assert.IsFalse(toolStoreItem.IsMostRecentVersion);
            OkDialog(toolStoreDlg, toolStoreDlg.CancelButton.PerformClick);
            Settings.Default.ToolList.Clear();
        }

        // tool is installed and is the most recent version
        private void TestToolFullyUpdated()
        {
            Settings.Default.ToolList.Add(GetSampleToolDescription(false));
            var toolStoreDlg = ShowDialog<ToolStoreDlg>(() => ShowToolStore(TestFilesDir.GetTestPath("TestBasicPopulation"), false)); // Not L10N
            Assert.AreEqual(1, toolStoreDlg.ToolCount);
            var toolStoreItem = toolStoreDlg.GetTools().First();
            AssertToolItemEquality(GetSampleTool(), toolStoreItem);
            Assert.IsTrue(toolStoreItem.Installed);
            Assert.IsTrue(toolStoreItem.IsMostRecentVersion);
            OkDialog(toolStoreDlg, toolStoreDlg.CancelButton.PerformClick);
            Settings.Default.ToolList.Clear();
        }

        // adding multiple tools to the store
        private void TestMultipleTools()
        {
            var toolStoreDlg = ShowDialog<ToolStoreDlg>(() => ShowToolStore(TestFilesDir.GetTestPath("TestMultipleTools"), false)); // Not L10N
            Assert.AreEqual(2, toolStoreDlg.ToolCount);
            var toolStoreItem1 = toolStoreDlg.GetTools().First();
            AssertToolItemEquality(GetSampleTool(), toolStoreItem1);
            Assert.IsFalse(toolStoreItem1.Installed);
            Assert.IsFalse(toolStoreItem1.IsMostRecentVersion);
            var toolStoreItem2 = toolStoreDlg.GetTools().ElementAt(1);
            AssertToolItemEquality(GetSampleToolAlternate(), toolStoreItem2);
            Assert.IsFalse(toolStoreItem2.Installed);
            Assert.IsFalse(toolStoreItem2.IsMostRecentVersion);
            OkDialog(toolStoreDlg, toolStoreDlg.CancelButton.PerformClick);
        }

        // tests image loading; the sample tool has an improperly formatted jpg image while the
        // alternate sample tool has a properly formatted png image to use; this is merely testing that both are loaded properly
        private void TestImageLoad()
        {
            var toolStoreDlg = ShowDialog<ToolStoreDlg>(() => ShowToolStore(TestFilesDir.GetTestPath("TestImageLoad"), false)); // Not L10N
            Assert.AreEqual(2, toolStoreDlg.ToolCount);
            OkDialog(toolStoreDlg, toolStoreDlg.CancelButton.PerformClick);
        }

        private void TestToolDownload()
        {
            TestDownloadFailure();
            TestDownloadSuccess();
        }

        // download failure
        private void TestDownloadFailure()
        {
            var toolStoreDlg = ShowDialog<ToolStoreDlg>(() => ShowToolStore(TestFilesDir.GetTestPath("TestBasicPopulation"), false)); // Not L10N
            Assert.AreEqual(1, toolStoreDlg.ToolCount);
            var toolStoreItem = toolStoreDlg.GetTools().First();
            AssertToolItemEquality(GetSampleTool(), toolStoreItem);
            var errorDlg = ShowDialog<MessageDlg>(toolStoreDlg.DownloadSelectedTool);
            Assert.AreEqual(Resources.TestToolStoreClient_GetToolZipFile_Error_downloading_tool, errorDlg.Message);
            OkDialog(errorDlg, errorDlg.OkDialog);
            OkDialog(toolStoreDlg, toolStoreDlg.CancelButton.PerformClick);
        }

        // download success
        private void TestDownloadSuccess()
        {
            var toolStoreDlg = ShowDialog<ToolStoreDlg>(() => ShowToolStore(TestFilesDir.GetTestPath("TestBasicPopulation"), true)); // Not L10N
            Assert.AreEqual(1, toolStoreDlg.ToolCount);
            var toolStoreItem = toolStoreDlg.GetTools().First();
            AssertToolItemEquality(GetSampleTool(), toolStoreItem);
            OkDialog(toolStoreDlg, toolStoreDlg.DownloadSelectedTool);
        }

        // tool installation via the configure tools dlg
        private void TestInstallation() 
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunUI(() =>
                {
                    ToolStoreUtil.ToolStoreClient = new TestToolStoreClient(TestFilesDir.GetTestPath("TestBasicPopulation"));
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.SaveTools();
                });
            RunDlg<ToolStoreDlg>(configureToolsDlg.AddFromWeb, dlg => dlg.DownloadSelectedTool());
            WaitForConditionUI(() => configureToolsDlg.ToolList.Count == 1);
            RunUI(() =>
                {
                    configureToolsDlg.SaveTools();
                    Assert.AreEqual(TITLE, configureToolsDlg.ToolList[0].Title);
                    Assert.AreEqual(COMMAND, configureToolsDlg.ToolList[0].Command);
                });
            OkDialog(configureToolsDlg, configureToolsDlg.Cancel);
            Settings.Default.ToolList.Clear();
        }

        // tests reinstallation
        private void TestReinstall()
        {
            // change the output to immediate window flag to true
            Settings.Default.ToolList.Add(new ToolDescription(GetSampleToolDescription(false)) {OutputToImmediateWindow = true});
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunUI(() =>
                {
                    ToolStoreUtil.ToolStoreClient = new TestToolStoreClient(TestFilesDir.GetTestPath("TestBasicPopulation"));
                });
            var toolStoreDlg = ShowDialog<ToolStoreDlg>(configureToolsDlg.AddFromWeb);
            var reinstallDlg = ShowDialog<MultiButtonMsgDlg>(toolStoreDlg.DownloadSelectedTool);
            OkDialog(reinstallDlg, reinstallDlg.Btn0Click);
            OkDialog(configureToolsDlg, configureToolsDlg.OkDialog);
            // on reinstall, it should be back to normal
            Assert.IsFalse(Settings.Default.ToolList.First().OutputToImmediateWindow);
            Settings.Default.ToolList.Clear();
        }

        private static void AssertToolItemEquality(ToolStoreItem tool1, ToolStoreItem tool2)
        {
            Assert.AreEqual(tool1.Authors, tool2.Authors);
            Assert.AreEqual(tool1.Description, tool2.Description);
            Assert.AreEqual(tool1.Identifier, tool2.Identifier);
            Assert.AreEqual(tool1.Name, tool2.Name);
            Assert.AreEqual(tool1.Provider, tool2.Provider);
            Assert.AreEqual(tool1.Version, tool2.Version);
        }

        private static void ShowToolStore(string testDirectory, bool downloadSuccess)
        {
            var client = new TestToolStoreClient(testDirectory) {FailDownload = !downloadSuccess};
            using (var dlg = new ToolStoreDlg(client, client.GetToolStoreItems()))
            {
                dlg.ShowDialog();
            }
        }

        // info.properties
        private const string AUTHOR = "Trevor Killeen";                         // Not L10N
        private const string ALTERNATE_AUTHOR = "Trevor Killeen2";              // Not L10N
        private const string DESCRIPTION = "Test";                              // Not L10N
        private const string ALTERNATE_DESCRIPTION = "Test2";                   // Not L10N
        private const string IDENTIFIER = "URN:LSID:test.com";                  // Not L10N
        private const string ALTERNATE_IDENTIFIER = "URN:LSID:test2.com";       // Not L10N
        private const string INFO_NAME = "Sample";                              // Not L10N
        private const string ALTERNATE_NAME = "Sample2";                        // Not L10N
        private const string PROVIDER = @"http://test.com";                     // Not L10N
        private const string ALTERNATE_PROVIDER = @"http://test2.com";          // Not L10N
        private const string VERSION = "1.0";                                   // Not L10N
        private const string OLD_VERSION = "0.9";                               // Not L10N

        // sample.properties
        private const string TITLE = "Sample";                                  // Not L10N
        private const string COMMAND = "http://test.com";                       // Not L10N

        private static ToolStoreItem GetSampleTool()
        {
            return new ToolStoreItem(INFO_NAME, AUTHOR, PROVIDER, VERSION, DESCRIPTION, IDENTIFIER, null);
        }

        private static ToolStoreItem GetSampleToolAlternate()
        {
            return new ToolStoreItem(ALTERNATE_NAME, ALTERNATE_AUTHOR, ALTERNATE_PROVIDER, OLD_VERSION, ALTERNATE_DESCRIPTION, ALTERNATE_IDENTIFIER, null);
        }

        private static ToolDescription GetSampleToolDescription(bool oldVersion)
        {
            return new ToolDescription(TITLE, COMMAND, string.Empty, string.Empty, false, string.Empty, string.Empty,
                                       string.Empty, string.Empty, new List<AnnotationDef>(),
                                       oldVersion ? OLD_VERSION : VERSION, IDENTIFIER, INFO_NAME);
        }

    }

}
