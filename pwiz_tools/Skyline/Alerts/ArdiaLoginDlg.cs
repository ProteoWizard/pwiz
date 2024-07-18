/*
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
using Microsoft.Web.WebView2.Core;
using pwiz.Skyline.Util.Extensions;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using IdentityModel;
using IdentityModel.Client;
using Newtonsoft.Json;
using pwiz.Common.Collections;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Alerts
{
    public partial class ArdiaLoginDlg : FormEx
    {
        public ArdiaAccount Account { get; private set; }
        public Func<HttpClient> AuthenticatedHttpClientFactory { get; private set; }

        public ArdiaLoginDlg(ArdiaAccount account/*, bool headless = false*/)
        {
            InitializeComponent();

            Account = account;
            _tempUserDataFolder = new TemporaryDirectory(null, @"~SK_WebView2");
            
            // To support command line, need to do something completely different since Ardia objects to Skyline code having username/password and also programmatic sign in to Ardia not possible for MFA and other possible issues.
            //
            //      The code to Programmatic sign in has been changed to use account.TestingOnly_NotSerialized_Username AND account.TestingOnly_NotSerialized_Password
            //
            // if (headless)
            // {
            //     if (account.Username.IsNullOrEmpty() || account.Password.IsNullOrEmpty())
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

        private TemporaryDirectory _tempUserDataFolder;

        private Cookie _bffCookie;

        private bool _firstTime_ExecuteClientRegistration = true;

        private bool _firstTime_ExecuteClientRegistration_Force_ApplicationCode_To_Fake = true;

        // private string _ardia_ApplicationCode__TEMP; //  TODO  Only used to hold ardia_ApplicationCode until store in Settings

        private string GetSavedArdiaApplicationCode()
        {
            //  Temp to NOT store in Settings

            // return _ardia_ApplicationCode__TEMP;

            //  END Temp NOT store in Settings

            var ardiaServerURL = Account.ServerUrl.Replace("https://", "");
            
            string applicationCode = null;
            
            {
                if (!Settings.Default.ArdiaRegistrationCodes.TryGetValue(ardiaServerURL,
                        out applicationCode))
                {
                    applicationCode = null;
                }
            }
            
            return applicationCode;
        }

        private void SetSavedArdiaApplicationCode(string applicationCode)
        {

            //  Temp to NOT store in Settings

            // _ardia_ApplicationCode__TEMP = applicationCode;

            //  END Temp NOT store in Settings

            var ardiaServerURL = Account.ServerUrl.Replace("https://", "");
            
            Settings.Default.ArdiaRegistrationCodes[ardiaServerURL] = applicationCode;
        }

        private Func<HttpClient> GetFactory()
        {
            return () =>
            {
                var applicationCode = GetSavedArdiaApplicationCode();


                if (applicationCode == null)
                {
                    throw new Exception("GetFactory(); if (applicationCode == null) ");
                }

                // var applicationCode = Account.ApplicationCode;

                var cookieContainer = new CookieContainer();
                var handler = new HttpClientHandler();
                handler.CookieContainer = cookieContainer;
                var client = new HttpClient(handler);
                client.BaseAddress = new Uri(Account.ServerUrl);
                cookieContainer.Add(new Uri(Account.ServerUrl.Replace(@"https://", @"https://api.")),
                    new Cookie(_bffCookie.Name, _bffCookie.Value));
                client.DefaultRequestHeaders.Add(@"Accept", @"application/json");

                client.DefaultRequestHeaders.Add(@"applicationCode", applicationCode);

                return client;
            };
        }

        protected override void OnShown(EventArgs e)
        {
            if (!Account.BffHostCookie.IsNullOrEmpty())
            {
                _bffCookie = new Cookie(@"Bff-Host", Account.BffHostCookie);
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

            InitializeWebView();
        }

        private async void InitializeWebView()
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
            webView.CoreWebView2.DOMContentLoaded += CoreWebView2_NavigationCompleted;

            StartAtClientRegistrationIfNeeded();
        }


        private async void StartAtClientRegistrationIfNeeded()
        {
            //  Cleanup in case execute this a second time
            webView.CoreWebView2.NavigationCompleted -= CoreWebView2OnNavigationCompleted_AfterNavigateToLoginURL;

            webView.CoreWebView2.NavigationCompleted += CoreWebView2OnNavigationCompleted_AfterNavigateTo_ClientRegistrationPage;

            //   START:  Stuffing in launch Register Device here to see if can get working

            // Account = Account.ChangeApplicationCode("6fFwDy55");

            var ardiaServerURL = Account.ServerUrl.Replace("https://", "");

            var applicationCode_BeforeRegister = GetSavedArdiaApplicationCode();
            

            // !!!!!!!!!!!!!!!!

            //  TODO DJJ  FAKE

            // if (_firstTime_ExecuteClientRegistration_Force_ApplicationCode_To_Fake)
            // {
            //     _firstTime_ExecuteClientRegistration_Force_ApplicationCode_To_Fake = false;
            //
            //     //  Use One of following "applicationCode_BeforeRegister = ..."
            //
            //     //  TODO  DJJ  FAKE set to null so do Client Registration always
            //
            //     // applicationCode_BeforeRegister = null;
            //
            //     //  TODO  DJJ  FAKE set to "FAKE"" so do Client Registration always
            //
            //     applicationCode_BeforeRegister = "FAKE";
            //
            //     SetSavedArdiaApplicationCode(applicationCode_BeforeRegister);
            // }

            // !!!!!!!!!!!!!!!!


            // var applicationCode_BeforeRegister = Account.ApplicationCode;

            if (applicationCode_BeforeRegister == null)
            {
                _firstTime_ExecuteClientRegistration = false;

                try
                {
                    // MessageDlg.Show(webView, "Register this Skyline instance with Ardia");

                    await RegisterDevice();

                    // MessageDlg.Show(webView, "Registration of this Skyline instance with Ardia is complete.  Continuing to Sign in.");

                }
                catch (Exception e)
                {
                    // MessageBox.Show(webView, e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    MessageDlg.ShowWithException(webView, "Error registering this Skyline instance to Ardia", e); //  TODO  DJJ  Maybe need something different

                    // throw;

                    //  TODO  May be better to close/exit this dialog here

                    // this does not not work here.  Get problems further down the line from "webView = null"


                    webView.CoreWebView2.Environment.BrowserProcessExited += (s, ea) => DialogResult = DialogResult.OK;
                    webView.Dispose();
                    webView = null;

                    return;
                }
            }

            var applicationCode_AfterRegister = GetSavedArdiaApplicationCode();
            
            if (applicationCode_AfterRegister == null)
            {
                throw new Exception("applicationCode_AfterRegister == null");
            }


            // !!!!!!!!!!!!!!!!

            // applicationCode_AfterRegister = "FAKE"; // TODO DJJ  See what happens when have invalid application code at the Login URL

            // !!!!!!!!!!!!!!!!

            //  Remove Listener for Client Registration page
            webView.CoreWebView2.NavigationCompleted -= CoreWebView2OnNavigationCompleted_AfterNavigateTo_ClientRegistrationPage;  // Remove Listener


            //   END:  Stuffing in launch Register Device here to see if can get working

            var _baseUrl = Account.ServerUrl.Replace("https://", "");


            // Navigate to the login page
            var loginUrl = $"https://api.{_baseUrl}/session-management/bff/login?applicationcode={applicationCode_AfterRegister}&returnUrl=https://{_baseUrl}/";

            // MessageDlg.Show(webView, "loginUrl: " + loginUrl);

            //  NOTE:  Opening the Login URL with invalid "applicationcode" results in 401 HTTP status code along with returned contents of:  "Unknown Client. Please register/activate the client"

            webView.CoreWebView2.NavigationCompleted += CoreWebView2OnNavigationCompleted_AfterNavigateToLoginURL;

            webView.CoreWebView2.Navigate(loginUrl);

            //  When Application Code is invalid, the Webview at 'login' URL shows "Unknown Client. Please register/activate the client"

            //  OLD LOGIN

            // Navigate to the login page
            // webView.CoreWebView2.Navigate($@"{Account.ServerUrl}/login");

        }

        private void CoreWebView2OnNavigationCompleted_AfterNavigateTo_ClientRegistrationPage(object sender, CoreWebView2NavigationCompletedEventArgs eventArgs)
        {

            if (!eventArgs.IsSuccess)
            {
                if (eventArgs.HttpStatusCode == 404)
                {
                    MessageDlg.Show(
                        webView, "Load Client Registration page failed with HTTP status code 404.  Page not found at URL.");

                    //  404 may result in something different being triggered

                    //  TODO DJJ   Probably want to direct UI to register client if that was NOT just done.  If the Registration Code (ApplicationCode) was just received there is a problem with it.

                }
                else
                {
                    MessageDlg.Show(
                        webView, "Load Client Registration page failed with HTTP status code " + eventArgs.HttpStatusCode + ".");
                }

                // TODO DJJ Not sure what to do here

                //  Exception throws does NOT appear to do anything.

                throw new Exception("Load Client Registraton page failed. if (!eventArgs.IsSuccess) ");
            }
        }

        private void CoreWebView2OnNavigationCompleted_AfterNavigateToLoginURL(object sender, CoreWebView2NavigationCompletedEventArgs eventArgs)
        {
            if (!eventArgs.IsSuccess)
            {
                if (eventArgs.HttpStatusCode == 401)
                {
                    //  Page load for Login failed with HTTP status code 401.  Assumed due to invalid ApplicationCode (Registration Code)

                    if (_firstTime_ExecuteClientRegistration)
                    {
                        //  Client Registration has NOT been executed yet to get new value

                        _firstTime_ExecuteClientRegistration = false;

                        //  Assume the Registration Code (ApplicationCode) is invalid and remove it and go through client registration to get a new one

                        SetSavedArdiaApplicationCode(null);


                        //  Start over at Client Registration if needed
                        StartAtClientRegistrationIfNeeded();
                    }
                    else
                    {
                        MessageDlg.Show(
                            webView,
                            "Load Login page failed with HTTP status code 401.  Likely that the Client Registration Code is invalid.  A new Client Registration Code was just received from the server so this is likely a bug.");

                    }

                    //  TODO DJJ   Probably want to direct UI to register client if that was NOT just done.  If the Registration Code (ApplicationCode) was just received there is a problem with it.

                }
                else if (eventArgs.HttpStatusCode == 404)
                {
                    MessageDlg.Show(
                        webView, "Load Login page failed with HTTP status code 404.  Page not found at URL.");

                    //  404 may result in something different being triggered


                    //  TODO DJJ   Probably want to direct UI to register client if that was NOT just done.  If the Registration Code (ApplicationCode) was just received there is a problem with it.

                }
                else
                {
                    MessageDlg.Show(
                        webView, "Load Login page failed with HTTP status code " + eventArgs.HttpStatusCode + ".");
                }

                // TODO DJJ Not sure what to do here

                //  Exception throws does NOT appear to do anything.

                throw new Exception("Load Login page failed. if (!eventArgs.IsSuccess) ");
            }
        }

        //   START:  Stuffing in launch Register Device here to see if can get working

        // Register the device with the Ardia platform using the WebView2 control to display the registration page
        public async Task RegisterDevice()
        {
            if (webView != null && webView.CoreWebView2 != null)
            {
                var _baseUrl = Account.ServerUrl.Replace("https://", "");

                try
                {
                    var authority = $"https://identity.{_baseUrl}";
                    var discoveryCache = new DiscoveryCache(authority);
                    var discoveryDocument = await discoveryCache.GetAsync();
                    if (discoveryDocument.IsError)
                    {
                        //  For Register Device, this is the first server access done so if the URL is invalid (404) or network problems this will be the error.

                        throw new Exception(discoveryDocument.Error);
                    }

                    var deviceAuthorizationResponse = await RequestDeviceAuthorizationAsync();
                    var verificationUri = $"{deviceAuthorizationResponse.VerificationUriComplete}&showContent=false";
                    webView.CoreWebView2.Navigate(verificationUri);
                    var userTokenResponse = await RequestUserTokenAsync(deviceAuthorizationResponse);
                    var z = 0;
                    var clientCode = await CreateNewClient(userTokenResponse);
                    var identityClient = await ActivateClient(clientCode, userTokenResponse);

                    var x = 0;
                    // StoreClientCredentials(identityClient);
                }
                catch (HttpRequestException e)
                {
                    var eToString = e.ToString();
                    var z = 0;
                    
                    // MessageDlg.Show(webView, "Error Registering Skyline Instance in Ardia as Client.  eToString: " + eToString );
                    //
                    // var eMessage = e.Message;


                    if (e.Message == "Response status code does not indicate success: 403 (Forbidden).")
                    {
                        MessageDlg.Show(webView, "Error Registering Skyline Instance in Ardia as Client.  Matched 403 Forbidden message.  Exception Message: " + e.Message);
                    }
                    else
                    {
                        MessageDlg.ShowWithException(webView, "Error Registering Skyline Instance in Ardia as Client", e);
                    }


                    //  added throw to code from Thermo
                    throw;
                }
                catch (Exception e)
                {
                    var eToString = e.ToString();
                    var z = 0;
                    MessageDlg.ShowWithException(webView, "Error Registering Skyline Instance in Ardia as Client", e);
                    //  added throw to code from Thermo
                    throw;
                }
            }
        }

        // Request a user token from the Ardia platform using the device code obtained from the device authorization response
        private async Task<TokenResponse> RequestUserTokenAsync(DeviceAuthorizationResponse deviceAuthorizationResponse)
        {
            using (var httpClient = new HttpClient())
            {
                var _baseUrl = Account.ServerUrl.Replace("https://", "");

                //  Hard coded for initial connection to get device registration
                var _ardiaDeviceClientId = "ardia.device.client.registration";

                var tokenEndpoint = $"https://identity.{_baseUrl}/connect/token";
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
                    // Wait for the interval and try again
                    await Task.Delay(deviceAuthorizationResponse.Interval * 1000);
                    return await RequestUserTokenAsync(deviceAuthorizationResponse);
                }
                else
                {
                    throw new Exception(response.Error);
                }
            }
        }

        // Request device authorization from the Ardia platform using the device authorization endpoint and the device client credentials
        private async Task<DeviceAuthorizationResponse> RequestDeviceAuthorizationAsync()
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var _baseUrl = Account.ServerUrl.Replace("https://", "");

                    //  Hard coded for initial connection to get device registration
                    var _ardiaDeviceClientId = "ardia.device.client.registration";

                    var deviceAuthorizationEndpoint = $"https://identity.{_baseUrl}/connect/deviceauthorization";
                    var response = await httpClient.RequestDeviceAuthorizationAsync(new DeviceAuthorizationRequest
                    {
                        Address = deviceAuthorizationEndpoint,
                        ClientId = _ardiaDeviceClientId,
                        Scope = "openid profile Ardia_Client_Registration"
                    });
                    return response;
                }
            // StoreClientCredentials(identityClient);
            }
            catch (HttpRequestException e)
            {
                var eToString = e.ToString();
                var z = 0;

                // MessageDlg.Show(webView, "Error Registering Skyline Instance in Ardia as Client.  eToString: " + eToString );
                //
                // var eMessage = e.Message;


                if (e.Message == "Response status code does not indicate success: 403 (Forbidden).")
                {
                    MessageDlg.Show(webView, "RequestDeviceAuthorizationAsync: Error Registering Skyline Instance in Ardia as Client.  Matched 403 Forbidden message.  Exception Message: " + e.Message);
                }
                else
                {
                    MessageDlg.ShowWithException(webView, "Error Registering Skyline Instance in Ardia as Client", e);
                }


                //  added throw to code from Thermo
                throw;
            }
            catch (Exception e)
            {
                var eToString = e.ToString();
                var z = 0;
                MessageDlg.ShowWithException(webView, "RequestDeviceAuthorizationAsync: Error Registering Skyline Instance in Ardia as Client", e);
                //  added throw to code from Thermo
                throw;
            }
        }

        // Create a new client in the Ardia platform using the user access token obtained from the token response
        private async Task<string> CreateNewClient(TokenResponse userTokenResponse)
        {
            try {
                var _baseUrl = Account.ServerUrl.Replace("https://", "");

                var newClientUri = $"https://api.{_baseUrl}/identity-registration/api/v2/Clients";
                var accessToken = userTokenResponse.AccessToken;
                var clientName = $"SkylineSampleApp{Guid.NewGuid().ToString()}";
                var newClient = new NewClient() { ClientName = clientName };
                var newClientData = JsonConvert.SerializeObject(newClient);

                using (var httpClient = new HttpClient())
                using (var request = new HttpRequestMessage(HttpMethod.Post, newClientUri))
                using (var content = new StringContent(newClientData, Encoding.UTF8, "application/json"))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
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
            catch (HttpRequestException e)
            {
                var eToString = e.ToString();
                var z = 0;

                // MessageDlg.Show(webView, "Error Registering Skyline Instance in Ardia as Client.  eToString: " + eToString );
                //
                // var eMessage = e.Message;


                if (e.Message == "Response status code does not indicate success: 403 (Forbidden).")
                {
                    MessageDlg.Show(webView, "Error Registering Skyline Instance in Ardia as Client.  Matched 403 Forbidden message.  Exception Message: " + e.Message);
                }
                else
                {
                    MessageDlg.ShowWithException(webView, "Error Registering Skyline Instance in Ardia as Client", e);
                }


                //  added throw to code from Thermo
                throw;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message, e);
            }
        }

        // Activate the client in the Ardia platform using the client code obtained from the client creation response
        private async Task<IdentityClient> ActivateClient(string clientCode, TokenResponse userTokenResponse)
        {
            var _baseUrl = Account.ServerUrl.Replace("https://", "");

            var activateClientUri = $"https://api.{_baseUrl}/identity-registration/api/v2/Clients/activate";
            var accessToken = userTokenResponse.AccessToken;
            // Define the client activation input
            var clientActivationInput = new ClientActivationInput()
            {
                ApplicationCode = clientCode,
                AppName = "SkylineSampleApp",
                Version = "1.0.0",
                PCName = Environment.MachineName,
                RedirectUris = new List<string>() { "http://localhost:5001/signin-oidc" },
                PostLogoutRedirectUris = new List<string>() { "http://localhost:5001/signout-oidc" },
                ClientUri = "http://localhost:5001",
                Scopes = new List<string>() { "openid", "profile", "offline_access", "DataServerApi", "IdentityServerApi" },
                GrantTypes = new List<string>() { "authorization_code", "urn:ietf:params:oauth:grant-type:token-exchange" },
            };
            var clientActivationInputData = JsonConvert.SerializeObject(clientActivationInput);

            using (var httpClient = new HttpClient())
            using (var clientActivationRequest = new HttpRequestMessage(HttpMethod.Post, activateClientUri))
            using (var clientActivationContent = new StringContent(clientActivationInputData, Encoding.UTF8, "application/json"))
            {
                clientActivationRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                clientActivationRequest.Content = clientActivationContent;
                using (var clientActivationResponse = await httpClient.SendAsync(clientActivationRequest))
                {
                    clientActivationResponse.EnsureSuccessStatusCode();
                    var clientActivationData = await clientActivationResponse.Content.ReadAsStringAsync();
                    var clientActivation = JsonConvert.DeserializeObject<ClientApplicationResponse>(clientActivationData);
                    var clientCredentialsUri =
                        $"https://api.{_baseUrl}/identity-registration/api/v2/Clients/credentials?code={clientActivation.RegistrationCode}";
                    using (var clientCredentialsRequest = new HttpRequestMessage(HttpMethod.Get, clientCredentialsUri))
                    {
                        clientCredentialsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                        using (var clientCredentialsResponse = await httpClient.SendAsync(clientCredentialsRequest))
                        {
                            clientCredentialsResponse.EnsureSuccessStatusCode();
                            var clientCredentialsResponseData = await clientCredentialsResponse.Content.ReadAsStringAsync();
                            var clientCredentials = JsonConvert.DeserializeObject<ClientCredentialsResponse>(clientCredentialsResponseData);

                            var ClientId = clientCredentials.ClientId;
                            var clientSecret = clientCredentials.ClientSecret;
                            var ApplicationCode = clientCredentials.ApplicationCode;
                            var Name = clientCredentials.Name;


                            SetSavedArdiaApplicationCode(clientCredentials.ApplicationCode);


                            await CheckForBffHostCookie();

                            return new IdentityClient
                            {
                                ClientId = clientCredentials.ClientId,
                                ClientSecret = clientCredentials.ClientSecret,
                                ClientCode = clientCredentials.ApplicationCode,
                                ClientName = clientCredentials.Name,
                            };


                        }
                    }
                }
            }
        }


        //   END:  Stuffing in launch Register Device here to see if can get working


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
                await Task.Delay(delayBetweenTries);
            }

            return null;
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
                Account = Account.ChangeBffHostCookie(_bffCookie.Value);
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

        private bool _doAutomatedLogin = true;
        private async void CoreWebView2_NavigationCompleted(object sender, CoreWebView2DOMContentLoadedEventArgs e)
        {
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

                bool hasUsername = Account.TestingOnly_NotSerialized_Username?.Any() ?? false;
                bool hasPassword = Account.TestingOnly_NotSerialized_Password?.Any() ?? false;
                bool hasRole = Account.TestingOnly_NotSerialized_Role?.Any() ?? false;

                if (buttonText == @"Continue")
                {
                    if (hasUsername)
                    {
                        await ExecuteScriptAsync(usernameSelector + @".value=" + Account.TestingOnly_NotSerialized_Username.Quote());
                        await ExecuteScriptAsync(usernameSelector + triggerInputEvent);
                    }

                    // start listening again
                    _doAutomatedLogin = true;

                    if (hasUsername)
                        await ExecuteScriptAsync(signinSelector + @".click()");
                }
                else if (buttonText == @"Sign In")
                {
                    if (hasPassword)
                    {
                        await ExecuteScriptAsync(passwordSelector + @".value=" + Account.Password.Quote());
                        await ExecuteScriptAsync(passwordSelector + triggerInputEvent);
                    }

                    // start listening again
                    _doAutomatedLogin = true;

                    if (hasPassword)
                        await ExecuteScriptAsync(signinSelector + @".click()");
                }
                else if (buttonText == @"Next") // select role
                {
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

                    // listening for navigation to start
                    webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;

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


        #region Helper classes
        // Class to hold the client code
        private class ClientCodeDto
        {
            public string Code { get; set; }
        }
        #endregion


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
        /// Represents an identity client.
        /// </summary>
        private class IdentityClient
        {
            /// <summary>
            /// Gets or sets the client ID.
            /// </summary>
            public string ClientId { get; set; }

            /// <summary>
            /// Gets or sets the client secret.
            /// </summary>
            public string ClientSecret { get; set; }

            /// <summary>
            /// Gets or sets the client name.
            /// </summary>
            public string ClientName { get; set; }

            /// <summary>
            /// Gets or sets the client code.
            /// </summary>
            public string ClientCode { get; set; }

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
    }

}
