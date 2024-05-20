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
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using pwiz.Skyline.Util;
using Microsoft.Web.WebView2.Core;
using pwiz.Skyline.Util.Extensions;
using System.Net;
using pwiz.Common.Collections;

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

            /*if (headless)
            {
                if (account.Username.IsNullOrEmpty() || account.Password.IsNullOrEmpty())
                {
                    throw new ArgumentException("importing an Ardia file from command-line requires the account to have username and password set up in Skyline (Tools > Options > Remote Accounts)");
                }
                var uiThread = new Thread(() =>
                {
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(async () =>
                    {
                        var environment = await CoreWebView2Environment.CreateAsync(null, null, null);
                        var controller = await environment.CreateCoreWebView2ControllerAsync(HWND_MESSAGE);
                        controller.CoreWebView2.Navigate("https://microsoft.com");
                    });

                    Dispatcher.Run();
                });

                uiThread.SetApartmentState(ApartmentState.STA);
                uiThread.Start();
                uiThread.Join();
                Visible = false;
            }*/
        }

        private TemporaryDirectory _tempUserDataFolder;

        private Cookie _bffCookie;

        private Func<HttpClient> GetFactory()
        {
            return () =>
            {
                var cookieContainer = new CookieContainer();
                var handler = new HttpClientHandler();
                handler.CookieContainer = cookieContainer;
                var client = new HttpClient(handler);
                client.BaseAddress = new Uri(Account.ServerUrl);
                cookieContainer.Add(new Uri(Account.ServerUrl.Replace(@"https://", @"https://api.")),
                    new Cookie(_bffCookie.Name, _bffCookie.Value));
                client.DefaultRequestHeaders.Add(@"Accept", @"application/json");
                client.DefaultRequestHeaders.Add(@"applicationCode", @"z78ja2c2");
                return client;
            };
        }

        protected override void OnShown(EventArgs e)
        {
            //if (Settings.Default.LastArdiaLoginCookieByUsername.ContainsKey(Account.Username))
            if (!Account.BffHostCookie.IsNullOrEmpty())
            {
                //_bffCookie = new Cookie(@"Bff-Host", Settings.Default.LastArdiaLoginCookieByUsername[Account.Username]);
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
            var options = new CoreWebView2EnvironmentOptions(@"--disable-web-security --allow-file-access-from-files --allow-file-access");
            var environment = await CoreWebView2Environment.CreateAsync(null, _tempUserDataFolder.DirPath, options);
            // EnsureCoreWebView2Async must be called before any other call
            // to WebView2 and before setting the Source property
            // since these will both cause initialization of the CoreWebView2 property
            // but using a default CoreWebView2Environment rather than your custom one.
            await webView.EnsureCoreWebView2Async(environment);
            webView.CoreWebView2.CookieManager.DeleteAllCookies();

            // Wait for the user to login and the source to change
            //webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            webView.CoreWebView2.DOMContentLoaded += CoreWebView2_NavigationCompleted;

            // Navigate to the login page
            webView.CoreWebView2.Navigate($@"{Account.ServerUrl}/login");
            webView.Focus();
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
                //Settings.Default.LastArdiaLoginCookieByUsername[Account.Username] = _bffCookie.Value;
                //Settings.Default.Save();
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

            string location = webView.Source.AbsolutePath.ToLower();
            if (!location.EndsWith(@"/login") && !location.EndsWith(@"/roleselection"))
                return;

            // stop listening (we may start again later depending on results below)
            _doAutomatedLogin = false;

            string buttonSelector = location.EndsWith(@"/login") ? signinSelector : nextSelector;
            string buttonText = await ExecuteScriptAsyncUntil(buttonSelector + @".textContent", s => Task.FromResult(s != null && s != @"null"));
            if (buttonText == null)
                return;

            bool hasUsername = Account.Username?.Any() ?? false;
            bool hasPassword = Account.Password?.Any() ?? false;
            bool hasRole = Account.Role?.Any() ?? false;

            if (buttonText == @"Continue")
            {
                if (hasUsername)
                {
                    await ExecuteScriptAsync(usernameSelector + @".value=" + Account.Username.Quote());
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
                    string roleNotFoundMsg = string.Format(AlertsResources.ArdiaLoginDlg_Role___0___is_not_an_available_option__Pick_your_role_manually_, Account.Role);

                    // loop through popper items to find one that matches the user's role name
                    string selectNamedRole = @"(function() {" +
                                             @"for (role of document.querySelector(""body > tf-popper > div"").shadowRoot.querySelectorAll(""tf-dropdown-item"")) {" +
                                             @$"if (role.shadowRoot.textContent == ""{Account.Role}"") {{" +
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
    }
}
