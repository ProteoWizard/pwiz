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
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.Ardia;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;
using static pwiz.Skyline.FileUI.PublishDocumentDlgArdia;

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

        // TODO: verify file successfully uploaded
        protected override void DoTest()
        {
            Assert.IsFalse(SkylineWindow.HasRegisteredArdiaAccount);
            Assert.AreEqual(0, Settings.Default.RemoteAccountList.Count);

            // Test setup - configure Ardia account
            var account = ArdiaTestUtil.GetTestAccount();

            OpenDocument("Basic.sky");

            RegisterRemoteServer(account);
            Assert.IsTrue(SkylineWindow.HasRegisteredArdiaAccount);
            Assert.AreEqual(1, Settings.Default.RemoteAccountList.Count);

            account = (ArdiaAccount)Settings.Default.RemoteAccountList[0];
            AssertEx.IsTrue(!string.IsNullOrEmpty(account.Token));

            // Test scenarios 
            TestValidateFolderName();

            TestAccountHasCredentials(account);

            TestGetFolders(account);

            // TestCreateFolder(account);

            TestSuccessfulUpload(account);
        }

        private static void TestAccountHasCredentials(ArdiaAccount account)
        {
            var ardiaClient = ArdiaClient.Create(account);

            // Verify values needed to authenticate this account are available. These
            // asserts leak implementation details but are useful to avoid chasing 
            // test failures.
            Assert.IsNotNull(ArdiaCredentialHelper.GetApplicationCode(account));
            Assert.IsNotNull(ArdiaCredentialHelper.GetToken(account));

            Assert.IsTrue(ardiaClient.HasCredentials);
        }

        private static void TestValidateFolderName()
        {
            var result = ValidateFolderName(@"New Folder");
            Assert.AreEqual(ValidateInputResult.valid, result);

            result = ValidateFolderName(@"A");
            Assert.AreEqual(ValidateInputResult.valid, result);

            result = ValidateFolderName(@"ABCDEFGHIJKLMNOPQURSTUVWXYZabcedefhijklmnopqurstuvwxyz0123456789 -_");
            Assert.AreEqual(ValidateInputResult.valid, result);

            result = ValidateFolderName(@"-----");
            Assert.AreEqual(ValidateInputResult.valid, result);
            
            result = ValidateFolderName(@"_____");
            Assert.AreEqual(ValidateInputResult.valid, result);

            result = ValidateFolderName(@"    ");
            Assert.AreEqual(ValidateInputResult.invalid_blank, result);

            result = ValidateFolderName(@" ");
            Assert.AreEqual(ValidateInputResult.invalid_blank, result);

            result = ValidateFolderName(@"New Folder <");
            Assert.AreEqual(ValidateInputResult.invalid_character, result);

            result = ValidateFolderName(@"New :: Folder");
            Assert.AreEqual(ValidateInputResult.invalid_character, result);

            result = ValidateFolderName(@"*New? Folder");
            Assert.AreEqual(ValidateInputResult.invalid_character, result);
        }

        private static void TestGetFolders(ArdiaAccount account)
        {
            var ardiaClient = ArdiaClient.Create(account);

            // Successful if no exception thrown
            ardiaClient.GetFolders(account.GetRootArdiaUrl(), null);
        }

        // TODO: enable test. Test passes if the ArdiaAccount has Super Admin role but fails when run as a Tester
        // private static void TestCreateFolder(ArdiaAccount account)
        // {
        //     var ardiaClient = ArdiaClient.Create(account);
        //
        //     ardiaClient.CreateFolder(@"/ZZZ-Document-Upload", @"NewFolder01", null);
        //     ardiaClient.DeleteFolder(@"/ZZZ-Document-Upload/NewFolder01");
        // }

        // TODO: enable upload. Skipping for now because Tester cannot delete files so uploads need to be removed manually
        private static void TestSuccessfulUpload(ArdiaAccount account) 
        {
            var publishDlg = ShowDialog<PublishDocumentDlgArdia>(() => SkylineWindow.PublishToArdia());
            publishDlg.SkipUpload = true;

            WaitForConditionUI(() => publishDlg.IsLoaded);

            RunUI(() => publishDlg.FoldersTree.Nodes[0].Expand());
            WaitForConditionUI(() => !publishDlg.RemoteCallPending );

            RunUI(() => publishDlg.SelectItem(DEFAULT_DIRECTORY_NAME));

            var shareTypeDlg = ShowDialog<ShareTypeDlg>(publishDlg.OkDialog);
            var docUploadedDlg = ShowDialog<MessageDlg>(shareTypeDlg.OkDialog);
            OkDialog(docUploadedDlg, docUploadedDlg.ClickOk);

            Assert.AreEqual(ArdiaClient.URL_PATH_SEPARATOR + DEFAULT_DIRECTORY_NAME, publishDlg.DestinationPath);
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