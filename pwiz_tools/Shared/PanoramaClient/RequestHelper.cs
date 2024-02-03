using System;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using pwiz.PanoramaClient.Properties;

namespace pwiz.PanoramaClient
{
    public interface IRequestHelper : IDisposable
    {
        string DoGet(Uri uri);
        JObject Get(Uri uri, string messageOnLabkeyError = null);
        byte[] DoPost(Uri uri, NameValueCollection postData);
        string DoPost(Uri uri, string postData);
        JObject Post(Uri uri, NameValueCollection postData, string messageOnLabkeyError = null);
        JObject Post(Uri uri, string postData, string messageOnLabkeyError);
        void DoRequest(HttpWebRequest request, string method, string authHeader, string messageOnLabkeyError = null);
        void RequestJsonResponse();
        void AddHeader(string name, string value);
        void AddHeader(HttpRequestHeader header, string value);
        void RemoveHeader(string name);
        void AsyncUploadFile(Uri address, string method, string fileName);
        void CancelAsyncUpload();
        void AddUploadFileCompletedEventHandler(UploadFileCompletedEventHandler handler);
        void AddUploadProgressChangedEventHandler(UploadProgressChangedEventHandler handler);
    }

    public abstract class AbstractRequestHelper : IRequestHelper
    {
        public abstract string DoGet(Uri uri);

        public abstract byte[] DoPost(Uri uri, NameValueCollection postData);

        public abstract string DoPost(Uri uri, string postData);

        public abstract void AddHeader(string name, string value);

        public abstract void AddHeader(HttpRequestHeader header, string value);

        public abstract void RemoveHeader(string name);

        public abstract void DoRequest(HttpWebRequest request, string method, string authHeader, string messageOnLabkeyError = null);

        public abstract void AsyncUploadFile(Uri address, string method, string fileName);

        public abstract void CancelAsyncUpload();

        public abstract void AddUploadFileCompletedEventHandler(UploadFileCompletedEventHandler handler);

        public abstract void AddUploadProgressChangedEventHandler(UploadProgressChangedEventHandler handler);

        public virtual JObject Get(Uri uri, string messageOnLabkeyError = null)
        {
            var response = DoGet(uri);
            return ParseResponse(response, uri, messageOnLabkeyError);
        }

        public virtual JObject Post(Uri uri, NameValueCollection postData, string messageOnLabkeyError = null)
        {
            postData ??= new NameValueCollection();
            return Post(uri, postData, null, messageOnLabkeyError);
        }

        public JObject Post(Uri uri, string postData, string messageOnLabkeyError = null)
        {
            return Post(uri, null, postData, messageOnLabkeyError);
        }

        protected virtual JObject Post(Uri uri, NameValueCollection postData, string postDataString, string messageOnLabkeyError)
        {
            RequestJsonResponse();
            string response;
            if (postData != null)
            {
                var responseBytes = DoPost(uri, postData);
                response = Encoding.UTF8.GetString(responseBytes);
            }
            else
            {
                AddHeader(HttpRequestHeader.ContentType, "application/json");
                response = DoPost(uri, postDataString);
            }
            return ParseResponse(response, uri, messageOnLabkeyError);
        }

        public void RequestJsonResponse()
        {
            // Get LabKey to send JSON instead of HTML.
            // When we request a JSON response, the HTTP status returned is always 200 even when the request fails on the server,
            // and the returned status would have been 400 otherwise. This is intentional:
            // Issue 44964: Don't use Bad Request/400 HTTP status codes for valid requests
            // https://www.labkey.org/home/Developer/issues/issues-details.view?issueId=44964
            // Notes from Josh:
            // 1.Malformed HTTP request, which doesn't comply with HTTP spec. 400 is clearly legit here.
            // 2.Well - formed HTTP requests with parameters that we don't like. This is less clear cut to me.
            // 3.Well - formed HTTP requests with legit parameters, but which exercise some sort of error scenario. We clearly shouldn't be sending a 400 for these.
            // 
            // We have to look for the status and any exception in the returned JSON rather than expecting a WebException
            // when the returned status is something other than 200.
            AddHeader(HttpRequestHeader.Accept, @"application/json");
        }

