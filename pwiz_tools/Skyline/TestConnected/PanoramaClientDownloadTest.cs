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
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.PanoramaClient;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;
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
    public class PanoramaClientDownloadTest : AbstractFunctionalTest
    {
        private const string TEST_USER = "skyline_tester@proteinms.net";
        private const string TEST_PASSWORD = "lclcmsms";
        private const string PANORAMA_WEB = "https://panoramaweb.org";
        private const string TEST_FOLDER = "SkylineTest";
        private const string PANORAMA_FOLDER = "ForPanoramaClientTest";
        private const string TEST_FILE = "Study9S_Site52_v1.sky.zip";
        private const string DELETED_FILE = "FileDeletedFromServer.sky.zip";
        private const string RENAMED_FILE = "TestFileRename.sky.zip";

        [TestMethod]
        public void TestPanoramaDownloadFile()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
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

            // Test adding a new server to Panorama - Test with and without username and password
            TestAddServer();
        }

        private void AddPanoramaServers()
        {
            var servers = Settings.Default.ServerList;
            servers.Add(CreatePanoramaServer());
            Settings.Default.ServerList = servers;
            Settings.Default.Save();
        }

        private Server CreatePanoramaServer()
        {
            return new Server(new Uri(PANORAMA_WEB), TEST_USER, TEST_PASSWORD);
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
                Assert.IsTrue(remoteDlg.FolderBrowser.SelectNode(TEST_FOLDER), "Unable to select {0}", TEST_FOLDER);
                Assert.IsTrue(remoteDlg.FolderBrowser.SelectNode(PANORAMA_FOLDER), "Unable to select {0}", PANORAMA_FOLDER);
            });
            var doc = SkylineWindow.Document;
            OkDialog(remoteDlg, () => Assert.IsTrue(remoteDlg.ClickFile(TEST_FILE), "Unable to click file {0}", TEST_FILE));
            WaitForCondition(() => File.Exists(path));
            var docLoaded = WaitForDocumentChangeLoaded(doc);
            AssertEx.IsDocumentState(docLoaded, null, 7, 22, 23, 115);
            FileEx.SafeDelete(path, true);
            Assert.IsFalse(File.Exists(path));
        }

        private void TestDownloadErrors()
        {
            AddPanoramaServers();
            var panoramaTestClient = new TestPanoramaClient(PANORAMA_WEB, TEST_USER, TEST_PASSWORD);
            var path = TestContext.GetTestResultsPath(TEST_FILE);
            var server = CreatePanoramaServer();
            var errorDlg = ShowDialog<MessageDlg>(() => RunUI(() =>
            {
                SkylineWindow.DownloadPanoramaFile(path, TEST_FILE, null,
                    server, 1, panoramaTestClient);
            }));
            Assert.AreEqual(new NullReferenceException().Message, errorDlg.Message);
            OkDialog(errorDlg, errorDlg.OkDialog);

            errorDlg = ShowDialog<MessageDlg>(() => RunUI(() =>
            {
                SkylineWindow.DownloadPanoramaFile(path, TEST_FILE, null,
                    server, 2, panoramaTestClient);
            }));
            Assert.AreEqual(new WebException().Message, errorDlg.Message);
            OkDialog(errorDlg, errorDlg.OkDialog);

            errorDlg = ShowDialog<MessageDlg>(() => RunUI(() =>
            {
                SkylineWindow.DownloadPanoramaFile(path, TEST_FILE, null,
                    server, 3, panoramaTestClient);
            }));
            Assert.AreEqual(new FileNotFoundException().Message, errorDlg.Message);
            OkDialog(errorDlg, errorDlg.OkDialog);

            errorDlg = ShowDialog<MessageDlg>(() => RunUI(() =>
            {
                SkylineWindow.DownloadPanoramaFile(path, TEST_FILE, null,
                    server, 4, panoramaTestClient);
            }));
            Assert.AreEqual(new UnauthorizedAccessException().Message, errorDlg.Message);
            OkDialog(errorDlg, errorDlg.OkDialog);

            errorDlg = ShowDialog<MessageDlg>(() => RunUI(() =>
            {
                SkylineWindow.DownloadPanoramaFile(path, TEST_FILE, null,
                    server, 5, panoramaTestClient);
            }));
            Assert.AreEqual(new InvalidOperationException().Message, errorDlg.Message);
            OkDialog(errorDlg, errorDlg.OkDialog);
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
                Assert.IsTrue(remoteDlg.FolderBrowser.SelectNode(TEST_FOLDER), "Unable to select {0}", TEST_FOLDER);
                Assert.IsTrue(remoteDlg.FolderBrowser.SelectNode(PANORAMA_FOLDER), "Unable to select {0}", PANORAMA_FOLDER);
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
                Assert.IsTrue(remoteDlg.FolderBrowser.SelectNode(TEST_FOLDER), "Unable to select {0}", TEST_FOLDER);
                Assert.IsTrue(remoteDlg.FolderBrowser.SelectNode(PANORAMA_FOLDER), "Unable to select {0}", PANORAMA_FOLDER);
                Assert.IsTrue(remoteDlg.ClickFile(DELETED_FILE), "Unable to click file {0}", DELETED_FILE);
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
                Assert.IsTrue(remoteDlg.FolderBrowser.SelectNode(TEST_FOLDER), "Unable to select {0}", TEST_FOLDER);
                Assert.IsTrue(remoteDlg.FolderBrowser.SelectNode(PANORAMA_FOLDER), "Unable to select {0}", PANORAMA_FOLDER);
                Assert.IsTrue(remoteDlg.ClickFile(RENAMED_FILE), "Unable to click file {0}", RENAMED_FILE);
                Assert.AreNotEqual(RENAMED_FILE, remoteDlg.GetItemName(0));
            });
            WaitForClosedForm(remoteDlg);
            WaitForCondition(() => File.Exists(path));
            var docLoaded = WaitForDocumentChangeLoaded(doc);
            AssertEx.IsDocumentState(docLoaded, null, 12, 68, 126, 576);
            FileEx.SafeDelete(path, true);
            Assert.IsFalse(File.Exists(path));
        }

        //TODO: Test adding a new server to Panorama - Test with and without username and password 
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
                editItem.URL = PANORAMA_WEB;
                editItem.Username = TEST_USER;
                editItem.Password = TEST_PASSWORD;
            });
            var remoteDlg = ShowDialog<PanoramaFilePicker>(editItem.OkDialog);
            if (Settings.Default.ServerList != null)
                Assert.AreEqual(1, Settings.Default.ServerList.Count);
            RunUI(() => Assert.IsTrue(remoteDlg.IsLoaded));
            OkDialog(remoteDlg, remoteDlg.Close);
        }

        // Test viewing webDav browser
        private void TestWebDav()
        {
            var selectedPath = "/SkylineTest/ForPanoramaClientTest/";
            var server = new PanoramaServer(new Uri(PANORAMA_WEB), TEST_USER, TEST_PASSWORD);
            var serverList = new List<PanoramaServer> { server };

            var remoteDlg = ShowDialog<PanoramaFilePicker>(() => ShowPanoramaFilePicker(serverList, selectedPath));
            WaitForConditionUI(() => remoteDlg.IsLoaded);
            RunUI(() => Assert.IsTrue(remoteDlg.FolderBrowser.SelectNode("@files"), "Unable to select @files")); 
            WaitForConditionUI(() => remoteDlg.FileNumber == 13);
            RunUI(() => Assert.IsTrue(remoteDlg.FolderBrowser.SelectNode("FileRenamedOnServer"), "Unable to select FileRenamedOnServer"));
            WaitForConditionUI(() => remoteDlg.FileNumber == 6);
            OkDialog(remoteDlg, remoteDlg.Close);
        }

        /// <summary>
        /// Hacky way to quickly create the <see cref="PanoramaFilePicker"/> form for testing.
        /// </summary>
        private void ShowPanoramaFilePicker(List<PanoramaServer> serverList, string selectedPath)
        {
            using var remoteDlg = new PanoramaFilePicker(serverList, string.Empty, true, selectedPath);
            remoteDlg.InitializeDialog();   // CONSIDER: Should this be using LongOptionRunner like SkylineWindow?
            remoteDlg.ShowDialog();
        }

        private class TestPanoramaClient : BaseTestPanoramaClient
        {
            public string Server { get; }
            public TestPanoramaClient(string server, string username, string password)
            {
                Server = server;
                Username = username;
                Password = password;
                try
                {
                    ServerUri = new Uri(server);
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            public override void DownloadFile(string fileUrl, string fileName, long fileSize, string realName,
                IProgressMonitor pm, IProgressStatus progressStatus)
            {
                Exception e = null;
                switch (fileSize)
                {
                    case 1:
                        e = new NullReferenceException();
                        break;
                    case 2:
                        e = new WebException();
                        break;
                    case 3:
                        e = new FileNotFoundException();
                        break;
                    case 4:
                        e = new UnauthorizedAccessException();
                        break;
                    case 5:
                        e = new InvalidOperationException();
                        break;
                }
                pm.UpdateProgress(progressStatus = progressStatus.ChangeErrorException(e));
            }
        }
    }
}
