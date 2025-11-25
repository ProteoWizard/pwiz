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
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using pwiz.Common.CommonResources;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Util.Extensions;

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
            // This is real and has been seen in a debugger. The InnerException is a WebException
            // HttpClient appears to use HttpWebRequest, but wrap its exceptions in HttpRequestException
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
        /// Begins recording live HTTP interactions for later playback.
        /// </summary>
        public static HttpClientTestHelper BeginRecording(HttpInteractionRecorder recorder)
        {
            if (recorder == null)
                throw new ArgumentNullException(nameof(recorder));
            var behavior = new RecordingHttpClientBehavior(recorder);
            return new HttpClientTestHelper(behavior);
        }

        /// <summary>
        /// Provides offline playback using previously recorded HTTP interactions.
        /// </summary>
        public static HttpClientTestHelper PlaybackFromInteractions(IEnumerable<HttpInteraction> interactions,
            ICollection<HttpInteraction> captureInteractions = null)
        {
            if (interactions == null)
                throw new ArgumentNullException(nameof(interactions));
            var behavior = new PlaybackHttpClientBehavior(interactions, captureInteractions);
            return new HttpClientTestHelper(behavior);
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
        public Func<Uri, Stream> ResponseFactory { get; set; }
        public Func<HttpRequestMessage, Stream> RequestResponseFactory { get; set; }

        public virtual void OnResponse(Uri uri, HttpResponseMessage response)
        {
        }

        public virtual Stream WrapResponseStream(Uri uri, Stream responseStream, long contentLength)
        {
            return responseStream;
        }

        public virtual void OnFailedResponse(Uri uri, HttpResponseMessage response, string responseBody, Exception exception)
        {
        }

        public virtual void OnException(Uri uri, Exception exception)
        {
        }

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
            if (MockResponseData != null)
            {
                contentLength = MockResponseData.Length;
                
                if (SimulateProgress)
                {
                    // Return a stream that simulates chunked reading for progress reporting
                    return new ProgressSimulatingStream(MockResponseData, OnProgressCallback);
                }
                
                return new MemoryStream(MockResponseData);
            }

            if (ResponseFactory != null)
            {
                var stream = ResponseFactory(uri);
                if (stream != null)
                {
                    if (stream.CanSeek)
                    {
                        stream.Position = 0;
                        contentLength = stream.Length;
                    }
                    else
                    {
                        contentLength = 0;
                    }
                    return stream;
                }
            }

            contentLength = 0;
            return null;
        }

        public Stream GetMockResponseStreamFromRequest(HttpRequestMessage request)
        {
            if (request == null)
                return null;

            // Prefer RequestResponseFactory if available (supports authorization header lookup)
            if (RequestResponseFactory != null)
            {
                var stream = RequestResponseFactory(request);
                if (stream != null)
                {
                    return stream;
                }
            }

            // Fall back to URL-based lookup
            if (request.RequestUri != null)
            {
                return GetMockResponseStream(request.RequestUri, out _);
            }

            return null;
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

    internal class RecordingHttpClientBehavior : HttpClientTestBehavior
    {
        private readonly HttpInteractionRecorder _recorder;

        public RecordingHttpClientBehavior(HttpInteractionRecorder recorder)
        {
            _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        }

        public override void OnResponse(Uri uri, HttpResponseMessage response)
        {
            _recorder.StartResponse(uri, response);
        }

        public override Stream WrapResponseStream(Uri uri, Stream responseStream, long contentLength)
        {
            var entry = _recorder.DequeuePendingResponse();
            if (entry != null)
                return _recorder.WrapResponseStream(entry, responseStream);
            return base.WrapResponseStream(uri, responseStream, contentLength);
        }

        public override void OnFailedResponse(Uri uri, HttpResponseMessage response, string responseBody, Exception exception)
        {
            var entry = _recorder.DequeuePendingResponse();
            _recorder.RecordFailedResponse(entry, uri, response, responseBody, exception);
        }

        public override void OnException(Uri uri, Exception exception)
        {
            _recorder.RecordException(uri, exception);
        }
    }

    internal class PlaybackHttpClientBehavior : HttpClientTestBehavior
    {
        private readonly Dictionary<string, HttpInteraction> _responses;
        private readonly ICollection<HttpInteraction> _captureInteractions;

        public PlaybackHttpClientBehavior(IEnumerable<HttpInteraction> interactions,
            ICollection<HttpInteraction> captureInteractions = null)
        {
            _responses = BuildResponseMap(interactions);
            _captureInteractions = captureInteractions;
            ResponseFactory = HandleResponse;
            RequestResponseFactory = HandleRequestResponse;
        }

        private static Dictionary<string, HttpInteraction> BuildResponseMap(IEnumerable<HttpInteraction> interactions)
        {
            var map = new Dictionary<string, HttpInteraction>(StringComparer.Ordinal);
            foreach (var interaction in interactions)
            {
                if (interaction?.Url == null)
                    continue;
                // Use both URL and authorization header as lookup key to distinguish between
                // different authentication contexts for the same URL (e.g., authenticated vs anonymous)
                var lookupKey = BuildLookupKey(interaction.Url, interaction.Authorization);
                // Store one interaction per URL+Authorization combination
                // If the same combination appears multiple times, use the first one
                if (!map.ContainsKey(lookupKey))
                {
                    map.Add(lookupKey, interaction);
                }
            }
            return map;
        }

        private static string BuildLookupKey(string url, string authorization)
        {
            // Use a separator that's unlikely to appear in URLs or auth headers
            // Empty authorization is treated as null for consistency
            var authKey = string.IsNullOrEmpty(authorization) ? string.Empty : authorization;
            return $"{url}#AUTH:{authKey}";
        }

        private Stream HandleResponse(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            // For backward compatibility with old recordings, try without authorization first
            var urlKey = uri.ToString();
            var lookupKey = BuildLookupKey(urlKey, null);
            if (!_responses.TryGetValue(lookupKey, out var interaction))
            {
                throw new InvalidOperationException($"Unexpected URL during playback: {uri}");
            }

            CaptureInteraction(interaction);
            if (!string.IsNullOrEmpty(interaction.ExceptionType))
                throw CreateException(uri, interaction);

            return GetResponseStream(interaction);
        }

        private Stream HandleRequestResponse(HttpRequestMessage request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var uri = request.RequestUri;
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            // Extract Authorization header from request
            string authorization = null;
            if (request.Headers?.Authorization != null)
            {
                var authScheme = request.Headers.Authorization.Scheme;
                var authParameter = request.Headers.Authorization.Parameter;
                if (!string.IsNullOrEmpty(authScheme) && !string.IsNullOrEmpty(authParameter))
                {
                    authorization = $"{authScheme} {authParameter}";
                }
            }

            var urlKey = uri.ToString();
            var lookupKey = BuildLookupKey(urlKey, authorization);
            
            // Fallback to lookup without authorization for backward compatibility
            if (!_responses.TryGetValue(lookupKey, out var interaction))
            {
                lookupKey = BuildLookupKey(urlKey, null);
                if (!_responses.TryGetValue(lookupKey, out interaction))
                {
                    throw new InvalidOperationException($"Unexpected URL during playback: {uri} (Authorization: {authorization ?? "none"})");
                }
            }

            CaptureInteraction(interaction);
            if (!string.IsNullOrEmpty(interaction.ExceptionType))
                throw CreateException(uri, interaction);

            return GetResponseStream(interaction);
        }

        private static Stream GetResponseStream(HttpInteraction interaction)
        {
            // Note: ResponseBodyIndex should have been resolved during deserialization,
            // but handle it defensively here as well
            if (interaction == null)
                throw new ArgumentNullException(nameof(interaction));

            if (interaction.ResponseBodyIndex.HasValue)
            {
                throw new InvalidOperationException(
                    $"ResponseBodyIndex {interaction.ResponseBodyIndex.Value} was not resolved during deserialization. " +
                    "This should not happen if LoadHttpInteractionsForType was used.");
            }

            byte[] responseBytes;
            if (interaction.ResponseBodyIsBase64)
            {
                // Decode base64-encoded binary content
                // If ResponseBodyLines is present, join lines first (line breaks are ignored in base64)
                string base64String;
                if (interaction.ResponseBodyLines != null && interaction.ResponseBodyLines.Count > 0)
                {
                    base64String = string.Join("", interaction.ResponseBodyLines);
                }
                else
                {
                    base64String = interaction.ResponseBody ?? string.Empty;
                }
                responseBytes = string.IsNullOrEmpty(base64String)
                    ? Array.Empty<byte>()
                    : Convert.FromBase64String(base64String);
            }
            else
            {
                // Reconstruct text from ResponseBodyLines if available (for better readability in JSON)
                // Otherwise use ResponseBody directly
                string text;
                if (interaction.ResponseBodyLines != null && interaction.ResponseBodyLines.Count > 0)
                {
                    text = string.Join("\n", interaction.ResponseBodyLines);
                }
                else
                {
                    text = interaction.ResponseBody ?? string.Empty;
                }
                responseBytes = Encoding.UTF8.GetBytes(text);
            }
            
            return new MemoryStream(responseBytes);
        }

        private void CaptureInteraction(HttpInteraction interaction)
        {
            if (_captureInteractions == null || interaction == null)
                return;
            _captureInteractions.Add(interaction.Clone());
        }

        private static Exception CreateException(Uri uri, HttpInteraction interaction)
        {
            if (interaction.ExceptionType == typeof(NetworkRequestException).FullName)
            {
                if (interaction.StatusCode.HasValue)
                {
                    return new NetworkRequestException(
                        interaction.ExceptionMessage ?? $"Recorded HTTP error for {uri}",
                        (HttpStatusCode)interaction.StatusCode.Value,
                        uri,
                        new HttpRequestException(interaction.ExceptionMessage ?? $"Recorded HTTP error for {uri}"),
                        interaction.ResponseBody);
                }

                if (!string.IsNullOrEmpty(interaction.FailureType) &&
                    Enum.TryParse(interaction.FailureType, true, out NetworkFailureType failureType) &&
                    failureType != NetworkFailureType.HttpError)
                {
                    return new NetworkRequestException(
                        interaction.ExceptionMessage ?? $"Recorded network error for {uri}",
                        failureType,
                        uri,
                        new IOException(interaction.ExceptionMessage ?? $"Recorded network error for {uri}"));
                }
            }

            var exceptionType = interaction.ExceptionType != null ? Type.GetType(interaction.ExceptionType) : null;
            if (exceptionType != null && typeof(Exception).IsAssignableFrom(exceptionType))
            {
                try
                {
                    return (Exception)Activator.CreateInstance(exceptionType, interaction.ExceptionMessage);
                }
                catch
                {
                    try
                    {
                        return (Exception)Activator.CreateInstance(exceptionType);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            return new Exception(interaction.ExceptionMessage ?? $"Recorded exception: {interaction.ExceptionType}");
        }
    }

    public class HttpInteraction
    {
        public string Url { get; set; }
        public string Method { get; set; }
        /// <summary>
        /// Authorization header value (e.g., "Basic base64credentials" or "Bearer token").
        /// Used to distinguish between different authentication contexts for the same URL during playback.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Authorization { get; set; }
        public int? StatusCode { get; set; }
        public string ContentType { get; set; }
        public string ResponseBody { get; set; }
        public bool ResponseBodyIsBase64 { get; set; }
        /// <summary>
        /// Alternative representation of ResponseBody as an array of lines for better JSON readability.
        /// Used when ResponseBody contains newlines and is text (not base64).
        /// During deserialization, if this is set, ResponseBody is reconstructed by joining lines.
        /// </summary>
        public List<string> ResponseBodyLines { get; set; }
        /// <summary>
        /// Index reference to a previously recorded response body with identical content.
        /// If set, ResponseBody and ResponseBodyLines should be null/empty as they are referenced by index.
        /// Used to deduplicate identical responses and reduce file size.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? ResponseBodyIndex { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ExceptionType { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ExceptionMessage { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string FailureType { get; set; }

        /// <summary>
        /// Suppresses ResponseBody serialization when ResponseBodyLines is present to avoid duplication.
        /// </summary>
        public bool ShouldSerializeResponseBody()
        {
            // Don't serialize ResponseBody if we have ResponseBodyLines (avoids duplication in JSON)
            // Also don't serialize if ResponseBodyIndex is set (response body is referenced by index)
            return (ResponseBodyLines == null || ResponseBodyLines.Count == 0) && 
                   !ResponseBodyIndex.HasValue;
        }

        /// <summary>
        /// Suppresses ResponseBodyLines serialization when ResponseBodyIndex is set.
        /// </summary>
        public bool ShouldSerializeResponseBodyLines()
        {
            // Don't serialize ResponseBodyLines if ResponseBodyIndex is set (response body is referenced by index)
            return !ResponseBodyIndex.HasValue;
        }

        public HttpInteraction Clone()
        {
            return (HttpInteraction) MemberwiseClone();
        }
    }

    /// <summary>
    /// Records HTTP traffic for use in offline playback.
    /// </summary>
    public class HttpInteractionRecorder
    {
        internal class RecordingEntry
        {
            public RecordingEntry(HttpInteraction interaction)
            {
                Interaction = interaction;
                ResponseBuffer = new MemoryStream();
            }

            public HttpInteraction Interaction { get; }
            public MemoryStream ResponseBuffer { get; }
            public bool Completed { get; set; }
        }

        private readonly List<HttpInteraction> _interactions = new List<HttpInteraction>();
        private readonly Queue<RecordingEntry> _pendingResponses = new Queue<RecordingEntry>();
        private readonly object _lock = new object();

        public IReadOnlyList<HttpInteraction> Interactions
        {
            get
            {
                lock (_lock)
                {
                    return _interactions.ToList();
                }
            }
        }

        internal RecordingEntry StartResponse(Uri uri, HttpResponseMessage response)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));
            if (response == null)
                throw new ArgumentNullException(nameof(response));

            // Extract Authorization header from request for playback lookup
            string authorization = null;
            if (response.RequestMessage?.Headers?.Authorization != null)
            {
                var authScheme = response.RequestMessage.Headers.Authorization.Scheme;
                var authParameter = response.RequestMessage.Headers.Authorization.Parameter;
                if (!string.IsNullOrEmpty(authScheme) && !string.IsNullOrEmpty(authParameter))
                {
                    authorization = $"{authScheme} {authParameter}";
                }
            }

            var urlString = uri.ToString();
            
            var interaction = new HttpInteraction
            {
                Url = urlString,
                Method = response.RequestMessage?.Method?.Method,
                Authorization = authorization,
                StatusCode = (int)response.StatusCode,
                ContentType = response.Content?.Headers?.ContentType?.ToString()
            };
            var entry = new RecordingEntry(interaction);

            lock (_lock)
            {
                _interactions.Add(interaction);
                _pendingResponses.Enqueue(entry);
            }

            return entry;
        }

        internal RecordingEntry DequeuePendingResponse()
        {
            lock (_lock)
            {
                if (_pendingResponses.Count == 0)
                    return null;
                return _pendingResponses.Dequeue();
            }
        }

        internal Stream WrapResponseStream(RecordingEntry entry, Stream responseStream)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (responseStream == null)
                throw new ArgumentNullException(nameof(responseStream));
            return new RecordingStream(responseStream, entry, this);
        }

        internal void RecordFailedResponse(RecordingEntry entry, Uri uri, HttpResponseMessage response, string responseBody, Exception exception)
        {
            if (entry == null)
            {
                var interaction = new HttpInteraction
                {
                    Url = uri?.ToString(),
                    Method = response?.RequestMessage?.Method?.Method,
                    ContentType = response?.Content?.Headers?.ContentType?.ToString(),
                    StatusCode = response != null ? (int)response.StatusCode : (int?)null
                };
                entry = new RecordingEntry(interaction);
                lock (_lock)
                {
                    _interactions.Add(interaction);
                }
            }

            entry.Interaction.ResponseBody = responseBody;
            entry.Interaction.ResponseBodyIsBase64 = false; // responseBody is already a string (text)
            // If text contains newlines, also store as array of lines for better JSON readability
            if (!string.IsNullOrEmpty(responseBody) && responseBody.Contains("\n"))
            {
                entry.Interaction.ResponseBodyLines = responseBody.ReadLines().ToList();
            }
            else
            {
                entry.Interaction.ResponseBodyLines = null;
            }
            UpdateException(entry.Interaction, exception);
            entry.Completed = true;
        }

        internal void RecordException(Uri uri, Exception exception)
        {
            if (exception == null)
                return;

            RecordingEntry pendingEntry = null;
            HttpInteraction interaction;
            lock (_lock)
            {
                if (_pendingResponses.Count > 0)
                    pendingEntry = _pendingResponses.Dequeue();

                interaction = pendingEntry?.Interaction ?? _interactions.LastOrDefault(i => Equals(i.Url, uri?.ToString()));

                if (pendingEntry == null && interaction != null && !string.IsNullOrEmpty(interaction.ExceptionType))
                    return;

                if (pendingEntry != null && !_interactions.Contains(pendingEntry.Interaction))
                    _interactions.Add(pendingEntry.Interaction);

                if (interaction == null)
                {
                    interaction = new HttpInteraction
                    {
                        Url = uri?.ToString()
                    };
                    _interactions.Add(interaction);
                }
            }

            if (pendingEntry != null)
            {
                UpdateException(pendingEntry.Interaction, exception);
                AssignResponseBody(pendingEntry.Interaction, pendingEntry.ResponseBuffer.ToArray());
                pendingEntry.Completed = true;
            }
            else
            {
                UpdateException(interaction, exception);
            }
        }

        private static void UpdateException(HttpInteraction interaction, Exception exception)
        {
            if (interaction == null || exception == null)
                return;

            interaction.ExceptionType = exception.GetType().FullName;
            interaction.ExceptionMessage = exception.Message;

            if (exception is NetworkRequestException networkException)
            {
                interaction.StatusCode = networkException.StatusCode != null ? (int)networkException.StatusCode : interaction.StatusCode;
                interaction.FailureType = networkException.FailureType.ToString();
                if (!string.IsNullOrEmpty(networkException.ResponseBody))
                {
                    interaction.ResponseBody = networkException.ResponseBody;
                    interaction.ResponseBodyIsBase64 = false; // networkException.ResponseBody is already a string (text)
                    // If text contains newlines, also store as array of lines for better JSON readability
                    if (networkException.ResponseBody.Contains("\n"))
                    {
                        interaction.ResponseBodyLines = networkException.ResponseBody.ReadLines().ToList();
                    }
                    else
                    {
                        interaction.ResponseBodyLines = null;
                    }
                }
            }
        }

        internal void CompleteSuccess(RecordingEntry entry)
        {
            if (entry == null)
                return;

            lock (_lock)
            {
                if (entry.Completed)
                    return;

                entry.Completed = true;
                AssignResponseBody(entry.Interaction, entry.ResponseBuffer.ToArray());
            }
        }

        private static void AssignResponseBody(HttpInteraction interaction, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                interaction.ResponseBody = string.Empty;
                interaction.ResponseBodyIsBase64 = false;
                return;
            }

            // Check if content is binary based on ContentType
            bool isBinary = IsBinaryContentType(interaction.ContentType);
            
            // If not determined by ContentType, try to decode as UTF-8 to detect binary
            if (!isBinary)
            {
                try
                {
                    var testString = Encoding.UTF8.GetString(bytes);
                    // Verify round-trip: if re-encoding doesn't match, it's likely binary
                    var reencoded = Encoding.UTF8.GetBytes(testString);
                    isBinary = !bytes.SequenceEqual(reencoded);
                }
                catch
                {
                    isBinary = true; // If decoding fails, treat as binary
                }
            }

            if (isBinary)
            {
                // Encode binary content as base64 and split into lines for better JSON readability
                // (similar to how .NET .resx files format base64 content)
                var base64String = Convert.ToBase64String(bytes);
                interaction.ResponseBody = base64String;
                interaction.ResponseBodyIsBase64 = true;
                // Split base64 into lines (76 characters per line is a common standard)
                interaction.ResponseBodyLines = SplitBase64IntoLines(base64String);
            }
            else
            {
                // Store text content as UTF-8 string
                var text = Encoding.UTF8.GetString(bytes);
                interaction.ResponseBody = text;
                interaction.ResponseBodyIsBase64 = false;
                
                // If text contains newlines, also store as array of lines for better JSON readability
                if (text.Contains("\n"))
                {
                    interaction.ResponseBodyLines = text.ReadLines().ToList();
                }
                else
                {
                    interaction.ResponseBodyLines = null; // Single line, no need for array
                }
            }
        }

        private static List<string> SplitBase64IntoLines(string base64String)
        {
            if (string.IsNullOrEmpty(base64String))
                return new List<string>();
            
            const int lineLength = 76; // Common base64 line length (matches .NET .resx files)
            var lines = new List<string>();
            
            for (int i = 0; i < base64String.Length; i += lineLength)
            {
                int length = Math.Min(lineLength, base64String.Length - i);
                lines.Add(base64String.Substring(i, length));
            }
            
            return lines;
        }

        private static bool IsBinaryContentType(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
                return false;

            var lowerContentType = contentType.ToLowerInvariant();
            // Common binary content types
            return lowerContentType.Contains("application/octet-stream") ||
                   lowerContentType.Contains("application/zip") ||
                   lowerContentType.Contains("application/x-zip") ||
                   lowerContentType.Contains("application/x-compressed") ||
                   lowerContentType.Contains("application/gzip") ||
                   lowerContentType.Contains("image/") ||
                   lowerContentType.Contains("video/") ||
                   lowerContentType.Contains("audio/") ||
                   lowerContentType.Contains("application/pdf") ||
                   lowerContentType.Contains("application/x-msdownload") ||
                   lowerContentType.Contains("application/x-executable");
        }

        private class RecordingStream : Stream
        {
            private readonly Stream _inner;
            private readonly RecordingEntry _entry;
            private readonly HttpInteractionRecorder _owner;
            private bool _completed;

            public RecordingStream(Stream inner, RecordingEntry entry, HttpInteractionRecorder owner)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                _entry = entry ?? throw new ArgumentNullException(nameof(entry));
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            }

            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => _inner.CanSeek;
            public override bool CanWrite => false;
            public override long Length => _inner.Length;
            public override long Position
            {
                get => _inner.Position;
                set => _inner.Position = value;
            }

            public override void Flush()
            {
                _inner.Flush();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return _inner.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                _inner.SetLength(value);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var bytesRead = _inner.Read(buffer, offset, count);
                if (bytesRead > 0)
                {
                    _entry.ResponseBuffer.Write(buffer, offset, bytesRead);
                }
                else
                {
                    Complete();
                }
                return bytesRead;
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                var bytesRead = await _inner.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
                if (bytesRead > 0)
                {
                    _entry.ResponseBuffer.Write(buffer, offset, bytesRead);
                }
                else
                {
                    Complete();
                }
                return bytesRead;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    try
                    {
                        Complete();
                    }
                    finally
                    {
                        _inner.Dispose();
                    }
                }
                base.Dispose(disposing);
            }

            private void Complete()
            {
                if (_completed)
                    return;
                _completed = true;
                _owner.CompleteSuccess(_entry);
            }
        }
    }

}
