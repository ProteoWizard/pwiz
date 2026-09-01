/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5) <noreply .at. anthropic.com>
 *
 * Copyright 2025 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.WatersConnect;
using pwiz.CommonFileDialogs;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class WatersConnectMethodExportTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestWatersConnectExportMethodDlg()
        {
            TestFilesZip = @"TestFunctional\WatersConnectMethodExportTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Test setup. Since we do not need to connect to the actual server a dummy account
            // suffices. The request-level mock routes every request by URL and HTTP method;
            // HttpClientTestHelper saves any enclosing behavior and restores it on dispose,
            // scoping the mock to this test.
            Settings.Default.RemoteAccountList.Add(RemoteAccountType.WATERS_CONNECT.GetEmptyAccount());
            _methodsResponse = BuildMethodsResponse();
            using var testBehavior = new HttpClientTestHelper(new HttpClientTestBehavior { RequestResponseFactory = HandleWcRequest });

            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("MixedPolarity02.sky")));
            WaitForDocumentLoaded();

            TestTemplateFileToolTipRestore();

            var exportMethodDlg = ShowDialog<ExportMethodDlg>(() =>
                SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));
            TestTemplateSelection(exportMethodDlg);
            TestMethodExport(exportMethodDlg);

            VerifyBehaviorReplacement();

            _authenticationError = true;
            exportMethodDlg = ShowDialog<ExportMethodDlg>(() =>
                SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));
            WaitForOpenForm<ExportMethodDlg>(1000);
            TestAuthenticationError(exportMethodDlg);
        }

        /// <summary>
        /// Verifies that the template-file tooltip is captured once at load time and
        /// restored verbatim after a temporary override. Guards the refactor that replaced
        /// per-call ComponentResourceManager.GetString("textTemplateFile.ToolTip") lookups
        /// (which tripped a ReSharper inspectcode CLI false positive) with a single captured
        /// field restored via ResetTemplateFileToolTip().
        /// </summary>
        private void TestTemplateFileToolTipRestore()
        {
            var exportMethodDlg = ShowDialog<ExportMethodDlg>(() =>
                SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));

            const string toolTipKey = "textTemplateFile.ToolTip";
            string originalToolTip = null;
            RunUI(() =>
            {
                // At load time the tooltip is the localized value the designer applied. The refactor
                // must capture the genuine localized resource, not a hardcoded or fallback string.
                // This relies on the dialog opening on a non-Waters-Connect instrument (or WC with no
                // saved template), so the tooltip is the designer value and not a URL override. That
                // holds because this runs first, before any test persists a WC instrument/template.
                originalToolTip = exportMethodDlg.TemplateFileToolTip;
                Assert.IsFalse(string.IsNullOrEmpty(originalToolTip), "Expected a non-empty designer tooltip.");

                // The tooltip is genuinely localized: Japanese is translated (ExportMethodDlg.ja.resx)
                // while French has no resx and falls back to invariant English, so the two differ. This
                // makes the capture assertion below non-vacuous - it proves the captured value tracks
                // the running culture rather than just echoing a single same-culture resx lookup.
                var resources = new ComponentResourceManager(typeof(ExportMethodDlg));
                var frenchToolTip = resources.GetString(toolTipKey, new CultureInfo("fr"));
                var japaneseToolTip = resources.GetString(toolTipKey, new CultureInfo("ja"));
                Assert.AreNotEqual(frenchToolTip, japaneseToolTip,
                    "Expected the template-file tooltip to differ between French and Japanese.");

                // The captured live tooltip must equal the resx value for the culture this test runs in.
                Assert.AreEqual(resources.GetString(toolTipKey, CultureInfo.CurrentUICulture), originalToolTip,
                    "Captured tooltip does not match the localized resx value for the current culture.");

                exportMethodDlg.InstrumentType = ExportInstrumentType.WATERS_XEVO_TQ_WATERS_CONNECT;
                exportMethodDlg.MethodType = ExportMethodType.Scheduled;
                exportMethodDlg.ExportStrategy = ExportStrategy.WcDecide;
            });

            // Select a Waters Connect template, which overrides the tooltip with the method URL.
            var templateDlg = ShowDialog<WatersConnectSelectMethodFileDialog>(() => exportMethodDlg.ClickTemplateButton());
            WaitForConditionUI(1000, () => templateDlg.ListViewItems.Count == 1);
            RunUI(() =>
            {
                Assert.AreEqual("Company", templateDlg.ListViewItems[0].Text);
                templateDlg.ListViewItems[0].Selected = true;
                templateDlg.KeyPressHandler(Keys.Enter);
            });
            WaitForConditionUI(1000,
                () => templateDlg.ListViewItems.Count == 1 && templateDlg.ListViewItems[0].Text == @"Skyline");
            RunUI(() =>
            {
                templateDlg.ListViewItems[0].Selected = true;
                templateDlg.KeyPressHandler(Keys.Enter);
            });
            ValidateSkylineFolder(templateDlg);
            RunUI(() => templateDlg.KeyPressHandler(Keys.Enter));

            RunUI(() =>
            {
                Assert.AreEqual("Company/Skyline/Test Method 37", exportMethodDlg.TemplatePathField.Text);
                // Selecting a template overrides the tooltip with the URL string.
                var overriddenToolTip = exportMethodDlg.TemplateFileToolTip;
                Assert.AreNotEqual(originalToolTip, overriddenToolTip,
                    "Selecting a Waters Connect template should override the template-file tooltip.");
                Assert.IsFalse(string.IsNullOrEmpty(overriddenToolTip));

                // Switching to a non-Waters-Connect instrument must restore the captured original.
                // Use MassLynx (local-file export) to avoid the Thermo installation probe dialog.
                exportMethodDlg.InstrumentType = ExportInstrumentType.WATERS_XEVO_TQ_MASS_LYNX;
                Assert.AreEqual(originalToolTip, exportMethodDlg.TemplateFileToolTip,
                    "Template-file tooltip was not restored to its original value after reset.");
            });

            CancelDialog(exportMethodDlg);
        }

        /// <summary>
        /// Exercises the template selection dialog functionality.
        /// </summary>
        private void TestTemplateSelection(ExportMethodDlg exportMethodDlg)
        {
            RunUI(() =>
            {   // Set export parameters
                exportMethodDlg.InstrumentType = ExportInstrumentType.WATERS_XEVO_TQ_WATERS_CONNECT;

                Assert.IsTrue(exportMethodDlg.IsOptimizeTypeEnabled);
                Assert.IsTrue(exportMethodDlg.IsTargetTypeEnabled);

                exportMethodDlg.MethodType = ExportMethodType.Scheduled;
                Assert.AreEqual(ExportMethodDlg.CONCUR_TRANS_TXT, exportMethodDlg.GetMaxLabelText);

                Assert.IsFalse(exportMethodDlg.IsRunLengthVisible);
                Assert.IsFalse(exportMethodDlg.IsDwellTimeVisible);
                Assert.IsTrue(string.IsNullOrEmpty(exportMethodDlg.TemplatePathField.Text));
                exportMethodDlg.ExportStrategy = ExportStrategy.WcDecide;
            });
            // Open template file selection dialog
            var templateDlg = ShowDialog<WatersConnectSelectMethodFileDialog>(() => exportMethodDlg.ClickTemplateButton());
            WaitForConditionUI(1000, () => templateDlg.ListViewItems.Count == 1);
            // Click through folders to Skyline folder
            RunUI(() =>
            {
                Assert.AreEqual("Company", templateDlg.ListViewItems[0].Text);
                templateDlg.ListViewItems[0].Selected = true;
                templateDlg.KeyPressHandler(Keys.Enter);
            });
            WaitForConditionUI(1000,
                () => templateDlg.ListViewItems.Count == 1 && templateDlg.ListViewItems[0].Text == @"Skyline");
            RunUI(() =>
            {
                templateDlg.ListViewItems[0].Selected = true;
                templateDlg.KeyPressHandler(Keys.Enter);
            });
            ValidateSkylineFolder(templateDlg);
            RunUI(() => templateDlg.KeyPressHandler(Keys.Enter));
            RunUI(() =>
            {
                Assert.AreEqual("Company/Skyline/Test Method 37", exportMethodDlg.TemplatePathField.Text);
            });
        }

        /// <summary>
        /// Exercises the dialog for the method file name/path selection.
        /// </summary>
        private void TestMethodExport(ExportMethodDlg exportMethodDlg)
        {
            var warningDlg = ShowDialog<MultiButtonMsgDlg>(() => exportMethodDlg.OkDialog(), 1000);
            var schedulingDataDlg = ShowDialog<SchedulingOptionsDlg>(() => warningDlg.ClickOk(), 1000);
            var methodFileDlg = ShowDialog<WatersConnectSaveMethodFileDialog>(() => schedulingDataDlg.OkDialog());
            ValidateSkylineFolder(methodFileDlg);
            NavigateIntoFolderA(methodFileDlg, 11);
            RunUI(() =>
            {
                Assert.AreEqual(0, methodFileDlg.ListViewItems.Count(item => item.ImageIndex == (int)BaseFileDialogNE.ImageIndex.ReadOnlyFolder));
                Assert.AreEqual(0, methodFileDlg.ListViewItems
                    .Count(item => item.ImageIndex == (int)BaseFileDialogNE.ImageIndex.ReadWriteFolder));
                Assert.AreEqual(11, methodFileDlg.ListViewItems
                    .Count(item => item.ImageIndex == (int)BaseFileDialogNE.ImageIndex.MethodFile));
                methodFileDlg.ListViewItems[0].Selected = true;
            });
            // Check the file overwrite logic
            var fileExistsDialog = ShowDialog<MessageDlg>(() => methodFileDlg.KeyPressHandler(Keys.Enter), 1000);
            Assert.IsNotNull(fileExistsDialog);
            OkDialog(fileExistsDialog, fileExistsDialog.OkDialog);
            // FolderA is writable: exercise creating a new folder here (success + permission-denied).
            // Done after the original-state assertions above so the created folder does not perturb them.
            VerifyNewFolder(methodFileDlg);
            VerifyRefresh(methodFileDlg);
            RunUI(() =>
            {
                methodFileDlg.ListViewItems[0].Selected = false;
                methodFileDlg.SourcePathTextBox.Text = @"TestMethod";
                methodFileDlg.KeyPressHandler(Keys.Enter);
            });
            RunUI(() =>
            {
                var uploadResultDlg = TryWaitForOpenForm<MessageDlg>(2000);
                Assert.IsNotNull(uploadResultDlg);
                Assert.IsTrue(uploadResultDlg.Message.StartsWith(FileUIResources.ExportMethodDlg_OkDialog_WC_Upload_Successful), "Method upload error: \n" + uploadResultDlg.DetailedMessage);
                uploadResultDlg.OkDialog();
            });
            WaitForClosedForm<ExportMethodDlg>();
        }

        /// <summary>
        /// Selects FolderA in the current listing, enters it, and waits for the expected number of
        /// items. On timeout the message reports the actual listing so a count mismatch (e.g. stale
        /// mock data leaking between runs) is diagnosable from the failure alone.
        /// </summary>
        private void NavigateIntoFolderA(WatersConnectSaveMethodFileDialog methodFileDlg, int expectedItemCount)
        {
            RunUI(() =>
            {
                var folderToSelect = methodFileDlg.ListViewItems.FirstOrDefault(item => item.Text == @"FolderA");
                Assert.IsNotNull(folderToSelect, "FolderA not found in the list of folders.");
                folderToSelect.Selected = true;
                methodFileDlg.KeyPressHandler(Keys.Enter);
            });
            WaitForConditionUI(1000, () => methodFileDlg.ListViewItems.Count == expectedItemCount,
                () => string.Format(@"FolderA listing is not populated within allotted time. Expected {0} items, found {1}: {2}",
                    expectedItemCount, methodFileDlg.ListViewItems.Count,
                    string.Join(@"; ", methodFileDlg.ListViewItems.Select(i => i.Text))));
        }

        /// <summary>
        /// Verifies that replacing <see cref="HttpClientWithProgress.TestBehavior"/> mid-process
        /// takes effect for clients created before the swap. This is the request-level-mocking
        /// property whose absence caused the #4603 nightly failure: the handler-level seam this
        /// port removed let a pooled pipeline keep serving the previous test run's mock (with its
        /// stale created-folder state), so FolderA listed 13 items instead of 11 on every run
        /// after the first in a process. The behavior is a static consulted inside the send path
        /// on every request; this guard fails if anyone reintroduces caching in front of it.
        /// </summary>
        private void VerifyBehaviorReplacement()
        {
            const string extraMethodName = "BehaviorSwapMethod";
            // The replacement behavior closes over its own augmented methods listing, so ONLY the
            // new instance can serve the extra method - shared test-instance state cannot leak the
            // extra method through the original behavior, and the swap itself is what is proved.
            var replacementMethods = BuildMethodsResponse(extraMethodName);
            using var replacementBehavior = new HttpClientTestHelper(new HttpClientTestBehavior
            {
                RequestResponseFactory = request => HandleWcRequest(request, replacementMethods)
            });

            var exportMethodDlg = ShowDialog<ExportMethodDlg>(() =>
                SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));
            var warningDlg = ShowDialog<MultiButtonMsgDlg>(() => exportMethodDlg.OkDialog(), 1000);
            var schedulingDataDlg = ShowDialog<SchedulingOptionsDlg>(() => warningDlg.ClickOk(), 1000);
            var methodFileDlg = ShowDialog<WatersConnectSaveMethodFileDialog>(() => schedulingDataDlg.OkDialog());
            // The dialog opens in the template's folder (ExportMethodDlg.OkDialog sets
            // InitialDirectory from the template URL). The replacement behavior serves the same
            // methods listing for every folder, so its extra method must appear - unless stale
            // responses from before the swap are still being served.
            WaitForConditionUI(5000, () => methodFileDlg.ListViewItems.Any(i => i.Text == extraMethodName),
                () => string.Format(@"The replacement behavior's extra method did not appear; responses from before the swap are still being served. Listing ({0} items): {1}",
                    methodFileDlg.ListViewItems.Count,
                    string.Join(@"; ", methodFileDlg.ListViewItems.Select(i => i.Text))));
            CancelDialog(methodFileDlg);
            CancelDialog(exportMethodDlg);
            // Disposing the helper restores the original behavior, so later phases do not see
            // the extra method.
        }

        /// <summary>
        /// Exercises the New Folder feature on the save-method dialog while positioned in a writable
        /// folder: the button is available, a successful create sends the right name and description,
        /// and a Forbidden response surfaces a permission message. Leaves the dialog in its original
        /// state (same folder, success handler restored) so the caller can continue.
        /// </summary>
        private void VerifyNewFolder(WatersConnectSaveMethodFileDialog methodFileDlg)
        {
            const string newFolderName = "NewTestFolder";
            RunUI(() =>
            {
                Assert.IsTrue(methodFileDlg.NewFolderButtonVisible, "New Folder button should be visible on the save dialog.");
                Assert.IsTrue(methodFileDlg.NewFolderButtonEnabled, "New Folder button should be enabled in a writable folder.");
            });

            // Success: the PUT carries the entered name and the generated description.
            _createdFolderName = _createdFolderDescription = null;
            RunUI(() => methodFileDlg.CreateNewFolderForTest(newFolderName));
            var account = (WatersConnectAccount) RemoteUrl.RemoteAccountStorage.GetRemoteAccounts().First();
            RunUI(() =>
            {
                Assert.AreEqual(newFolderName, _createdFolderName);
                Assert.AreEqual(
                    string.Format(FileUIResources.WatersConnectSaveMethodFileDialog_CreateNewFolder_Created_by__0__using_Skyline, account.Username),
                    _createdFolderDescription);
            });

            // The created folder must show up in the refreshed listing so the user can navigate into it.
            WaitForConditionUI(5000, () => methodFileDlg.ListViewItems.Any(i => i.Text == newFolderName),
                () => "The new folder did not appear in the list after creation.");
            RunUI(() => Assert.AreEqual((int) BaseFileDialogNE.ImageIndex.ReadWriteFolder,
                methodFileDlg.ListViewItems.First(i => i.Text == newFolderName).ImageIndex,
                "The new folder should be listed as a writable folder."));

            // Permission error: a Forbidden response surfaces an explanatory message.
            _folderCreateForbidden = true;
            try
            {
                var errorDlg = ShowDialog<MessageDlg>(() => methodFileDlg.CreateNewFolderForTest(newFolderName));
                AssertEx.Contains(errorDlg.Message,
                    string.Format(FileUIResources.WatersConnectSaveMethodFileDialog_CreateNewFolder_You_do_not_have_permission_to_create_the_folder__0__, newFolderName));
                OkDialog(errorDlg, errorDlg.OkDialog);
            }
            finally
            {
                _folderCreateForbidden = false; // restore success behavior for the rest of the test
            }
        }

        /// <summary>
        /// Exercises the Refresh command on the save-method dialog: a folder that appears on the server
        /// after the current listing was cached shows up only once Refresh re-fetches the directory.
        /// Guards the cache invalidation - without it, RefreshFromServer would repopulate from the cached
        /// response and the new folder would never appear. Runs immediately after
        /// <see cref="VerifyNewFolder"/> so it can reuse the folder just created into as the parent.
        /// </summary>
        private void VerifyRefresh(WatersConnectSaveMethodFileDialog methodFileDlg)
        {
            const string serverFolderName = "RefreshedFolder";
            RunUI(() => Assert.IsTrue(methodFileDlg.RefreshButtonVisible, "Refresh button should be visible on the save dialog."));

            // Simulate a folder created on the server outside Skyline after the listing was cached, in the
            // same parent VerifyNewFolder just used. The cached listing must not show it yet.
            RunUI(() =>
            {
                var parentFolderId = _createdFolders.Last().Key;
                _createdFolders.Add(new KeyValuePair<string, string>(parentFolderId, serverFolderName));
                Assert.IsFalse(methodFileDlg.ListViewItems.Any(i => i.Text == serverFolderName),
                    "The server-side folder should not appear in the cached listing before refreshing.");
            });

            // Refresh invalidates the cached listing and re-fetches, so the new folder now appears.
            RunUI(() => methodFileDlg.RefreshForTest());
            WaitForConditionUI(5000, () => methodFileDlg.ListViewItems.Any(i => i.Text == serverFolderName),
                () => "The server-side folder did not appear after refresh.");
        }

        private void TestAuthenticationError(ExportMethodDlg exportMethodDlg)
        {
            Assert.IsTrue(RemoteUrl.RemoteAccountStorage.GetRemoteAccounts().Any());
            Assert.IsNotNull(RemoteUrl.RemoteAccountStorage.GetRemoteAccounts().First() as WatersConnectAccount);
            // Remove the cached token to force authentication server call
            WatersConnectAccount._authenticationTokens.Clear();
            var authErrorDlg = ShowDialog<MessageDlg>(() => exportMethodDlg.ClickTemplateButton());
            Assert.IsTrue(
                authErrorDlg.Message.Contains(FileUIResources.ExportMethodDlg_btnBrowseTemplate_Click_Selected_account_does_not_support_method_development__Please__create_or_select_another_account_),
                "Expected authentication error message not found.");
            var templateDialog = ShowDialog<WatersConnectSelectMethodFileDialog>(authErrorDlg.OkDialog);
            Assert.IsNotNull(templateDialog);
            CancelDialog(templateDialog);
            CancelDialog(exportMethodDlg);
            // Restore normal authentication behavior
            _authenticationError = false;
        }

        private void ValidateSkylineFolder(WatersConnectMethodFileDialog templateDlg)
        {
            WaitForConditionUI(2000, () => templateDlg.ListViewItems.Count == 16, () => "Template selection dialog is not populated within allotted time.");
            RunUI(() =>
            {
                Assert.AreEqual(1, templateDlg.ListViewItems.Count(item => item.ImageIndex == (int)BaseFileDialogNE.ImageIndex.ReadOnlyFolder));
                Assert.AreEqual(4, templateDlg.ListViewItems.Count(item => item.ImageIndex == (int)BaseFileDialogNE.ImageIndex.ReadWriteFolder));
            });
            RunUI(() =>
            {
                Assert.AreEqual(11, templateDlg.ListViewItems.Count(item => item.ImageIndex == (int)BaseFileDialogNE.ImageIndex.MethodFile));
                templateDlg.ListViewItems[12].Selected = true;
            });
        }

        /// <summary>
        /// Validates the method data uploaded to Waters Connect server.
        /// </summary>
        public void ValidateMethodUpload(string jsonPayload)
        {
            var methodModel = JObject.Parse(jsonPayload);
            Assert.AreNotEqual("FailMethod", methodModel["name"]?.ToString().Trim());    // FailMethod is used to test the failed upload
            Assert.AreEqual("TestMethod", methodModel["name"]?.ToString().Trim());
            Assert.AreEqual("Multiple", methodModel["creationMode"]?.ToString().Trim());
            Assert.AreEqual("AcquisitionWindows", methodModel["scheduleType"]?.ToString().Trim());
            Assert.AreEqual(12, methodModel["compounds"]?.Children().Count());
            Assert.IsTrue(methodModel["compounds"] != null && methodModel["compounds"].Children().All(item =>
            {
                if (double.TryParse(item["startTime"]?.Value<string>(), NumberStyles.Any, CultureInfo.InvariantCulture, out var startTime))
                    return startTime >= 0;
                return false;
            }), "Negative start time in the method");
            methodModel["compounds"].Children().ToList().ForEach(compound =>
            {
                var badAdducts = compound["adducts"].Children().ToList().FindAll(
                    adduct => adduct["transitions"].Children().Count(transition => transition["isQuanIon"] != null && transition["isQuanIon"].Value<bool>()) != 1);
                if (badAdducts.Count > 0)
                    Assert.Fail($"Compound {compound["name"]} has adducts with incorrect number of quant ions.");
            });

            ValidateJsonAgainstSchema(jsonPayload, TestFilesDir.GetTestPath("method-dev-spec.json"));
        }

        /// <summary>
        /// Validates a JSON string against a JSON schema file.
        /// Throws an exception if validation fails.
        /// </summary>
        /// <param name="jsonString">The JSON string to validate.</param>
        /// <param name="schemaFilePath">The path to the JSON schema file.</param>
        public static void ValidateJsonAgainstSchema(string jsonString, string schemaFilePath)
        {
            // Load the schema
            var schemaText = File.ReadAllText(schemaFilePath);
#pragma warning disable CS0618 // Type or member is obsolete
            var schema = JsonSchema.Parse(schemaText);

            // Parse the JSON
            var json = JToken.Parse(jsonString);

            // Validate
            if (!json.IsValid(schema, out IList<string> errorMessages))
            {
                throw new InvalidDataException("JSON schema validation failed: " + string.Join("; ", errorMessages));
            }
#pragma warning restore CS0618 // Type or member is obsolete
        }

        // Captures the most recent folder-creation (PUT) request body so tests can assert the payload.
        private string _createdFolderName;
        private string _createdFolderDescription;
        // When set, the mock makes folder creation (PUT) fail with Forbidden. Read per request,
        // so toggling it flips behavior for clients created before the toggle.
        private bool _folderCreateForbidden;
        // When set, the mock fails token requests, simulating an unreachable identity server.
        private bool _authenticationError;
        // Folders created via a successful PUT (parent folder GUID -> new folder name). The folders
        // enumeration response injects these as children so a refreshed list shows the new folder.
        private readonly List<KeyValuePair<string, string>> _createdFolders = new List<KeyValuePair<string, string>>();
        // The methods enumeration response body served by the behavior installed at test start,
        // built by BuildMethodsResponse in DoTest.
        private string _methodsResponse;

        /// <summary>
        /// Builds the methods enumeration response body. When <paramref name="extraMethodName"/> is
        /// set, the listing carries one additional method with that name, so
        /// <see cref="VerifyBehaviorReplacement"/> can tell the replacement behavior's responses
        /// from the original's. Built here rather than per request so a bad fixture fails fast.
        /// </summary>
        private string BuildMethodsResponse(string extraMethodName = null)
        {
            var methodsJson = File.ReadAllText(TestFilesDir.GetTestPath("MockHttpData\\WCMethods.json"));
            if (extraMethodName == null)
                return methodsJson;
            var methods = JArray.Parse(methodsJson);
            var extraMethod = (JObject) methods[0].DeepClone();
            extraMethod["name"] = extraMethodName;
            var readOnlyProperties = (JObject) extraMethod["readOnlyProperties"];
            Assert.IsNotNull(readOnlyProperties, "WCMethods.json fixture is missing readOnlyProperties.");
            readOnlyProperties["methodVersionId"] = "00000000-0000-0000-0000-000000000def";
            methods.Add(extraMethod);
            return methods.ToString();
        }

        /// <summary>
        /// Routes a mocked waters_connect request. Failures are simulated by throwing: an
        /// <see cref="NetworkRequestException"/> reproduces an HTTP error
        /// exactly as production code would see it, and any other exception propagates unmapped.
        /// Unmatched requests fail with 404 so nothing ever reaches the real network.
        /// </summary>
        private Stream HandleWcRequest(HttpRequestMessage request)
        {
            return HandleWcRequest(request, _methodsResponse);
        }

        private Stream HandleWcRequest(HttpRequestMessage request, string methodsResponse)
        {
            var url = request.RequestUri.ToString();
            // ReSharper disable StringIndexOfIsCultureSpecific.1
            if (url.IndexOf(@"/connect/token") >= 0)
            {
                if (_authenticationError)
                    throw new AuthenticationException();
                return StringStream("{\"access_token\":\"qqq\",\"expires_in\":3,\"token_type\":\"Bearer\",\"scope\":\"webapi\"}");
            }
            if (request.Method == HttpMethod.Put && url.IndexOf(WatersConnectAccount.GET_FOLDERS) >= 0)
                return HandleFolderCreate(request);
            if (url.IndexOf(WatersConnectSessionAcquisitionMethod.GET_METHODS_ENDPOINT) >= 0)
                return StringStream(methodsResponse);
            if (url.IndexOf(WatersConnectSessionAcquisitionMethod.UPLOAD_METHOD_ENDPOINT) >= 0)
                return HandleMethodUpload(request);
            if (url.IndexOf(WatersConnectAccount.GET_FOLDERS) >= 0)
                return StringStream(GetFoldersResponse());
            // ReSharper restore StringIndexOfIsCultureSpecific.1
            throw new NetworkRequestException(
                @"Request not matched by the waters_connect mock: " + url,
                HttpStatusCode.NotFound, request.RequestUri, new HttpRequestException(), string.Empty);
        }

        // Folder creation (PUT): Forbidden while the flag is set, otherwise record the payload
        // and remember the folder so the folders enumeration can serve it as a child.
        private Stream HandleFolderCreate(HttpRequestMessage request)
        {
            if (_folderCreateForbidden)
            {
                throw new NetworkRequestException(
                    @"403 Forbidden", HttpStatusCode.Forbidden, request.RequestUri,
                    new HttpRequestException(), "{\"message\" : \"Insufficient permissions\"}");
            }
            var body = JObject.Parse(request.Content.ReadAsStringAsync().Result);
            _createdFolderName = body["Name"]?.ToString();
            _createdFolderDescription = body["Description"]?.ToString();
            var parentId = request.RequestUri.Segments.Last().TrimEnd('/'); // .../folders/{parentGuid}
            _createdFolders.Add(new KeyValuePair<string, string>(parentId, _createdFolderName));
            return StringStream("{\"id\" : \"00000000-0000-0000-0000-000000000abc\"}");
        }

        // Folders enumeration: serve the static hierarchy with any created folders injected
        // as children of their parent, so a refreshed listing reflects a successful create.
        private string GetFoldersResponse()
        {
            var root = JObject.Parse(File.ReadAllText(TestFilesDir.GetTestPath("MockHttpData\\WCFolders.json")));
            foreach (var created in _createdFolders)
            {
                var parent = FindFolderNode(root, created.Key);
                if (!(parent?["children"] is JArray children))
                    continue;
                if (children.Any(c => (string) c["name"] == created.Value))
                    continue;
                children.Add(new JObject
                {
                    ["name"] = created.Value,
                    ["description"] = string.Empty,
                    ["path"] = (string) parent["path"] + "/" + created.Value,
                    ["id"] = "00000000-0000-0000-0000-000000000abc",
                    ["accessType"] = new JObject { ["read"] = true, ["write"] = true },
                    ["children"] = new JArray()
                });
            }
            return root.ToString();
        }

        // Method upload: validate the uploaded payload and echo the method back.
        private Stream HandleMethodUpload(HttpRequestMessage request)
        {
            var format = "{{\"methods\" : [ {{\"id\" : {0}, \"name\" : {1}, \"description\" : {2} }} ]}}";
            var requestContent = request.Content.ReadAsStringAsync().Result;
            ValidateMethodUpload(requestContent);
            var jObject = JObject.Parse(requestContent);
            var id = jObject["templateMethodVersionId"]?.ToString();
            var name = jObject["name"]?.ToString() ?? string.Empty;
            var description = jObject["description"]?.ToString() ?? string.Empty;
            return StringStream(string.Format(format, id, name, description));
        }

        private static Stream StringStream(string content)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(content));
        }

        // Depth-first search of the folder hierarchy for the node with the given id.
        private static JObject FindFolderNode(JObject node, string id)
        {
            if ((string) node["id"] == id)
                return node;
            if (node["children"] is JArray children)
            {
                foreach (var child in children.OfType<JObject>())
                {
                    var found = FindFolderNode(child, id);
                    if (found != null)
                        return found;
                }
            }
            return null;
        }
    }
}
