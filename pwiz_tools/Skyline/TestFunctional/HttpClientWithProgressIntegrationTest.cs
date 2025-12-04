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
using System.Text;

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
            // Category: Download - Network failure tests
            TestDnsFailureHandling();
            TestConnectionFailureHandling();
            TestTimeoutHandling();
            TestConnectionLossHandling();
            TestCancellationHandling();
            TestCancellationClickByException();
            TestNoNetworkInterfaceHandling();
            
            // Category: Download - HTTP status code tests
            TestHttp404Handling();
            TestHttp401Handling();
            TestHttp403Handling();
            TestHttp500Handling();
            TestHttp429Handling();
            TestHttpGenericErrorHandling();
            
            // Category: Download - Successful tests (with mock data)
            TestDownloadStringSuccess();
            TestDownloadDataSuccess();
            TestDownloadFileSuccess();
            
            // Category: Download - Progress reporting tests
            TestDownloadProgressReporting();
            
            // Category: Download - Cancellation tests
            TestDownloadCancellationViaButton();
            
            // Category: Upload - Network failure tests
            TestUploadFileDnsFailure();
            TestUploadFileConnectionFailure();
            TestUploadFileTimeout();
            TestUploadFileConnectionLoss();
            TestUploadFileNoNetworkInterface();
            TestUploadFileCancellation();
            
            // Category: Upload - HTTP status code tests
            TestUploadFileHttp401();
            TestUploadFileHttp403();
            TestUploadFileHttp404();
            TestUploadFileHttp500();
            TestUploadFileHttp429();
            TestUploadFileHttpGenericError();
            
            // Category: Upload - Successful tests (without network)
            TestUploadFileSuccess();
            TestUploadDataSuccess();
            
            // Category: Upload - Progress reporting tests
            TestUploadFileProgressReporting();
            TestProgressMessageDoesNotRepeatSize();
            
            // Category: Upload - Cancellation tests
            TestUploadFileCancellationViaButton();
            
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
            // Test with UTF-8 characters to ensure proper encoding/decoding
            const string mockData = "Test download with UTF-8: café 中文 日本語 🔬, Special: <>&\"'";
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
                Assert.AreEqual(mockData, result, "Downloaded string should match mock data including UTF-8 characters");
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
            // Test with UTF-8 to ensure proper encoding through download-to-file path
            const string mockData = "Downloaded file with UTF-8: café 中文 🧬\nLine 2: Special <>&\nLine 3";
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
                
                var fileContent = File.ReadAllText(testFilePath, new UTF8Encoding(false)); // No BOM
                Assert.AreEqual(mockData, fileContent, "Downloaded file content should match mock data including UTF-8 characters");
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

        #region Upload Failure Tests

        private void TestUploadFileDnsFailure()
        {
            using var helper = HttpClientTestHelper.SimulateDnsFailure();
            ValidateUploadFailure(helper, "http://nonexistent.example.com");
        }

        private void TestUploadFileConnectionFailure()
        {
            using var helper = HttpClientTestHelper.SimulateConnectionFailure();
            ValidateUploadFailure(helper, "http://unreachable.example.com");
        }

        private void TestUploadFileTimeout()
        {
            using var helper = HttpClientTestHelper.SimulateTimeout();
            ValidateUploadFailure(helper, "http://slow.example.com");
        }

        private void TestUploadFileConnectionLoss()
        {
            using var helper = HttpClientTestHelper.SimulateConnectionLoss();
            ValidateUploadFailure(helper);
        }

        private void TestUploadFileNoNetworkInterface()
        {
            using var helper = HttpClientTestHelper.SimulateNoNetworkInterface();
            ValidateUploadFailure(helper);
        }

        private void TestUploadFileCancellation()
        {
            using var helper = HttpClientTestHelper.SimulateCancellation();
            ValidateUploadFailure<OperationCanceledException>(helper);
        }

        private void TestUploadFileHttp401()
        {
            using var helper = HttpClientTestHelper.SimulateHttp401();
            ValidateUploadFailure(helper, "http://example.com/protected");
        }

        private void TestUploadFileHttp403()
        {
            using var helper = HttpClientTestHelper.SimulateHttp403();
            ValidateUploadFailure(helper, "http://example.com/forbidden");
        }

        private void TestUploadFileHttp404()
        {
            using var helper = HttpClientTestHelper.SimulateHttp404();
            ValidateUploadFailure(helper, "http://example.com/notfound");
        }

        private void TestUploadFileHttp500()
        {
            using var helper = HttpClientTestHelper.SimulateHttp500();
            ValidateUploadFailure(helper, "http://example.com/error");
        }

        private void TestUploadFileHttp429()
        {
            using var helper = HttpClientTestHelper.SimulateHttp429();
            ValidateUploadFailure(helper, "http://example.com/ratelimited");
        }

        private void TestUploadFileHttpGenericError()
        {
            using var helper = HttpClientTestHelper.SimulateHttpError(503, "Service Unavailable");
            ValidateUploadFailure(helper, "http://example.com/unavailable");
        }

        #endregion

        #region Upload Success Tests

        private void TestUploadFileSuccess()
        {
            // Test with UTF-8 characters: Latin extended, Greek, Cyrillic, CJK, emoji
            // Ensures proper encoding/decoding through the entire upload path
            const string testContent = "Test upload with UTF-8: " +
                                       "Latin: café, naïve, Zürich\n" +
                                       "Greek: αβγδ, Ω\n" +
                                       "Cyrillic: Москва, Київ\n" +
                                       "CJK: 中文, 日本語, 한글\n" +
                                       "Emoji: 🔬 🧬 📊\n" +
                                       "Special: <>&\"'\n";
            using var helper = HttpClientTestHelper.SimulateSuccessfulUpload();
            
            var tempFile = TestContext.GetTestResultsPath("upload_test.txt");
            File.WriteAllText(tempFile, testContent, new UTF8Encoding(false)); // No BOM

            RunUI(() =>
            {
                using var dlg = new LongWaitDlg();
                dlg.PerformWork(SkylineWindow, 0, progressMonitor =>
                {
                    var progressStatus = new ProgressStatus("Uploading test file");
                    using var httpClient = new HttpClientWithProgress(progressMonitor, progressStatus);
                    httpClient.UploadFile(new Uri("http://example.com/upload"), "PUT", tempFile);
                });
                Assert.IsFalse(dlg.IsCanceled);
                
                // Verify uploaded data matches source file (including UTF-8 multi-byte sequences)
                var captureStream = helper.GetCaptureStream() as MemoryStream;
                Assert.IsNotNull(captureStream, "Should have capture stream for upload validation");
                var uploadedData = Encoding.UTF8.GetString(captureStream.ToArray());
                Assert.AreEqual(testContent, uploadedData, "Uploaded data should match source file content including UTF-8 characters");
            });
        }

        private void TestUploadDataSuccess()
        {
            // Test with UTF-8 multi-byte sequences and binary data
            // Ensures byte-level accuracy through upload path
            var testString = "UTF-8 bytes: café 中文 🔬, Binary: \0\x01\x02\xFF";
            var testData = Encoding.UTF8.GetBytes(testString);
            using var helper = HttpClientTestHelper.SimulateSuccessfulUpload();

            RunUI(() =>
            {
                using var dlg = new LongWaitDlg();
                dlg.PerformWork(SkylineWindow, 0, progressMonitor =>
                {
                    var progressStatus = new ProgressStatus("Uploading test data");
                    using var httpClient = new HttpClientWithProgress(progressMonitor, progressStatus);
                    httpClient.UploadData(new Uri("http://example.com/upload"), "PUT", testData);
                });
                Assert.IsFalse(dlg.IsCanceled);
                
                // Verify uploaded bytes match source (validates UTF-8 encoding preservation)
                var captureStream = helper.GetCaptureStream() as MemoryStream;
                Assert.IsNotNull(captureStream, "Should have capture stream for upload validation");
                var uploadedBytes = captureStream.ToArray();
                CollectionAssert.AreEqual(testData, uploadedBytes, "Uploaded bytes should match source including UTF-8 multi-byte sequences");
                
                // Also verify we can decode back to original string
                var uploadedString = Encoding.UTF8.GetString(uploadedBytes);
                Assert.AreEqual(testString, uploadedString, "UTF-8 decoding should recover original string");
            });
        }

        #endregion

        #region Upload Progress Tests

        private void TestUploadFileProgressReporting()
        {
            // Create temp file with 20KB data = ~3 chunks of 8192 bytes (multiple progress updates)
            // Progress reporting logic is shared with downloads via TransferStreamWithProgress,
            // so we just verify upload completes successfully with chunked reading
            var tempFile = TestContext.GetTestResultsPath("upload_progress_test.bin");
            var testData = new byte[20 * 1024];
            for (int i = 0; i < testData.Length; i++)
                testData[i] = (byte)(i % 256);
            File.WriteAllBytes(tempFile, testData);

            using var helper = HttpClientTestHelper.SimulateSuccessfulUpload();

            RunUI(() =>
            {
                using var dlg = new LongWaitDlg();
                var status = dlg.PerformWork(SkylineWindow, 0, progressMonitor =>
                {
                    var progressStatus = new ProgressStatus("Uploading large file");
                    using var httpClient = new HttpClientWithProgress(progressMonitor, progressStatus);
                    httpClient.UploadFile(new Uri("http://example.com/upload"), "PUT", tempFile);
                });

                Assert.IsFalse(status.IsCanceled);
                // Progress logic verified via shared TransferStreamWithProgress method (tested in downloads)
            });
        }

        private static void TestProgressMessageDoesNotRepeatSize()
        {
            // Regression test: Verify that GetProgressMessageWithSize doesn't append size repeatedly
            // Original bug: Each progress update would append size to the previous message,
            // resulting in "Downloading\n\n1 KB\n\n2 KB\n\n3 KB" instead of "Downloading\n\n3 KB"
            
            const string baseMessage = "Downloading test file";
            const long transferred = 5 * 1024 * 1024; // 5 MB
            const long total = 10 * 1024 * 1024; // 10 MB

            // Build message multiple times with same base - should produce identical results
            var message1 = HttpClientWithProgress.GetProgressMessageWithSize(baseMessage, transferred, total);
            var message2 = HttpClientWithProgress.GetProgressMessageWithSize(baseMessage, transferred, total);
            var message3 = HttpClientWithProgress.GetProgressMessageWithSize(baseMessage, transferred, total);

            Assert.AreEqual(message1, message2, "GetProgressMessageWithSize should be idempotent");
            Assert.AreEqual(message2, message3, "GetProgressMessageWithSize should be idempotent");

            // Verify structure: base message + blank line + size
            var lines = message1.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            Assert.IsTrue(lines.Length >= 3, "Expected at least 3 lines: base message, blank, size");
            Assert.AreEqual(baseMessage, lines[0], "First line should be base message");
            Assert.AreEqual(string.Empty, lines[1], "Second line should be blank");
            AssertEx.Contains(lines[2], "5"); // Should contain size info
            AssertEx.Contains(lines[2], "10"); // Should contain total size
            
            // Verify no repeated size information
            var messageText = message1;
            var sizeOccurrences = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("MB") || lines[i].Contains("KB"))
                    sizeOccurrences++;
            }
            Assert.AreEqual(1, sizeOccurrences, "Size should appear exactly once, not be repeated");
        }

        #endregion

        #region Upload Cancellation Tests

        private void TestUploadFileCancellationViaButton()
        {
            using var helper = HttpClientTestHelper.SimulateCancellationClickWithException();
            RunUI(() =>
            {
                var tempFile = TestContext.GetTestResultsPath("upload_cancel_test.txt");
                File.WriteAllText(tempFile, @"Test upload cancellation content");

                using var waitDlg = new LongWaitDlg();
                try
                {
                    waitDlg.PerformWork(SkylineWindow, 0, progressMonitor =>
                    {
                        var progressStatus = new ProgressStatus("Uploading file for cancellation test");
                        using var httpClient = new HttpClientWithProgress(progressMonitor, progressStatus);
                        httpClient.UploadFile(new Uri("http://example.com/upload"), "PUT", tempFile);
                    });
                }
                catch (Exception x)
                {
                    Assert.Fail($"Unexpected exception thrown {x.Message}");
                }
            });
        }

        #endregion

        #region Upload Helper Methods

        /// <summary>
        /// Most concise helper - validates upload failure using HttpClientTestHelper.
        /// The helper provides both the simulation and expected message for the IOException type.
        /// </summary>
        private void ValidateUploadFailure(HttpClientTestHelper helper, string urlText = null)
        {
            ValidateUploadFailure<IOException>(helper, urlText);
        }

        /// <summary>
        /// Helper function to test a failure in HttpClientWithProgress.UploadFile()
        /// with a type TEx exception.
        /// </summary>
        private void ValidateUploadFailure<TEx>(HttpClientTestHelper helper, string urlText = null)
            where TEx : Exception
        {
            var uri = new Uri(urlText ?? "http://example.com");
            ValidateUploadFileFailure<TEx>(uri, helper.GetExpectedMessage(uri));
        }

        /// <summary>
        /// Helper function to test a failure in HttpClientWithProgress.UploadFile()
        /// with a type TEx exception containing a message that references the requested URI.
        /// </summary>
        private void ValidateUploadFileFailure<TEx>(Uri uri, string message)
            where TEx : Exception
        {
            ValidateHttpClient<TEx>(progressMonitor =>
                {
                    var tempFile = TestContext.GetTestResultsPath("upload_failure_test.txt");
                    File.WriteAllText(tempFile, @"Test upload failure content");
                    
                    using var httpClient = new HttpClientWithProgress(progressMonitor);
                    httpClient.UploadFile(uri, "PUT", tempFile);
                },
                message);
        }

        #endregion
    }
}
