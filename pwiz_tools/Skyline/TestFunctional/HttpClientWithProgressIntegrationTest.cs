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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;
using System;
using System.IO;

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
            TestCancellationClickByException();
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
            
            // Category: Progress reporting tests
            TestDownloadProgressReporting();
            
            // Category: Cancellation tests
            TestDownloadCancellationViaButton();
            
            TestSuccessCase();
        }

        private static void TestDnsFailureHandling()
        {
            using var helper = HttpClientTestHelper.SimulateDnsFailure();
            ValidateDownloadFailure(helper, "http://nonexistent.example.com");
        }

        private static void TestConnectionFailureHandling()
        {
            using var helper = HttpClientTestHelper.SimulateConnectionFailure();
            ValidateDownloadFailure(helper, "http://unreachable.example.com");
        }

        private static void TestTimeoutHandling()
        {
            using var helper = HttpClientTestHelper.SimulateTimeout();
            ValidateDownloadFailure(helper, "http://slow.example.com");
        }

        private static void TestConnectionLossHandling()
        {
            using var helper = HttpClientTestHelper.SimulateConnectionLoss();
            ValidateDownloadFailure(helper);
        }

        private static void TestCancellationHandling()
        {
            using var helper = HttpClientTestHelper.SimulateCancellation();
            ValidateDownloadFailure<OperationCanceledException>(helper);
        }
        
        private static void TestCancellationClickByException()
        {
            using var helper = HttpClientTestHelper.SimulateCancellationClickWithException();
            RunUI(() =>
            {
                using var waitDlg = new LongWaitDlg();
                try
                {
                    var uri = new Uri("http://canceled.example.com");
                    var status = waitDlg.PerformWork(SkylineWindow, 0, progressMonitor => {
                        using var httpClient = new HttpClientWithProgress(progressMonitor);
                        httpClient.DownloadString(uri);
                    });
                    // Expecting a canceled status without an exception
                    Assert.IsTrue(status.IsCanceled);
                }
                catch (Exception x)
                {
                    Assert.Fail($"Unexpected exception thrown {x.Message}");
                }
            });
        }

        private static void TestNoNetworkInterfaceHandling()
        {
            using var helper = HttpClientTestHelper.SimulateNoNetworkInterface();
            ValidateDownloadFailure(helper);
        }

        private static void TestHttp404Handling()
        {
            using var helper = HttpClientTestHelper.SimulateHttp404();
            ValidateDownloadFailure(helper, "http://example.com/notfound");
        }

        private static void TestHttp401Handling()
        {
            using var helper = HttpClientTestHelper.SimulateHttp401();
            ValidateDownloadFailure(helper, "http://example.com/protected");
        }

        private static void TestHttp403Handling()
        {
            using var helper = HttpClientTestHelper.SimulateHttp403();
            ValidateDownloadFailure(helper, "http://example.com/forbidden");
        }

        private static void TestHttp500Handling()
        {
            using var helper = HttpClientTestHelper.SimulateHttp500();
            ValidateDownloadFailure(helper, "http://example.com/error");
        }

        private static void TestHttp429Handling()
        {
            using var helper = HttpClientTestHelper.SimulateHttp429();
            ValidateDownloadFailure(helper, "http://example.com/ratelimited");
        }

        private static void TestHttpGenericErrorHandling()
        {
            using var helper = HttpClientTestHelper.SimulateHttpError(503, "Service Unavailable");
            ValidateDownloadFailure(helper, "http://example.com/unavailable");
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

        private static void TestDownloadProgressReporting()
        {
            // 20KB mock data = ~3 chunks of 8192 bytes each (for multiple progress updates)
            var mockData = new byte[20 * 1024];
            for (int i = 0; i < mockData.Length; i++)
                mockData[i] = (byte)(i % 256);
            
            int progressCallbackCount = 0;
            var behavior = new HttpClientTestBehavior
            {
                MockResponseData = mockData,
                SimulateProgress = true,
                OnProgressCallback = () => progressCallbackCount++
            };
            
            using var helper = new HttpClientTestHelper(behavior);
            
            RunUI(() =>
            {
                using var dlg = new LongWaitDlg();
                var status = dlg.PerformWork(SkylineWindow, 0, progressMonitor =>
                {
                    using var httpClient = new HttpClientWithProgress(progressMonitor);
                    var result = httpClient.DownloadData("http://example.com/test.dat");
                    
                    // Verify data was downloaded correctly
                    Assert.AreEqual(mockData.Length, result.Length);
                });
                
                Assert.IsFalse(status.IsCanceled);
                
                // Verify progress was reported multiple times (proves chunked reading works)
                // With 20KB and 8192-byte chunks, we expect at least 2 progress callbacks
                Assert.IsTrue(progressCallbackCount >= 2, 
                    $"Expected at least 2 progress callbacks for {mockData.Length} bytes, got {progressCallbackCount}");
            });
        }

        private static void TestDownloadCancellationViaButton()
        {
            // 50KB mock data to ensure we're mid-download when canceling
            var mockData = new byte[50 * 1024];
            for (int i = 0; i < mockData.Length; i++)
                mockData[i] = (byte)(i % 256);
            
            LongWaitDlg capturedDlg = null;
            bool cancelButtonClicked = false;
            
            var behavior = new HttpClientTestBehavior
            {
                MockResponseData = mockData,
                SimulateProgress = true,
                OnProgressCallback = () =>
                {
                    // Click Cancel button after first chunk is read
                    if (!cancelButtonClicked && capturedDlg != null)
                    {
                        cancelButtonClicked = true;
                        WaitForConditionUI(() => capturedDlg.IsHandleCreated);
                        capturedDlg.Invoke((Action) capturedDlg.CancelDialog);
                    }
                }
            };
            
            using var helper = new HttpClientTestHelper(behavior);
            
            RunUI(() =>
            {
                using var dlg = new LongWaitDlg();
                capturedDlg = dlg;
                
                bool operationCanceledExceptionThrown = false;
                var status = dlg.PerformWork(SkylineWindow, 0, progressMonitor =>
                {
                    using var httpClient = new HttpClientWithProgress(progressMonitor);
                    
                    // This should throw OperationCanceledException when cancel is detected
                    try
                    {
                        httpClient.DownloadData("http://example.com/large.dat");
                        Assert.Fail("Expected OperationCanceledException to be thrown when user cancels");
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected - operation was canceled by user
                        operationCanceledExceptionThrown = true;
                    }
                });
                
                // Verify cancellation was detected
                Assert.IsTrue(status.IsCanceled, "Status should indicate cancellation");
                Assert.IsTrue(cancelButtonClicked, "Cancel button should have been clicked");
                Assert.IsTrue(operationCanceledExceptionThrown, "OperationCanceledException should have been thrown");
            });
            
            // Verify no MessageDlg was shown - user-initiated cancellation should be silent
            // If a MessageDlg were shown, WaitForOpenForm would find it
            Assert.IsNull(TryWaitForOpenForm<MessageDlg>(50, () =>
                FindOpenForm<LongWaitDlg>() == null));
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
        /// Most concise helper - validates download failure using HttpClientTestHelper.
        /// The helper provides both the simulation and expected message for the IOException type.
        /// </summary>
        /// <param name="helper">The test helper that provides simulation and expected message</param>
        /// <param name="urlText">Optional URL string (defaults to "http://example.com")</param>
        private static void ValidateDownloadFailure(HttpClientTestHelper helper, string urlText = null)
        {
            ValidateDownloadFailure<IOException>(helper, urlText);
        }

        /// <summary>
        /// Helper function to test a failure in HttpClientWithProgress.DownloadString()
        /// with a type TEx exception.
        /// </summary>
        /// <typeparam name="TEx">The expected Exception type thrown</typeparam>
        /// <param name="helper">The test helper that provides simulation and expected message</param>
        /// <param name="urlText">Optional URL string (defaults to "http://example.com")</param>
        private static void ValidateDownloadFailure<TEx>(HttpClientTestHelper helper, string urlText = null)
            where TEx : Exception
        {
            var uri = new Uri(urlText ?? "http://example.com");
            ValidateDownloadStringFailure<TEx>(uri, helper.GetExpectedMessage(uri));
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
