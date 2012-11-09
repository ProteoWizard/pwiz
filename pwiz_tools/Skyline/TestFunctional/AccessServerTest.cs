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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;
using Newtonsoft.Json.Linq;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class AccessServerTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAccessServer()
        {
            TestFilesZip = ExtensionTestContext.CanImportThermoRaw ? @"https://skyline.gs.washington.edu/tutorials/OptimizeCE.zip"
                : @"https://skyline.gs.washington.edu/tutorials/OptimizeCEMzml.zip";
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

            CheckPublishFailure(VALID_PANORAMA_SERVER);
            CheckPublishFailure(NO_WRITE_TARGETED);
            CheckPublishFailure(WRITE_NO_TARGETED);
            CheckPublishSuccess(WRITE_TARGETED);
        }

        public void CheckPublishSuccess(string nodeSelection)
        {
            RunDlg<PublishDocumentDlg>(() => SkylineWindow.ShowPublishDlg(_testPublishClient),
                    publishDocumentDlg =>
                    {
                        publishDocumentDlg.SelectItem(nodeSelection);
                        publishDocumentDlg.OkDialog();
                    });
        }

        public void CheckPublishFailure(string nodeSelection)
        {
            var publishDocument = ShowDialog<PublishDocumentDlg>(() => SkylineWindow.ShowPublishDlg(_testPublishClient));
            WaitForCondition(60 * 1000, () => publishDocument.IsLoaded);
            RunUI(() => publishDocument.SelectItem(nodeSelection));
            RunDlg<MessageDlg>(publishDocument.OkDialog, messageDlg => messageDlg.OkDialog());
            OkDialog(publishDocument, publishDocument.CancelButton.PerformClick);
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
        }

        private class TestPanoramaPublishClient : IPanoramaPublishClient
        {
            public JToken GetInfoForFolders(Server server)
            {
                JObject child1 = new JObject();
                child1["name"] = NO_WRITE_NO_TARGETED;
                child1["userPermissions"] = 1;
                child1["children"] = new JArray();
                child1["activeModules"] = new JArray("MS0", "MS1", "MS3");

                JObject child2 = new JObject();
                child2["name"] = NO_WRITE_TARGETED;
                child2["userPermissions"] = 1;
                child2["children"] = new JArray();
                child2["activeModules"] = new JArray("MS0", "MS1", "TargetedMS", "MS3");

                JObject child3 = new JObject();
                child3["name"] = WRITE_TARGETED;
                child3["userPermissions"] = 3;
                child3["children"] = new JArray();
                child3["activeModules"] = new JArray("MS0", "MS1", "TargetedMS", "MS3");

                JObject child4 = new JObject();
                child4["name"] = WRITE_NO_TARGETED;
                child4["userPermissions"] = 3;
                child4["children"] = new JArray();
                child4["activeModules"] = new JArray("MS0", "MS1", "MS3");

                JObject testFolders = new JObject();
                testFolders["children"] = new JArray(child1,child2,child3,child4);

                return testFolders;
            }

            public void SendZipFile(Server server, string folderPath, string zipFilePath, ILongWaitBroker longWaitBroker)
            {                
            }
        }
    }
}
