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
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using pwiz.Common.Collections;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.Ardia;
using pwiz.CommonMsData.RemoteApi.Unifi;
using pwiz.CommonMsData.RemoteApi.WatersConnect;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.ToolsUI
{
    public partial class EditRemoteAccountDlg : FormEx
    {
        private static readonly int UNIFI_WIZARD_PAGE_INDEX = 0;
        private static readonly int ARDIA_WIZARD_PAGE_INDEX = 1;

        private readonly RemoteAccount _originalAccount;
        private readonly IList<RemoteAccount> _existing;

        private string _btnTest_OriginalLabel_Test;

        //  Ardia Test Only Pass Through
        private string _ardia_TestingOnly_NotSerialized_Username;
        private string _ardia_TestingOnly_NotSerialized_Password;
        private string _ardia_TestingOnly_NotSerialized_Role;

        private ArdiaAccount _ardiaAccount_CurrentlyLoggedIn;


        public EditRemoteAccountDlg(RemoteAccount remoteAccount, IEnumerable<RemoteAccount> existing)
        {
            // _remoteAccount_PassedIntoEdit = remoteAccount;

            InitializeComponent();

            _btnTest_OriginalLabel_Test = btnTest.Text;

            textArdiaServerURL.TextChanged += textArdiaServerURL_TextChanged;

            _existing = ImmutableList.ValueOf(existing);
            _originalAccount = remoteAccount;
            comboAccountType.Items.AddRange(RemoteAccountType.ALL.ToArray());
            if (remoteAccount == null)
            {
                _originalAccount = UnifiAccount.DEFAULT;
                SetRemoteAccount(UnifiAccount.DEFAULT);
            }
            else
            {
                SetRemoteAccount(remoteAccount);
                comboAccountType.Enabled = false;
            }
        }

        private void textArdiaServerURL_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (_ardiaAccount_CurrentlyLoggedIn != null)
                {
                    _ardiaAccount_CurrentlyLoggedIn = null;

                    process_ardiaAccount_CurrentlyLoggedIn_EnableDisableControls();
                }
            }
            catch
            {
                // If there is an error, 
                
            }
        }

        public void SetRemoteAccount(RemoteAccount remoteAccount)
        {
            comboAccountType.SelectedIndex = RemoteAccountType.ALL.IndexOf(remoteAccount.AccountType);
     
            if (remoteAccount is UnifiAccount unifiAccount)
            {
                wizardPagesByAccountType.SelectedIndex = UNIFI_WIZARD_PAGE_INDEX;

                btnTest.Text = _btnTest_OriginalLabel_Test;

                textUsername.Text = remoteAccount.Username;
                textPassword.Text = remoteAccount.Password;
                textServerURL.Text = remoteAccount.ServerUrl;

                tbxIdentityServer.Text = unifiAccount.IdentityServer;
                tbxClientScope.Text = unifiAccount.ClientScope;
                tbxClientSecret.Text = unifiAccount.ClientSecret;
            }
            else if (remoteAccount is WatersConnectAccount wcAccount)
            {
                textUsername.Text = remoteAccount.Username;
                textPassword.Text = remoteAccount.Password;
                textServerURL.Text = remoteAccount.ServerUrl;

                tbxIdentityServer.Text = wcAccount.IdentityServer;
                tbxClientScope.Text = wcAccount.ClientScope;
                tbxClientSecret.Text = wcAccount.ClientSecret;
            }
            else if (remoteAccount is ArdiaAccount ardiaAccount)
            {
                wizardPagesByAccountType.SelectedIndex = ARDIA_WIZARD_PAGE_INDEX;

                textArdiaAlias_Username.Text = remoteAccount.Username;
                textArdiaServerURL.Text = remoteAccount.ServerUrl;

                if (TestLoggedInArdiaAccount(ardiaAccount))
                {
                    _ardiaAccount_CurrentlyLoggedIn = ardiaAccount;
                }

                cbArdiaDeleteRawAfterImport.Checked = ardiaAccount.DeleteRawAfterImport;
                
                //  Ardia Test Only Pass Through
                _ardia_TestingOnly_NotSerialized_Username = ardiaAccount.TestingOnly_NotSerialized_Username;
                _ardia_TestingOnly_NotSerialized_Password = ardiaAccount.TestingOnly_NotSerialized_Password;
                _ardia_TestingOnly_NotSerialized_Role = ardiaAccount.TestingOnly_NotSerialized_Role;
            }

            process_ardiaAccount_CurrentlyLoggedIn_EnableDisableControls();
        }

        public RemoteAccount GetRemoteAccount()
        {
            var accountType = (RemoteAccountType) comboAccountType.SelectedItem;

            var remoteAccount = accountType.GetEmptyAccount();
            if (accountType == RemoteAccountType.UNIFI)
            {
                remoteAccount = remoteAccount.ChangeServerUrl(textServerURL.Text.Trim().TrimEnd('/'))
                    .ChangeUsername(textUsername.Text.Trim()).ChangePassword(textPassword.Text);

                var unifiAccount = (UnifiAccount) remoteAccount;
                unifiAccount = unifiAccount.ChangeIdentityServer(tbxIdentityServer.Text)
                    .ChangeClientScope(tbxClientScope.Text)
                    .ChangeClientSecret(tbxClientSecret.Text);
                remoteAccount = unifiAccount;
            }
            else if (accountType == RemoteAccountType.WATERS_CONNECT)
            {
                remoteAccount = remoteAccount.ChangeServerUrl(textServerURL.Text.Trim().TrimEnd('/'))
                    .ChangeUsername(textUsername.Text.Trim()).ChangePassword(textPassword.Text);

                var wcAccount = (WatersConnectAccount) remoteAccount;
                wcAccount = wcAccount.ChangeIdentityServer(tbxIdentityServer.Text)
                    .ChangeClientScope(tbxClientScope.Text)
                    .ChangeClientSecret(tbxClientSecret.Text);
                remoteAccount = wcAccount;
            }
            else if (accountType == RemoteAccountType.ARDIA)
            {
                // CONSIDER: does serverUrl need more input validation? For example:
                //              (1) removing the scheme (ex: https://), if provided
                //              (2) checking for a valid URL?
                remoteAccount = remoteAccount.ChangeServerUrl(textArdiaServerURL.Text.Trim().TrimEnd('/'));
                remoteAccount = remoteAccount.ChangeUsername(textArdiaAlias_Username.Text.Trim());

                var ardiaAccount = (ArdiaAccount) remoteAccount;
                ardiaAccount = ardiaAccount.ChangeDeleteRawAfterImport(cbArdiaDeleteRawAfterImport.Checked);

                if (_ardiaAccount_CurrentlyLoggedIn != null
                    && _ardiaAccount_CurrentlyLoggedIn.ServerUrl.Equals(ardiaAccount.ServerUrl))
                {
                    ardiaAccount = ardiaAccount.ChangeToken(_ardiaAccount_CurrentlyLoggedIn.Token);
                }

                //  Ardia Test Only Pass Through
                ardiaAccount = ardiaAccount.ChangeTestingOnly_NotSerialized_Username(_ardia_TestingOnly_NotSerialized_Username);
                ardiaAccount = ardiaAccount.ChangeTestingOnly_NotSerialized_Password(_ardia_TestingOnly_NotSerialized_Password);
                ardiaAccount = ardiaAccount.ChangeTestingOnly_NotSerialized_Role(_ardia_TestingOnly_NotSerialized_Role);

                remoteAccount = ardiaAccount;
            }
            return remoteAccount;
        }

        // Test Only
        public void LogoutAccount()
        {
            logoutArdia_UsingSystemDefaultBrowser();
        }

        private void btnLogoutArdia_Click(object sender, EventArgs e)
        {
            logoutArdia_UsingSystemDefaultBrowser();
        }

        private async void logoutArdia_UsingSystemDefaultBrowser()
        {
            var logoutUrl = await GetBrowserLogoutUrl();

            _ardiaAccount_CurrentlyLoggedIn = null;

            process_ardiaAccount_CurrentlyLoggedIn_EnableDisableControls();


            if (logoutUrl == null)
            {
                return;
            }

            await Task.Run(() =>
            {
                Process browserProcess = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = logoutUrl,
                        UseShellExecute = true,
                        Verb = string.Empty
                    }
                };
                browserProcess.Start();
            });

        }

        // Method used to get the logout URL for the browser login process
        private async Task<string> GetBrowserLogoutUrl()
        {
            var serverUri = new Uri(_ardiaAccount_CurrentlyLoggedIn.ServerUrl, UriKind.Absolute);

            var baseUrl = _ardiaAccount_CurrentlyLoggedIn.ServerUrl.Replace(@"https://", @"");

            if (!Settings.Default.ArdiaRegistrationCodeEntries.TryGetValue(baseUrl, out var ardiaRegistrationEntry))
            {
                MessageDlg.Show(this, ToolsUIResources.EditRemoteAccountDlg_GetBrowserLogoutUrl_Error__no_Ardia_registration_code_for_URL);
                return null;
            }
            
            var applicationCode = ardiaRegistrationEntry.ClientApplicationCode;

            var apiBaseUri = new UriBuilder { Scheme = serverUri.Scheme, Host = @$"api.{baseUrl}" }.Uri;
            var baseUri = new UriBuilder { Scheme = serverUri.Scheme, Host = baseUrl }.Uri;
            var userEndpointPath = @"session-management/bff/user";
            var userEndpointUri = new Uri(apiBaseUri, userEndpointPath);

            var cookieContainer = new CookieContainer();
            using var handler = new HttpClientHandler();
            handler.CookieContainer = cookieContainer;
            using var httpClient = new HttpClient(handler);
            httpClient.BaseAddress = baseUri;
            // Add the Bff-Host cookie to the cookie container
            cookieContainer.Add(apiBaseUri, new Cookie(@"Bff-Host", ArdiaCredentialHelper.GetToken(_ardiaAccount_CurrentlyLoggedIn).Decrypted));
            // Add the required headers to the request
            httpClient.DefaultRequestHeaders.Add(@"Accept", @"application/json");
            httpClient.DefaultRequestHeaders.Add(@"applicationCode", applicationCode);
            httpClient.DefaultRequestHeaders.Add(@"x-csrf", @"1");

            var response = await httpClient.GetAsync(userEndpointUri);

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                //  Ardia session cookie is not valid so already logged out so just drop the session in Skyline
                return null;
            }
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            var responseJson = JsonConvert.DeserializeObject<IEnumerable<IDictionary<string, string>>>(responseString);
            // Get the logout URL from the response JSON object
            var logoutUrl = responseJson.FirstOrDefault(x => x[@"type"] == @"bff:logout_url")?[@"value"];

            if (logoutUrl != null)
            {
                var logoutEndpointPath = logoutUrl.Split('?')[0];
                // The query string contains the session ID
                var queryString = logoutUrl.Split('?')[1];

                // Construct the complete logout URL
                var completeLogoutUrl = new UriBuilder(apiBaseUri)
                {
                    Path = logoutEndpointPath,
                    Query = @$"{queryString}&applicationcode={applicationCode}"
                }.Uri.ToString();
                return completeLogoutUrl;
            }
            else
            {
                MessageDlg.Show(this, ToolsUIResources.EditRemoteAccountDlg_GetBrowserLogoutUrl_Error__unable_to_compute_URL_for_logout);
                return null;
            }
        }

        //  CURRENTLY UNUSED Logout from Ardia using WebView
        //
        // private void logoutArdia_UsingWebView_Using_ArdiaLogoutDlg()
        // {
        //     using var logoutDlg = new ArdiaLogoutDlg(_ardiaAccount_CurrentlyLoggedIn);
        //     if (DialogResult.Cancel == logoutDlg.ShowDialog(this))
        //     {
        //     }
        //
        //     // Remove AuthenticatedHttpClientFactory from _ardiaAccount_PassedIntoEdit since is now logged out.
        //     //   Getting removed regardless of what the user does in the child window
        //     _ardiaAccount_CurrentlyLoggedIn.ResetAuthenticatedHttpClientFactory();
        //
        //     _ardiaAccount_CurrentlyLoggedIn = null;
        //
        //     process_ardiaAccount_CurrentlyLoggedIn_EnableDisableControls();
        // }

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

        /// <summary>
        /// Button event handler. For Ardia accounts, the label might be [Test] or [Connect].
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
            return account switch
            {
                UnifiAccount unifiAccount => TestUnifiAccount(unifiAccount),
                ArdiaAccount ardiaAccount => TestArdiaAccount(ardiaAccount),
                WatersConnectAccount wcAccount => TestWatersConnectAccount(wcAccount),
                _ => throw new InvalidOperationException(@"remote account type not handled in TestSettings")
            };
        }

        private bool TestWatersConnectAccount(WatersConnectAccount wcAccount)
        {
            using (var wcSession = new WatersConnectSession(wcAccount))
            {
                try
                {
                    var tokenResponse = wcAccount.Authenticate();
                    if (tokenResponse.IsError)
                    {
                        string error = tokenResponse.ErrorDescription ?? tokenResponse.Error;
                        MessageDlg.Show(this, TextUtil.LineSeparate(ToolsUIResources.EditRemoteAccountDlg_TestUnifiAccount_An_error_occurred_while_trying_to_authenticate_, error));
                        if (tokenResponse.Error == @"invalid_scope")
                        {
                            tbxClientScope.Focus();
                        }
                        else if (tokenResponse.Error == @"invalid_client")
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
                    MessageDlg.ShowWithException(this, ToolsUIResources.EditRemoteAccountDlg_TestUnifiAccount_An_error_occurred_while_trying_to_authenticate_, e);
                    tbxIdentityServer.Focus();
                    return false;
                }

                return TestAccount(wcSession);
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
                        MessageDlg.Show(this, TextUtil.LineSeparate(ToolsUIResources.EditRemoteAccountDlg_TestUnifiAccount_An_error_occurred_while_trying_to_authenticate_, error));
                        if (tokenResponse.Error == @"invalid_scope")
                        {
                            tbxClientScope.Focus();
                        }
                        else if (tokenResponse.Error == @"invalid_client")
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
                    MessageDlg.ShowWithException(this, ToolsUIResources.EditRemoteAccountDlg_TestUnifiAccount_An_error_occurred_while_trying_to_authenticate_, e);
                    tbxIdentityServer.Focus();
                    return false;
                }

                return TestAccount(unifiSession);
            } 
        }

        private bool TestArdiaAccount(ArdiaAccount ardiaAccount)
        {
            if (_ardiaAccount_CurrentlyLoggedIn == null)
            {
                try
                {
                    RemoteSession.RemoteAccountUserInteraction.UserLogin(ardiaAccount);
                }
                catch (Exception e)
                {
                    MessageDlg.ShowWithException(this, ToolsUIResources.EditRemoteAccountDlg_TestSettings_Account_credentials_not_valid, e);
                    textArdiaServerURL.Focus();
                    return false;
                }
            }

            var testResult = TestLoggedInArdiaAccount(ardiaAccount);

            _ardiaAccount_CurrentlyLoggedIn = testResult ? ardiaAccount : null;
            process_ardiaAccount_CurrentlyLoggedIn_EnableDisableControls();
            var message = testResult ? ToolsUIResources.EditRemoteAccountDlg_TestSettings_Settings_are_correct : ToolsUIResources.EditRemoteAccountDlg_TestSettings_Account_credentials_not_valid;
            MessageDlg.Show(this, message);
            return testResult;
        }

        private bool TestLoggedInArdiaAccount(ArdiaAccount ardiaAccount)
        {
            using var longWaitDlg = new LongWaitDlg();
            longWaitDlg.Text = ToolsUIResources.EditRemoteAccountDlg_TestAccount_Testing_remote_account_connection;
            var authenticationResult = ArdiaResult.Default;
            try
            {
                longWaitDlg.PerformWork(this, 1000, (ILongWaitBroker broker) =>
                {
                    authenticationResult = ardiaAccount.CheckAuthentication();
                });
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            return !longWaitDlg.IsCanceled && authenticationResult.IsSuccess;
        }

        private bool TestAccount(RemoteSession session)
        {
            
            bool[] contentsAvailable = new bool[1];
            session.ContentsAvailable += () =>
            {
                lock (contentsAvailable)
                {
                    contentsAvailable[0] = true;
                    Monitor.Pulse(contentsAvailable);
                }
            };
            using (var longWaitDlg = new LongWaitDlg())
            {
                longWaitDlg.Text = ToolsUIResources.EditRemoteAccountDlg_TestAccount_Testing_remote_account_connection;
                try
                {
                    longWaitDlg.PerformWork(this, 1000, (ILongWaitBroker broker) =>
                    {
                        while (!broker.IsCanceled)
                        {
                            RemoteServerException remoteServerException;
                            if (session.AsyncFetchContents(session.Account.GetRootUrl(),
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
                    //  Property has "_TestUnifiAccount_" but is used for all accounts.  Property text currently does not have Unifi in it so can use for all accounts for now.
                    MessageDlg.ShowWithException(this, ToolsUIResources.EditRemoteAccountDlg_TestUnifiAccount_An_exception_occurred_while_trying_to_fetch_the_directory_listing_, e);
                    textServerURL.Focus();
                    return false;
                }
                if (longWaitDlg.IsCanceled)
                {
                    return false;
                }
            }

            MessageDlg.Show(this, ToolsUIResources.EditRemoteAccountDlg_TestSettings_Settings_are_correct);
            return true;
        }

        private bool ValidateValues()
        {
            var remoteAccount = GetRemoteAccount();

            if (RemoteAccountType.UNIFI.Equals(AccountType))
            {
                if (string.IsNullOrEmpty(remoteAccount.Username))
                {
                    MessageDlg.Show(this,
                        ToolsUIResources.EditRemoteAccountDlg_ValidateValues_Username_cannot_be_blank);
                    textUsername.Focus();
                    return false;
                }
            }

            if (string.IsNullOrEmpty(remoteAccount.ServerUrl))
            {
                MessageDlg.Show(this, ToolsUIResources.EditRemoteAccountDlg_ValidateValues_Server_cannot_be_blank);
                if (RemoteAccountType.UNIFI.Equals(AccountType))
                {
                    textServerURL.Focus();
                }
                else if (RemoteAccountType.ARDIA.Equals(AccountType))
                {
                    textArdiaServerURL.Focus();
                }
                return false;
            }
            if (remoteAccount.GetKey() != _originalAccount.GetKey())
            {
                if (_existing.Select(existing => existing.GetKey()).Contains(remoteAccount.GetKey()))
                {
                    MessageDlg.Show(this, string.Format(ToolsUIResources.EditRemoteAccountDlg_ValidateValues_There_is_already_an_account_defined_for_the_user__0__on_the_server__1_, remoteAccount.Username, remoteAccount.ServerUrl));
                }
            }
            try
            {
                var uri = new Uri(remoteAccount.ServerUrl, UriKind.Absolute);
                if (uri.Scheme != @"https" && uri.Scheme != @"http")
                {
                    MessageDlg.Show(this, ToolsUIResources.EditRemoteAccountDlg_ValidateValues_Server_URL_must_start_with_https____or_http___);
                    if (RemoteAccountType.UNIFI.Equals(AccountType))
                    {
                        textServerURL.Focus();
                    }
                    else if (RemoteAccountType.ARDIA.Equals(AccountType))
                    {
                        textArdiaServerURL.Focus();
                    }
                    return false;
                }
            }
            catch
            {
                MessageDlg.Show(this, ToolsUIResources.EditRemoteAccountDlg_ValidateValues_Invalid_server_URL_);
                if (RemoteAccountType.UNIFI.Equals(AccountType))
                {
                    textServerURL.Focus();
                }
                else if (RemoteAccountType.ARDIA.Equals(AccountType))
                {
                    textArdiaServerURL.Focus();
                }
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
            if (RemoteAccountType.UNIFI.Equals(AccountType) || RemoteAccountType.WATERS_CONNECT.Equals(AccountType))
            {
                wizardPagesByAccountType.SelectedIndex = UNIFI_WIZARD_PAGE_INDEX;

                process_ardiaAccount_CurrentlyLoggedIn_EnableDisableControls();
            }

            if (RemoteAccountType.ARDIA.Equals(AccountType))
            {
                // Skyline only supports one Ardia account so prevent adding a second account and 
                // cancel this dialog, returning to the list of remote accounts.
                //
                // First, however, switch to the Ardia account creation screen so the message
                // is more in context and doesn't appear atop the UI for adding a UNIFI account.

                wizardPagesByAccountType.SelectedIndex = ARDIA_WIZARD_PAGE_INDEX;

                process_ardiaAccount_CurrentlyLoggedIn_EnableDisableControls();

                if (_existing.ToList().Any(account => account.AccountType == RemoteAccountType.ARDIA))
                {
                    MessageDlg.Show(this, ToolsUIResources.EditRemoteAccountDlg_Ardia_OneAccountSupported, false, MessageBoxButtons.OK);

                    // Reset back to the default account type
                    AccountType = RemoteAccountType.UNIFI;
                }
            }
        }

        private void process_ardiaAccount_CurrentlyLoggedIn_EnableDisableControls()
        {
            if (RemoteAccountType.UNIFI.Equals(AccountType))
            {
                if (!btnTest.Text.Equals(_btnTest_OriginalLabel_Test))
                {
                    btnTest.Text = _btnTest_OriginalLabel_Test;
                }

                return;
            }
    
            if (_ardiaAccount_CurrentlyLoggedIn != null)
            {
                btnLogoutArdia.Enabled = true;

                if (!btnTest.Text.Equals(_btnTest_OriginalLabel_Test))
                {
                    btnTest.Text = _btnTest_OriginalLabel_Test;
                }
            }
            else
            {
                btnLogoutArdia.Enabled = false;

                var btnText_Connect = ToolsUIResources.EditRemoteAccountDlg_TestConnectButton_AltLabelText_Connect;

                if (!btnTest.Text.Equals(btnText_Connect))
                {
                    btnTest.Text = btnText_Connect;
                }
            }
        }

        // Test helper
        public bool IsVisibleAccountType(RemoteAccountType accountType)
        {
            return comboAccountType.SelectedIndex == RemoteAccountType.ALL.IndexOf(accountType);
        }
    }
}
