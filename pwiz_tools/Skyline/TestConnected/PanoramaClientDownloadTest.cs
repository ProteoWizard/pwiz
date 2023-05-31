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
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using pwiz.Common.SystemUtil;
using pwiz.PanoramaClient;
using pwiz.Skyline.Properties;
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
        private const string VALID_USER_NAME = "skyline_tester@proteinms.net";
        private const string VALID_PASSWORD = "lclcmsms";
        private const string VALID_SERVER = "https://panoramaweb.org";

        [TestMethod]
        public void TestPanoramaDownloadFile()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            //Test successful download
            TestDownloadFile();

            //Test unsuccessful download with corrupted file
            //TODO: Import file to test folder then delete it and make sure the error is thrown when downloading
            //TestUnsuccessfulDownload();

            //Test canceling a download during longWaitDlg
            TestCancelDownload();

            //Test various download errors
            //TestDownloadErrors();

            //Make sure PanoramaFilePicker states are being preserved between runs
            TestPreserveStates();
        }

        //Downloads and opens a file successfully 
        private void TestDownloadFile()
        {
            var server = new Server(new Uri(VALID_SERVER), VALID_USER_NAME, VALID_PASSWORD);
            var servers = Settings.Default.ServerList;
            servers.Add(server);
            Settings.Default.ServerList = servers;
            Settings.Default.Save();
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
                SkylineWindow.DownloadPanoramaFile(path, "Study9S_Site52_v1.sky.zip", remoteDlg.FileUrl, remoteDlg.ActiveServer, remoteDlg.FileSize);
                Assert.IsTrue(File.Exists(path));
                FileEx.SafeDelete(path, true);
                Assert.IsFalse(File.Exists(path));

            });
            WaitForClosedForm(remoteDlg);
        }

        //TODO: Upload corrupted file to test folder and ensure that corrupted files are being caught in error checking while attempting to download
        private void TestUnsuccessfulDownload()
        {
            
        }

        //Test canceling a download: Does is get deleted? Does the download actually get canceled?
        private void TestCancelDownload()
        {
            var server = new Server(new Uri(VALID_SERVER), VALID_USER_NAME, VALID_PASSWORD);
            var servers = Settings.Default.ServerList;
            servers.Add(server);
            Settings.Default.ServerList = servers;
            Settings.Default.Save();
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
                SkylineWindow.DownloadPanoramaFile(path, "Study9S_Site52_v1.sky.zip", remoteDlg.FileUrl, remoteDlg.ActiveServer, remoteDlg.FileSize, true);
                Assert.IsFalse(File.Exists(path));

            });
            WaitForClosedForm(remoteDlg);
        }

        private void TestDownloadErrors()
        {
            var state = string.Empty;
            var server = new Server(new Uri(VALID_SERVER), VALID_USER_NAME, VALID_PASSWORD);
            var servers = Settings.Default.ServerList;
            servers.Add(server);
            Settings.Default.ServerList = servers;
            Settings.Default.Save();
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() =>
                SkylineWindow.OpenFromPanorama());
            var testClient = new TestPanoramaClient(VALID_SERVER, VALID_USER_NAME, VALID_PASSWORD);
            WaitForCondition(9000, () => remoteDlg.IsLoaded);

            RunUI(() =>
            {
                var selectedPath = string.Empty;
                remoteDlg.FolderBrowser.SelectNode("SkylineTest");
                remoteDlg.FolderBrowser.SelectNode("ForPanoramaClientTest");
                remoteDlg.ClickFile("Study9S_Site52_v1.sky.zip");
                Debug.WriteLine(Directory.GetCurrentDirectory());
                var path = Path.Combine(Directory.GetCurrentDirectory(),
                    "Study9S_Site52_v1.sky.zip");
                remoteDlg.ClickOpen();
                remoteDlg.Close();
            });
            WaitForClosedForm(remoteDlg);
        }

        //Make sure PanoramaFilePicker states are being preserved between runs
        private void TestPreserveStates()
        {
            var state = string.Empty;
            var server = new Server(new Uri(VALID_SERVER), VALID_USER_NAME, VALID_PASSWORD);
            var servers = Settings.Default.ServerList;
            servers.Add(server);
            Settings.Default.ServerList = servers;
            Settings.Default.Save();
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() =>
                SkylineWindow.OpenFromPanorama());
            var panoramaWebClient = new WebPanoramaClient(server.URI);

            WaitForCondition(9000, () => remoteDlg.IsLoaded);

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

            //Use this method to throw specific errors and make sure they are caught correctly 
            public void DownloadFile(string warning, string fileName, long fileSize, string realName, PanoramaServer server, IProgressMonitor pm, IProgressStatus progressStatus, bool cancel = false)
            {
                throw new ArgumentNullException(warning);
            }
        }
    }
}
