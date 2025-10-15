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
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using pwiz.Common.SystemUtil;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// Test helper for HttpClientWithProgress integration testing.
    /// Allows simulation of various HTTP failure scenarios without making actual network requests.
    /// </summary>
    public class HttpClientTestHelper : IDisposable
    {
        // ReSharper disable once NotAccessedField.Local
        private readonly HttpClientTestBehavior _behavior;
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
        public bool SimulateProgress { get; set; }
        public Action OnProgressCallback { get; set; }

        public Stream GetMockResponseStream(Uri uri, out long contentLength)
        {
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
