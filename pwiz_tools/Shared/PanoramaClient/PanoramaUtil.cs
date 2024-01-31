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
                // TODO: remove this check? Replace with return currentUser != null && currentUser.Value<string>(@"email") != null;
                var email = currentUser.Value<string>(@"email");
                return email != null && email.Equals(expectedEmail, StringComparison.OrdinalIgnoreCase);
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
            folderPath ??= string.Empty;
            folderPath = folderPath.Trim().TrimStart('/').TrimEnd('/');
            var path = controller + @"/" + folderPath + (string.Empty.Equals(folderPath) ? string.Empty : @"/") + method + (isApi ? @".api" : @".view");

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
            path = path.TrimStart('/');

            return new Uri(serverUri, path);
        }

        public static Uri GetContainersUri(Uri serverUri, string folder, bool includeSubfolders)
        {
            var queryString = string.Format(@"includeSubfolders={0}&moduleProperties=TargetedMS",
                includeSubfolders ? @"true" : @"false");
            return Call(serverUri, @"project", folder, @"getContainers", queryString);
        }

        public static Uri GetPipelineContainerUrl(Uri serverUri, string folderPath)
        {
            return Call(serverUri, @"pipeline", folderPath, @"getPipelineContainer", true);
        }

        public static Uri GetImportSkylineDocUri(Uri serverUri, string folderPath)
        {
            return Call(serverUri, @"targetedms", folderPath, @"skylineDocUploadApi");
        }

        public static Uri GetPipelineJobStatusUri(Uri serverUri, string folderPath, int pipelineJobRowId)
        {
            return Call(serverUri, @"query", folderPath, @"selectRows",
                @"query.queryName=job&schemaName=pipeline&query.rowId~eq=" + pipelineJobRowId);
        }

        public static LabKeyError GetIfErrorInResponse(JObject jsonResponse)
        {
            if (jsonResponse[@"exception"] != null)
            {
                return new LabKeyError(jsonResponse[@"exception"].ToString(), jsonResponse[@"status"]?.ToObject<int>());
            }
            return null;
        }

        public static LabKeyError GetIfErrorInResponse(WebResponse response)
        {
            var stream = response?.GetResponseStream();
            if (stream != null)
            {
                var responseString = new StreamReader(stream).ReadToEnd();
                return GetIfErrorInResponse(responseString);
            }

            return null;
        }

        public static LabKeyError GetIfErrorInResponse(string responseString)
        {
            try
            {
                return GetIfErrorInResponse(JObject.Parse(responseString));
            }
            catch (JsonReaderException)
            {
            }
            return null;
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

        public virtual Uri SendZipFile(string folderPath, string zipFilePath, IProgressMonitor progressMonitor)
        {
            throw new NotImplementedException();
        }

        public virtual JObject SupportedVersionsJson()
        {
            throw new NotImplementedException();
        }
    }

    public class PanoramaServerException : Exception
    {
        public PanoramaServerException(string message) : base(message)
        {
        }

        public PanoramaServerException(string message, Exception e) : base(message, e)
        {
        }

        public PanoramaServerException(string message, Uri requestUri, Exception e, LabKeyError labkeyError) 
            : base(AppendErrorAndUri(message, requestUri, e.Message, labkeyError), e)
        {
        }

        public PanoramaServerException(string message, Uri requestUri, LabKeyError labkeyError)
            : base(AppendErrorAndUri(message, requestUri, null, labkeyError))
        {
        }

        public PanoramaServerException(ServerStateEnum state, Uri uri, Uri requestUri, WebResponse response, Exception e)
            : this(state, uri, requestUri, PanoramaUtil.GetIfErrorInResponse(response), e)
        {
        }

        public PanoramaServerException(ServerStateEnum state, Uri uri, Uri requestUri, LabKeyError labkeyError, Exception e) 
            : base(GetErrorMessage(state, uri, requestUri, labkeyError, e), e)
        {
        }

        public PanoramaServerException(ServerStateEnum state, Uri uri, Uri requestUri, string error)
            : this(GetErrorMessage(state, uri, requestUri, null, error))
        {
        }

        public PanoramaServerException(UserStateEnum state, Uri uri, Uri requestUri, WebResponse response, string additionalMessage)
            : this(state, uri, requestUri, PanoramaUtil.GetIfErrorInResponse(response), additionalMessage)
        {
        }

        public PanoramaServerException(UserStateEnum state, Uri uri, Uri requestUri, LabKeyError error,string additionalMessage)
            : base(GetErrorMessage(state, uri, requestUri, error, additionalMessage))
        {
        }

        public PanoramaServerException(UserStateEnum state, Uri uri, Uri requestUri, WebResponse response, Exception e)
            : this(state, uri, requestUri, PanoramaUtil.GetIfErrorInResponse(response), e)
        {
        }

        public PanoramaServerException(UserStateEnum state, Uri uri, Uri requestUri, string error)
            : this(GetErrorMessage(state, uri, requestUri, null, error))
        {
        }

        public PanoramaServerException(UserStateEnum state, Uri uri,Uri requestUri, LabKeyError error, Exception e) 
            : base(GetErrorMessage(state, uri, requestUri, error, e), e)
        {
        }

        public PanoramaServerException(FolderState state, string folderPath, Uri uri, Uri requestUri, string username)
            : base(GetErrorMessage(state, folderPath, uri, requestUri, username, null, null))
        {
        }

        public PanoramaServerException(FolderState state, string folderPath, Uri uri, Uri requestUri, string username, WebResponse response, Exception e)
            : this(state, folderPath, uri, requestUri, username, PanoramaUtil.GetIfErrorInResponse(response), e)
        {
        }

        public PanoramaServerException(FolderState state, string folderPath, Uri uri, Uri requestUri, string username, LabKeyError error, Exception e) 
            : base(GetErrorMessage(state, folderPath, uri, requestUri, username, error, e), e)
        {
        }


        private static string GetErrorMessage(ServerStateEnum state, Uri serverUri, Uri requestUri, LabKeyError error, Exception e)
        {
            return GetErrorMessage(state, serverUri, requestUri, error, e?.Message);
        }

        private static string GetErrorMessage(ServerStateEnum state, Uri serverUri, Uri requestUri, LabKeyError error, string additionalErrorMessage)
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

            return AppendErrorAndUri(stateError, requestUri, additionalErrorMessage, error);
        }

        private static string GetErrorMessage(UserStateEnum state, Uri serverUri, Uri requestUri, LabKeyError error, Exception e)
        {
            return GetErrorMessage(state, serverUri, requestUri, error, e?.Message);
        }

        private static string GetErrorMessage(UserStateEnum state, Uri serverUri, Uri requestUri, LabKeyError error, string additionalErrorMessage)
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

            return AppendErrorAndUri(stateError, requestUri, additionalErrorMessage, error);
        }

        private static string GetErrorMessage(FolderState state, string folderPath, Uri serverUri, Uri requestUri, string username, LabKeyError error, Exception e)
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

            return AppendErrorAndUri(stateError, requestUri, e?.Message, error);
        }

        private static string AppendErrorAndUri(string mainMessage, Uri uri, string error, LabKeyError labkeyError)
        {
            var message = mainMessage;

            var sb = new StringBuilder();

            if (labkeyError != null)
            {
                sb.AppendLine(labkeyError.ToString());
            }

            if (error != null)
            {
                sb.AppendLine(string.Format(Resources.GenericState_AppendErrorAndUri_Error___0_, error));
            }

            if (uri != null)
            {
                sb.AppendLine(string.Format(Resources.GenericState_AppendErrorAndUri_URL___0_, uri));
            }

            if (sb.Length > 0)
            {
                message = TextUtil.LineSeparate(message, string.Empty, sb.ToString());
            }
            
            return message;
        }
    }

    public class PanoramaImportErrorException : Exception
    {
        public PanoramaImportErrorException(Uri serverUrl, Uri jobUrl, bool jobCancelled = false)
        {
            ServerUrl = serverUrl;
            JobUrl = jobUrl;
            JobCancelled = jobCancelled;
        }

        public Uri ServerUrl { get; private set; }
        public Uri JobUrl { get; private set; }
        public bool JobCancelled { get; private set; }
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
    }

    public class WebClientWithCredentials : UTF8WebClient
    {
        private CookieContainer _cookies = new CookieContainer();
        private string _csrfToken;
        private readonly Uri _serverUri;
    
        private static string LABKEY_CSRF = @"X-LABKEY-CSRF";
    
        public WebClientWithCredentials(Uri serverUri, string username, string password)
        {
            // Add the Authorization header
            Headers.Add(HttpRequestHeader.Authorization, PanoramaServer.GetBasicAuthHeader(username, password));
            _serverUri = serverUri;
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
                GetCsrfToken(httpResponse, request.RequestUri);
            }
            return response;
        }
    
        private void GetCsrfToken(HttpWebResponse response, Uri requestUri)
        {
            if (!string.IsNullOrEmpty(_csrfToken))
            {
                return;
            }

            var csrf = GetCsrfCookieFromResponse(response);
            if (csrf == null)
            {
                // If we did not find it in the Response cookies look in the Request cookies.
                // org.labkey.api.util.CSRFUtil.getExpectedToken() will not add the CSRF cookie to the response if the cookie
                // is already there in the request.  This can happen if our WebClient is used to make multiple requests
                // (e.g. WebPanoramaPublishClient.SendZipFile()) and the first request in the series gets redirected. When this
                // happens CSRFUtil will not add the cookie to the response, because it is already in the redirected request. 
                csrf = GetCsrfCookieFromRequest(requestUri);
            }
            if (csrf != null)
            {
                // The server set a cookie called X-LABKEY-CSRF, get its value
                _csrfToken = csrf.Value;
            }
        }

        private Cookie GetCsrfCookieFromResponse(HttpWebResponse response)
        {
            return response.Cookies[LABKEY_CSRF];
        }

        private Cookie GetCsrfCookieFromRequest(Uri requestUri)
        {
            return _cookies.GetCookies(requestUri)[LABKEY_CSRF];
        }

        public void GetCsrfTokenFromServer()
        {
            if (string.IsNullOrEmpty(_csrfToken))
            {
                // After making this request the client should have the X-LABKEY-CSRF token 
                DownloadString(new Uri(_serverUri, PanoramaUtil.ENSURE_LOGIN_PATH));
            }
            // After making this request the client should have the X-LABKEY-CSRF token 
            // DownloadString(new Uri(_serverUri, PanoramaUtil.ENSURE_LOGIN_PATH));
            // DoGet(new Uri(_serverUri, PanoramaUtil.ENSURE_LOGIN_PATH));
        }
    }

    public class NonStreamBufferingWebClient : WebClientWithCredentials
    {
        public NonStreamBufferingWebClient(Uri serverUri, string username, string password)
            : base(serverUri, username, password)
        {
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = base.GetWebRequest(address);

            var httpWebRequest = request as HttpWebRequest;
            if (httpWebRequest != null)
            {
                httpWebRequest.Timeout = Timeout.Infinite;
                httpWebRequest.AllowWriteStreamBuffering = false;
            }
            return request;
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