        protected JObject ParseJsonResponse(string response, Uri uri)
        {
            try
            {
                return JObject.Parse(response);
            }
            catch (JsonReaderException e)
            {
                throw new PanoramaServerException(TextUtil.LineSeparate(
                    Resources.BaseWebClient_ParseJsonResponse_Error_parsing_response_as_JSON_,
                    string.Format(Resources.GenericState_AppendErrorAndUri_Error___0_, e.Message),
                    string.Format(Resources.GenericState_AppendErrorAndUri_URL___0_, uri),
                    string.Format(Resources.BaseWebClient_ParseJsonResponse_Response___0_, response)), e);
            }
        }

        protected JObject ParseResponse(string response, Uri uri, string messageOnLabKeyError)
        {
            var jsonResponse = ParseJsonResponse(response, uri);
            var serverError = PanoramaUtil.GetIfErrorInResponse(jsonResponse);
            if (serverError != null)
            {
                throw new PanoramaServerException(TextUtil.LineSeparate(messageOnLabKeyError, serverError.ToString()));
            }

            return jsonResponse;
        }

        public abstract void Dispose();
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

        public override JObject Get(Uri uri, string messageOnLabkeyError = null)
        {
            try
            {
                return base.Get(uri, messageOnLabkeyError);
            }
            catch (WebException e)
            {
                messageOnLabkeyError ??= string.Format("{0} request was unsuccessful", @"GET");
                throw PanoramaServerException(uri, messageOnLabkeyError, e);
            }
        }

        private static PanoramaServerException PanoramaServerException(Uri uri, string messageOnLabkeyError, WebException e)
        {
            using (var r = e.Response)
            {
                // A WebException is usually thrown if the response status code is something other than 200
                // We could still have a LabKey error in the JSON response. 
                var labKeyError = PanoramaUtil.GetIfErrorInResponse(r);
                return new PanoramaServerException(messageOnLabkeyError, uri, e, labKeyError);
            }
        }

        protected override JObject Post(Uri uri, NameValueCollection postData, string postDataString, string messageOnLabkeyError)
        {
            try
            {
                _client.GetCsrfTokenFromServer();
                return base.Post(uri, postData, postDataString, messageOnLabkeyError);
            }
            catch (WebException e)
            {
                messageOnLabkeyError ??= string.Format("{0} request was unsuccessful", @"POST");
                throw PanoramaServerException(uri, messageOnLabkeyError, e);
            }
        }

        public override void DoRequest(HttpWebRequest request, string method, string authHeader, string messageOnLabkeyError = null)
        {
            request.Method = method;
            request.Headers.Add(HttpRequestHeader.Authorization, authHeader);
            request.Accept = @"application/json"; // Get LabKey to send JSON instead of HTML

            try
            {
                using (var response = request.GetResponse())
                {
                    // If the JSON response contains an exception message, throw a PanoramaServerException
                    var labkeyError = PanoramaUtil.GetIfErrorInResponse(response);
                    if (labkeyError != null)
                    {
                        throw new PanoramaServerException(messageOnLabkeyError, request.RequestUri, labkeyError);
                    }
                }
            }
            catch (WebException e)
            {
                messageOnLabkeyError ??= string.Format("{0} request was unsuccessful", method);
                throw PanoramaServerException(request.RequestUri, messageOnLabkeyError, e);
            }
        }

        public override void AsyncUploadFile(Uri address, string method, string fileName)
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


        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _client?.Dispose();
            }
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~PanoramaRequestHelper()
        {
            Dispose(false);
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
                serverError = TextUtil.LineSeparate(serverError,
                    string.Format(Resources.LabKeyError_ToString_Response_status___0_, Status));
            return serverError;
        }
    }
}
