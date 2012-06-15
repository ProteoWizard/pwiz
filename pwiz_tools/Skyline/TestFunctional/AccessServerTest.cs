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
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class AccessServerTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAccessServer()
        {
            RunFunctionalTest();
        }

        private readonly IPanoramaClient _testClient = new TestPanoramaClient();    // Use null for WebClient
        private const string VALID_USER_NAME = "user";
        private const string VALID_PASSWORD = "password";

        private const string VALID_PANORAMA_SERVER = "128.208.10.133:8070";
        private const string VALID_NON_PANORAMA_SERVER = "www.google.com";
        private const string NON_EXISTENT_SERVER = "www.noexist.edu";
        private const string UNKNOWN_STATE_SERVER = "unkown.server-state.com";

        private ToolOptionsUI ToolOptionsDlg { get; set; }
        private static string Server { get; set; }
        private string Username { get; set; }
        private string Password { get; set; }

        protected override void DoTest()
        {
            ToolOptionsDlg = ShowDialog<ToolOptionsUI>(SkylineWindow.ShowToolOptionsUI);

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

            RunUI(ToolOptionsDlg.OkDialog);
            WaitForClosedForm(ToolOptionsDlg);

        }

        public void CheckServerInfoFailure(int serverCount)
        {
            var editServerListDlg = ShowDialog<EditListDlg<SettingsListBase<Server>, Server>>(ToolOptionsDlg.EditServers);
            var editServerDlg = ShowDialog<EditServerDlg>(editServerListDlg.AddItem);
            RunUI(() =>
            {
                if (_testClient != null)
                    editServerDlg.PanoramaClient = _testClient;
                editServerDlg.Server = new Server(Server, Username, Password);
            });
            RunDlg<MessageDlg>(editServerDlg.OkDialog, messageDlg => messageDlg.OkDialog());
            RunUI(() =>
                      {
                          editServerDlg.CancelButton.PerformClick();
                          editServerListDlg.OkDialog();
                      });
            WaitForClosedForm(editServerDlg);
            WaitForClosedForm(editServerListDlg);
            Assert.AreEqual(serverCount, ToolOptionsDlg.GetServers().Count);
        }

        private void CheckServerInfoSuccess(int serverCount)
        {
            var editServerListDlg = ShowDialog<EditListDlg<SettingsListBase<Server>, Server>>(ToolOptionsDlg.EditServers);
            var editServerDlg = ShowDialog<EditServerDlg>(editServerListDlg.AddItem);
            RunUI(() =>
                      {
                          if (_testClient != null)
                              editServerDlg.PanoramaClient = _testClient;
                          editServerDlg.Server = new Server(Server, Username, Password);
                          editServerDlg.OkDialog();
                          editServerListDlg.OkDialog();
                      });
            WaitForClosedForm(editServerDlg);
            WaitForClosedForm(editServerListDlg);
            Assert.AreEqual(serverCount, ToolOptionsDlg.GetServers().Count);
        }

        class TestPanoramaClient : IPanoramaClient
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

            public bool IsPanorama()
            {
                if (Server.Contains(VALID_PANORAMA_SERVER))
                    return true;
                return false;
            }

            public bool IsValidUser(string username, string password)
            {
                if (string.Equals(username, VALID_USER_NAME) &&
                        string.Equals(password, VALID_PASSWORD))
                {
                    return true;
                }
                return false;
            }
        }
    }
}
