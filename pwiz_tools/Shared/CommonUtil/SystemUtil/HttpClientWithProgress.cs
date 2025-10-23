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
    /// </summary>
    public class HttpClientWithProgress : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly CookieContainer _cookieContainer;
        private readonly IProgressMonitor _progressMonitor;
        private IProgressStatus _progressStatus;
        private string _progressMessageWithoutSize; // Base message before download size is appended
        private const int ReadTimeoutMilliseconds = 15000; // timeout per chunk to avoid long hangs when network drops

        /// <summary>
        /// Creates an HttpClient wrapper with optional progress reporting.
        /// </summary>
        /// <param name="progressMonitor">Progress monitor for reporting download progress and handling cancellation</param>
        /// <param name="status">Initial progress status to update with an ID tracked by the progress monitor. If null, creates a new ProgressStatus.</param>
        /// <param name="cookieContainer">Optional cookie container for session management (e.g., Panorama authentication). If null, cookies are not persisted.</param>
        public HttpClientWithProgress(IProgressMonitor progressMonitor, IProgressStatus status = null, CookieContainer cookieContainer = null)
        {
            // Ensure HttpClient respects system proxy (including PAC) and supports gzip/deflate
            var handler = new HttpClientHandler
            {
                UseProxy = true,
                Proxy = WebRequest.DefaultWebProxy,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseDefaultCredentials = true,
                PreAuthenticate = true
            };
            if (handler.Proxy != null)
            {
                handler.Proxy.Credentials = CredentialCache.DefaultCredentials;
            }

            // Configure cookie container for session management (Panorama, authenticated sites)
            if (cookieContainer != null)
            {
                handler.CookieContainer = cookieContainer;
                handler.UseCookies = true;
                _cookieContainer = cookieContainer;
            }

            _httpClient = new HttpClient(handler);
            _progressMonitor = progressMonitor;
            _progressStatus = status ?? new ProgressStatus();
        }

        /// <summary>
        /// Adds an Authorization header to all requests made by this HttpClient instance.
        /// Use this for authenticated downloads (e.g., Basic auth, Bearer tokens).
        /// </summary>
        /// <param name="authHeaderValue">The authorization header value (e.g., "Basic base64credentials" or "Bearer token")</param>
        public void AddAuthorizationHeader(string authHeaderValue)
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", authHeaderValue);
        }

        /// <summary>
        /// Adds a custom header to all requests made by this HttpClient instance.
        /// Use this for CSRF tokens, API keys, or other custom headers required by the server.
        /// If the header already exists, it will be removed and replaced with the new value.
        /// </summary>
        /// <param name="name">The header name (e.g., "X-LABKEY-CSRF", "X-API-Key")</param>
        /// <param name="value">The header value</param>
        public void AddHeader(string name, string value)
        {
            // Remove existing header if present
            if (_httpClient.DefaultRequestHeaders.Contains(name))
            {
                _httpClient.DefaultRequestHeaders.Remove(name);
            }
            // Add new header
            _httpClient.DefaultRequestHeaders.Add(name, value);
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
        public void DownloadFile(Uri uri, string fileName)
        {
            using var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
            DownloadToStream(uri, fileStream);
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
            var request = new HttpRequestMessage(HttpMethod.Head, uri);
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
            var content = new StringContent(data, Encoding.UTF8, @"application/x-www-form-urlencoded");
            var request = new HttpRequestMessage(new HttpMethod(method), uri) { Content = content };

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

            var request = new HttpRequestMessage(new HttpMethod(method), uri) { Content = formContent };

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
        /// Downloads content from the specified URI to the provided stream with progress reporting and cancellation support.
        /// </summary>
        private void DownloadToStream(Uri uri, Stream outputStream)
        {
            // Check if we should use a mock response stream for testing
            Stream contentStream;
            long totalBytes;
            
            if (TestBehavior != null)
            {
                contentStream = TestBehavior.GetMockResponseStream(uri, out totalBytes);
                if (contentStream != null)
                {
                    // Use mock stream for testing
                    using (contentStream)
                    {
                        DownloadFromStream(contentStream, outputStream, totalBytes, uri);
                    }
                    return;
                }
            }

            // Normal path: get response from network
            var response = GetResponseHeadersRead(uri);
            totalBytes = response.Content.Headers.ContentLength ?? 0;
            contentStream = response.Content.ReadAsStreamAsync().Result;
            
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
                    throw MapHttpException(ex, uri);
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
                    _progressMonitor.UpdateProgress(_progressStatus =
                        _progressStatus.ChangeMessage(message));
                }
            }
        }

        private string GetProgressMessage(long totalBytes, long downloadedBytes)
        {
            // Capture base message on first progress update
            _progressMessageWithoutSize ??= _progressStatus.Message;

            // Report progress with download size appended
            return GetProgressMessageWithSize(_progressMessageWithoutSize, downloadedBytes, totalBytes);
        }

        /// <summary>
        /// Builds progress message with size information appended.
        /// Prevents size from being appended repeatedly by using separate base message and size components.
        /// </summary>
        /// <param name="baseMessage">The base progress message without size (e.g., "Downloading file.txt")</param>
        /// <param name="transferredBytes">Number of bytes transferred</param>
        /// <param name="totalBytes">Total bytes to transfer (0 if unknown)</param>
        /// <returns>Progress message with size appended in format: "{baseMessage}\n\n{transferred} / {total}"</returns>
        public static string GetProgressMessageWithSize(string baseMessage, long transferredBytes, long totalBytes)
        {
            return new StringBuilder()
                .AppendLine(baseMessage)
                .AppendLine()
                .AppendLine(FormatDownloadSize(transferredBytes, totalBytes))
                .ToString();
        }

        /// <summary>
        /// Formats download size for progress display (e.g., "5.2 MB / 10.4 MB" or "5.2 MB").
        /// </summary>
        private static string FormatDownloadSize(long downloaded, long totalBytes)
        {
            var formatProvider = new FileSizeFormatProvider();
            if (totalBytes > 0)
            {
                return string.Format(@"{0} / {1}",
                    string.Format(formatProvider, @"{0:fs1}", downloaded),
                    string.Format(formatProvider, @"{0:fs1}", totalBytes));
            }
            return string.Format(formatProvider, @"{0:fs1}", downloaded);
        }

        /// <summary>
        /// Sends an HTTP request with cancellation support.
        /// </summary>
        private HttpResponseMessage SendRequest(HttpRequestMessage request)
        {
            return WithExceptionHandling(request.RequestUri,
                () => _httpClient.SendAsync(request, CancellationToken).Result);
        }

        /// <summary>
        /// Gets a response from the specified URI with ResponseHeadersRead completion option for streaming.
        /// </summary>
        private HttpResponseMessage GetResponseHeadersRead(Uri uri)
        {
            return WithExceptionHandling(uri,
                () => _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, CancellationToken).Result);
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
            /// Gets a destination stream for testing upload operations.
            /// Upload data will be written to this stream instead of being sent to the network.
            /// Returns null to use actual network upload.
            /// </summary>
            /// <param name="uri">The URI being uploaded to</param>
            /// <returns>A stream to capture uploaded data, or null to use actual network</returns>
            Stream GetMockUploadStream(Uri uri);
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
                throw MapHttpException(ex, uri);
            }
        }

        private HttpResponseMessage WithExceptionHandling(Uri uri, Func<HttpResponseMessage> getResponse)
        {
            try
            {
                if (TestBehavior?.FailureException != null)
                    throw TestBehavior.FailureException;
                
                var response = getResponse();
                response.EnsureSuccessStatusCode();
                return response;
            }
            catch (Exception ex)
            {
                throw MapHttpException(ex, uri);
            }
        }

        private Exception MapHttpException(Exception ex, Uri uri)
        {
            var root = ex is AggregateException ae ? ae.Flatten().InnerExceptions.FirstOrDefault() ?? ex : ex;

            // Check for cancellation first (but distinguish between user cancellation and timeout)
            if (root is TaskCanceledException)
                return new IOException(string.Format(MessageResources.HttpClientWithProgress_MapHttpException_The_request_to__0__timed_out__Please_try_again_, uri), root);

            if (root is OperationCanceledException)
                return root;

            if (root is TimeoutException)
                return new IOException(string.Format(MessageResources.HttpClientWithProgress_MapHttpException_The_request_to__0__timed_out__Please_try_again_, uri), root);

            if (root is NoNetworkTestException || !NetworkInterface.GetIsNetworkAvailable())
                return new IOException(MessageResources.HttpClientWithProgress_MapHttpException_No_network_connection_detected__Please_check_your_internet_connection_and_try_again_, root);

            if (root is HttpRequestException httpEx)
            {
                string server = uri?.Host ?? MessageResources.HttpClientWithProgress_MapHttpException_server;
                
                // Check if this is an HTTP status code error from EnsureSuccessStatusCode()
                if (httpEx.Message.Contains("Response status code does not indicate success"))
                {
                    // Extract status code from message like "Response status code does not indicate success: 404 (Not Found)"
                    var match = System.Text.RegularExpressions.Regex.Match(httpEx.Message, @"(\d{3})\s*\(([^)]+)\)");
                    if (match.Success)
                    {
                        var statusCode = int.Parse(match.Groups[1].Value);
                        var reasonPhrase = match.Groups[2].Value;
                        
                        // Use full URI for user context, but preserve the original HttpRequestException
                        // with status code details in the inner exception chain for troubleshooting
                        string uriString = uri?.ToString() ?? server;
                        string message;
                        switch (statusCode)
                        {
                            case 404:
                                message = string.Format(MessageResources.HttpClientWithProgress_MapHttpException_The_requested_resource_at__0__was_not_found__HTTP_404___Please_verify_the_URL_, uriString);
                                break;
                            case 500:
                                message = string.Format(MessageResources.HttpClientWithProgress_MapHttpException_The_server__0__encountered_an_internal_error__HTTP_500___Please_try_again_later_or_contact_the_server_administrator_, server);
                                break;
                            case 401:
                                message = string.Format(MessageResources.HttpClientWithProgress_MapHttpException_Access_to__0__was_denied__HTTP_401___Authentication_may_be_required_, uriString);
                                break;
                            case 403:
                                message = string.Format(MessageResources.HttpClientWithProgress_MapHttpException_Access_to__0__was_forbidden__HTTP_403___You_may_not_have_permission_to_access_this_resource_, uriString);
                                break;
                            case 429:
                                message = string.Format(MessageResources.HttpClientWithProgress_MapHttpException_Too_many_requests_to__0___HTTP_429___Please_wait_before_trying_again_, server);
                                break;
                            default:
                                message = string.Format(MessageResources.HttpClientWithProgress_MapHttpException_The_server__0__returned_an_error__HTTP__1____Please_try_again_or_contact_support_, server, statusCode);
                                break;
                        }
                        // Wrap with NetworkRequestException to provide structured access to status code
                        // Preserves original HttpRequestException as inner exception for detailed troubleshooting
                        return new NetworkRequestException(message, (HttpStatusCode)statusCode, uri, root);
                    }
                }
                
                // DNS resolution failure (e.g., 'The remote name could not be resolved')
                if (httpEx.InnerException is WebException webEx && webEx.Status == WebExceptionStatus.NameResolutionFailure)
                    return new IOException(string.Format(MessageResources.HttpClientWithProgress_MapHttpException_Failed_to_resolve_host__0___Please_check_your_DNS_settings_or_VPN_proxy_, server), root);

                return new IOException(string.Format(MessageResources.HttpClientWithProgress_MapHttpException_Failed_to_connect_to__0___Please_check_your_network_connection__VPN_proxy__or_firewall_, server), root);
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
                    return new IOException(MessageResources.HttpClientWithProgress_MapHttpException_The_connection_was_lost_during_download__Please_check_your_internet_connection_and_try_again_, root);
                }
            }

            return root;
        }

        private int ReadChunk(Stream stream, byte[] buffer, Uri uri)
        {
            var readTask = stream.ReadAsync(buffer, 0, buffer.Length, CancellationToken);
            var completed = Task.WaitAny(readTask, Task.Delay(ReadTimeoutMilliseconds, CancellationToken));

            // Check if cancellation was requested before throwing timeout exception
            CancellationToken.ThrowIfCancellationRequested();

            if (completed != 0)
                throw new TimeoutException(string.Format(MessageResources.HttpClientWithProgress_ReadWithTimeout_The_read_operation_timed_out_while_downloading_from__0__, uri));
            return readTask.Result;
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
        /// Uploads a stream to the specified URI with progress reporting and cancellation support.
        /// Streams directly from input to network without buffering entire file in memory.
        /// Progress is reported during the network upload (the expensive operation), not during file read.
        /// </summary>
        private void UploadFromStream(Uri uri, string method, Stream inputStream, string fileName = null)
        {
            long totalBytes = inputStream.Length;

            // Wrap stream with progress tracking - ProgressStream.Read() will be called by HttpClient
            // during SendAsync(), reporting progress during the actual NETWORK upload
            var progressStream = new ProgressStream(inputStream, totalBytes, (uploaded) =>
            {
                UpdateUploadProgress(uploaded, totalBytes, uri);
            });

            // Build the HTTP request (exercises request building code in tests)
            var request = new HttpRequestMessage(new HttpMethod(method), uri);
            request.Content = new StreamContent(progressStream);
            
            if (!string.IsNullOrEmpty(fileName))
            {
                request.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                {
                    FileName = fileName
                };
            }

            // Send the upload to the network (or simulate for testing)
            // HttpClient reads from progressStream AS IT UPLOADS, triggering progress callbacks
            WithExceptionHandling(uri, () =>
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
                var response = _httpClient.SendAsync(request, CancellationToken).Result;
                response.EnsureSuccessStatusCode();
                return response;
            });
        }

        private void UpdateUploadProgress(long uploadedBytes, long totalBytes, Uri uri)
        {
            if (_progressMonitor == null)
                return;

            var message = GetProgressMessage(totalBytes, uploadedBytes);

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

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    /// <summary>
    /// Exception thrown by HttpClientWithProgress when an HTTP request fails.
    /// Extends IOException for backward compatibility with existing catch blocks.
    /// Provides structured access to HTTP status code and request URI without requiring message parsing.
    /// </summary>
    public class NetworkRequestException : IOException
    {
        /// <summary>
        /// The HTTP status code returned by the server, or null if the failure occurred before receiving a response.
        /// </summary>
        public HttpStatusCode? StatusCode { get; }

        /// <summary>
        /// The URI that was requested when the error occurred.
        /// </summary>
        public Uri RequestUri { get; }

        /// <summary>
        /// Creates a new NetworkRequestException with the specified message, status code, URI, and inner exception.
        /// </summary>
        /// <param name="message">User-friendly error message describing what went wrong</param>
        /// <param name="statusCode">HTTP status code if available, or null for non-HTTP errors</param>
        /// <param name="requestUri">The URI that was being requested</param>
        /// <param name="innerException">The underlying exception that caused this error</param>
        public NetworkRequestException(string message, HttpStatusCode? statusCode, Uri requestUri, Exception innerException)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            RequestUri = requestUri;
        }
    }
}

