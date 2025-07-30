using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.Ardia;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;
using System;
using static pwiz.SkylineTestUtil.ArdiaTestUtil;

namespace pwiz.SkylineTestConnected
{
    [TestClass]
    public class ArdiaRemoteAccountEditTest : AbstractFunctionalTestEx
    {

        [TestMethod]
        public void TestArdiaRemoteAccountEdit()
        {
            if (!EnableArdiaTests)
            {
                Console.Error.WriteLine(
                    "NOTE: skipping Ardia test because username/password for Ardia is not configured in environment variables");
                return;
            }

            TestFilesZip = @"TestConnected\ArdiaRemoteAccountEditTest.zip";

            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            Assert.IsFalse(SkylineWindow.HasRegisteredArdiaAccount);
            Assert.AreEqual(0, Settings.Default.RemoteAccountList.Count);

            // Test setup - configure Ardia account
            var account = GetTestAccount(AccountType.SingleRole);

            RegisterRemoteServer(account);
            Assert.IsTrue(SkylineWindow.HasRegisteredArdiaAccount);
            Assert.AreEqual(1, Settings.Default.RemoteAccountList.Count);

            account = (ArdiaAccount)Settings.Default.RemoteAccountList[0];
            AssertEx.IsTrue(!string.IsNullOrEmpty(account.Token), "Ardia account does not have a token.");

            RemoveRemoteServer();
            Assert.AreEqual(0, Settings.Default.RemoteAccountList.Count, "Server removal was unsuccessful.");

            RegisterRemoteServer(account);

            CopyRemoteServer();
            Assert.AreEqual(2, Settings.Default.RemoteAccountList.Count, "Second server added was not recognized.");

            account = (ArdiaAccount)Settings.Default.RemoteAccountList[1];
            AssertEx.IsTrue(!string.IsNullOrEmpty(account.Token), "Ardia account does not have a token.");

            LogoutServerTestConnection();

            ResetRemoteServers();
            Assert.AreEqual(0, Settings.Default.RemoteAccountList.Count, "Server list reset was unsuccessful.");
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

        private static void RemoveRemoteServer()
        {
            var optionsDlg = ShowDialog<ToolOptionsUI>(() => SkylineWindow.ShowToolOptionsUI(ToolOptionsUI.TABS.Remote));
            var editRemoteAccountListDlg = ShowDialog<EditListDlg<SettingsListBase<RemoteAccount>, RemoteAccount>>(() => optionsDlg.EditRemoteAccounts());
            RunUI(() => editRemoteAccountListDlg.RemoveItem());

            OkDialog(editRemoteAccountListDlg, editRemoteAccountListDlg.OkDialog);
            OkDialog(optionsDlg, optionsDlg.OkDialog);
        }

        private static void CopyRemoteServer()
        {
            var optionsDlg = ShowDialog<ToolOptionsUI>(() => SkylineWindow.ShowToolOptionsUI(ToolOptionsUI.TABS.Remote));
            var editRemoteAccountListDlg = ShowDialog<EditListDlg<SettingsListBase<RemoteAccount>, RemoteAccount>>(() => optionsDlg.EditRemoteAccounts());
            var copyAccountDlg = ShowDialog<EditRemoteAccountDlg>(() => editRemoteAccountListDlg.CopyItem());
            OkDialog(copyAccountDlg, copyAccountDlg.OkDialog);

            RunUI(() => editRemoteAccountListDlg.SelectLastItem());
            var editAccountDlg = ShowDialog<EditRemoteAccountDlg>(() => editRemoteAccountListDlg.EditItem());
            var testSuccessfulDlg = ShowDialog<MessageDlg>(() => editAccountDlg.TestSettings());
            OkDialog(testSuccessfulDlg, testSuccessfulDlg.OkDialog);
            OkDialog(editAccountDlg, editAccountDlg.OkDialog);
            OkDialog(editRemoteAccountListDlg, editRemoteAccountListDlg.OkDialog);
            OkDialog(optionsDlg, optionsDlg.OkDialog);
        }

        private static void LogoutServerTestConnection()
        {
            var optionsDlg = ShowDialog<ToolOptionsUI>(() => SkylineWindow.ShowToolOptionsUI(ToolOptionsUI.TABS.Remote));
            var editRemoteAccountListDlg = ShowDialog<EditListDlg<SettingsListBase<RemoteAccount>, RemoteAccount>>(() => optionsDlg.EditRemoteAccounts());
            var editAccountDlg = ShowDialog<EditRemoteAccountDlg>(() => editRemoteAccountListDlg.EditItem());
            RunUI(() => editAccountDlg.LogoutAccount());
            //Assert.AreEqual("Connect", editAccountDlg.BtnConnectTestStatus());
            OkDialog(editAccountDlg, editAccountDlg.OkDialog);
            OkDialog(editRemoteAccountListDlg, editRemoteAccountListDlg.OkDialog);
            OkDialog(optionsDlg, optionsDlg.OkDialog);
        }

        private static void ResetRemoteServers()
        {
            var optionsDlg = ShowDialog<ToolOptionsUI>(() => SkylineWindow.ShowToolOptionsUI(ToolOptionsUI.TABS.Remote));
            var editRemoteAccountListDlg = ShowDialog<EditListDlg<SettingsListBase<RemoteAccount>, RemoteAccount>>(() => optionsDlg.EditRemoteAccounts());
            RunUI(() => editRemoteAccountListDlg.ResetList());
            OkDialog(editRemoteAccountListDlg, editRemoteAccountListDlg.OkDialog);
            OkDialog(optionsDlg, optionsDlg.OkDialog);
        }
    }
}
