﻿/*
 * Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
 *
 * Copyright 2024 University of Washington - Seattle, WA
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

using pwiz.Skyline.Model.Results.RemoteApi.Ardia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using pwiz.Skyline.Util;
using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using pwiz.Skyline.Util.Extensions;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using IdentityModel;
using IdentityModel.Client;
using Newtonsoft.Json;
using pwiz.Common.Collections;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Alerts
{
    public partial class ArdiaLoginDlg : FormEx
    {
        private static readonly bool FORCE_DO_REGISTRATION_WITH_ARDIA_STEP  = false;


        private static readonly int TESTING_WEBVIEW_WIZARD_PAGE_INDEX = 0;
        private static readonly int MAIN_LOADING_MESSAGE_WIZARD_PAGE_INDEX = 1;
        private static readonly int MAIN_REGISTER_SKYLINE_WIZARD_PAGE_INDEX = 2;
        private static readonly int MAIN_REGISTER_SKYLINE_COMPLETE_WIZARD_PAGE_INDEX = 3;
        private static readonly int MAIN_LOGIN_WIZARD_PAGE_INDEX = 4;

        //  For wizardPagesRegisterPhases index
        //private static readonly int REGISTER_BUTTON_wizardPagesRegisterPhases_INDEX = 0;
        private static readonly int REGISTER_IN_PROGRESS_wizardPagesRegisterPhases_INDEX = 1;

        //  For wizardPagesLoginPhases index
        //private static readonly int LOGIN_BUTTON_wizardPagesLoginPhases_INDEX = 0;
        private static readonly int LOGIN_IN_PROGRESS_wizardPagesLoginPhases_INDEX = 1;

        private enum RegisterDevice_UsingSystemDefaultBrowser_ResultEnum
        {
            SUCCESS, FAIL_GENERAL, FAIL_NOT_AUTHORIZED
        }

        public ArdiaAccount Account { get; private set; }
        public Func<HttpClient> AuthenticatedHttpClientFactory { get; private set; }

        private Uri _ardiaServer_URI;
        private string _ardiaServerURL_BaseURL;
        private string _ardiaServerURL_Transport;



        //  Used for log in to Ardia in Default System Browser
        private Process _browserProcess;

        private HttpListener _httpListener;
        private int _httpListener_Port;


        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private CancellationToken _cancellationToken;


        public ArdiaLoginDlg(ArdiaAccount account/*, bool headless = false*/)
        {
            _cancellationToken = _cancellationTokenSource.Token;

            InitializeComponent();

            Account = account;
            _tempUserDataFolder = new TemporaryDirectory(null, @"~SK_WebView2");


            if (string.IsNullOrEmpty(Account.TestingOnly_NotSerialized_Username) ||
                string.IsNullOrEmpty(Account.TestingOnly_NotSerialized_Password))
            {
                //  Login via System Default Browser.   Standard way to Login

                wizardPagesMain.SelectedIndex = MAIN_LOADING_MESSAGE_WIZARD_PAGE_INDEX;
            }
            else
            {   //   Login via Embedded WebView.    Used for Programmatic Login for Testing Only

                wizardPagesMain.SelectedIndex = TESTING_WEBVIEW_WIZARD_PAGE_INDEX;
            }

            {
                var serverUrl = Account.ServerUrl;

                _ardiaServer_URI = new Uri(serverUrl, UriKind.Absolute);

                var transport_HTTPS = @"https://";
                var transport_HTTP = @"http://";

                if (serverUrl.StartsWith(transport_HTTPS))
                {
                    _ardiaServerURL_BaseURL = serverUrl.Substring(transport_HTTPS.Length);
                    _ardiaServerURL_Transport = transport_HTTPS;
                }
                else if (serverUrl.StartsWith(transport_HTTP))
                {
                    _ardiaServerURL_BaseURL = serverUrl.Substring(transport_HTTP.Length);
                    _ardiaServerURL_Transport = transport_HTTP;
                }
                else
                {
                    //  Should not get here
                    _ardiaServerURL_BaseURL = serverUrl;
                    _ardiaServerURL_Transport = "";
                }
                
            }


            this.FormClosing += (sender, e) =>
            {
                _cancellationTokenSource.Cancel();

                _cancellationTokenSource.Dispose();

                Stop_HTTPListener();
            };

            // To support command line, need to do something completely different since Ardia objects to Skyline code having username/password and also programmatic sign in to Ardia not possible for MFA and other possible issues.
            //
            //      The code to Programmatic sign in has been changed to use account.TestingOnly_NotSerialized_Username AND account.TestingOnly_NotSerialized_Password
            //
            // if (headless)
            // {
            //     if (account.TestingOnly_NotSerialized_Username.IsNullOrEmpty() || account.TestingOnly_NotSerialized_Password.IsNullOrEmpty())
            //     {
            //         throw new ArgumentException("importing an Ardia file from command-line requires the account to have username and password set up in Skyline (Tools > Options > Remote Accounts)");
            //     }
            //     var uiThread = new Thread(() =>
            //     {
            //         System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(async () =>
            //         {
            //             var environment = await CoreWebView2Environment.CreateAsync(null, null, null);
            //             var controller = await environment.CreateCoreWebView2ControllerAsync(HWND_MESSAGE);
            //             controller.CoreWebView2.Navigate("https://microsoft.com");
            //         });
            //
            //         Dispatcher.Run();
            //     });
            //
            //     uiThread.SetApartmentState(ApartmentState.STA);
            //     uiThread.Start();
            //     uiThread.Join();
            //     Visible = false;
            // }
        }

        ///////////////////
        ///  
        /// Handlers

        private void btnRegisterSkyline_Click(object sender, EventArgs e)
        {
            StartRegistrationSkylineWithArdia_ButtonClicked();
        }

        private void btnRegisterSkyline_InErrorBlock_Click(object sender, EventArgs e)
        {
            pnlRegisterFailedUserNotAuth.Visible = false;
            StartRegistrationSkylineWithArdia_ButtonClicked();
        }

        private void btnViewAccount_Click(object sender, EventArgs e)
        {
            OpenBrowserWithServerUrl_ToShowAccount();
        }

        private void btnViewAccount_2_Click(object sender, EventArgs e)
        {
            OpenBrowserWithServerUrl_ToShowAccount();
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            StartLogin_WithArdia_ButtonClicked();
        }

        private void btnShowLoginTab_Click(object sender, EventArgs e)
        {
            wizardPagesMain.SelectedIndex = MAIN_LOGIN_WIZARD_PAGE_INDEX;
        }

        ///////////////////
        ///  
        /// 

        private TemporaryDirectory _tempUserDataFolder;

        private Cookie _bffCookie;

        private bool _firstTime_ExecuteClientRegistration = true;

        private void Stop_HTTPListener()
        {
            // Stop the HttpListener
            if (_httpListener != null)
            {
                try
                {
                    _httpListener.Stop();
                }
                catch (Exception)
                {
                    // ignored
                }
                try
                {
                    _httpListener.Abort();
                }
                catch (Exception)
                {
                    // ignored
                }
                try
                {
                    _httpListener.Close();
                }
                catch (Exception)
                {
                    // ignored
                }

                _httpListener = null;
            }
        }

        // private ArdiaRegistrationCodeEntry _ardia_ArdiaRegistrationCodeEntry__TEMP; //  TODO  Only used to hold ArdiaRegistrationCodeEntry until store in Settings

        private ArdiaRegistrationCodeEntry GetSavedArdiaRegistrationEntry()
        {
            //  Temp to NOT store in Settings

            // return _ardia_ArdiaRegistrationCodeEntry__TEMP;

            //  END Temp NOT store in Settings

            ArdiaRegistrationCodeEntry entry = null;

            {
                if (!Settings.Default.ArdiaRegistrationCodeEntries.TryGetValue(_ardiaServerURL_BaseURL,
                        out entry))
                {
                    entry = null;
                }
            }
            
            return entry;
        }

        private void SetSavedArdiaRegistrationData(ArdiaRegistrationCodeEntry entry)
        {
            //  Temp to NOT store in Settings

            // _ardia_ArdiaRegistrationCodeEntry__TEMP = entry;

            //  END Temp NOT store in Settings

            Settings.Default.ArdiaRegistrationCodeEntries[_ardiaServerURL_BaseURL] = entry;
        }

        private Func<HttpClient> GetFactory()
        {
            return () =>
            {
                var ardiaRegistrationEntry = GetSavedArdiaRegistrationEntry();

                if (ardiaRegistrationEntry == null)
                {
                    throw new Exception(@"GetFactory(); if (ardiaRegistrationEntry == null) ");
                }

                var cookieURI_String = _ardiaServerURL_Transport + @"api." + _ardiaServerURL_BaseURL;

                var cookieContainer = new CookieContainer();
                var handler = new HttpClientHandler();
                handler.CookieContainer = cookieContainer;
                var client = new HttpClient(handler);
                client.BaseAddress = new Uri(Account.ServerUrl);
                cookieContainer.Add(new Uri(cookieURI_String), new Cookie(_bffCookie.Name, _bffCookie.Value));
                client.DefaultRequestHeaders.Add(@"Accept", @"application/json");

                client.DefaultRequestHeaders.Add(@"applicationCode", ardiaRegistrationEntry.ClientApplicationCode);

                return client;
            };
        }

        protected override void OnShown(EventArgs e)
        {
            try
            {
                var sessionCookieString = ArdiaAccount.GetSessionCookieString(Account);

                if (sessionCookieString != null && (!sessionCookieString.IsNullOrEmpty()))
                {
                    _bffCookie = new Cookie(@"Bff-Host", sessionCookieString);
                    AuthenticatedHttpClientFactory = GetFactory();

                    // check that cookie is still valid
                    using var client = AuthenticatedHttpClientFactory();
                    var response = client.GetAsync(Account.GetFolderContentsUrl()).Result;
                    try
                    {
                        response.EnsureSuccessStatusCode();
                        DialogResult = DialogResult.OK;
                        return;
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }

                if (string.IsNullOrEmpty(Account.TestingOnly_NotSerialized_Username) ||
                    string.IsNullOrEmpty(Account.TestingOnly_NotSerialized_Password))
                {
                    //  Login via System Default Browser.   Standard way to Login

                    Initialize_LoginIn_SystemDefaultBrowser();
                }
                else
                {   //   Login via Embedded WebView.    Used for Programmatic Login for Testing Only

                    Initialize_LoginIn_WebView();
                }

            }
            catch (Exception exception)
            {
                MessageDlg.ShowWithException(this, AlertsResources.ArdiaLoginDlg_OnShown_Error_connecting_to_Ardia_server, exception);
                DialogResult = DialogResult.OK;
            }
        }

        //////////////////////////////////////////////////////////////////
        ///
        ///   Login via System Default Browser.   Standard way to Login
        ///
        ///
        #region Handlers for login via system default browser

        private async void Initialize_LoginIn_SystemDefaultBrowser()
        {
            {
                var doRegistration = true;

                {
                    var ardiaRegistrationEntry_BeforeRegister = GetSavedArdiaRegistrationEntry();

                    // !!!!!!!!!!!!!!!!

                    if (FORCE_DO_REGISTRATION_WITH_ARDIA_STEP)
                    {
                        ardiaRegistrationEntry_BeforeRegister = null; //  TODO  FAKE
                    }


                    if (ardiaRegistrationEntry_BeforeRegister != null)
                    {
                        var activationStatus = await ValidateActivationStatus(ardiaRegistrationEntry_BeforeRegister);

                        if (activationStatus)
                        {
                            doRegistration = false;
                        }
                    }
                }

                if (doRegistration)
                {
                    wizardPagesMain.SelectedIndex = MAIN_REGISTER_SKYLINE_WIZARD_PAGE_INDEX;


                    _firstTime_ExecuteClientRegistration = false;

                }
                else
                {
                    wizardPagesMain.SelectedIndex = MAIN_LOGIN_WIZARD_PAGE_INDEX;
                }
            }

        }

        private async void StartRegistrationSkylineWithArdia_ButtonClicked()
        {
            wizardPagesRegisterPhases.SelectedIndex = REGISTER_IN_PROGRESS_wizardPagesRegisterPhases_INDEX;
            try
            {
                var registerDevice_UsingSystemDefaultBrowser_ResultEnum_Value = await RegisterDevice_UsingSystemDefaultBrowser();

                if (registerDevice_UsingSystemDefaultBrowser_ResultEnum_Value == RegisterDevice_UsingSystemDefaultBrowser_ResultEnum.FAIL_GENERAL)
                {
                    DialogResult = DialogResult.OK;

                    return;
                }

                if (registerDevice_UsingSystemDefaultBrowser_ResultEnum_Value ==
                    RegisterDevice_UsingSystemDefaultBrowser_ResultEnum.FAIL_NOT_AUTHORIZED)
                {
                    // MessageDlg.Show(this, "Error Registering Skyline Instance in Ardia as Client.");
                    pnlRegisterFailedUserNotAuth.Visible = true;

                    return;
                }

                wizardPagesMain.SelectedIndex = MAIN_REGISTER_SKYLINE_COMPLETE_WIZARD_PAGE_INDEX;
            }
            catch (Exception e)
            {
                MessageDlg.ShowWithException(this,
                    AlertsResources.ArdiaLoginDlg_StartRegistrationSkylineWithArdia_ButtonClicked_Error_registering_this_Skyline_instance_to_Ardia,
                    e); //  TODO   Maybe need something different
                DialogResult = DialogResult.OK;
            }
        }

        private void OpenBrowserWithServerUrl_ToShowAccount()
        {

            // Create a new browser process to open the verification URL in the default system web browser
            var browserProcess = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Account.ServerUrl,
                    UseShellExecute = true,
                    Verb = string.Empty
                }
            };
            // Start the browser process to open the verification URL in the default system web browser
            browserProcess.Start();
        }

        private async void StartLogin_WithArdia_ButtonClicked()
        {
            wizardPagesLoginPhases.SelectedIndex = LOGIN_IN_PROGRESS_wizardPagesLoginPhases_INDEX;

            try
            {
                var bffCookie = await LoginIn_UsingSystemDefaultBrowser();
                if (bffCookie != null)
                {
                    ArdiaAccount.SetSessionCookieString(Account, bffCookie.Value );

                    AuthenticatedHttpClientFactory = GetFactory();
                }

                DialogResult = DialogResult.OK;
            }
            catch (Exception e)
            {
                MessageDlg.ShowWithException(this, AlertsResources.ArdiaLoginDlg_StartLogin_WithArdia_ButtonClicked_Error_logging_in_to_Ardia, e); //  TODO    Maybe need something different

                DialogResult = DialogResult.OK;
            }
        }


        // Validate the client activation status using the client credentials
        private async Task<bool> ValidateActivationStatus(ArdiaRegistrationCodeEntry ardia_ArdiaRegistrationCodeEntry)
        {
            var clientCode = ardia_ArdiaRegistrationCodeEntry.ClientApplicationCode;
            var clientId = ardia_ArdiaRegistrationCodeEntry.ClientId;
            var clientName = ardia_ArdiaRegistrationCodeEntry.ClientName;

            if (string.IsNullOrEmpty(clientCode) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientName))
            {
                return false;
            }

            string errorPrefix = AlertsResources.ArdiaLoginDlg_ValidateActivationStatus_Error_validating_Skyline_registration_in_Ardia_;

            var identityBaseUri = new UriBuilder { Scheme = _ardiaServer_URI.Scheme, Host = $"identity.{_ardiaServerURL_BaseURL}" }.Uri;
            var clientStatusEndpointPath = @"api/v2/Clients/clientStatus";
            var credentialsValidationUri = new UriBuilder(identityBaseUri)
            {
                Path = clientStatusEndpointPath,
                Query = $"code={clientCode}&clientId={clientId}&clientName={clientName}"
            }.Uri.ToString();
            using (var httpClient = new HttpClient())
            using (var clientCredentialsRequest = new HttpRequestMessage(HttpMethod.Get, credentialsValidationUri))
            {
                using (var clientCredentialsResponse = await httpClient.SendAsync(clientCredentialsRequest))
                {
                    if (!(clientCredentialsResponse.StatusCode == HttpStatusCode.OK ||
                          clientCredentialsResponse.StatusCode == HttpStatusCode.NotFound))
                    {
                        //  "NotFound (404)" returned when ApplicationCode is not found so process that status code in the next step

                        //  For Check Device Registration, this is the first server access done so if the URL is invalid (404) or network problems this will be the error.

                        var errorMessage =
                            string.Format(
                                AlertsResources.ArdiaLoginDlg_ValidateActivationStatus__3__Failed_to_connect_to_URL__0___Server_URL___1___Error_message___2_,
                                credentialsValidationUri, Account.ServerUrl, clientCredentialsResponse, errorPrefix);
                        MessageDlg.Show(this, errorMessage);

                        clientCredentialsResponse.EnsureSuccessStatusCode();
                    }

                    try
                    {
                        var content = await clientCredentialsResponse.Content.ReadAsStringAsync();

                        //  Parse the content as JSON and validate content

                        try
                        {
                            var clientStatusResultObject = JsonConvert.DeserializeObject<ClientStatusWebserviceResultDto>(content);

                            if (clientStatusResultObject == null)
                            {
                                if (clientCredentialsResponse.StatusCode == HttpStatusCode.OK)
                                {
                                    //  200 HTTP Status code for OK and NOT able to parse or process the webservice response so present this error
                                    var errorMessage =
                                        string.Format(
                                            AlertsResources.ArdiaLoginDlg_ValidateActivationStatus__2__Failed_to_process_result_from_webservice_call_to_URL__0___Server_URL___1_,
                                            credentialsValidationUri, Account.ServerUrl, errorPrefix);
                                    MessageDlg.Show(this, errorMessage);
                                }
                                else if (clientCredentialsResponse.StatusCode == HttpStatusCode.NotFound)
                                {
                                    //  404 HTTP Status code for NotFound and NOT able to parse or process the webservice response so present this error
                                    var errorMessage =
                                        string.Format(
                                            AlertsResources.ArdiaLoginDlg_ValidateActivationStatus__3__Failed_to_process_webservice_response___Probably_failed_to_connect_to_URL__0___Server_URL___1___Error_message___2_,
                                            credentialsValidationUri, Account.ServerUrl, clientCredentialsResponse, errorPrefix);
                                    MessageDlg.Show(this, errorMessage);
                                }
                                else
                                {
                                    var errorMessage =
                                        string.Format(
                                            AlertsResources.ArdiaLoginDlg_ValidateActivationStatus__2__Failed_to_process_webservice_response__URL__0___Server_URL___1_,
                                            credentialsValidationUri, Account.ServerUrl, errorPrefix);
                                    MessageDlg.Show(this, errorMessage);
                                }
                                return false;
                            }

                            if (IsClientRegistrationActivated_SingleField(clientStatusResultObject.clientCodeStatus) &&
                                IsClientRegistrationActivated_SingleField(clientStatusResultObject.clientIdStatus) &&
                                IsClientRegistrationActivated_SingleField(clientStatusResultObject.clientNameStatus))
                            {
                                return true; // ApplicationCode is valid
                            }

                            return false;

                        }
                        catch (Exception e)
                        {
                            if (clientCredentialsResponse.StatusCode == HttpStatusCode.OK)
                            {
                                //  200 HTTP Status code for OK and NOT able to parse or process the webservice response so present this error
                                var errorMessage =
                                    string.Format(
                                        AlertsResources.ArdiaLoginDlg_ValidateActivationStatus__2__Failed_to_process_result_from_webservice_call_to_URL__0___Server_URL___1_,
                                        credentialsValidationUri, Account.ServerUrl, errorPrefix);
                                MessageDlg.ShowWithException(this, errorMessage, e);
                            }
                            else if (clientCredentialsResponse.StatusCode == HttpStatusCode.NotFound)
                            {
                                //  404 HTTP Status code for NotFound and NOT able to parse or process the webservice response so present this error
                                var errorMessage =
                                    string.Format(
                                        AlertsResources.ArdiaLoginDlg_ValidateActivationStatus__3__Failed_to_process_webservice_response___Probably_failed_to_connect_to_URL__0___Server_URL___1___Error_message___2_,
                                        credentialsValidationUri, Account.ServerUrl, clientCredentialsResponse, errorPrefix);
                                MessageDlg.ShowWithException(this, errorMessage, e);
                            }
                            else
                            {
                                var errorMessage =
                                    string.Format(
                                        AlertsResources.ArdiaLoginDlg_ValidateActivationStatus__2__Failed_to_process_webservice_response__URL__0___Server_URL___1_,
                                        credentialsValidationUri, Account.ServerUrl, errorPrefix);
                                MessageDlg.ShowWithException(this, errorMessage, e);
                            }
                            return false;
                        }

                    }
                    catch (Exception)
                    {
                        if (!clientCredentialsResponse.IsSuccessStatusCode)
                        {
                            //  For Check Device Registration, this is the first server access done so if the URL is invalid (404) or network problems this will be the error.

                            var errorMessage =
                                string.Format(
                                    AlertsResources.ArdiaLoginDlg_ValidateActivationStatus__3__Failed_to_connect_to_URL__0___Server_URL___1___Error_message___2_,
                                    credentialsValidationUri, Account.ServerUrl, clientCredentialsResponse, errorPrefix);
                            MessageDlg.Show(this, errorMessage);

                            clientCredentialsResponse.EnsureSuccessStatusCode();
                        }

                        throw;
                    }
                }
            }
        }

        // Check if the given status string contains the "has been activated" message
        private bool IsClientRegistrationActivated_SingleField(string status)
        {
            if (status == null)
            {
                return false;
            }
            return status.Contains(@"has been activated");
        }


        // Register the device with the Ardia platform using the default system browser to display the registration page
        private async Task<RegisterDevice_UsingSystemDefaultBrowser_ResultEnum> RegisterDevice_UsingSystemDefaultBrowser()
        {
            var authority = @$"https://identity.{_ardiaServerURL_BaseURL}";

            string errorPrefix = AlertsResources.ArdiaLoginDlg_RegisterDevice_UsingSystemDefaultBrowser_Error_registering_Skyline_in_Ardia;

            try
            {
                var discoveryCache = new DiscoveryCache(authority);
                var discoveryDocument = await discoveryCache.GetAsync();
                if (discoveryDocument.IsError)
                {
                    //  For Register Device, this is the first server access done so if the URL is invalid (404) or network problems this will be the error.

                    var errorMessage =
                        string.Format(
                            AlertsResources.ArdiaLoginDlg_RegisterDevice_UsingSystemDefaultBrowser__3__Failed_to_connect_to_URL__0___Server_URL___1___Error_message___2_,
                            authority, Account.ServerUrl, discoveryDocument.Error, errorPrefix);
                    MessageDlg.Show(this, errorMessage);
                    
                    return RegisterDevice_UsingSystemDefaultBrowser_ResultEnum.FAIL_GENERAL;

                    //  TODO  How to close dialog here with failure instead.

                    // throw new Exception(discoveryDocument.Error);
                }

                var deviceAuthorizationResponse = await RequestDeviceAuthorizationAsync();
                var verificationUri = @$"{deviceAuthorizationResponse.VerificationUriComplete}&showContent=false";

                // Create a new browser process to open the verification URL in the default system web browser
                _browserProcess = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = verificationUri,
                        UseShellExecute = true,
                        Verb = string.Empty
                    }
                };
                // Start the browser process to open the verification URL in the default system web browser
                _browserProcess.Start();

                var userTokenResponse = await RequestUserTokenAsync(deviceAuthorizationResponse);

                if (userTokenResponse == null)
                {
                    //  Returns null if form has been closed

                    return RegisterDevice_UsingSystemDefaultBrowser_ResultEnum.FAIL_GENERAL;
                }

                var clientCode = await CreateNewClient(userTokenResponse);
                var ardiaRegistrationEntry = await ActivateClient(clientCode, userTokenResponse);

                SetSavedArdiaRegistrationData(ardiaRegistrationEntry);
            }
            catch (HttpRequestException e)
            {
                if (e.Message.Contains(@"403"))
                {
                    return RegisterDevice_UsingSystemDefaultBrowser_ResultEnum.FAIL_NOT_AUTHORIZED;
                }
                else
                {
                    MessageDlg.ShowWithException(this, errorPrefix, e);

                    return RegisterDevice_UsingSystemDefaultBrowser_ResultEnum.FAIL_GENERAL;
                }
            }
            catch (Exception e)
            {
                var errorMessage =
                    string.Format(
                        AlertsResources.ArdiaLoginDlg_RegisterDevice_UsingSystemDefaultBrowser__2__Failed_to_connect_to_URL__0___Server_URL___1_,
                        authority, Account.ServerUrl, errorPrefix);
                MessageDlg.ShowWithException(this, errorMessage, e);

                return RegisterDevice_UsingSystemDefaultBrowser_ResultEnum.FAIL_GENERAL;
            }

            return RegisterDevice_UsingSystemDefaultBrowser_ResultEnum.SUCCESS;
        }

        // Methods required to log into the Ardia platform and retrieve the Bff-Host cookie using the default system web browser
        
        // Method used to log the user into the Ardia platform using the default system web browser
        public async Task<Cookie> LoginIn_UsingSystemDefaultBrowser()
        {
            try
            {
                // Initialize the browser cookie to null
                _bffCookie = null;
                // Initiate the browser login process
                InitiateBrowserLoginProcess();

                // Wait for the user to login and the Bff-Host cookie to be retrieved
                while (_bffCookie == null || _bffCookie.Value == string.Empty)
                {
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        return null;
                    }

                    try
                    {
                        await Task.Delay(100, _cancellationToken);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
                try
                {
                    await Task.Delay(300, _cancellationToken);  // Await so final browser response from local _httpListener is sent to the browser
                }
                catch (Exception)
                {
                    // ignored
                }

                return _bffCookie;
            }
            finally
            {
                // Stop the HttpListener
                Stop_HTTPListener();
            }
        }

        // Method used to initiate the browser login process
        private void InitiateBrowserLoginProcess()
        {
            int portNumber_StartNumber = 5001;
            int portAddition_Max = 2500;

            int portAddition = 0;

            string returnUrl = null;

            while (true)
            {
                try
                {
                    Stop_HTTPListener();

                    _httpListener_Port = portNumber_StartNumber + portAddition;

                    // Get the return URL for the http listener
                    returnUrl = GetBrowserReturnUrl();

                    // Create a new HttpListener instance and start listening for incoming requests on the redirect URL
                    _httpListener = new HttpListener();
                    _httpListener.Prefixes.Add(returnUrl);
                    _httpListener.Start();
                    // Set the timeout values for the HttpListener
                    _httpListener.TimeoutManager.IdleConnection = TimeSpan.FromMinutes(2.5);
                    _httpListener.TimeoutManager.HeaderWait = TimeSpan.FromMinutes(2.5);

                    break; //  Exit Loop since successful create of listener
                }
                catch (Exception)
                {
                    if (portAddition >= portAddition_Max)
                    {
                        throw;
                    }

                    portAddition++;
                }
            }

            // Get the login URL for the browser login process
            var loginUrl = GetBrowserLoginUrl(returnUrl);

            // Create a new browser process to open the login URL in the default system web browser
            _browserProcess = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = loginUrl,
                    UseShellExecute = true,
                    Verb = string.Empty
                }
            };
            // Start the browser process to open the login URL in the default system web browser
            _browserProcess.Start();

            // Begin the HttpListener request callback
            var result = _httpListener.BeginGetContext(RequestCallback, _httpListener);
        }

        // Method used to handle the request callback from the HttpListener when the user logs in and retrieve the PAT token
        private async void RequestCallback(IAsyncResult asyncResult)
        {
            try
            {
                if (_cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // Get the HttpListener instance and the context from the async result
                var httpListener = (HttpListener)asyncResult.AsyncState;
                var context = httpListener.EndGetContext(asyncResult);
                var request = context.Request;
                var response = context.Response;

                var responseString = GetBrowserLoginResponseString();
                var buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                var responseOutput = response.OutputStream;
                // Write the response to the output stream
                await responseOutput.WriteAsync(buffer, 0, buffer.Length).ContinueWith((task) => {
                    responseOutput.Close();
                });

                if (_cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var patTokenUrl = GetBrowserPatTokenUrl();
                // Create a new browser process to open the PAT token URL in the default system web browser
                _browserProcess = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = patTokenUrl,
                        UseShellExecute = true,
                        Verb = string.Empty
                    }
                };
                // Start the browser process to open the PAT token URL in the default system web browser
                _browserProcess.Start();

                // Begin the HttpListener PAT request callback
                _httpListener.BeginGetContext(PatRequestCallback, _httpListener);
            }
            catch (Exception e)
            {
                Program.ReportException(e);
            }
        }

        // Method used to handle the PAT request callback from the HttpListener when the user logs in and retrieve the session cookie
        private async void PatRequestCallback(IAsyncResult asyncResult)
        {
            try
            {
                if (_cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // Get the HttpListener instance and the context from the async result
                var httpListener = (HttpListener)asyncResult.AsyncState;
                var context = httpListener.EndGetContext(asyncResult);
                // Get the PAT token from the context request
                var patToken = context.Request.QueryString[@"pat"];
                var sessionCookieUrl = GetBrowserSessionCookieUrl();
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add(@"Authorization", @$"Bearer {patToken}");
                // Get the session cookie from the Session Management API
                var httpClientResponse = await httpClient.GetAsync(sessionCookieUrl);

                if (_cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // Get the session cookie value from the response
                var sessionCookie = await httpClientResponse.Content.ReadAsStringAsync();
                var domain = @$"api.{_ardiaServerURL_BaseURL}";
                // Create a new cookie object to store the Bff-Host cookie value
                _bffCookie = new Cookie
                {
                    Name = "Bff-Host",
                    Value = JsonConvert.DeserializeObject<string>(sessionCookie),
                    Domain = domain,
                    Path = "/",
                    Secure = true,
                    HttpOnly = true
                };

                // Send a response to the browser to close the window
                var listenerResponse = context.Response;
                var responseString = GetBrowserLoginResponseString();
                var buffer = Encoding.UTF8.GetBytes(responseString);
                listenerResponse.ContentLength64 = buffer.Length;
                var responseOutput = listenerResponse.OutputStream;
                // Write the response to the output stream
                await responseOutput.WriteAsync(buffer, 0, buffer.Length).ContinueWith((task) =>
                {
                    responseOutput.Close();
                });
            }
            catch (Exception e)
            {
                Program.ReportException(e);
            }
        }

        // Method used to get the login URL for the browser login process
        private string GetBrowserLoginUrl(string returnUri)
        {
            var ardiaRegistrationEntry = GetSavedArdiaRegistrationEntry();

            var returnUriEncoded = WebUtility.UrlEncode(returnUri);

            var apiBaseUri = new UriBuilder { Scheme = _ardiaServer_URI.Scheme, Host = $"api.{_ardiaServerURL_BaseURL}" }.Uri;
            var loginEndpointPath = @"session-management/bff/login";
            var applicationCode = ardiaRegistrationEntry.ClientApplicationCode;
            var applicationName = ardiaRegistrationEntry.ClientName;
            const bool useArdiaDPoP = false;

            // Construct the login URL
            var loginUrl = new UriBuilder(apiBaseUri)
            {
                Path = loginEndpointPath,
                Query = $"application={applicationName}&returnUrl={returnUriEncoded}&ApplicationCode={applicationCode}&UseArdiaDPoP={useArdiaDPoP}"
            }.Uri.ToString();

            return loginUrl;
        }

        // Method used to get the return and redirect URLs for the browser login process
        private string GetBrowserReturnUrl()
        {
            var uriBuilder = new UriBuilder()
            {
                Scheme = Uri.UriSchemeHttp,
                Host = "localhost",
                Port = _httpListener_Port,
                Path = "/signin-oidc/"
            };
            var uri = uriBuilder.Uri.ToString();

            return uri;
        }

        // Method used to get the PAT token URL for the browser login process
        private string GetBrowserPatTokenUrl()
        {
            var ardiaRegistrationEntry = GetSavedArdiaRegistrationEntry();

            var returnUri = GetBrowserReturnUrl();

            var apiBaseUri = new UriBuilder { Scheme = _ardiaServer_URI.Scheme, Host = $"api.{_ardiaServerURL_BaseURL}" }.Uri;
            var patTokenEndpoint = @"session-management/bff/identity-server/tokenApi/pat";
            var applicationCode = ardiaRegistrationEntry.ClientApplicationCode;

            // Construct the PAT token URL
            var patTokenUrl = new UriBuilder(apiBaseUri)
            {
                Path = patTokenEndpoint,
                Query = $"applicationCode={applicationCode}&redirectUri={returnUri}"
            }.Uri.ToString();

            return patTokenUrl;
        }

        // Method used to get the session cookie URL for the browser login process
        private string GetBrowserSessionCookieUrl()
        {
            var apiBaseUri = new UriBuilder { Scheme = _ardiaServer_URI.Scheme, Host = $"api.{_ardiaServerURL_BaseURL}" }.Uri;
            var sessionCookieEndpoint = @"session-management/api/v1/SessionManagement/sessioncookie";

            // Construct the session cookie URL
            var sessionCookieUrl = new UriBuilder(apiBaseUri)
            {
                Path = sessionCookieEndpoint
            }.Uri.ToString();

            return sessionCookieUrl;
        }

        // Method used to get the response string for the browser login process
        private string GetBrowserLoginResponseString()
        {
            return string.Format(AlertsResources.ArdiaLoginDlg_GetBrowserLoginResponseString_, Account.ServerUrl);
        }

        #endregion


        /////////////////////////////////////////////////////////////////
        /// 
        ///
        ///   Login via Embedded WebView.    Used for Programmatic Login for Testing Only
        ///
        /// 
        #region Handlers for login via embedded WebView for automated testing

        private async void Initialize_LoginIn_WebView()
        {
            var options =
                new CoreWebView2EnvironmentOptions(
                    @"--disable-web-security --allow-file-access-from-files --allow-file-access");
            var environment = await CoreWebView2Environment.CreateAsync(null, _tempUserDataFolder.DirPath, options);
            // EnsureCoreWebView2Async must be called before any other call
            // to WebView2 and before setting the Source property
            // since these will both cause initialization of the CoreWebView2 property
            // but using a default CoreWebView2Environment rather than your custom one.
            await webView.EnsureCoreWebView2Async(environment);
            webView.CoreWebView2.CookieManager.DeleteAllCookies();

            webView.Focus();

            // Wait for the user to login and the source to change
            //webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            webView.CoreWebView2.NavigationCompleted += CoreWebView2OnNavigationCompleted_AfterEveryNavigate;

            {
                bool hasUsername = Account.TestingOnly_NotSerialized_Username?.Any() ?? false;
                bool hasPassword = Account.TestingOnly_NotSerialized_Password?.Any() ?? false;

                if (hasUsername && hasPassword)
                {
                    //  Yes TestingOnly Username and Password for Programmatic Login for Testing so add event listener for Programmatic Login

                    webView.CoreWebView2.DOMContentLoaded += DOMContentLoaded_CheckForSessionCookie_ProgrammaticLoginForTesting;
                }
            }

            StartAtClientRegistrationIfNeeded_LoginIn_WebView();
        }


        private async void StartAtClientRegistrationIfNeeded_LoginIn_WebView()
        {
            //  Cleanup in case execute this a second time
            webView.CoreWebView2.NavigationCompleted -= CoreWebView2OnNavigationCompleted_AfterNavigateToLoginURL;

            webView.CoreWebView2.NavigationCompleted += CoreWebView2OnNavigationCompleted_AfterNavigateTo_ClientRegistrationPage;

            var ardiaRegistrationEntry_BeforeRegister = GetSavedArdiaRegistrationEntry();

            if (FORCE_DO_REGISTRATION_WITH_ARDIA_STEP)
            {
                ardiaRegistrationEntry_BeforeRegister = null; //  TODO  FAKE
            }

            if (ardiaRegistrationEntry_BeforeRegister == null)
            {
                _firstTime_ExecuteClientRegistration = false;

                try
                {
                    var registerSuccessful = await RegisterDevice_UsingWebView();

                    if (!registerSuccessful)
                    {
                        DialogResult = DialogResult.OK;

                        return;
                    }
                }
                catch (Exception e)
                {
                    MessageDlg.ShowWithException(this, AlertsResources.ArdiaLoginDlg_RegisterDevice_UsingSystemDefaultBrowser_Error_registering_Skyline_in_Ardia, e); //  TODO    Maybe need something different

                    // throw;

                    //  TODO  May be better to close/exit this dialog here

                    // this does not not work here.  Get problems further down the line from "webView = null"


                    webView.CoreWebView2.Environment.BrowserProcessExited += (s, ea) => DialogResult = DialogResult.OK;
                    webView.Dispose();
                    webView = null;

                    return;
                }
            }

            //  Remove Listener for Client Registration page
            webView.CoreWebView2.NavigationCompleted -= CoreWebView2OnNavigationCompleted_AfterNavigateTo_ClientRegistrationPage;  // Remove Listener

            // !!!!!!!!!!!!!!!!

            var ardiaRegistrationEntry_AfterRegister = GetSavedArdiaRegistrationEntry();

            if (ardiaRegistrationEntry_AfterRegister == null)
            {
                throw new Exception(@"ardiaRegistrationEntry_AfterRegister == null");
            }

            //  Start of User Login

            Reset_ProgrammaticLoginFlags();

            var ardiaServer_BaseUrl = _ardiaServerURL_BaseURL;

            var applicationCode_AfterRegister = ardiaRegistrationEntry_AfterRegister.ClientApplicationCode;

            // Navigate to the login page
            var loginUrl = @$"{_ardiaServerURL_Transport}api.{ardiaServer_BaseUrl}/session-management/bff/login?applicationcode={applicationCode_AfterRegister}&returnUrl={_ardiaServerURL_Transport}{ardiaServer_BaseUrl}/";
            
            //  NOTE:  Opening the Login URL with invalid "applicationcode" results in 401 HTTP status code along with returned contents of:  "Unknown Client. Please register/activate the client"

            webView.CoreWebView2.NavigationCompleted += CoreWebView2OnNavigationCompleted_AfterNavigateToLoginURL;

            webView.CoreWebView2.Navigate(loginUrl);

            //  When Application Code is invalid, the Webview at 'login' URL shows "Unknown Client. Please register/activate the client"
        }

        private void CoreWebView2OnNavigationCompleted_AfterNavigateTo_ClientRegistrationPage(object sender, CoreWebView2NavigationCompletedEventArgs eventArgs)
        {

            if (!eventArgs.IsSuccess)
            {
                var currentURLofWebview = webView.Source.AbsolutePath;
                var errorMessage = string.Format(
                    AlertsResources
                        .ArdiaLoginDlg_CoreWebView2OnNavigationCompleted_AfterNavigateTo_ClientRegistrationPage_Load_Client_Registration_page_failed_with_HTTP_status_code__0___Page_URL___1_,
                    eventArgs.HttpStatusCode, currentURLofWebview);

                if (eventArgs.HttpStatusCode == 404 || eventArgs.HttpStatusCode == 0)
                {
                    MessageDlg.Show(this, errorMessage);

                    //  404 may result in something different being triggered
                }
                else
                {
                    MessageDlg.Show(this, errorMessage);
                }

                // TODO  Not sure what to do here

                //  Exception throws does NOT appear to do anything.
            }
        }

        private void CoreWebView2OnNavigationCompleted_AfterNavigateToLoginURL(object sender, CoreWebView2NavigationCompletedEventArgs eventArgs)
        {
            if (!eventArgs.IsSuccess)
            {
                var currentURLofWebview = webView.Source.AbsolutePath;

                if (eventArgs.HttpStatusCode == 401)
                {
                    //  Page load for Login failed with HTTP status code 401.  Assumed due to invalid ApplicationCode (Registration Code)

                    if (_firstTime_ExecuteClientRegistration)
                    {
                        //  Client Registration has NOT been executed yet to get new value

                        _firstTime_ExecuteClientRegistration = false;

                        //  Assume the Registration Code (ApplicationCode) is invalid and remove it and go through client registration to get a new one

                        SetSavedArdiaRegistrationData(null);


                        //  Start over at Client Registration if needed
                        StartAtClientRegistrationIfNeeded_LoginIn_WebView();

                        return; // Exit since event handled
                    }
                    else
                    {
                        //  NOT First Time executing Registration.  Show Error message

                        var errorMessage = string.Format(AlertsResources.ArdiaLoginDlg_CoreWebView2OnNavigationCompleted_AfterNavigateToLoginURL_Load_Login_page_failed_with_HTTP_status_code__0__Login_Page_URL___1_,
                            eventArgs.HttpStatusCode, currentURLofWebview);
                        MessageDlg.Show(webView, errorMessage);
                    }
                }
                else if (eventArgs.HttpStatusCode == 404 || eventArgs.HttpStatusCode == 0)
                {
                    var errorMessage = string.Format(AlertsResources.ArdiaLoginDlg_CoreWebView2OnNavigationCompleted_AfterNavigateToLoginURL_Load_Login_page_failed_with_HTTP_status_code__0___Login_Page_URL___1_, eventArgs.HttpStatusCode, currentURLofWebview);
                    MessageDlg.Show(this, errorMessage);
                }
                else
                {
                    var errorMessage = string.Format(AlertsResources.ArdiaLoginDlg_CoreWebView2OnNavigationCompleted_AfterNavigateToLoginURL_Load_Login_page_failed_with_HTTP_status_code__0___Login_Page_URL___1_, eventArgs.HttpStatusCode, currentURLofWebview);
                    MessageDlg.Show(this, errorMessage);
                }

                DialogResult = DialogResult.OK;
            }
        }

        private async Task<bool> RegisterDevice_UsingWebView()
        {
            if (webView == null || webView.CoreWebView2 == null)
                return false;

            var authority = @$"{_ardiaServerURL_Transport}identity.{_ardiaServerURL_BaseURL}";
            var errorPrefix = AlertsResources.ArdiaLoginDlg_RegisterDevice_UsingSystemDefaultBrowser_Error_registering_Skyline_in_Ardia;

            try
            {
                var discoveryCache = new DiscoveryCache(authority);
                var discoveryDocument = await discoveryCache.GetAsync();
                if (discoveryDocument.IsError)
                {
                    //  For Register Device, this is the first server access done so if the URL is invalid (404) or network problems this will be the error.
            
                    var errorMessage =
                        string.Format(
                            AlertsResources.ArdiaLoginDlg_RegisterDevice_UsingSystemDefaultBrowser__3__Failed_to_connect_to_URL__0___Server_URL___1___Error_message___2_,
                            authority, Account.ServerUrl, discoveryDocument.Error, errorPrefix);
                    MessageDlg.Show(this, errorMessage);
            
                    DialogResult = DialogResult.OK;
            
                    return false;
                }
            
                var deviceAuthorizationResponse = await RequestDeviceAuthorizationAsync();
                var verificationUri = @$"{deviceAuthorizationResponse.VerificationUriComplete}&showContent=false";
                webView.CoreWebView2.Navigate(verificationUri);
                var userTokenResponse = await RequestUserTokenAsync(deviceAuthorizationResponse);

                if (userTokenResponse == null)
                {
                    return false;  //  Returns null if form has been closed
                }

                var clientCode = await CreateNewClient(userTokenResponse);
                var ardiaRegistrationEntry = await ActivateClient(clientCode, userTokenResponse);

                SetSavedArdiaRegistrationData(ardiaRegistrationEntry);

                await CheckForBffHostCookie();
            }
            catch (HttpRequestException e)
            {
                if (e.Message.Contains(@"403"))
                {
                    var errorMessage =
                        string.Format(
                            AlertsResources.ArdiaLoginDlg_RegisterDevice_UsingSystemDefaultBrowser__3__Failed_to_connect_to_URL__0___Server_URL___1___Error_message___2_,
                            authority, Account.ServerUrl, e.Message, errorPrefix);
                    MessageDlg.Show(this, errorMessage);
                }
                else
                {
                    MessageDlg.ShowWithException(this, AlertsResources.ArdiaLoginDlg_RegisterDevice_UsingSystemDefaultBrowser_Error_registering_Skyline_in_Ardia, e);
                }
            
                return false;
            }
            catch (Exception e)
            {
                var errorMessage =
                    string.Format(
                        AlertsResources.ArdiaLoginDlg_RegisterDevice_UsingSystemDefaultBrowser__2__Failed_to_connect_to_URL__0___Server_URL___1_,
                        authority, Account.ServerUrl, errorPrefix);
                MessageDlg.ShowWithException(this, errorMessage, e);
            
                return false;
            }

            return true;
        }
        #endregion

        ///////////////////////////////////////////////////////
        ///
        /// Common Code both 


        // This method calls itself when response.Error is AuthorizationPending or SlowDown
        // Request a user token from the Ardia platform using the device code obtained from the device authorization response
        private async Task<TokenResponse> RequestUserTokenAsync(DeviceAuthorizationResponse deviceAuthorizationResponse)
        {
            using (var httpClient = new HttpClient())
            {
                //  Hard coded for initial connection to get device registration
                var _ardiaDeviceClientId = @"ardia.device.client.registration";

                var tokenEndpoint = @$"{_ardiaServerURL_Transport}identity.{_ardiaServerURL_BaseURL}/connect/token";
                var response = await httpClient.RequestDeviceTokenAsync(new DeviceTokenRequest
                {
                    Address = tokenEndpoint,
                    ClientId = _ardiaDeviceClientId,
                    DeviceCode = deviceAuthorizationResponse.DeviceCode
                });

                if (!response.IsError)
                {
                    return response;
                }
                // Handle slow down and authorization pending errors
                if (response.Error == OidcConstants.TokenErrors.AuthorizationPending ||
                    response.Error == OidcConstants.TokenErrors.SlowDown)
                {
                    if (_cancellationToken.IsCancellationRequested) // this.FormClosing called so exit.  Caller updated to handle null
                    {
                        return null;
                    }
                    // Wait for the interval and try again
                    
                    try
                    {
                        await Task.Delay(deviceAuthorizationResponse.Interval * 1000, _cancellationToken);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }

                    if (_cancellationToken.IsCancellationRequested) // this.FormClosing called so exit.  Caller updated to handle null
                    {
                        return null;
                    }

                    return await RequestUserTokenAsync(deviceAuthorizationResponse);
                }
                else
                {
                    MessageDlg.Show(this, AlertsResources.ArdiaLoginDlg_RegisterDevice_UsingSystemDefaultBrowser_Error_registering_Skyline_in_Ardia);
                    throw new Exception(response.Error);  //  TODO    Need something different here
                }
            }
        }

        // Request device authorization from the Ardia platform using the device authorization endpoint and the device client credentials
        private async Task<DeviceAuthorizationResponse> RequestDeviceAuthorizationAsync()
        {
            //  Hard coded for initial connection to get device registration
            var _ardiaDeviceClientId = @"ardia.device.client.registration";
            var deviceAuthorizationEndpoint = @$"{_ardiaServerURL_Transport}identity.{_ardiaServerURL_BaseURL}/connect/deviceauthorization";

            try
            {
                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.RequestDeviceAuthorizationAsync(new DeviceAuthorizationRequest
                    {
                        Address = deviceAuthorizationEndpoint,
                        ClientId = _ardiaDeviceClientId,
                        Scope = @"openid profile Ardia_Client_Registration"
                    });
                    return response;
                }
            // StoreClientCredentials(identityClient);
            }
            catch (HttpRequestException e)
            {
                if (e.Message.Contains(@"403"))
                {
                    var errorMessage =
                        string.Format(
                            AlertsResources.ArdiaLoginDlg_RegisterDevice_UsingSystemDefaultBrowser__3__Failed_to_connect_to_URL__0___Server_URL___1___Error_message___2_,
                            deviceAuthorizationEndpoint, Account.ServerUrl, e.Message, AlertsResources.ArdiaLoginDlg_StartRegistrationSkylineWithArdia_ButtonClicked_Error_registering_this_Skyline_instance_to_Ardia);
                    MessageDlg.Show(this, errorMessage);
                }
                else
                {
                    MessageDlg.ShowWithException(this, AlertsResources.ArdiaLoginDlg_RegisterDevice_UsingSystemDefaultBrowser_Error_registering_Skyline_in_Ardia, e);
                }

                throw;
            }
            catch (Exception e)
            {
                MessageDlg.ShowWithException(this, AlertsResources.ArdiaLoginDlg_RegisterDevice_UsingSystemDefaultBrowser_Error_registering_Skyline_in_Ardia, e);

                throw;
            }
        }

        // Create a new client in the Ardia platform using the user access token obtained from the token response
        private async Task<string> CreateNewClient(TokenResponse userTokenResponse)
        {
            var newClientUri = @$"{_ardiaServerURL_Transport}api.{_ardiaServerURL_BaseURL}/identity-registration/api/v2/Clients";
            var accessToken = userTokenResponse.AccessToken;
            var clientName = @$"SkylineSampleApp{Guid.NewGuid().ToString()}";
            var newClient = new NewClient() { ClientName = clientName };
            var newClientData = JsonConvert.SerializeObject(newClient);

            using (var httpClient = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Post, newClientUri))
            using (var content = new StringContent(newClientData, Encoding.UTF8, @"application/json"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(@"Bearer", accessToken);
                request.Content = content;
                using (var response = await httpClient.SendAsync(request))
                {
                    response.EnsureSuccessStatusCode();
                    var responseData = await response.Content.ReadAsStringAsync();
                    var clientCode = JsonConvert.DeserializeObject<ClientCodeDto>(responseData);
                    return clientCode.Code;
                }
            }
        }

        // Activate the client in the Ardia platform using the client code obtained from the client creation response
        private async Task<ArdiaRegistrationCodeEntry> ActivateClient(string clientCode, TokenResponse userTokenResponse)
        {
            var skylineVersion = Install.Version;

            var activateClientUri = @$"{_ardiaServerURL_Transport}api.{_ardiaServerURL_BaseURL}/identity-registration/api/v2/Clients/activate";
            var accessToken = userTokenResponse.AccessToken;
            // Define the client activation input
            var clientActivationInput = new ClientActivationInput()
            {
                ApplicationCode = clientCode,
                AppName = @"SkylineApp",
                Version = skylineVersion,
                PCName = Environment.MachineName,
                RedirectUris = new List<string>() { @"http://localhost:5001/signin-oidc" },
                PostLogoutRedirectUris = new List<string>() { @"http://localhost:5001/signout-oidc" },
                ClientUri = @"http://localhost:5001",
                Scopes = new List<string>() { @"openid", @"profile", @"offline_access", @"DataServerApi", @"IdentityServerApi" },
                GrantTypes = new List<string>() { @"authorization_code", @"urn:ietf:params:oauth:grant-type:token-exchange" },
            };
            var clientActivationInputData = JsonConvert.SerializeObject(clientActivationInput);

            using var httpClient = new HttpClient();
            using var clientActivationRequest = new HttpRequestMessage(HttpMethod.Post, activateClientUri);
            using var clientActivationContent = new StringContent(clientActivationInputData, Encoding.UTF8, @"application/json");
            clientActivationRequest.Headers.Authorization = new AuthenticationHeaderValue(@"Bearer", accessToken);
            clientActivationRequest.Content = clientActivationContent;
            using var clientActivationResponse = await httpClient.SendAsync(clientActivationRequest);
            clientActivationResponse.EnsureSuccessStatusCode();
            var clientActivationData = await clientActivationResponse.Content.ReadAsStringAsync();
            var clientActivation = JsonConvert.DeserializeObject<ClientApplicationResponse>(clientActivationData);
            var clientCredentialsUri =
                @$"{_ardiaServerURL_Transport}api.{_ardiaServerURL_BaseURL}/identity-registration/api/v2/Clients/credentials?code={clientActivation.RegistrationCode}";
            using var clientCredentialsRequest = new HttpRequestMessage(HttpMethod.Get, clientCredentialsUri);
            clientCredentialsRequest.Headers.Authorization = new AuthenticationHeaderValue(@"Bearer", accessToken);
            using var clientCredentialsResponse = await httpClient.SendAsync(clientCredentialsRequest);
            clientCredentialsResponse.EnsureSuccessStatusCode();
            var clientCredentialsResponseData = await clientCredentialsResponse.Content.ReadAsStringAsync();
            var clientCredentials = JsonConvert.DeserializeObject<ClientCredentialsResponse>(clientCredentialsResponseData);

            return new ArdiaRegistrationCodeEntry
            {
                ClientId = clientCredentials.ClientId,
                ClientSecret = clientCredentials.ClientSecret,
                ClientApplicationCode = clientCredentials.ApplicationCode,
                ClientName = clientCredentials.Name,
            };
        }


        public class CoreWebView2ExceptionWrapper : Exception
        {
            private readonly CoreWebView2ScriptException _ex;

            public CoreWebView2ExceptionWrapper(CoreWebView2ScriptException ex) : base(ex.Message)
            {
                _ex = ex;
            }

            public override string ToString()
            {
                return _ex.ToJson;
            }
        }

        private async Task<string> ExecuteScriptAsync(string script, bool showExceptions = true)
        {
            var result = await webView.CoreWebView2.ExecuteScriptWithResultAsync(@"(function() { return " + script + @"; })()");
            if (!result.Succeeded)
            {
                if (showExceptions)
                {
                    var wrappedException = new CoreWebView2ExceptionWrapper(result.Exception);
                    MessageDlg.ShowWithException(this, AlertsResources.ArdiaLoginDlg_Error_interacting_with_web_page, wrappedException);
                }

                return null;
            }

            result.TryGetResultAsString(out var resultStr, out int isString);
            if (isString == 0)
                resultStr = result.ResultAsJson;
            return resultStr;
        }

        private async Task<string> ExecuteScriptAsyncUntil(string script, Func<string, Task<bool>> predicate, int numTries = 10, int delayBetweenTries = 500)
        {
            for (int i = 0; i < numTries; ++i)
            {
                var result = await ExecuteScriptAsync(script, false);
                if (await predicate(result))
                    return result;

                if (_cancellationToken.IsCancellationRequested) // this.FormClosing called so exit.
                {
                    return null;
                }

                try
                {
                    await Task.Delay(delayBetweenTries, _cancellationToken);
                }
                catch (Exception)
                {
                    // ignored
                }

                if (_cancellationToken.IsCancellationRequested) // this.FormClosing called so exit.
                {
                    return null;
                }
            }

            return null;
        }

        private async void CoreWebView2OnNavigationCompleted_AfterEveryNavigate(object sender, CoreWebView2NavigationCompletedEventArgs eventArgs)
        {
            //  Called after every navigation

            //   Available for debugging for now

            // var location = webView.Source.AbsolutePath;

            if (!eventArgs.IsSuccess)
            {
                if (eventArgs.HttpStatusCode == 404)
                {
                    //Trace.WriteLine("Load Client Registration page failed with HTTP status code 404: page not found at URL. AbsolutePath: " + webView.Source.AbsolutePath);

                    //  404 may result in something different being triggered

                    //  TODO    Probably want to direct UI to register client if that was NOT just done.  If the Registration Code (ApplicationCode) was just received there is a problem with it.

                }
                else
                {
                    //Trace.WriteLine(string.Format("Load Client Registration page failed with HTTP status code {0}. AbsolutePath: {1}", eventArgs.HttpStatusCode, webView.Source.AbsolutePath));
                }

                // TODO  Not sure what to do here

                //  Add exception here to make sure we report that navigation is invalid

                if (Program.FunctionalTest)
                {
                    throw new Exception(@"ArdiaLoginDlg: Webview Navigation failed with HTTP status code " + eventArgs.HttpStatusCode + @", AbsolutePath: " + webView.Source.AbsolutePath );
                }
                else
                {
                    //  Show specific MessageDlg in other places specific to loading Registration or Login URLs
                }

                //  Exception throws does NOT appear to do anything.

                // throw new Exception("Load Client Registraton page failed. if (!eventArgs.IsSuccess) ");
            }

            await CheckForBffHostCookie();
        }


        private async Task CheckForBffHostCookie()
        {
            // Get the list of cookies from the webview
            var cookies = await webView.CoreWebView2.CookieManager.GetCookiesAsync(Account.ServerUrl);

            // Find the Bff-Host cookie
            var bffCookie = cookies.FirstOrDefault(x => x.Name == @"Bff-Host");
            if (bffCookie != null)
            {
                _bffCookie = bffCookie.ToSystemNetCookie();

                ArdiaAccount.SetSessionCookieString(Account, _bffCookie.Value);

                AuthenticatedHttpClientFactory = GetFactory();

                webView.CoreWebView2.Environment.BrowserProcessExited += (s, ea) => DialogResult = DialogResult.OK;
                webView.Dispose();
                webView = null;
            }
        }

        private async void CoreWebView2_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            await CheckForBffHostCookie();
        }

        //////////////////////////////////////////////////////
        ///
        ///   Programmatic Login for Testing Purposes only
        
        private bool _doAutomatedLogin = true;
        private bool _programmaticLogin_HaveEnteredUsername;
        private bool _programmaticLogin_HaveEnteredPassword;
        private bool _programmaticLogin_HaveEnteredRole;

        private void Reset_ProgrammaticLoginFlags()
        {
            _programmaticLogin_HaveEnteredUsername = false;
            _programmaticLogin_HaveEnteredPassword = false;
            _programmaticLogin_HaveEnteredRole = false;
        }

        private async void DOMContentLoaded_CheckForSessionCookie_ProgrammaticLoginForTesting(object sender, CoreWebView2DOMContentLoadedEventArgs e)
        {
            bool hasUsername = Account.TestingOnly_NotSerialized_Username?.Any() ?? false;
            bool hasPassword = Account.TestingOnly_NotSerialized_Password?.Any() ?? false;
            bool hasRole = Account.TestingOnly_NotSerialized_Role?.Any() ?? false;


            if (!hasUsername || !hasPassword)
            {
                //  No TestingOnly Username or no TestingOnly Password for Programmatic Login for Testing so exit
                return;
            }

            await CheckForBffHostCookie();

            if (!_doAutomatedLogin)
                return;

            const string usernameSelector = "document.querySelector(\"#applogin\").shadowRoot.querySelector(\"#username\")";
            const string passwordSelector = "document.querySelector(\"#applogin\").shadowRoot.querySelector(\"#password\")";
            const string signinSelector = "document.querySelector(\"#applogin\").shadowRoot.querySelector(\"#signin\")";
            const string nextSelector = "document.querySelector(\"#selectRole\").shadowRoot.querySelector(\"#signin\")";
            const string triggerInputEvent = ".dispatchEvent(new Event('input', { bubbles: true }));";

            if (webView == null)
                return;

            try
            {
                string location = webView.Source.AbsolutePath.ToLower();
                if (!location.EndsWith(@"/login") && !location.EndsWith(@"/roleselection"))
                    return;

                // stop listening (we may start again later depending on results below)
                _doAutomatedLogin = false;

                string buttonSelector = location.EndsWith(@"/login") ? signinSelector : nextSelector;
                string buttonText = await ExecuteScriptAsyncUntil(buttonSelector + @".textContent", s => Task.FromResult(s != null && s != @"null"));
                if (buttonText == null)
                    return;

                if (buttonText == @"Continue")
                {
                    if (_programmaticLogin_HaveEnteredUsername)
                    {
                        // Have already entered username.  Is an error that arrived here again for same instance of this Dialog so log error and exit.

                        //  Failed login for invalid password results in username input being displayed again.

                        throw new Exception(@"Already entered Username so appears to be stuck in login loop");
                    }

                    _programmaticLogin_HaveEnteredUsername = true;

                    //if (hasUsername)
                    {
                        await ExecuteScriptAsync(usernameSelector + @".value=" + Account.TestingOnly_NotSerialized_Username.Quote());
                        await ExecuteScriptAsync(usernameSelector + triggerInputEvent);
                    }

                    // start listening again
                    _doAutomatedLogin = true;

                    //if (hasUsername)
                        await ExecuteScriptAsync(signinSelector + @".click()");
                }
                else if (buttonText == @"Sign In")
                {
                    if (_programmaticLogin_HaveEnteredPassword)
                    {
                        // Have already entered password.  Is an error that arrived here again for same instance of this Dialog so log error and exit.

                        //  Possibly Failed login for invalid password.

                        throw new Exception(@"Already entered Password so appears to be stuck in login loop");
                    }

                    _programmaticLogin_HaveEnteredPassword= true;

                    //if (hasPassword)
                    {
                        await ExecuteScriptAsync(passwordSelector + @".value=" + Account.TestingOnly_NotSerialized_Password.Quote());
                        await ExecuteScriptAsync(passwordSelector + triggerInputEvent);
                    }

                    // start listening again
                    _doAutomatedLogin = true;

                    // listening for navigation to start
                    webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;

                    //if (hasPassword)
                        await ExecuteScriptAsync(signinSelector + @".click()");
                }
                else if (buttonText == @"Enter") // select role
                {

                    if (_programmaticLogin_HaveEnteredRole)
                    {
                        // Have already entered role.  Is an error that arrived here again for same instance of this Dialog so log error and exit.

                        //  Possibly Failed login for invalid password.

                        throw new Exception(@"Already entered Role so appears to be stuck in login loop");
                    }

                    _programmaticLogin_HaveEnteredRole = true;

                    if (hasRole)
                    {
                        const string clickDropDownBox = "document.querySelector(\"#selectRole\").shadowRoot.querySelector(\"#roleSelection\").shadowRoot.firstChild.click()";
                        const string selectPopper = "document.querySelector(\"body > tf-popper\")";
                        //const string selectFirstRole = "document.querySelector(\"body > tf-popper > div\").shadowRoot.querySelector(\"div > tf-dropdown-item:nth-child(1)\").click()";
                        string roleNotFoundMsg = string.Format(AlertsResources.ArdiaLoginDlg_Role___0___is_not_an_available_option__Pick_your_role_manually_, Account.TestingOnly_NotSerialized_Role);

                        // loop through popper items to find one that matches the user's role name
                        string selectNamedRole = @"(function() {" +
                                                 @"for (role of document.querySelector(""body > tf-popper > div"").shadowRoot.querySelectorAll(""tf-dropdown-item"")) {" +
                                                 @$"if (role.shadowRoot.textContent == ""{Account.TestingOnly_NotSerialized_Role}"") {{" +
                                                 @"role.click(); return; }" +
                                                 @$"}} alert(""{roleNotFoundMsg}""); }})()";
                        await ExecuteScriptAsyncUntil(clickDropDownBox, async s => await ExecuteScriptAsync(selectPopper) != @"null");
                        await ExecuteScriptAsync(selectNamedRole);
                    }

                    if (hasRole)
                    {
                        var nextEnabled = await ExecuteScriptAsyncUntil(nextSelector + @".disabled", s => Task.FromResult(s == @"false"), 4);
                        if (nextEnabled == @"false")
                        {
                            await ExecuteScriptAsync(nextSelector + @".click()");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // only throw automated login exceptions when testing: users can just login manually
                if (Program.FunctionalTest)
                    throw;
                else
                    Console.Error.WriteLine(ex.ToString());
            }
        }


        #region Helper classes from Ardia
        // Class to hold the client code
        private class ClientCodeDto
        {
            public string Code { get; set; }
        }

        //  Class to hold the clientStatus result from webservice
        private class ClientStatusWebserviceResultDto
        {
            public string clientCodeStatus { get; set; }
            public string clientIdStatus { get; set; }
            public string clientNameStatus { get; set; }
        }


        /// <summary>
        /// The New Client model.
        /// </summary>
        private class NewClient
        {
            /// <summary>
            /// Gets or sets the client name.
            /// </summary>
            public string ClientName { get; set; }

            /// <summary>
            /// Gets or sets the grant types.
            /// </summary>
            public List<string> GrantTypes { get; set; }

            /// <summary>
            /// Gets or sets the redirect uris.
            /// </summary>
            public List<string> RedirectUris { get; set; }

            /// <summary>
            /// Gets or sets the post logout redirect uris.
            /// </summary>
            public List<string> PostLogoutRedirectUris { get; set; }

            /// <summary>
            /// Gets or sets the client uri.
            /// </summary>
            public string ClientUri { get; set; }

            /// <summary>
            /// Gets or sets the scopes.
            /// </summary>
            public List<string> Scopes { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether require consent.
            /// </summary>
            public bool RequireConsent { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether always include user claims in id token.
            /// </summary>
            public bool AlwaysIncludeUserClaimsInIdToken { get; set; } = true;

            /// <summary>
            /// Gets or sets a value indicating whether require pkce.
            /// </summary>
            public bool RequirePkce { get; set; } = true;

            /// <summary>
            /// Gets or sets a value indicating whether client secret is required.
            /// </summary>
            public bool RequireClientSecret { get; set; } = true;

            /// <summary>
            /// Gets or sets the metadata.
            /// </summary>
            public Dictionary<string, string> MetaData { get; set; }

            /// <summary>
            /// Gets or sets the client id.
            /// </summary>
            public string ClientId { get; set; }

            /// <summary>
            /// Gets or sets the client secret.
            /// </summary>
            public string ClientSecret { get; set; }

            /// <summary>
            /// Gets or sets the identity token lifetime, defaulted to 300 seconds.
            /// </summary>
            public ushort IdentityTokenLifetime { get; set; } = 300;

            /// <summary>
            /// Gets or sets the access token lifetime, defaulted to 1200 seconds.
            /// </summary>
            public ushort AccessTokenLifetime { get; set; } = 1200;

            /// <summary>
            /// Gets or sets the absolute refresh token lifetime, defaulted to 2592000 seconds.
            /// </summary>
            public uint AbsoluteRefreshTokenLifetime { get; set; } = 2592000u;

            /// <summary>
            /// Gets or sets the sliding refresh token lifetime, defaulted to 1296000 seconds.
            /// </summary>
            public uint SlidingRefreshTokenLifetime { get; set; } = 1296000u;

            /// <summary>
            /// Gets or sets the refresh token usage.
            /// Defaulted to 0 reuse.
            /// </summary>
            public int RefreshTokenUsage { get; set; }

            /// <summary>
            /// Gets or sets the refresh token expiration.
            /// The refresh token will expire on a fixed point in time (specified by the AbsoluteRefreshTokenLifetime).
            /// This is the default.
            /// </summary>
            public int RefreshTokenExpiration { get; set; } = 1;

            /// <summary>
            /// Gets or sets a value indicating whether allow offline access.
            /// </summary>
            public bool AllowOfflineAccess { get; set; }

            /// <summary>
            /// Gets or sets the device code lifetime defaulted to 300 seconds.
            /// </summary>
            public ushort DeviceCodeLifetime { get; set; } = 300;
        }


        /// <summary>
        /// The model used to activate a client.
        /// </summary>
        private class ClientActivationInput
        {
            /// <summary>
            /// Code to lookup row to update.
            /// </summary>
            public string ApplicationCode { get; set; }

            /// <summary>
            /// Application Name.
            /// </summary>
            public string AppName { get; set; }

            /// <summary>
            /// IP Address that is activating the code.
            /// </summary>
            public string IPAddress { get; set; }

            /// <summary>
            /// Version.
            /// </summary>
            public string Version { get; set; }

            /// <summary>
            /// PC name that is activating the code.
            /// </summary>
            public string PCName { get; set; }

            /// <summary>
            /// Gets or sets the redirect uris.
            /// </summary>
            public List<string> RedirectUris { get; set; }

            /// <summary>
            /// Gets or sets the post logout redirect uris.
            /// </summary>
            public List<string> PostLogoutRedirectUris { get; set; }

            /// <summary>
            /// Gets or sets the client uri.
            /// </summary>
            public string ClientUri { get; set; }

            /// <summary>
            /// Gets or sets the scopes.
            /// </summary>
            public List<string> Scopes { get; set; }

            /// <summary>
            /// Gets or sets the grant types.
            /// </summary>
            public List<string> GrantTypes { get; set; }

            /// <summary>
            /// The access token lifetime in seconds.
            /// </summary>
            public int AccessTokenLifetime { get; set; }
        }


        /// <summary>
        /// Represents the response from the client application.
        /// </summary>
        public class ClientApplicationResponse
        {
            /// <summary>
            /// Gets or sets the registration code.
            /// </summary>
            public string RegistrationCode { get; set; }

            /// <summary>
            /// Gets or sets the application name.
            /// </summary>
            public string AppName { get; set; }

            /// <summary>
            /// Gets or sets the IP address.
            /// </summary>
            public string IPAddress { get; set; }

            /// <summary>
            /// Gets or sets the version of the application.
            /// </summary>
            public string Version { get; set; }

            /// <summary>
            /// Gets or sets the PC name.
            /// </summary>
            public string PCName { get; set; }

            /// <summary>
            /// Gets or sets the registration status of the client application.
            /// </summary>
            public ClientRegistrationStatus Status { get; set; }

            /// <summary>
            /// Gets or sets the date when the registration code was generated.
            /// </summary>
            public DateTime? CodeDate { get; set; }

            /// <summary>
            /// Gets or sets the age of the registration code.
            /// </summary>
            public TimeSpan? CodeAge { get; set; }

            /// <summary>
            /// Gets or sets the date when the client application was activated.
            /// </summary>
            public DateTime? ActivationDate { get; set; }
        }

        /// <summary>
        /// Represents the status of a client registration.
        /// </summary>
        public enum ClientRegistrationStatus
        {
            /// <summary>
            /// Indicates a new client registration.
            /// </summary>
            New,

            /// <summary>
            /// Indicates an active client registration.
            /// </summary>
            Active,

            /// <summary>
            /// Indicates an unused client registration.
            /// </summary>
            Unused,

            /// <summary>
            /// Indicates a disabled client registration.
            /// </summary>
            Disabled
        }


        /// <summary>
        /// Client credentials.
        /// </summary>
        public class ClientCredentialsResponse
        {
            /// <summary>
            /// Gets or sets the Client Name.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Gets or sets the Client Id.
            /// </summary>
            public string ClientId { get; set; }

            /// <summary>
            /// Gets or sets the Client Secret.
            /// </summary>
            public string ClientSecret { get; set; }

            /// <summary>
            /// Gets or sets list of supported Client scopes.
            /// </summary>
            public string[] ClientScopes { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether the client is activate or not.
            /// </summary>
            public bool Activated { get; set; }

            /// <summary>
            /// Gets or sets a value for the Application Code.
            /// </summary>
            public string ApplicationCode { get; set; }

            /// <summary>
            /// Gets or sets the name of the application.
            /// </summary>
            public string ApplicationName { get; set; }

            /// <summary>
            /// Gets or sets the name of the application version.
            /// </summary>
            public string ApplicationVersion { get; set; }
        }
        #endregion

    }

}
