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
        JObject Get(Uri uri, string messageOnError = null);
        JObject Post(Uri uri, NameValueCollection postData, string messageOnError = null);
        JObject Post(Uri uri, string postData, string messageOnError);
        void DoRequest(HttpWebRequest request, string method, string authHeader, string messageOnError = null);
        void RequestJsonResponse();
        void AddHeader(string name, string value);
        void RemoveHeader(string name);
        void AsyncUploadFile(Uri address, string method, string fileName);
        void CancelAsyncUpload();
        void AddUploadFileCompletedEventHandler(UploadFileCompletedEventHandler handler);
        void AddUploadProgressChangedEventHandler(UploadProgressChangedEventHandler handler);
    }

    public abstract class AbstractRequestHelper : IRequestHelper
    {
        protected const string APPLICATION_JSON = @"application/json";


        #region IRequestHelper methods

        public abstract void AddHeader(string name, string value);

        public abstract void RemoveHeader(string name);

        public abstract void CancelAsyncUpload();

        public abstract void AddUploadFileCompletedEventHandler(UploadFileCompletedEventHandler handler);

        public abstract void AddUploadProgressChangedEventHandler(UploadProgressChangedEventHandler handler);

        public abstract void Dispose();

        #endregion

        public abstract string DoGet(Uri uri);

        public abstract byte[] DoPost(Uri uri, NameValueCollection postData);

        public abstract string DoPost(Uri uri, string postData); // Used only in AuditLogTutorialTest

        public abstract void DoAsyncFileUpload(Uri address, string method, string fileName);
        
        public abstract void AddHeader(HttpRequestHeader header, string value);

        public abstract string GetResponse(HttpWebRequest request);

        public abstract LabKeyError GetErrorFromException(WebException e);

        public JObject Get(Uri uri, string messageOnError = null)
        {
            try
            {
                var response = DoGet(uri);
                return ParseResponse(response, uri, messageOnError);
            }
            catch (WebException e)
            {
                throw NewPanoramaServerException(messageOnError, uri, @"GET", e);
            }
            catch (NetworkRequestException e)
            {
                // HttpPanoramaRequestHelper throws NetworkRequestException instead of WebException
                messageOnError ??= string.Format(Resources.AbstractRequestHelper_DoRequest__0__request_was_unsuccessful_, @"GET");
                throw PanoramaServerException.CreateWithResponseDisposal(messageOnError, uri, PanoramaUtil.GetErrorFromNetworkRequestException, e);
            }
        }

        private PanoramaServerException NewPanoramaServerException(string messageOnError, Uri uri, string requestMethod, WebException e)
        {
            messageOnError ??= string.Format(Resources.AbstractRequestHelper_DoRequest__0__request_was_unsuccessful_, requestMethod);
            return PanoramaServerException.CreateWithResponseDisposal(messageOnError, uri, GetErrorFromException, e);
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

        protected virtual JObject Post(Uri uri, NameValueCollection postData, string postDataString, string messageOnError)
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
            catch (WebException e)
            {
                throw NewPanoramaServerException(messageOnError, uri, @"POST", e);
            }
            catch (NetworkRequestException e)
            {
                // HttpPanoramaRequestHelper throws NetworkRequestException instead of WebException
                messageOnError ??= string.Format(Resources.AbstractRequestHelper_DoRequest__0__request_was_unsuccessful_, @"POST");
                throw PanoramaServerException.CreateWithResponseDisposal(messageOnError, uri, PanoramaUtil.GetErrorFromNetworkRequestException, e);
            }
        }

        public void RequestJsonResponse()
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
            // We have to look for the status and any exception in the returned JSON rather than expecting a WebException (non-200 response) if the request
            // fails on the server. Example JSON response with error: {"exception":"Filename may not contain space followed by dash.","success":false,"status":400}
            AddHeader(HttpRequestHeader.Accept, APPLICATION_JSON);
        }

        public void DoRequest(HttpWebRequest request, string method, string authHeader, string messageOnError = null)
        {
            request.Method = method;
            request.Headers.Add(HttpRequestHeader.Authorization, authHeader);
            request.Accept = APPLICATION_JSON; // Get LabKey to send JSON instead of HTML

            messageOnError ??= string.Format(Resources.AbstractRequestHelper_DoRequest__0__request_was_unsuccessful_, method);
            try
            {
                var response = GetResponse(request);
                // If the JSON response contains an exception message, throw a PanoramaServerException
                var labkeyError = PanoramaUtil.GetIfErrorInResponse(response);
                if (labkeyError != null)
                {
                    throw new PanoramaServerException(new ErrorMessageBuilder(messageOnError).Uri(request.RequestUri)
                        .LabKeyError(labkeyError).ToString());
                }
            }
            catch (WebException e)
            {
                throw NewPanoramaServerException(messageOnError, request.RequestUri, method, e);
            }
        }

        public void AsyncUploadFile(Uri address, string method, string fileName)
        {
            try
            {
                DoAsyncFileUpload(address, method, fileName);
            }
            catch (WebException e)
            {
                throw PanoramaServerException.CreateWithResponseDisposal(
                    Resources.AbstractPanoramaClient_UploadTempZipFile_There_was_an_error_uploading_the_file_, address, GetErrorFromException, e);
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
                throw PanoramaServerException.Create(
                    Resources.AbstractRequestHelper_ParseJsonResponse_Error_parsing_response_as_JSON_, uri, response, e);
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
    /// DEPRECATED: Use HttpPanoramaRequestHelper instead.
    /// This class remains for backward compatibility with AutoQC and SkylineBatch executables.
    /// Uses LabkeySessionWebClient (WebClient-based) for network operations.
    /// See TODO-tools_webclient_replacement.md for migration plan.
    /// </summary>
    public class PanoramaRequestHelper : AbstractRequestHelper
    {
        private readonly LabkeySessionWebClient _client;
        
        public PanoramaRequestHelper(LabkeySessionWebClient webClient)
        {
            _client = webClient;
        }

        public override string DoGet(Uri uri)
        {
            return _client.DownloadString(uri);
        }

        public override void AddHeader(string name, string value)
        {
            _client.Headers.Add(name, value);
        }

        public override void AddHeader(HttpRequestHeader header, string value)
        {
            _client.Headers.Add(header, value);
        }

        public override void RemoveHeader(string name)
        {
            _client.Headers.Remove(name);
        }

        public override byte[] DoPost(Uri uri, NameValueCollection postData)
        {
            return _client.UploadValues(uri, PanoramaUtil.FORM_POST, postData);
        }

        public override string DoPost(Uri uri, string postData)
        {
            return _client.UploadString(uri, PanoramaUtil.FORM_POST, postData);
        }


        protected override JObject Post(Uri uri, NameValueCollection postData, string postDataString, string messageOnError)
        {
            // Ensure we have a CSRF token before POST
            try
            {
                _client.GetCsrfTokenFromServer();
            }
            catch (WebException e)
            {
                throw PanoramaServerException.CreateWithResponseDisposal(
                    Resources.PanoramaRequestHelper_Post_There_was_an_error_getting_a_CSRF_token_from_the_server_,
                    uri,
                    GetErrorFromException,
                    e);
            }

            try
            {
                return base.Post(uri, postData, postDataString, messageOnError);
            }
            catch (PanoramaServerException e)
            {
                if (e.HttpStatus == HttpStatusCode.Unauthorized)
                {
                    // Clear the CSRF token if there is an authentication error. We may need to just get a new token and try the request again.
                    // An example is the PanoramaPinger class in the AutoQC project that sends a POST request to the Panorama server every few minutes.
                    _client.ClearCsrfToken();
                }
                throw;
            }
        }

        public override string GetResponse(HttpWebRequest request)
        {
            return PanoramaUtil.GetResponseString(request.GetResponse());
        }

        public override LabKeyError GetErrorFromException(WebException e)
        {
            return PanoramaUtil.GetErrorFromWebException(e);
        }

        public override void DoAsyncFileUpload(Uri address, string method, string fileName)
        {
            _client.UploadFileAsync(address, method, fileName);
        }

        public override void CancelAsyncUpload()
        {
            _client.CancelAsync();
        }

        public override void AddUploadFileCompletedEventHandler(UploadFileCompletedEventHandler handler)
        {
            _client.UploadFileCompleted += handler;
        }

        public override void AddUploadProgressChangedEventHandler(UploadProgressChangedEventHandler handler)
        {
            _client.UploadProgressChanged += handler;
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _client?.Dispose();
            }
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
        private readonly Dictionary<string, string> _customHeaders = new Dictionary<string, string>();
        private bool _requestJsonResponse;

        public HttpPanoramaRequestHelper(PanoramaServer server, IProgressMonitor progressMonitor = null, IProgressStatus progressStatus = null)
        {
            _server = server;
            _serverUri = server.URI;
            _cookies = new CookieContainer();
            _progressMonitor = progressMonitor;
            _progressStatus = progressStatus;
        }

        public override string DoGet(Uri uri)
        {
            using var httpClient = CreateHttpClient();
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
                        formData.Append("&");
                    formData.Append(Uri.EscapeDataString(key));
                    formData.Append("=");
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
                // Note: HttpRequestHeader.ContentType.ToString() returns "ContentType" (no hyphen)
                // Also check "Content-Type" in case it was added via string overload
                string contentType = @"application/x-www-form-urlencoded"; // Default for form posts
                if (_customHeaders.TryGetValue(HttpRequestHeader.ContentType.ToString(), out var customContentType) ||
                    _customHeaders.TryGetValue("Content-Type", out customContentType))
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

        public override void DoAsyncFileUpload(Uri address, string method, string fileName)
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
                    throw PanoramaServerException.CreateWithResponseDisposal(
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

            // Add any custom headers that were set via AddHeader()
            // Skip Content-Type here - it must be set on HttpContent, not DefaultRequestHeaders
            foreach (var header in _customHeaders)
            {
                if (header.Key != HttpRequestHeader.ContentType.ToString() && header.Key != "Content-Type")
                {
                    httpClient.AddHeader(header.Key, header.Value);
                }
            }

            // Add Accept: application/json if requested
            if (_requestJsonResponse)
            {
                httpClient.AddHeader("Accept", APPLICATION_JSON);
            }

            return httpClient;
        }

        private void GetCsrfTokenFromServer()
        {
            if (string.IsNullOrEmpty(_csrfToken))
            {
                // Make a request to get the CSRF token from the server
                // After this, the token will be in the cookie container
                using var httpClient = CreateHttpClient();
                httpClient.DownloadString(new Uri(_serverUri, PanoramaUtil.ENSURE_LOGIN_PATH));
                
                // Extract the CSRF token from cookies
                // GetCookie() returns the cookie value as a string
                _csrfToken = httpClient.GetCookie(new Uri(_serverUri, "/"), LABKEY_CSRF);
            }
        }

        public void ClearCsrfToken()
        {
            _csrfToken = null;
        }

        public override void AddHeader(string name, string value)
        {
            _customHeaders[name] = value;
        }

        public override void AddHeader(HttpRequestHeader header, string value)
        {
            _customHeaders[header.ToString()] = value;
        }

        public override void RemoveHeader(string name)
        {
            _customHeaders.Remove(name);
        }

        public new void RequestJsonResponse()
        {
            _requestJsonResponse = true;
        }

        public override void CancelAsyncUpload()
        {
            // Cancellation is handled via IProgressMonitor.IsCanceled
            // No async operations to cancel - uploads are synchronous with progress callbacks
        }

        public override void AddUploadFileCompletedEventHandler(UploadFileCompletedEventHandler handler)
        {
            // No-op: HttpClientWithProgress uses synchronous uploads with IProgressMonitor
            // Event handlers are not needed since UploadFile() blocks until complete
        }

        public override void AddUploadProgressChangedEventHandler(UploadProgressChangedEventHandler handler)
        {
            // No-op: HttpClientWithProgress uses IProgressMonitor for progress, not events
            // Progress is reported automatically during upload via the IProgressMonitor passed to constructor
        }

        public override string GetResponse(HttpWebRequest request)
        {
            // DoRequest() calls this after setting Method, Authorization, and Accept headers
            // We extract the needed info from the HttpWebRequest and use HttpClient instead
            var uri = request.RequestUri;
            var method = request.Method;
            
            using var httpClient = CreateHttpClient();
            
            // The request.Accept was set to application/json in DoRequest()
            httpClient.AddHeader("Accept", APPLICATION_JSON);
            
            // For HEAD/DELETE/MOVE methods, use generic HTTP request
            using var httpRequest = new System.Net.Http.HttpRequestMessage(new System.Net.Http.HttpMethod(method), uri);
            var response = httpClient.SendRequest(httpRequest);
            
            // Read response body
            return response.Content.ReadAsStringAsync().Result;
        }

        public override LabKeyError GetErrorFromException(WebException e)
        {
            // For compatibility - but HttpPanoramaRequestHelper throws NetworkRequestException instead
            return PanoramaUtil.GetErrorFromWebException(e);
        }

        public override void Dispose()
        {
            // HttpClientWithProgress instances are created and disposed per-request
            // CookieContainer and CSRF token persist for the lifetime of this RequestHelper
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
