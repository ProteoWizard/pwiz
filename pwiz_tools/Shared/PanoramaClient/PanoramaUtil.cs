/*
 * Original author: Shannon Joyner <saj9191 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using pwiz.Common.SystemUtil;
using pwiz.PanoramaClient.Properties;

namespace pwiz.PanoramaClient
{
    public class PanoramaUtil
    {
        public const string PANORAMA_WEB = "https://panoramaweb.org/";
        public const string FORM_POST = "POST";
        public const string LABKEY_CTX = "/labkey/";
        public const string ENSURE_LOGIN_PATH = "security/home/ensureLogin.view";
        public const string WEBDAV = @"_webdav";
        public const string WEBDAV_W_SLASH = WEBDAV + @"/";
        public const string FILES = @"@files";
        public const string FILES_W_SLASH = @"/" + FILES;

        public static Uri ServerNameToUri(string serverName)
        {
            try
            {
                return new Uri(ServerNameToUrl(serverName));
            }
            catch (UriFormatException)
            {
                return null;
            }
        }

        private static string ServerNameToUrl(string serverName)
        {
            const string https = "https://";
            const string http = "http://";

            var httpsIndex = serverName.IndexOf(https, StringComparison.Ordinal);
            var httpIndex = serverName.IndexOf(http, StringComparison.Ordinal);

            if (httpsIndex == -1 && httpIndex == -1)
            {
                serverName = serverName.Insert(0, https);
            }

            return serverName;
        }

        public static bool TryGetJsonResponse(HttpWebResponse response, ref JObject jsonResponse)
        {
            using (var stream = response.GetResponseStream())
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        var responseText = reader.ReadToEnd();
                        try
                        {
                            jsonResponse = JObject.Parse(responseText);
                            return true;
                        }
                        catch (JsonReaderException) {}
                    }
                }
            }

            return false;
        }

        public static bool IsValidEnsureLoginResponse(JObject jsonResponse, string expectedEmail)
        {
            // Example JSON response:
            /*
             * {
                  "currentUser" : {
                    "canUpdateOwn" : "false",
                    "canUpdate" : "false",
                    "canDeleteOwn" : "false",
                    "canInsert" : "false",
                    "displayName" : "test_user",
                    "canDelete" : "false",
                    "id" : 1166,
                    "isAdmin" : "false",
                    "email" : "test_user@uw.edu"
                  }
                }
             */
            jsonResponse.TryGetValue(@"currentUser", out JToken currentUser);
            if (currentUser != null)
            {
                var email = currentUser.Value<string>(@"email");
                return email != null && email.Equals(expectedEmail);
            }

            return false;
        }

        public static Uri GetEnsureLoginUri(PanoramaServer pServer)
        {
            return new Uri(pServer.URI, ENSURE_LOGIN_PATH);
        }

        /// <summary>
        /// Parses the JSON returned from the getContainers LabKey API to look "TargetedMS" in the 'activeModules' array.
        /// </summary>
        public static bool HasTargetedMsModule(JToken folderJson)
        {
            var modules = folderJson?[@"activeModules"];
            return modules != null && modules.Any(module => string.Equals(module.ToString(), @"TargetedMS"));
        }

        public static bool CheckReadPermissions(JToken folderJson)
        {
            return CheckFolderPermissions(folderJson, FolderPermission.read);
        }

        public static bool CheckInsertPermissions(JToken folderJson)
        {
            return CheckFolderPermissions(folderJson, FolderPermission.insert);
        }

        /// <summary>
        /// Parses the JSON returned from the getContainers LabKey API to look for user permissions in the container.
        /// </summary>
        /// <returns>True if the user has the given permission type.</returns>
        public static bool CheckFolderPermissions(JToken folderJson, FolderPermission permissionType)
        {
            if (folderJson != null)
            {
                var userPermissions = folderJson.Value<int?>(@"userPermissions");
                return userPermissions != null && Equals(userPermissions & (int)permissionType, (int)permissionType);
            }

            return false;
        }

        public static Uri Call(Uri serverUri, string controller, string folderPath, string method, bool isApi = false)
        {
            return Call(serverUri, controller, folderPath, method, null, isApi);
        }

        public static Uri Call(Uri serverUri, string controller, string folderPath, string method, string query,
            bool isApi = false)
        {
            string path = controller + @"/" + (folderPath ?? string.Empty) + @"/" +
                          method + (isApi ? @".api" : @".view");

            if (!string.IsNullOrEmpty(query))
            {
                path = path + @"?" + query;
            }

            return new Uri(serverUri, path);
        }

        public static Uri CallNewInterface(Uri serverUri, string controller, string folderPath, string method,
            string query,
            bool isApi = false)
        {
            string apiString = isApi ? @"api" : @"view";
            string queryString = string.IsNullOrEmpty(query) ? "" : @"?" + query;
            string path = $@"{folderPath}/{controller}-{method}.{apiString}{queryString}";

            return new Uri(serverUri, path);
        }

        public static Uri GetContainersUri(Uri serverUri, string folder, bool includeSubfolders)
        {
            var queryString = string.Format(@"includeSubfolders={0}&moduleProperties=TargetedMS",
                includeSubfolders ? @"true" : @"false");
            return Call(serverUri, @"project", folder, @"getContainers", queryString);
        }

        public static IPanoramaClient CreatePanoramaClient(Uri serverUri, string userName, string password)
        {
            return new WebPanoramaClient(serverUri, userName, password);
        }
    }

    public enum ServerStateEnum { unknown, missing, notpanorama, available }
    public enum UserStateEnum { valid, nonvalid, unknown }
    public enum FolderState { valid, notpanorama, nopermission, notfound, unknown }

    public enum FolderPermission
    {
        read = 1,   // Defined in org.labkey.api.security.ACL.java: public static final int PERM_READ = 0x00000001;
        insert = 2, // Defined in org.labkey.api.security.ACL.java: public static final int PERM_INSERT = 0x00000002;
        delete = 8, // Defined in org.labkey.api.security.ACL.java: public static final int PERM_DELETE = 0x00000008;
        admin = 32768  // Defined in org.labkey.api.security.ACL.java: public static final int PERM_ADMIN = 0x00008000;
    }

    public interface IPanoramaClient
    {
        Uri ServerUri { get; }

        string Username { get; }

        string Password { get; }

        PanoramaServer ValidateServer();

        void ValidateFolder(string folderPath, FolderPermission? permission, bool checkTargetedMs = true);

        JToken GetInfoForFolders(string folder);

        void DownloadFile(string fileUrl, string fileName, long fileSize, string realName,
            IProgressMonitor pm, IProgressStatus progressStatus);
    }

    public class WebPanoramaClient : IPanoramaClient
    {
        public Uri ServerUri { get; private set; }
        public string Username { get; }
        public string Password { get; }

        public WebPanoramaClient(Uri serverUri, string username, string password)
        {
            ServerUri = serverUri;
            Username = username;
            Password = password;
        }

        public PanoramaServer ValidateServer()
        {
            var validatedUri = ValidateUri(ServerUri);
            var validatedServer = ValidateServerAndUser(validatedUri, Username, Password);
            ServerUri = validatedServer.URI;
            return validatedServer;
        }

        private Uri ValidateUri(Uri uri, bool tryNewProtocol = true)
        {
            try
            {
                using (var webClient = new WebClient())
                {
                    webClient.DownloadString(uri);
                    return uri;
                }
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                // Invalid URL
                if (ex.Status == WebExceptionStatus.NameResolutionFailure)
                {
                    var responseUri = response?.ResponseUri;
                    throw new PanoramaServerException(ServerStateEnum.missing, ex.Message, uri, 
                        (responseUri != null && !uri.Equals(responseUri) ? responseUri : null));
                }
                else if (tryNewProtocol)
                {
                    // try again using https
                    if (uri.Scheme.Equals(@"http"))
                    {
                        var httpsUri = new Uri(uri.AbsoluteUri.Replace(@"http", @"https"));
                        return ValidateUri(httpsUri, false);
                    }
                    // We assume "https" (PanoramaUtil.ServerNameToUrl) if there is no scheme in the user provided URL.
                    // Try http. LabKey Server may not be running under SSL. 
                    else if (uri.Scheme.Equals(@"https"))
                    {
                        var httpUri = new Uri(uri.AbsoluteUri.Replace(@"https", @"http"));
                        return ValidateUri(httpUri, false);
                    }
                }

                throw new PanoramaServerException(ServerStateEnum.unknown, ex.Message, ServerUri, uri);
            }
        }

        private PanoramaServer ValidateServerAndUser(Uri serverUri, string username, string password)
        {
            var pServer = new PanoramaServer(serverUri, username, password);

            try
            {
                return EnsureLogin(pServer);
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;

                if (response != null && response.StatusCode == HttpStatusCode.NotFound) // 404
                {
                    var newServer = pServer.AddLabKeyContextPath();
                    if (!ReferenceEquals(pServer, newServer))
                    {
                        // e.g. Given server URL is https://panoramaweb.org but LabKey Server is not deployed as the root webapp.
                        // Try again with '/labkey' context path
                        return EnsureLogin(newServer);
                    }
                    else 
                    {
                        newServer = pServer.RemoveContextPath();
                        if (!ReferenceEquals(pServer, newServer))
                        {
                            // e.g. User entered the home page of the LabKey Server, running as the root webapp: 
                            // https://panoramaweb.org/project/home/begin.view OR https://panoramaweb.org/home/project-begin.view
                            // We will first try https://panoramaweb.org/project/ OR https://panoramaweb.org/home/ as the server URL. 
                            // And that will fail.  Remove the assumed context path and try again.
                            return EnsureLogin(newServer);
                        }
                    }
                }

                throw new PanoramaServerException(UserStateEnum.unknown, ex.Message, ServerUri, PanoramaUtil.GetEnsureLoginUri(pServer));
            }
        }

        private PanoramaServer EnsureLogin(PanoramaServer pServer)
        {
            var requestUri = PanoramaUtil.GetEnsureLoginUri(pServer);
            var request = (HttpWebRequest)WebRequest.Create(requestUri);
            if (pServer.HasUserAccount())
            {
                request.Headers.Add(HttpRequestHeader.Authorization,
                    PanoramaServer.GetBasicAuthHeader(pServer.Username, pServer.Password));
            }

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new PanoramaServerException(UserStateEnum.nonvalid,
                            string.Format(
                                Resources
                                    .PanoramaUtil_EnsureLogin_Could_not_authenticate_user__Response_received_from_server___0___1_,
                                response.StatusCode, response.StatusDescription),
                            ServerUri, requestUri);
                    }

                    JObject jsonResponse = null;

                    if (!(PanoramaUtil.TryGetJsonResponse(response, ref jsonResponse) 
                        && PanoramaUtil.IsValidEnsureLoginResponse(jsonResponse, pServer.Username)))
                    {
                        if (jsonResponse == null)
                        {
                            throw new PanoramaServerException(UserStateEnum.unknown,
                                string.Format(
                                    Resources
                                        .PanoramaUtil_EnsureLogin_Server_did_not_return_a_valid_JSON_response___0__is_not_a_Panorama_server_,
                                    ServerUri),
                                ServerUri, requestUri);
                        }
                        else
                        {
                            var jsonText = jsonResponse.ToString(Formatting.None);
                            jsonText = jsonText.Replace(@"{", @"{{"); // escape curly braces
                            throw new PanoramaServerException(UserStateEnum.unknown,
                                string.Format(Resources.PanoramaUtil_EnsureLogin_Unexpected_JSON_response_from_the_server___0_, jsonText),
                                ServerUri, requestUri);
                        }
                    }

                    return pServer;
                }
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;

                if (response != null && response.StatusCode == HttpStatusCode.Unauthorized) // 401
                {
                    var responseUri = response.ResponseUri;
                    if (!requestUri.Equals(responseUri))
                    {
                        // This means we were redirected.  Authorization headers are not persisted across redirects. Try again
                        // with the responseUri.
                        var redirectedServer =
                            pServer.Redirect(responseUri.AbsoluteUri, PanoramaUtil.ENSURE_LOGIN_PATH);
                        if (!ReferenceEquals(pServer, redirectedServer))
                        {
                            return EnsureLogin(redirectedServer);
                        }
                        else
                        {
                            throw new PanoramaServerException(UserStateEnum.nonvalid, ex.Message, ServerUri, requestUri); // User cannot be authenticated
                        }
                    }

                    if (!pServer.HasUserAccount())
                    {
                        // We were not given a username / password. This means that the user wants anonymous access
                        // to the server. Since we got a 401 (Unauthorized) error, not a 404 (Not found), this means
                        // that the server is a Panorama server.
                        return pServer;
                    }

                    throw new PanoramaServerException(UserStateEnum.nonvalid, ex.Message, ServerUri, requestUri); // User cannot be authenticated
                }

                throw;
            }
        }

        public void ValidateFolder(string folderPath, FolderPermission? permission, bool checkTargetedMs = true)
        {
            var folderState = GetFolderState(folderPath, permission, checkTargetedMs);
            if (folderState != FolderState.valid)
            {
                throw new PanoramaServerException(folderState, folderPath, null, ServerUri, null, Username);
            }
        }

        private FolderState GetFolderState(string folderPath, FolderPermission? permission, bool checkTargetedMs = true)
        {
            var requestUri = PanoramaUtil.GetContainersUri(ServerUri, folderPath, false);

            try
            {
                using (var webClient = new WebClientWithCredentials(ServerUri, Username, Password))
                {
                    JToken response = webClient.Get(requestUri);

                    if (permission != null && !PanoramaUtil.CheckFolderPermissions(response, (FolderPermission)permission))
                    {
                        return FolderState.nopermission;
                    }

                    if (checkTargetedMs && !PanoramaUtil.HasTargetedMsModule(response))
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
                else
                {
                    throw new PanoramaServerException(FolderState.unknown, folderPath, ex.Message, ServerUri,
                        requestUri, Username);
                }
            }
            return FolderState.valid;
        }

        public JToken GetInfoForFolders(string folder)
        {
            var server = new PanoramaServer(ServerUri, Username, Password);
            if (server.HasUserAccount())
            {
                server = EnsureLogin(server);
                ServerUri = server.URI;
            }


            // Retrieve folders from server.
            Uri uri = PanoramaUtil.GetContainersUri(ServerUri, folder, true);

            using (var webClient = new WebClientWithCredentials(ServerUri, Username, Password))
            {
                return webClient.Get(uri);
            }
        }

        /// <summary>
        /// Downloads a given file to a given folder path and shows the progress
        /// of the download during downloading
        /// </summary>
        public void DownloadFile(string fileUrl, string fileName, long fileSize, string realName, IProgressMonitor pm, IProgressStatus progressStatus)
        {
            using var wc = new WebClientWithCredentials(ServerUri, Username, Password);
            wc.DownloadProgressChanged += (s, e) =>
            {
                var progressPercent = e.ProgressPercentage > 0 ? e.ProgressPercentage : -1;
                if (progressPercent == -1 && fileSize > 0)
                {
                    progressPercent = (int)(e.BytesReceived * 100 / fileSize);
                }
                var downloaded = e.BytesReceived;
                var message = TextUtil.LineSeparate(
                    string.Format(Resources.WebPanoramaClient_DownloadFile_Downloading__0_, realName),
                    string.Empty,
                    GetDownloadedSize(downloaded, fileSize > 0 ? fileSize : 0));
                progressStatus = progressStatus.ChangeMessage(message);
                pm.UpdateProgress(progressStatus = progressStatus.ChangePercentComplete(progressPercent));
            };
            var downloadComplete = false;
            wc.DownloadFileCompleted += (s, e) =>
            {
                if (e.Error != null && !pm.IsCanceled)
                {
                    pm.UpdateProgress(progressStatus = progressStatus.ChangeErrorException(e.Error));
                }
                downloadComplete = true;
            };
            wc.DownloadFileAsync(

                // Param1 = Link of file
                new Uri(fileUrl),
                // Param2 = Path to save
                fileName
            );

            while (!downloadComplete)
            {
                if (pm.IsCanceled)
                {
                    wc.CancelAsync();
                }
                Thread.Sleep(100);
            }
        }


        /// <summary>
        /// Borrowed from SkypSupport.cs, displays download progress
        /// </summary>
        /// <param name="downloaded"></param>
        /// <param name="fileSize"></param>
        /// <returns></returns>
        public static string GetDownloadedSize(long downloaded, long fileSize)
        {
            var formatProvider = new FileSizeFormatProvider();
            if (fileSize > 0)
            {
                return string.Format(@"{0} / {1}", string.Format(formatProvider, @"{0:fs1}", downloaded), string.Format(formatProvider, @"{0:fs1}", fileSize));
            }
            else
            {
                return string.Format(formatProvider, @"{0:fs1}", downloaded);
            }
        }

    }

    /// <summary>
    /// Base class for panorama clients used in tests.
    /// </summary>
    public class BaseTestPanoramaClient : IPanoramaClient
    {
        public Uri ServerUri { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }

        public virtual PanoramaServer ValidateServer()
        {
            throw new NotImplementedException();
        }

        public virtual void ValidateFolder(string folderPath, FolderPermission? permission, bool checkTargetedMs = true)
        {
            throw new NotImplementedException();
        }

        public virtual JToken GetInfoForFolders(string folder)
        {
            throw new NotImplementedException();
        }

        public virtual void DownloadFile(string fileUrl, string fileName, long fileSize, string realName,
            IProgressMonitor pm, IProgressStatus progressStatus)
        {
            throw new NotImplementedException();
        }
    }

    public class PanoramaServerException : Exception
    {
        public PanoramaServerException(string message) : base(message)
        {
        }

        public PanoramaServerException(ServerStateEnum state, string error, Uri uri) : this(state, error, uri, null)
        {
        }

        public PanoramaServerException(ServerStateEnum state, string error, Uri uri, Uri requestUri) : base(GetErrorMessage(state, error, uri, requestUri))
        {
        }

        public PanoramaServerException(UserStateEnum state, string error, Uri uri) : this(state, error, uri, null)
        {
        }

        public PanoramaServerException(UserStateEnum state, string error, Uri uri,Uri requestUri) : base(GetErrorMessage(state, error, uri, requestUri))
        {
        }

        public PanoramaServerException(FolderState state, string folderPath, string error, Uri uri, Uri requestUri, string username) 
            : base(GetErrorMessage(state, folderPath, error, uri, requestUri, username))
        {
        }

        private static string GetErrorMessage(ServerStateEnum state, string error, Uri serverUri, Uri requestUri)
        {
            var stateError = string.Empty;
            switch (state)
            {
                case ServerStateEnum.missing:
                    stateError = string.Format(
                        Resources.ServerState_GetErrorMessage_The_server__0__does_not_exist_,
                        serverUri.AbsoluteUri);
                    break;
                case ServerStateEnum.notpanorama:
                    stateError = string.Format("The server {0} is not a Panorama server", serverUri.AbsoluteUri);
                    break;
                case ServerStateEnum.unknown:
                    stateError = string.Format(
                        Resources.ServerState_GetErrorMessage_Unable_to_connect_to_the_server__0__,
                        serverUri.AbsoluteUri);
                    break;
            }

            return AppendErrorAndUri(stateError, error, requestUri);
        }

        private static string GetErrorMessage(UserStateEnum state, string error, Uri serverUri, Uri requestUri)
        {
            var stateError = string.Empty;
            switch (state)
            {
                case UserStateEnum.nonvalid:
                    stateError = Resources.UserState_GetErrorMessage_The_username_and_password_could_not_be_authenticated_with_the_panorama_server_;
                    break;
                case UserStateEnum.unknown:
                    stateError = string.Format(
                        Resources.UserState_GetErrorMessage_There_was_an_error_authenticating_user_credentials_on_the_server__0__,
                        serverUri.AbsoluteUri);
                    break;
            }

            return AppendErrorAndUri(stateError, error, requestUri);
        }

        private static string GetErrorMessage(FolderState state, string folderPath, string error, Uri serverUri, Uri requestUri, string username)
        {
            var stateError = string.Empty;
            switch (state)
            {
                case FolderState.notfound:
                    stateError = string.Format(
                        Resources.PanoramaUtil_VerifyFolder_Folder__0__does_not_exist_on_the_Panorama_server__1_,
                        folderPath, serverUri);
                    break;
                case FolderState.nopermission:
                    stateError = string.Format(Resources
                            .PanoramaUtil_VerifyFolder_User__0__does_not_have_permissions_to_upload_to_the_Panorama_folder__1_,
                        username, folderPath);
                    break;
                case FolderState.notpanorama:
                    stateError = string.Format(Resources.PanoramaUtil_VerifyFolder__0__is_not_a_Panorama_folder, folderPath);
                    break;
                case FolderState.unknown:
                    stateError = string.Format("Unrecognized error trying to get status for folder {0}.", folderPath);
                    break;
            }

            return AppendErrorAndUri(stateError, error, requestUri);
        }

        private static string AppendErrorAndUri(string mainMessage, string error, Uri uri)
        {
            var message = mainMessage;

            if (error != null || uri != null)
            {
                var sb = new StringBuilder();

                if (error != null)
                {
                    sb.AppendLine(string.Format(Resources.GenericState_AppendErrorAndUri_Error___0_, error));
                }

                if (uri != null)
                {
                    sb.AppendLine(string.Format(Resources.GenericState_AppendErrorAndUri_URL___0_, uri));
                }

                message = TextUtil.LineSeparate(message, string.Empty, sb.ToString());
            }

            return message;
        }
    }

    public class FolderInformation
    {
        private readonly PanoramaServer _server;
        private readonly bool _hasWritePermission;
        private readonly bool _isTargetedMS;
        private readonly string _folderPath;
        
        public FolderInformation(PanoramaServer server, bool hasWritePermission)
        {
            _server = server;
            _hasWritePermission = hasWritePermission;
        }

        public FolderInformation(PanoramaServer server, string folderPath, bool isTargetedMS)
        {
            _server = server;
            _folderPath = folderPath;
            _isTargetedMS = isTargetedMS;
        }

        public PanoramaServer Server
        {
            get { return _server; }
        }

        public bool HasWritePermission
        {
            get { return _hasWritePermission; }
        }

        public string FolderPath
        {
            get { return _folderPath; }
        }

        public bool IsTargetedMS
        {
            get { return _isTargetedMS; }
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

    public class PanoramaServer : Immutable
    {
        public Uri URI { get; protected set; }
        public string Username { get; protected set; }
        public string Password { get; protected set; }

        protected PanoramaServer()
        {
        }

        public PanoramaServer(Uri serverUri) : this(serverUri, null, null)
        {
        }

        public PanoramaServer(Uri serverUri, string username, string password)
        {
            Username = username;
            Password = password;

            var path = serverUri.AbsolutePath;

            if (path.Length > 1)
            {
                // Get the context path (e.g. /labkey) from the path
                var idx = path.IndexOf(@"/", 1, StringComparison.Ordinal);
                if (idx != -1 && path.Length > idx + 1)
                {
                    path = path.Substring(0, idx + 1);
                }
            }

            // Need trailing '/' for correct URIs with new Uri(baseUri, relativeUri) method
            // With no trailing '/', new Uri("https://panoramaweb.org/labkey", "project/getContainers.view") will
            // return https://panoramaweb.org/project/getContainers.view (no labkey)
            // ReSharper disable LocalizableElement
            path = path + (path.EndsWith("/") ? "" : "/");
            // ReSharper restore LocalizableElement

            URI = new UriBuilder(serverUri) { Path = path, Query = string.Empty, Fragment = string.Empty }.Uri;
        }

        public string AuthHeader => GetBasicAuthHeader(Username, Password);

        public PanoramaServer ChangeUri(Uri uri)
        {
            return ChangeProp(ImClone(this), im => im.URI = uri);
        }

        public bool HasUserAccount()
        {
            return Username?.Trim().Length > 0 && Password?.Trim().Length > 0;
        }

        public PanoramaServer RemoveContextPath()
        {
            if (!URI.AbsolutePath.Equals(@"/"))
            {
                var newUri = new UriBuilder(URI) { Path = @"/" }.Uri;
                return new PanoramaServer(newUri, Username, Password);
            }

            return this;
        }

        public PanoramaServer AddLabKeyContextPath()
        {
            if (URI.AbsolutePath.Equals(@"/"))
            {
                var newUri = new UriBuilder(URI) { Path = PanoramaUtil.LABKEY_CTX }.Uri;
                return new PanoramaServer(newUri, Username, Password);
            }

            return this;
        }

        public PanoramaServer Redirect(string redirectUri, string panoramaActionPath)
        {
            if (!Uri.IsWellFormedUriString(redirectUri, UriKind.Absolute))
            {
                return this;
            }

            var idx = redirectUri.IndexOf(panoramaActionPath, StringComparison.Ordinal);
            if (idx != -1)
            {
                var newUri = new Uri(redirectUri.Remove(idx));
                if (!URI.Host.Equals(newUri.Host))
                {
                    return this;
                }

                return new PanoramaServer(newUri, Username, Password);
            }
            return this;
        }

        public static string getFolderPath(PanoramaServer server, Uri serverPlusPath)
        {
            var path = serverPlusPath.AbsolutePath;
            var contextPath = server.URI.AbsolutePath;
            return path.StartsWith(contextPath) ? path.Remove(0, contextPath.Length) : path;
        }

        public static string GetBasicAuthHeader(string username, string password)
        {
            byte[] authBytes = Encoding.UTF8.GetBytes(String.Format(@"{0}:{1}", username, password));
            var authHeader = @"Basic " + Convert.ToBase64String(authBytes);
            return authHeader;
        }
    }
}
