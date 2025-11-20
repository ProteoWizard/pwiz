using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using pwiz.Common.SystemUtil;
using pwiz.PanoramaClient.Properties;

namespace pwiz.PanoramaClient
{
    public interface IRequestHelper : IDisposable
    {
        void SetProgressMonitor(IProgressMonitor progressMonitor, IProgressStatus progressStatus);
        JObject Get(Uri uri, string messageOnError = null);
        JObject Post(Uri uri, NameValueCollection postData, string messageOnError = null);
        JObject Post(Uri uri, string postData, string messageOnError);
        void DoRequest(Uri uri, string method, IDictionary<string, string> headers = null, string messageOnError = null);
        void RequestJsonResponse();
        void AsyncUploadFile(Uri address, string method, string fileName, IDictionary<string, string> headers = null);
    }

    public abstract class AbstractRequestHelper : IRequestHelper
    {
        protected const string APPLICATION_JSON = @"application/json";


        #region IRequestHelper methods

        public abstract void SetProgressMonitor(IProgressMonitor progressMonitor, IProgressStatus progressStatus);

        public abstract void Dispose();

        #endregion

        public abstract string DoGet(Uri uri);

        public abstract byte[] DoPost(Uri uri, NameValueCollection postData);

        public abstract string DoPost(Uri uri, string postData); // Used only in AuditLogTutorialTest

        public abstract void DoAsyncFileUpload(Uri address, string method, string fileName, IDictionary<string, string> headers = null);

        public abstract void AddHeader(HttpRequestHeader header, string value);

        public abstract string GetResponse(Uri uri, string method, IDictionary<string, string> headers = null);

        public JObject Get(Uri uri, string messageOnError = null)
        {
            try
            {
                var response = DoGet(uri);
                return ParseResponse(response, uri, messageOnError);
            }
            catch (NetworkRequestException e)
            {
                messageOnError ??= string.Format(Resources.AbstractRequestHelper_DoRequest__0__request_was_unsuccessful_, @"GET");
                throw PanoramaServerException.CreateWithLabKeyError(messageOnError, uri, PanoramaUtil.GetErrorFromNetworkRequestException, e);
            }
        }

        public JObject Post(Uri uri, NameValueCollection postData, string messageOnError = null)
        {
            postData ??= new NameValueCollection();
            return Post(uri, postData, null, messageOnError);
        }

        public JObject Post(Uri uri, string postData, string messageOnError = null)
        {
            return Post(uri, null, postData, messageOnError);
        }

        protected JObject Post(Uri uri, NameValueCollection postData, string postDataString, string messageOnError)
        {
            RequestJsonResponse();
            string response;
            try
            {
                if (postData != null)
                {
                    var responseBytes = DoPost(uri, postData);
                    response = Encoding.UTF8.GetString(responseBytes);
                }
                else
                {
                    AddHeader(HttpRequestHeader.ContentType, APPLICATION_JSON);
                    response = DoPost(uri, postDataString);
                }
                return ParseResponse(response, uri, messageOnError);
            }
            catch (NetworkRequestException e)
            {
                // HttpPanoramaRequestHelper throws NetworkRequestException
                messageOnError ??= string.Format(Resources.AbstractRequestHelper_DoRequest__0__request_was_unsuccessful_, @"POST");
                throw PanoramaServerException.CreateWithLabKeyError(messageOnError, uri, PanoramaUtil.GetErrorFromNetworkRequestException, e);
            }
        }

        public virtual void RequestJsonResponse()
        {
            // Get LabKey to send JSON instead of HTML.
            // When we request a JSON response, the HTTP status can be 200 even when the request fails on the server,
            // and the returned status would have been 400 otherwise. This is intentional:
            // Issue 44964: Don't use Bad Request/400 HTTP status codes for valid requests
            // https://www.labkey.org/home/Developer/issues/issues-details.view?issueId=44964
            // Notes from Josh:
            // 1.Malformed HTTP request, which doesn't comply with HTTP spec. 400 is clearly legit here.
            // 2.Well - formed HTTP requests with parameters that we don't like. This is less clear cut to me.
            // 3.Well - formed HTTP requests with legit parameters, but which exercise some sort of error scenario. We clearly shouldn't be sending a 400 for these.
            // 
            // We have to look for the status and any exception in the returned JSON rather than expecting a NetworkRequestException (non-200 response) if the request
            // fails on the server. Example JSON response with error: {"exception":"Filename may not contain space followed by dash.","success":false,"status":400}
            AddHeader(HttpRequestHeader.Accept, APPLICATION_JSON);
        }

