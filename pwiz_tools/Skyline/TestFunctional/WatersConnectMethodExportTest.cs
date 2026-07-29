/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Authentication;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.WatersConnect;
using pwiz.CommonFileDialogs;
using pwiz.Skyline;
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
            // Test setup
            SetRequestHandlers();
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("MixedPolarity02.sky")));
            WaitForDocumentLoaded();

            TestTemplateFileToolTipRestore();

            var exportMethodDlg = ShowDialog<ExportMethodDlg>(() =>
                SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));
            TestTemplateSelection(exportMethodDlg);
            TestMethodExport(exportMethodDlg);

            SetAuthenticationErrorHandler();
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
            RunUI(() =>
            {
                var folderToSelect = methodFileDlg.ListViewItems.FirstOrDefault(item => item.Text == @"FolderA");
                Assert.IsNotNull(folderToSelect, "FolderA not found in the list of folders.");
                folderToSelect.Selected = true;
                methodFileDlg.KeyPressHandler(Keys.Enter);
            });
            WaitForConditionUI(1000, () => methodFileDlg.ListViewItems.Count == 11, () => "Template selection dialog is not populated within allotted time.");
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
            // Restore normal handlers
            SetRequestHandlers();
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

        private void SetRequestHandlers()
        {
            // Since we do not need to connect to the actual server a dummy account suffices
            Settings.Default.RemoteAccountList.Add(RemoteAccountType.WATERS_CONNECT.GetEmptyAccount());

            var wcHandler = new MockHttpMessageHandler();  
            // ReSharper disable StringIndexOfIsCultureSpecific.1
            // Folders enumeration request
            wcHandler.AddMatcher(new RequestMatcherFile(
                req => req.RequestUri.ToString().IndexOf(WatersConnectAccount.GET_FOLDERS) >= 0,
                TestFilesDir.GetTestPath("MockHttpData\\WCFolders.json")));
            // Methods enumeration request
            wcHandler.AddMatcher(new RequestMatcherFile(
                req => req.RequestUri.ToString().IndexOf(WatersConnectSessionAcquisitionMethod.GET_METHODS_ENDPOINT) >= 0,
                TestFilesDir.GetTestPath("MockHttpData\\WCMethods.json")));
            // Method upload request
            wcHandler.AddMatcher(new RequestMatcherFunction(
                req => req.RequestUri.ToString().IndexOf(WatersConnectSessionAcquisitionMethod.UPLOAD_METHOD_ENDPOINT) >= 0,
                req =>
                {
                    var format = "{{\"methods\" : [ {{\"id\" : {0}, \"name\" : {1}, \"description\" : {2} }} ]}}";
                    var requestContent = req.Content.ReadAsStringAsync().Result;
                    ValidateMethodUpload(requestContent);
                    var jObject = JObject.Parse(requestContent);
                    var id = jObject["templateMethodVersionId"]?.ToString();
                    var name = jObject["name"]?.ToString() ?? string.Empty;
                    var description = jObject["description"]?.ToString() ?? string.Empty;
                    return string.Format(format, id, name, description);
                }));
            Program.HttpMessageHandlerFactory.CreateReplaceHandler(WatersConnectAccount.HANDLER_NAME, wcHandler);


            var authHandler = new MockHttpMessageHandler(); 
            authHandler.AddMatcher(new RequestMatcherFunction(req => true,  // req.RequestUri.ToString().IndexOf(@"/connect/token") >=0, 
                req =>
                {
                    Trace.WriteLine(req.Content.ReadAsStringAsync().Result);
                    return "{\"access_token\":\"qqq\",\"expires_in\":3,\"token_type\":\"Bearer\",\"scope\":\"webapi\"}";
                }
            ));
            Program.HttpMessageHandlerFactory.CreateReplaceHandler(WatersConnectAccount.AUTH_HANDLER_NAME, authHandler);
            // ReSharper enable StringIndexOfIsCultureSpecific.1
        }

        private void SetAuthenticationErrorHandler()
        {
            var authHandler = new MockHttpMessageHandler();
            authHandler.AddMatcher(new RequestMatcherException(req => true,  // req.RequestUri.ToString().IndexOf(@"/connect/token") >=0, 
                new AuthenticationException()
            ));
            Program.HttpMessageHandlerFactory.CreateReplaceHandler(WatersConnectAccount.AUTH_HANDLER_NAME, authHandler);
        }
    }
}
