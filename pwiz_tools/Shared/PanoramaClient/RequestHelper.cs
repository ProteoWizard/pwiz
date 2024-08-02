using System;
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
        private const string APPLICATION_JSON = @"application/json";


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

    public class PanoramaRequestHelper : AbstractRequestHelper
    {
        private readonly WebClientWithCredentials _client;
        
        public PanoramaRequestHelper(WebClientWithCredentials webClient)
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
