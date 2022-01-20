using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;
using SkylineBatch;

namespace SkylineBatchTest
{
    [TestClass]
    public class RemoteFileSourceFunctionalTest : AbstractSkylineBatchFunctionalTest
    {
        public static string CONFIG_FOLDER;
        public static string TEST_FOLDER;

        public static string BRUDERER_SOURCE_NAME = "Bruderer";
        public static string BRUDERER_FOLDER_LINK = @"https://panoramaweb.org/_webdav/MacCoss/brendan/Instruction/2021-DIA-PUBS/2015-Bruderer/%40files/";
        public static string SELEVSEK_SOURCE_NAME = "Selevsek";
        public static string SELEVSEK_FOLDER_LINK = @"https://panoramaweb.org/_webdav/MacCoss/brendan/Instruction/2021-DIA-PUBS/2015-Selevsek/%40files/";


        [TestMethod]
        public void RemoteFileSourceTest()
        {
            TestFilesZipPaths = new[]
               {@"SkylineBatchTest\RemoteFileSourceFunctionalTest.zip", @"SkylineBatchTest\TestConfigurationFiles.zip"};
               
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TEST_FOLDER = TestFilesDirs[0].FullPath;
            CONFIG_FOLDER = TestFilesDirs[1].FullPath;

            var mainWindow = MainFormWindow();
            var mainForm = mainWindow as MainForm;
            WaitForShownForm(mainForm);
            Assert.IsNotNull(mainForm, "Main program window is not an instance of MainForm.");
            Assert.AreEqual(0, mainForm.ConfigCount());

            TestAddRemoteFileSource(mainForm);
            
            TestEditCurrentRemoteFileSource(mainForm);

            /*TestEditListRemoteFileSource(mainForm);

            TestReplaceRemoteFileSources(mainForm);

            TestImportRemoteFileSource(mainForm);*/
        }

        public void TestAddRemoteFileSource(MainForm mainForm)
        {
            var configForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickAdd());
            AddBrudererSelevsekSources(configForm);
            CloseFormsInOrder(false, configForm);
            configForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickAdd());
            var dataServerForm = ShowDialog<DataServerForm>(() => configForm.dataControl.btnDownload.PerformClick());
            var remoteFileControl = dataServerForm.remoteFileControl;
            CheckRemoteFileSourceList(remoteFileControl, new string[] { BRUDERER_SOURCE_NAME, SELEVSEK_SOURCE_NAME });
            CloseFormsInOrder(false, dataServerForm, configForm);
            mainForm.ClearRemoteFileSources();
        }

        public void AddBrudererSelevsekSources(SkylineBatchConfigForm configForm)
        {
            var remoteFileForm = ShowDialog<RemoteFileForm>(() => configForm.templateControl.btnDownload.PerformClick());
            var remoteFileControl = remoteFileForm.remoteFileControl;
            var remoteSourceForm = ShowDialog<RemoteSourceForm>(() => remoteFileControl.comboRemoteFileSource.SelectedItem = "<Add...>");
            AddRemoteFileSource(remoteSourceForm, BRUDERER_SOURCE_NAME, BRUDERER_FOLDER_LINK);
            CheckRemoteFileSourceList(remoteFileControl, new string[] { BRUDERER_SOURCE_NAME });
            var editRemoteFileSourcesForm = ShowDialog<EditRemoteFileSourcesForm>(() => remoteFileControl.comboRemoteFileSource.SelectedItem = "<Edit list...>");
            remoteSourceForm = ShowDialog<RemoteSourceForm>(() => editRemoteFileSourcesForm.btnAdd.PerformClick());
            AddRemoteFileSource(remoteSourceForm, SELEVSEK_SOURCE_NAME, SELEVSEK_FOLDER_LINK);
            CloseFormsInOrder(true, editRemoteFileSourcesForm);
            CheckRemoteFileSourceList(remoteFileControl, new string[] { BRUDERER_SOURCE_NAME, SELEVSEK_SOURCE_NAME });
            CloseFormsInOrder(false, remoteFileForm);
        }

        private void AddRemoteFileSource(RemoteSourceForm remoteSourceForm, string name, string url, string username = null, string password = null, bool encrypt = true)
        {
            RunUI(() =>
            {
                remoteSourceForm.textName.Text = name;
                remoteSourceForm.textFolderUrl.Text = url;
                remoteSourceForm.textUserName.Text = username ?? string.Empty;
                remoteSourceForm.textPassword.Text = password ?? string.Empty;
                remoteSourceForm.checkBoxNoEncryption.Checked = !encrypt;
            });
            CloseFormsInOrder(true, remoteSourceForm);
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


        private void CheckRemoteFileSourceList(RemoteFileControl remoteFileControl, string[] expectedNames)
        {
            var expectedNumber = expectedNames.Length;
            RunUI(() =>
            {
                Assert.IsTrue(remoteFileControl.comboRemoteFileSource.Items.Count == expectedNumber + 3, $"Remote file source added incorrectly. Expected {expectedNumber} items in list but instead got {remoteFileControl.comboRemoteFileSource.Items.Count - 3}");
                int index = 0;
                foreach (var expectedName in expectedNames)
                {
                    Assert.IsTrue(expectedName.Equals(remoteFileControl.comboRemoteFileSource.Items[index]),
                    $"Expected remote file source {expectedName} at index {index} but instead got {remoteFileControl.comboRemoteFileSource.Items[index]}.");
                    index++;
                }
            });
            var editRemoteFileSourcesForm = ShowDialog<EditRemoteFileSourcesForm>(() => remoteFileControl.comboRemoteFileSource.SelectedItem = "<Edit list...>");
            RunUI(() =>
            {
                Assert.IsTrue(editRemoteFileSourcesForm.listSources.Items.Count == expectedNumber,
                $"Expected {expectedNumber} sources in EditRemoteFileSourcesForm but got {editRemoteFileSourcesForm.listSources.Items.Count}.");
                int index = 0;
                foreach (var expectedName in expectedNames)
                {
                    Assert.IsTrue(expectedName.Equals(editRemoteFileSourcesForm.listSources.Items[index]),
                    $"Expected remote file source {expectedName} at index {index} in EditRemoteFileSourcesForm but instead got {remoteFileControl.comboRemoteFileSource.Items[index]}.");
                    index++;
                }
            });
            CloseFormsInOrder(true, editRemoteFileSourcesForm);
        }

        public void TestEditCurrentRemoteFileSource(MainForm mainForm)
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
            CheckRemoteFileSourceList(remoteFileControl, new string[] { BRUDERER_SOURCE_NAME, "TEST" });
            CloseFormsInOrder(false, remoteFileForm, configForm);
            mainForm.ClearRemoteFileSources();
        }

        public void TestEditListRemoteFileSource(MainForm mainForm)
        {
            throw new NotImplementedException();

        }

        public void TestReplaceRemoteFileSources(MainForm mainForm)
        {
            throw new NotImplementedException();

        }

        public void TestImportRemoteFileSource(MainForm mainForm)
        {
            throw new NotImplementedException();

        }
    }
}
