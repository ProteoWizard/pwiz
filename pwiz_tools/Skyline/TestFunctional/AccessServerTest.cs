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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;
using Newtonsoft.Json.Linq;
using pwiz.PanoramaClient;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Serialization;
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
            using (new FakeKoina(null))
            {
                RunFunctionalTest();
            }
        }

        private const string VALID_USER_NAME = "user@user.edu";
        private const string VALID_PASSWORD = "password";

        private const string VALID_PANORAMA_SERVER = "https://128.208.10.133:8070/";
        private const string VALID_NON_PANORAMA_SERVER = "http://www.google.com";
        private const string NON_EXISTENT_SERVER = "http://www.noexist.edu";
        private const string UNKNOWN_STATE_SERVER = "http://unknown.server-state.com";
        private const string BAD_SERVER_URL = "http://w ww.google.com";
        private const string EMPTY_SERVER_URL = " ";

        private ToolOptionsUI ToolOptionsDlg { get; set; }

        private const string NO_WRITE_NO_TARGETED = "No write permissions/TargetedMS is not active";
        private const string NO_WRITE_TARGETED = "Does not have write permission/TargetedMS active";
        private const string WRITE_TARGETED = "Write permissions/TargetedMS active";
        private const string WRITE_NO_TARGETED = "Write permissions/TargetedMS is not active";

        protected override void DoTest()
        {

            // Cycle through all tabs for the benefit of SkylineTester's "Forms" view, which is used in L10N development
            foreach (var tab in (ToolOptionsUI.TABS[]) Enum.GetValues(typeof(ToolOptionsUI.TABS)))
            {
                var dlg = ShowDialog<ToolOptionsUI>(() => SkylineWindow.ShowToolOptionsUI(tab));
                OkDialog(dlg, dlg.CancelDialog);
            }

            // Now show the tab we really need for this test
            ToolOptionsDlg = ShowDialog<ToolOptionsUI>(() => SkylineWindow.ShowToolOptionsUI(ToolOptionsUI.TABS.Panorama));
            
            // Incorrect password.
            CheckServerInfoFailure(new TestPanoramaClient(VALID_PANORAMA_SERVER, VALID_USER_NAME, "bad password"),0);
            
            // Non-existent username.
            CheckServerInfoFailure(new TestPanoramaClient(VALID_PANORAMA_SERVER, "fake-user@user.edu", "bad password"),0);
            
            // Successful login.
            CheckServerInfoSuccess(new TestPanoramaClient(VALID_PANORAMA_SERVER, VALID_USER_NAME, VALID_PASSWORD), 1);
            
            // Existing non-Panorama server
            CheckServerInfoFailure(new TestPanoramaClient(VALID_NON_PANORAMA_SERVER, VALID_USER_NAME, VALID_PASSWORD), 1);
            
            // We assume that the first component of the path element is the context path where LabKey Server is deployed.
            // Both VALID_PANORAMA_Server and VALID_PANORAMA_SERVER/libkey will be saved as two different servers.
            CheckServerInfoSuccess(new TestPanoramaClient(VALID_PANORAMA_SERVER + "/libkey", VALID_USER_NAME, VALID_PASSWORD), 2);
            
            // Non-existent server
            CheckServerInfoFailure(new TestPanoramaClient(NON_EXISTENT_SERVER, VALID_USER_NAME, VALID_PASSWORD), 2);
            
            // Unknown state server
            CheckServerInfoFailure(new TestPanoramaClient(UNKNOWN_STATE_SERVER, VALID_USER_NAME, VALID_PASSWORD), 2);
            
            // Bad URI Format
            CheckServerInfoFailure(new TestPanoramaClient(BAD_SERVER_URL, VALID_USER_NAME, VALID_PASSWORD), 2);
            
            // No server given
            CheckServerInfoFailure(new TestPanoramaClient(EMPTY_SERVER_URL, VALID_USER_NAME, VALID_PASSWORD), 2);
            
            OkDialog(ToolOptionsDlg, ToolOptionsDlg.OkDialog);
            
            RunUI(() => SkylineWindow.SaveDocument(TestContext.GetTestResultsPath("test.sky")));
            
            var testPanoramaClient = new TestPanoramaClient(VALID_PANORAMA_SERVER, VALID_USER_NAME, VALID_USER_NAME);
            
            CheckPublishFailure(VALID_PANORAMA_SERVER, Resources.PublishDocumentDlg_OkDialog_Please_select_a_folder, testPanoramaClient);
            CheckPublishFailure(NO_WRITE_TARGETED,
                Resources
                    .PublishDocumentDlg_UploadSharedZipFile_You_do_not_have_permission_to_upload_to_the_given_folder, testPanoramaClient);
            CheckPublishFailure(WRITE_NO_TARGETED,
                Resources
                    .PublishDocumentDlg_UploadSharedZipFile_You_do_not_have_permission_to_upload_to_the_given_folder, testPanoramaClient);
            CheckPublishSuccess(WRITE_TARGETED, false, testPanoramaClient);
            
            
            
            // Document has no chromatograms, should be published regardless of skyd version supported by server -- Success
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("no_chromatograms.sky")));
            WaitForDocumentLoaded();
            testPanoramaClient.ServerSkydVersion = "7";
            DocumentFormat? documentFormat = DocumentFormat.VERSION_1_9;
            var savedFileVersion = string.Format(Resources.ShareTypeDlg_ShareTypeDlg_Current_saved_file___0__,
                documentFormat.Value.GetDescription());
            // Document does not have any chromatograms. The current file format as well as all the versions supported
            // for sharing should be listed as options in the ShareTypeDlg.
            var supportedVersions = new List<string> { savedFileVersion };
            supportedVersions.AddRange(SkylineVersion.SupportedForSharing().Select(v => v.ToString()));
            CheckPublishSuccess(WRITE_TARGETED, false, supportedVersions, testPanoramaClient);
            
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("skyd9.sky")));
            WaitForDocumentLoaded();
            // Server supports a version lower than the chromatogram cache in this document -- Fail
            testPanoramaClient.ServerSkydVersion = "7";
            CheckPublishFailure(WRITE_TARGETED, string.Format(Resources.PublishDocumentDlg_ServerSupportsSkydVersion_, "9"), testPanoramaClient);
            Assert.AreEqual(SkylineWindow.Document.Settings.DataSettings.PanoramaPublishUri, null);
            
            // Server supports the version of the chromatogram cache in this document -- Success
            testPanoramaClient.ServerSkydVersion = "9";
            
            // Server supports the document's cache version. Even though this version (skyd 9) is not associated with
            // any of the Skyline versions supported for sharing we should see all of them in the available options since 
            // the cache format of the document does not change when it is shared.
            CheckPublishSuccess(WRITE_TARGETED, false, supportedVersions, testPanoramaClient);
            Assert.AreNotEqual(SkylineWindow.Document.Settings.DataSettings.PanoramaPublishUri, null);
            
            // Server supports a higher version than the document. 
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("skyd9.sky")));
            WaitForDocumentLoaded();
            testPanoramaClient.ServerSkydVersion = "13";
            // The current saved file format and all the Skyline versions supported for sharing should be available as options.
            CheckPublishSuccess(WRITE_TARGETED, false, supportedVersions, testPanoramaClient);
            Assert.AreNotEqual(SkylineWindow.Document.Settings.DataSettings.PanoramaPublishUri, null);
            
            // Document should be published even if an invalid version is returned by the server -- Success 
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("skyd9.sky")));
            WaitForDocumentLoaded();
            testPanoramaClient.ServerSkydVersion = "NINE";
            // If there is an error in getting a response from the server or an error in parsing the version returned by the server
            // PanoramaPublishClient.GetSupportedSkydVersion() returns CacheFormatVersion.CURRENT. So the available options in the 
            // ShareTypeDlg will include the current saved file format and all versions supported for sharing.
            CheckPublishSuccess(WRITE_TARGETED, false, supportedVersions, testPanoramaClient);
            
            // After a document has been published, it is "dirty" because a "panorama_publish_uri" is added.
            // Attempting to publish it again will save the document. Now the selected "Current file version..." option
            // will be the Skyline version that corresponds to the latest document format.
            documentFormat = DocumentFormat.CURRENT;
            savedFileVersion = string.Format(Resources.ShareTypeDlg_ShareTypeDlg_Current_saved_file___0__,
                documentFormat.Value.GetDescription());
            supportedVersions = new List<string> { savedFileVersion };
            supportedVersions.AddRange(SkylineVersion.SupportedForSharing().Select(v => v.ToString()));
            CheckPublishSuccess(WRITE_TARGETED, true, supportedVersions, testPanoramaClient);
            
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("skyd15.sky")));
            WaitForDocumentLoaded();
            testPanoramaClient.ServerSkydVersion = "14";
            // Document's cache version is higher than what the server supports. The available options in ShareTypeDlg should not 
            // include the current saved file format or any Skyline versions associated with a cache version higher than 14.
            supportedVersions = SkylineVersion.SupportedForSharing()
                .Where(ver => ver.CacheFormatVersion <= CacheFormatVersion.Fourteen)
                .Select(v => v.ToString()).ToList();
            CheckPublishSuccess(WRITE_TARGETED, false, supportedVersions, testPanoramaClient);
            
            TestPanoramaServerUrls();
            
            TestAnonymousServers();
        }

        public void CheckPublishSuccess(string nodeSelection, bool expectingSavedUri, TestPanoramaClient panoramaClient)
        {
            CheckPublishSuccess(nodeSelection, expectingSavedUri, null, panoramaClient);
        }

        public void CheckPublishSuccess(string nodeSelection, bool expectingSavedUri, IList<string> supportedVersions, TestPanoramaClient panoramaClient)
        {
            bool hasSavedUri = SkylineWindow.Document.Settings.DataSettings.PanoramaPublishUri != null;
            Assert.AreEqual(expectingSavedUri, hasSavedUri);

            var testPublishClient = new TestPanoramaPublishClient(panoramaClient);
            if (hasSavedUri)
            {
                // Test click don't use saved uri
                MultiButtonMsgDlg publishToSavedUri = ShowDialog<MultiButtonMsgDlg>(() => SkylineWindow.ShowPublishDlg(testPublishClient));

                OkDialog(publishToSavedUri, publishToSavedUri.ClickYes);
                var shareTypeDlg = WaitForOpenForm<ShareTypeDlg>();
                if (supportedVersions != null)
                {
                    VerifySupportedVersions(supportedVersions, shareTypeDlg);
                }
                OkDialog(shareTypeDlg, shareTypeDlg.OkDialog);
            }
            else
            {
                var publishDocumentDlg = ShowDialog<PublishDocumentDlg>(() => SkylineWindow.ShowPublishDlg(testPublishClient));
                WaitForCondition(60 * 1000, () => publishDocumentDlg.IsLoaded);
                RunUI(() =>
                {
                    publishDocumentDlg.SelectItem(nodeSelection);
                    Assert.AreEqual(nodeSelection, publishDocumentDlg.GetSelectedNodeText());
                });
                RunDlg<ShareTypeDlg>(publishDocumentDlg.OkDialog, shareTypeDlg =>
                {
                    if (supportedVersions != null)
                    {
                        VerifySupportedVersions(supportedVersions, shareTypeDlg);
                    }
                    shareTypeDlg.OkDialog();
                });

            }
            var goToSite = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(goToSite, goToSite.ClickNo);
        }

        private static void VerifySupportedVersions(IList<string> supportedVersions, ShareTypeDlg shareTypeDlg)
        {
            var availableItems = shareTypeDlg.GetAvailableVersionItems();
            AssertEx.AreEqualDeep(supportedVersions, availableItems);
        }

        public void CheckPublishFailure(string nodeSelection, string failureMessage, TestPanoramaClient panoramaClient)
        {
            var testPublishClient = new TestPanoramaPublishClient(panoramaClient);

            var publishDocumentDlg = ShowDialog<PublishDocumentDlg>(() => SkylineWindow.ShowPublishDlg(testPublishClient));
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

        public void CheckServerInfoFailure(TestPanoramaClient testClient, int expectedServerCount)
        {
            var editServerListDlg = ShowDialog<EditListDlg<SettingsListBase<Server>, Server>>(ToolOptionsDlg.EditServers);
            var editServerDlg = ShowDialog<EditServerDlg>(editServerListDlg.AddItem);
            RunUI(() =>
            {
                editServerDlg.PanoramaClient = testClient;
                editServerDlg.URL = testClient.Server;
                editServerDlg.Username = testClient.Username;
                editServerDlg.Password = testClient.Password;
            });
            var messageDlg = ShowDialog<MessageDlg>(editServerDlg.OkDialog);
            var errorMsg = messageDlg.Message;
            if (testClient.ServerUri != null)
            {
                Assert.IsTrue(errorMsg.Contains(testClient.Server));
            }
            else if (BAD_SERVER_URL.Equals(testClient.Server))
            {
                Assert.AreEqual(string.Format(Resources.EditServerDlg_OkDialog_The_text__0__is_not_a_valid_server_name_, BAD_SERVER_URL), errorMsg);
            }
            else if (EMPTY_SERVER_URL.Equals(testClient.Server))
            {
                var expectedMsg = string.Format(Resources.MessageBoxHelper_ValidateNameTextBox__0__cannot_be_empty, editServerDlg.GetTextServerUrlControlLabel());
                Assert.AreEqual(expectedMsg, errorMsg);
            }
            OkDialog(messageDlg, messageDlg.OkDialog);
            OkDialog(editServerDlg, editServerDlg.CancelDialog);
            OkDialog(editServerListDlg, editServerListDlg.OkDialog);
            TryWaitForConditionUI(() => expectedServerCount == Settings.Default.ServerList.Count);
            RunUI(() => Assert.AreEqual(expectedServerCount, Settings.Default.ServerList.Count));
        }

        private void CheckServerInfoSuccess(TestPanoramaClient testClient, int expectedServerCount)
        {
            var editServerListDlg = ShowDialog<EditListDlg<SettingsListBase<Server>, Server>>(ToolOptionsDlg.EditServers);
            var editServerDlg = ShowDialog<EditServerDlg>(editServerListDlg.AddItem);
            RunUI(() =>
            {
                editServerDlg.PanoramaClient = testClient;
                editServerDlg.URL = testClient.Server;
                editServerDlg.Username = testClient.Username;
                editServerDlg.Password = testClient.Password;
            });
            OkDialog(editServerDlg, editServerDlg.OkDialog);
            OkDialog(editServerListDlg, editServerListDlg.OkDialog);
            TryWaitForConditionUI(() => expectedServerCount == Settings.Default.ServerList.Count);
            RunUI(() => Assert.AreEqual(expectedServerCount, Settings.Default.ServerList.Count));
        }

        private void TestPanoramaServerUrls()
        {
            var PWEB = "panoramaweb.org";
            var PWEB_FULL = "https://panoramaweb.org/";
            var PWEB_LK = "panoramaweb.org/labkey";
            var PWEB_LK_FULL = "https://panoramaweb.org/labkey/";

            var serverUri = PanoramaUtil.ServerNameToUri(PWEB);
            var pServer = new PanoramaServer(serverUri, string.Empty, string.Empty);
            Assert.AreEqual(pServer.URI.AbsoluteUri, PWEB_FULL);

            var tempServer = pServer.RemoveContextPath(); // pServer does not have a context path. Nothing to remove.
            Assert.IsTrue(ReferenceEquals(pServer, tempServer));
            tempServer = pServer.AddLabKeyContextPath(); // pServer does not have a context path. It should get added to tempServer.
            Assert.IsFalse(ReferenceEquals(pServer, tempServer));
            Assert.AreEqual(tempServer.URI.AbsoluteUri, PWEB_LK_FULL);

            serverUri = PanoramaUtil.ServerNameToUri(PWEB_LK);
            pServer = new PanoramaServer(serverUri, string.Empty, string.Empty);
            Assert.AreEqual(pServer.URI.AbsoluteUri, PWEB_LK_FULL);

            tempServer = pServer.AddLabKeyContextPath(); // pServer has a 'labkey' context path. Nothing to add.
            Assert.IsTrue(ReferenceEquals(pServer, tempServer));
            tempServer = pServer.RemoveContextPath(); // 'labkey' context path should be removed
            Assert.IsFalse(ReferenceEquals(pServer, tempServer));
            Assert.AreEqual(tempServer.URI.AbsoluteUri, PWEB_FULL);

            serverUri = PanoramaUtil.ServerNameToUri(PWEB_LK);
            pServer = new PanoramaServer(serverUri, string.Empty, string.Empty);
            Assert.AreEqual(pServer.URI, PWEB_LK_FULL);
            // Redirect from https://panoramaweb.org/labkey/ -> "https://panoramaweb.org/
            tempServer = pServer.Redirect(PWEB_FULL + PanoramaUtil.ENSURE_LOGIN_PATH, PanoramaUtil.ENSURE_LOGIN_PATH);
            Assert.IsFalse(ReferenceEquals(pServer, tempServer));
            Assert.AreEqual(tempServer.URI, PWEB_FULL);

            // No redirection in the following cases
            Assert.IsTrue(ReferenceEquals(pServer, pServer.Redirect("/labkey/" + PanoramaUtil.ENSURE_LOGIN_PATH, PanoramaUtil.ENSURE_LOGIN_PATH))); // Need full URL
            Assert.IsTrue(ReferenceEquals(pServer, pServer.Redirect("http:/another.server/" + PanoramaUtil.ENSURE_LOGIN_PATH, PanoramaUtil.ENSURE_LOGIN_PATH))); // Not well formed URL.
            Assert.IsTrue(ReferenceEquals(pServer, pServer.Redirect("http://another.server/" + PanoramaUtil.ENSURE_LOGIN_PATH, PanoramaUtil.ENSURE_LOGIN_PATH))); // Not the same host
        }

        private void TestAnonymousServers()
        {
            // Remove all saved Panorama servers
            ToolOptionsDlg = ShowDialog<ToolOptionsUI>(() => SkylineWindow.ShowToolOptionsUI(ToolOptionsUI.TABS.Panorama));
            var editServerListDlg = ShowDialog<EditListDlg<SettingsListBase<Server>, Server>>(ToolOptionsDlg.EditServers);
            RunUI(editServerListDlg.ResetList);
            OkDialog(editServerListDlg, editServerListDlg.OkDialog);
            OkDialog(ToolOptionsDlg, ToolOptionsDlg.OkDialog);

            // Open a Skyline document
            RunUI(() =>
            {
                SkylineWindow.NewDocument(true);
                SkylineWindow.SaveDocument(TestContext.GetTestResultsPath("test_anonymous_servers.sky"));
            });
            WaitForDocumentLoaded();

            // Try to "Upload to Panorama"
            var testPanoramaClient = new TestPanoramaClient(VALID_PANORAMA_SERVER, VALID_USER_NAME, VALID_USER_NAME);
            IPanoramaPublishClient publishClient = new TestPanoramaPublishClient(testPanoramaClient);
            var noServersDlg = ShowDialog<MultiButtonMsgDlg>(() => SkylineWindow.ShowPublishDlg(publishClient));
            var text = noServersDlg.Message;
            RunUI(() => Assert.IsTrue(noServersDlg.Message.Contains(Resources.SkylineWindow_ShowPublishDlg_Press_Register_to_register_for_a_project_on_PanoramaWeb_)));
            OkDialog(noServersDlg, noServersDlg.CancelDialog);


            // Add an anonymous server
            AddAnonymousServer(VALID_PANORAMA_SERVER, 1);


            // Try "Upload to Panorama" again
            noServersDlg = ShowDialog<MultiButtonMsgDlg>(() => SkylineWindow.ShowPublishDlg(publishClient));
            RunUI(() => Assert.IsTrue(noServersDlg.Message.Contains(Resources
                .SkylineWindow_ShowPublishDlg_There_are_no_Panorama_servers_with_a_user_account__To_upload_documents_to_a_server_a_user_account_is_required_)));

            // 1. Click "Edit existing". "Anonymous access" checkbox should be disabled. Add account information
            var editServerDlg = ShowDialog<EditServerDlg>(noServersDlg.ClickYes);
            var testClient = new TestPanoramaClient(VALID_PANORAMA_SERVER, VALID_USER_NAME, VALID_PASSWORD);
            RunUI(() =>
            {
                Assert.IsFalse(editServerDlg.AnonymousSeverCbEnabled());
                Assert.AreEqual(VALID_PANORAMA_SERVER, editServerDlg.URL);
                editServerDlg.PanoramaClient = testClient;
                editServerDlg.Username = testClient.Username;
                editServerDlg.Password = testClient.Password;
            });


            // 2. PublishDocumentDlg should NOT display the "Show anonymous servers" checkbox
            var publishDocDlg = ShowDialog<PublishDocumentDlg>(editServerDlg.OkDialog);
            RunUI( () => Assert.IsFalse(publishDocDlg.CbAnonymousServersVisible));
            OkDialog(publishDocDlg, publishDocDlg.CancelDialog);



            // Add another anonymous server
            // 1. PublishDocumentDlg should display the "Show anonymous servers" checkbox
            // 2. View anonymous servers
            const string pweb = "https://panoramaweb.org/";
            AddAnonymousServer(pweb, 2);
            publishDocDlg = ShowDialog<PublishDocumentDlg>(() => SkylineWindow.ShowPublishDlg(publishClient));
            RunUI(() =>
            {
                // 1. PublishDocumentDlg should display the "Show anonymous servers" checkbox
                Assert.IsTrue(publishDocDlg.CbAnonymousServersVisible);

                var servers = publishDocDlg.GetServers();
                Assert.AreEqual(1, servers.Count);
                Assert.AreEqual(VALID_PANORAMA_SERVER, servers[0]);

                // 2. View anonymous servers
                publishDocDlg.ShowAnonymousServers = true;
                servers = publishDocDlg.GetServers();
                Assert.AreEqual(2, servers.Count);
                Assert.AreEqual(pweb + UtilResources.Server_GetKey___anonymous_, servers[1]);

                publishDocDlg.ShowAnonymousServers = false;
                servers = publishDocDlg.GetServers();
                Assert.AreEqual(1, servers.Count);
                Assert.AreEqual(VALID_PANORAMA_SERVER, servers[0]);
            });
            OkDialog(publishDocDlg, publishDocDlg.CancelDialog);
        }

        private void AddAnonymousServer(string server, int expectedServerCount)
        {
            ToolOptionsDlg = ShowDialog<ToolOptionsUI>(() => SkylineWindow.ShowToolOptionsUI(ToolOptionsUI.TABS.Panorama));
            var editServerListDlg = ShowDialog<EditListDlg<SettingsListBase<Server>, Server>>(ToolOptionsDlg.EditServers);
            var testClient = new TestAnonymousPanoramaClient(server);
            var editServerDlg = ShowDialog<EditServerDlg>(editServerListDlg.AddItem);
            RunUI(() =>
            {
                editServerDlg.PanoramaClient = testClient;
                editServerDlg.URL = testClient.Server;
            });
            var messageDlg = ShowDialog<MessageDlg>(editServerDlg.OkDialog);
            RunUI(() => Assert.AreEqual(string.Format(Resources.MessageBoxHelper_ValidateNameTextBox__0__cannot_be_empty, editServerDlg.GetTextUsernameControlLabel()), messageDlg.Message));

            OkDialog(messageDlg, messageDlg.OkDialog);
            RunUI(() =>
            {
                editServerDlg.AnonymousServer = true;
            });

            OkDialog(editServerDlg, editServerDlg.OkDialog);
            OkDialog(editServerListDlg, editServerListDlg.OkDialog);
            TryWaitForConditionUI(() => expectedServerCount == Settings.Default.ServerList.Count);
            RunUI(() => Assert.AreEqual(expectedServerCount, Settings.Default.ServerList.Count));
            OkDialog(ToolOptionsDlg, ToolOptionsDlg.OkDialog);
        }

        static JObject CreateFolder(string name, bool write, bool targeted)
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

        public class TestPanoramaClient : BaseTestPanoramaClient
        {
            string _serverSkydVersion = ChromatogramCache.FORMAT_VERSION_CACHE.ToString();

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

            public override PanoramaServer ValidateServer()
            {
                if (string.Equals(Server, VALID_NON_PANORAMA_SERVER))
                {
                    throw new PanoramaServerException(new ErrorMessageBuilder(UserStateEnum.nonvalid.Error(ServerUri)).Uri(ServerUri).ErrorDetail("Test WebException").ToString());
                }

                else if (string.Equals(Server, NON_EXISTENT_SERVER))
                    throw new PanoramaServerException(new ErrorMessageBuilder(ServerStateEnum.missing.Error(ServerUri)).Uri(ServerUri).ErrorDetail("Test WebException - NameResolutionFailure").ToString());

                else if (Server.Contains(VALID_PANORAMA_SERVER))
                {
                    if (string.Equals(Username, VALID_USER_NAME) &&
                        string.Equals(Password, VALID_PASSWORD))
                    {
                        return new PanoramaServer(ServerUri, Username, Password);
                    }
                    else
                    {
                        throw new PanoramaServerException(new ErrorMessageBuilder(UserStateEnum.nonvalid.Error(ServerUri))
                            .Uri(PanoramaUtil.GetEnsureLoginUri(new PanoramaServer(ServerUri, Username, Password))).ErrorDetail("Test WebException").ToString());
                    }
                }
                throw new PanoramaServerException(new ErrorMessageBuilder(ServerStateEnum.unknown.Error(ServerUri)).Uri(ServerUri).ErrorDetail("Test WebException - unknown failure").ToString());
            }

            public override void ValidateFolder(string folderPath, FolderPermission? permission, bool checkTargetedMs = true)
            {
            }

            public string ServerSkydVersion
            {
                private get { return _serverSkydVersion; }
                set { _serverSkydVersion = value; }
            }

            public override JToken GetInfoForFolders(string folder)
            {
                // this addition is hacky but necessary as far as I can tell to get PanoramaSavedUri testing to work
                // basically adds a WRITE_TARGET type folder in the root because the new code to deal with publishing to a 
                // saved uri doesn't load a folder tree, but instead the directory structure for a single folder
                var testFolders = CreateFolder(WRITE_TARGETED, true, true);
                testFolders["children"] = new JArray(
                    CreateFolder(NO_WRITE_NO_TARGETED, false, false),
                    CreateFolder(NO_WRITE_TARGETED, false, true),
                    CreateFolder(WRITE_TARGETED, true, true),
                    CreateFolder(WRITE_NO_TARGETED, true, false));
                return testFolders;
            }

            public override JObject SupportedVersionsJson()
            {
                var obj = new JObject();
                obj["SKYD_version"] = ServerSkydVersion;
                return obj;
            }

            public override Uri SendZipFile(string folderPath, string zipFilePath, IProgressMonitor progressMonitor)
            {
                return null;
            }
        }

        public class TestAnonymousPanoramaClient : TestPanoramaClient
        {
            public TestAnonymousPanoramaClient(string server) : base(server, string.Empty, string.Empty)
            {
            }

            public override PanoramaServer ValidateServer()
            {
                return new PanoramaServer(ServerUri, string.Empty, string.Empty);
            }
        }

        public class TestPanoramaPublishClient : AbstractPanoramaPublishClient
        {
            private TestPanoramaClient _panoramaClient;

            public TestPanoramaPublishClient(TestPanoramaClient panoramaClient)
            {
                _panoramaClient = panoramaClient;
            }

            public override IPanoramaClient PanoramaClient => _panoramaClient;
        }
    }
}
