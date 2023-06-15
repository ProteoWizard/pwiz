using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using pwiz.PanoramaClient;
using SharedBatch;
using SkylineBatch;
using SkylineBatch.Properties;
using AlertDlg = SharedBatch.AlertDlg;
using PanoramaServer = pwiz.PanoramaClient.PanoramaServer;

namespace SkylineBatchTest
{
    [TestClass]
    public class RemoteFileSourceFunctionalTest : AbstractSkylineBatchFunctionalTest
    {
        public static string TEST_FOLDER;

        public static string BRUDERER_SOURCE_NAME = "Bruderer Panorama Folder";

        public static string BRUDERER_FOLDER_LINK =
            @"https://panoramaweb.org/_webdav/MacCoss/brendan/Instruction/2021-DIA-PUBS/2015-Bruderer/%40files/";

        public static string SELEVSEK_SOURCE_NAME = "Selevsek Panorama Folder";

        public static string SELEVSEK_FOLDER_LINK =
            @"https://panoramaweb.org/_webdav/MacCoss/brendan/Instruction/2021-DIA-PUBS/2015-Selevsek/%40files/";

        public static string TARGETED_SOURCE_NAME = "TargetedMS Panorama Folder";
        public static string TARGETED_FOLDER_LINK = "https://panoramaweb.org/_webdav/TargetedMS_folder/";


        private const string VALID_USER_NAME = "skyline_tester@proteinms.net";
        private const string VALID_PASSWORD = "lclcmsms";
        private const string VALID_SERVER = "https://panoramaweb.org";

        private const string TARGETED_LIBRARY = "TargetedMS folder/Library module property";
        private const string TARGETED = "TargetedMS_folder";
        private const string NO_TARGETED = "Not a TargetedMS folder";
        private const string TARGETED_COLLABORATION = "Collaboration folder/TargetedMS module property";


        [TestMethod]
        public void RemoteFileSourceTest()
        {
            TestFilesZip = @"SkylineBatchTest\RemoteFileSourceFunctionalTest.zip";

            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TEST_FOLDER = TestFilesDirs[0].FullPath;

            var mainWindow = MainFormWindow();
            var mainForm = mainWindow as MainForm;
            WaitForShownForm(mainForm);
            Assert.IsNotNull(mainForm, "Main program window is not an instance of MainForm.");
            Assert.AreEqual(0, mainForm.ConfigCount());

            TestAddRemoteFileSource(mainForm);
            
            TestPanoramaButtonVisibility(mainForm);

            TestEditRemoteFileSource(mainForm);

            TestImportRemoteFileSource(mainForm);

            TestReplaceRemoteFileSources(mainForm);
        }

