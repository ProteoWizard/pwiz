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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.WatersConnect;
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
            var exportMethodDlg = ShowDialog<ExportMethodDlg>(() =>
                SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));
            TestTemplateSelection(exportMethodDlg);
            TestMethodExport(exportMethodDlg);
        }

        /// <summary>
        /// Exercises the template selection dialog functionality.
        /// </summary>
        private void TestTemplateSelection(ExportMethodDlg exportMethodDlg)
        {
            RunUI(() =>
            {
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
    }
}
