/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Cursor (Claude Sonnet 4) <cursor .at. anysphere.co>
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using pwiz.Common.GUI;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Controls.Editor
{
    public partial class DocumentationViewer : CommonFormEx
    {
        private string _documentationHtml = string.Empty;
        private string _webView2Html = string.Empty;

        /// <summary>
        /// Static property to set the WebView2 environment directory for test scenarios.
        /// Tests should set this before creating a DocumentationViewer and reset to null when done.
        /// </summary>
        public static string TestWebView2EnvironmentDirectory { get; set; }

        public DocumentationViewer(bool showInTaskBar)
        {
            InitializeComponent();

            // WINDOWS 10 UPDATE HACK: Because Windows 10 update version 1803 causes un-parented non-ShowInTaskbar windows to leak GDI and User handles
            ShowInTaskbar = showInTaskBar;
        }

        public bool IsWebView2Initialized { get; private set; }

        public string DocumentationHtml
        {
            get => _documentationHtml;
            set
            {
                _documentationHtml = value;

                if (IsWebView2Initialized)
                    NavigateToHtml();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            InitializeWebView2();
            
            base.OnHandleCreated(e);
        }

        private void InitializeWebView2()
        {
            try
            {
                // Initialize WebView2 environment on UI thread to avoid COM threading issues
                var environment = InitWebView2Environment();
                var task = webView2.EnsureCoreWebView2Async(environment);

                // Initialize WebView2 on a background thread
                CommonActionUtil.RunAsync(() =>
                {
                    try
                    {
                        // Initialize WebView2 with the environment
                        task.Wait();
                        IsWebView2Initialized = true;

                        // Navigate to the HTML if it was set before initialization
                        if (!string.IsNullOrEmpty(_documentationHtml))
                        {
                            RunUIAsync(NavigateToHtml);
                        }
                    }
                    catch (Exception ex)
                    {
                        RunUIAsync(() => CommonAlertDlg.ShowException(this, ex));
                    }
                });
            }
            catch (Exception ex)
            {
                CommonAlertDlg.ShowException(this, ex);
            }
        }

        private CoreWebView2Environment InitWebView2Environment()
        {
            if (string.IsNullOrEmpty(TestWebView2EnvironmentDirectory))
                return null;

            // Create environment with custom user data folder on UI thread
            return CoreWebView2Environment.CreateAsync(null, TestWebView2EnvironmentDirectory).Result;
        }

        private void RunUIAsync(Action act)
        {
            CommonActionUtil.SafeBeginInvoke(this, act);
        }

        private void NavigateToHtml()
        {
            if (IsWebView2Initialized && !string.IsNullOrEmpty(_documentationHtml))
            {
                webView2.NavigateToString(_documentationHtml);
            }
        }

        #region Test helpers
        
        /// <summary>
        /// Gets the HTML content currently displayed in the WebView2 control.
        /// This is useful for testing to verify that the content was actually rendered.
        /// </summary>
        public string GetWebView2HtmlContent(int minLen = 1)
        {
            // Wait for the HTML content of the control to stabilize as matching the documentation HTML
            // Normalize line endings for comparison since WebView2 may normalize \r\n to \n
            if (_webView2Html.Length >= minLen)
                return _webView2Html;
            
            if (!IsWebView2Initialized || webView2?.CoreWebView2 == null)
                return string.Empty;

            try
            {
                // Execute JavaScript to get the document's HTML content
                var task = webView2.CoreWebView2.ExecuteScriptAsync(@"document.documentElement.outerHTML");
                CommonActionUtil.RunAsync(() =>
                {
                    try
                    {
                        var encodedHtml = task.Result;
                        // Decode Unicode escape sequences (e.g., \u003C -> <)
                        var decodedHtml = System.Text.RegularExpressions.Regex.Unescape(encodedHtml);
                        // Remove leading and trailing quotation marks from JavaScript string
                        var rawHtml = decodedHtml.Trim('"');
                        // Normalize line endings to match WebView2's normalization
                        _webView2Html = rawHtml;
                    }
                    catch (Exception ex)
                    {
                        // Ignore but log to debug console in debug builds
                        Debug.WriteLine($@"Failed to get WebView2 outerHtml: {ex}");
                    }
                });
            }
            catch (Exception ex)
            {
                // Ignore but log to debug console in debug builds
                Debug.WriteLine($@"Failed to execute script on WebView2: {ex}");
            }
            return string.Empty;
        }

        #endregion
    }
}
