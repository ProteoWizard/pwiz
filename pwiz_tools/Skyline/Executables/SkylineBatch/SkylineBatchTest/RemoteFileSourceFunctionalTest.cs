using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;
using SkylineBatch;
using SkylineBatch.Properties;

namespace SkylineBatchTest
{
    [TestClass]
    public class RemoteFileSourceFunctionalTest : AbstractSkylineBatchFunctionalTest
    {
        public static string TEST_FOLDER;

        public static string BRUDERER_SOURCE_NAME = "Bruderer Panorama Folder";
        public static string BRUDERER_FOLDER_LINK = @"https://panoramaweb.org/_webdav/MacCoss/brendan/Instruction/2021-DIA-PUBS/2015-Bruderer/%40files/";
        public static string SELEVSEK_SOURCE_NAME = "Selevsek Panorama Folder";
        public static string SELEVSEK_FOLDER_LINK = @"https://panoramaweb.org/_webdav/MacCoss/brendan/Instruction/2021-DIA-PUBS/2015-Selevsek/%40files/";


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
            
            TestEditRemoteFileSource(mainForm);

            TestImportRemoteFileSource(mainForm);

            TestReplaceRemoteFileSources(mainForm);
        }

        public void TestAddRemoteFileSource(MainForm mainForm)
        {
            var configForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickAdd());
            AddBrudererSelevsekSources(configForm);
            CloseFormsInOrder(false, configForm);
            configForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickAdd());
            var dataServerForm = ShowDialog<DataServerForm>(() => configForm.dataControl.btnDownload.PerformClick());
            var remoteFileControl = dataServerForm.remoteFileControl;
            CheckRemoteFileSourceList(remoteFileControl, new HashSet<string> { BRUDERER_SOURCE_NAME, SELEVSEK_SOURCE_NAME });
            CloseFormsInOrder(false, dataServerForm, configForm);
            mainForm.ClearRemoteFileSources();
        }

        public void AddBrudererSelevsekSources(SkylineBatchConfigForm configForm)
        {
            var remoteFileForm = ShowDialog<RemoteFileForm>(() => configForm.templateControl.btnDownload.PerformClick());
            var remoteFileControl = remoteFileForm.remoteFileControl;
            var remoteSourceForm = ShowDialog<RemoteSourceForm>(() => remoteFileControl.comboRemoteFileSource.SelectedItem = "<Add...>");
            ChangeRemoteFileSource(remoteSourceForm, BRUDERER_SOURCE_NAME, BRUDERER_FOLDER_LINK);
            CheckRemoteFileSourceList(remoteFileControl, new HashSet<string> { BRUDERER_SOURCE_NAME });
            var editRemoteFileSourcesForm = ShowDialog<EditRemoteFileSourcesForm>(() => remoteFileControl.comboRemoteFileSource.SelectedItem = "<Edit list...>");
            remoteSourceForm = ShowDialog<RemoteSourceForm>(() => editRemoteFileSourcesForm.btnAdd.PerformClick());
            ChangeRemoteFileSource(remoteSourceForm, SELEVSEK_SOURCE_NAME, SELEVSEK_FOLDER_LINK);
            CloseFormsInOrder(true, editRemoteFileSourcesForm);
            CheckRemoteFileSourceList(remoteFileControl, new HashSet<string> { SELEVSEK_SOURCE_NAME, BRUDERER_SOURCE_NAME });
            CloseFormsInOrder(false, remoteFileForm);
        }

        private void ChangeRemoteFileSource(RemoteSourceForm remoteSourceForm, string name, string url, string username = null, string password = null, bool encrypt = true, bool closeForm = true)
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

        private void CheckRemoteFileSource(RemoteSourceForm remoteSourceForm, string name, string url, string username = null, string password = null, bool encrypt = true)
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
                RunUI(() => {
                    if (save)
                        form.AcceptButton.PerformClick();
                    else
                        form.CancelButton.PerformClick();
                });
                WaitForClosedForm(form);
            }
        }


        private void CheckRemoteFileSourceList(RemoteFileControl remoteFileControl, HashSet<string> expectedNames)
        {
            var expectedNumber = expectedNames.Count;
            RunUI(() =>
            {
                Assert.IsTrue(remoteFileControl.comboRemoteFileSource.Items.Count == expectedNumber + 3, $"Remote file source added incorrectly. Expected {expectedNumber} items in list but instead got {remoteFileControl.comboRemoteFileSource.Items.Count - 3}");
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
            var editRemoteFileSourcesForm = ShowDialog<EditRemoteFileSourcesForm>(() => remoteFileControl.comboRemoteFileSource.SelectedItem = "<Edit list...>");
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
            var remoteFileForm = ShowDialog<RemoteFileForm>(() => configForm.templateControl.btnDownload.PerformClick());
            var remoteFileControl = remoteFileForm.remoteFileControl;
            RunUI(() => remoteFileControl.comboRemoteFileSource.SelectedItem = BRUDERER_SOURCE_NAME);
            var remoteSourceForm = ShowDialog<RemoteSourceForm>(() => remoteFileControl.comboRemoteFileSource.SelectedItem = "<Edit current...>");
            CheckRemoteFileSource(remoteSourceForm, BRUDERER_SOURCE_NAME, BRUDERER_FOLDER_LINK);
            var editRemoteFileSourcesForm = ShowDialog<EditRemoteFileSourcesForm>(() => remoteFileControl.comboRemoteFileSource.SelectedItem = "<Edit list...>");
            RunUI(() => editRemoteFileSourcesForm.listSources.SelectedItem = BRUDERER_SOURCE_NAME);
            remoteSourceForm = ShowDialog<RemoteSourceForm>(() => editRemoteFileSourcesForm.btnEdit.PerformClick());
            
            CheckRemoteFileSource(remoteSourceForm, BRUDERER_SOURCE_NAME, BRUDERER_FOLDER_LINK);
            CloseFormsInOrder(false, editRemoteFileSourcesForm);
            editRemoteFileSourcesForm = ShowDialog<EditRemoteFileSourcesForm>(() => remoteFileControl.comboRemoteFileSource.SelectedItem = "<Edit list...>");
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
            var templateRemoteFileForm = ShowDialog<RemoteFileForm>(() => configForm.templateControl.btnDownload.PerformClick());
            var templateRemoteFileControl = templateRemoteFileForm.remoteFileControl;
            CheckRemoteFileSourceList(templateRemoteFileControl, new HashSet<string> {
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
            var dataRemoteFileForm = ShowDialog<DataServerForm>(() => configForm.dataControl.btnDownload.PerformClick());
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
            var templateRemoteFileForm = ShowDialog<RemoteFileForm>(() => configForm.templateControl.btnDownload.PerformClick());
            var templateRemoteFileControl = templateRemoteFileForm.remoteFileControl;
            CheckRemoteFileSourceList(templateRemoteFileControl, new HashSet<string> {
                "panoramaweb.org RawFiles",
                "panoramaweb.org Bruderer.sky.zip",
                });
            RunUI(() => templateRemoteFileControl.comboRemoteFileSource.SelectedItem = "panoramaweb.org Bruderer.sky.zip");
            var remoteSourceForm = ShowDialog<RemoteSourceForm>(() => templateRemoteFileControl.comboRemoteFileSource.SelectedItem = "<Edit current...>");
            ChangeRemoteFileSource(remoteSourceForm, BRUDERER_SOURCE_NAME, BRUDERER_FOLDER_LINK, closeForm: false);
            RunDlg<AlertDlg>(() => remoteSourceForm.btnSave.PerformClick(),
                dlg =>
                {
                    var expectedMessage = Resources.SkylineBatchConfigManagerState_ReplaceRemoteFileSource_Changing_this_file_source_will_impact_the_following_configurations_ +
                              Environment.NewLine + Environment.NewLine +
                              "Bruderer 1" + Environment.NewLine + "Bruderer 2" + Environment.NewLine + Environment.NewLine +
                    Resources.SkylineBatchConfigManagerState_ReplaceRemoteFileSource_Do_you_want_to_continue_;
                     Assert.AreEqual(expectedMessage, dlg.Message);
                    dlg.ClickOk();
                });
            CheckRemoteFileSourceList(templateRemoteFileControl, new HashSet<string> {
                "panoramaweb.org RawFiles",
                BRUDERER_SOURCE_NAME,
                });
            RunUI(() => templateRemoteFileControl.textRelativePath.Text = @"Bruderer.sky.zip");
            CloseFormsInOrder(true, templateRemoteFileForm, configForm);
            RunUI(() => mainForm.ClickConfig(1));
            configForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickEdit());
            templateRemoteFileForm = ShowDialog<RemoteFileForm>(() => configForm.templateControl.btnDownload.PerformClick());
            templateRemoteFileControl = templateRemoteFileForm.remoteFileControl;
            RunUI(() => Assert.AreEqual(BRUDERER_SOURCE_NAME, templateRemoteFileControl.comboRemoteFileSource.SelectedItem));
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
            var dataRemoteFileForm = ShowDialog<DataServerForm>(() => configForm.dataControl.btnDownload.PerformClick());
            var dataRemoteFileControl = dataRemoteFileForm.remoteFileControl;
            CheckRemoteFileSourceList(dataRemoteFileControl, new HashSet<string> { BRUDERER_SOURCE_NAME });
            remoteSourceForm = ShowDialog<RemoteSourceForm>(() => dataRemoteFileControl.comboRemoteFileSource.SelectedItem = "<Edit current...>");
            ChangeRemoteFileSource(remoteSourceForm, BRUDERER_SOURCE_NAME, SELEVSEK_FOLDER_LINK, closeForm: false);
            RunDlg<AlertDlg>(() => remoteSourceForm.btnSave.PerformClick(),
                dlg =>
                {
                    var expectedMessage = Resources.SkylineBatchConfigManagerState_ReplaceRemoteFileSource_Changing_this_file_source_will_impact_the_following_configurations_ +
                              Environment.NewLine + Environment.NewLine +
                              "Bruderer" + Environment.NewLine + "Bruderer (MSstats)" + Environment.NewLine + Environment.NewLine +
                    Resources.SkylineBatchConfigManagerState_ReplaceRemoteFileSource_Do_you_want_to_continue_;
                    Assert.AreEqual(expectedMessage, dlg.Message);
                    dlg.ClickOk();
                });
            RunUI(() => dataRemoteFileControl.textRelativePath.Text = string.Empty);
            CloseFormsInOrder(true, dataRemoteFileForm);
            RunUI(() =>
            {
                configForm.tabsConfig.SelectedIndex = 3;

            });
            var editReportForm = ShowDialog<ReportsAddForm>(() => configForm.ClickEditReport(0));
            var rScriptForm = ShowDialog<RScriptForm>(() => editReportForm.btnEdit.PerformClick());
            var rScriptRemoteFileForm = ShowDialog<RemoteFileForm>(() => rScriptForm.fileControl.btnDownload.PerformClick());
            var reportRemoteFileControl = rScriptRemoteFileForm.remoteFileControl;
            remoteSourceForm = ShowDialog<RemoteSourceForm>(() => reportRemoteFileControl.comboRemoteFileSource.SelectedItem = "<Edit current...>");
            CheckRemoteFileSource(remoteSourceForm, BRUDERER_SOURCE_NAME, SELEVSEK_FOLDER_LINK);
            CloseFormsInOrder(false, rScriptRemoteFileForm, rScriptForm, editReportForm, configForm);

            RunUI(() => mainForm.ClickConfig(0));
            configForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickEdit());
            var annotationsRemoteFileForm = ShowDialog<RemoteFileForm>(() => configForm.annotationsControl.btnDownload.PerformClick());
            var annotationsRemoteFileControl = annotationsRemoteFileForm.remoteFileControl;
            CheckRemoteFileSource(remoteSourceForm, BRUDERER_SOURCE_NAME, SELEVSEK_FOLDER_LINK);
            CloseFormsInOrder(false, annotationsRemoteFileForm, configForm);

            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            mainForm.ClearRemoteFileSources();
        }
    }
}
