/*
 * Original author: Vagisha Sharma <vsharma .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Net; // HttpStatusCode
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using pwiz.Common.SystemUtil;
using pwiz.PanoramaClient.Properties;

namespace pwiz.PanoramaClient
{
    public interface IPanoramaClient
    {
        Uri ServerUri { get; }

        string Username { get; }

        string Password { get; }

        PanoramaServer ValidateServer();

        void ValidateFolder(string folderPath, PermissionSet permissionSet, bool checkTargetedMs = true);

        JToken GetInfoForFolders(string folder,
            IProgressMonitor progressMonitor, IProgressStatus progressStatus);

        void DownloadFile(string fileUrl, string fileName, long fileSize, string realName,
            IProgressMonitor progressMonitor, IProgressStatus progressStatus);

        Uri SendZipFile(string folderPath, string zipFilePath,
            IProgressMonitor progressMonitor, IProgressStatus progressStatus);

        JObject SupportedVersionsJson();

        IRequestHelper GetRequestHelper();
    }

    public abstract class AbstractPanoramaClient : IPanoramaClient
    {
        protected IProgressMonitor _progressMonitor;
        protected IProgressStatus _progressStatus;

        private readonly Regex _runningStatusRegex = new Regex(@"RUNNING, (\d+)%");
        private int _waitTime = 1;

        public Uri ServerUri { get; private set; }
        public string Username { get; }
        public string Password { get; }

        public AbstractPanoramaClient(Uri serverUri, string username, string password)
        {
            ServerUri = serverUri;
            Username = username;
            Password = password;
        }

        public abstract void DownloadFile(string fileUrl, string fileName, long fileSize, string realName,
            IProgressMonitor progressMonitor, IProgressStatus progressStatus);

        public abstract IRequestHelper GetRequestHelper();

        protected abstract Uri ValidateUri(Uri serverUri, bool tryNewProtocol = true);
        protected abstract PanoramaServer ValidateServerAndUser(Uri serverUri, string username, string password);
        public abstract PanoramaServer EnsureLogin(PanoramaServer pServer);

        public PanoramaServer ValidateServer()
        {
            var validatedUri = ValidateUri(ServerUri);
            var validatedServer = ValidateServerAndUser(validatedUri, Username, Password);
            ServerUri = validatedServer.URI;
            return validatedServer;
        }

        public void ValidateFolder(string folderPath, PermissionSet permissionSet, bool checkTargetedMs = true)
        {
            var requestUri = PanoramaUtil.GetContainersUri(ServerUri, folderPath, false);
            ValidateFolder(requestUri, folderPath, permissionSet, checkTargetedMs);
        }

        public virtual void ValidateFolder(Uri requestUri, string folderPath, PermissionSet requiredPermissions, bool checkTargetedMs = true)
        {
            using (var requestHelper = GetRequestHelper())
            {
                JToken response = requestHelper.Get(requestUri,
                    string.Format(Resources.AbstractPanoramaClient_ValidateFolder_Error_validating_folder___0__,
                        folderPath));

                if (requiredPermissions != null && !PanoramaUtil.CheckFolderPermissions(response, requiredPermissions))
                {
                    throw new PanoramaServerException(
                        new ErrorMessageBuilder(FolderState.nopermission.Error(ServerUri, folderPath, Username))
                            .Uri(requestUri).ToString());
                }

                if (checkTargetedMs && !PanoramaUtil.HasTargetedMsModule(response))
                {
                    throw new PanoramaServerException(
                        new ErrorMessageBuilder(FolderState.notpanorama.Error(ServerUri, folderPath, Username))
                            .Uri(requestUri).ToString());
                }
            }
        }

        public virtual JToken GetInfoForFolders(string folder, IProgressMonitor progressMonitor, IProgressStatus progressStatus)
        {
            var server = new PanoramaServer(ServerUri, Username, Password);
            if (server.HasUserAccount())
            {
                server = EnsureLogin(server);
                ServerUri = server.URI;
            }

            // Retrieve folders from server.
            // Use server.FolderPath if available (allows project-specific server URLs for faster responses)
            var uri = PanoramaUtil.GetContainersUri(ServerUri, folder, true, server);

            using (var requestHelper = GetRequestHelper())
            {
                requestHelper.SetProgressMonitor(progressMonitor, progressStatus);

                // Provide a more descriptive folder name in error messages
                var folderDescription = string.IsNullOrEmpty(folder) 
                    ? Resources.AbstractPanoramaClient_GetInfoForFolders_all_folders 
                    : string.Format(Resources.AbstractPanoramaClient_GetInfoForFolders_folder___0__, folder);

                return requestHelper.Get(uri,
                    string.Format(
                        Resources.AbstractPanoramaClient_GetInfoForFolders_Error_getting_information_for__0__,
                        folderDescription));
            }
        }

        public virtual Uri SendZipFile(string folderPath, string zipFilePath,
            IProgressMonitor progressMonitor, IProgressStatus progressStatus)
        {
            _progressMonitor = progressMonitor;
            _progressStatus = progressStatus;
            var zipFileName = Path.GetFileName(zipFilePath) ?? string.Empty;

            // Upload zip file to pipeline folder.
            using (var requestHelper = GetRequestHelper())
            {
                // This request helper will be used for a series of requests.
                // Set the Accept header set to "application/json" for all requests so that we can parse any LabKey-specific errors.
                requestHelper.RequestJsonResponse(); 

                var webDavUrl = GetWebDavPath(folderPath, requestHelper);

                // Upload Url minus the name of the zip file.
                var baseUploadUri = new Uri(ServerUri, webDavUrl); // webDavUrl will start with the context path (e.g. /labkey/...). We will get the correct URL even if serverUri has a context path.

                // Use Uri.EscapeDataString instead of Uri.EscapeUriString.  
                // The latter will not escape characters such as '+' or '#' 
                var escapedZipFileName = Uri.EscapeDataString(zipFileName);
                var uploadUri = new Uri(baseUploadUri, escapedZipFileName);

                var tmpUploadUri = UploadTempZipFile(zipFilePath, baseUploadUri, escapedZipFileName, requestHelper);

                if (progressMonitor.IsCanceled)
                {
                    // Delete the temporary file on the server
                    progressMonitor.UpdateProgress(
                        _progressStatus =
                            _progressStatus.ChangeMessage(
                                Resources.AbstractPanoramaClient_SendZipFile_Deleting_temporary_file_on_server));
                    DeleteTempZipFile(tmpUploadUri, requestHelper);
                    return null;
                }

                // Make sure the temporary file was uploaded to the server
                ConfirmFileOnServer(tmpUploadUri, requestHelper);

                // Rename the temporary file
                _progressStatus = _progressStatus.ChangeMessage(Resources.AbstractPanoramaClient_SendZipFile_Renaming_temporary_file_on_server);
                progressMonitor.UpdateProgress(_progressStatus);

                RenameTempZipFile(tmpUploadUri, uploadUri, requestHelper);

                // Add a document import job to the queue on the Panorama server
                _progressStatus = _progressStatus.ChangeMessage(Resources.AbstractPanoramaClient_SendZipFile_Waiting_for_data_import_completion___);
                progressMonitor.UpdateProgress(_progressStatus = _progressStatus.ChangePercentComplete(-1));

                var rowId = QueueDocUploadPipelineJob(folderPath, zipFileName, requestHelper);

                return WaitForDocumentImportCompleted(folderPath, rowId, progressMonitor, requestHelper);
            }
        }

        private Uri WaitForDocumentImportCompleted(string folderPath, int pipelineJobRowId, IProgressMonitor progressMonitor, 
            IRequestHelper requestHelper)
        {
            var statusUri = PanoramaUtil.GetPipelineJobStatusUri(ServerUri, folderPath, pipelineJobRowId);

            // Wait for import to finish before returning.
            var startTime = DateTime.UtcNow;
            var importFailed = false;
            while (true)
            {
                if (progressMonitor.IsCanceled)
                    return null;

                JToken jStatusResponse = requestHelper.Get(statusUri,
                    Resources
                        .AbstractPanoramaClient_WaitForDocumentImportCompleted_There_was_an_error_getting_the_status_of_the_document_import_pipeline_job_);
                JToken rows = jStatusResponse[@"rows"]!;
                var row = rows.FirstOrDefault(r => (int)r[@"RowId"] == pipelineJobRowId);
                if (row == null)
                    continue;

                var jobUrl = new Uri(ServerUri, row.Value<string>(@"_labkeyurl_RowId"));
                var status = new ImportStatus((string)row[@"Status"]);
                if (status.IsComplete)
                {
                    progressMonitor.UpdateProgress(_progressStatus.Complete());
                    return new Uri(ServerUri, row.Value<string>(@"_labkeyurl_Description"));
                }

                else if (status.IsCancelled)
                {
                    var e = new PanoramaImportErrorException(ServerUri, jobUrl, null, status.IsCancelled);
                    progressMonitor.UpdateProgress(
                        _progressStatus = _progressStatus.ChangeErrorException(e));
                    throw e;
                }
                else if (status.IsError )
                {
                    var error = (string)row[@"Info"];
                    if (@"Import failed".Equals(error) && !importFailed)
                    {
                        // We will see "Import failed" if we happen to query the status before the actual error message is set on job on the Panorama server.
                        // Check the status one more time.
                        importFailed = true;
                    }
                    else
                    {
                        var e = new PanoramaImportErrorException(ServerUri, jobUrl, error, status.IsCancelled);
                        progressMonitor.UpdateProgress(
                            _progressStatus = _progressStatus.ChangeErrorException(e));
                        throw e;
                    }
                }

                UpdateProgressAndWait(status, progressMonitor, startTime);
            }
        }

        private int QueueDocUploadPipelineJob(string folderPath, string zipFileName,
            IRequestHelper requestHelper)
        {
            var importUrl = PanoramaUtil.GetImportSkylineDocUri(ServerUri, folderPath);

            // Need to tell server which uploaded file to import.
            var dataImportInformation = new NameValueCollection
            {
                // For now, we only have one root that user can upload to
                { @"path", @"./" },
                { @"file", zipFileName }
            };

            JToken importResponse = requestHelper.Post(importUrl, dataImportInformation,
                Resources.AbstractPanoramaClient_QueueDocUploadPipelineJob_There_was_an_error_adding_the_document_import_job_on_the_server_);


            // ID to check import status.
            var details = importResponse[@"UploadedJobDetails"]!;
            return (int)details[0]![@"RowId"];
        }

        private Uri UploadTempZipFile(string zipFilePath, Uri baseUploadUri, string escapedZipFileName,
            IRequestHelper requestHelper)
        {
            var tmpUploadUri = new Uri(baseUploadUri, escapedZipFileName + @".part");

            var headers = new Dictionary<string, string>
            {
                // Add a "Temporary" header so that LabKey marks this as a temporary file.
                // https://www.labkey.org/issues/home/Developer/issues/details.view?issueId=19220
                {@"Temporary", @"T"}
            };

            try
            {
                // For HttpPanoramaRequestHelper, this is synchronous with IProgressMonitor progress
                requestHelper.AsyncUploadFile(tmpUploadUri, @"PUT", PathEx.SafePath(zipFilePath), headers);
            }
            catch (NetworkRequestException ex)
            {
                // Extract LabKey-specific error from response body if available and throw PanoramaServerException
                throw PanoramaServerException.CreateWithLabKeyError(
                    Resources.AbstractPanoramaClient_UploadTempZipFile_There_was_an_error_uploading_the_file_,
                    tmpUploadUri,
                    PanoramaUtil.GetErrorFromNetworkRequestException,
                    ex);
            }

            return tmpUploadUri;
        }

        private string GetWebDavPath(string folderPath, IRequestHelper requestHelper)
        {
            var getPipelineContainerUri = PanoramaUtil.GetPipelineContainerUrl(ServerUri, folderPath);

            var jsonResponse = requestHelper.Get(getPipelineContainerUri,
                string.Format(
                    Resources
                        .AbstractPanoramaClient_GetWebDavPath_There_was_an_error_getting_the_WebDAV_url_for_folder___0__,
                    folderPath));
            var webDavUrl = (string)jsonResponse[@"webDavURL"];
            if (webDavUrl == null)
            {
                throw new PanoramaServerException(new ErrorMessageBuilder(Resources.AbstractPanoramaClient_GetWebDavPath_Missing_webDavURL_in_response_)
                    .Uri(getPipelineContainerUri).Response(jsonResponse).ToString());
            }
            return webDavUrl;
        }

        private class ImportStatus
        {
            public string StatusString { get; }
            public bool IsComplete => string.Equals(@"COMPLETE", StatusString);
            public bool IsRunning => StatusString.Contains(@"RUNNING"); // "IMPORT RUNNING" pre LK19.3, RUNNING, x% in LK19.3
            public bool IsError => string.Equals(@"ERROR", StatusString);
            public bool IsCancelled => string.Equals(@"CANCELLED", StatusString);

            public ImportStatus(string status)
            {
                StatusString = status;
            }
        }

        private void UpdateProgressAndWait(ImportStatus jobStatus, IProgressMonitor progressMonitor, DateTime startTime)
        {
            var match = _runningStatusRegex.Match(jobStatus.StatusString);
            if (match.Success)
            {
                var currentProgress = _progressStatus.PercentComplete;

                if (int.TryParse(match.Groups[1].Value, out var progress))
                {
                    progress = Math.Max(progress, currentProgress);
                    _progressStatus = _progressStatus.ChangeMessage(string.Format(
                        Resources.AbstractPanoramaClient_updateProgressAndWait_Importing_data___0___complete_,
                        progress));
                    _progressMonitor.UpdateProgress(_progressStatus = _progressStatus.ChangePercentComplete(progress));

                    var delta = progress - currentProgress;
                    if (delta > 1)
                    {
                        // If progress is > 1% half the wait time
                        _waitTime = Math.Max(1, _waitTime / 2);
                    }
                    else if (delta < 1)
                    {
                        // If progress is < 1% double the wait time, up to a max of 10 seconds.
                        _waitTime = Math.Min(10, _waitTime * 2);
                    }

                    for (var i = 0; i < _waitTime * 10; i++)
                    {
                        if (progressMonitor.IsCanceled)
                            return;
                        Thread.Sleep(100);
                    }
                    return;
                }
            }

            if (!jobStatus.IsRunning)
            {
                // Display the status since we don't recognize it.  This could be, for example, an "Import Waiting" status if another 
                // Skyline document is currently being imported on the server. 
                _progressMonitor.UpdateProgress(_progressStatus = _progressStatus =
                    _progressStatus.ChangeMessage(string.Format(
                        Resources.AbstractPanoramaClient_updateProgressAndWait_Status_on_server_is___0_,
                        jobStatus.StatusString)));
            }

            else if (!_progressStatus.Message.Equals(Resources.AbstractPanoramaClient_SendZipFile_Waiting_for_data_import_completion___))
            {
                // Import is running now. Reset the progress message in case it had been set to something else (e.g. "Import Waiting") in a previous iteration.  
                progressMonitor.UpdateProgress(_progressStatus =
                    _progressStatus.ChangeMessage(Resources.AbstractPanoramaClient_SendZipFile_Waiting_for_data_import_completion___));
            }

            // This is probably an older server (pre LK19.3) that does not include the progress percent in the status.
            // Wait between 1 and 5 seconds before checking status again.
            var elapsed = (DateTime.UtcNow - startTime).TotalMinutes;
            var sleepTime = elapsed > 5 ? 5 * 1000 : (int)(Math.Max(1, elapsed % 5) * 1000);
            Thread.Sleep(sleepTime);
        }

        private void RenameTempZipFile(Uri sourceUri, Uri destUri, IRequestHelper requestHelper)
        {
            // Headers for MOVE request
            var headers = new Dictionary<string, string>
            {
                // Do not use Uri.ToString since it does not return the escaped version.
                {@"Destination", destUri.AbsoluteUri}, 

                // If a file already exists at the destination URI, it will not be overwritten.  
                // The server would return a 412 Precondition Failed status code.
                {@"Overwrite", @"F"}
            };
            requestHelper.DoRequest(
                sourceUri,
                @"MOVE",
                headers,
                Resources
                    .AbstractPanoramaClient_RenameTempZipFile_There_was_an_error_renaming_the_temporary_zip_file_on_the_server_
            );
        }

        private void DeleteTempZipFile(Uri sourceUri, IRequestHelper requestHelper)
        {
            // DELETE request, no custom headers needed
            requestHelper.DoRequest(
                sourceUri,
                @"DELETE",
                null,
                Resources
                    .AbstractPanoramaClient_DeleteTempZipFile_There_was_an_error_deleting_the_temporary_zip_file_on_the_server_
            );
        }

        private void ConfirmFileOnServer(Uri sourceUri, IRequestHelper requestHelper)
        {
            // Do a HEAD request to check if the file exists on the server. No custom headers needed.
            requestHelper.DoRequest(
                sourceUri,
                @"HEAD",
                null,
                Resources.AbstractPanoramaClient_ConfirmFileOnServer_File_was_not_uploaded_to_the_server__Please_try_again__or_if_the_problem_persists__please_contact_your_Panorama_server_administrator_
            );
        }

        public virtual JObject SupportedVersionsJson()
        {
            var uri = PanoramaUtil.Call(ServerUri, @"targetedms", null, @"getMaxSupportedVersions");

            using (var requestHelper = GetRequestHelper())
            {
                try
                {
                    return requestHelper.Get(uri);
                }
                catch (Exception)
                {
                    // An exception will be thrown if the response code is not 200 (Success).
                    // We may be communicating with an older server that does not understand the request.
                    // An exception can also be throws if the returned response could not be parsed as JSON.
                    return null;
                }
            }
        }

        public void CreateTargetedMsFolder(string parentFolderPath, string folderName)
        {
            var folderToCreate = $@"{parentFolderPath}/{folderName}";

            if (FolderExists(folderToCreate))
            {
                // Folder exists on the server at the given path. Cannot create a folder with the same name
                throw new PanoramaServerException(string.Format(
                    Resources.AbstractPanoramaClient_CreateTargetedMsFolder_Folder_already_exists___0__,
                    folderToCreate));
            }

            ValidateFolder(parentFolderPath, PermissionSet.FOLDER_ADMIN, false); // Parent folder should exist and have admin permissions.


            //Create JSON body for the request
            var requestData = new Dictionary<string, string>
            {
                [@"name"] = folderName,
                [@"title"] = folderName,
                [@"description"] = folderName,
                [@"type"] = @"normal",
                [@"folderType"] = @"Targeted MS"
            };
            string createRequest = JsonConvert.SerializeObject(requestData);

            using (var requestHelper = GetRequestHelper())
            {
                var requestUri = PanoramaUtil.CallNewInterface(ServerUri, @"core", parentFolderPath, @"createContainer", string.Empty, true);
                requestHelper.Post(requestUri, createRequest,
                    string.Format(
                        Resources.AbstractPanoramaClient_CreateTargetedMsFolder_Error_creating_Panorama_folder__0__,
                        folderToCreate));
            }
        }

        public  bool FolderExists(string folderPath)
        {
            try
            {
                ValidateFolder(folderPath, null);
                return true;
            }
            catch (PanoramaServerException)
            {
                // We expect this exception if the folder does not exist
            }

            return false;
        }

        public void DeleteFolderIfExists(string folderPath)
        {
            if (FolderExists(folderPath))
            {
                using (var requestHelper = GetRequestHelper())
                {
                    var requestUri = PanoramaUtil.CallNewInterface(ServerUri, @"core", folderPath, @"deleteContainer", string.Empty, true);
                    requestHelper.Post(requestUri, "",
                        string.Format(
                            Resources.AbstractPanoramaClient_DeleteFolderIfExists_Error_deleting_Panorama_folder__0__,
                            folderPath));
                }
            }
        }
    }

    public class WebPanoramaClient : AbstractPanoramaClient
    {
        public WebPanoramaClient(Uri serverUri, string username, string password) : base(serverUri, username, password)
        {
        }

        protected override Uri ValidateUri(Uri uri, bool tryNewProtocol = true)
        {
            try
            {
                using var httpClient = new HttpClientWithProgress(new SilentProgressMonitor());
                
                // Use admin-healthCheck.view endpoint to verify it's a LabKey Server
                // This endpoint is specifically designed for server health checks:
                // - Always present on every LabKey Server
                // - Returns minimal JSON immediately (no login required)
                // - Returns {"healthy": true} for a valid LabKey Server
                // - Much smaller than downloading HTML home page
                // IMPORTANT: Use only the root server URL (without folder paths or WebDAV paths)
                // The admin-healthCheck.view endpoint must be called at the server root level
                // Use PanoramaServer to extract the root server URI for consistency with ValidateServerAndUser
                var pServer = new PanoramaServer(uri);
                var validationUri = new Uri(pServer.URI, @"admin-healthCheck.view");
                string responseBody = httpClient.DownloadString(validationUri);
                
                // Verify the response is valid JSON and contains the expected health check structure
                try
                {
                    var jsonResponse = JObject.Parse(responseBody);
                    // Verify it's a LabKey Server by checking for the "healthy" property
                    var healthy = jsonResponse.Value<bool?>(@"healthy");
                    if (!healthy.HasValue)
                    {
                        throw new PanoramaServerException(
                            new ErrorMessageBuilder(ServerStateEnum.unknown.Error(uri))
                                .Uri(validationUri)
                                .ErrorDetail(string.Format(
                                    Resources.WebPanoramaClient_ValidateUri_Server_did_not_return_a_valid_LabKey_Server_response___0__is_not_a_LabKey_server_,
                                    uri)).ToString());
                    }
                }
                catch (JsonReaderException)
                {
                    // Response is not JSON - likely HTML or other non-LabKey content
                    throw new PanoramaServerException(
                        new ErrorMessageBuilder(ServerStateEnum.unknown.Error(uri))
                            .Uri(validationUri)
                            .ErrorDetail(string.Format(
                                Resources.WebPanoramaClient_ValidateUri_Server_did_not_return_a_valid_LabKey_Server_response___0__is_not_a_LabKey_server_,
                                uri)).ToString());
                }
                
                return uri;
            }
            catch (NetworkRequestException ex)
            {
                // HttpClientWithProgress consistently throws NetworkRequestException for all network errors
                // Check if this is a DNS resolution failure
                if (ex.IsDnsFailure())
                {
                    throw PanoramaServerException.CreateWithLabKeyError(
                        ServerStateEnum.missing.Error(uri),
                        ex.RequestUri ?? uri,
                        PanoramaUtil.GetErrorFromNetworkRequestException,
                        ex);
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

                throw PanoramaServerException.CreateWithLabKeyError(
                    ServerStateEnum.unknown.Error(ServerUri),
                    uri,
                    PanoramaUtil.GetErrorFromNetworkRequestException,
                    ex);
            }
        }

        protected override PanoramaServer ValidateServerAndUser(Uri serverUri, string username, string password)
        {
            var pServer = new PanoramaServer(serverUri, username, password);

            try
            {
                return EnsureLogin(pServer);
            }
            catch (NetworkRequestException ex)
            {
                if (ex.StatusCode == HttpStatusCode.NotFound) // 404
                {
                    var newServer = pServer.HasContextPath()
                        // e.g. User entered the home page of the LabKey Server, running as the root webapp: 
                        // https://panoramaweb.org/project/home/begin.view OR https://panoramaweb.org/home/project-begin.view
                        // We will first try https://panoramaweb.org/project/ OR https://panoramaweb.org/home/ as the server URL. 
                        // And that will fail.  Remove the assumed context path and try again.
                        ? pServer.RemoveContextPath()
                        // e.g. Given server URL is https://panoramaweb.org but LabKey Server is not deployed as the root webapp.
                        // Try again with '/labkey' context path
                        : pServer.AddLabKeyContextPath();

                    if (!ReferenceEquals(pServer, newServer))
                    {
                        return EnsureLogin(newServer);
                    }
                }

                throw PanoramaServerException.CreateWithLabKeyError(
                    UserStateEnum.unknown.Error(ServerUri), 
                    PanoramaUtil.GetEnsureLoginUri(pServer), 
                    PanoramaUtil.GetErrorFromNetworkRequestException, 
                    ex);
            }
        }

        public override PanoramaServer EnsureLogin(PanoramaServer pServer)
        {
            var requestUri = PanoramaUtil.GetEnsureLoginUri(pServer);
            
            using var httpClient = new HttpClientWithProgress(new SilentProgressMonitor());
            
            // Add authorization header if credentials are available
            if (pServer.HasUserAccount())
            {
                httpClient.AddAuthorizationHeader(PanoramaServer.GetBasicAuthHeader(pServer.Username, pServer.Password));
            }

            try
            {
                string responseBody = httpClient.DownloadString(requestUri);
                
                // Validate JSON response
                JObject jsonResponse = null;
                try
                {
                    jsonResponse = JObject.Parse(responseBody);
                }
                catch
                {
                    throw new PanoramaServerException(
                        new ErrorMessageBuilder(UserStateEnum.unknown.Error(ServerUri))
                            .Uri(requestUri)
                            .ErrorDetail(string.Format(
                                Resources.WebPanoramaClient_EnsureLogin_Server_did_not_return_a_valid_JSON_response___0__is_not_a_Panorama_server_,
                                ServerUri)).ToString());
                }

                if (!PanoramaUtil.IsValidEnsureLoginResponse(jsonResponse, pServer.Username))
                {
                    throw new PanoramaServerException(
                        new ErrorMessageBuilder(UserStateEnum.unknown.Error(ServerUri))
                            .Uri(requestUri)
                            .ErrorDetail(string.Format(Resources.PanoramaUtil_EnsureLogin_Unexpected_JSON_response_from_the_server___0_, ServerUri))
                            .LabKeyError(PanoramaUtil.GetIfErrorInResponse(jsonResponse))
                            .Response(jsonResponse).ToString());
                }

                return pServer;
            }
            catch (NetworkRequestException ex)
            {
                if (ex.StatusCode == HttpStatusCode.Unauthorized) // 401
                {
                    // Check if we were redirected (request URI != response URI)
                    // HttpClientWithProgress doesn't expose redirect info, so we detect it by retrying
                    // Authorization headers are not persisted across redirects by HttpClient
                    // TODO: Consider if we need explicit redirect handling here

                    if (!pServer.HasUserAccount())
                    {
                        // We were not given a username / password. This means that the user wants anonymous access
                        // to the server. Since we got a 401 (Unauthorized) error, not a 404 (Not found), this means
                        // that the server is a Panorama server.
                        return pServer;
                    }

                    throw PanoramaServerException.CreateWithLabKeyError(
                        UserStateEnum.nonvalid.Error(ServerUri),
                        requestUri,
                        PanoramaUtil.GetErrorFromNetworkRequestException,
                        ex);
                }

                throw;
            }
        }

        /// <summary>
        /// Downloads a given file to a given folder path and shows the progress
        /// of the download during downloading
        /// </summary>
        public override void DownloadFile(string fileUrl, string fileName, long fileSize, string realName,
            IProgressMonitor progressMonitor, IProgressStatus progressStatus)
        {
            var initialMessage = string.Format(Resources.WebPanoramaClient_DownloadFile_Downloading__0_, realName);
            progressStatus = progressStatus.ChangeMessage(initialMessage);
            progressMonitor.UpdateProgress(progressStatus);

            using var httpClient = new HttpClientWithProgress(progressMonitor, progressStatus);
            
            // Add authorization header if credentials are available
            var pServer = new PanoramaServer(ServerUri, Username, Password);
            if (pServer.HasUserAccount())
            {
                httpClient.AddAuthorizationHeader(pServer.AuthHeader);
            }

            // Use the known file size for accurate progress reporting (from .skyp file or Panorama API)
            httpClient.DownloadFile(new Uri(fileUrl), fileName, fileSize);
        }

        public override IRequestHelper GetRequestHelper()
        {
            var panoramaServer = new PanoramaServer(ServerUri, Username, Password);
            return new HttpPanoramaRequestHelper(panoramaServer, _progressMonitor, _progressStatus);
        }

        // Used by SkylineBatch
        // ReSharper disable once UnusedMember.Global
        public string DownloadStringAsync(Uri queryUri, CancellationToken cancelToken)
        {
            // Create a progress monitor that respects the cancellation token
            var progressMonitor = new SilentProgressMonitor(cancelToken);
            
            using var httpClient = new HttpClientWithProgress(progressMonitor);
            
            // Add authorization header if credentials are available
            var pServer = new PanoramaServer(ServerUri, Username, Password);
            if (pServer.HasUserAccount())
            {
                httpClient.AddAuthorizationHeader(pServer.AuthHeader);
            }

            // HttpClientWithProgress will throw OperationCanceledException if cancelToken is cancelled
            return httpClient.DownloadString(queryUri);
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
            throw new InvalidOperationException();
        }

        public virtual void ValidateFolder(string folderPath, PermissionSet permissionSet, bool checkTargetedMs = true)
        {
            throw new InvalidOperationException();
        }

        public virtual JToken GetInfoForFolders(string folder,
            IProgressMonitor progressMonitor, IProgressStatus progressStatus)
        {
            throw new InvalidOperationException();
        }

        public virtual void DownloadFile(string fileUrl, string fileName, long fileSize, string realName,
            IProgressMonitor progressMonitor, IProgressStatus progressStatus)
        {
            throw new InvalidOperationException();
        }

        public virtual Uri SendZipFile(string folderPath, string zipFilePath,
            IProgressMonitor progressMonitor, IProgressStatus progressStatus)
        {
            throw new InvalidOperationException();
        }

        public virtual JObject SupportedVersionsJson()
        {
            throw new InvalidOperationException();
        }

        public IRequestHelper GetRequestHelper()
        {
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Generates LabKey folder JSON for testing
        /// </summary>
        public class PanoramaFolder
        {
            public string Name { get; }
            public bool Writable { get; }
            public bool IsTargetedMsFolder { get; }
            public bool IsLibraryFolder { get; }
            public bool IsTargetedMsModuleEnabled { get; }
            private string _parentPath;
            public readonly IList<PanoramaFolder> _children;

            public PanoramaFolder(string name, bool writable = true, bool isTargetedMsFolder = true, 
                bool isTargetedMsModuleEnabled = false, bool isLibrary = false)
            {
                Name = name;
                Writable = writable;
                IsTargetedMsFolder = isTargetedMsFolder;
                // Targeted MS module can be enabled in a folder that is not a "Targeted MS" folder type. 
                // It can be enabled in a "Collaboration" folder type, for example.
                IsTargetedMsModuleEnabled = isTargetedMsModuleEnabled || isTargetedMsFolder;
                IsLibraryFolder = isLibrary;
                _children = new List<PanoramaFolder>();
            }

            public void AddChild(PanoramaFolder child)
            {
                child._parentPath = GetPath();
                _children.Add(child);
            }

            private bool HasChildren() => _children.Count > 0;

            public string GetPath() => $@"{_parentPath ?? @"/"}{Name}/";

            // ReSharper disable LocalizableElement
            public JObject ToJson(bool addRoot = false)
            {
                if (!HasChildren() && (!Writable || !IsTargetedMsModuleEnabled))
                {
                    // Automatically add a writable subfolder if this folder is not writable, i.e. it is
                    // not a targetedMS folder or the user does not have write permissions in this folder.
                    // Otherwise, it will not get added to the folder tree (PublishDocumentDlg.AddChildContainers()).
                    AddChild(new PanoramaFolder("Subfolder"));
                }

                var folderJson = new JObject
                {
                    ["name"] = Name,
                    ["path"] = GetPath(),
                    [PanoramaUtil.PERMS_JSON_PROP] = Writable 
                        ? new JArray(PermissionSet.AUTHOR.Permissions)
                        : new JArray(PermissionSet.READER.Permissions),
                    ["folderType"] = IsTargetedMsFolder ? "Targeted MS" : "Collaboration",
                    ["activeModules"] = IsTargetedMsModuleEnabled ? new JArray("TargetedMS")
                        : new JArray("SomethingElse"),
                    ["children"] = new JArray(_children.Select(child => child.ToJson()))
                };

                if (IsLibraryFolder)
                {
                    var libraryFolder = new JObject();
                    libraryFolder["effectiveValue"] = "Library";
                    folderJson.Add("moduleProperties", new JArray(libraryFolder));
                }
                if (!addRoot) return folderJson;
                var root = new JObject
                {
                    ["name"] = "",
                    ["children"] = new JArray(folderJson)
                };
                return root;
            }
            // ReSharper restore LocalizableElement
        }
    }
}
