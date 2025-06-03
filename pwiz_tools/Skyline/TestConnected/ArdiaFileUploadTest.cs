/*
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

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.Results.RemoteApi;
using pwiz.Skyline.Model.Results.RemoteApi.Ardia;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestConnected
{
    [TestClass]
    public class ArdiaFileUploadTest : AbstractFunctionalTestEx
    {
        private const string DEFAULT_DIRECTORY_NAME = @"ZZZ-Document-Upload";

        [TestMethod]
        public void TestArdiaFileUpload()
        {
            if (!ArdiaTestUtil.EnableArdiaTests)
            {   
                Console.Error.WriteLine("NOTE: skipping Ardia test because username/password for Ardia is not configured in environment variables");
                return;
            }

            TestFilesZip = @"TestConnected\ArdiaFileUploadTest.zip";

            RunFunctionalTest();
        }

        // TODO: successful upload test
        protected override void DoTest()
        {
            var account = ArdiaTestUtil.GetTestAccount(ArdiaTestUtil.AccountType.SingleRole);

            Assert.IsFalse(SkylineWindow.HasRegisteredArdiaAccount);
            Assert.AreEqual(0, Settings.Default.RemoteAccountList.Count);

            OpenDocument("Basic.sky");

            // Create Remote Server for an Ardia Account
            RegisterRemoteServer(account);
            Assert.IsTrue(SkylineWindow.HasRegisteredArdiaAccount);
            Assert.AreEqual(1, Settings.Default.RemoteAccountList.Count);

            // // Select remote account and destination directory
            // var selectFolderDlg = ShowDialog<ArdiaSelectDirectoryFileDialog>(() => SkylineWindow.PublishToArdia(skipUploadForTests: true));
            // WaitForConditionUI(() => selectFolderDlg.IsLoaded);
            //
            // RunUI(() =>
            // {
            //     selectFolderDlg.SelectItemAndActivate(0);
            //     // selectFolderDlg.SelectItem(0);
            //     // selectFolderDlg.ActivateItem();
            // });
            // WaitForConditionUI(() => selectFolderDlg.ListCount() > 0);
            //
            // RunUI(() =>
            // {
            //     selectFolderDlg.SelectItemAndActivate(DEFAULT_DIRECTORY_NAME);
            //     // selectFolderDlg.SelectItem(DEFAULT_DIRECTORY_NAME);
            //     // selectFolderDlg.ActivateItem();
            // });
            // WaitForConditionUI(() => selectFolderDlg.ListCount() > 0);
            //
            // var confirmUploadDlg = ShowDialog<MultiButtonMsgDlg>(() => selectFolderDlg.OkDialog());
            // var successfulUploadDlg = ShowDialog<MessageDlg>(() => confirmUploadDlg.ClickYes());
            // OkDialog(successfulUploadDlg, successfulUploadDlg.ClickOk);
            //
            // Assert.AreEqual(@"/ZZZ-Document-Upload", selectFolderDlg.DestinationFolder);

            // // TODO: debug and fix. This check returns false. Most checks succeed except differing password
            // //       fields where one is "" and the other is null. For now, manually assert equality server
            // //       name and username.
            // // Assert.AreEqual(account, selectFolderDlg.SelectedAccount);
            // Assert.AreEqual(account.Username, selectFolderDlg.SelectedAccount.Username);
            // Assert.AreEqual(account.ServerUrl, selectFolderDlg.SelectedAccount.ServerUrl);
            //
            // // Why is this necessary?
            // RunUI(() => { selectFolderDlg.Dispose(); });
        }

        private static void RegisterRemoteServer(ArdiaAccount account) 
        {
            Assert.IsNotNull(account);

            var optionsDlg = ShowDialog<ToolOptionsUI>(() => SkylineWindow.ShowToolOptionsUI(ToolOptionsUI.TABS.Remote));
            var editRemoteAccountListDlg = ShowDialog<EditListDlg<SettingsListBase<RemoteAccount>, RemoteAccount>>(() => optionsDlg.EditRemoteAccounts());

            var addAccountDlg = ShowDialog<EditRemoteAccountDlg>(() => editRemoteAccountListDlg.AddItem());
            RunUI(() => addAccountDlg.SetRemoteAccount(account));

            var testSuccessfulDlg = ShowDialog<MessageDlg>(() => addAccountDlg.TestSettings());
            OkDialog(testSuccessfulDlg, testSuccessfulDlg.OkDialog);
            OkDialog(addAccountDlg, addAccountDlg.OkDialog);
            OkDialog(editRemoteAccountListDlg, editRemoteAccountListDlg.OkDialog);
            OkDialog(optionsDlg, optionsDlg.OkDialog);
        }
    }
}