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
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            //TestDownloadFile();

            //Test canceling a download during longWaitDlg
            //TestCancelDownload();

            //Test various download errors
            //TestDownloadErrors();

            //Make sure PanoramaFilePicker states are being preserved between runs
            //TestPreserveStates();

            //Test downloading from test server
            //TestMissingFile();

            //Test downloading a file that has been renamed on Panorama
            //TestRenamedFile();

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


        //Test canceling a download: Does is get deleted? Does the download actually get canceled?
        private void TestCancelDownload()
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
                var result = SkylineWindow.DownloadPanoramaFile(path, "Study9S_Site52_v1.sky.zip", remoteDlg.FileUrl, remoteDlg.ActiveServer, remoteDlg.FileSize, null);
                Assert.IsFalse(result);
                Assert.IsFalse(File.Exists(path));

            });
            WaitForClosedForm(remoteDlg);
        }

        private void TestDownloadErrors()
        {
            AddSkylineServer();
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() =>
                SkylineWindow.OpenFromPanorama());
            WaitForCondition(9000, () => remoteDlg.IsLoaded);

            RunUI(() =>
            {
                //TODO: Test that a specific error is thrown and the message is shown, change the scenarios that I'm testing to be more realistic, not having a server or url wouldn't likely happen, downloading to somewhere where a user does not have permissions for example
                var selectedPath = string.Empty;
                remoteDlg.FolderBrowser.SelectNode("SkylineTest");
                remoteDlg.FolderBrowser.SelectNode("ForPanoramaClientTest");
                remoteDlg.ClickFile("Study9S_Site52_v1.sky.zip");
                var path = Path.Combine(Directory.GetCurrentDirectory(),
                    "Study9S_Site52_v1.sky.zip");
                remoteDlg.ClickOpen();
                var result = SkylineWindow.DownloadPanoramaFile(path, "Study9S_Site52_v1.sky.zip", string.Empty,
                    remoteDlg.ActiveServer, remoteDlg.FileSize);
                Assert.IsFalse(result);
                Assert.IsFalse(File.Exists(path));

                result = SkylineWindow.DownloadPanoramaFile(null, "Study9S_Site52_v1.sky.zip", remoteDlg.FileUrl,
                    remoteDlg.ActiveServer, remoteDlg.FileSize);
                Assert.IsFalse(result);
                Assert.IsFalse(File.Exists(path));

                result = SkylineWindow.DownloadPanoramaFile(path, "Study9S_Site52_v1.sky.zip", remoteDlg.FileUrl,
                    null, remoteDlg.FileSize);
                Assert.IsFalse(result);
                Assert.IsFalse(File.Exists(path));

                result = SkylineWindow.DownloadPanoramaFile(null, "Study9S_Site52_v1.sky.zip", remoteDlg.FileUrl,
                    remoteDlg.ActiveServer, -1);
                Assert.IsFalse(result);
                Assert.IsFalse(File.Exists(path));

            });
            WaitForClosedForm(remoteDlg);
        }

        //Make sure PanoramaFilePicker states are being preserved between runs
        private void TestPreserveStates()
        {
            var state = string.Empty;
            AddSkylineServer();
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() =>
                SkylineWindow.OpenFromPanorama());

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
        
        //Test downloading a file that has been deleted on the server
        private void TestMissingFile()
        {
            AddSkylineServer();
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() => SkylineWindow.OpenFromPanorama());
            WaitForCondition(9000, () => remoteDlg.IsLoaded);

            RunUI(() =>
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "FileDeletedFromServer.sky.zip");
                Assert.IsFalse(File.Exists(path));
                remoteDlg.FolderBrowser.SelectNode("SkylineTest");
                remoteDlg.FolderBrowser.SelectNode("ForPanoramaClientTest");
                remoteDlg.ClickFile("FileDeletedFromServer.sky.zip");
                remoteDlg.ClickOpen();
                var result = SkylineWindow.DownloadPanoramaFile(path, "FileDeletedFromServer.sky.zip", remoteDlg.FileUrl, remoteDlg.ActiveServer, remoteDlg.FileSize);
                Assert.IsFalse(result);
                Assert.IsFalse(File.Exists(path));

            });
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
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() => SkylineWindow.OpenFromPanorama());
            WaitForCondition(9000, () => remoteDlg.IsLoaded);
        }
    }
}
