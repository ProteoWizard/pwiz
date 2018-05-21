/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Results.RemoteApi;
using pwiz.Skyline.Model.Results.RemoteApi.Chorus;
using pwiz.Skyline.Model.Results.RemoteApi.Unifi;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.ToolsUI
{
    public partial class EditRemoteAccountDlg : FormEx
    {
        private readonly RemoteAccount _originalAccount;
        private readonly IList<RemoteAccount> _existing;
        public EditRemoteAccountDlg(RemoteAccount remoteAccount, IEnumerable<RemoteAccount> existing)
        {
            InitializeComponent();
            _existing = ImmutableList.ValueOf(existing);
            _originalAccount = remoteAccount;
            comboAccountType.Items.AddRange(RemoteAccountType.ALL.ToArray());
            SetRemoteAccount(UnifiAccount.DEFAULT);
            SetRemoteAccount(remoteAccount);
        }

        public void SetRemoteAccount(RemoteAccount remoteAccount)
        {
            comboAccountType.SelectedIndex = RemoteAccountType.ALL.IndexOf(remoteAccount.AccountType);
            textUsername.Text = remoteAccount.Username;
            textPassword.Text = remoteAccount.Password;
            textServerURL.Text = remoteAccount.ServerUrl;
            var unifiAccount = remoteAccount as UnifiAccount;
            if (unifiAccount != null)
            {
                tbxIdentityServer.Text = unifiAccount.IdentityServer;
                tbxClientScope.Text = unifiAccount.ClientScope;
                tbxClientSecret.Text = unifiAccount.ClientSecret;
            }
        }

        public RemoteAccount GetRemoteAccount()
        {
            var accountType = (RemoteAccountType) comboAccountType.SelectedItem;
            var remoteAccount = accountType.GetEmptyAccount().ChangeServerUrl(textServerURL.Text.Trim())
                .ChangeUsername(textUsername.Text.Trim()).ChangePassword(textPassword.Text);
            if (accountType == RemoteAccountType.UNIFI)
            {
                var unifiAccount = (UnifiAccount) remoteAccount;
                unifiAccount = unifiAccount.ChangeIdentityServer(tbxIdentityServer.Text)
                    .ChangeClientScope(tbxClientScope.Text)
                    .ChangeClientSecret(tbxClientSecret.Text);
                remoteAccount = unifiAccount;
            }
            return remoteAccount;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            if (!ValidateValues())
            {
                return;
            }
            DialogResult = DialogResult.OK;
        }

        private void btnTest_Click(object sender, EventArgs e)
        {
            TestSettings();
        }

        public bool TestSettings()
        {
            if (!ValidateValues())
            {
                return false;
            }
            var account = GetRemoteAccount();
            var chorusAccount = account as ChorusAccount;
            if (chorusAccount != null)
            {
                return TestChorusAccount(chorusAccount);
            }
            var unifiAccount = account as UnifiAccount;
            if (unifiAccount != null)
            {
                return TestUnifiAccount(unifiAccount);
            }
            return true;
        }

        private bool TestChorusAccount(ChorusAccount chorusAccount)
        {
            using (ChorusSession chorusSession = new ChorusSession(chorusAccount))
            {
                try
                {
                    CookieContainer cookieContainer = new CookieContainer();
                    try
                    {
                        chorusSession.Login(chorusAccount, cookieContainer);
                        MessageDlg.Show(this, Resources.EditChorusAccountDlg_TestSettings_Settings_are_correct);
                        return true;
                    }
                    catch (RemoteServerException chorusException)
                    {
                        MessageDlg.ShowException(this, chorusException);
                        textPassword.Focus();
                        return false;
                    }
                }
                catch (Exception x)
                {
                    MessageDlg.ShowWithException(this, Resources.EditChorusAccountDlg_TestSettings_Error_connecting_to_server__ + x.Message, x);
                    textServerURL.Focus();
                    return false;
                }
            }
        }

        private bool TestUnifiAccount(UnifiAccount unifiAccount)
        {
            using (var unifiSession = new UnifiSession(unifiAccount))
            {
                try
                {
                    var tokenResponse = unifiAccount.Authenticate();
                    if (tokenResponse.IsError)
                    {
                        string error = tokenResponse.ErrorDescription ?? tokenResponse.Error;
                        MessageDlg.Show(this, TextUtil.LineSeparate(Resources.EditRemoteAccountDlg_TestUnifiAccount_An_error_occurred_while_trying_to_authenticate_, error));
                        if (tokenResponse.Error == "invalid_scope") // Not L10N
                        {
                            tbxClientScope.Focus();
                        }
                        else if (tokenResponse.Error == "invalid_client") // Not L10N
                        {
                            tbxClientSecret.Focus();
                        }
                        else if (tokenResponse.HttpStatusCode == HttpStatusCode.NotFound)
                        {
                            tbxIdentityServer.Focus();
                        }
                        else
                        {
                            textPassword.Focus();
                        }
                        return false;
                    }
                }
                catch (Exception e)
                {
                    MessageDlg.ShowWithException(this, Resources.EditRemoteAccountDlg_TestUnifiAccount_An_error_occurred_while_trying_to_authenticate_, e);
                    tbxIdentityServer.Focus();
                    return false;
                }
                bool[] contentsAvailable = new bool[1];
                unifiSession.ContentsAvailable += () =>
                {
                    lock (contentsAvailable)
                    {
                        contentsAvailable[0] = true;
                        Monitor.Pulse(contentsAvailable);
                    }
                };
                using (var longWaitDlg = new LongWaitDlg())
                {
                    try
                    {
                        longWaitDlg.PerformWork(this, 1000, (ILongWaitBroker broker) =>
                        {
                            while (!broker.IsCanceled)
                            {
                                RemoteServerException remoteServerException;
                                if (unifiSession.AsyncFetchContents(unifiAccount.GetRootUrl(),
                                    out remoteServerException))
                                {
                                    if (remoteServerException != null)
                                    {
                                        throw remoteServerException;
                                    }
                                    break;
                                }
                                lock (contentsAvailable)
                                {
                                    while (!contentsAvailable[0] && !broker.IsCanceled)
                                    {
                                        Monitor.Wait(contentsAvailable, 10);
                                    }
                                }
                            }
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        return false;
                    }
                    catch (Exception e)
                    {
                        MessageDlg.ShowWithException(this, Resources.EditRemoteAccountDlg_TestUnifiAccount_An_exception_occurred_while_trying_to_fetch_the_directory_listing_, e);
                        textServerURL.Focus();
                        return false;
                    }
                }
            } 
            MessageDlg.Show(this, Resources.EditChorusAccountDlg_TestSettings_Settings_are_correct);
            return true;
        }

        private bool ValidateValues()
        {
            var chorusAccount = GetRemoteAccount();
            if (string.IsNullOrEmpty(chorusAccount.Username))
            {
                MessageDlg.Show(this, Resources.EditChorusAccountDlg_ValidateValues_Username_cannot_be_blank);
                textUsername.Focus();
                return false;
            }
            if (string.IsNullOrEmpty(chorusAccount.ServerUrl))
            {
                MessageDlg.Show(this, Resources.EditChorusAccountDlg_ValidateValues_Server_cannot_be_blank);
                textServerURL.Focus();
                return false;
            }
            if (chorusAccount.GetKey() != _originalAccount.GetKey())
            {
                if (_existing.Select(existing => existing.GetKey()).Contains(chorusAccount.GetKey()))
                {
                    MessageDlg.Show(this, string.Format(Resources.EditChorusAccountDlg_ValidateValues_There_is_already_an_account_defined_for_the_user__0__on_the_server__1_, chorusAccount.Username, chorusAccount.ServerUrl));
                }
            }
            try
            {
                var uri = new Uri(chorusAccount.ServerUrl, UriKind.Absolute);
                if (uri.Scheme != "https" && uri.Scheme != "http") // Not L10N
                {
                    MessageDlg.Show(this, Resources.EditChorusAccountDlg_ValidateValues_Server_URL_must_start_with_https____or_http___);
                    textServerURL.Focus();
                    return false;
                }
            }
            catch
            {
                MessageDlg.Show(this, Resources.EditChorusAccountDlg_ValidateValues_Invalid_server_URL_);
                textServerURL.Focus();
                return false;
            }
            return true;
        }

        public RemoteAccountType AccountType
        {
            get
            {
                return comboAccountType.SelectedItem as RemoteAccountType;
            }
            set { comboAccountType.SelectedItem = value; }
        }

        private void comboAccountType_SelectedIndexChanged(object sender, EventArgs e)
        {
            groupBoxUnifi.Visible = RemoteAccountType.UNIFI.Equals(AccountType);
            if (AccountType == RemoteAccountType.CHORUS)
            {
                if (textServerURL.Text == UnifiAccount.DEFAULT.ServerUrl)
                {
                    textServerURL.Text = ChorusAccount.DEFAULT_SERVER;
                }
            }
            if (AccountType == RemoteAccountType.UNIFI)
            {
                if (textServerURL.Text == ChorusAccount.DEFAULT_SERVER)
                {
                    textServerURL.Text = UnifiAccount.DEFAULT.ServerUrl;
                }
            }
        }
    }
}
