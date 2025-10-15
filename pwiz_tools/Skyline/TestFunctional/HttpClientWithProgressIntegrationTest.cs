/*
 * Original author: brendanx .at. uw.edu
 * AI assistance: Cursor (Claude Sonnet 4) <cursor .at. anysphere.co>
 *
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.CommonResources;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;
using System;
using System.IO;
using pwiz.Skyline.Alerts;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class HttpClientWithProgressIntegrationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestHttpClientWithProgressIntegration()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Category: Network failure tests
            TestDnsFailureHandling();
            TestConnectionFailureHandling();
            TestTimeoutHandling();
            TestConnectionLossHandling();
            TestCancellationHandling();
            TestNoNetworkInterfaceHandling();
            
            // Category: HTTP status code tests
            TestHttp404Handling();
            TestHttp401Handling();
            TestHttp403Handling();
            TestHttp500Handling();
            TestHttp429Handling();
            TestHttpGenericErrorHandling();
            
            // Category: Successful download tests (with mock data)
            TestDownloadStringSuccess();
            TestDownloadDataSuccess();
            TestDownloadFileSuccess();
            
            TestSuccessCase();
        }

        private static void TestDnsFailureHandling()
        {
            using var helper = HttpClientTestHelper.SimulateDnsFailure();
            var uri = new Uri("http://nonexistent.example.com");
            ValidateDownloadStringFailure(uri,
                string.Format(MessageResources.HttpClientWithProgress_MapHttpException_Failed_to_resolve_host__0___Please_check_your_DNS_settings_or_VPN_proxy_,
                    uri.Host));
        }

        private static void TestConnectionFailureHandling()
        {
            using var helper = HttpClientTestHelper.SimulateConnectionFailure();
            var uri = new Uri("http://unreachable.example.com");
            ValidateDownloadStringFailure(uri,
                string.Format(MessageResources.HttpClientWithProgress_MapHttpException_Failed_to_connect_to__0___Please_check_your_network_connection__VPN_proxy__or_firewall_,
                    uri.Host));
        }

        private static void TestTimeoutHandling()
        {
            using var helper = HttpClientTestHelper.SimulateTimeout();
            var uri = new Uri("http://slow.example.com");
            ValidateDownloadStringFailure(uri,
                string.Format(MessageResources.HttpClientWithProgress_MapHttpException_The_request_to__0__timed_out__Please_try_again_,
                    uri));
        }

        private static void TestConnectionLossHandling()
        {
            using var helper = HttpClientTestHelper.SimulateConnectionLoss();
            ValidateDownloadStringFailure(
                MessageResources.HttpClientWithProgress_MapHttpException_The_connection_was_lost_during_download__Please_check_your_internet_connection_and_try_again_);
        }

        private static void TestCancellationHandling()
        {
            using var helper = HttpClientTestHelper.SimulateCancellation();
            ValidateDownloadStringFailure<OperationCanceledException>(new OperationCanceledException().Message);
        }

        private static void TestNoNetworkInterfaceHandling()
        {
            using var helper = HttpClientTestHelper.SimulateNoNetworkInterface();
            ValidateDownloadStringFailure(
                MessageResources.HttpClientWithProgress_MapHttpException_No_network_connection_detected__Please_check_your_internet_connection_and_try_again_);
        }

        private static void TestHttp404Handling()
        {
            using var helper = HttpClientTestHelper.SimulateHttp404();
            var uri = new Uri("http://example.com/notfound");
            ValidateDownloadStringFailure(uri,
                string.Format(MessageResources.HttpClientWithProgress_MapHttpException_The_requested_resource_at__0__was_not_found__HTTP_404___Please_verify_the_URL_,
                    uri));
        }

        private static void TestHttp401Handling()
        {
            using var helper = HttpClientTestHelper.SimulateHttp401();
            var uri = new Uri("http://example.com/protected");
            ValidateDownloadStringFailure(uri,
                string.Format(MessageResources.HttpClientWithProgress_MapHttpException_Access_to__0__was_denied__HTTP_401___Authentication_may_be_required_,
                    uri));
        }

        private static void TestHttp403Handling()
        {
            using var helper = HttpClientTestHelper.SimulateHttp403();
            var uri = new Uri("http://example.com/forbidden");
            ValidateDownloadStringFailure(uri,
                string.Format(MessageResources.HttpClientWithProgress_MapHttpException_Access_to__0__was_forbidden__HTTP_403___You_may_not_have_permission_to_access_this_resource_,
                    uri));
        }

        private static void TestHttp500Handling()
        {
            using var helper = HttpClientTestHelper.SimulateHttp500();
            var uri = new Uri("http://example.com/error");
            ValidateDownloadStringFailure(uri,
                string.Format(MessageResources.HttpClientWithProgress_MapHttpException_The_server__0__encountered_an_internal_error__HTTP_500___Please_try_again_later_or_contact_the_server_administrator_,
                    uri.Host));
        }

        private static void TestHttp429Handling()
        {
            using var helper = HttpClientTestHelper.SimulateHttp429();
            var uri = new Uri("http://example.com/ratelimited");
            ValidateDownloadStringFailure(uri,
                string.Format(MessageResources.HttpClientWithProgress_MapHttpException_Too_many_requests_to__0___HTTP_429___Please_wait_before_trying_again_,
                    uri.Host));
        }

        private static void TestHttpGenericErrorHandling()
        {
            using var helper = HttpClientTestHelper.SimulateHttpError(503, "Service Unavailable");
            var uri = new Uri("http://example.com/unavailable");
            ValidateDownloadStringFailure(uri,
                string.Format(MessageResources.HttpClientWithProgress_MapHttpException_The_server__0__returned_an_error__HTTP__1____Please_try_again_or_contact_support_,
                    uri.Host, 503));
        }

        private static void TestDownloadStringSuccess()
        {
            const string mockData = "Test download string content with special chars: <>&\"'";
            using var helper = HttpClientTestHelper.SimulateSuccessfulDownload(mockData);
            
            RunUI(() =>
            {
                using var dlg = new LongWaitDlg();
                string result = null;
                var status = dlg.PerformWork(SkylineWindow, 0, progressMonitor =>
                {
                    using var httpClient = new HttpClientWithProgress(progressMonitor);
                    result = httpClient.DownloadString(new Uri("http://example.com/test.txt"));
                });
                
                Assert.IsFalse(status.IsCanceled);
                Assert.AreEqual(mockData, result);
            });
        }

        private static void TestDownloadDataSuccess()
        {
            // Create binary test data with non-ASCII bytes
            var mockData = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE, 0xFD, 0x42, 0x43, 0x44 };
            using var helper = HttpClientTestHelper.SimulateSuccessfulDownload(mockData);
            
            RunUI(() =>
            {
                using var dlg = new LongWaitDlg();
                byte[] result = null;
                var status = dlg.PerformWork(SkylineWindow, 0, progressMonitor =>
                {
                    using var httpClient = new HttpClientWithProgress(progressMonitor);
                    result = httpClient.DownloadData("http://example.com/binary.dat");
                });
                
                Assert.IsFalse(status.IsCanceled);
                Assert.IsNotNull(result);
                Assert.AreEqual(mockData.Length, result.Length);
                CollectionAssert.AreEqual(mockData, result);
            });
        }

        private void TestDownloadFileSuccess()
        {
            const string mockData = "Test file content\nLine 2\nLine 3";
            using var helper = HttpClientTestHelper.SimulateSuccessfulDownload(mockData);
            
            var testFilePath = TestContext.GetTestResultsPath("downloaded_test_file.txt");
            
            RunUI(() =>
            {
                using var dlg = new LongWaitDlg();
                var status = dlg.PerformWork(SkylineWindow, 0, progressMonitor =>
                {
                    using var httpClient = new HttpClientWithProgress(progressMonitor);
                    httpClient.DownloadFile(new Uri("http://example.com/file.txt"), testFilePath);
                });
                
                Assert.IsFalse(status.IsCanceled);
                Assert.IsTrue(File.Exists(testFilePath), "Downloaded file should exist");
                
                var fileContent = File.ReadAllText(testFilePath);
                Assert.AreEqual(mockData, fileContent);
            });
        }

        private static void TestSuccessCase()
        {
            // No helper needed - test normal operation
            RunUI(() =>
            {
                using var dlg = new LongWaitDlg();
                var status = dlg.PerformWork(SkylineWindow, 0, progressMonitor =>
                {
                    using var httpClient = new HttpClientWithProgress(progressMonitor);
                    // TODO: Come up with a way to mock up a request/response
                });
                Assert.IsFalse(status.IsCanceled);
            });
        }

        /// <summary>
        /// Helper function to test a failure in HttpClientWithProgress.DownloadString()
        /// with an IOException containing a constant message.
        /// </summary>
        /// <param name="message">The constant message where the requested URI is not part of the validation</param>
        private static void ValidateDownloadStringFailure(string message)
        {
            var uri = new Uri("http://example.com");
            ValidateDownloadStringFailure<IOException>(uri, message);
        }

        /// <summary>
        /// Helper function to test a failure in HttpClientWithProgress.DownloadString()
        /// with an IOException containing a message that references the requested URI.
        /// </summary>
        /// <param name="uri">The requested URI</param>
        /// <param name="message">The expected message that refers to the URI</param>
        private static void ValidateDownloadStringFailure(Uri uri, string message)
        {
            ValidateDownloadStringFailure<IOException>(uri, message);
        }

        /// <summary>
        /// Helper function to test a failure in HttpClientWithProgress.DownloadString()
        /// with a type TEx exception containing a constant message.
        /// </summary>
        /// <typeparam name="TEx">The expected Exception type thrown</typeparam>
        /// <param name="message">The expected message that refers to the URI</param>
        private static void ValidateDownloadStringFailure<TEx>(string message)
            where TEx : Exception
        {
            var uri = new Uri("http://example.com");
            ValidateDownloadStringFailure<TEx>(uri, message);
        }

        /// <summary>
        /// Helper function to test a failure in HttpClientWithProgress.DownloadString()
        /// with a type TEx exception containing a message that references the requested URI.
        /// </summary>
        /// <typeparam name="TEx">The expected Exception type thrown</typeparam>
        /// <param name="uri">The requested URI</param>
        /// <param name="message">The expected message</param>
        private static void ValidateDownloadStringFailure<TEx>(Uri uri, string message)
            where TEx : Exception
        {
            ValidateHttpClient<TEx>(progressMonitor =>
                {
                    using var httpClient = new HttpClientWithProgress(progressMonitor);
                    httpClient.DownloadString(uri);
                },
                message);
        }

        private static void ValidateHttpClient<TEx>(Action<IProgressMonitor> act, string message)
            where TEx : Exception
        {
            RunDlg<MessageDlg>(() =>
                {
                    using var waitDlg = new LongWaitDlg();
                    try
                    {
                        waitDlg.PerformWork(SkylineWindow, 0, act);
                    }
                    catch (TEx x)
                    {
                        ExceptionUtil.DisplayOrReportException(SkylineWindow, x);
                    }
                },
                dlg =>
                {
                    Assert.AreEqual(message, dlg.Message);
                    dlg.OkDialog();
                });
        }
    }
}
