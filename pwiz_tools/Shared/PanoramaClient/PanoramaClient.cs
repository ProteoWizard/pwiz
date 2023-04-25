using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace pwiz.PanoramaClient
{
    public interface IPanoramaClient
    {
        Uri ServerUri { get; }
        ServerState GetServerState();
        UserState IsValidUser(string username, string password);
        FolderState IsValidFolder(string folderPath, string username, string password);

        /**
         * Returns FolderOperationStatus.OK if created successfully, otherwise returns the reason
         * why the folder was not created.
         */
        FolderOperationStatus CreateFolder(string parentPath, string folderName, string username, string password);
        /**
         * Returns FolderOperationStatus.OK if the folder was successfully deleted, otherwise returns the reason
         * why the folder was not deleted.
         */
        FolderOperationStatus DeleteFolder(string folderPath, string username, string password);

        JToken GetInfoForFolders(PanoramaServer server, string folder);

    }

    public class WebPanoramaClient : IPanoramaClient
    {
        public Uri ServerUri { get; private set; }
        public string SelectedPath { get; private set; }

        public WebPanoramaClient(Uri server)
        {
            ServerUri = server;
        }

        public ServerState GetServerState()
        {
            return TryGetServerState();
        }

        private ServerState TryGetServerState(bool tryNewProtocol = true)
        {
            try
            {
                using (var webClient = new WebClient())
                {
                    webClient.DownloadString(ServerUri);
                    return ServerState.VALID;
                }
            }
            catch (WebException ex)
            {
                // Invalid URL
                if (ex.Status == WebExceptionStatus.NameResolutionFailure)
                {
                    return new ServerState(ServerStateEnum.missing, ex.Message, ServerUri);
                }
                else if (tryNewProtocol)
                {
                    if (TryNewProtocol(() => TryGetServerState(false).IsValid()))
                        return ServerState.VALID;

                    return new ServerState(ServerStateEnum.unknown, ex.Message, ServerUri);
                }
            }
            return new ServerState(ServerStateEnum.unknown, null, ServerUri);
        }

        // This function must be true/false returning; no exceptions can be thrown
        private bool TryNewProtocol(Func<bool> testFunc)
        {
            Uri currentUri = ServerUri;

            // try again using https
            if (ServerUri.Scheme.Equals(@"http"))
            {
                ServerUri = new Uri(currentUri.AbsoluteUri.Replace(@"http", @"https"));
                return testFunc();
            }
            // We assume "https" (PanoramaClient.ServerNameToUrl) if there is no scheme in the user provided URL.
            // Try http. LabKey Server may not be running under SSL. 
            else if (ServerUri.Scheme.Equals(@"https"))
            {
                ServerUri = new Uri(currentUri.AbsoluteUri.Replace(@"https", @"http"));
                return testFunc();
            }

            ServerUri = currentUri;
            return false;
        }

        public UserState IsValidUser(string username, string password)
        {
            var refServerUri = ServerUri;
            var userState = PanoramaUtil.ValidateServerAndUser(ref refServerUri, username, password);
            if (userState.IsValid())
            {
                ServerUri = refServerUri;
            }
            return userState;
        }

        public FolderState IsValidFolder(string folderPath, string username, string password)
        {
            try
            {
                var uri = PanoramaUtil.GetContainersUri(ServerUri, folderPath, false);

                using (var webClient = new WebClientWithCredentials(ServerUri, username, password))
                {
                    JToken response = webClient.Get(uri);

                    // User needs write permissions to publish to the folder
                    if (!PanoramaUtil.CheckFolderPermissions(response))
                    {
                        return FolderState.nopermission;
                    }

                    // User can only upload to a TargetedMS folder type.
                    if (!PanoramaUtil.CheckFolderType(response))
                    {
                        return FolderState.notpanorama;
                    }
                }
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                {
                    return FolderState.notfound;
                }
                else throw;
            }
            return FolderState.valid;
        }

        public FolderOperationStatus CreateFolder(string folderPath, string folderName, string username, string password)
        {

            if (IsValidFolder($@"{folderPath}/{folderName}", username, password) == FolderState.valid)
                return FolderOperationStatus.alreadyexists;        //cannot create a folder with the same name
            var parentFolderStatus = IsValidFolder(folderPath, username, password);
            switch (parentFolderStatus)
            {
                case FolderState.nopermission:
                    return FolderOperationStatus.nopermission;
                case FolderState.notfound:
                    return FolderOperationStatus.notfound;
                case FolderState.notpanorama:
                    return FolderOperationStatus.notpanorama;
            }

            //Create JSON body for the request
            Dictionary<string, string> requestData = new Dictionary<string, string>();
            requestData[@"name"] = folderName;
            requestData[@"title"] = folderName;
            requestData[@"description"] = folderName;
            requestData[@"type"] = @"normal";
            requestData[@"folderType"] = @"Targeted MS";
            string createRequest = JsonConvert.SerializeObject(requestData);

            try
            {
                using (var webClient = new WebClientWithCredentials(ServerUri, username, password))
                {
                    Uri requestUri = PanoramaUtil.CallNewInterface(ServerUri, @"core", folderPath, @"createContainer", "", true);
                    JObject result = webClient.Post(requestUri, createRequest);
                    return FolderOperationStatus.OK;
                }
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response != null && response.StatusCode != HttpStatusCode.OK)
                {
                    return FolderOperationStatus.error;
                }
                else throw;
            }
        }

        public FolderOperationStatus DeleteFolder(string folderPath, string username, string password)
        {
            var parentFolderStatus = IsValidFolder(folderPath, username, password);
            switch (parentFolderStatus)
            {
                case FolderState.nopermission:
                    return FolderOperationStatus.nopermission;
                case FolderState.notfound:
                    return FolderOperationStatus.notfound;
                case FolderState.notpanorama:
                    return FolderOperationStatus.notpanorama;
            }

            try
            {
                using (var webClient = new WebClientWithCredentials(ServerUri, username, password))
                {
                    Uri requestUri = PanoramaUtil.CallNewInterface(ServerUri, @"core", folderPath, @"deleteContainer", "", true);
                    JObject result = webClient.Post(requestUri, "");
                    return FolderOperationStatus.OK;
                }
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response != null && response.StatusCode != HttpStatusCode.OK)
                {
                    return FolderOperationStatus.error;
                }
                else throw;
            }
        }

        public PanoramaServer EnsureLogin(PanoramaServer server)
        {
            var refServerUri = server.URI;
            UserState userState = PanoramaUtil.ValidateServerAndUser(ref refServerUri, server.Username, server.Password);
            if (userState.IsValid())
            {
                return server.ChangeUri(refServerUri);
            }
            else
            {
                throw new PanoramaServerException(userState.GetErrorMessage(refServerUri));
            }
        }

        public JToken GetInfoForFolders(PanoramaServer server, string folder)
        {
            if (server.HasUserCredentials())
            { 
                server = EnsureLogin(server);
            }
           

            // Retrieve folders from server.
            Uri uri = PanoramaUtil.GetContainersUri(server.URI, folder, true);

            using (var webClient = new WebClientWithCredentials(server.URI, server.Username, server.Password))
            {
                return webClient.Get(uri);
            }
        }


        // NOTE (vsharma): serverUri, user, pass are not used. Remove them. 
        public string SaveFile(string fileName, string lastPath)
        {
            var dlg = new FolderBrowserDialog
            {
                SelectedPath = lastPath
            };
            var downloadPath = string.Empty;
            dlg.Description = "Select the folder the file will be downloaded to";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                SelectedPath = dlg.SelectedPath;
                var selected = dlg.SelectedPath;
                downloadPath = Path.Combine(selected, fileName);
            }
            return downloadPath;
        }

        public void DownloadFile(string path, PanoramaServer server, string downloadName)
        {
            using var wc = new WebClientWithCredentials(server.URI, server.Username, server.Password);
            wc.DownloadFile(

                // Param1 = Link of file
                new Uri(downloadName),
                // Param2 = Path to save
                path
            );
        }

    }

    public class WebClientWithCredentials : UTF8WebClient
    {
        private CookieContainer _cookies = new CookieContainer();
        private string _csrfToken;
        private Uri _serverUri;

        private static string LABKEY_CSRF = @"X-LABKEY-CSRF";

        public WebClientWithCredentials(Uri serverUri, string username, string password)
        {
            // Add the Authorization header
            Headers.Add(HttpRequestHeader.Authorization, PanoramaServer.GetBasicAuthHeader(username, password));
            _serverUri = serverUri;
        }

        public JObject Post(Uri uri, NameValueCollection postData)
        {
            if (string.IsNullOrEmpty(_csrfToken))
            {
                // After this the client should have the X-LABKEY-CSRF token 
                DownloadString(new Uri(_serverUri, PanoramaUtil.ENSURE_LOGIN_PATH));
            }
            if (postData == null)
            {
                postData = new NameValueCollection();
            }
            var responseBytes = UploadValues(uri, PanoramaUtil.FORM_POST, postData);
            var response = Encoding.UTF8.GetString(responseBytes);
            return JObject.Parse(response);
        }

        public JObject Post(Uri uri, string postData)
        {
            if (string.IsNullOrEmpty(_csrfToken))
            {
                // After this the client should have the X-LABKEY-CSRF token 
                DownloadString(new Uri(_serverUri, PanoramaUtil.ENSURE_LOGIN_PATH));
            }
            Headers.Add(HttpRequestHeader.ContentType, "application/json");
            var response = UploadString(uri, PanoramaUtil.FORM_POST, postData);
            return JObject.Parse(response);
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = base.GetWebRequest(address);

            var httpWebRequest = request as HttpWebRequest;
            if (httpWebRequest != null)
            {
                httpWebRequest.CookieContainer = _cookies;

                if (request.Method == PanoramaUtil.FORM_POST)
                {
                    if (!string.IsNullOrEmpty(_csrfToken))
                    {
                        // All POST requests to LabKey Server will be checked for a CSRF token
                        request.Headers.Add(LABKEY_CSRF, _csrfToken);
                    }
                }
            }
            return request;
        }

        protected override WebResponse GetWebResponse(WebRequest request)
        {
            var response = base.GetWebResponse(request);
            var httpResponse = response as HttpWebResponse;
            if (httpResponse != null)
            {
                GetCsrfToken(httpResponse);
            }
            return response;
        }

        private void GetCsrfToken(HttpWebResponse response)
        {
            if (!string.IsNullOrEmpty(_csrfToken))
            {
                return;
            }

            var csrf = response.Cookies[LABKEY_CSRF];
            if (csrf != null)
            {
                // The server set a cookie called X-LABKEY-CSRF, get its value
                _csrfToken = csrf.Value;
            }
        }
    }

    public class UTF8WebClient : WebClient
    {
        public UTF8WebClient()
        {
            Encoding = Encoding.UTF8;
        }

        public JObject Get(Uri uri)
        {
            var response = DownloadString(uri);
            return JObject.Parse(response);
        }
    }
}