        public void DoRequest(Uri uri, string method, IDictionary<string, string> headers = null, string messageOnError = null)
        {
            messageOnError ??= string.Format(Resources.AbstractRequestHelper_DoRequest__0__request_was_unsuccessful_, method);
            try
            {
                var response = GetResponse(uri, method, headers);
                // If the JSON response contains an exception message, throw a PanoramaServerException
                var labkeyError = PanoramaUtil.GetIfErrorInResponse(response);
                if (labkeyError != null)
                {
                    throw new PanoramaServerException(new ErrorMessageBuilder(messageOnError).Uri(uri)
                        .LabKeyError(labkeyError).ToString());
                }
            }
            catch (NetworkRequestException e)
            {
                // HttpPanoramaRequestHelper throws NetworkRequestException.
                // NetworkRequestException includes response body for error extraction.
                throw PanoramaServerException.CreateWithLabKeyError(messageOnError, uri, PanoramaUtil.GetErrorFromNetworkRequestException, e);
            }
        }

        public void AsyncUploadFile(Uri address, string method, string fileName, IDictionary<string, string> headers = null)
        {
            try
            {
                DoAsyncFileUpload(address, method, fileName, headers);
            }
            catch (NetworkRequestException e)
            {
                // HttpPanoramaRequestHelper throws NetworkRequestException.
                throw PanoramaServerException.CreateWithLabKeyError(
                    Resources.AbstractPanoramaClient_UploadTempZipFile_There_was_an_error_uploading_the_file_, address, PanoramaUtil.GetErrorFromNetworkRequestException, e);
            }
        }

        private JObject ParseJsonResponse(string response, Uri uri)
        {
            try
            {
                return JObject.Parse(response);
            }
            catch (JsonReaderException e)
            {
                throw new PanoramaServerException(new ErrorMessageBuilder(Resources.AbstractRequestHelper_ParseJsonResponse_Error_parsing_response_as_JSON_)
                    .ExceptionMessage(e.Message).Uri(uri).Response(response).ToString(), e);
            }
        }

        private JObject ParseResponse(string response, Uri uri, string messageOnError)
        {
            var jsonResponse = ParseJsonResponse(response, uri);
            var serverError = PanoramaUtil.GetIfErrorInResponse(jsonResponse);
            if (serverError != null)
            {
                throw new PanoramaServerException(new ErrorMessageBuilder(messageOnError).Uri(uri).LabKeyError(serverError).ToString());
            }

            return jsonResponse;
        }
    }

    /// <summary>
    /// RequestHelper implementation using HttpClientWithProgress for all network operations.
    /// Manages cookies and CSRF tokens for LabKey Server session management.
    /// </summary>
    public class HttpPanoramaRequestHelper : AbstractRequestHelper
    {
        private readonly CookieContainer _cookies;
        private readonly Uri _serverUri;
        private string _csrfToken;
        private IProgressMonitor _progressMonitor;
        private IProgressStatus _progressStatus;
        private const string LABKEY_CSRF = @"X-LABKEY-CSRF";
        private readonly PanoramaServer _server;
        private readonly Dictionary<HttpRequestHeader, string> _customHeaders = new Dictionary<HttpRequestHeader, string>();
        private bool _requestJsonResponse;

        public HttpPanoramaRequestHelper(PanoramaServer server, IProgressMonitor progressMonitor = null, IProgressStatus progressStatus = null)
        {
            _server = server;
            _serverUri = server.URI;
            _cookies = new CookieContainer();
            _progressMonitor = progressMonitor;
            _progressStatus = progressStatus;
        }

        public override void SetProgressMonitor(IProgressMonitor progressMonitor, IProgressStatus progressStatus)
        {
            _progressMonitor = progressMonitor;
            _progressStatus = progressStatus;
        }

        public override string DoGet(Uri uri)
        {
            // Don't show size for GET requests (typically fast API calls)
            // The expensive operation is server-side JSON generation, not the download
            using var httpClient = CreateHttpClient();
            httpClient.ShowTransferSize = false;
            return httpClient.DownloadString(uri);
        }

