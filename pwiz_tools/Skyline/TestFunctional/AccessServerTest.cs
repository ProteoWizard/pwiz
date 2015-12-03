/*
 * Original author: Shannon Joyner <saj9191 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;
using Newtonsoft.Json.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class AccessServerTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAccessServer()
        {
            TestFilesZip = @"TestFunctional\AccessServerTest.zip";
            RunFunctionalTest();
        }

        private readonly IPanoramaClient _testClient = new TestPanoramaClient(); // Use null for WebClient
        private readonly IPanoramaPublishClient _testPublishClient = new TestPanoramaPublishClient();
        private const string VALID_USER_NAME = "user";
        private const string VALID_PASSWORD = "password";

        private const string VALID_PANORAMA_SERVER = "https://128.208.10.133:8070/";
        private const string VALID_NON_PANORAMA_SERVER = "www.google.com";
        private const string NON_EXISTENT_SERVER = "www.noexist.edu";
        private const string UNKNOWN_STATE_SERVER = "unknown.server-state.com";

        private ToolOptionsUI ToolOptionsDlg { get; set; }
        private static string Server { get; set; }
        private string Username { get; set; }
        private string Password { get; set; }

        private const string NO_WRITE_NO_TARGETED = "No write permissions/TargetedMS is not active";
        private const string NO_WRITE_TARGETED = "Does not have write permission/TargetedMS active";
        private const string WRITE_TARGETED = "Write permissions/TargetedMS active";
        private const string WRITE_NO_TARGETED = "Write permissions/TargetedMS is not active";

        protected override void DoTest()
        {

            ToolOptionsDlg = ShowDialog<ToolOptionsUI>(() => SkylineWindow.ShowToolOptionsUI());

            // Incorrect password.
            Server = VALID_PANORAMA_SERVER;
            Username = VALID_USER_NAME;
            Password = "bad password";
            CheckServerInfoFailure(0);

            // Non-existant username.
            Username = "fake username";
            CheckServerInfoFailure(0);

            // Successful login.
            Username = VALID_USER_NAME;
            Password = VALID_PASSWORD;
            CheckServerInfoSuccess(1);

            // Existing non-Panorama server
            Server = VALID_NON_PANORAMA_SERVER;
            CheckServerInfoFailure(1);

            // Same server (subpage of server)
            Server = VALID_PANORAMA_SERVER + "/libkey";
            CheckServerInfoFailure(1);

            // Non-existent server
            Server = NON_EXISTENT_SERVER;
            CheckServerInfoFailure(1);

            // Unknown state server
            if (_testClient != null)
            {
                Server = UNKNOWN_STATE_SERVER;
                CheckServerInfoFailure(1);
            }

            // Bad URI Format
            Server = "w ww.google.com";
            CheckServerInfoFailure(1);

            // No server given
            Server = " ";
            CheckServerInfoFailure(1);

            OkDialog(ToolOptionsDlg, ToolOptionsDlg.OkDialog);

            RunUI(() => SkylineWindow.SaveDocument(TestContext.GetTestPath("test.sky")));

            CheckPublishFailure(VALID_PANORAMA_SERVER, Resources.PublishDocumentDlg_OkDialog_Please_select_a_folder);
            CheckPublishFailure(NO_WRITE_TARGETED,
                Resources
                    .PublishDocumentDlg_UploadSharedZipFile_You_do_not_have_permission_to_upload_to_the_given_folder);
            CheckPublishFailure(WRITE_NO_TARGETED,
                Resources
                    .PublishDocumentDlg_UploadSharedZipFile_You_do_not_have_permission_to_upload_to_the_given_folder);
            CheckPublishSuccess(WRITE_TARGETED, false);

            // Document has no chromatograms, should be published regardless of skyd version supported by server -- Success
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("no_chromatograms.sky")));
            WaitForDocumentLoaded();
            ((TestPanoramaPublishClient)_testPublishClient).ServerSkydVersion = "7";
            CheckPublishSuccess(WRITE_TARGETED, false);

            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("skyd9.sky")));
            WaitForDocumentLoaded();
            // Server supports a version lower than the chromatogram cache in this document -- Fail
            ((TestPanoramaPublishClient)_testPublishClient).ServerSkydVersion = "7";
            CheckPublishFailure(WRITE_TARGETED, string.Format(Resources.PublishDocumentDlg_ServerSupportsSkydVersion_, "9"));
            Assert.AreEqual(SkylineWindow.Document.Settings.DataSettings.PanoramaPublishUri, null);

            // Server supports the version of the chromatogram cache in this document -- Success
            ((TestPanoramaPublishClient) _testPublishClient).ServerSkydVersion = "9";
            CheckPublishSuccess(WRITE_TARGETED, false);
            Assert.AreNotEqual(SkylineWindow.Document.Settings.DataSettings.PanoramaPublishUri, null);

            // Document should be published even if an invalid version is returned by the server -- Success 
            ((TestPanoramaPublishClient)_testPublishClient).ServerSkydVersion = "NINE";
            CheckPublishSuccess(WRITE_TARGETED, true);
        }

        public void CheckPublishSuccess(string nodeSelection, bool expectingSavedUri)
        {
            bool hasSavedUri = SkylineWindow.Document.Settings.DataSettings.PanoramaPublishUri != null;
            Assert.AreEqual(expectingSavedUri, hasSavedUri);
            if (hasSavedUri)
            {
                // Test click don't use saved uri
                var publishToSavedUri = ShowDialog<MultiButtonMsgDlg>(() => SkylineWindow.ShowPublishDlg(_testPublishClient));
                OkDialog(publishToSavedUri, publishToSavedUri.ClickYes);
            }
            else
            {
                var publishDocumentDlg = ShowDialog<PublishDocumentDlg>(() => SkylineWindow.ShowPublishDlg(_testPublishClient));
                WaitForCondition(60 * 1000, () => publishDocumentDlg.IsLoaded);
                RunUI(() =>
                {
                    publishDocumentDlg.SelectItem(nodeSelection);
                    Assert.AreEqual(nodeSelection, publishDocumentDlg.GetSelectedNodeText());
                });
                OkDialog(publishDocumentDlg, publishDocumentDlg.OkDialog);
            }
            var goToSite = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(goToSite, goToSite.ClickNo);
        }

        public void CheckPublishFailure(string nodeSelection, string failureMessage)
        {
            var publishDocumentDlg = ShowDialog<PublishDocumentDlg>(() => SkylineWindow.ShowPublishDlg(_testPublishClient));
            WaitForCondition(60 * 1000, () => publishDocumentDlg.IsLoaded);
            RunUI(() =>
            {
                publishDocumentDlg.SelectItem(nodeSelection);
                Assert.AreEqual(nodeSelection, publishDocumentDlg.GetSelectedNodeText());
            });

            RunDlg<MessageDlg>(publishDocumentDlg.OkDialog, messageDlg =>
            {
                Assert.AreEqual(messageDlg.Message, failureMessage);
                messageDlg.OkDialog();
            });
            OkDialog(publishDocumentDlg, publishDocumentDlg.CancelButton.PerformClick);
        }

        public void CheckServerInfoFailure(int serverCount)
        {
            var editServerListDlg = ShowDialog<EditListDlg<SettingsListBase<Server>, Server>>(ToolOptionsDlg.EditServers);
            var editServerDlg = ShowDialog<EditServerDlg>(editServerListDlg.AddItem);
            RunUI(() =>
                      {
                          if (_testClient != null)
                              editServerDlg.PanoramaClient = _testClient;
                          editServerDlg.URL = Server;
                          editServerDlg.Username = Username;
                          editServerDlg.Password = Password;
                      });
            RunDlg<MessageDlg>(editServerDlg.OkDialog, messageDlg => messageDlg.OkDialog());
            RunUI(() =>
                      {
                          editServerDlg.CancelButton.PerformClick();
                          editServerListDlg.OkDialog();
                      });
            WaitForClosedForm(editServerDlg);
            WaitForClosedForm(editServerListDlg);
            Assert.AreEqual(serverCount, Settings.Default.ServerList.Count);
        }

        private void CheckServerInfoSuccess(int serverCount)
        {
            var editServerListDlg = ShowDialog<EditListDlg<SettingsListBase<Server>, Server>>(ToolOptionsDlg.EditServers);
            var editServerDlg = ShowDialog<EditServerDlg>(editServerListDlg.AddItem);
            RunUI(() =>
                      {
                          if (_testClient != null)
                              editServerDlg.PanoramaClient = _testClient;
                          editServerDlg.URL = Server;
                          editServerDlg.Username = Username;
                          editServerDlg.Password = Password;
                          editServerDlg.OkDialog();
                          editServerListDlg.OkDialog();
                      });
            WaitForClosedForm(editServerDlg);
            WaitForClosedForm(editServerListDlg);
            Assert.AreEqual(serverCount, Settings.Default.ServerList.Count);
        }

        private class TestPanoramaClient : IPanoramaClient
        {
            public Uri ServerUri { get { return null; } }
            
            public ServerState GetServerState()
            {
                if (Server.Contains(VALID_PANORAMA_SERVER) ||
                    string.Equals(Server, VALID_NON_PANORAMA_SERVER))
                    return ServerState.available;

                else if (string.Equals(Server, UNKNOWN_STATE_SERVER))
                    return ServerState.unknown;

                return ServerState.missing;
            }

            public PanoramaState IsPanorama()
            {
                if (Server.Contains(VALID_PANORAMA_SERVER))
                    return PanoramaState.panorama;
                return PanoramaState.other;
            }

            public UserState IsValidUser(string username, string password)
            {
                if (string.Equals(username, VALID_USER_NAME) &&
                    string.Equals(password, VALID_PASSWORD))
                {
                    return UserState.valid;
                }
                return UserState.nonvalid;
            }

            public FolderState IsValidFolder(string folderPath, string username, string password)
            {
                return FolderState.valid;
            }
        }

        private class TestPanoramaPublishClient : AbstractPanoramaPublishClient
        {
            string _serverSkydVersion = ChromatogramCache.FORMAT_VERSION_CACHE.ToString();

            public string ServerSkydVersion
            {
                private get { return _serverSkydVersion; }
                set { _serverSkydVersion = value; }
            }

            private JObject CreateFolder(string name, bool write, bool targeted)
            {
                JObject obj = new JObject();
                obj["name"] = name;
                obj["path"] = "/" + name + "/";
                obj["userPermissions"] = write ? 3 : 1;
                if (!write || !targeted)
                {
                    // Create a writable subfolder if this folder is not writable, i.e. it is
                    // not a targetedMS folder or the user does not have write permissions in this folder.
                    // Otherwise, it will not get added to the folder tree (PublishDocumentDlg.AddChildContainers()).
                    obj["children"] = new JArray(CreateFolder("Subfolder", true, true));    
                }
                else
                {
                    obj["children"] = new JArray();    
                }
                
                obj["folderType"] = targeted ? "Targeted MS" : "Collaboration";
                obj["activeModules"] = targeted
                    ? new JArray("MS0", "MS1", "TargetedMS", "MS3")
                    : new JArray("MS0", "MS1", "MS3");
                return obj;
            }

            public override JToken GetInfoForFolders(Server server, string folder)
            {
                JObject testFolders = new JObject();
                // this addition is hacky but necessary as far as I can tell to get PanoramaSavedUri testing to work
                // basically adds a WRITE_TARGET type folder in the root because the new code to deal with publishing to a 
                // saved uri doesn't load a folder tree, but instead the directory structure for a single folder
                testFolders = CreateFolder(WRITE_TARGETED, true, true);
                testFolders["children"] = new JArray(
                    CreateFolder(NO_WRITE_NO_TARGETED, false, false),
                    CreateFolder(NO_WRITE_TARGETED, false, true),
                    CreateFolder(WRITE_TARGETED, true, true),
                    CreateFolder(WRITE_NO_TARGETED, true, false));
                return testFolders;
            }

            public override Uri SendZipFile(Server server, string folderPath, string zipFilePath, IProgressMonitor progressMonitor)
            {
                return null;
            }

            public override JObject SupportedVersionsJson(Server server)
            {
                var obj = new JObject();
                obj["SKYD_version"] = ServerSkydVersion;
                return obj;
            }

        }
    }
}
