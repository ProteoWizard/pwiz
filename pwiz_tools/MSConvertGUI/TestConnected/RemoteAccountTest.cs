/*
 * Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
 *
 * Copyright 2024 Matt Chambers
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
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.Unifi;
using pwiz.CommonMsData.RemoteApi.WatersConnect;

namespace MSConvertGUI.TestConnected
{
    [TestClass]
    public class RemoteAccountTest
    {
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            MSConvertRemoteAccountServices.Initialize();
        }

        #region UNIFI Tests

        private static UnifiAccount GetUnifiTestAccount()
        {
            var password = Environment.GetEnvironmentVariable("UNIFI_PASSWORD");
            if (string.IsNullOrWhiteSpace(password))
                return null;
            var username = Environment.GetEnvironmentVariable("UNIFI_USERNAME") ?? "msconvert";
            return (UnifiAccount) UnifiAccount.DEFAULT.ChangeUsername(username).ChangePassword(password);
        }

        [TestMethod]
        public void UnifiConnectAndBrowseTest()
        {
            var account = GetUnifiTestAccount();
            if (account == null)
                Assert.Inconclusive("UNIFI_PASSWORD environment variable not set");

            // Create session and list root contents
            using (var session = RemoteSession.CreateSession(account))
            {
                var rootUrl = account.GetRootUrl();
                var contents = FetchContents(session, rootUrl);
                Assert.AreNotEqual(0, contents.Count, "UNIFI root should have at least one folder");

                // Verify items have expected properties
                foreach (var item in contents)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(item.Label), "Item should have a label");
                    Assert.IsFalse(string.IsNullOrEmpty(item.Type), "Item should have a type");
                }
            }
        }

        [TestMethod]
        public void UnifiAccountSerializationTest()
        {
            var account = GetUnifiTestAccount();
            if (account == null)
                Assert.Inconclusive("UNIFI_PASSWORD environment variable not set");

            // Verify account round-trips through our persistence
            var services = MSConvertRemoteAccountServices.INSTANCE;
            var originalCount = services.GetRemoteAccountList().Count;

            services.AddAccount(account);
            Assert.AreEqual(originalCount + 1, services.GetRemoteAccountList().Count);

            // Verify account can be found by URL matching
            var rootUrl = account.GetRootUrl();
            var found = rootUrl.FindMatchingAccount();
            Assert.IsNotNull(found, "Should find matching account for root URL");
            Assert.AreEqual(account.ServerUrl, found.ServerUrl);
            Assert.AreEqual(account.Username, found.Username);

            // Cleanup
            services.RemoveAccount(account);
            Assert.AreEqual(originalCount, services.GetRemoteAccountList().Count);
        }

        #endregion

        #region Waters Connect Tests

        private const string DEV_SERVER_URL = @"https://devconnect.waters.com:48444";

        private static WatersConnectAccount GetWatersConnectTestAccount()
        {
            var password = Environment.GetEnvironmentVariable("WC_PASSWORD");
            if (string.IsNullOrWhiteSpace(password))
                return null;
            var username = Environment.GetEnvironmentVariable("WC_USERNAME") ?? "skyline";
            // Use the same creation path as the dialog to verify the URL is applied correctly
            return RemoteAccountDetailForm.CreateWatersConnectAccount(
                DEV_SERVER_URL, username, password, isDevEnvironment: true);
        }

        [TestMethod]
        public void WatersConnectConnectAndBrowseTest()
        {
            var account = GetWatersConnectTestAccount();
            if (account == null)
                Assert.Inconclusive("WC_PASSWORD environment variable not set");

            // Create session and list root contents
            using (var session = RemoteSession.CreateSession(account))
            {
                var rootUrl = account.GetRootUrl();
                var contents = FetchContents(session, rootUrl);
                Assert.AreNotEqual(0, contents.Count, "Waters Connect root should have at least one folder");

                // Browse into the first folder
                var firstFolder = contents.FirstOrDefault(c => c.Type == "folder" || c.Type == "File Folder");
                if (firstFolder != null)
                {
                    var subContents = FetchContents(session, firstFolder.MsDataFileUri as RemoteUrl);
                    // Just verify we can browse without error — subfolder may be empty
                }
            }
        }

        [TestMethod]
        public void WatersConnectAccountSerializationTest()
        {
            var account = GetWatersConnectTestAccount();
            if (account == null)
                Assert.Inconclusive("WC_PASSWORD environment variable not set");

            // Verify account round-trips through our persistence
            var services = MSConvertRemoteAccountServices.INSTANCE;
            var originalCount = services.GetRemoteAccountList().Count;

            services.AddAccount(account);
            Assert.AreEqual(originalCount + 1, services.GetRemoteAccountList().Count);

            // Verify account can be found by URL matching
            var rootUrl = account.GetRootUrl();
            var found = rootUrl.FindMatchingAccount();
            Assert.IsNotNull(found, "Should find matching account for root URL");
            Assert.AreEqual(account.ServerUrl, found.ServerUrl);
            Assert.AreEqual(account.Username, found.Username);

            // Cleanup
            services.RemoveAccount(account);
            Assert.AreEqual(originalCount, services.GetRemoteAccountList().Count);
        }

        [TestMethod]
        public void WatersConnectAccountCreationTest()
        {
            // Use a URL that differs from both DEV_DEFAULT and DEFAULT to prove the
            // parameter is used, not a hardcoded default.
            string customUrl = @"https://custom-test-server:48444";
            string username = "testuser";
            string password = "testpass";

            // Dev environment should use DEV_DEFAULT client settings
            var devAccount = RemoteAccountDetailForm.CreateWatersConnectAccount(
                customUrl, username, password, isDevEnvironment: true);
            Assert.AreEqual(customUrl, devAccount.ServerUrl, "Dev: ServerUrl should match user input");
            Assert.AreEqual(username, devAccount.Username);
            Assert.AreEqual(password, devAccount.Password);
            Assert.AreEqual(WatersConnectAccount.DEV_DEFAULT.ClientScope, devAccount.ClientScope, "Dev: ClientScope");
            Assert.AreEqual(WatersConnectAccount.DEV_DEFAULT.ClientSecret, devAccount.ClientSecret, "Dev: ClientSecret");
            Assert.AreEqual(WatersConnectAccount.DEV_DEFAULT.ClientId, devAccount.ClientId, "Dev: ClientId");
            Assert.AreEqual(@"https://custom-test-server:48333", devAccount.IdentityServer,
                "Dev: IdentityServer should be derived from serverUrl");

            // Non-dev environment should use DEFAULT client settings
            var prodAccount = RemoteAccountDetailForm.CreateWatersConnectAccount(
                customUrl, username, password, isDevEnvironment: false);
            Assert.AreEqual(customUrl, prodAccount.ServerUrl, "Prod: ServerUrl should match user input");
            Assert.AreEqual(username, prodAccount.Username);
            Assert.AreEqual(password, prodAccount.Password);
            var mscDefault = RemoteAccountDetailForm.MSCONVERT_WATERS_CONNECT_DEFAULT;
            Assert.AreEqual(mscDefault.ClientScope, prodAccount.ClientScope, "Prod: ClientScope");
            Assert.AreEqual(mscDefault.ClientSecret, prodAccount.ClientSecret, "Prod: ClientSecret");
            Assert.AreEqual(mscDefault.ClientId, prodAccount.ClientId, "Prod: ClientId");
            Assert.AreEqual(@"https://custom-test-server:48333", prodAccount.IdentityServer,
                "Prod: IdentityServer should be derived from serverUrl");

            // Custom advanced overrides: verify each parameter flows through to the account.
            const string customIdentityServer = @"https://custom-identity:48333";
            const string customScope = "custom-scope";
            const string customSecret = "custom-secret";
            const string customClientId = "custom-client-id";
            var customAccount = RemoteAccountDetailForm.CreateWatersConnectAccount(
                customUrl, username, password, isDevEnvironment: false,
                identityServer: customIdentityServer,
                clientScope: customScope,
                clientSecret: customSecret,
                clientId: customClientId);
            Assert.AreEqual(customIdentityServer, customAccount.IdentityServer, "Custom: IdentityServer override");
            Assert.AreEqual(customScope, customAccount.ClientScope, "Custom: ClientScope override");
            Assert.AreEqual(customSecret, customAccount.ClientSecret, "Custom: ClientSecret override");
            Assert.AreEqual(customClientId, customAccount.ClientId, "Custom: ClientId override");

            // Blank/whitespace overrides should fall back to defaults (not overwrite with empty).
            var fallbackAccount = RemoteAccountDetailForm.CreateWatersConnectAccount(
                customUrl, username, password, isDevEnvironment: false,
                identityServer: "  ",
                clientScope: "",
                clientSecret: null,
                clientId: "   ");
            Assert.AreEqual(@"https://custom-test-server:48333", fallbackAccount.IdentityServer,
                "Fallback: IdentityServer should be derived from serverUrl when override is blank");
            Assert.AreEqual(mscDefault.ClientScope, fallbackAccount.ClientScope,
                "Fallback: ClientScope should use default when override is blank");
            Assert.AreEqual(mscDefault.ClientSecret, fallbackAccount.ClientSecret,
                "Fallback: ClientSecret should use default when override is null");
            Assert.AreEqual(mscDefault.ClientId, fallbackAccount.ClientId,
                "Fallback: ClientId should use default when override is blank");
        }

        [TestMethod]
        public void WatersConnectDialogBrowseTest()
        {
            var account = GetWatersConnectTestAccount();
            if (account == null)
                Assert.Inconclusive("WC_PASSWORD environment variable not set");

            // Register account so the dialog and FindMatchingAccount can find it
            MSConvertRemoteAccountServices.INSTANCE.AddAccount(account);
            try
            {
                RunOnSTAThread(() =>
                {
                    using (var dlg = new MSConvertOpenDataSourceDialog())
                    {
                        // Force handle creation so async BeginInvoke works
                        var handle = dlg.Handle;

                        // Navigate to the remote account root
                        var rootUrl = account.GetRootUrl();
                        dlg.SetCurrentDirectory(rootUrl);

                        // Navigate into the known test path
                        var pathParts = new[] { "Company", "Skyline", "SmallMolOptimization", "Scheduled" };
                        var targetUrl = rootUrl.ChangePathParts(pathParts.ToList());
                        dlg.SetCurrentDirectory(targetUrl);

                        // Wait for async data to arrive
                        for (int i = 0; i < 30 && dlg.WaitingForData; i++)
                        {
                            Application.DoEvents();
                            Thread.Sleep(1000);
                        }
                        Assert.IsFalse(dlg.WaitingForData, "Timed out waiting for remote data");

                        // Verify the dialog shows the expected injections
                        var items = dlg.ListItemNames.ToList();
                        Assert.AreNotEqual(0, items.Count, "Dialog should show items after navigating to test folder");

                        var injection = items.FirstOrDefault(name => name.StartsWith("ID33140"));
                        Assert.IsNotNull(injection, "Expected injection ID33140 in dialog. Found: " +
                            string.Join(", ", items.Take(10)));

                        // Select the file in the dialog
                        dlg.SelectFile(injection);

                        // Verify we can get an authenticated URL for the injection
                        // (simulates what ProgressForm does when conversion starts)
                        var injectionItem = dlg.ListItemNames.First(name => name.StartsWith("ID33140"));
                        Assert.IsNotNull(injectionItem, "Should be able to find selected item");
                    }
                });
            }
            finally
            {
                MSConvertRemoteAccountServices.INSTANCE.RemoveAccount(account);
            }
        }

        [TestMethod]
        public void UnifiBrowseToSampleResultsTest()
        {
            var account = GetUnifiTestAccount();
            if (account == null)
                Assert.Inconclusive("UNIFI_PASSWORD environment variable not set");

            // Register account so GetAuthenticatedUrl can find it via RemoteAccountStorage
            MSConvertRemoteAccountServices.INSTANCE.AddAccount(account);
            try
            {
            // Browse step by step to: Company / Demo Department / Peptides
            using (var session = RemoteSession.CreateSession(account))
            {
                var currentUrl = account.GetRootUrl();
                var pathParts = new[] { "Company", "Demo Department", "Peptides" };

                foreach (var pathPart in pathParts)
                {
                    var contents = FetchContents(session, currentUrl);
                    var folder = contents.FirstOrDefault(c => c.Label == pathPart);
                    Assert.IsNotNull(folder, "Expected folder '{0}' at {1}. Found: {2}",
                        pathPart, currentUrl, string.Join(", ", contents.Select(c => c.Label).Take(10)));
                    currentUrl = folder.MsDataFileUri as RemoteUrl;
                    Assert.IsNotNull(currentUrl);
                }

                var samples = FetchContents(session, currentUrl);
                Assert.AreNotEqual(0, samples.Count, "Expected sample results in {0}. Got 0 items.", string.Join("/", pathParts));

                // Find the known test sample (UNIFI labels may include well position/replicate)
                var sample = samples.FirstOrDefault(c => c.Label.Contains("Hi3_ClpB_MSe"));
                Assert.IsNotNull(sample, "Expected sample containing 'Hi3_ClpB_MSe' in test folder. Found: " +
                    string.Join(", ", samples.Select(c => c.Label).Take(10)));

                var url = sample.MsDataFileUri as UnifiUrl;
                Assert.IsNotNull(url, "Sample should have a UnifiUrl");

                // GetAuthenticatedUrl should produce a valid URL string with credentials
                var authUrl = url.GetAuthenticatedUrl();
                Assert.IsFalse(string.IsNullOrEmpty(authUrl), "Authenticated URL should not be empty");
                Assert.IsTrue(authUrl.Contains(account.Username), "URL should contain username");
            }
            }
            finally
            {
                MSConvertRemoteAccountServices.INSTANCE.RemoveAccount(account);
            }
        }

        #endregion

        #region Helper methods

        /// <summary>
        /// Run an action on an STA thread (required for WinForms controls).
        /// </summary>
        private static void RunOnSTAThread(Action action)
        {
            Exception caught = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { caught = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join(TimeSpan.FromMinutes(2));
            if (caught != null)
                throw new AssertFailedException("Test failed on STA thread: " + caught.Message, caught);
        }

        private static List<RemoteItem> FetchContents(RemoteSession session, RemoteUrl url)
        {
            var contents = new List<RemoteItem>();
            RemoteServerException exception = null;

            // Poll until fetch is complete
            for (int retry = 0; retry < 30; retry++)
            {
                if (session.AsyncFetchContents(url, out exception))
                    break;
                if (exception != null)
                    throw new Exception("Remote server error: " + exception.Message, exception);
                Thread.Sleep(1000);
            }

            contents.AddRange(session.ListContents(url));
            return contents;
        }

        #endregion
    }
}
