using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using pwiz.Common.Mock;
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
            SetRequestHandlers();
            var dlg = TestTemplateSelection();
            TestMethodExport(dlg);
        }

        private ExportMethodDlg TestTemplateSelection()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("MixedPolarity02.sky")));
            WaitForDocumentLoaded();

            var exportMethodDlg = ShowDialog<ExportMethodDlg>(() =>
                SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));

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
              
            });
            var templateDlg = ShowDialog<WatersConnectSelectMethodFileDialog>(() => exportMethodDlg.ClickTemplateButton());
            RunUI(() =>
            {
                Assert.AreEqual(1, templateDlg.ListViewItems.Count);
                Assert.AreEqual("Company", templateDlg.ListViewItems[0].Text);
                templateDlg.ListViewItems[0].Selected = true;
                templateDlg.KeyPressHandler(Keys.Enter);
            });
            RunUI(() =>
            {
                Assert.AreEqual(1, templateDlg.ListViewItems.Count);
                Assert.AreEqual("Skyline", templateDlg.ListViewItems[0].Text);
                templateDlg.ListViewItems[0].Selected = true;
                templateDlg.KeyPressHandler(Keys.Enter);
            });
            ValidateSkylineFolder(templateDlg);
            RunUI(() => templateDlg.KeyPressHandler(Keys.Enter));
            RunUI(() =>
            {
                Assert.AreEqual("Company/Skyline/Test Method 37", exportMethodDlg.TemplatePathField.Text);
            });
            return exportMethodDlg;
        }

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
            WaitForConditionUI(2000, () => methodFileDlg.ListViewItems.Count == 11, () => "Template selection dialog is not populated within allotted time.");
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
            RunUI(() => fileExistsDialog.OkDialog());
            RunUI(() =>
            {
                methodFileDlg.ListViewItems[0].Selected = false;
                methodFileDlg.SourcePathTextBox.Text = @"TestMethod";
                methodFileDlg.KeyPressHandler(Keys.Enter);
            });
            RunUI(() =>
            {
                var uploadErrorDlg = TryWaitForOpenForm<MessageDlg>(1000);
                Assert.IsNull(uploadErrorDlg, "Method upload error: \n" + uploadErrorDlg?.DetailMessage);
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

        public void ValidateMethodUpload(string jsonPayload)
        {
            var methodModel = JObject.Parse(jsonPayload);
            Assert.AreEqual("TestMethod", methodModel["name"].ToString().Trim());
            Assert.AreEqual(23, methodModel["targets"].Children().Count());
            Assert.IsTrue(methodModel["targets"].Children().All(item =>
            {
                if (double.TryParse(item["startTime"].Value<string>(), out var startTime))
                    return startTime >= 0;
                return false;
            }), "Negative start time in the method");
        }

        private void SetRequestHandlers()
        {
            // Since we do not need to connect to the actual server a dummy account suffices
            Settings.Default.RemoteAccountList.Add(RemoteAccountType.WATERS_CONNECT.GetEmptyAccount());

            var wcHandler = Program.HttpMessageHandlerFactory.CreateReplaceHandler(WatersConnectAccount.HANDLER_NAME);
            // ReSharper disable StringIndexOfIsCultureSpecific.1
            // Folders enumeration request
            wcHandler.AddMatcher(new RequestMatcherFile(
                req => req.RequestUri.ToString().IndexOf(@"/waters_connect/v1.0/folders") >= 0,
                TestFilesDir.GetTestPath("MockHttpData\\WCFolders.json")));
            // Methods enumeration request
            wcHandler.AddMatcher(new RequestMatcherFile(
                req => req.RequestUri.ToString().IndexOf(@"/waters_connect/v2.0/published-methods") >= 0,
                TestFilesDir.GetTestPath("MockHttpData\\WCMethods.json")));
            // Method upload request
            wcHandler.AddMatcher(new RequestMatcherFunction(
                req => req.RequestUri.ToString().IndexOf(@"/waters_connect/v1.0/acq-method-versions") >= 0,
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


            var authHandler = Program.HttpMessageHandlerFactory.CreateReplaceHandler(WatersConnectAccount.AUTH_HANDLER_NAME);
            authHandler.AddMatcher(new RequestMatcherFunction(req => true,  // req.RequestUri.ToString().IndexOf(@"/connect/token") >=0, 
                req =>
                {
                    Trace.WriteLine(req.Content.ReadAsStringAsync().Result);
                    return "{\"access_token\":\"qqq\",\"expires_in\":3,\"token_type\":\"Bearer\",\"scope\":\"webapi\"}";
                }
            ));
            // ReSharper enable StringIndexOfIsCultureSpecific.1
        }
    }
}