        public override byte[] DoPost(Uri uri, NameValueCollection postData)
        {
            try
            {
                // Ensure we have a CSRF token before POST
                GetCsrfTokenFromServer();

                using var httpClient = CreateHttpClient();
                
                // Add CSRF token header for all POST requests
                if (!string.IsNullOrEmpty(_csrfToken))
                {
                    httpClient.AddHeader(LABKEY_CSRF, _csrfToken);
                }

                // Convert NameValueCollection to URL-encoded form data
                var formData = new StringBuilder();
                foreach (string key in postData.Keys)
                {
                    if (formData.Length > 0)
                        formData.Append(@"&");
                    formData.Append(Uri.EscapeDataString(key));
                    formData.Append(@"=");
                    formData.Append(Uri.EscapeDataString(postData[key] ?? string.Empty));
                }

                return Encoding.UTF8.GetBytes(httpClient.UploadString(uri, PanoramaUtil.FORM_POST, formData.ToString()));
            }
            catch (NetworkRequestException ex)
            {
                // Clear CSRF token on 401 errors - we may need a fresh token
                if (ex.StatusCode == HttpStatusCode.Unauthorized)
                {
                    ClearCsrfToken();
                }
                throw;
            }
        }

        public override string DoPost(Uri uri, string postData)
        {
            try
            {
                // Ensure we have a CSRF token before POST
                GetCsrfTokenFromServer();

                using var httpClient = CreateHttpClient();
                
                // Add CSRF token header for all POST requests
                if (!string.IsNullOrEmpty(_csrfToken))
                {
                    httpClient.AddHeader(LABKEY_CSRF, _csrfToken);
                }

                // Check if a custom Content-Type was set (e.g., application/json for API calls)
                string contentType = @"application/x-www-form-urlencoded"; // Default for form posts
                if (_customHeaders.TryGetValue(HttpRequestHeader.ContentType, out var customContentType))
                {
                    contentType = customContentType;
                }

                return httpClient.UploadString(uri, PanoramaUtil.FORM_POST, postData, contentType);
            }
            catch (NetworkRequestException ex)
            {
                // Clear CSRF token on 401 errors - we may need a fresh token
                if (ex.StatusCode == HttpStatusCode.Unauthorized)
                {
                    ClearCsrfToken();
                }
                throw;
            }
        }

