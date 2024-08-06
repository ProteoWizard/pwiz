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
using System.Text.RegularExpressions;
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

        public static bool TryGetJsonResponse(WebResponse response, ref JObject jsonResponse)
        {
            var responseText = GetResponseString(response);
            if (responseText != null)
            {
                try
                {
                    jsonResponse = JObject.Parse(responseText);
                    return true;
                }
                catch (JsonReaderException) { }
            }
            return false;
        }

        public static string GetResponseString(WebResponse response)
        {
            using (var stream = response?.GetResponseStream())
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }

            return null;
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
                // Do a case insensitive comparison. LabKey does not allow creating different accounts for same email address, different case.
                // User reported error in AutoQC Loader: https://panoramaweb.org/home/support/announcements-thread.view?rowId=9136. They entered
                // an all-caps version of their email address in AutoQC Loader.
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
            const string separator = @"/";
            // Trim leading and trailing '/' from the folderPath so that we don't end up with double '//' in the Uri
            folderPath = string.IsNullOrEmpty(folderPath) ? string.Empty : folderPath.Trim().Trim(separator.ToCharArray());
            if (!(string.IsNullOrEmpty(folderPath) || folderPath.EndsWith(separator))) folderPath += separator;
            var path = controller + separator + folderPath + method + (isApi ? @".api" : @".view");

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

        public static LabKeyError GetErrorFromWebException(WebException e)
        {
            if (e == null || e.Response == null) return null;
            
            // A WebException is usually thrown if the response status code is something other than 200
            // We could still have a LabKey error in the JSON response. For example, when we get a 404
            // response when trying to upload to a folder that does not exist. The response contains a 
            // LabKey error like "No such folder or workbook..."
            return GetIfErrorInResponse(e.Response);
        }

        public static LabKeyError GetIfErrorInResponse(JObject jsonResponse)
        {
            if (jsonResponse?[@"exception"] != null)
            {
                return new LabKeyError(jsonResponse[@"exception"].ToString(), jsonResponse[@"status"]?.ToObject<int>());
            }
            return null;
        }

        public static LabKeyError GetIfErrorInResponse(WebResponse response)
        {
            JObject jsonResponse = null;
            TryGetJsonResponse(response, ref jsonResponse);
            return jsonResponse != null ? GetIfErrorInResponse(jsonResponse) : null;
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

        // From org.labkey.api.util.FileUtil.isAllowedFileName()
        // Note: To test uploads to LK server running on a dev machine, check "Block file upload with potentially malicious names"
        // in the Admin Console > Configuration > Files
        private const string RESTRICTED_CHARS = "\\/:*?\"<>|`";
        private const string ILLEGAL_START_CHARS = "$-";
        private const string INVALID_FILE_NAME_PATTERN = "(.*\\s--[^ ].*)|(.*\\s-[^- ].*)";

        public static bool LabKeyAllowedFileName(string filePath, out string error)
        {
            error = null;

            var fileName = Path.GetFileName(filePath);

            if (fileName.IndexOfAny(RESTRICTED_CHARS.ToCharArray()) != -1)
            {
                error = string.Format(
                    Resources.PanoramaUtil_LabKeyAllowedFileName_File_name_may_not_contain_any_of_these_characters___0_,
                    RESTRICTED_CHARS);
            }
            else if (ILLEGAL_START_CHARS.Contains(fileName[0]))
            {
                error = string.Format(
                    Resources.PanoramaUtil_LabKeyAllowedFileName_File_name_may_not_begin_with_any_of_these_characters___0_,
                    ILLEGAL_START_CHARS);
            }
            else if (Regex.IsMatch(fileName, INVALID_FILE_NAME_PATTERN))
            {
                error = Resources.PanoramaUtil_LabKeyAllowedFileName_File_name_may_not_contain_space_followed_by_dash;
            }

            return error == null;
        }
    }

    public enum ServerStateEnum { unknown, missing, notpanorama, available }
    public enum UserStateEnum { valid, nonvalid, unknown }
    public enum FolderState { valid, notpanorama, nopermission, notfound }

    public enum FolderPermission
    {
        read = 1,   // Defined in org.labkey.api.security.ACL.java: public static final int PERM_READ = 0x00000001;
        insert = 2, // Defined in org.labkey.api.security.ACL.java: public static final int PERM_INSERT = 0x00000002;
        delete = 8, // Defined in org.labkey.api.security.ACL.java: public static final int PERM_DELETE = 0x00000008;
        admin = 32768  // Defined in org.labkey.api.security.ACL.java: public static final int PERM_ADMIN = 0x00008000;
    }

    public static class ServerStateErrors
    {
        public static string Error(this ServerStateEnum state, Uri serverUri)
        {
            var stateError = string.Empty;
            switch (state)
            {
                case ServerStateEnum.missing:
                    stateError = string.Format(
                        Resources.ServerState_GetErrorMessage_The_server__0__does_not_exist_,
                        serverUri?.AbsoluteUri);
                    break;
                case ServerStateEnum.notpanorama:
                    stateError = string.Format(Resources.ServerStateErrors_Error_The_server__0__is_not_a_Panorama_server_, serverUri?.AbsoluteUri);
                    break;
                case ServerStateEnum.unknown:
                    stateError = string.Format(
                        Resources.ServerState_GetErrorMessage_Unable_to_connect_to_the_server__0__,
                        serverUri?.AbsoluteUri);
                    break;
            }
            return stateError;
        }
    }

    public static class UserStateErrors
    {
        public static string Error(this UserStateEnum state, Uri serverUri)
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
                        serverUri?.AbsoluteUri);
                    break;
            }
            return stateError;
        }
    }

    public static class FolderStateErrors
    {
        public static string Error(this FolderState state, Uri serverUri, string folderPath, string username)
        {
            var stateError = string.Empty;
            switch (state)
            {
                case FolderState.notfound:
                    stateError = string.Format(
                        Resources.PanoramaUtil_VerifyFolder_Folder__0__does_not_exist_on_the_Panorama_server__1_,
                        folderPath, serverUri?.AbsoluteUri);
                    break;
                case FolderState.nopermission:
                    stateError = string.Format(Resources
                            .PanoramaUtil_VerifyFolder_User__0__does_not_have_permissions_to_upload_to_the_Panorama_folder__1_,
                        username, folderPath);
                    break;
                case FolderState.notpanorama:
                    stateError = string.Format(Resources.PanoramaUtil_VerifyFolder__0__is_not_a_Panorama_folder, folderPath);
                    break;
            }
            return stateError;
        }
    }

    public class PanoramaServerException : Exception
    {
        public HttpStatusCode? HttpStatus { get; }

        public PanoramaServerException(string message) : base(message)
        {
        }

        private PanoramaServerException(string message, Exception e) : base(message, e)
        {
            HttpStatus = ((e as WebException)?.Response as HttpWebResponse)?.StatusCode;
        }

        public static PanoramaServerException Create(string message, Uri uri, string response, Exception e)
        {
            if (e is WebException webException)
            {
                return CreateWithResponseDisposal(message, uri, response, webException);
            }

            var errorMessage = new ErrorMessageBuilder (Resources.AbstractRequestHelper_ParseJsonResponse_Error_parsing_response_as_JSON_)
                    .ExceptionMessage(e.Message).Uri(uri).Response(response).ToString();
            return new PanoramaServerException(errorMessage, e);
        }

        public static PanoramaServerException CreateWithResponseDisposal(string message, Uri uri, Func<WebException, LabKeyError> getLabKeyError, WebException e)
        {
            var errorMessageBuilder = new ErrorMessageBuilder(message)
                .Uri(uri)
                .ExceptionMessage(e.Message)
                .LabKeyError(getLabKeyError(e)); // Will read the WebException's Response property.

            return CreateWithResponseDisposal(errorMessageBuilder.ToString(), e);
        }

        private static PanoramaServerException CreateWithResponseDisposal(string message, Uri uri, string response, WebException e)
        {
            var errorMessageBuilder = new ErrorMessageBuilder(message)
                .Uri(uri)
                .ExceptionMessage(e.Message)
                .Response(response);

            return CreateWithResponseDisposal(errorMessageBuilder.ToString(), e);
        }

        private static PanoramaServerException CreateWithResponseDisposal(string errorMessage, WebException e)
        {
            var exception = new PanoramaServerException(errorMessage, e); // Will read WebException's Response.StatusCode
            e.Response?.Dispose();
            return exception;
        }
    }

    public class ErrorMessageBuilder
    {
        // This error from LabKey when there is a 401 (Unauthorized) error is not very useful, and can be confusing to the Skyline user. Ignore it.
        private const string LABKEY_LOGIN_ERR_MESSAGE_IGNORE = @"You must log in to view this content.";

        private readonly string _error;
        private string _errorDetail;
        private string _exceptionMessage;
        private LabKeyError _labkeyError;
        private Uri _uri;
        private string _responseString;

        public ErrorMessageBuilder (string error)
        {
            _error = error;
        }
        public ErrorMessageBuilder ErrorDetail(string errorDetail)
        {
            _errorDetail = errorDetail;
            return this;
        }
        public ErrorMessageBuilder LabKeyError(LabKeyError labkeyError)
        {
            if (!LABKEY_LOGIN_ERR_MESSAGE_IGNORE.Equals(labkeyError?.ErrorMessage,
                    StringComparison.OrdinalIgnoreCase))
            {
                _labkeyError = labkeyError;
            }

            return this;
        }

        public ErrorMessageBuilder ExceptionMessage(string exceptionMessage)
        {
            _exceptionMessage = exceptionMessage;
            return this;
        }

        public ErrorMessageBuilder Uri(Uri requestUri)
        {
            _uri = requestUri;
            return this;
        }

        public ErrorMessageBuilder Response(JObject json)
        {
            if (json != null)
            {
                _responseString = json.ToString(Formatting.Indented);
            }
            return this;
        }
        public ErrorMessageBuilder Response(string response)
        {
            _responseString = response;
            return this;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(_errorDetail) || !string.IsNullOrEmpty(_exceptionMessage) || _labkeyError != null)
            {
                sb.Append(Resources.ErrorMessageBuilder_Build_Error__);
                if (!string.IsNullOrEmpty(_errorDetail)) sb.AppendLine(_errorDetail);
                if (!string.IsNullOrEmpty(_exceptionMessage)) sb.AppendLine(_exceptionMessage);
                if (_labkeyError != null) sb.AppendLine(_labkeyError.ToString());
            }
            if (_uri != null)
            {
                sb.AppendLine(string.Format(Resources.GenericState_AppendErrorAndUri_URL___0_, _uri.AbsoluteUri));
            }

            if (!string.IsNullOrEmpty(_responseString))
            {
                // TODO: Should we truncate the response string, or display it at all?
                //       CommonAlertDlg truncates the displayed string to 50000. 
                //       The response is not useful for the user but it may be useful for debugging. The response gets set
                //       only if there in an error doing a GET request. Since the request URL is included in the  message 
                //       dialog, a developer could also recreate the response by impersonating the user. 
                sb.AppendLine(Resources.ErrorMessageBuilder_Build_Response__).AppendLine(_responseString);
            }

            return sb.Length > 0 ? CommonTextUtil.LineSeparate(_error, sb.ToString().TrimEnd()) : _error;
        }
    }

    public class PanoramaImportErrorException : Exception
    {
        public PanoramaImportErrorException(Uri serverUrl, Uri jobUrl, string error, bool jobCancelled = false)
        {
            ServerUrl = serverUrl;
            JobUrl = jobUrl;
            JobCancelled = jobCancelled;
            Error = error;
        }

        public Uri ServerUrl { get; private set; }
        public Uri JobUrl { get; private set; }
        public bool JobCancelled { get; private set; }

        public string Error { get; private set; }
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
                // is already there in the request cookies.
                // An example of where this can happen is WebPanoramaClient.SendZipFile(). This uses one instance of the web client
                // to do multiple requests. Normally the first request (getPipelineContainer) retrieves the CSRF token from the response
                // cookies and saves it for any future requests. But this request may get redirected(302) because the Panorama folder was 
                // renamed, and if that happens the response cookies get copied to the request's cookie container, and CSRFUtil.getExpectedToken
                // does not add a response cookie because it sees the CSRF cookie in the request cookies. So when we look for the CSRF cookie
                // in the response after the redirected request returns we don't find it. But we can find it in the request cookies.
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
        }

        public void ClearCsrfToken()
        {
            _csrfToken = null;
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
            path += path.EndsWith("/") ? "" : "/";
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

        public bool HasContextPath()
        {
            return !URI.AbsolutePath.Equals(@"/");
        }

        public PanoramaServer RemoveContextPath()
        {
            if (HasContextPath())
            {
                var newUri = new UriBuilder(URI) { Path = @"/" }.Uri;
                return new PanoramaServer(newUri, Username, Password);
            }

            return this;
        }

        public PanoramaServer AddLabKeyContextPath()
        {
            if (!HasContextPath())
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
