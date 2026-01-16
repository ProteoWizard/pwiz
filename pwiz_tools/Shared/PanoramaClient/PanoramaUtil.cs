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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net; // HttpStatusCode
using System.Text;
using System.Text.RegularExpressions;
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
        public const string WEBDAV_W_SLASH = WEBDAV + SLASH;
        public const string FILES = @"@files";
        public const string FILES_W_SLASH = SLASH + FILES;
        public const string PERMS_JSON_PROP = @"effectivePermissions";
        private const string SLASH = @"/";

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

        public static bool HasReadPermissions(JToken folderJson)
        {
            return CheckFolderPermissions(folderJson, PermissionSet.READER);
        }

        public static bool HasUploadPermissions(JToken folderJson)
        {
            return CheckFolderPermissions(folderJson, PermissionSet.AUTHOR);
        }

        /// <summary>
        /// Parses the JSON returned from the getContainers LabKey API to look for user permissions in the container.
        /// </summary>
        /// <returns>True if the user has the given permissions.</returns>
        public static bool CheckFolderPermissions(JToken folderJson, PermissionSet requiredPermissions)
        {
            var effectivePermissions = folderJson?[PERMS_JSON_PROP]?
                .Select(token => token.ToString())
                .ToArray();

            return effectivePermissions != null && new PermissionSet(effectivePermissions).HasAllPermissions(requiredPermissions);
        }

        public static Uri Call(Uri serverUri, string controller, string folderPath, string method, bool isApi = false)
        {
            return Call(serverUri, controller, folderPath, method, null, isApi);
        }

        public static Uri Call(Uri serverUri, string controller, string folderPath, string method, string query,
            bool isApi = false)
        {
            const string separator = SLASH;
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

        public static Uri GetContainersUri(Uri serverUri, string folder, bool includeSubfolders, PanoramaServer server = null, string serverFolderPath = null)
        {
            var queryString = string.Format(@"includeSubfolders={0}&moduleProperties=TargetedMS",
                includeSubfolders ? @"true" : @"false");
            
            // Combine server folder path with the folder parameter (if provided)
            // Prefer server.GetFullPath() if server is provided, otherwise use serverFolderPath parameter
            string effectiveFolder = null;
            if (server != null)
            {
                // Use GetFullPath() to combine server.FolderPath with folder parameter
                // folder parameter is relative (no leading "/"), so prepend "/" for GetFullPath
                // GetFullPath returns paths with leading "/", but URLs need paths without leading "/"
                string folderPathForGetFullPath = string.IsNullOrEmpty(folder) ? folder : SLASH + folder.TrimStart('/');
                string fullPath = server.GetFullPath(folderPathForGetFullPath);
                effectiveFolder = string.IsNullOrEmpty(fullPath) || fullPath == SLASH 
                    ? null 
                    : fullPath.TrimStart('/');
            }
            else if (!string.IsNullOrEmpty(serverFolderPath))
            {
                // Legacy path: combine serverFolderPath with folder parameter
                if (!string.IsNullOrEmpty(folder))
                {
                    // Combine paths: serverFolderPath + folder
                    // e.g., "SkylineTest" + "ForPanoramaClientTest" -> "SkylineTest/ForPanoramaClientTest"
                    effectiveFolder = serverFolderPath + SLASH + folder.Trim('/');
                }
                else
                {
                    // Use server folder path only
                    effectiveFolder = serverFolderPath;
                }
            }
            else if (!string.IsNullOrEmpty(folder))
            {
                // Use folder parameter only
                effectiveFolder = folder;
            }
            
            // Always use new-style URL format: {folder}/project-getContainers.view (preferred by LabKey Server)
            // This format is required when serverFolderPath is set (e.g., "SkylineTest")
            // Old format (project/folder/getContainers.view) would incorrectly place the module between server and folder
            // If effectiveFolder is null/empty, returns project-getContainers.view (gets all folders)
            // If effectiveFolder is "SkylineTest", returns SkylineTest/project-getContainers.view (gets only that folder)
            if (string.IsNullOrEmpty(effectiveFolder))
            {
                // For empty folder, construct URL directly without leading slash
                // Using CallNewInterface with empty folder would add a leading slash, so construct manually
                string apiString = @"view";
                string queryParam = string.IsNullOrEmpty(queryString) ? string.Empty : @"?" + queryString;
                string path = $@"project-getContainers.{apiString}{queryParam}";
                return new Uri(serverUri, path);
            }
            else
            {
                // Use new-style format: {folder}/project-getContainers.view
                // e.g., SkylineTest/project-getContainers.view
                return CallNewInterface(serverUri, @"project", effectiveFolder, @"getContainers", queryString);
            }
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

        public static LabKeyError GetErrorFromNetworkRequestException(NetworkRequestException e)
        {
            if (e == null || string.IsNullOrEmpty(e.ResponseBody)) return null;
            
            // NetworkRequestException may contain a JSON response body with LabKey-specific error details
            return GetIfErrorInResponse(e.ResponseBody);
        }

        public static LabKeyError GetIfErrorInResponse(JObject jsonResponse)
        {
            if (jsonResponse?[@"exception"] != null)
            {
                return new LabKeyError(jsonResponse[@"exception"].ToString(), jsonResponse[@"status"]?.ToObject<int>());
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

    public static class Permission
    {
        public const string READ = "org.labkey.api.security.permissions.ReadPermission";
        public const string INSERT = "org.labkey.api.security.permissions.InsertPermission";
        public const string UPDATE = "org.labkey.api.security.permissions.UpdatePermission";
        public const string DELETE = "org.labkey.api.security.permissions.DeletePermission";
        public const string ADMIN = "org.labkey.api.security.permissions.AdminPermission";
    }

    public class PermissionSet
    {
        public static readonly PermissionSet READER = CreatePermissionSet(Permission.READ);

        public static readonly PermissionSet AUTHOR = CreatePermissionSet(Permission.READ, Permission.INSERT);
        
        public static readonly PermissionSet EDITOR = CreatePermissionSet(Permission.READ, Permission.INSERT, Permission.UPDATE, Permission.DELETE);
        
        public static readonly PermissionSet FOLDER_ADMIN = CreatePermissionSet(Permission.READ, Permission.INSERT, Permission.UPDATE, Permission.DELETE, Permission.ADMIN);


        private static PermissionSet CreatePermissionSet(params string[] permissions)
        {
            return new PermissionSet(permissions);
        }

        private readonly HashSet<string> _permissions;

        public IReadOnlyCollection<string> Permissions => _permissions;

        public PermissionSet(IEnumerable<string> permissions)
        {
            if (permissions == null)
            {
                throw new ArgumentNullException(nameof(permissions));
            }
            _permissions = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);
        }

        public bool IsEmpty() => !_permissions.Any();

        // Checks if this permission set contains all the permissions from another permission set.
        // Returns true if the other permission set is null or empty.
        public bool HasAllPermissions(PermissionSet permissions)
        {
            return permissions == null 
                   || permissions.IsEmpty() 
                   || _permissions.IsSupersetOf(permissions.Permissions);
        }

        public override string ToString()
        {
            return IsEmpty()
                ? @"PermissionSet: [Empty]"
                : string.Format(@"PermissionSet: {0}", string.Join(@", ", _permissions));
        }
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

    /// <summary>
    /// Base class for all Panorama-specific exceptions.
    /// Inherits from IOException so that these exceptions are recognized as user-actionable
    /// rather than programming defects by ExceptionUtil.IsProgrammingDefect().
    /// </summary>
    public class PanoramaException : IOException
    {
        public PanoramaException(string message) : base(message) { }
        public PanoramaException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class PanoramaServerException : PanoramaException
    {
        public HttpStatusCode? HttpStatus { get; }

        public PanoramaServerException(string message) : base(message)
        {
        }

        public PanoramaServerException(string message, Exception e) : base(message, e)
        {
            HttpStatus = (e as NetworkRequestException)?.StatusCode;
        }

        public static PanoramaServerException CreateWithLabKeyError(string message, Uri uri, Func<NetworkRequestException, LabKeyError> getLabKeyError, NetworkRequestException e)
        {
            var labKeyError = getLabKeyError?.Invoke(e);
            var errorMessageBuilder = new ErrorMessageBuilder(message)
                .Uri(uri);

            // Add LabKey error if available (most specific server-side error)
            if (labKeyError != null)
            {
                errorMessageBuilder.LabKeyError(labKeyError);

                // Don't include the NetworkRequestException message when we have a LabKey error
                // The LabKey error is the server's specific error message and is what users need
                // The exception message would be technical details (e.g., "Response status code does not indicate success: 500...")
                // which are redundant when we already show the LabKey error and status code
            }
            else
            {
                // No LabKey error - use the full NetworkRequestException message which includes helpful context
                // (e.g., "The request to https://... timed out. Please try again.")
                errorMessageBuilder.ExceptionMessage(e.Message);
            }

            return new PanoramaServerException(errorMessageBuilder.ToString(), e);
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

    public class PanoramaImportErrorException : PanoramaException
    {
        public PanoramaImportErrorException(Uri serverUrl, Uri jobUrl, string error, bool jobCancelled = false)
            : base(error)
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




    public class PanoramaServer : Immutable
    {
        private const string SLASH = "/";
        private const char SLASH_C = '/';

        public Uri URI { get; protected set; }
        public string Username { get; protected set; }
        public string Password { get; protected set; }

        /// <summary>
        /// Optional folder path (e.g., "SkylineTest") that can be combined with URI for folder-specific operations.
        /// This allows users to specify a project-specific server URL for faster responses.
        /// The base URI (without folder path) is used for operations like ensureLogin.
        /// </summary>
        public string FolderPath { get; protected set; }

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

            (URI, FolderPath) = ParseUri(serverUri);
        }

        private (Uri uri, string folderPath) ParseUri(Uri serverUri)
        {
            var fullPath = serverUri.AbsolutePath;
            string contextPath = null;
            string folderPath = null;

            // Parse the path to separate context path (e.g., /labkey) from folder path (e.g., /SkylineTest)
            // Context path is typically the first segment after root (e.g., /labkey)
            // Folder path is any additional segments (e.g., /SkylineTest)
            if (fullPath.Length > 1)
            {
                var segments = fullPath.Trim(SLASH_C).Split(SLASH_C);
                if (segments.Length > 0)
                {
                    // First segment is typically the context path (e.g., "labkey")
                    // But if it looks like a project folder name (no "labkey" pattern), treat it as folder path
                    // For now, assume context path is empty for panoramaweb.org (most common case)
                    // and any path segments are folder paths
                    if (segments[0].Equals(@"labkey", StringComparison.OrdinalIgnoreCase))
                    {
                        // Context path found
                        contextPath = SLASH + segments[0];
                        if (segments.Length > 1)
                        {
                            // Remaining segments are folder path
                            folderPath = string.Join(SLASH, segments.Skip(1));
                        }
                    }
                    else
                    {
                        // No context path, all segments are folder path
                        folderPath = string.Join(SLASH, segments);
                    }
                }
            }

            // Construct base URI with context path only (no folder path)
            var basePath = contextPath ?? SLASH;
            // Need trailing '/' for correct URIs with new Uri(baseUri, relativeUri) method
            // With no trailing '/', new Uri("https://panoramaweb.org/labkey", "project/getContainers.view") will
            // return https://panoramaweb.org/project/getContainers.view (no labkey)
            // ReSharper disable LocalizableElement
            if (!basePath.EndsWith(SLASH))
                basePath += SLASH;
            // ReSharper restore LocalizableElement

            var uri = new UriBuilder(serverUri) { Path = basePath, Query = string.Empty, Fragment = string.Empty }.Uri;
            // Normalize FolderPath: remove leading/trailing slashes so it's stored consistently
            // This allows us to avoid trimming in GetFullPath() and other methods
            folderPath = NormalizePath(folderPath);
            return (uri, folderPath);
        }

        /// <summary>
        /// Normalizes a folder path by trimming leading/trailing slashes.
        /// Returns null for null/empty strings, or the trimmed path (which may be empty string if input was just slashes).
        /// </summary>
        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            var trimmed = path.Trim(SLASH_C);
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
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
            return !URI.AbsolutePath.Equals(SLASH);
        }

        public PanoramaServer RemoveContextPath()
        {
            if (HasContextPath())
            {
                var newUri = new UriBuilder(URI) { Path = SLASH }.Uri;
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

        /// <summary>
        /// Constructs a full URI by combining the server's base URI with a folder path.
        /// Handles trailing slashes in the base URI and leading slashes in the folder path to avoid double slashes.
        /// Note: This method does NOT incorporate PanoramaServer.FolderPath. If you need FolderPath included,
        /// use GetFullUri() instead, or call GetFullPath() first and pass the result to this method.
        /// </summary>
        /// <param name="folderPath">A folder path (maybe null, empty, or have leading slashes, will be normalized)</param>
        /// <param name="webdav">If true, includes the "_webdav/" prefix in the path</param>
        /// <returns>The full URI combining Server.URI with the folder path</returns>
        public string GetUri(string folderPath, bool webdav = false)
        {
            var serverUri = URI.ToString();
            var normalizedFolderPath = folderPath ?? string.Empty;
            
            if (webdav)
            {
                normalizedFolderPath = AppendPath(PanoramaUtil.WEBDAV, normalizedFolderPath);
            }
            
            return AppendPath(serverUri, normalizedFolderPath);
        }

        protected static string AppendPath(string root, string append)
        {
            if (string.IsNullOrEmpty(append))
                return root;
            if (root.EndsWith(SLASH))
                root = root.Substring(0, root.Length - 1);
            if (append.StartsWith(SLASH))
                append = append.Substring(1);
            return root + SLASH + append;
        }

        /// <summary>
        /// Constructs a full URI by combining the server's base URI with a folder path,
        /// incorporating PanoramaServer.FolderPath if it is set.
        /// This is equivalent to calling GetFullPath() followed by GetUri().
        /// </summary>
        /// <param name="folderPath">A folder path (maybe absolute or relative, will be combined with FolderPath if set)</param>
        /// <param name="webdav">If true, includes the "_webdav/" prefix in the path</param>
        /// <returns>The full URI combining Server.URI with FolderPath (if set) and the folder path</returns>
        public string GetFullUri(string folderPath = null, bool webdav = false)
        {
            return GetUri(GetFullPath(folderPath), webdav);
        }

        /// <summary>
        /// Combines the server's FolderPath with a relative or absolute folder path.
        /// Returns the full path accounting for the server's base folder.
        /// Both FolderPath and the input folderPath are normalized (no leading/trailing slashes)
        /// to ensure consistent behavior.
        /// </summary>
        /// <param name="folderPath">A folder path (maybe absolute like "/SkylineTest/ForPanoramaClientTest" or relative like "/ForPanoramaClientTest" or "ForPanoramaClientTest")</param>
        /// <returns>The full path combining FolderPath with folderPath, with a leading slash and no trailing slash</returns>
        public string GetFullPath(string folderPath)
        {
            return SLASH + GetNormalizedFullPath(folderPath);
        }

        private string GetNormalizedFullPath(string folderPath)
        {
            // Normalize input folderPath (returns null for null/empty/just-slashes)
            string normalizedInput = NormalizePath(folderPath);

            // If FolderPath is not set, return normalized input with leading slash (or "/" if null)
            if (FolderPath == null)
            {
                return normalizedInput ?? string.Empty;
            }

            // If normalized input is null, return just FolderPath
            if (normalizedInput == null)
            {
                return FolderPath;
            }

            // Check if normalizedInput already starts with or is FolderPath
            if (normalizedInput.StartsWith(FolderPath + SLASH, StringComparison.Ordinal) ||
                normalizedInput == FolderPath)
            {
                // Input already includes FolderPath, return with leading slash
                return normalizedInput;
            }

            // Otherwise, combine FolderPath with normalizedInput
            return AppendPath(FolderPath, normalizedInput);
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

        public static string GetFolderPath(PanoramaServer server, Uri serverPlusPath)
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
