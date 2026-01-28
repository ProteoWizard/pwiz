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
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using pwiz.Common.CommonResources;

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// HttpClient wrapper that provides progress reporting and cancellation support
    /// using IProgressMonitor. Replaces WebClient functionality with modern HttpClient.
    /// Use with LongWaitDlg.PerformWork(Action&lt;IProgressMonitor&gt;) for UI progress.
    /// 
    /// This class uses a singleton HttpClient internally to avoid thread handle leaks from
    /// repeated creation/disposal. Each instance stores per-request state (headers, cookies)
    /// and applies it to HttpRequestMessage objects when making requests.
    /// </summary>
    public class HttpClientWithProgress : IDisposable
    {
        private static readonly HttpClient _sharedHttpClient = HttpClientSingleton.Instance;
        private readonly CookieContainer _cookieContainer;
        private readonly IProgressMonitor _progressMonitor;
        private IProgressStatus _progressStatus;
        private string _progressMessageWithoutSize; // Base message before download size is appended
        private const int ReadTimeoutMilliseconds = 15000; // timeout per chunk to avoid long hangs when network drops
        private TimeSpan? _requestTimeout;

        // Per-request state (stored in instance, applied to HttpRequestMessage)
        private string _authHeader;
        private readonly Dictionary<string, string> _customHeaders = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets whether to show transfer size in progress messages.
        /// Set to false for fast operations (API calls, small downloads) to avoid brief size flash.
        /// Default is true (show size for file downloads/uploads).
        /// </summary>
        public bool ShowTransferSize { get; set; } = true;

        /// <summary>
        /// Optional request timeout. This property is retained for API compatibility but does not
        /// affect the underlying HttpClient (which uses infinite timeout). Timeouts are handled
        /// per-chunk via ReadTimeoutMilliseconds to detect stalled transfers.
        /// </summary>
        public TimeSpan? RequestTimeout
        {
            get => _requestTimeout;
            set
            {
                if (value.HasValue && value.Value <= TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(nameof(value), @"Timeout must be positive.");

                _requestTimeout = value;
                // Note: We don't set _sharedHttpClient.Timeout because it's shared across all instances
                // and uses infinite timeout. Per-chunk timeouts are handled in ReadChunk().
            }
        }

        /// <summary>
        /// Creates an HttpClient wrapper with optional progress reporting.
        /// Uses a singleton HttpClient internally to avoid thread handle leaks.
        /// </summary>
        /// <param name="progressMonitor">Progress monitor for reporting download progress and handling cancellation</param>
        /// <param name="status">Initial progress status to update with an ID tracked by the progress monitor. If null, creates a new ProgressStatus.</param>
        /// <param name="cookieContainer">Optional cookie container for session management (e.g., Panorama authentication). If null, cookies are not persisted.</param>
        public HttpClientWithProgress(IProgressMonitor progressMonitor = null, IProgressStatus status = null, CookieContainer cookieContainer = null)
        {
            // Store cookie container reference for per-request cookie handling
            _cookieContainer = cookieContainer;
            
            // Set infinite timeout for large file uploads/downloads
            // We have per-chunk timeouts (ReadTimeoutMilliseconds) for detecting stalled transfers
            // The overall request timeout should not apply to operations that may take a long time
            RequestTimeout = null;
            
            // Default to SilentProgressMonitor if null - maintains non-null invariant
            // This allows simple callers to pass null while complex callers can use SilentProgressMonitor(cancelToken)
            _progressMonitor = progressMonitor ?? new SilentProgressMonitor();
            _progressStatus = status ?? new ProgressStatus();
        }

        /// <summary>
        /// Adds an Authorization header to requests made by this HttpClientWithProgress instance.
        /// Use this for authenticated downloads (e.g., Basic auth, Bearer tokens).
        /// The header is stored in instance state and applied per-request.
        /// </summary>
        /// <param name="authHeaderValue">The authorization header value (e.g., "Basic base64credentials" or "Bearer token")</param>
        public void AddAuthorizationHeader(string authHeaderValue)
        {
            _authHeader = authHeaderValue;
        }

        /// <summary>
        /// Adds a custom header to requests made by this HttpClientWithProgress instance.
        /// Use this for CSRF tokens, API keys, or other custom headers required by the server.
        /// If the header already exists, it will be removed and replaced with the new value.
        /// The header is stored in instance state and applied per-request.
        /// </summary>
        /// <param name="name">The header name (e.g., "X-LABKEY-CSRF", "X-API-Key")</param>
        /// <param name="value">The header value</param>
        public void AddHeader(string name, string value)
        {
            _customHeaders[name] = value;
        }

        /// <summary>
        /// Gets a cookie value from the cookie container for a given URI.
        /// Returns null if no cookie container was provided or if the cookie is not present.
        /// </summary>
        /// <param name="uri">The URI to get cookies for (e.g., https://panoramaweb.org/)</param>
        /// <param name="cookieName">The name of the cookie to retrieve (e.g., "X-LABKEY-CSRF")</param>
        /// <returns>The cookie value, or null if not found</returns>
        public string GetCookie(Uri uri, string cookieName)
        {
            if (_cookieContainer == null)
                return null;

            var cookies = _cookieContainer.GetCookies(uri);
            return cookies[cookieName]?.Value;
        }

        /// <summary>
        /// Downloads a string from the specified URI with cancellation support.
        /// For small responses only. Use DownloadData() for large files with progress reporting.
        /// </summary>
        public string DownloadString(string uri)
        {
            return DownloadString(new Uri(uri));
        }

        /// <summary>
        /// Downloads a string from the specified URI with cancellation support.
        /// For small responses only. Use DownloadData() for large files with progress reporting.
        /// </summary>
        public string DownloadString(Uri uri)
        {
            using var memoryStream = new MemoryStream();
            DownloadToStream(uri, memoryStream);
            return Encoding.UTF8.GetString(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
        }

        /// <summary>
        /// Downloads data as byte array from the specified URI with progress reporting and cancellation support.
        /// </summary>
        public byte[] DownloadData(string uri)
        {
            return DownloadData(new Uri(uri));
        }

        /// <summary>
        /// Downloads data as byte array from the specified URI with progress reporting and cancellation support.
        /// </summary>
        public byte[] DownloadData(Uri uri)
        {
            using var memoryStream = new MemoryStream();
            DownloadToStream(uri, memoryStream);
            return memoryStream.ToArray();
        }

        /// <summary>
        /// Downloads a file from the specified URI to the specified file path with progress reporting and cancellation support.
        /// </summary>
        public void DownloadFile(string uri, string fileName)
        {
            DownloadFile(new Uri(uri), fileName);
        }

        /// <summary>
        /// Downloads a file from the specified URI to the specified file path with progress reporting and cancellation support.
        /// </summary>
        /// <param name="uri">The URI to download from</param>
        /// <param name="fileName">The local file path to save to</param>
        /// <param name="knownFileSize">Optional known file size in bytes (from .skyp file or Panorama API). 
        /// If null, will attempt to use Content-Length header. If neither available, shows indeterminate progress.</param>
        public void DownloadFile(Uri uri, string fileName, long? knownFileSize = null)
        {
            using var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
            DownloadToStream(uri, fileStream, knownFileSize);
        }

        /// <summary>
        /// Performs a HEAD request to the specified URI and returns true if successful.
        /// Useful for checking if a resource exists without downloading the full content.
        /// </summary>
        public bool Head(string uri)
        {
            return Head(new Uri(uri));
        }

        /// <summary>
        /// Performs a HEAD request to the specified URI and returns true if successful.
        /// Useful for checking if a resource exists without downloading the full content.
        /// </summary>
        public bool Head(Uri uri)
        {
            using var request = CreateRequest(HttpMethod.Head, uri);
            var response = SendRequest(request);
            return response.IsSuccessStatusCode;
        }

        /// <summary>
        /// Uploads form data to the specified URI and returns the response as a string.
        /// For small requests/responses only. Does not report progress on upload or download.
        /// </summary>
        public string UploadString(string uri, string method, string data)
        {
            return UploadString(new Uri(uri), method, data);
        }

        /// <summary>
        /// Uploads form data to the specified URI and returns the response as a string.
        /// For small requests/responses only. Does not report progress on upload or download.
        /// </summary>
        public string UploadString(Uri uri, string method, string data)
        {
            return UploadString(uri, method, data, @"application/x-www-form-urlencoded");
        }

        /// <summary>
        /// Uploads string data with a specified Content-Type.
        /// For small requests/responses only. Does not report progress on upload or download.
        /// </summary>
        public string UploadString(Uri uri, string method, string data, string contentType)
        {
            var content = new StringContent(data, Encoding.UTF8, contentType);
            using var request = CreateRequest(new HttpMethod(method), uri);
            request.Content = content;

            var response = SendRequest(request);
            return WithExceptionHandling(uri, () => response.Content.ReadAsStringAsync().Result);
        }

        /// <summary>
        /// Uploads form data to the specified URI and returns the response as byte array.
        /// For small requests/responses only. Does not report progress on upload or download.
        /// </summary>
        public byte[] UploadValues(string uri, string method, NameValueCollection data)
        {
            return UploadValues(new Uri(uri), method, data);
        }

        /// <summary>
        /// Uploads form data to the specified URI and returns the response as byte array.
        /// For small requests/responses only. Does not report progress on upload or download.
        /// </summary>
        public byte[] UploadValues(Uri uri, string method, NameValueCollection data)
        {
            var formContent = new FormUrlEncodedContent(
                data.AllKeys.Select(key => new KeyValuePair<string, string>(key, data[key])));

            using var request = CreateRequest(new HttpMethod(method), uri);
            request.Content = formContent;

            var response = SendRequest(request);
            return WithExceptionHandling(uri, () => response.Content.ReadAsByteArrayAsync().Result);
        }

        /// <summary>
        /// Gets the cancellation token from the progress monitor, or None if no monitor is available.
        /// </summary>
        private CancellationToken CancellationToken
        {
            get
            {
                if (_progressMonitor is IProgressMonitorWithCancellationToken pm)
                    return pm.CancellationToken;
                // IProgressMonitor doesn't expose CancellationToken directly; use None and rely on IsCanceled checks
                return CancellationToken.None;
            }
        }

        /// <summary>
        /// Creates an HttpRequestMessage with per-request headers and cookies applied.
        /// This ensures that each request uses the correct headers/cookies for this instance,
        /// even though we're using a shared HttpClient.
        /// </summary>
        private HttpRequestMessage CreateRequest(HttpMethod method, Uri uri)
        {
            var request = new HttpRequestMessage(method, uri);
            ApplyHeadersToRequest(request);
            return request;
        }

        /// <summary>
        /// Processes Set-Cookie headers from the response and adds them to the cookie container.
        /// This ensures cookies are persisted for subsequent requests in the same session.
        /// </summary>
        private void ProcessResponseCookies(HttpResponseMessage response, Uri uri)
        {
            if (_cookieContainer == null)
                return;

            // Extract Set-Cookie headers from response
            if (response.Headers.TryGetValues(@"Set-Cookie", out var setCookieHeaders))
            {
                foreach (var setCookieHeader in setCookieHeaders)
                {
                    // Parse and add cookie to container
                    // CookieContainer.SetCookies handles parsing the Set-Cookie header format
                    _cookieContainer.SetCookies(uri, setCookieHeader);
                }
            }
        }

        /// <summary>
        /// Downloads content from the specified URI to the provided stream with progress reporting and cancellation support.
        /// </summary>
        /// <param name="uri">The URI to download from</param>
        /// <param name="outputStream">The <see cref="Stream"/> to download to</param>
        /// <param name="knownTotalBytes">Optional known file size. If null, uses Content-Length header. If neither available, shows indeterminate progress.</param>
        private void DownloadToStream(Uri uri, Stream outputStream, long? knownTotalBytes = null)
        {
            // Check if we should use a mock response stream for testing
            Stream contentStream;
            long totalBytes;
            
            // Normal path: create request first (needed for authorization-aware playback)
            using var request = CreateRequest(HttpMethod.Get, uri);
            
            if (TestBehavior != null)
            {
                // Try GetMockResponseStreamFromRequest first (supports authorization header lookup)
                contentStream = TestBehavior.GetMockResponseStreamFromRequest(request);
                if (contentStream != null)
                {
                    totalBytes = contentStream.CanSeek ? contentStream.Length : 0;
                    // Use mock stream for testing (prefer mock's total bytes over known size)
                    using (contentStream)
                    {
                        DownloadFromStream(contentStream, outputStream, totalBytes, uri);
                    }
                    return;
                }

                contentStream = TestBehavior.GetMockResponseStream(uri, out totalBytes);
                if (contentStream != null)
                {
                    // Use mock stream for testing (prefer mock's total bytes over known size)
                    using (contentStream)
                    {
                        DownloadFromStream(contentStream, outputStream, totalBytes, uri);
                    }
                    return;
                }
            }

            // Normal path: get response from network
            var response = GetResponseHeadersRead(request);

            // Process cookies from response
            ProcessResponseCookies(response, uri);

            // Use known size if provided, otherwise try Content-Length header, fallback to 0 (indeterminate)
            totalBytes = knownTotalBytes ?? response.Content.Headers.ContentLength ?? 0;
            // totalBytes = 0; // TEST: Uncomment to force marquee progress for unknown file sizes
            contentStream = response.Content.ReadAsStreamAsync().Result;
            if (TestBehavior != null)
            {
                contentStream = TestBehavior.WrapResponseStream(uri, contentStream, totalBytes) ?? contentStream;
            }
            
            using (contentStream)
            {
                DownloadFromStream(contentStream, outputStream, totalBytes, uri);
            }
        }

        private void DownloadFromStream(Stream contentStream, Stream outputStream, long totalBytes, Uri uri)
        {
            var downloadedBytes = 0L;
            var buffer = new byte[8192];

            while (true)
            {
                int bytesRead;
                try
                {
                    bytesRead = ReadChunk(contentStream, buffer, uri);
                }
                catch (Exception ex)
                {
                    var mapped = MapHttpException(ex, uri);
                    TestBehavior?.OnException(uri, mapped);
                    throw mapped;
                }

                if (bytesRead <= 0)
                    break;

                // Check for cancellation
                if (_progressMonitor.IsCanceled)
                    throw new OperationCanceledException();

                outputStream.Write(buffer, 0, bytesRead);
                downloadedBytes += bytesRead;

                var message = GetProgressMessage(totalBytes, downloadedBytes);

                if (totalBytes > 0)
                {
                    var percentage = (int)(downloadedBytes * 100 / totalBytes);
                    _progressMonitor.UpdateProgress(_progressStatus =
                        _progressStatus.ChangeMessage(message).ChangePercentComplete(percentage));
                }
                else
                {
                    // Set percent to -1 for indeterminate/marquee progress when total size is unknown
                    _progressMonitor.UpdateProgress(_progressStatus =
                        _progressStatus.ChangeMessage(message).ChangePercentComplete(-1));
                }
            }
        }

        private int ReadChunk(Stream stream, byte[] buffer, Uri uri)
        {
            var readTask = stream.ReadAsync(buffer, 0, buffer.Length, CancellationToken);

            // Use a dedicated CTS for the delay so we can cancel it when done.
            // This prevents timer accumulation - each Task.Delay creates an internal Timer
            // that would otherwise run for the full ReadTimeoutMilliseconds duration.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            try
            {
                var delayTask = Task.Delay(ReadTimeoutMilliseconds, timeoutCts.Token);
                var completed = Task.WaitAny(readTask, delayTask);

                // Check if cancellation was requested before throwing timeout exception
                CancellationToken.ThrowIfCancellationRequested();

                if (completed != 0)
                    throw new TimeoutException(string.Format(MessageResources.HttpClientWithProgress_ReadWithTimeout_The_read_operation_timed_out_while_downloading_from__0__, uri));
                return readTask.Result;
            }
            finally
            {
                // Cancel the delay timer to prevent timer/handle accumulation
                timeoutCts.Cancel();
            }
        }

        /// <summary>
        /// Sends an HTTP request with cancellation support.
        /// Used for custom HTTP methods like HEAD, DELETE, MOVE that aren't covered by standard download/upload methods.
        /// Applies per-instance headers (Authorization, custom headers) and cookies to the request.
        /// </summary>
        public HttpResponseMessage SendRequest(HttpRequestMessage request)
        {
            // Apply per-instance headers and cookies to the request
            ApplyHeadersToRequest(request);
            
            var response = WithExceptionHandling(request.RequestUri,
                () => _sharedHttpClient.SendAsync(request, CancellationToken).Result);
            
            // Process cookies from response
            ProcessResponseCookies(response, request.RequestUri);
            
            return response;
        }

        /// <summary>
        /// Applies per-instance headers and cookies to an HttpRequestMessage.
        /// Used when requests are created externally (e.g., via SendRequest()).
        /// </summary>
        private void ApplyHeadersToRequest(HttpRequestMessage request)
        {
            const string authHeaderName = "Authorization";
            const string cookieHeaderName = "Cookie";
            // Add Authorization header if set
            if (!string.IsNullOrEmpty(_authHeader) && !request.Headers.Contains(authHeaderName))
            {
                request.Headers.Add(authHeaderName, _authHeader);
            }

            // Add custom headers
            foreach (var header in _customHeaders.Where(header =>
                         !request.Headers.Contains(header.Key)))
            {
                request.Headers.Add(header.Key, header.Value);
            }

            // Add cookies from cookie container
            if (_cookieContainer != null && !request.Headers.Contains(cookieHeaderName))
            {
                var cookies = _cookieContainer.GetCookies(request.RequestUri);
                if (cookies.Count > 0)
                {
                    var cookieHeader = string.Join(@"; ", 
                        cookies.Cast<Cookie>().Select(c => $@"{c.Name}={c.Value}"));
                    request.Headers.Add(cookieHeaderName, cookieHeader);
                }
            }
        }

        /// <summary>
        /// Gets a response from the specified URI with ResponseHeadersRead completion option for streaming.
        /// Useful for getting response headers (like ContentLength) without downloading the full content.
        /// </summary>
        /// <param name="uri">The URI to request</param>
        /// <returns>The HTTP response message with headers read</returns>
        public HttpResponseMessage GetResponseHeadersRead(Uri uri)
        {
            using var request = CreateRequest(HttpMethod.Get, uri);
            return GetResponseHeadersRead(request);
        }

        /// <summary>
        /// Gets a response from the specified request with ResponseHeadersRead completion option for streaming.
        /// </summary>
        private HttpResponseMessage GetResponseHeadersRead(HttpRequestMessage request)
        {
            // Process cookies from response
            var response = WithExceptionHandling(request.RequestUri,
                () => _sharedHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, CancellationToken).Result);
            
            ProcessResponseCookies(response, request.RequestUri);
            
            return response;
        }

        /// <summary>
        /// Uploads a file to the specified URI with progress reporting and cancellation support.
        /// Uses the same chunked upload pattern as downloads for consistency.
        /// </summary>
        /// <param name="uri">The URI to upload to</param>
        /// <param name="method">The HTTP method to use (e.g., "PUT", "POST")</param>
        /// <param name="fileName">The path to the file to upload</param>
        public void UploadFile(Uri uri, string method, string fileName)
        {
            using var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            UploadFromStream(uri, method, fileStream, Path.GetFileName(fileName));
        }

        /// <summary>
        /// Uploads a file and returns the response body as a string.
        /// Used when the server response needs to be checked for errors (e.g., LabKey JSON responses).
        /// </summary>
        /// <param name="uri">The URI to upload to</param>
        /// <param name="method">The HTTP method to use (e.g., "PUT", "POST")</param>
        /// <param name="fileName">The path to the file to upload</param>
        /// <returns>The response body as a string</returns>
        public string UploadFileWithResponse(Uri uri, string method, string fileName)
        {
            using var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            return UploadFromStreamWithResponse(uri, method, fileStream, Path.GetFileName(fileName));
        }

        /// <summary>
        /// Uploads a byte array to the specified URI with progress reporting and cancellation support.
        /// Uses the same chunked upload pattern as downloads for consistency.
        /// </summary>
        /// <param name="uri">The URI to upload to</param>
        /// <param name="method">The HTTP method to use (e.g., "PUT", "POST")</param>
        /// <param name="data">The byte array to upload</param>
        public void UploadData(Uri uri, string method, byte[] data)
        {
            using var memoryStream = new MemoryStream(data);
            UploadFromStream(uri, method, memoryStream);
        }


        /// <summary>
        /// Uploads a stream and returns the response body.
        /// Used when the server response needs to be checked for errors (e.g., LabKey JSON responses).
        /// </summary>
        private string UploadFromStreamWithResponse(Uri uri, string method, Stream inputStream, string fileName = null)
        {
            var response = UploadFromStreamInternal(uri, method, inputStream, fileName);
            // Read response body for LabKey error checking
            return response.Content.ReadAsStringAsync().Result;
        }

        /// <summary>
        /// Uploads a stream to the specified URI with progress reporting and cancellation support.
        /// Streams directly from input to network without buffering entire file in memory.
        /// Progress is reported during the network upload (the expensive operation), not during file read.
        /// </summary>
        private void UploadFromStream(Uri uri, string method, Stream inputStream, string fileName = null)
        {
            // Discard response - caller doesn't need it
            UploadFromStreamInternal(uri, method, inputStream, fileName);
        }

        /// <summary>
        /// Internal upload implementation that returns the HttpResponseMessage.
        /// Allows callers to either discard the response or read it for error checking.
        /// </summary>
        private HttpResponseMessage UploadFromStreamInternal(Uri uri, string method, Stream inputStream, string fileName = null)
        {
            long totalBytes = inputStream.Length;

            // Wrap stream with progress tracking - ProgressStream.Read() will be called by HttpClient
            // during SendAsync(), reporting progress during the actual NETWORK upload
            var progressStream = new ProgressStream(inputStream, totalBytes, (uploaded) =>
            {
                UpdateUploadProgress(uploaded, totalBytes, uri);
            });

            // Build the HTTP request (exercises request building code in tests)
            using var request = CreateRequest(new HttpMethod(method), uri);
            request.Content = new StreamContent(progressStream);

            if (!string.IsNullOrEmpty(fileName))
            {
                request.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue(@"attachment")
                {
                    FileName = fileName
                };
            }

            // Send the upload to the network (or simulate for testing)
            // HttpClient reads from progressStream AS IT UPLOADS, triggering progress callbacks
            return WithExceptionHandling(uri, () =>
            {
                // Check if we should capture upload data for testing (bypass actual network send)
                var mockUploadStream = TestBehavior?.GetMockUploadStream(uri);
                if (mockUploadStream != null)
                {
                    // Testing mode: Read from request content and write to mock stream
                    // This exercises ProgressStream.Read() and UpdateUploadProgress() just like real uploads
                    // AND validates that the correct data is being uploaded
                    var buffer = new byte[8192];
                    var contentStream = request.Content.ReadAsStreamAsync().Result;
                    while (true)
                    {
                        int bytesRead = contentStream.Read(buffer, 0, buffer.Length);
                        if (bytesRead <= 0)
                            break;
                        mockUploadStream.Write(buffer, 0, bytesRead);
                    }
                    // Return success response without actually sending to network
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }

                // Normal path: send the request - HttpClient reads from progressStream during upload
                // Progress is reported during the NETWORK operation, not during file read
                var response = _sharedHttpClient.SendAsync(request, CancellationToken).Result;
                
                // Process cookies from response
                ProcessResponseCookies(response, uri);

                // Check status code - but don't call EnsureSuccessStatusCode() yet
                // We need to return the response so caller can read the body
                if (!response.IsSuccessStatusCode)
                    ThrowResponseFailedException(uri, response);
                
                return response;
            });
        }

        private void UpdateUploadProgress(long uploadedBytes, long totalBytes, Uri uri)
        {
            var message = GetProgressMessage(totalBytes, uploadedBytes, true);

            if (totalBytes > 0)
            {
                var percentage = (int)(uploadedBytes * 100 / totalBytes);
                _progressMonitor.UpdateProgress(_progressStatus =
                    _progressStatus.ChangeMessage(message).ChangePercentComplete(percentage));
            }
            else
            {
                _progressMonitor.UpdateProgress(_progressStatus =
                    _progressStatus.ChangeMessage(message).ChangePercentComplete(-1));
            }

            // Check for cancellation during upload
            if (_progressMonitor.IsCanceled)
                throw new OperationCanceledException();
        }

        /// <summary>
        /// Stream wrapper that reports progress during read operations.
        /// Used for upload progress tracking - wraps the source stream so that progress
        /// is reported as HttpClient reads from it during SendAsync().
        /// Reads are NOT chunked with timeout here because HttpClient controls the read loop.
        /// Progress tracking and cancellation happen in the callback.
        /// </summary>
        private class ProgressStream : Stream
        {
            private readonly Stream _innerStream;
            // ReSharper disable once NotAccessedField.Local
            private readonly long _totalBytes;  // For debugging
            private readonly Action<long> _progressCallback;
            private long _bytesRead;

            public ProgressStream(Stream innerStream, long totalBytes, Action<long> progressCallback)
            {
                _innerStream = innerStream;
                _totalBytes = totalBytes;
                _progressCallback = progressCallback;
            }

            public override bool CanRead => _innerStream.CanRead;
            public override bool CanSeek => _innerStream.CanSeek;
            public override bool CanWrite => false;
            public override long Length => _innerStream.Length;
            public override long Position
            {
                get => _innerStream.Position;
                set => _innerStream.Position = value;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                // HttpClient calls this during SendAsync() as it uploads
                // Read from the inner stream (e.g., FileStream from disk)
                int bytesRead = _innerStream.Read(buffer, offset, count);
                _bytesRead += bytesRead;

                // Report progress - callback will check cancellation
                _progressCallback?.Invoke(_bytesRead);

                return bytesRead;
            }

            public override void Flush() => _innerStream.Flush();
            public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
            public override void SetLength(long value) => _innerStream.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private string GetProgressMessage(long totalBytes, long downloadedBytes, bool isUpload = false)
        {
            // Capture base message on first progress update
            // _progressStatus is never null (guaranteed by constructor), but Message could theoretically be null
            _progressMessageWithoutSize ??= _progressStatus.Message ?? string.Empty;

            // For fast operations (API calls), don't show size to avoid brief flash
            if (!ShowTransferSize)
                return _progressMessageWithoutSize;
            
            // Report progress with size appended
            return GetProgressMessageWithSize(_progressMessageWithoutSize, downloadedBytes, totalBytes, isUpload);
        }

        /// <summary>
        /// Builds progress message with size information appended.
        /// Prevents size from being appended repeatedly by using separate base message and size components.
        /// </summary>
        /// <param name="baseMessage">The base progress message without size (e.g., "Downloading file.txt")</param>
        /// <param name="transferredBytes">Number of bytes transferred</param>
        /// <param name="totalBytes">Total bytes to transfer (0 if unknown)</param>
        /// <param name="isUpload">True if this is for an upload, false for download</param>
        /// <returns>Progress message with size appended</returns>
        public static string GetProgressMessageWithSize(string baseMessage, long transferredBytes, long totalBytes, bool isUpload = false)
        {
            var formatProvider = new FileSizeFormatProvider();
            string transferredSize = string.Format(formatProvider, @"{0:fs1}", transferredBytes);
            
            // If base message is empty, format size text more naturally
            if (string.IsNullOrWhiteSpace(baseMessage))
            {
                if (totalBytes > 0)
                {
                    string totalSize = string.Format(formatProvider, @"{0:fs1}", totalBytes);
                    var formatString = isUpload
                        ? MessageResources.HttpClientWithProgress_GetProgressMessageWithSize_Uploaded__0__of__1_
                        : MessageResources.HttpClientWithProgress_GetProgressMessageWithSize_Downloaded__0__of__1_;
                    return string.Format(formatString, transferredSize, totalSize);
                }
                return transferredSize;
            }
            
            // Otherwise combine them on separate lines with size in "X / Y" format
            string sizeText = totalBytes > 0
                ? string.Format(MessageResources.HttpClientWithProgress_GetProgressMessageWithSize__0____1_, transferredSize, string.Format(formatProvider, @"{0:fs1}", totalBytes))
                : transferredSize;
            
            return new StringBuilder()
                .AppendLine(baseMessage)
                .AppendLine()
                .AppendLine(sizeText).ToString();
        }

        public static IHttpClientTestBehavior TestBehavior;

        /// <summary>
        /// Interface for controlling HttpClientWithProgress behavior during testing.
        /// Allows simulation of failures, mock responses, and progress/cancellation scenarios
        /// without making actual network requests.
        /// </summary>
        public interface IHttpClientTestBehavior
        {
            /// <summary>
            /// Exception to throw when simulating a failure, or null for success.
            /// </summary>
            Exception FailureException { get; }

            /// <summary>
            /// Gets a mock response stream for testing download operations.
            /// Returns null to use the actual network response.
            /// </summary>
            /// <param name="uri">The URI being requested</param>
            /// <param name="contentLength">Output parameter for the content length (for progress reporting)</param>
            /// <returns>A stream containing mock response data, or null to use actual network</returns>
            Stream GetMockResponseStream(Uri uri, out long contentLength);

            /// <summary>
            /// Gets a mock response stream for testing download operations using the full request message.
            /// This allows behaviors to access request headers (e.g., Authorization) for lookup.
            /// Returns null to fall back to GetMockResponseStream or actual network response.
            /// </summary>
            /// <param name="request">The HTTP request message (includes headers like Authorization)</param>
            /// <returns>A stream containing mock response data, or null to fall back to other methods</returns>
            Stream GetMockResponseStreamFromRequest(HttpRequestMessage request);

            /// <summary>
            /// Gets a destination stream for testing upload operations.
            /// Upload data will be written to this stream instead of being sent to the network.
            /// Returns null to use actual network upload.
            /// </summary>
            /// <param name="uri">The URI being uploaded to</param>
            /// <returns>A stream to capture uploaded data, or null to use actual network</returns>
            Stream GetMockUploadStream(Uri uri);

            /// <summary>
            /// Notifies the behavior that an HTTP response has been received (including failures).
            /// </summary>
            /// <param name="uri">The requested URI</param>
            /// <param name="response">The HTTP response message</param>
            void OnResponse(Uri uri, HttpResponseMessage response);

            /// <summary>
            /// Allows the behavior to wrap or intercept the response stream before it is consumed.
            /// </summary>
            /// <param name="uri">The requested URI</param>
            /// <param name="responseStream">The stream returned by HttpClient</param>
            /// <param name="contentLength">The reported content length</param>
            /// <returns>The stream that should be consumed by the caller</returns>
            Stream WrapResponseStream(Uri uri, Stream responseStream, long contentLength);

            /// <summary>
            /// Notifies the behavior that an HTTP response will result in an exception.
            /// </summary>
            /// <param name="uri">The requested URI</param>
            /// <param name="response">The HTTP response message</param>
            /// <param name="responseBody">The response body, if available</param>
            /// <param name="exception">The exception that will be thrown</param>
            void OnFailedResponse(Uri uri, HttpResponseMessage response, string responseBody, Exception exception);

            /// <summary>
            /// Notifies the behavior that an exception is being thrown to the caller.
            /// </summary>
            /// <param name="uri">The requested URI</param>
            /// <param name="exception">The exception being thrown</param>
            void OnException(Uri uri, Exception exception);
        }

        public class NoNetworkTestException : Exception
        {
            public NoNetworkTestException()
                : base(@"Network adapter disabling simulated")
            {
            }
        }
        
        private TRet WithExceptionHandling<TRet>(Uri uri, Func<TRet> action)
        {
            try
            {
                if (TestBehavior?.FailureException != null)
                    throw TestBehavior.FailureException;
                
                return action();
            }
            catch (Exception ex)
            {
                var mapped = MapHttpException(ex, uri);
                TestBehavior?.OnException(uri, mapped);
                throw mapped;
            }
        }

        private HttpResponseMessage WithExceptionHandling(Uri uri, Func<HttpResponseMessage> getResponse)
        {
            try
            {
                if (TestBehavior?.FailureException != null)
                    throw TestBehavior.FailureException;
                
                var response = getResponse();
                TestBehavior?.OnResponse(uri, response);
                
                // Check status code and capture response body for error details (e.g., LabKey-specific errors)
                if (!response.IsSuccessStatusCode)
                    ThrowResponseFailedException(uri, response);
                
                return response;
            }
            catch (Exception ex)
            {
                var mapped = MapHttpException(ex, uri);
                TestBehavior?.OnException(uri, mapped);
                throw mapped;
            }
        }

        /// <summary>
        /// Handles a failed HTTP response by creating an exception, notifying test behavior, and throwing.
        /// This ensures consistent error handling across all response failure paths (downloads and uploads).
        /// </summary>
        private void ThrowResponseFailedException(Uri uri, HttpResponseMessage response)
        {
            var exception = CreateResponseFailedException(uri, response);
            TestBehavior?.OnFailedResponse(uri, response, exception.ResponseBody, exception);
            throw exception;
        }

        /// <summary>
        /// Creates a NetworkRequestException for a failed HTTP response and notifies test behavior.
        /// This ensures consistent error handling across all response failure paths.
        /// </summary>
        private NetworkRequestException CreateResponseFailedException(Uri uri, HttpResponseMessage response)
        {
            string responseBody = null;
            try
            {
                // Attempt to read response body for server-specific error details
                // This is important for servers like LabKey that include structured error info in responses
                responseBody = response.Content.ReadAsStringAsync().Result;
            }
            catch
            {
                // If we can't read the response body, continue without it
            }

            // Create an HttpRequestException similar to what EnsureSuccessStatusCode() would throw
            var statusCode = (int)response.StatusCode;
            var reasonPhrase = string.IsNullOrEmpty(response.ReasonPhrase) 
                ? response.StatusCode.ToString() 
                : response.ReasonPhrase;
            var message = string.Format(MessageResources.HttpClientWithProgress_CreateResponseFailedException_Response_status_code_does_not_indicate_success___0____1___for__2_,
                statusCode, reasonPhrase, uri);
                    
            // Throw with response body attached for server-specific error extraction
            return new NetworkRequestException(message, response.StatusCode, uri, new HttpRequestException(message), responseBody);
        }

        private Exception MapHttpException(Exception ex, Uri uri)
        {
            var root = ex is AggregateException ae ? ae.Flatten().InnerExceptions.FirstOrDefault() ?? ex : ex;

            // If we've already created a NetworkRequestException with response body, pass it through
            if (root is NetworkRequestException)
                return root;

            // Check for cancellation first (but distinguish between user cancellation and timeout)
            // TaskCanceledException is a subclass of OperationCanceledException
            if (root is TaskCanceledException)
            {
                // If OUR CancellationToken is canceled, it's a user cancellation - fall through
                // If OUR token is NOT canceled, it's a timeout (HttpClient's internal timeout expired)
                if (!CancellationToken.IsCancellationRequested)
                {
                    return new NetworkRequestException(
                        string.Format(MessageResources.HttpClientWithProgress_MapHttpException_The_request_to__0__timed_out__Please_try_again_, uri), 
                        NetworkFailureType.Timeout, uri, root);
                }
                // Otherwise it's user cancellation - fall through to OperationCanceledException handling
            }

            // OperationCanceledException (including user-triggered TaskCanceledException) - pass through unchanged
            if (root is OperationCanceledException)
                return root;

            // Explicit timeout
            if (root is TimeoutException)
                return new NetworkRequestException(
                    string.Format(MessageResources.HttpClientWithProgress_MapHttpException_The_request_to__0__timed_out__Please_try_again_, uri), 
                    NetworkFailureType.Timeout, uri, root);

            // No network available
            if (root is NoNetworkTestException || !IsNetworkReallyAvailable())
                return new NetworkRequestException(
                    MessageResources.HttpClientWithProgress_MapHttpException_No_network_connection_detected__Please_check_your_internet_connection_and_try_again_, 
                    NetworkFailureType.NoConnection, uri, root);

            if (root is HttpRequestException httpEx)
            {
                return MapHttpRequestException(uri, httpEx);
            }

            if (root is IOException ioEx)
            {
                // Check for network connection failures using HResult instead of message text
                // Common HResult values for network failures:
                //      0x8007006D (network name no longer available)
                //      0x8007006E (network path not found)
                //      0x80070050 (file exists but network issues)
                //      0x8007006F (network path not found - alternative)
                var hResult = ioEx.HResult;
                if (hResult == unchecked((int)0x8007006D) || hResult == unchecked((int)0x8007006E) || 
                    hResult == unchecked((int)0x80070050) || hResult == unchecked((int)0x8007006F))
                {
                    return new NetworkRequestException(
                        MessageResources.HttpClientWithProgress_MapHttpException_The_connection_was_lost_during_download__Please_check_your_internet_connection_and_try_again_, 
                        NetworkFailureType.ConnectionLost, uri, root);
                }
            }

            return root;
        }

        private static NetworkRequestException MapHttpRequestException(Uri uri, HttpRequestException httpEx)
        {
            string server = uri?.Host ?? MessageResources.HttpClientWithProgress_MapHttpException_server;
                
            // Try to extract status code from the exception chain
            // First check if there's a NetworkRequestException in the inner exception (from WithExceptionHandling)
            HttpStatusCode? statusCode = null;
            
            // Check inner exceptions for NetworkRequestException which has the status code
            var inner = httpEx.InnerException;
            while (inner != null && statusCode == null)
            {
                if (inner is NetworkRequestException networkEx)
                {
                    statusCode = networkEx.StatusCode;
                }
                inner = inner.InnerException;
            }
            
            // If no status code found, try parsing from message as fallback (may be English-only)
            // This handles cases where HttpRequestException comes from elsewhere (e.g., EnsureSuccessStatusCode)
            if (statusCode == null)
            {
                // Try to extract status code from message like "Response status code does not indicate success: 404 (Not Found)"
                // Note: This may fail on non-English systems where message is localized
                var match = System.Text.RegularExpressions.Regex.Match(httpEx.Message, @"(\d{3})\s*\(([^)]+)\)");
                if (match.Success)
                {
                    statusCode = (HttpStatusCode)int.Parse(match.Groups[1].Value);
                }
            }
            
            if (statusCode != null)
            {
                var statusCodeValue = statusCode.Value;
                        
                // Use full URI for user context, but preserve the original HttpRequestException
                // with status code details in the inner exception chain for troubleshooting
                // For resource-specific errors (404, 401, 403), show full URI in user message
                // For server errors (500, 429, etc.), show just hostname (more concise)
                // Full URI is always available in NetworkRequestException.RequestUri and inner exception
                string uriString = uri?.ToString() ?? server;
                string message;
                switch ((int)statusCodeValue)
                {
                    case 404:
                        // Resource-specific: show full URI so user can verify the exact path
                        message = string.Format(MessageResources.HttpClientWithProgress_MapHttpException_The_requested_resource_at__0__was_not_found__HTTP_404___Please_verify_the_URL_, uriString);
                        break;
                    case 500:
                        // Server error: show hostname (problem is server-side, not specific resource)
                        message = string.Format(MessageResources.HttpClientWithProgress_MapHttpException_The_server__0__encountered_an_internal_error__HTTP_500___Please_try_again_later_or_contact_the_server_administrator_, server);
                        break;
                    case 401:
                        // Auth-specific: show full URI so user knows what they're being denied access to
                        message = string.Format(MessageResources.HttpClientWithProgress_MapHttpException_Access_to__0__was_denied__HTTP_401___Authentication_may_be_required_, uriString);
                        break;
                    case 403:
                        // Permission-specific: show full URI so user knows what resource is forbidden
                        message = string.Format(MessageResources.HttpClientWithProgress_MapHttpException_Access_to__0__was_forbidden__HTTP_403___You_may_not_have_permission_to_access_this_resource_, uriString);
                        break;
                    case 429:
                        // Rate limit: show hostname (typically server-wide limit, not resource-specific)
                        message = string.Format(MessageResources.HttpClientWithProgress_MapHttpException_Too_many_requests_to__0___HTTP_429___Please_wait_before_trying_again_, server);
                        break;
                    default:
                        // Generic server error: show hostname
                        message = string.Format(MessageResources.HttpClientWithProgress_MapHttpException_The_server__0__returned_an_error__HTTP__1____Please_try_again_or_contact_support_, server, (int)statusCodeValue);
                        break;
                }
                // Wrap with NetworkRequestException to provide structured access to status code
                // Preserves original HttpRequestException as inner exception for detailed troubleshooting
                return new NetworkRequestException(message, statusCodeValue, uri, httpEx);
            }
                
            // DNS resolution failure (e.g., 'The remote name could not be resolved')
            // This is real and has been seen in a debugger. The InnerException is a WebException
            // HttpClient appears to use HttpWebRequest, but wrap its exceptions in HttpRequestException
            if (httpEx.InnerException is WebException { Status: WebExceptionStatus.NameResolutionFailure })
                return new NetworkRequestException(
                    string.Format(MessageResources.HttpClientWithProgress_MapHttpException_Failed_to_resolve_host__0___Please_check_your_DNS_settings_or_VPN_proxy_, server), 
                    NetworkFailureType.DnsResolution, uri, httpEx);

            // Generic connection failure (no HTTP response received)
            return new NetworkRequestException(
                string.Format(MessageResources.HttpClientWithProgress_MapHttpException_Failed_to_connect_to__0___Please_check_your_network_connection__VPN_proxy__or_firewall_, server), 
                NetworkFailureType.ConnectionFailed, uri, httpEx);
        }

        public static bool IsNetworkReallyAvailable()
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
                return false;

            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Skip non-operational, loopback, and tunnel interfaces
                if ((ni.OperationalStatus != OperationalStatus.Up) ||
                    (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) ||
                    (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel))
                    continue;

                // Skip virtual adapters
                if ((ni.Description.IndexOf(@"virtual", StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (ni.Name.IndexOf(@"virtual", StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Disposes this HttpClientWithProgress instance.
        /// Note: The underlying HttpClient is a singleton and is never disposed.
        /// This method clears instance state but does not dispose the shared HttpClient.
        /// </summary>
        public void Dispose()
        {
            // Clear per-request state
            _authHeader = null;
            _customHeaders.Clear();
            // Note: _cookieContainer is managed by the caller (e.g., HttpPanoramaRequestHelper)
            // and should not be disposed here. The shared HttpClient is never disposed.
        }
    }

    /// <summary>
    /// Provides a singleton HttpClient instance that is shared across all HttpClientWithProgress instances.
    /// The HttpClient is created on first use and lives until process termination to avoid thread handle leaks.
    /// </summary>
    internal static class HttpClientSingleton
    {
        private static HttpClient _instance = CreateHttpClient();
        private static HttpClientHandler _handler;
        private static bool _disposed;

        public static HttpClient Instance => _instance;

        private static HttpClient CreateHttpClient()
        {
            // Register for process exit to dispose HttpClient cleanly
            // This prevents background threads from blocking process shutdown
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => DisposeInstance();

            // Use HttpClientHandler (SocketsHttpHandler is not available in .NET Framework 4.7.2)
            // Note: Connection pooling and DNS refresh are handled automatically by HttpClient
            _handler = new HttpClientHandler
            {
                UseProxy = true,
                Proxy = WebRequest.DefaultWebProxy,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseDefaultCredentials = true,
                PreAuthenticate = true,
                UseCookies = false  // We handle cookies per-request via HttpRequestMessage
            };
            if (_handler.Proxy != null)
            {
                _handler.Proxy.Credentials = CredentialCache.DefaultCredentials;
            }

            // Create HttpClient with disposeHandler: false so we manually dispose the handler on process exit
            // The HttpClient and handler are disposed on process exit to allow background threads to shut down cleanly
            // This prevents thread handle leaks from repeated creation/disposal during process lifetime
            return new HttpClient(_handler, disposeHandler: false)
            {
                Timeout = Timeout.InfiniteTimeSpan  // We handle timeouts per-chunk, not per-request
            };
        }

        /// <summary>
        /// Disposes the singleton HttpClient and its handler to allow background threads to shut down cleanly.
        /// Called automatically on process exit to prevent process shutdown from hanging.
        /// </summary>
        private static void DisposeInstance()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                // Dispose HttpClient to close connections and stop background threads
                _instance?.Dispose();

                // Dispose handler to release resources
                _handler?.Dispose();
            }
            catch (Exception)
            {
                // Ignore exceptions during shutdown - process is exiting anyway
            }
        }
    }

    /// <summary>
    /// Classifies the type of network failure that occurred.
    /// </summary>
    public enum NetworkFailureType
    {
        /// <summary>
        /// The server returned an HTTP error status code (e.g., 404, 500, 401).
        /// StatusCode will be non-null and ResponseBody may contain server-specific error details.
        /// </summary>
        HttpError,

        /// <summary>
        /// DNS resolution failed - the server name could not be resolved to an IP address.
        /// Check DNS settings, VPN, or verify the server name is correct.
        /// </summary>
        DnsResolution,

        /// <summary>
        /// The request timed out while waiting for the server to respond.
        /// The server may be slow, overloaded, or the network connection may be poor.
        /// </summary>
        Timeout,

        /// <summary>
        /// No network connection is available.
        /// The device is not connected to a network or the network adapter is disabled.
        /// </summary>
        NoConnection,

        /// <summary>
        /// Failed to establish a connection to the server.
        /// The server may be down, unreachable, or blocked by a firewall/proxy.
        /// </summary>
        ConnectionFailed,

        /// <summary>
        /// The connection was lost during the request (e.g., during download/upload).
        /// The network connection was interrupted after being successfully established.
        /// </summary>
        ConnectionLost
    }

    /// <summary>
    /// Exception thrown by HttpClientWithProgress when an HTTP request fails.
    /// Extends IOException for backward compatibility with existing catch blocks.
    /// Provides structured access to HTTP status code and request URI without requiring message parsing.
    /// </summary>
    public class NetworkRequestException : IOException
    {
        /// <summary>
        /// The type of network failure that occurred.
        /// </summary>
        public NetworkFailureType FailureType { get; }

        /// <summary>
        /// The HTTP status code returned by the server, or null if the failure occurred before receiving a response.
        /// Non-null only when FailureType is HttpError.
        /// </summary>
        public HttpStatusCode? StatusCode { get; }

        /// <summary>
        /// The URI that was requested when the error occurred.
        /// </summary>
        public Uri RequestUri { get; }

        /// <summary>
        /// The response body as a string, if available. Used for extracting server-specific error details.
        /// Non-null only when FailureType is HttpError and the server returned a response body.
        /// </summary>
        public string ResponseBody { get; }

        /// <summary>
        /// Creates a new NetworkRequestException for HTTP status code errors.
        /// </summary>
        /// <param name="message">User-friendly error message describing what went wrong</param>
        /// <param name="statusCode">HTTP status code returned by the server</param>
        /// <param name="requestUri">The URI that was being requested</param>
        /// <param name="innerException">The underlying exception that caused this error</param>
        /// <param name="responseBody">Optional response body for server-specific error extraction</param>
        public NetworkRequestException(string message, HttpStatusCode statusCode, Uri requestUri, Exception innerException, string responseBody = null)
            : base(message, innerException)
        {
            FailureType = NetworkFailureType.HttpError;
            StatusCode = statusCode;
            RequestUri = requestUri;
            ResponseBody = responseBody;
        }

        /// <summary>
        /// Creates a new NetworkRequestException for non-HTTP network failures.
        /// </summary>
        /// <param name="message">User-friendly error message describing what went wrong</param>
        /// <param name="failureType">The type of network failure that occurred</param>
        /// <param name="requestUri">The URI that was being requested</param>
        /// <param name="innerException">The underlying exception that caused this error</param>
        public NetworkRequestException(string message, NetworkFailureType failureType, Uri requestUri, Exception innerException)
            : base(message, innerException)
        {
            if (failureType == NetworkFailureType.HttpError)
                throw new ArgumentException(@"Use the other constructor for HttpError failures with a status code", nameof(failureType));

            FailureType = failureType;
            StatusCode = null;
            RequestUri = requestUri;
            ResponseBody = null;
        }

        /// <summary>
        /// Returns true if this exception represents a DNS resolution failure.
        /// </summary>
        public bool IsDnsFailure()
        {
            return FailureType == NetworkFailureType.DnsResolution;
        }

        public static HttpStatusCode? GetHttpStatusCode(Exception e)
        {
            while (e != null)
            {
                if (e is NetworkRequestException netEx)
                    return netEx.StatusCode;

                e = e.InnerException;
            }
            return null;
        }
    }
}