        public void TestAddRemoteFileSource(MainForm mainForm)
        {
            var configForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickAdd());
            AddBrudererSelevsekSources(configForm);
            AddPanoramaClientSource(configForm);
            CloseFormsInOrder(false, configForm);
            configForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickAdd());
            var dataServerForm = ShowDialog<DataServerForm>(() => configForm.dataControl.btnDownload.PerformClick());
            var remoteFileControl = dataServerForm.remoteFileControl;
            CheckRemoteFileSourceList(remoteFileControl,
                new HashSet<string> { BRUDERER_SOURCE_NAME, SELEVSEK_SOURCE_NAME, TARGETED_SOURCE_NAME });
            CloseFormsInOrder(false, dataServerForm, configForm);
            mainForm.ClearRemoteFileSources();
        }

        public void AddBrudererSelevsekSources(SkylineBatchConfigForm configForm)
        {
            var remoteFileForm =
                ShowDialog<RemoteFileForm>(() => configForm.templateControl.btnDownload.PerformClick());
            var remoteFileControl = remoteFileForm.RemoteFileControl;
            var remoteSourceForm =
                ShowDialog<RemoteSourceForm>(() => remoteFileControl.comboRemoteFileSource.SelectedItem = "<Add...>");
            ChangeRemoteFileSource(remoteSourceForm, BRUDERER_SOURCE_NAME, BRUDERER_FOLDER_LINK);
            CheckRemoteFileSourceList(remoteFileControl, new HashSet<string> { BRUDERER_SOURCE_NAME });
            var editRemoteFileSourcesForm = ShowDialog<EditRemoteFileSourcesForm>(() =>
                remoteFileControl.comboRemoteFileSource.SelectedItem = "<Edit list...>");
            remoteSourceForm = ShowDialog<RemoteSourceForm>(() => editRemoteFileSourcesForm.btnAdd.PerformClick());
            ChangeRemoteFileSource(remoteSourceForm, SELEVSEK_SOURCE_NAME, SELEVSEK_FOLDER_LINK);
            CloseFormsInOrder(true, editRemoteFileSourcesForm);
            CheckRemoteFileSourceList(remoteFileControl,
                new HashSet<string> { SELEVSEK_SOURCE_NAME, BRUDERER_SOURCE_NAME });
            CloseFormsInOrder(false, remoteFileForm);
        }


        private void ChangeRemoteFileSource(RemoteSourceForm remoteSourceForm, string name, string url,
            string username = null, string password = null, bool encrypt = true, bool closeForm = true)
        {
            RunUI(() =>
            {
                remoteSourceForm.textName.Text = name;
                remoteSourceForm.textFolderUrl.Text = url;
                remoteSourceForm.textUserName.Text = username ?? string.Empty;
                remoteSourceForm.textPassword.Text = password ?? string.Empty;
                remoteSourceForm.checkBoxNoEncryption.Checked = !encrypt;
            });
            if (closeForm) CloseFormsInOrder(true, remoteSourceForm);
        }

        public void AddPanoramaClientSource(SkylineBatchConfigForm configForm)
        {
            var remoteFileForm =
                ShowDialog<RemoteFileForm>(() => configForm.templateControl.btnDownload.PerformClick());
            var remoteFileControl = remoteFileForm.RemoteFileControl;
            var remoteSourceForm =
                ShowDialog<RemoteSourceForm>(() => remoteFileControl.comboRemoteFileSource.SelectedItem = "<Add...>");
            ChangeRemoteFileSourceWithPanoramaClient(remoteSourceForm, TARGETED_SOURCE_NAME);
            CloseFormsInOrder(false, remoteFileForm);

        }

        private void ChangeRemoteFileSourceWithPanoramaClient(RemoteSourceForm remoteSourceForm, string name,
            bool closeForm = true)
        {
            RunUI(() =>
            {
                remoteSourceForm.textName.Text = name;
            });
            var testClient = new TestClientJson();
            var folderJson = testClient.GetInfoForFolders(new PanoramaServer(new Uri(VALID_SERVER), VALID_USER_NAME, VALID_PASSWORD),
                TARGETED);
            var remoteDlg = ShowDialog<PanoramaDirectoryPicker>(() => remoteSourceForm.OpenFromPanorama(VALID_SERVER, VALID_USER_NAME, VALID_PASSWORD,folderJson));
            WaitForConditionUI(9000, () => remoteDlg.IsLoaded);
            RunUI(() =>
            {
                remoteDlg.FolderBrowser.SelectNode(VALID_SERVER);
                remoteDlg.FolderBrowser.SelectNode(TARGETED);
                remoteDlg.ClickOpen();
            });
            WaitForClosedForm(remoteDlg);
            RunUI(() =>
            {
                remoteSourceForm.textFolderUrl.Text = remoteDlg.Selected;
                Assert.AreEqual(remoteSourceForm.textFolderUrl.Text, TARGETED_FOLDER_LINK);
            });
            if (closeForm) CloseFormsInOrder(true,remoteSourceForm);
            WaitForClosedForm(remoteSourceForm);

        }

        public void TestPanoramaButtonVisibility(MainForm mainForm)
        {
            var configForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickAdd());
            var remoteFileForm =
                ShowDialog<RemoteFileForm>(() => configForm.templateControl.btnDownload.PerformClick());
            var remoteFileControl = remoteFileForm.RemoteFileControl;

            Assert.IsFalse(remoteFileControl.btnOpenFromPanorama.Visible);

            var remoteSourceForm =
                ShowDialog<RemoteSourceForm>(() => remoteFileControl.comboRemoteFileSource.SelectedItem = "<Add...>");
            ChangeRemoteFileSource(remoteSourceForm, BRUDERER_SOURCE_NAME, BRUDERER_FOLDER_LINK, VALID_USER_NAME, VALID_PASSWORD);

            Assert.IsTrue(remoteFileControl.btnOpenFromPanorama.Visible);
            CloseFormsInOrder(false, remoteFileForm, configForm);
            mainForm.ClearRemoteFileSources();
        }

        private void CheckRemoteFileSource(RemoteSourceForm remoteSourceForm, string name, string url,
            string username = null, string password = null, bool encrypt = true)
        {
            RunUI(() =>
            {
                Assert.AreEqual(name, remoteSourceForm.textName.Text, name);
                Assert.AreEqual(url, remoteSourceForm.textFolderUrl.Text);
                Assert.AreEqual(username ?? string.Empty, remoteSourceForm.textUserName.Text);
                Assert.AreEqual(password ?? string.Empty, remoteSourceForm.textPassword.Text);
                Assert.AreEqual(!encrypt, remoteSourceForm.checkBoxNoEncryption.Checked);
            });
            CloseFormsInOrder(true, remoteSourceForm);
        }

        public void CloseFormsInOrder(bool save, params System.Windows.Forms.Form[] forms)
        {
            foreach (var form in forms)
            {
                RunUI(() =>
                {
                    if (save)
                        form.AcceptButton.PerformClick();
                    else
                        form.CancelButton.PerformClick();
                });
                WaitForClosedForm(form);
            }
        }


        private void  CheckRemoteFileSourceList(RemoteFileControl remoteFileControl, HashSet<string> expectedNames)
        {
            var expectedNumber = expectedNames.Count;
            RunUI(() =>
            {
                Assert.IsTrue(remoteFileControl.comboRemoteFileSource.Items.Count == expectedNumber + 3,
                    $"Remote file source added incorrectly. Expected {expectedNumber} items in list but instead got {remoteFileControl.comboRemoteFileSource.Items.Count - 3}");
                int index = 0;
                foreach (var actualName in remoteFileControl.comboRemoteFileSource.Items)
                {
                    if (index < remoteFileControl.comboRemoteFileSource.Items.Count - 3)
                    {
                        Assert.IsTrue(expectedNames.Contains((string)actualName),
                            $"Unexpected remote file source in list: {actualName}");
                    }

                    index++;
                }
            });
            var editRemoteFileSourcesForm = ShowDialog<EditRemoteFileSourcesForm>(() =>
                remoteFileControl.comboRemoteFileSource.SelectedItem = "<Edit list...>");
            RunUI(() =>
            {
                Assert.IsTrue(editRemoteFileSourcesForm.listSources.Items.Count == expectedNumber,
                    $"Expected {expectedNumber} sources in EditRemoteFileSourcesForm but got {editRemoteFileSourcesForm.listSources.Items.Count}.");
                foreach (var actualName in editRemoteFileSourcesForm.listSources.Items)
                {
                    Assert.IsTrue(expectedNames.Contains((string)actualName),
                        $"Unexpected remote file source in list: {actualName}");
                }
            });
            CloseFormsInOrder(true, editRemoteFileSourcesForm);
        }

        public void TestEditRemoteFileSource(MainForm mainForm)
        {
            var configForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickAdd());
            AddBrudererSelevsekSources(configForm);
            var remoteFileForm =
                ShowDialog<RemoteFileForm>(() => configForm.templateControl.btnDownload.PerformClick());
            var remoteFileControl = remoteFileForm.RemoteFileControl;
            RunUI(() => remoteFileControl.comboRemoteFileSource.SelectedItem = BRUDERER_SOURCE_NAME);
            var remoteSourceForm = ShowDialog<RemoteSourceForm>(() =>
                remoteFileControl.comboRemoteFileSource.SelectedItem = "<Edit current...>");
            CheckRemoteFileSource(remoteSourceForm, BRUDERER_SOURCE_NAME, BRUDERER_FOLDER_LINK);
            var editRemoteFileSourcesForm = ShowDialog<EditRemoteFileSourcesForm>(() =>
                remoteFileControl.comboRemoteFileSource.SelectedItem = "<Edit list...>");
            RunUI(() => editRemoteFileSourcesForm.listSources.SelectedItem = BRUDERER_SOURCE_NAME);
            remoteSourceForm = ShowDialog<RemoteSourceForm>(() => editRemoteFileSourcesForm.btnEdit.PerformClick());

            CheckRemoteFileSource(remoteSourceForm, BRUDERER_SOURCE_NAME, BRUDERER_FOLDER_LINK);
            CloseFormsInOrder(false, editRemoteFileSourcesForm);
            editRemoteFileSourcesForm = ShowDialog<EditRemoteFileSourcesForm>(() =>
                remoteFileControl.comboRemoteFileSource.SelectedItem = "<Edit list...>");
            RunUI(() => editRemoteFileSourcesForm.listSources.SelectedItem = SELEVSEK_SOURCE_NAME);
            remoteSourceForm = ShowDialog<RemoteSourceForm>(() => editRemoteFileSourcesForm.btnEdit.PerformClick());
            RunUI(() => remoteSourceForm.textName.Text = "TEST");
            CheckRemoteFileSource(remoteSourceForm, "TEST", SELEVSEK_FOLDER_LINK);
            CloseFormsInOrder(true, editRemoteFileSourcesForm);
            CheckRemoteFileSourceList(remoteFileControl, new HashSet<string> { BRUDERER_SOURCE_NAME, "TEST" });
            CloseFormsInOrder(false, remoteFileForm, configForm);
            mainForm.ClearRemoteFileSources();
        }

        public void TestImportRemoteFileSource(MainForm mainForm)
        {
            var brudererBcfgSeperateSources = Path.Combine(TEST_FOLDER, "Bruderer_SeperateFileSources.bcfg");
            RunUI(() =>
            {
                mainForm.DoImport(brudererBcfgSeperateSources);
                FunctionalTestUtil.CheckConfigs(2, 0, mainForm);
                mainForm.ClickConfig(0);
            });
            var configForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickEdit());
            var templateRemoteFileForm =
                ShowDialog<RemoteFileForm>(() => configForm.templateControl.btnDownload.PerformClick());
            var templateRemoteFileControl = templateRemoteFileForm.RemoteFileControl;
            CheckRemoteFileSourceList(templateRemoteFileControl, new HashSet<string>
            {
                "panoramaweb.org RawFiles",
                "panoramaweb.org Bruderer.sky.zip",
            });
            CloseFormsInOrder(false, templateRemoteFileForm, configForm);
            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            mainForm.ClearRemoteFileSources();

            var brudererBcfgOneSource = Path.Combine(TEST_FOLDER, "Bruderer_OneFileSource.bcfg");
            RunUI(() =>
            {
                mainForm.DoImport(brudererBcfgOneSource);
                FunctionalTestUtil.CheckConfigs(2, 0, mainForm);
                mainForm.ClickConfig(1);
            });
            configForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickEdit());
            var dataRemoteFileForm =
                ShowDialog<DataServerForm>(() => configForm.dataControl.btnDownload.PerformClick());
            var dataRemoteFileControl = dataRemoteFileForm.remoteFileControl;
            CheckRemoteFileSourceList(dataRemoteFileControl, new HashSet<string> { BRUDERER_SOURCE_NAME });
            CloseFormsInOrder(false, dataRemoteFileForm, configForm);
            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            mainForm.ClearRemoteFileSources();

        }

        public void TestReplaceRemoteFileSources(MainForm mainForm)
        {
            var brudererBcfgSeperateSources = Path.Combine(TEST_FOLDER, "Bruderer_SeperateFileSources.bcfg");
            RunUI(() =>
            {
                mainForm.DoImport(brudererBcfgSeperateSources);
                FunctionalTestUtil.CheckConfigs(2, 0, mainForm);
                mainForm.ClickConfig(0);
            });
            var configForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickEdit());
            var templateRemoteFileForm =
                ShowDialog<RemoteFileForm>(() => configForm.templateControl.btnDownload.PerformClick());
            var templateRemoteFileControl = templateRemoteFileForm.RemoteFileControl;
            CheckRemoteFileSourceList(templateRemoteFileControl, new HashSet<string>
            {
                "panoramaweb.org RawFiles",
                "panoramaweb.org Bruderer.sky.zip",
            });
            RunUI(() => templateRemoteFileControl.comboRemoteFileSource.SelectedItem =
                "panoramaweb.org Bruderer.sky.zip");
            var RemoteSourceForm = ShowDialog<RemoteSourceForm>(() =>
                templateRemoteFileControl.comboRemoteFileSource.SelectedItem = "<Edit current...>");
            ChangeRemoteFileSource(RemoteSourceForm, BRUDERER_SOURCE_NAME, BRUDERER_FOLDER_LINK, closeForm: false);
            RunDlg<AlertDlg>(() => RemoteSourceForm.btnSave.PerformClick(),
                dlg =>
                {
                    var expectedMessage =
                        Resources
                            .SkylineBatchConfigManagerState_ReplaceRemoteFileSource_Changing_this_file_source_will_impact_the_following_configurations_ +
                        Environment.NewLine + Environment.NewLine +
                        "Bruderer 1" + Environment.NewLine + "Bruderer 2" + Environment.NewLine + Environment.NewLine +
                        Resources.SkylineBatchConfigManagerState_ReplaceRemoteFileSource_Do_you_want_to_continue_;
                    Assert.AreEqual(expectedMessage, dlg.Message);
                    dlg.ClickOk();
                });
            CheckRemoteFileSourceList(templateRemoteFileControl, new HashSet<string>
            {
                "panoramaweb.org RawFiles",
                BRUDERER_SOURCE_NAME,
            });
            RunUI(() => templateRemoteFileControl.textRelativePath.Text = @"Bruderer.sky.zip");
            CloseFormsInOrder(true, templateRemoteFileForm, configForm);
            RunUI(() => mainForm.ClickConfig(1));
            configForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickEdit());
            templateRemoteFileForm =
                ShowDialog<RemoteFileForm>(() => configForm.templateControl.btnDownload.PerformClick());
            templateRemoteFileControl = templateRemoteFileForm.RemoteFileControl;
            RunUI(() => Assert.AreEqual(BRUDERER_SOURCE_NAME,
                templateRemoteFileControl.comboRemoteFileSource.SelectedItem));
            CloseFormsInOrder(false, templateRemoteFileForm, configForm);

            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            mainForm.ClearRemoteFileSources();

            var brudererBcfgOneSource = Path.Combine(TEST_FOLDER, "Bruderer_OneFileSource.bcfg");
            RunUI(() =>
            {
                mainForm.DoImport(brudererBcfgOneSource);
                FunctionalTestUtil.CheckConfigs(2, 0, mainForm);
                mainForm.ClickConfig(1);
            });
            configForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickEdit());
            var dataRemoteFileForm =
                ShowDialog<DataServerForm>(() => configForm.dataControl.btnDownload.PerformClick());
            var dataRemoteFileControl = dataRemoteFileForm.remoteFileControl;
            CheckRemoteFileSourceList(dataRemoteFileControl, new HashSet<string> { BRUDERER_SOURCE_NAME });
            RemoteSourceForm = ShowDialog<RemoteSourceForm>(() =>
                dataRemoteFileControl.comboRemoteFileSource.SelectedItem = "<Edit current...>");
            ChangeRemoteFileSource(RemoteSourceForm, BRUDERER_SOURCE_NAME, SELEVSEK_FOLDER_LINK, closeForm: false);
            RunDlg<AlertDlg>(() => RemoteSourceForm.btnSave.PerformClick(),
                dlg =>
                {
                    var expectedMessage =
                        Resources
                            .SkylineBatchConfigManagerState_ReplaceRemoteFileSource_Changing_this_file_source_will_impact_the_following_configurations_ +
                        Environment.NewLine + Environment.NewLine +
                        "Bruderer" + Environment.NewLine + "Bruderer (MSstats)" + Environment.NewLine +
                        Environment.NewLine +
                        Resources.SkylineBatchConfigManagerState_ReplaceRemoteFileSource_Do_you_want_to_continue_;
                    Assert.AreEqual(expectedMessage, dlg.Message);
                    dlg.ClickOk();
                });
            RunUI(() => dataRemoteFileControl.textRelativePath.Text = string.Empty);
            CloseFormsInOrder(true, dataRemoteFileForm);
            RunUI(() => { configForm.tabsConfig.SelectedIndex = 3; });
            var editReportForm = ShowDialog<ReportsAddForm>(() => configForm.ClickEditReport(0));
            var rScriptForm = ShowDialog<RScriptForm>(() => editReportForm.btnEdit.PerformClick());
            var rScriptRemoteFileForm =
                ShowDialog<RemoteFileForm>(() => rScriptForm.fileControl.btnDownload.PerformClick());
            var reportRemoteFileControl = rScriptRemoteFileForm.RemoteFileControl;
            RemoteSourceForm = ShowDialog<RemoteSourceForm>(() =>
                reportRemoteFileControl.comboRemoteFileSource.SelectedItem = "<Edit current...>");
            CheckRemoteFileSource(RemoteSourceForm, BRUDERER_SOURCE_NAME, SELEVSEK_FOLDER_LINK);
            CloseFormsInOrder(false, rScriptRemoteFileForm, rScriptForm, editReportForm, configForm);

            RunUI(() => mainForm.ClickConfig(0));
            configForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickEdit());
            var annotationsRemoteFileForm =
                ShowDialog<RemoteFileForm>(() => configForm.annotationsControl.btnDownload.PerformClick());
            var annotationsRemoteFileControl = annotationsRemoteFileForm.RemoteFileControl;
            CheckRemoteFileSource(RemoteSourceForm, BRUDERER_SOURCE_NAME, SELEVSEK_FOLDER_LINK);
            CloseFormsInOrder(false, annotationsRemoteFileForm, configForm);

            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            mainForm.ClearRemoteFileSources();
        }

        private class TestClientJson
        {

            public JToken CreateFiles()
            {
                var root = new JObject();
                root["rowCount"] = 4;
                root["rows"] = new JArray(CreateObj("File1", ("/" + TARGETED + "/"), false, null, 2, 1, 1, 4), CreateObj("File2", ("/" + TARGETED + "/"), true, 1, 3, 2, 2, 4), CreateObj("File3", ("/" + TARGETED + "/"), true, 2, 4, 3, 3, 4), CreateObj("File4", ("/" + TARGETED + "/"), true, 3, null, 4, 4, 4));
                return root;
            }

            public JToken CreateFile()
            {
                var root = new JObject();
                root["rowCount"] = 1;
                root["rows"] = new JArray(CreateObj("File1", ("/" + TARGETED_LIBRARY + "/"), false, null, null, 1, 1, 1));
                return root;
            }

            public JToken CreateSizeJson()
            {
                var root = new JObject();
                root["rowCount"] = 4;
                root["rows"] = new JArray(CreateSizeObj("1", "3"));
                return root;
            }

            public JToken CreateSizesJson()
            {
                var root = new JObject();
                root["rowCount"] = 4;
                root["rows"] = new JArray(CreateSizeObj("1", "3"), CreateSizeObj("2", "200"), CreateSizeObj("3", "6000"), CreateSizeObj("4", "50"));
                return root;
            }

            public JToken CreateSizeObj(string id, string size)
            {
                var obj = new JObject();
                obj["Id"] = id;
                obj["DocumentSize"] = size;
                return obj;
            }

            public JToken CreateObj(string name, string path, bool replaced, int? replacedBy, int? replaces, int id, int rowId, int versions)
            {
                var obj = new JObject();
                obj["Container/Path"] = path;
                obj["Name"] = name;
                obj["File/Proteins"] = 13;
                obj["File/Peptides"] = 13;
                obj["File/Replicates"] = 13;
                obj["File/Precursors"] = 13;
                obj["File/Transitions"] = 13;
                obj["Replaced"] = replaced;
                obj["ReplacedByRun"] = replacedBy;
                obj["ReplacesRun"] = replaces;
                obj["File/Id"] = id;
                obj["RowId"] = rowId;
                obj["Created"] = "5/11/23";
                obj["File/Versions"] = versions;
                return obj;
            }

            public JObject CreatePrivateFolder(string name)
            {
                JObject obj = new JObject();
                obj["name"] = name;
                obj["path"] = "/" + name + "/";
                obj["userPermissions"] = 0;
                obj["children"] = new JArray();
                obj["folderType"] = "Targeted MS";
                obj["activeModules"] = new JArray("MS0", "MS1", "MS3");

                return obj;
            }

            public JObject CreateFolder(string name, bool subfolder, bool targeted, bool collaboration = false, bool library = false)
            {
                JObject obj = new JObject();
                obj["name"] = name;
                obj["path"] = "/" + name + "/";
                obj["userPermissions"] = subfolder ? 3 : 1;
                if (subfolder || !targeted)
                {
                    // Create a writable subfolder if this folder is not writable, i.e. it is
                    // not a targetedMS folder or the user does not have write permissions in this folder.
                    // Otherwise, it will not get added to the folder tree (PublishDocumentDlg.AddChildContainers()).
                    obj["children"] = new JArray(CreateFolder("Subfolder", false, true));
                }
                else
                {
                    obj["children"] = new JArray();
                }

                if (library)
                {
                    JObject objChild = new JObject();
                    objChild["effectiveValue"] = "Library";
                    obj["moduleProperties"] = new JArray(objChild);
                }
                obj["folderType"] = collaboration ? "Collaboration" : "Targeted MS";

                obj["activeModules"] = targeted
                    ? new JArray("MS0", "MS1", "TargetedMS", "MS3")
                    : new JArray("MS0", "MS1", "MS3");

                return obj;
            }

            //Only generating 3 nodes in the tree
            public JToken GetInfoForFolders(pwiz.PanoramaClient.PanoramaServer server, string folder)
            {
                JObject testFolders = new JObject();
                testFolders = CreateFolder(TARGETED, true, true);
                testFolders["children"] = new JArray(
                    CreateFolder(TARGETED_LIBRARY, true, true, false, true),
                    CreateFolder(TARGETED, true, true),
                    CreateFolder(NO_TARGETED, true, false),
                    CreateFolder(TARGETED_COLLABORATION, true, true, true));
                return testFolders;
            }
        }
    }
}
