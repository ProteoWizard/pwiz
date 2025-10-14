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
        private readonly IProgressMonitor _progressMonitor;
        private IProgressStatus _progressStatus;
        private const int ReadTimeoutMilliseconds = 15000; // timeout per chunk to avoid long hangs when network drops

        /// <summary>
        /// Creates an HttpClient wrapper with optional progress reporting.
        /// </summary>
        /// <param name="progressMonitor">Progress monitor for reporting download progress and handling cancellation</param>
        /// <param name="status">Initial progress status to update with an ID tracked by the progress monitor. If null, creates a new ProgressStatus.</param>
        public HttpClientWithProgress(IProgressMonitor progressMonitor, IProgressStatus status = null)
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

            _httpClient = new HttpClient(handler);
            _progressMonitor = progressMonitor;
            _progressStatus = status ?? new ProgressStatus();
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
            var response = GetResponse(uri);
            response.EnsureSuccessStatusCode();
            return WithExceptionHandling(uri, () => response.Content.ReadAsStringAsync().Result);
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
            response.EnsureSuccessStatusCode();
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
            response.EnsureSuccessStatusCode();
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
            var response = GetResponseHeadersRead(uri);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var downloadedBytes = 0L;

            using var contentStream = response.Content.ReadAsStreamAsync().Result;
            var buffer = new byte[8192];

            while (true)
            {
                int bytesRead;
                try
                {
                    bytesRead = ReadWithTimeout(contentStream, buffer, ReadTimeoutMilliseconds, uri);
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

                // Report progress
                if (totalBytes > 0)
                {
                    var percentage = (int)(downloadedBytes * 100 / totalBytes);
                    _progressMonitor.UpdateProgress(_progressStatus =
                        _progressStatus.ChangePercentComplete(percentage));
                }
            }
        }

        /// <summary>
        /// Gets a response from the specified URI with default completion option.
        /// </summary>
        private HttpResponseMessage GetResponse(Uri uri)
        {
            return WithExceptionHandling(uri,
                () => _httpClient.GetAsync(uri, CancellationToken).Result);
        }

        /// <summary>
        /// Gets a response from the specified URI with ResponseHeadersRead completion option for streaming.
        /// </summary>
        private HttpResponseMessage GetResponseHeadersRead(Uri uri)
        {
            return WithExceptionHandling(uri,
                () => _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, CancellationToken).Result);
        }

        /// <summary>
        /// Sends an HTTP request with cancellation support.
        /// </summary>
        private HttpResponseMessage SendRequest(HttpRequestMessage request)
        {
            return WithExceptionHandling(request.RequestUri,
                () => _httpClient.SendAsync(request, CancellationToken).Result);
        }

        public static Exception TestFailure;
        
        private TRet WithExceptionHandling<TRet>(Uri uri, Func<TRet> action)
        {
            try
            {
                if (TestFailure != null)
                    throw TestFailure;
                
                return action();
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

            if (!NetworkInterface.GetIsNetworkAvailable())
                return new IOException(MessageResources.HttpClientWithProgress_MapHttpException_No_network_connection_detected__Please_check_your_internet_connection_and_try_again_, root);

            if (root is HttpRequestException httpEx)
            {
                string server = uri?.Host ?? MessageResources.HttpClientWithProgress_MapHttpException_server;
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

        private static int ReadWithTimeout(Stream stream, byte[] buffer, int timeoutMs, Uri uri)
        {
            var readTask = stream.ReadAsync(buffer, 0, buffer.Length);
            var completed = Task.WaitAny(readTask, Task.Delay(timeoutMs));
            if (completed != 0)
                throw new TimeoutException(string.Format(MessageResources.HttpClientWithProgress_ReadWithTimeout_The_read_operation_timed_out_while_downloading_from__0__, uri));
            return readTask.Result;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}