        public override void DoAsyncFileUpload(Uri address, string method, string fileName, IDictionary<string, string> headers = null)
        {
            // Add CSRF token for upload if needed
            if (method.Equals(PanoramaUtil.FORM_POST, StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(_csrfToken))
            {
                GetCsrfTokenFromServer();
            }

            try
            {
                using var httpClient = CreateHttpClient();
                
                // Add CSRF token header if available
                if (!string.IsNullOrEmpty(_csrfToken))
                {
                    httpClient.AddHeader(LABKEY_CSRF, _csrfToken);
                }

                AddHeadersToHttpClient(headers, httpClient);

                // UploadFile with response body - LabKey can return errors in JSON even with HTTP 200
                string responseBody = httpClient.UploadFileWithResponse(address, method, fileName);

                // Check for LabKey errors in the response body
                // This handles the case where LabKey returns HTTP 200 but includes an error in the JSON
                if (_requestJsonResponse && !string.IsNullOrEmpty(responseBody))
                {
                    var labKeyError = PanoramaUtil.GetIfErrorInResponse(responseBody);
                    if (labKeyError != null)
                    {
                        throw new PanoramaServerException(
                            new ErrorMessageBuilder(Resources.AbstractPanoramaClient_UploadTempZipFile_There_was_an_error_uploading_the_file_)
                                .Uri(address)
                                .LabKeyError(labKeyError).ToString());
                    }
                }
            }
            catch (NetworkRequestException ex)
            {
                // NetworkRequestException already has the response body - check for LabKey errors
                var labKeyError = PanoramaUtil.GetErrorFromNetworkRequestException(ex);
                if (labKeyError != null)
                {
                    // Re-throw with LabKey error details
                    throw PanoramaServerException.CreateWithLabKeyError(
                        Resources.AbstractPanoramaClient_UploadTempZipFile_There_was_an_error_uploading_the_file_,
                        address,
                        PanoramaUtil.GetErrorFromNetworkRequestException,
                        ex);
                }
                throw;
            }
        }

        private HttpClientWithProgress CreateHttpClient()
        {
            var httpClient = new HttpClientWithProgress(_progressMonitor, _progressStatus, _cookies);
            
            // Add authorization header if credentials are available
            if (_server.HasUserAccount())
            {
                httpClient.AddAuthorizationHeader(_server.AuthHeader);
            }

            // Add any custom headers that were set via AddHeader().
            // This will include the Accept header for JSON responses set via RequestJsonResponse().
            // Skip Content-Type here - it must be set on HttpContent, not DefaultRequestHeaders
            foreach (var header in _customHeaders)
            {
                if (header.Key != HttpRequestHeader.ContentType)
                {
                    httpClient.AddHeader(header.Key.ToString(), header.Value);
                }
            }

            return httpClient;
        }

        private void GetCsrfTokenFromServer()
        {
            if (string.IsNullOrEmpty(_csrfToken))
            {
                try
                {
                    // Make a request to get the CSRF token from the server
                    // After this, the token will be in the cookie container
                    using var httpClient = CreateHttpClient();
                    httpClient.DownloadString(new Uri(_serverUri, PanoramaUtil.ENSURE_LOGIN_PATH));
                    
                    // Extract the CSRF token from cookies
                    // GetCookie() returns the cookie value as a string
                    _csrfToken = httpClient.GetCookie(new Uri(_serverUri, "/"), LABKEY_CSRF);
                }
                catch (NetworkRequestException ex)
                {
                    // Wrap CSRF token retrieval failures with a more informative error message
                    var csrfUri = new Uri(_serverUri, PanoramaUtil.ENSURE_LOGIN_PATH);
                    throw PanoramaServerException.CreateWithLabKeyError(
                        Resources.HttpPanoramaRequestHelper_GetCsrfTokenFromServer_There_was_an_error_getting_a_CSRF_token_from_the_server_,
                        csrfUri, PanoramaUtil.GetErrorFromNetworkRequestException, ex);
                }
            }
        }

        public void ClearCsrfToken()
        {
            _csrfToken = null;
        }

        // These headers persist for the lifetime of the RequestHelper instance
        public override void AddHeader(HttpRequestHeader header, string value)
        {
            _customHeaders[header] = value;
        }

        public override void RequestJsonResponse()
        {
            _requestJsonResponse = true;
            base.RequestJsonResponse();
        }

        public override string GetResponse(Uri uri, string method, IDictionary<string, string> headers = null)
        {
            using var httpClient = CreateHttpClient();

            // For HEAD/DELETE/MOVE methods, use generic HTTP request
            using var httpRequest = new System.Net.Http.HttpRequestMessage(new System.Net.Http.HttpMethod(method), uri);

            AddHeadersToHttpClient(headers, httpClient);

            var response = httpClient.SendRequest(httpRequest);
            // Read response body
            return response.Content.ReadAsStringAsync().Result;
        }

        private static void AddHeadersToHttpClient(IDictionary<string, string> headers, HttpClientWithProgress httpClient)
        {
            if (headers == null) return;

            // Copy custom headers to the httpClient for a single request. 
            // These include headers like "Destination" and "Overwrite" for MOVE requests,
            // or "Temporary" for file upload requests.
            foreach (var header in headers)
            {
                var headerValue = header.Value;
                var headerName = header.Key;
                if (!string.IsNullOrEmpty(headerValue) && !ShouldSkipHeader(headerName))
                {
                    // NOTE: This method will add the headers to underlying HttpClient's DefaultRequestHeaders
                    // that applies to ALL requests made by the HttpClient.  
                    // This is safe for HttpPanoramaRequestHelper since it creates a new HttpClientWithProgress
                    // instance for each request so there will not be any header carryover.
                    httpClient.AddHeader(headerName, headerValue);
                }
            }
        }

        private static bool ShouldSkipHeader(string headerName)
        {
            // Skip headers that are handled by other parts of the code
            // Note: HttpRequestHeader.ContentType.ToString() returns "ContentType" (no hyphen)
            // but the actual HTTP header name is "Content-Type" (with hyphen), so check both
            return headerName.Equals(HttpRequestHeader.Authorization.ToString(), StringComparison.OrdinalIgnoreCase) ||
                   headerName.Equals(HttpRequestHeader.Accept.ToString(), StringComparison.OrdinalIgnoreCase) ||
                   headerName.Equals(HttpRequestHeader.ContentType.ToString(), StringComparison.OrdinalIgnoreCase) ||
                   headerName.Equals(@"Content-Type", StringComparison.OrdinalIgnoreCase) ||
                   headerName.Equals(LABKEY_CSRF, StringComparison.OrdinalIgnoreCase);
        }

        public override void Dispose()
        {
            // HttpClientWithProgress instances are created and disposed per-request
            // CookieContainer and CSRF token persist for the lifetime of this RequestHelper
            // Custom headers added via AddHeader() also persist for the lifetime of this RequestHelper
        }
    }

    public class LabKeyError
    {
        public string ErrorMessage { get; }
        public int? Status { get; }

        public LabKeyError(string exception, int? status)
        {
            ErrorMessage = exception;
            Status = status;
        }

        public override string ToString()
        {
            var serverError = ErrorMessage;
            if (Status != null)
                serverError = CommonTextUtil.LineSeparate(serverError,
                    string.Format(Resources.LabKeyError_ToString_Response_status___0_, Status));
            return serverError;
        }
    }
}
