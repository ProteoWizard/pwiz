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
using System.IO;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
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

        [TestMethod]
        public void TestPanoramaDownloadFile()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            //Test successful download
            TestDownloadFile();

            //Test various download errors
            TestDownloadErrors();

            //Make sure PanoramaFilePicker states are being preserved between runs
            TestPreserveStates();

            //Test downloading from test server
            TestMissingFile();

            //Test downloading a file that has been renamed on Panorama
            TestRenamedFile();

            //Test adding a new server to Panorama - Test with and without username and password
            //TestAddServer();
        }

        private void AddSkylineServer()
        {
            var server = new Server(new Uri(PANORAMA_WEB), TEST_USER, TEST_PASSWORD);
            var servers = Settings.Default.ServerList;
            servers.Add(server);
            Settings.Default.ServerList = servers;
            Settings.Default.Save();
        }

        //Downloads and opens a file successfully 
        private void TestDownloadFile()
        {
            AddSkylineServer();
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() => SkylineWindow.OpenFromPanorama());
            WaitForCondition(9000, () => remoteDlg.IsLoaded);
            
            RunUI(() =>
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "Study9S_Site52_v1.sky.zip");
                Assert.IsFalse(File.Exists(path));
                remoteDlg.FolderBrowser.SelectNode("SkylineTest");
                remoteDlg.FolderBrowser.SelectNode("ForPanoramaClientTest");
                remoteDlg.ClickFile("Study9S_Site52_v1.sky.zip");
                remoteDlg.ClickOpen();
                var result = SkylineWindow.DownloadPanoramaFile(path, "Study9S_Site52_v1.sky.zip", remoteDlg.FileUrl, remoteDlg.ActiveServer, remoteDlg.FileSize);
                Assert.IsTrue(result);
                Assert.IsTrue(File.Exists(path));
                FileEx.SafeDelete(path, true);
                Assert.IsFalse(File.Exists(path));

            });
            WaitForClosedForm(remoteDlg);
        }

        private void TestDownloadErrors()
        {
            AddSkylineServer();
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() =>
                SkylineWindow.OpenFromPanorama());
            var panoramaTestClient = new TestPanoramaClient(PANORAMA_WEB, TEST_USER, TEST_PASSWORD);
            var path = Path.Combine(Directory.GetCurrentDirectory(),
                "Study9S_Site52_v1.sky.zip");
            WaitForCondition(9000, () => remoteDlg.IsLoaded);

            RunUI(() =>
            {
                remoteDlg.FolderBrowser.SelectNode("SkylineTest");
                var url = remoteDlg.FileUrl;
                remoteDlg.FolderBrowser.SelectNode("ForPanoramaClientTest");
                remoteDlg.ClickFile("Study9S_Site52_v1.sky.zip");
                remoteDlg.ClickOpen();
            });

            var errorDlg = ShowDialog<MessageDlg>(() => RunUI(() =>
            {
                SkylineWindow.DownloadPanoramaFile(path, "Study9S_Site52_v1.sky.zip", remoteDlg.FileUrl,
                    remoteDlg.ActiveServer, 1, panoramaTestClient);
            }));
            Assert.AreEqual(new NullReferenceException().Message, errorDlg.Message);
            Assert.IsFalse(File.Exists(path));
            OkDialog(errorDlg, errorDlg.OkDialog);

            errorDlg = ShowDialog<MessageDlg>(() => RunUI(() =>
            {
                SkylineWindow.DownloadPanoramaFile(path, "Study9S_Site52_v1.sky.zip", remoteDlg.FileUrl,
                    remoteDlg.ActiveServer, 2, panoramaTestClient);
            }));
            Assert.AreEqual(new WebException().Message, errorDlg.Message);
            Assert.IsFalse(File.Exists(path));
            OkDialog(errorDlg, errorDlg.OkDialog);

            errorDlg = ShowDialog<MessageDlg>(() => RunUI(() =>
            {
                SkylineWindow.DownloadPanoramaFile(path, "Study9S_Site52_v1.sky.zip", remoteDlg.FileUrl,
                    remoteDlg.ActiveServer, 3, panoramaTestClient);
            }));
            Assert.AreEqual(new FileNotFoundException().Message, errorDlg.Message);
            Assert.IsFalse(File.Exists(path));
            OkDialog(errorDlg, errorDlg.OkDialog);

            errorDlg = ShowDialog<MessageDlg>(() => RunUI(() =>
            {
                SkylineWindow.DownloadPanoramaFile(path, "Study9S_Site52_v1.sky.zip", remoteDlg.FileUrl,
                    remoteDlg.ActiveServer, 4, panoramaTestClient);
            }));
            Assert.AreEqual(new UnauthorizedAccessException().Message, errorDlg.Message);
            Assert.IsFalse(File.Exists(path));
            OkDialog(errorDlg, errorDlg.OkDialog);

            errorDlg = ShowDialog<MessageDlg>(() => RunUI(() =>
            {
                SkylineWindow.DownloadPanoramaFile(path, "Study9S_Site52_v1.sky.zip", remoteDlg.FileUrl,
                    remoteDlg.ActiveServer, 5, panoramaTestClient);
            }));
            Assert.AreEqual(new InvalidOperationException().Message, errorDlg.Message);
            Assert.IsFalse(File.Exists(path));
            OkDialog(errorDlg, errorDlg.OkDialog);
            WaitForClosedForm(remoteDlg);
        }

        //Make sure PanoramaFilePicker states are being preserved between runs
        private void TestPreserveStates()
        {
            var state = string.Empty;
            AddSkylineServer();
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() => SkylineWindow.OpenFromPanorama());
            var dlg = remoteDlg;
            WaitForCondition(9000, () => dlg.IsLoaded);

            RunUI(() =>
            {
                remoteDlg.FolderBrowser.SelectNode("SkylineTest");
                remoteDlg.FolderBrowser.SelectNode("ForPanoramaClientTest");
                remoteDlg.Close();
                state = remoteDlg.TreeState;
            });
            WaitForClosedForm(remoteDlg);

            remoteDlg = ShowDialog<PanoramaFilePicker>(() =>
                SkylineWindow.OpenFromPanorama());
            WaitForCondition(9000, () => remoteDlg.IsLoaded);

            RunUI(() =>
            {
                Assert.AreEqual(state, remoteDlg.TreeState);
                Assert.IsTrue(remoteDlg.FolderBrowser.IsSelected("ForPanoramaClientTest"));
                remoteDlg.Close();
            });
            WaitForClosedForm(remoteDlg);

        }
        
        //Test downloading a file that has been deleted on the server
        private void TestMissingFile()
        {
            AddSkylineServer();
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() => SkylineWindow.OpenFromPanorama());
            WaitForCondition(9000, () => remoteDlg.IsLoaded);
            var path = Path.Combine(Directory.GetCurrentDirectory(), "FileDeletedFromServer.sky.zip");
            RunUI(() =>
            {
                Assert.IsFalse(File.Exists(path));
                remoteDlg.FolderBrowser.SelectNode("SkylineTest");
                remoteDlg.FolderBrowser.SelectNode("ForPanoramaClientTest");
                remoteDlg.ClickFile("FileDeletedFromServer.sky.zip");
                remoteDlg.ClickOpen();
            });
            var errorDlg = ShowDialog<MessageDlg>(() => RunUI(() =>
            {
                SkylineWindow.DownloadPanoramaFile(path, "FileDeletedFromServer.sky.zip", remoteDlg.FileUrl,
                    remoteDlg.ActiveServer, remoteDlg.FileSize);
            }));
            Assert.IsFalse(File.Exists(path));
            Assert.AreEqual("@File does not exist. It may have been deleted on the server.", errorDlg.Message);
            OkDialog(errorDlg, errorDlg.OkDialog);
            WaitForClosedForm(remoteDlg);
        }

        //Test downloading a file that has been renamed on Panorama
        private void TestRenamedFile()
        {
            AddSkylineServer();
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() => SkylineWindow.OpenFromPanorama());
            WaitForCondition(9000, () => remoteDlg.IsLoaded);

            RunUI(() =>
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "TestFileRename.sky.zip");
                Assert.IsFalse(File.Exists(path));
                remoteDlg.FolderBrowser.SelectNode("SkylineTest");
                remoteDlg.FolderBrowser.SelectNode("ForPanoramaClientTest");
                remoteDlg.ClickFile("TestFileRename.sky.zip");
                remoteDlg.ClickOpen();
                Assert.AreNotEqual("TestFileRename.sky.zip", remoteDlg.GetItemName(0));
                var result = SkylineWindow.DownloadPanoramaFile(path, "TestFileRename.sky.zip", remoteDlg.FileUrl, remoteDlg.ActiveServer, remoteDlg.FileSize);
                Assert.IsTrue(result);
                Assert.IsTrue(File.Exists(path));

                FileEx.SafeDelete(path, true);
                Assert.IsFalse(File.Exists(path));

            });
            WaitForClosedForm(remoteDlg);
        }

        //TODO: Test adding a new server to Panorama - Test with and without username and password 
        private void TestAddServer()
        {
            var serverDlg = ShowDialog<MultiButtonMsgDlg>(() => RunUI(() =>
            {
                SkylineWindow.OpenFromPanorama();
            }));
            var editItem = ShowDialog<EditServerDlg>(serverDlg.ClickOk);
            RunUI(() =>
            {
                editItem.URL = PANORAMA_WEB;
                editItem.Username = TEST_USER;
                editItem.Password = TEST_PASSWORD;
            });
            OkDialog(editItem, editItem.OkDialog);
            //TODO: Assert something here
        }

        public class TestPanoramaClient : IPanoramaClient
        {
            public Uri ServerUri { get; set; }

            public string Server { get; }
            public string Username { get; }
            public string Password { get; }
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

            public ServerState GetServerState()
            {
                throw new NotImplementedException();
            }

            public UserState IsValidUser(string username, string password)
            {
                throw new NotImplementedException();
            }

            public FolderState IsValidFolder(string folderPath, string username, string password)
            {
                return FolderState.valid;
            }

            public FolderOperationStatus CreateFolder(string parentPath, string folderName, string username, string password)
            {
                throw new NotImplementedException();
            }

            public FolderOperationStatus DeleteFolder(string folderPath, string username, string password)
            {
                throw new NotImplementedException();
            }

            public JToken GetInfoForFolders(PanoramaServer server, string folder)
            {
                throw new NotImplementedException();
            }

            public void DownloadFile(string fileUrl, string fileName, long fileSize, string realName,
                PanoramaServer server, IProgressMonitor pm, IProgressStatus progressStatus)
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
