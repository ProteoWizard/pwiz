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
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Ionic.Zip;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
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
    public class ToolStoreDlgTest : AbstractFunctionalTestEx 
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
            // Tidy up our tempfiles
            FileEx.SafeDelete(Path.Combine(Environment.GetEnvironmentVariable(@"TMP") ?? string.Empty, INFO_NAME + @"Tool.zip"));
        }

        private void TestStoreLoad()
        {
            TestServerConnectionFailure();
            TestProperPopulation();
        }

        private static void TestServerConnectionFailure()
        {
            // No cancellation test here, because GetToolsJson() uses SilentProgressMonitor
            // which does not allow user cancellation, and this is the call that is failing.

            TestHttpClientWithNoNetwork(SkylineWindow.ShowToolStoreDlg, Resources.ConfigureToolsDlg_GetZipFromWeb_Error_connecting_to_the_Tool_Store_);

            using (var helper = HttpClientTestHelper.SimulateConnectionLoss())
            {
                var expectedMessage = TextUtil.LineSeparate(
                    Resources.ConfigureToolsDlg_GetZipFromWeb_Error_connecting_to_the_Tool_Store_,
                    helper.GetExpectedMessage());
                var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
                TestMessageDlgShown(configureToolsDlg.AddFromWeb, expectedMessage);
                OkDialog(configureToolsDlg, configureToolsDlg.OkDialog);
            }
        }

        private void TestProperPopulation()
        {
            TestToolNotInstalled();
            TestToolOldVersion();
            TestToolFullyUpdated();
            TestMultipleTools();
            TestImageLoad();
        }

        private TestToolStoreClient CreateToolStoreClient()
        {
            return CreateToolStoreClient("TestBasicPopulation");
        }

        private TestToolStoreClient CreateToolStoreClient(string toolsDir)
        {
            return new TestToolStoreClient(TestFilesDir.GetTestPath(toolsDir));
        }

        // tool is not installed
        private void TestToolNotInstalled()
        {
            using var client = CreateToolStoreClient();
            var toolStoreDlg = ShowDialog<ToolStoreDlg>(() => ShowToolStore(client));
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
            using var client = CreateToolStoreClient();
            var toolStoreDlg = ShowDialog<ToolStoreDlg>(() => ShowToolStore(client));
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
            using var client = CreateToolStoreClient();
            var toolStoreDlg = ShowDialog<ToolStoreDlg>(() => ShowToolStore(client));
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
            using var client = CreateToolStoreClient("TestMultipleTools");
            var toolStoreDlg = ShowDialog<ToolStoreDlg>(() => ShowToolStore(client));
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
            using var client = CreateToolStoreClient("TestImageLoad");
            var toolStoreDlg = ShowDialog<ToolStoreDlg>(() => ShowToolStore(client));
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
            ToolStoreDlg toolStoreDlg;
            // Use the TestToolStoreClient to populate the form
            using (var client = CreateToolStoreClient())
            {
                toolStoreDlg = ShowDialog<ToolStoreDlg>(() => ShowToolStore(client));
                Assert.AreEqual(1, toolStoreDlg.ToolCount);
                var toolStoreItem = toolStoreDlg.GetTools().First();
                AssertToolItemEquality(GetSampleTool(), toolStoreItem);
            }

            // Set ToolStoreClient on the form back to WebToolStoreClient for failure testing
            toolStoreDlg.ToolStoreClient = ToolStoreUtil.ToolStoreClient;
            
            TestHttpClientCancellation(toolStoreDlg.DownloadSelectedTool);

            using (var helper = HttpClientTestHelper.SimulateNoNetworkInterface())
            {
                TestMessageDlgShown(toolStoreDlg.DownloadSelectedTool, helper.GetExpectedMessage());
            }

            OkDialog(toolStoreDlg, toolStoreDlg.CancelDialog);
        }

        // download success
        private void TestDownloadSuccess()
        {
            using var client = CreateToolStoreClient();
            var toolStoreDlg = ShowDialog<ToolStoreDlg>(() => ShowToolStore(client));
            Assert.AreEqual(1, toolStoreDlg.ToolCount);
            var toolStoreItem = toolStoreDlg.GetTools().First();
            AssertToolItemEquality(GetSampleTool(), toolStoreItem);
            OkDialog(toolStoreDlg, toolStoreDlg.DownloadSelectedTool);
        }

        // tool installation via the configure tools dlg
        private void TestInstallation() 
        {
            using var client = CreateToolStoreClient();
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunUI(() =>
                {
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
            using var client = CreateToolStoreClient();
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

        private static void ShowToolStore(IToolStoreClient client)
        {
            // Dummy install delegate for testing - actual installation tested separately
            ToolInstallUI.InstallProgram installProgram = (ppc, packages, script) => null;

            using (var dlg = new ToolStoreDlg(client, client.GetToolStoreItems(), installProgram))
            {
                dlg.ShowDialog(SkylineWindow);
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

    public class TestToolStoreClient : IToolStoreClient, IDisposable
    {
        private readonly IToolStoreClient _originalClient;
        private readonly DirectoryInfo _toolDir;

        public TestToolStoreClient(string toolDirPath)
        {
            _toolDir = new DirectoryInfo(toolDirPath);
            _originalClient = ToolStoreUtil.ToolStoreClient;
            ToolStoreUtil.ToolStoreClient = this;
        }

        private IList<ToolStoreItem> ToolStoreItems { get; set; }
        private static readonly string[] IMAGE_EXTENSIONS = { @".jpg", @".png", @".bmp" };

        public IList<ToolStoreItem> GetToolStoreItems()
        {
            if (ToolStoreItems != null)
                return ToolStoreItems;

            var tools = new List<ToolStoreItem>();
            if (_toolDir == null)
                return tools;
            foreach (var toolDir in _toolDir.GetFiles())
            {
                if (toolDir == null || string.IsNullOrEmpty(toolDir.DirectoryName))
                    continue;

                string fileName = Path.GetFileNameWithoutExtension(toolDir.Name);
                string path = Path.Combine(toolDir.DirectoryName, fileName);
                using (new TemporaryDirectory(path))
                {
                    using (var zipFile = new ZipFile(toolDir.FullName))
                    {
                        // extract files
                        zipFile.ExtractAll(path, ExtractExistingFileAction.OverwriteSilently);

                        var toolInf = new DirectoryInfo(Path.Combine(path, ToolInstaller.TOOL_INF));
                        if (!Directory.Exists(toolInf.FullName))
                            continue;

                        if (!toolInf.GetFiles(ToolInstaller.INFO_PROPERTIES).Any())
                            continue;

                        var pictures = toolInf.GetFiles().Where(info => IMAGE_EXTENSIONS.Contains(info.Extension)).ToList();
                        var toolImage = ToolStoreUtil.DefaultImage;
                        if (pictures.Count != 0)
                        {
                            // try and read in the image
                            try
                            {
                                using (var stream = new FileStream(pictures.First().FullName, FileMode.Open, FileAccess.Read))
                                {
                                    toolImage = Image.FromStream(stream);
                                }
                            }
                            // ReSharper disable EmptyGeneralCatchClause
                            catch (Exception)
                            // ReSharper restore EmptyGeneralCatchClause
                            {
                                // if anything goes wrong -- just use the default image :)
                            }
                        }

                        ExternalToolProperties readin;
                        try
                        {
                            readin = new ExternalToolProperties(Path.Combine(toolInf.FullName, ToolInstaller.INFO_PROPERTIES));
                        }
                        catch (Exception)
                        {
                            continue;
                        }

                        tools.Add(new ToolStoreItem(readin.Name, readin.Author, readin.Provider, readin.Version,
                                                    readin.Description, readin.Identifier, toolImage, toolDir.FullName));
                    }
                }
            }
            // sort toollist by name
            tools.Sort(((item, storeItem) => String.Compare(item.Name, storeItem.Name, StringComparison.Ordinal)));
            return tools;
        }

        public void GetToolZipFile(IProgressMonitor progressMonitor, IProgressStatus progressStatus, string packageIdentifier, FileSaver fileSaver)
        {
            // Find the tool zip file path for this identifier
            var toolZipPath = GetToolZipPath(packageIdentifier);
            if (toolZipPath == null)
                throw new ToolExecutionException(ToolsUIResources.TestToolStoreClient_GetToolZipFile_Cannot_find_a_file_with_that_identifier_);

            // Use HttpClientTestHelper to mock the download and call the real production code
            var uri = new UriBuilder(WebToolStoreClient.TOOL_STORE_URI)
            {
                Path = "/skyts/home/downloadTool.view",
                Query = @"lsid=" + Uri.EscapeDataString(packageIdentifier)
            };

            using var helper = HttpClientTestHelper.WithMockResponseFile(uri.Uri, toolZipPath);
            WebToolStoreClient.GetToolZipFileWithProgress(progressMonitor, progressStatus, packageIdentifier, fileSaver);
        }

        private string GetToolZipPath(string packageIdentifier)
        {
            foreach (var item in ToolStoreItems ?? GetToolStoreItems())
            {
                if (item.Identifier.Equals(packageIdentifier))
                {
                    Assert.IsNotNull(item.FilePath);
                    return item.FilePath;
                }
            }

            return null;
        }

        public bool IsToolUpdateAvailable(string identifier, Version version)
        {
            if (ToolStoreItems == null)
                ToolStoreItems = GetToolStoreItems();

            var tool = ToolStoreItems.FirstOrDefault(item => item.Identifier.Equals(identifier));
            return tool != null && version < new Version(tool.Version);
        }

        public void Dispose()
        {
            ToolStoreUtil.ToolStoreClient = _originalClient;
        }
    }
}
