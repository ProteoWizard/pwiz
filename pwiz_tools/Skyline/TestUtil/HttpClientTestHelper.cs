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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using pwiz.Common.CommonResources;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// Test helper for HttpClientWithProgress integration testing.
    /// Allows simulation of various HTTP failure scenarios without making actual network requests.
    /// </summary>
    public class HttpClientTestHelper : IDisposable
    {
        private readonly HttpClientTestBehavior _behavior;
        private readonly Exception _simulatedException;
        private readonly HttpClientWithProgress.IHttpClientTestBehavior _originalTestBehavior;

        /// <summary>
        /// Creates a test helper that simulates the specified failure scenario.
        /// </summary>
        /// <param name="simulatedException">Exception to simulate (null for success)</param>
        public HttpClientTestHelper(Exception simulatedException)
            : this(new HttpClientTestBehavior { FailureException = simulatedException })
        {
        }

        /// <summary>
        /// Creates a test helper with custom test behavior.
        /// </summary>
        /// <param name="behavior">The test behavior to use</param>
        public HttpClientTestHelper(HttpClientTestBehavior behavior)
        {
            _behavior = behavior;
            _simulatedException = behavior.FailureException;
            
            // Store the original test behavior
            _originalTestBehavior = HttpClientWithProgress.TestBehavior;
            
            // Set up the test scenario
            HttpClientWithProgress.TestBehavior = behavior;
        }

        /// <summary>
        /// Simulates a DNS resolution failure (NameResolutionFailure)
        /// </summary>
        public static HttpClientTestHelper SimulateDnsFailure(string hostname = "nonexistent.example.com")
        {
            var webEx = new WebException($"The remote name could not be resolved: '{hostname}'", WebExceptionStatus.NameResolutionFailure);
            var httpEx = new HttpRequestException("An error occurred while sending the request.", webEx);
            return new HttpClientTestHelper(httpEx);
        }

        /// <summary>
        /// Simulates a network connection failure
        /// </summary>
        public static HttpClientTestHelper SimulateConnectionFailure()
        {
            var httpEx = new HttpRequestException("An error occurred while sending the request.");
            return new HttpClientTestHelper(httpEx);
        }

        /// <summary>
        /// Simulates a timeout during the request
        /// </summary>
        public static HttpClientTestHelper SimulateTimeout()
        {
            return new HttpClientTestHelper(new TimeoutException("The operation has timed out."));
        }

        private class ConnectionLossException : IOException
        {
            public ConnectionLossException()
                : base("The network path was not found.")
            {
                // Set a specific HResult that indicates network connection loss
                HResult = unchecked((int)0x8007006D);
            }
        }
        /// <summary>
        /// Simulates a network connection loss during download (specific HResult values)
        /// </summary>
        public static HttpClientTestHelper SimulateConnectionLoss()
        {
            var ioEx = new ConnectionLossException();
            return new HttpClientTestHelper(ioEx);
        }

        /// <summary>
        /// Simulates no network interface available
        /// </summary>
        public static HttpClientTestHelper SimulateNoNetworkInterface()
        {
            var noNetEx = new HttpClientWithProgress.NoNetworkTestException();
            return new HttpClientTestHelper(noNetEx);
        }

        /// <summary>
        /// Simulates operation cancellation
        /// </summary>
        public static HttpClientTestHelper SimulateCancellation()
        {
            var cancelEx = new OperationCanceledException();
            return new HttpClientTestHelper(cancelEx);
        }

        /// <summary>
        /// Simulates operation cancellation via a click exception treated as a user click
        /// </summary>
        public static HttpClientTestHelper SimulateCancellationClickWithException()
        {
            var cancelEx = new LongWaitDlg.CancelClickedTestException();
            return new HttpClientTestHelper(cancelEx);
        }

        /// <summary>
        /// Simulates an HTTP 404 Not Found response
        /// </summary>
        public static HttpClientTestHelper SimulateHttp404()
        {
            var httpEx = new HttpRequestException("Response status code does not indicate success: 404 (Not Found).");
            return new HttpClientTestHelper(httpEx);
        }

        /// <summary>
        /// Simulates an HTTP 401 Unauthorized response
        /// </summary>
        public static HttpClientTestHelper SimulateHttp401()
        {
            var httpEx = new HttpRequestException("Response status code does not indicate success: 401 (Unauthorized).");
            return new HttpClientTestHelper(httpEx);
        }

        /// <summary>
        /// Simulates an HTTP 403 Forbidden response
        /// </summary>
        public static HttpClientTestHelper SimulateHttp403()
        {
            var httpEx = new HttpRequestException("Response status code does not indicate success: 403 (Forbidden).");
            return new HttpClientTestHelper(httpEx);
        }

        /// <summary>
        /// Simulates an HTTP 500 Internal Server Error response
        /// </summary>
        public static HttpClientTestHelper SimulateHttp500()
        {
            var httpEx = new HttpRequestException("Response status code does not indicate success: 500 (Internal Server Error).");
            return new HttpClientTestHelper(httpEx);
        }

        /// <summary>
        /// Simulates an HTTP 429 Too Many Requests response
        /// </summary>
        public static HttpClientTestHelper SimulateHttp429()
        {
            var httpEx = new HttpRequestException("Response status code does not indicate success: 429 (Too Many Requests).");
            return new HttpClientTestHelper(httpEx);
        }

        /// <summary>
        /// Simulates an HTTP error response with a custom status code
        /// </summary>
        public static HttpClientTestHelper SimulateHttpError(int statusCode, string reasonPhrase)
        {
            var httpEx = new HttpRequestException($"Response status code does not indicate success: {statusCode} ({reasonPhrase}).");
            return new HttpClientTestHelper(httpEx);
        }

        /// <summary>
        /// Creates a test helper that simulates a successful download with mock data.
        /// </summary>
        /// <param name="mockData">The data to return from the download</param>
        /// <param name="simulateProgress">If true, simulate chunked reading for progress reporting</param>
        public static HttpClientTestHelper SimulateSuccessfulDownload(byte[] mockData, bool simulateProgress = false)
        {
            var behavior = new HttpClientTestBehavior
            {
                MockResponseData = mockData,
                SimulateProgress = simulateProgress
            };
            return new HttpClientTestHelper(behavior);
        }

        /// <summary>
        /// Creates a test helper that simulates a successful download with mock string data.
        /// </summary>
        public static HttpClientTestHelper SimulateSuccessfulDownload(string mockData, bool simulateProgress = false)
        {
            return SimulateSuccessfulDownload(Encoding.UTF8.GetBytes(mockData), simulateProgress);
        }

        /// <summary>
        /// Creates a test helper that simulates a successful upload without network access.
        /// Upload operations will write to the provided capture stream instead of network.
        /// Tests can verify the captured data matches what was uploaded.
        /// </summary>
        /// <param name="captureStream">Stream to capture uploaded data for validation. If null, creates a new MemoryStream.</param>
        /// <param name="simulateProgress">If true, simulates chunked processing for progress reporting tests</param>
        /// <returns>Test helper with the capture stream available via GetCaptureStream()</returns>
        public static HttpClientTestHelper SimulateSuccessfulUpload(Stream captureStream = null, bool simulateProgress = false)
        {
            captureStream ??= new MemoryStream();
            var behavior = new HttpClientTestBehavior
            {
                MockUploadCaptureStream = captureStream,
                SimulateProgress = simulateProgress
            };
            return new HttpClientTestHelper(behavior);
        }

        /// <summary>
        /// Creates a test helper with mock responses from a dictionary mapping URIs to streams.
        /// Most flexible option - supports multiple URIs and custom stream types.
        /// </summary>
        /// <param name="responses">Dictionary mapping URIs to response streams</param>
        public static HttpClientTestHelper WithMockResponses(Dictionary<Uri, Stream> responses)
        {
            var behavior = new HttpClientTestBehavior
            {
                MockResponseMap = responses
            };
            return new HttpClientTestHelper(behavior);
        }

        /// <summary>
        /// Creates a test helper with mock responses from URL strings to file paths.
        /// Convenience method for common pattern of serving test data files.
        /// </summary>
        /// <param name="urlToFilePath">Dictionary mapping URL strings to file paths</param>
        public static HttpClientTestHelper WithMockResponseFiles(Dictionary<Uri, string> urlToFilePath)
        {
            var streams = urlToFilePath.ToDictionary(
                kvp => kvp.Key,
                kvp => (Stream)File.OpenRead(kvp.Value)
            );
            return WithMockResponses(streams);
        }

        /// <summary>
        /// Creates a test helper with a single mock response file.
        /// Most concise option for single-file test scenarios.
        /// </summary>
        /// <param name="url">The URL to mock</param>
        /// <param name="filePath">Path to the file containing response data</param>
        public static HttpClientTestHelper WithMockResponseFile(Uri url, string filePath)
        {
            return WithMockResponseFiles(new Dictionary<Uri, string> { { url, filePath } });
        }

        /// <summary>
        /// Creates a test helper with a single mock response string.
        /// Convenience method for inline test data (JSON, XML, etc.).
        /// </summary>
        /// <param name="url">The URL to mock</param>
        /// <param name="data">The response data as a string</param>
        public static HttpClientTestHelper WithMockResponseString(Uri url, string data)
        {
            return WithMockResponses(new Dictionary<Uri, Stream>
            {
                { url, new MemoryStream(Encoding.UTF8.GetBytes(data)) }
            });
        }

        /// <summary>
        /// Creates a test helper with mock upload capture streams.
        /// Upload operations will write to the mapped streams instead of network.
        /// </summary>
        /// <param name="uploadCaptures">Dictionary mapping URIs to capture streams</param>
        public static HttpClientTestHelper WithMockUploadCapture(Dictionary<Uri, Stream> uploadCaptures)
        {
            var behavior = new HttpClientTestBehavior
            {
                MockUploadMap = uploadCaptures
            };
            return new HttpClientTestHelper(behavior);
        }

        /// <summary>
        /// Creates a test helper with a single upload capture stream.
        /// Convenience method for single-upload test scenarios.
        /// </summary>
        /// <param name="url">The URL to capture uploads for</param>
        /// <param name="captureStream">Output parameter - the stream that will capture uploaded data</param>
        public static HttpClientTestHelper WithUploadCapture(string url, out Stream captureStream)
        {
            captureStream = new MemoryStream();
            return WithMockUploadCapture(new Dictionary<Uri, Stream>
            {
                { new Uri(url), captureStream }
            });
        }

        /// <summary>
        /// Gets the stream that captured uploaded data during a simulated upload.
        /// Use this to verify the uploaded data matches expectations.
        /// </summary>
        /// <returns>The capture stream, or null if not in upload simulation mode</returns>
        public Stream GetCaptureStream()
        {
            return _behavior?.MockUploadCaptureStream;
        }

        /// <summary>
        /// Gets the captured stream for a specific URI from the upload map.
        /// Use this when testing multiple uploads to different URIs.
        /// </summary>
        /// <param name="uri">The URI to get the capture stream for</param>
        public Stream GetCaptureStream(Uri uri)
        {
            if (_behavior?.MockUploadMap != null && _behavior.MockUploadMap.TryGetValue(uri, out var stream))
                return stream;
            return _behavior?.MockUploadCaptureStream;
        }

        #region Expected message helpers

        /// <summary>
        /// Gets the expected error message for this simulation based on the exception type.
        /// </summary>
        /// <param name="uri">Optional URI for messages that include URL/hostname in the error</param>
        /// <returns>The expected user-facing error message</returns>
        public string GetExpectedMessage(Uri uri = null)
        {
            if (_simulatedException == null)
                return null; // Success case, no error message
            
            // Check exception type and return appropriate message
            if (_simulatedException is HttpClientWithProgress.NoNetworkTestException)
                return GetNoNetworkInterfaceMessage();
            
            if (_simulatedException is ConnectionLossException)
                return GetConnectionLossMessage();
            
            if (_simulatedException is LongWaitDlg.CancelClickedTestException)
                return null; // User cancellation - no message shown
            
            if (_simulatedException is OperationCanceledException)
                return new OperationCanceledException().Message; // System cancellation message
            
            if (_simulatedException is TimeoutException)
                return uri != null ? GetTimeoutMessage(uri) : null;
            
            // HttpRequestException - check message for HTTP status codes
            if (_simulatedException is HttpRequestException httpEx)
            {
                if (uri == null)
                    return null;
                
                var message = httpEx.Message;
                if (message.Contains("404"))
                    return GetHttp404Message(uri);
                if (message.Contains("401"))
                    return GetHttp401Message(uri);
                if (message.Contains("403"))
                    return GetHttp403Message(uri);
                if (message.Contains("500"))
                    return GetHttp500Message(uri);
                if (message.Contains("429"))
                    return GetHttp429Message(uri);
                if (message.Contains("503"))
                    return GetHttp503Message(uri);
                
                // Check for WebException inner exception
                if (httpEx.InnerException is WebException webEx)
                {
                    if (webEx.Status == WebExceptionStatus.NameResolutionFailure)
                        return GetDnsFailureMessage(uri);
                }
                
                // Generic connection failure
                return GetConnectionFailureMessage(uri);
            }
            
            return null;
        }

        /// <summary>
        /// Gets the expected error message for no network interface scenario.
        /// </summary>
        public static string GetNoNetworkInterfaceMessage()
        {
            return MessageResources.HttpClientWithProgress_MapHttpException_No_network_connection_detected__Please_check_your_internet_connection_and_try_again_;
        }

        /// <summary>
        /// Gets the expected error message for connection loss scenario.
        /// </summary>
        public static string GetConnectionLossMessage()
        {
            return MessageResources.HttpClientWithProgress_MapHttpException_The_connection_was_lost_during_download__Please_check_your_internet_connection_and_try_again_;
        }

        /// <summary>
        /// Gets the expected error message for connection failure.
        /// </summary>
        /// <param name="uri">The URI that failed (uses Host)</param>
        public static string GetConnectionFailureMessage(Uri uri)
        {
            return string.Format(
                MessageResources.HttpClientWithProgress_MapHttpException_Failed_to_connect_to__0___Please_check_your_network_connection__VPN_proxy__or_firewall_,
                uri.Host);
        }

        /// <summary>
        /// Gets the expected error message for DNS failure.
        /// </summary>
        /// <param name="uri">The URI that failed (uses Host)</param>
        public static string GetDnsFailureMessage(Uri uri)
        {
            return string.Format(
                MessageResources.HttpClientWithProgress_MapHttpException_Failed_to_resolve_host__0___Please_check_your_DNS_settings_or_VPN_proxy_,
                uri.Host);
        }

        /// <summary>
        /// Gets the expected error message for timeout.
        /// </summary>
        /// <param name="uri">The URI that timed out (uses full URI)</param>
        public static string GetTimeoutMessage(Uri uri)
        {
            return string.Format(
                MessageResources.HttpClientWithProgress_MapHttpException_The_request_to__0__timed_out__Please_try_again_,
                uri);
        }

        /// <summary>
        /// Gets the expected error message for HTTP 404 Not Found.
        /// </summary>
        /// <param name="uri">The URI that was not found (uses full URI)</param>
        public static string GetHttp404Message(Uri uri)
        {
            return string.Format(
                MessageResources.HttpClientWithProgress_MapHttpException_The_requested_resource_at__0__was_not_found__HTTP_404___Please_verify_the_URL_,
                uri);
        }

        /// <summary>
        /// Gets the expected error message for HTTP 401 Unauthorized.
        /// </summary>
        /// <param name="uri">The URI that denied access (uses full URI)</param>
        public static string GetHttp401Message(Uri uri)
        {
            return string.Format(
                MessageResources.HttpClientWithProgress_MapHttpException_Access_to__0__was_denied__HTTP_401___Authentication_may_be_required_,
                uri);
        }

        /// <summary>
        /// Gets the expected error message for HTTP 403 Forbidden.
        /// </summary>
        /// <param name="uri">The URI that was forbidden (uses full URI)</param>
        public static string GetHttp403Message(Uri uri)
        {
            return string.Format(
                MessageResources.HttpClientWithProgress_MapHttpException_Access_to__0__was_forbidden__HTTP_403___You_may_not_have_permission_to_access_this_resource_,
                uri);
        }

        /// <summary>
        /// Gets the expected error message for HTTP 500 Internal Server Error.
        /// </summary>
        /// <param name="uri">The URI that encountered an error (uses Host)</param>
        public static string GetHttp500Message(Uri uri)
        {
            return string.Format(
                MessageResources.HttpClientWithProgress_MapHttpException_The_server__0__encountered_an_internal_error__HTTP_500___Please_try_again_later_or_contact_the_server_administrator_,
                uri.Host);
        }

        /// <summary>
        /// Gets the expected error message for HTTP 429 Too Many Requests.
        /// </summary>
        /// <param name="uri">The URI that was rate limited (uses Host)</param>
        public static string GetHttp429Message(Uri uri)
        {
            return string.Format(
                MessageResources.HttpClientWithProgress_MapHttpException_Too_many_requests_to__0___HTTP_429___Please_wait_before_trying_again_,
                uri.Host);
        }

        /// <summary>
        /// Gets the expected error message for HTTP 503 Service Unavailable.
        /// </summary>
        /// <param name="uri">The URI that was unavailable (uses Host)</param>
        public static string GetHttp503Message(Uri uri)
        {
            return string.Format(
                MessageResources.HttpClientWithProgress_MapHttpException_The_server__0__returned_an_error__HTTP__1____Please_try_again_or_contact_support_,
                uri.Host, 503);
        }

        #endregion

        public void Dispose()
        {
            // Restore the original test behavior
            HttpClientWithProgress.TestBehavior = _originalTestBehavior;
        }
    }

    /// <summary>
    /// Implementation of HttpClientWithProgress.IHttpClientTestBehavior for testing HttpClientWithProgress.
    /// </summary>
    public class HttpClientTestBehavior : HttpClientWithProgress.IHttpClientTestBehavior
    {
        public Exception FailureException { get; set; }
        public byte[] MockResponseData { get; set; }
        public Stream MockUploadCaptureStream { get; set; }
        public Dictionary<Uri, Stream> MockResponseMap { get; set; }
        public Dictionary<Uri, Stream> MockUploadMap { get; set; }
        public bool SimulateProgress { get; set; }
        public Action OnProgressCallback { get; set; }

        public Stream GetMockUploadStream(Uri uri)
        {
            // Check URI map first (more flexible)
            if (MockUploadMap != null && MockUploadMap.TryGetValue(uri, out var mappedStream))
                return mappedStream;
            
            // Fall back to single capture stream (backward compatible)
            return MockUploadCaptureStream;
        }

        public Stream GetMockResponseStream(Uri uri, out long contentLength)
        {
            // Check URI map first (more flexible, supports multiple URIs)
            if (MockResponseMap != null && MockResponseMap.TryGetValue(uri, out var mappedStream))
            {
                contentLength = mappedStream.CanSeek ? mappedStream.Length : 0;
                return mappedStream;
            }

            // Fall back to single response data (backward compatible)
            if (MockResponseData == null)
            {
                contentLength = 0;
                return null;
            }

            contentLength = MockResponseData.Length;
            
            if (SimulateProgress)
            {
                // Return a stream that simulates chunked reading for progress reporting
                return new ProgressSimulatingStream(MockResponseData, OnProgressCallback);
            }
            
            return new MemoryStream(MockResponseData);
        }

        /// <summary>
        /// Stream that simulates chunked reading to test progress reporting.
        /// </summary>
        private class ProgressSimulatingStream : Stream
        {
            private readonly byte[] _data;
            private readonly Action _onProgressCallback;
            private int _position;
            private const int ChunkSize = 8192; // Match HttpClientWithProgress buffer size

            public ProgressSimulatingStream(byte[] data, Action onProgressCallback)
            {
                _data = data;
                _onProgressCallback = onProgressCallback;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _data.Length;
            public override long Position
            {
                get => _position;
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_position >= _data.Length)
                    return 0;

                // Read in chunks to simulate network behavior
                int bytesToRead = Math.Min(Math.Min(count, ChunkSize), _data.Length - _position);
                Array.Copy(_data, _position, buffer, offset, bytesToRead);
                _position += bytesToRead;

                // Invoke progress callback after each chunk
                _onProgressCallback?.Invoke();

                return bytesToRead;
            }

            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
