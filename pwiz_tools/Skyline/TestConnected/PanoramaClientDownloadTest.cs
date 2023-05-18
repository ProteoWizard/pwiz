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
using pwiz.Common.SystemUtil;
using pwiz.PanoramaClient;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Databinding.Entities;
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
            TestUnsuccessfulDownload();
    

        }

        //Downloads and opens a file successfully 
        private void TestDownloadFile()
        {
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
                var selectedPath = string.Empty;
                remoteDlg.FolderBrowser.SelectNode("SkylineTest");
                remoteDlg.FolderBrowser.SelectNode("ForPanoramaClientTest");
                remoteDlg.ClickFile("Study9S_Site52_v1.sky.zip");
                Debug.WriteLine(Directory.GetCurrentDirectory());
                var path = Path.Combine(Directory.GetCurrentDirectory(),
                    "Study9S_Site52_v1.sky.zip");
                using var longWaitDlg = new LongWaitDlg();
                var progressStatus = longWaitDlg.PerformWork(SkylineWindow, 800,
                    progressMonitor => panoramaWebClient.DownloadFile(remoteDlg.FileUrl, path, remoteDlg.FileSize,
                        remoteDlg.FileName, remoteDlg.ActiveServer,
                        progressMonitor, new ProgressStatus()));
                if (progressStatus.IsCanceled || progressStatus.IsError)
                {
                    FileEx.SafeDelete(path, true);
                    return;
                }
                Assert.IsTrue(File.Exists(path));
                var result = SkylineWindow.OpenSharedFile(path);
                Assert.IsTrue(result);
                FileEx.SafeDelete(path, true);
                Assert.IsFalse(File.Exists(path));

                remoteDlg.Close();
            });
            WaitForClosedForm(remoteDlg);
        }

        private void TestUnsuccessfulDownload()
        {
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
                remoteDlg.ClickFile("Study9S_Site52_v1.sky.zip");
                Debug.WriteLine(Directory.GetCurrentDirectory());
                var path = Path.Combine(Directory.GetCurrentDirectory(),
                    "Study9S_Site52_v1.sky.zip");
                using var longWaitDlg = new LongWaitDlg();
                var progressStatus = longWaitDlg.PerformWork(SkylineWindow, 800,
                    progressMonitor => panoramaWebClient.DownloadFile(remoteDlg.FileUrl, path, remoteDlg.FileSize,
                        remoteDlg.FileName, remoteDlg.ActiveServer,
                        progressMonitor, new ProgressStatus()));
                if (progressStatus.IsCanceled || progressStatus.IsError)
                {
                    FileEx.SafeDelete(path, true);
                    return;
                }
                Assert.IsTrue(File.Exists(path));
                SkylineWindow.OpenSharedFile(path);
                Assert.AreEqual("Study9S_Site52_v1.sky.zip", SkylineWindow.Text);
                FileEx.SafeDelete(path, true);
                Assert.IsFalse(File.Exists(path));
                remoteDlg.Close();
            });
            WaitForClosedForm(remoteDlg);
        }
    }
}
