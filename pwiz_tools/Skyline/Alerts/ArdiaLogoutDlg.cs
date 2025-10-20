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

using pwiz.CommonMsData.RemoteApi.Ardia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using pwiz.Skyline.Util;
using Microsoft.Web.WebView2.Core;
using System.Net;
using Newtonsoft.Json;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Alerts
{
    public partial class ArdiaLogoutDlg : FormEx
    {
        public ArdiaAccount Account { get; private set; }

        private TemporaryDirectory _tempUserDataFolder;

        private CoreWebView2Cookie _bffCookie;

        public ArdiaLogoutDlg(ArdiaAccount account /*, bool headless = false*/)
        {
            InitializeComponent();

            _tempUserDataFolder = new TemporaryDirectory(null, @"~SK_WebView2");

            Account = account;
        }

        private string StripUrlScheme(string urlWithScheme)
        {
            const string httpsPrefix = "https://";
            if (urlWithScheme.StartsWith(httpsPrefix, StringComparison.InvariantCultureIgnoreCase))
                return urlWithScheme.Substring(httpsPrefix.Length);
            // CONSIDER: support http:// ?
            return urlWithScheme;
        }
        private string GetSavedArdiaApplicationCode()
        {
            var ardiaServerURL = StripUrlScheme(Account.ServerUrl);

            Settings.Default.ArdiaRegistrationCodeEntries.TryGetValue(ardiaServerURL, out var applicationCode);
            return applicationCode?.ClientApplicationCode;
        }

        protected override void OnShown(EventArgs e)
        {
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

            var _baseUrl = StripUrlScheme(Account.ServerUrl);

            var applicationCode = GetSavedArdiaApplicationCode();


            // Get the logout URL
            var logoutUrl = await GetLogoutUrl();

            if (logoutUrl != null && webView != null && webView.CoreWebView2 != null)
            {
                // Construct the complete logout URL
                var url = $@"https://api.{_baseUrl}{logoutUrl}&applicationcode={applicationCode}&returnUrl=https://{_baseUrl}/";

                // Add an event handler to the webview component to get the Bff-Host cookie once the user has logged out
                webView.SourceChanged += WebView_SourceChanged;
                // Navigate the user to the logout page
                webView.Visible = true;
                webView.CoreWebView2.Navigate(url);
                // Wait for the user to logout
                while (_bffCookie == null || _bffCookie.Value != string.Empty)
                {
                    await Task.Delay(100);
                }

                // Hide the webview once the user has logged out
                webView.Visible = false;

                webView.CoreWebView2.Environment.BrowserProcessExited += (s, ea) => DialogResult = DialogResult.OK;
                webView.Dispose();
                webView = null;

                // Show a message box to indicate that the user has logged out successfully
                MessageDlg.Show(this, AlertsResources.ArdiaLogoutDlg_InitializeWebView_User_logged_out_successfully_);
            }
        }


        // Get the logout URL via the "user" endpoint of the Session Management API
        private async Task<string> GetLogoutUrl()
        {
            var _baseUrl = StripUrlScheme(Account.ServerUrl);

            var url = new Uri($@"https://api.{_baseUrl}/session-management/bff/user");

            var applicationCode = GetSavedArdiaApplicationCode();



            var cookieContainer = new CookieContainer();
            using var handler = new HttpClientHandler();
            handler.CookieContainer = cookieContainer;
            using var httpClient = Account.GetAuthenticatedHttpClient();
            cookieContainer.Add(new Uri($"https://api.{_baseUrl}"), new Cookie(_bffCookie.Name, _bffCookie.Value));
            httpClient.DefaultRequestHeaders.Add(@"Accept", @"application/json");
            httpClient.DefaultRequestHeaders.Add(@"applicationCode", applicationCode);
            // Add the x-csrf header to the request
            httpClient.DefaultRequestHeaders.Add(@"x-csrf", @"1");

            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            var responseJson =
                JsonConvert.DeserializeObject<IEnumerable<IDictionary<string, string>>>(responseString);
            // Get the logout URL from the response JSON object
            var logoutUrl = responseJson.FirstOrDefault(x => x[@"type"] == @"bff:logout_url")?[@"value"];
            return logoutUrl;
        }


        // Event handler used to call the GetCookies method when the source of the webview changes 
        private async void WebView_SourceChanged(object sender, CoreWebView2SourceChangedEventArgs e)
        {
            // if (sender is WebView2 webView)
                await GetCookies();
        }

        // Get the Bff-Host cookie from the webview component once the user has logged in
        private async Task GetCookies()
        {
            // Get the list of cookies from the webview
            var cookies = await webView.CoreWebView2.CookieManager.GetCookiesAsync(Account.ServerUrl);
            // Find the Bff-Host cookie
            _bffCookie = cookies.FirstOrDefault(x => x.Name == @"Bff-Host");
        }
    }
}
