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
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using pwiz.Common.Collections;
using pwiz.Common.GUI;
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

            if (remoteAccount != null)
                comboAccountType.Enabled = false;   // cannot change type when editing existing account
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
            if (remoteAccount.HasAlias)
                textAlias.Text = remoteAccount.AccountAlias;

            if (remoteAccount is UnifiAccount unifiAccount)
            {
                wizardPagesByAccountType.SelectedIndex = UNIFI_WIZARD_PAGE_INDEX;
                groupBoxUnifi.Text = RemoteApiResources.Unifi_GroupBox_Label;

                btnTest.Text = _btnTest_OriginalLabel_Test;

                textUsername.Text = remoteAccount.Username;
                textPassword.Text = remoteAccount.Password;
                textServerURL.Text = remoteAccount.ServerUrl;

                tbxIdentityServer.Text = unifiAccount.IdentityServer;
                tbxClientScope.Text = unifiAccount.ClientScope;
                tbxClientSecret.Text = unifiAccount.ClientSecret;
                tbxClientId.Text = unifiAccount.ClientId;
            }
            else if (remoteAccount is WatersConnectAccount wcAccount)
            {
                wizardPagesByAccountType.SelectedIndex = UNIFI_WIZARD_PAGE_INDEX;
                groupBoxUnifi.Text = RemoteApiResources.Waters_Connect_GroupBox_Label;

                btnTest.Text = _btnTest_OriginalLabel_Test;

                textUsername.Text = remoteAccount.Username;
                textPassword.Text = remoteAccount.Password;
                textServerURL.Text = remoteAccount.ServerUrl;

                tbxIdentityServer.Text = wcAccount.IdentityServer;
                tbxClientScope.Text = wcAccount.ClientScope;
                tbxClientSecret.Text = wcAccount.ClientSecret;
                tbxClientId.Text = wcAccount.ClientId;
            }
            else if (remoteAccount is ArdiaAccount ardiaAccount)
            {
                wizardPagesByAccountType.SelectedIndex = ARDIA_WIZARD_PAGE_INDEX;

                textArdiaAlias_Username.Text = remoteAccount.Username;
                textArdiaServerURL.Text = remoteAccount.ServerUrl;

                if (ardiaAccount.HasAuthenticatedHttpClientFactory())
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
                    .ChangeClientSecret(tbxClientSecret.Text)
                    .ChangeClientId(tbxClientId.Text);
                remoteAccount = unifiAccount;
            }
            else if (accountType == RemoteAccountType.WATERS_CONNECT)
            {
                remoteAccount = remoteAccount.ChangeServerUrl(textServerURL.Text.Trim().TrimEnd('/'))
                    .ChangeUsername(textUsername.Text.Trim()).ChangePassword(textPassword.Text);

                var wcAccount = (WatersConnectAccount) remoteAccount;
                wcAccount = wcAccount.ChangeIdentityServer(tbxIdentityServer.Text)
                    .ChangeClientScope(tbxClientScope.Text)
                    .ChangeClientSecret(tbxClientSecret.Text)
                    .ChangeClientId(tbxClientId.Text);
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
                    ardiaAccount.SetAuthenticatedHttpClientFactory(_ardiaAccount_CurrentlyLoggedIn);
                    ardiaAccount = ardiaAccount.ChangeToken(_ardiaAccount_CurrentlyLoggedIn.Token);
                }

                //  Ardia Test Only Pass Through
                ardiaAccount = ardiaAccount.ChangeTestingOnly_NotSerialized_Username(_ardia_TestingOnly_NotSerialized_Username);
                ardiaAccount = ardiaAccount.ChangeTestingOnly_NotSerialized_Password(_ardia_TestingOnly_NotSerialized_Password);
                ardiaAccount = ardiaAccount.ChangeTestingOnly_NotSerialized_Role(_ardia_TestingOnly_NotSerialized_Role);

                remoteAccount = ardiaAccount;
            }
            remoteAccount.AccountAlias = textAlias.Text.Trim();
            return remoteAccount;
        }

        private void btnLogoutArdia_Click(object sender, EventArgs e)
        {
            logoutArdia_UsingSystemDefaultBrowser();
        }

        private async void logoutArdia_UsingSystemDefaultBrowser()
        {
            var logoutUrl = await GetBrowserLogoutUrl();

            // Remove AuthenticatedHttpClientFactory from _ardiaAccount_PassedIntoEdit since is now logged out.
            //   Getting removed regardless of what the user does in the child window
            _ardiaAccount_CurrentlyLoggedIn.ResetAuthenticatedHttpClientFactory();

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
            var res = account switch
            {
                UnifiAccount unifiAccount => TestUnifiAccount(unifiAccount),
                ArdiaAccount ardiaAccount => TestArdiaAccount(ardiaAccount),
                WatersConnectAccount wcAccount => TestWatersConnectAccount(wcAccount),
                _ => throw new InvalidOperationException(@"remote account type not handled in TestSettings")
            };
            if (!res)
            {
                btnOK.Enabled = false;
            }
            return res;
        }

        private bool TestWatersConnectAccount(WatersConnectAccount wcAccount)
        {
            try
            {
                using (var wcSession = new WatersConnectSession(wcAccount))
                {
                    return TestAccount(wcSession);
                }
            }
            catch (AuthenticationException ae)
            {
                if (!ae.Data.Contains(WatersConnectAccount.TOKEN_DATA) ||
                    ae.Data[WatersConnectAccount.TOKEN_DATA] == null)
                {
                    MessageDlg.Show(this,
                        TextUtil.LineSeparate(
                            ToolsUIResources
                                .EditRemoteAccountDlg_TestUnifiAccount_An_error_occurred_while_trying_to_authenticate_,
                            ae.Message));
                    return false;
                }
                var tokenResponse = JObject.Parse((string)ae.Data[WatersConnectAccount.TOKEN_DATA]);
                string error = (tokenResponse[@"error_description"] ?? tokenResponse[@"error"] ?? "").ToString();
                var errorType = (tokenResponse[@"error"] ?? "").ToString();
                if (errorType.ToString() == @"invalid_scope")
                {
                    tbxClientScope.Focus();
                }
                else if (errorType.ToString() == @"invalid_client")
                {
                    tbxClientSecret.Focus();
                    error = ToolsUIResources.EditRemoteAccountDlg_TestWatersConnectAccount_invalid_client_id_or_secret;
                }
                else if (errorType.ToString() == @"invalid_grant")
                {
                    textPassword.Focus();
                }
                else
                {
                    tbxIdentityServer.Focus();
                }
                MessageDlg.ShowError(this, TextUtil.LineSeparate(ToolsUIResources.EditRemoteAccountDlg_TestUnifiAccount_An_error_occurred_while_trying_to_authenticate_, error));
                return false;
            }
            catch (Exception e)
            {
                MessageDlg.ShowWithException(this, ToolsUIResources.EditRemoteAccountDlg_TestUnifiAccount_An_error_occurred_while_trying_to_authenticate_, e);
                tbxIdentityServer.Focus();
                return false;
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
            using (var ardiaSession = new ArdiaSession(ardiaAccount))
            {
                try
                {
                    var authenticatedHttpClient = ardiaAccount.GetAuthenticatedHttpClient();
                    if (authenticatedHttpClient == null)
                    {
                        return false;
                    }
                }
                catch (OperationCanceledException)
                {
                    MessageDlg.Show(this, ToolsUIResources.EditRemoteAccountDlg_TestArdiaAccount_Login_was_canceled);
                    return false;
                }
                catch (Exception e)
                {
                    MessageDlg.ShowWithException(this, ToolsUIResources.EditRemoteAccountDlg_TestUnifiAccount_An_error_occurred_while_trying_to_authenticate_, e);
                    textArdiaServerURL.Focus();
                    return false;
                }

                var testResults = TestAccount(ardiaSession);

                if (testResults && ardiaAccount.HasAuthenticatedHttpClientFactory())
                {
                    _ardiaAccount_CurrentlyLoggedIn = ardiaAccount;
                }

                process_ardiaAccount_CurrentlyLoggedIn_EnableDisableControls();


                return testResults;
            }
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

            if (session.Account is WatersConnectAccount wca && !wca.SupportsMethodDevelopment)
                MessageDlg.Show(this,
                    ToolsUIResources.EditRemoteAccountDlg_TestAccount_Settings_are_correct__but_this_Waters_Connect_instance_does_not_support_method_development__Method_upload_will_be_disabled_,
                    false, MessageIcons.Warning);
            else
                MessageDlg.Show(this, ToolsUIResources.EditRemoteAccountDlg_TestSettings_Settings_are_correct, false, MessageIcons.Success);

            return true;
        }

        private bool ValidateValues()
        {
            var remoteAccount = GetRemoteAccount();

            if (RemoteAccountType.UNIFI.Equals(AccountType) || RemoteAccountType.WATERS_CONNECT.Equals(AccountType))
            {
                if (string.IsNullOrEmpty(remoteAccount.Username))
                {
                    MessageDlg.ShowError(this, ToolsUIResources.EditRemoteAccountDlg_ValidateValues_Username_cannot_be_blank);
                    textUsername.Focus();
                    return false;
                }
            }

            if (string.IsNullOrEmpty(remoteAccount.ServerUrl))
            {
                MessageDlg.ShowError(this, ToolsUIResources.EditRemoteAccountDlg_ValidateValues_Server_cannot_be_blank);
                if (RemoteAccountType.UNIFI.Equals(AccountType) || RemoteAccountType.WATERS_CONNECT.Equals(AccountType))
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
                    MessageDlg.ShowError(this, string.Format(ToolsUIResources.EditRemoteAccountDlg_ValidateValues_There_is_already_an_account_defined_for_the_user__0__on_the_server__1_, remoteAccount.Username, remoteAccount.ServerUrl));
                }
            }
            try
            {
                var uri = new Uri(remoteAccount.ServerUrl, UriKind.Absolute);
                if (uri.Scheme != @"https" && uri.Scheme != @"http")
                {
                    MessageDlg.ShowError(this, ToolsUIResources.EditRemoteAccountDlg_ValidateValues_Server_URL_must_start_with_https____or_http___);
                    if (RemoteAccountType.UNIFI.Equals(AccountType) || RemoteAccountType.WATERS_CONNECT.Equals(AccountType))
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
                MessageDlg.ShowError(this, ToolsUIResources.EditRemoteAccountDlg_ValidateValues_Invalid_server_URL_);
                if (RemoteAccountType.UNIFI.Equals(AccountType) || RemoteAccountType.WATERS_CONNECT.Equals(AccountType))
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

                if (RemoteAccountType.UNIFI.Equals(AccountType))
                {
                    SetRemoteAccount( UnifiAccount.DEFAULT );
                }
                else if (RemoteAccountType.WATERS_CONNECT.Equals(AccountType))
                {
                    if (Install.IsDeveloperInstall)
                    {
                        SetRemoteAccount(WatersConnectAccount.DEV_DEFAULT);
                    }
                    else
                        SetRemoteAccount(WatersConnectAccount.DEFAULT);
                }
            }

            if (RemoteAccountType.ARDIA.Equals(AccountType))
            {
                wizardPagesByAccountType.SelectedIndex = ARDIA_WIZARD_PAGE_INDEX;

                process_ardiaAccount_CurrentlyLoggedIn_EnableDisableControls();

                // Skyline only supports one Ardia account so these checks prevent adding second account in two scenarios:
                // 
                //   1) Users click the [Edit] button on an existing Ardia account. This allows editing an existing Ardia account.
                //   2) Users click the [Add] button and choose "Ardia" as the account type in the new account dialog
                //
                // Both scenarios are handled here because this combobox event handler runs in both cases.

                // Scenario #1: Allow editing an existing Ardia account by checking the account passed to the edit dialog.
                if (_originalAccount != null && _originalAccount.AccountType == RemoteAccountType.ARDIA)
                {
                    return;
                }

                // Scenario #2: if Skyline gets here, it's not editing an existing Ardia account so must be adding a new one. So see if
                // an Ardia account is already defined, show an error message if so, and switch the dialog's AccountType back to the default (Unifi).
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
            if (RemoteAccountType.UNIFI.Equals(AccountType) || RemoteAccountType.WATERS_CONNECT.Equals(AccountType))
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

        public void text_TextChanged(object sender, EventArgs e)
        {
            btnOK.Enabled = true;
        }
        // Test helper
        public bool IsVisibleAccountType(RemoteAccountType accountType)
        {
            return comboAccountType.SelectedIndex == RemoteAccountType.ALL.IndexOf(accountType);
        }
    }
}
