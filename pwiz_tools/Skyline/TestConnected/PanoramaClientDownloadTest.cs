/*
 * Original author: Sophie Pallanck <srpall .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.PanoramaClient;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestConnected
{
    /// <summary>
    /// THIS TEST DEPENDS ON THE PANORAMA FOLDER: ForPanoramaClientTest
    /// IF TEST IS FAILING, CHECK THAT THE FOLDER HAS NOT BEEN DELETED
    /// </summary>
    [TestClass]
    public class PanoramaClientDownloadTest : AbstractFunctionalTestEx
    {
        private const string TEST_USER = "skyline_tester@proteinms.net";
        private const string TEST_PASSWORD = "Lclcmsms1!";
        private const string PANORAMA_WEB = "https://panoramaweb.org";
        private const string TEST_FOLDER = "SkylineTest";
        private const string TEST_PUBLIC_FOLDER = "Panorama Public";
        private const string PANORAMA_FOLDER = "ForPanoramaClientDownloadTest";
        private const string PANORAMA_DELETED_FILE_FOLDER = "ForPanoramaClientTest";
        private const string TEST_FILE = "Study9S_Site52_v1_min.sky.zip";
        private const string DELETED_FILE = "FileDeletedFromServer.sky.zip";
        private const string RENAMED_FILE = "TestFileRename_min.sky.zip";
        private const string NOT_RENAMED_FOLDER = "FileRenamedOnServer_min";
        protected override bool IsRecordMode => false; // Set to true for recording

        [TestMethod]
        public void TestPanoramaDownloadFile()
        {
            DoActualWebAccess = false; // Use recorded playback
            RunFunctionalTest();
        }

        [TestMethod]
        public void TestPanoramaDownloadFileWeb()
        {
            // Only run this if SkylineTester has enabled web access or web responses are being recorded
            if (AllowInternetAccess || IsRecordMode)
            {
                DoActualWebAccess = true; // Actually go to the web
                RunFunctionalTest();
            }
        }

        protected override void DoTest()
        {
            using var scope = GetHttpRecordingScope();

            // Test successful download
            TestDownloadFile();

            // Test various download errors
            TestDownloadErrors();

            // Make sure PanoramaFilePicker states are being preserved between runs
            TestPreserveStates();
            
            // Test downloading from test server
            TestMissingFile();

            // Test downloading a file that has been renamed on Panorama
            TestRenamedFile();

            // Test viewing webDav browser
            TestWebDav();

            // Test adding a new server to Panorama
            TestAddServer();

            // Test adding PanoramaWeb as an anonymous server, and downloading a document from Panorama Public.
            TestWithAnonymousServer();
        }

        private void AddPanoramaServers()
        {
            RunUI(() =>
            {
                var servers = Settings.Default.ServerList;
                servers.Add(CreatePanoramaServer());
                Settings.Default.ServerList = servers;
            });
        }

        /// <summary>
        /// Gets the Panorama server URI, using folder-limited URI when recording to reduce response size.
        /// When recording or in playback mode, uses project-specific URI (e.g., https://panoramaweb.org/SkylineTest/)
        /// to record only the specific folder tree instead of all folders.
        /// During live unrecorded tests, uses the full URI to allow browsing all folders.
        /// </summary>
        private Uri GetPanoramaServerUri(string testFolder = null)
        {
            if (IsRecordMode || !DoActualWebAccess)
            {
                // Recording: use project-specific URI for smaller recorded responses
                return new Uri(PANORAMA_WEB + @"/" + (testFolder ?? TEST_FOLDER) + @"/");
            }
            else
            {
                // During live unrecorded tests, the full URI allows browsing all folders
                return new Uri(PANORAMA_WEB);
            }
        }

        private Server CreatePanoramaServer()
        {
            return new Server(GetPanoramaServerUri(), TEST_USER, TEST_PASSWORD);
        }

        //Downloads and opens a file successfully 
        private void TestDownloadFile()
        {
            AddPanoramaServers();
            var path = TestContext.GetTestResultsPath(TEST_FILE);
            FileEx.SafeDelete(path, true);
            Assert.IsFalse(File.Exists(path));
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() => SkylineWindow.OpenFromPanorama(path));
            WaitForCondition(9000, () => remoteDlg.IsLoaded);
            RunUI(() =>
            {
                SelectNode(remoteDlg, TEST_FOLDER);
                SelectNode(remoteDlg, PANORAMA_FOLDER);
            });
            var doc = SkylineWindow.Document;
            OkDialog(remoteDlg, () => ClickFile(remoteDlg, TEST_FILE));
            WaitForCondition(() => File.Exists(path));
            var docLoaded = WaitForDocumentChangeLoaded(doc);
            AssertEx.IsDocumentState(docLoaded, null, 7, 22, 23, 115);
            FileEx.SafeDelete(path, true);
            Assert.IsFalse(File.Exists(path));
        }

        private static void ClickFile(PanoramaFilePicker remoteDlg, string fileName)
        {
            Assert.IsTrue(remoteDlg.ClickFile(fileName), "Unable to click file {0}", fileName);
        }

        private static void SelectNode(PanoramaFilePicker remoteDlg, string nodeName)
        {
            Assert.IsTrue(remoteDlg.FolderBrowser.SelectNode(nodeName), "Unable to select {0}", nodeName);
        }

        /// <summary>
        /// Test error handling in DownloadPanoramaFile using HttpClientTestHelper to simulate network failures.
        /// These tests verify that network errors are properly caught and displayed to the user.
        /// </summary>
        private void TestDownloadErrors()
        {
            AddPanoramaServers();
            var server = CreatePanoramaServer();
            var path = TestContext.GetTestResultsPath(TEST_FILE);
            var fileUrl = $"{PANORAMA_WEB}/_webdav/{TEST_FOLDER}/{PANORAMA_FOLDER}/@files/{TEST_FILE}";
            var fileUri = new Uri(fileUrl);

            // Test user cancellation - verify file is not created
            TestDownloadCancellation(path, fileUri, server);

            // Test no network interface - verify friendly error message
            TestDownloadError(path, fileUri, server, HttpClientTestHelper.SimulateNoNetworkInterface);

            // Test 401 Unauthorized - verify authentication error message - CONSIDER: More informative messaging like SkypSupport
            TestDownloadError(path, fileUri, server, HttpClientTestHelper.SimulateHttp401);

            // Test 403 Forbidden - verify access denied message - CONSIDER: More informative messaging like SkypSupport
            TestDownloadError(path, fileUri, server, HttpClientTestHelper.SimulateHttp403);

            // Test 500 Server Error - verify server error message
            TestDownloadError(path, fileUri, server, HttpClientTestHelper.SimulateHttp500);

            // Test DNS failure - verify DNS resolution error message
            TestDownloadError(path, fileUri, server,
                () => HttpClientTestHelper.SimulateDnsFailure(fileUri.Host));
        }

        private void TestDownloadError(string path, Uri fileUri, Server server,
            Func<HttpClientTestHelper> simulateFailure)
        {
            FileEx.SafeDelete(path, true);
            using (var helper = simulateFailure())
            {
                TestMessageDlgShownContaining(() => SkylineWindow.DownloadPanoramaFile(path, TEST_FILE, fileUri.ToString(), server, 12345),
                    helper.GetExpectedMessage(fileUri));
            }
            Assert.IsFalse(File.Exists(path));
        }

        private void TestDownloadCancellation(string path, Uri fileUri, Server server)
        {
            FileEx.SafeDelete(path, true);
            TestHttpClientCancellation(() =>
            {
                var result = SkylineWindow.DownloadPanoramaFile(path, TEST_FILE, fileUri.ToString(), server, 12345);
                Assert.IsFalse(result);
            });
            Assert.IsFalse(File.Exists(path));
        }

        //Make sure PanoramaFilePicker states are being preserved between runs
        private void TestPreserveStates()
        {
            var state = string.Empty;
            AddPanoramaServers();
            var path = "TESTING";
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() => SkylineWindow.OpenFromPanorama(path));
            var dlg = remoteDlg;
            WaitForCondition(9000, () => dlg.IsLoaded);

            RunUI(() =>
            {
                SelectNode(remoteDlg, TEST_FOLDER);
                SelectNode(remoteDlg, PANORAMA_FOLDER);
                remoteDlg.Close();
                state = remoteDlg.FolderBrowser.TreeState;
            });
            WaitForClosedForm(remoteDlg);

            remoteDlg = ShowDialog<PanoramaFilePicker>(() =>
                SkylineWindow.OpenFromPanorama(path));
            WaitForCondition(9000, () => remoteDlg.IsLoaded);

            RunUI(() =>
            {
                Assert.AreEqual(state, remoteDlg.FolderBrowser.TreeState);
                Assert.IsTrue(remoteDlg.FolderBrowser.IsSelected(PANORAMA_FOLDER));
            });
            OkDialog(remoteDlg, remoteDlg.Close);
        }
        
        //Test downloading a file that has been deleted on the server
        private void TestMissingFile()
        {
            AddPanoramaServers();
            var path = TestContext.GetTestResultsPath(DELETED_FILE);
            FileEx.SafeDelete(path, true);
            Assert.IsFalse(File.Exists(path));
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() => SkylineWindow.OpenFromPanorama(path));
            WaitForCondition(9000, () => remoteDlg.IsLoaded);
            RunUI(() =>
            {
                SelectNode(remoteDlg, TEST_FOLDER);
                SelectNode(remoteDlg, PANORAMA_DELETED_FILE_FOLDER);
                ClickFile(remoteDlg, DELETED_FILE);
            });
            var errorDlg = ShowDialog<MessageDlg>(remoteDlg.ClickOpen);
            Assert.IsFalse(File.Exists(path));
            Assert.AreEqual(Resources.SkylineWindow_DownloadPanoramaFile_File_does_not_exist__It_may_have_been_deleted_on_the_server_, errorDlg.Message);
            OkDialog(errorDlg, errorDlg.OkDialog);
        }

        //Test downloading a file that has been renamed on Panorama
        private void TestRenamedFile()
        {
            AddPanoramaServers();
            var path = TestContext.GetTestResultsPath(RENAMED_FILE);
            FileEx.SafeDelete(path, true);
            Assert.IsFalse(File.Exists(path));
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() => SkylineWindow.OpenFromPanorama(path));
            WaitForCondition(9000, () => remoteDlg.IsLoaded);

            var doc = SkylineWindow.Document;
            RunUI(() =>
            {
                SelectNode(remoteDlg, TEST_FOLDER);
                SelectNode(remoteDlg, PANORAMA_FOLDER);
                ClickFile(remoteDlg, RENAMED_FILE);
            });
            WaitForClosedForm(remoteDlg);
            WaitForCondition(() => File.Exists(path));
            var docLoaded = WaitForDocumentChangeLoaded(doc);
            AssertEx.IsDocumentState(docLoaded, null, 11, 39, 68, 194);
            FileEx.SafeDelete(path, true);
            Assert.IsFalse(File.Exists(path));
        }

        //Test adding a new Panorama server.
        private void TestAddServer()
        {
            var path = TEST_FOLDER;
            Settings.Default.ServerList = null;
            var serverDlg = ShowDialog<MultiButtonMsgDlg>(() => RunUI(() =>
            { 
                SkylineWindow.OpenFromPanorama(path);
            }));
            var editItem = ShowDialog<EditServerDlg>(serverDlg.ClickOk);
            RunUI(() =>
            {
                // EditServerDlg now preserves folder paths in URIs, so we can use the folder-limited URI
                // when recording to reduce response size (e.g., https://panoramaweb.org/SkylineTest/)
                editItem.URL = GetPanoramaServerUri().ToString();
                editItem.Username = TEST_USER;
                editItem.Password = TEST_PASSWORD;
            });
            var remoteDlg = ShowDialog<PanoramaFilePicker>(editItem.OkDialog);
            if (Settings.Default.ServerList != null)
                Assert.AreEqual(1, Settings.Default.ServerList.Count);
            RunUI(() =>
            {
                Assert.IsTrue(remoteDlg.IsLoaded);
                SelectNode(remoteDlg, TEST_FOLDER);
                SelectNode(remoteDlg, PANORAMA_FOLDER);
            });
            
            OkDialog(remoteDlg, remoteDlg.Close);
        }

        private void TestWithAnonymousServer()
        {
            var toolsOptionsDlg = ShowDialog<ToolOptionsUI>(() => SkylineWindow.ShowToolOptionsUI(ToolOptionsUI.TABS.Panorama));

            // Clear the Panorama server list
            var editServerListDlg = ShowDialog<EditListDlg<SettingsListBase<Server>, Server>>(toolsOptionsDlg.EditServers);
            RunUI(editServerListDlg.ResetList);

            // Add PanoramaWeb as an anonymous server
            var editServerDlg = ShowDialog<EditServerDlg>(editServerListDlg.AddItem);
            RunUI(() =>
            {
                editServerDlg.URL = GetPanoramaServerUri(TEST_PUBLIC_FOLDER).ToString();
                editServerDlg.AnonymousServer = true;
            });
            OkDialog(editServerDlg, editServerDlg.OkDialog);
            OkDialog(editServerListDlg, editServerListDlg.OkDialog);
            OkDialog(toolsOptionsDlg, toolsOptionsDlg.OkDialog);

            Assert.IsNotNull(Settings.Default.ServerList);
            Assert.AreEqual(1, Settings.Default.ServerList.Count);

            const string panoramaPublicFolderPath = TEST_PUBLIC_FOLDER + "/2018/MacLean - Baker IMS";
            // Document in the "Panorama Public/2018/MacLean - Baker IMS" folder on https://panoramaweb.org that we will download.
            const string skyDocName = "BSA-Training_2017-09-21_17-59-13.sky.zip";
            var downloadFilePath = TestContext.GetTestResultsPath(skyDocName);
            FileEx.SafeDelete(downloadFilePath, true);
            Assert.IsFalse(File.Exists(downloadFilePath));

            var panoramaFilePickerDlg = ShowDialog<PanoramaFilePicker>(() => SkylineWindow.OpenFromPanorama(downloadFilePath));
            RunUI(() =>
            {
                Assert.IsTrue(panoramaFilePickerDlg.IsLoaded);
                Assert.IsFalse(panoramaFilePickerDlg.FolderBrowser.SelectNode(TEST_FOLDER),
                    "Guest user should not be able to see the private folder '{0}'.", TEST_FOLDER);

                foreach (var folder in panoramaPublicFolderPath.Split('/'))
                {
                    Assert.IsTrue(panoramaFilePickerDlg.FolderBrowser.SelectNode(folder),
                        "Guest user should be able to see folder '{0}' in the folder path '{1}'.", folder, panoramaPublicFolderPath);
                }
            });

            var doc = SkylineWindow.Document;
            OkDialog(panoramaFilePickerDlg, () => Assert.IsTrue(panoramaFilePickerDlg.ClickFile(skyDocName), "Unable to select Skyline document {0}", skyDocName));
            WaitForCondition(() => File.Exists(downloadFilePath));
            var docLoaded = WaitForDocumentChangeLoaded(doc);
            AssertEx.IsDocumentState(docLoaded, null, 1, 34, 38, 404);
            FileEx.SafeDelete(downloadFilePath, true);
            Assert.IsFalse(File.Exists(downloadFilePath));
        }

        // Test viewing webDav browser
        private void TestWebDav()
        {
            var selectedPath = $"/{TEST_FOLDER}/{PANORAMA_FOLDER}/";
            var server = CreatePanoramaServer();
            var serverList = new List<PanoramaServer> { server };

            var remoteDlg = ShowDialog<PanoramaFilePicker>(() => ShowPanoramaFilePicker(serverList, selectedPath));
            WaitForConditionUI(() => remoteDlg.IsLoaded);
            RunUI(() => SelectNode(remoteDlg, "@files"));
            WaitForConditionUI(15000, () => remoteDlg.FileNumber == 4);
            RunUI(() => SelectNode(remoteDlg, NOT_RENAMED_FOLDER));
            WaitForConditionUI(15000, () => remoteDlg.FileNumber == 6);
            OkDialog(remoteDlg, remoteDlg.Close);
        }

        /// <summary>
        /// Hacky way to quickly create the <see cref="PanoramaFilePicker"/> form for testing.
        /// </summary>
        private void ShowPanoramaFilePicker(List<PanoramaServer> serverList, string selectedPath)
        {
            using var remoteDlg = new PanoramaFilePicker(serverList, string.Empty, true, selectedPath);
            remoteDlg.LoadServerData(new SilentProgressMonitor());   // Currently a no-op for WebDAV
            remoteDlg.ShowDialog();
        }
    }
}
