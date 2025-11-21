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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData.RemoteApi;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;
using static pwiz.SkylineTestUtil.ArdiaTestUtil;

namespace pwiz.SkylineTestConnected
{
    [TestClass]
    public class ArdiaAccountTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestArdiaCannotRegisterSecondAccount()
        {
            if (!EnableArdiaTests)
            {
                Console.Error.WriteLine("NOTE: skipping Ardia test because username/password for Ardia is not configured in environment variables");
                return;
            }

            TestFilesZip = @"TestConnected\ArdiaFileUploadTest.zip";

            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            OpenDocument("Basic.sky");

            // Register an Ardia account. Expected result: success
            var accountOne = GetTestAccount();

            ArdiaFileUploadTest.RegisterRemoteServer(accountOne);

            Assert.IsTrue(SkylineWindow.HasRegisteredArdiaAccount);
            Assert.AreEqual(1, Settings.Default.RemoteAccountList.Count);

            var registeredArdiaAccount = Settings.Default.RemoteAccountList.First();

            // Attempt to register second Ardia account. Expected result: cannot register second account, Skyline shows correct error message
            var optionsDlg = ShowDialog<ToolOptionsUI>(() => SkylineWindow.ShowToolOptionsUI(ToolOptionsUI.TABS.Remote));
            var editRemoteAccountListDlg = ShowDialog<EditListDlg<SettingsListBase<RemoteAccount>, RemoteAccount>>(() => optionsDlg.EditRemoteAccounts());
            var editRemoteAccountDlg = ShowDialog<EditRemoteAccountDlg>(() => editRemoteAccountListDlg.AddItem());
            var messageDlg = ShowDialog<MessageDlg>(() => editRemoteAccountDlg.AccountType = RemoteAccountType.ARDIA);

            RunUI(() =>
            {
                // Assert dialog updated to UI for configuring Ardia account
                Assert.IsTrue(editRemoteAccountDlg.IsVisibleAccountType(RemoteAccountType.ARDIA));

                // Assert correct error message appears
                Assert.AreEqual(ToolsUIResources.EditRemoteAccountDlg_Ardia_OneAccountSupported, messageDlg.Message);
            });
            // Close error message
            OkDialog(messageDlg, messageDlg.OkDialog);

            // Assert visible dialog is EditRemoteAccountDlg and UI reverted to default (UNIFI)
            RunUI(() =>
            {
                var topOpenForm = FormUtil.FindTopLevelOpenForm();
                Assert.AreEqual(editRemoteAccountDlg, topOpenForm);
                Assert.IsTrue(editRemoteAccountDlg.IsVisibleAccountType(RemoteAccountType.UNIFI));
            });

            // Assert RemoteAccountList remains as expected
            Assert.IsTrue(SkylineWindow.HasRegisteredArdiaAccount);
            Assert.AreEqual(1, Settings.Default.RemoteAccountList.Count);
            Assert.AreEqual(registeredArdiaAccount, Settings.Default.RemoteAccountList.First());

            OkDialog(editRemoteAccountDlg, editRemoteAccountDlg.CancelDialog);
            OkDialog(editRemoteAccountListDlg, editRemoteAccountListDlg.OkDialog);
            OkDialog(optionsDlg, optionsDlg.OkDialog);
        }
    }
}