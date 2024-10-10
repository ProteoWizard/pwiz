using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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

        Uri SendZipFile(string folderPath, string zipFilePath, IProgressMonitor progressMonitor);

        JObject SupportedVersionsJson();

        IRequestHelper GetRequestHelper(bool forPublish = false);
    }

    public abstract class AbstractPanoramaClient : IPanoramaClient
    {
        private IProgressMonitor _progressMonitor;
        private IProgressStatus _progressStatus;

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
            IProgressMonitor pm, IProgressStatus progressStatus);

        public abstract IRequestHelper GetRequestHelper(bool forPublish = false);

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

        public void ValidateFolder(string folderPath, FolderPermission? permission, bool checkTargetedMs = true)
        {
            var requestUri = PanoramaUtil.GetContainersUri(ServerUri, folderPath, false);
            ValidateFolder(requestUri, folderPath, permission, checkTargetedMs);
        }

        public virtual void ValidateFolder(Uri requestUri, string folderPath, FolderPermission? permission, bool checkTargetedMs = true)
        {
            using (var requestHelper = GetRequestHelper())
            {
                JToken response = requestHelper.Get(requestUri,
                    string.Format(Resources.AbstractPanoramaClient_ValidateFolder_Error_validating_folder___0__,
                        folderPath));

                if (permission != null && !PanoramaUtil.CheckFolderPermissions(response, (FolderPermission)permission))
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

        public virtual JToken GetInfoForFolders(string folder)
        {
            var server = new PanoramaServer(ServerUri, Username, Password);
            if (server.HasUserAccount())
            {
                server = EnsureLogin(server);
                ServerUri = server.URI;
            }

            // Retrieve folders from server.
            var uri = PanoramaUtil.GetContainersUri(ServerUri, folder, true);

            using (var requestHelper = GetRequestHelper())
            {
                return requestHelper.Get(uri,
                    string.Format(
                        Resources.AbstractPanoramaClient_GetInfoForFolders_Error_getting_information_for_folder___0__,
                        folder));
            }
        }

        public virtual Uri SendZipFile(string folderPath, string zipFilePath, IProgressMonitor progressMonitor)
        {
            _progressMonitor = progressMonitor;
            _progressStatus = new ProgressStatus(string.Empty);
            var zipFileName = Path.GetFileName(zipFilePath) ?? string.Empty;

            // Upload zip file to pipeline folder.
            using (var requestHelper = GetRequestHelper(true))
            {
                var webDavUrl = GetWebDavPath(folderPath, requestHelper);

                // Upload Url minus the name of the zip file.
                var baseUploadUri = new Uri(ServerUri, webDavUrl); // webDavUrl will start with the context path (e.g. /labkey/...). We will get the correct URL even if serverUri has a context path.

                // Use Uri.EscapeDataString instead of Uri.EscapeUriString.  
                // The latter will not escape characters such as '+' or '#' 
                var escapedZipFileName = Uri.EscapeDataString(zipFileName);
                var uploadUri = new Uri(baseUploadUri, escapedZipFileName);

                var tmpUploadUri = UploadTempZipFile(zipFilePath, baseUploadUri, escapedZipFileName, requestHelper);

                var authHeader = new PanoramaServer(ServerUri, Username, Password).AuthHeader;
                if (progressMonitor.IsCanceled)
                {
                    // Delete the temporary file on the server
                    progressMonitor.UpdateProgress(
                        _progressStatus =
                            _progressStatus.ChangeMessage(
                                Resources.AbstractPanoramaClient_SendZipFile_Deleting_temporary_file_on_server));
                    DeleteTempZipFile(tmpUploadUri, authHeader, requestHelper);
                    return null;
                }

                // Make sure the temporary file was uploaded to the server
                ConfirmFileOnServer(tmpUploadUri, authHeader, requestHelper);

                // Rename the temporary file
                _progressStatus = _progressStatus.ChangeMessage(
                    "Renaming temporary file on server");
                progressMonitor.UpdateProgress(_progressStatus);

                RenameTempZipFile(tmpUploadUri, uploadUri, authHeader, requestHelper);

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
                JToken rows = jStatusResponse[@"rows"];
                var row = rows.FirstOrDefault(r => (int)r[@"RowId"] == pipelineJobRowId);
                if (row == null)
                    continue;

                var jobUrl = new Uri(ServerUri, (string)row[@"_labkeyurl_RowId"]);
                var status = new ImportStatus((string)row[@"Status"]);
                if (status.IsComplete)
                {
                    progressMonitor.UpdateProgress(_progressStatus.Complete());
                    return new Uri(ServerUri, (string)row[@"_labkeyurl_Description"]);
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
            var details = importResponse[@"UploadedJobDetails"];
            return (int)details[0][@"RowId"];
        }

        private Uri UploadTempZipFile(string zipFilePath, Uri baseUploadUri, string escapedZipFileName,
            IRequestHelper requestHelper)
        {
            LabKeyError uploadError = null; // This is set if LabKey returns an error while uploading the file.

            requestHelper.AddUploadFileCompletedEventHandler((sender, e) => webClient_UploadFileCompleted(sender, e, out uploadError));
            requestHelper.AddUploadProgressChangedEventHandler((sender, e) => webClient_UploadProgressChanged(sender, e, requestHelper));

            var tmpUploadUri = new Uri(baseUploadUri, escapedZipFileName + @".part");
            lock (this)
            {
                // Write to a temp file first. This will be renamed after a successful upload or deleted if the upload is canceled.
                // Add a "Temporary" header so that LabKey marks this as a temporary file.
                // https://www.labkey.org/issues/home/Developer/issues/details.view?issueId=19220
                requestHelper.AddHeader(@"Temporary", @"T");
                requestHelper.RequestJsonResponse(); // Request a JSON response so that we can parse any LabKey-specific errors
                requestHelper.AsyncUploadFile(tmpUploadUri, @"PUT",
                    PathEx.SafePath(zipFilePath));

                // Wait for the upload to complete
                Monitor.Wait(this);
            }

            if (uploadError != null)
            {
                // There was an error uploading the file.
                // uploadError gets set in webClient_UploadFileCompleted if there was an error in the LabKey JSON response.
                throw new PanoramaServerException(
                    new ErrorMessageBuilder(Resources.AbstractPanoramaClient_UploadTempZipFile_There_was_an_error_uploading_the_file_)
                        .Uri(tmpUploadUri)
                        .LabKeyError(uploadError).ToString());
            }

            // Remove the "Temporary" header added while uploading the temporary file
            requestHelper.RemoveHeader(@"Temporary");

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

        private void RenameTempZipFile(Uri sourceUri, Uri destUri, string authHeader, IRequestHelper requestHelper)
        {
            var request = (HttpWebRequest)WebRequest.Create(sourceUri);

            // Destination URI.  
            // NOTE: Do not use Uri.ToString since it does not return the escaped version.
            request.Headers.Add(@"Destination", destUri.AbsoluteUri);

            // If a file already exists at the destination URI, it will not be overwritten.  
            // The server would return a 412 Precondition Failed status code.
            request.Headers.Add(@"Overwrite", @"F");

            requestHelper.DoRequest(request,
                @"MOVE",
                authHeader,
                Resources
                    .AbstractPanoramaClient_RenameTempZipFile_There_was_an_error_renaming_the_temporary_zip_file_on_the_server_
            );
        }

        private void DeleteTempZipFile(Uri sourceUri, string authHeader, IRequestHelper requestHelper)
        {
            var request = (HttpWebRequest)WebRequest.Create(sourceUri.ToString());

            requestHelper.DoRequest(request,
                @"DELETE",
                authHeader,
                Resources
                    .AbstractPanoramaClient_DeleteTempZipFile_There_was_an_error_deleting_the_temporary_zip_file_on_the_server_
            );
        }

        private void ConfirmFileOnServer(Uri sourceUri, string authHeader, IRequestHelper requestHelper)
        {
            var request = (HttpWebRequest)WebRequest.Create(sourceUri);
            // Do a HEAD request to check if the file exists on the server.
            requestHelper.DoRequest(request,
                @"HEAD",
                authHeader,
                Resources.AbstractPanoramaClient_ConfirmFileOnServer_File_was_not_uploaded_to_the_server__Please_try_again__or_if_the_problem_persists__please_contact_your_Panorama_server_administrator_
            );
        }

        private void webClient_UploadProgressChanged(object sender, UploadProgressChangedEventArgs e,
            IRequestHelper requestHelper)
        {
            var message = e == null
                ? Resources.AbstractPanoramaClient_webClient_UploadProgressChanged_Progress_Updated
                : string.Format(FileSize.FormatProvider,
                    Resources.AbstractPanoramaClient_webClient_UploadProgressChanged_Uploaded__0_fs__of__1_fs_,
                    e.BytesSent, e.TotalBytesToSend);
            int percentComplete = e == null ? 20 : e.ProgressPercentage;
            _progressStatus = _progressStatus.ChangeMessage(message).ChangePercentComplete(percentComplete);
            _progressMonitor.UpdateProgress(_progressStatus);
            if (_progressMonitor.IsCanceled)
                requestHelper.CancelAsyncUpload();
        }

        private void webClient_UploadFileCompleted(object sender, UploadFileCompletedEventArgs e, out LabKeyError uploadError)
        {
            lock (this)
            {
                uploadError = ParseUploadFileCompletedEventArgs(e);
                Monitor.PulseAll(this);
            }
        }

        protected virtual LabKeyError ParseUploadFileCompletedEventArgs(UploadFileCompletedEventArgs e)
        {
            if (e == null) return null;
            // Check the Error and Cancelled properties first to determine whether the asynchronous upload completed.
            // If the upload file operation did not complete correctly, the Result property's value is not valid
            // and accessing it to read the server response throws a TargetInvocationException.
            if (e.Error != null)
            {
                return new LabKeyError(e.Error.ToString(), null);
            }
            if (e.Cancelled)
            {
                return new LabKeyError(Resources.AbstractPanoramaClient_ParseUploadFileCompletedEventArgs_Request_cancelled, null);
            }

            try
            {
                var serverResponse = e.Result;
                return serverResponse != null ? PanoramaUtil.GetIfErrorInResponse(Encoding.UTF8.GetString(serverResponse)) : null;
            }
            catch (Exception ex)
            {
                // Asynchronous file upload runs on a worker thread. Handle any exceptions that are thrown otherwise the Skyline window will crash.
                return new LabKeyError(CommonTextUtil.LineSeparate(
                    Resources
                        .AbstractPanoramaClient_ParseUploadFileCompletedEventArgs_There_was_an_error_reading_the_server_response_,
                    ex.ToString()), null);
            }
            
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

            ValidateFolder(parentFolderPath, FolderPermission.admin, false); // Parent folder should exist and have admin permissions.


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
                using (var webClient = new WebClient())
                {
                    webClient.DownloadString(uri);
                    return uri;
                }
            }
            catch (WebException ex)
            {
                // Invalid URL
                if (ex.Status == WebExceptionStatus.NameResolutionFailure)
                {
                    var response = ex.Response as HttpWebResponse;
                    var responseUri = response?.ResponseUri;

                    throw PanoramaServerException.CreateWithResponseDisposal(
                        ServerStateEnum.missing.Error(uri),
                        responseUri != null && !uri.Equals(responseUri) ? responseUri : null,
                        PanoramaUtil.GetErrorFromWebException,
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

                throw PanoramaServerException.CreateWithResponseDisposal(
                    ServerStateEnum.unknown.Error(ServerUri), 
                    uri,
                    PanoramaUtil.GetErrorFromWebException,
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
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;

                if (response != null && response.StatusCode == HttpStatusCode.NotFound) // 404
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

                throw PanoramaServerException.CreateWithResponseDisposal(
                    UserStateEnum.unknown.Error(ServerUri), 
                    PanoramaUtil.GetEnsureLoginUri(pServer), 
                    PanoramaUtil.GetErrorFromWebException, 
                    ex);
            }
        }

        public override PanoramaServer EnsureLogin(PanoramaServer pServer)
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
                        throw new PanoramaServerException(
                            new ErrorMessageBuilder(UserStateEnum.nonvalid.Error(ServerUri))
                                .Uri(requestUri)
                                .ErrorDetail(string.Format("Response received from server: {0} {1}",
                                    response.StatusCode, response.StatusDescription))
                                .LabKeyError(PanoramaUtil.GetIfErrorInResponse(response)).ToString()
                        );
                    }

                    JObject jsonResponse = null;

                    if (!(PanoramaUtil.TryGetJsonResponse(response, ref jsonResponse)
                          && PanoramaUtil.IsValidEnsureLoginResponse(jsonResponse, pServer.Username)))
                    {
                        var error = UserStateEnum.unknown.Error(ServerUri);
                        if (jsonResponse == null)
                        {
                            throw new PanoramaServerException(
                                new ErrorMessageBuilder(error)
                                    .Uri(requestUri)
                                    .ErrorDetail(string.Format(
                                        Resources.WebPanoramaClient_EnsureLogin_Server_did_not_return_a_valid_JSON_response___0__is_not_a_Panorama_server_,
                                        ServerUri)).ToString());
                        }
                        else
                        {
                            throw new PanoramaServerException(
                                new ErrorMessageBuilder(error)
                                    .Uri(requestUri)
                                    .ErrorDetail(Resources.PanoramaUtil_EnsureLogin_Unexpected_JSON_response_from_the_server___0_)
                                    .LabKeyError(PanoramaUtil.GetIfErrorInResponse(response))
                                    .Response(jsonResponse).ToString());
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

                        throw PanoramaServerException.CreateWithResponseDisposal(
                            UserStateEnum.nonvalid.Error(ServerUri), 
                            requestUri, 
                            PanoramaUtil.GetErrorFromWebException, 
                            ex);
                    }

                    if (!pServer.HasUserAccount())
                    {
                        // We were not given a username / password. This means that the user wants anonymous access
                        // to the server. Since we got a 401 (Unauthorized) error, not a 404 (Not found), this means
                        // that the server is a Panorama server.
                        return pServer;
                    }

                    throw PanoramaServerException.CreateWithResponseDisposal(
                        UserStateEnum.nonvalid.Error(ServerUri),
                        requestUri, 
                        PanoramaUtil.GetErrorFromWebException, 
                        ex);
                }

                throw;
            }
        }

        /// <summary>
        /// Downloads a given file to a given folder path and shows the progress
        /// of the download during downloading
        /// </summary>
        public override void DownloadFile(string fileUrl, string fileName, long fileSize, string realName, IProgressMonitor pm, IProgressStatus progressStatus)
        {
            // TODO: Change this to use IRequestHelper
            using var wc = new WebClientWithCredentials(ServerUri, Username, Password);
            wc.DownloadProgressChanged += (s, e) =>
            {
                var progressPercent = e.ProgressPercentage > 0 ? e.ProgressPercentage : -1;
                if (progressPercent == -1 && fileSize > 0)
                {
                    progressPercent = (int)(e.BytesReceived * 100 / fileSize);
                }
                var downloaded = e.BytesReceived;
                var message = CommonTextUtil.LineSeparate(
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

        public override IRequestHelper GetRequestHelper(bool forPublish = false)
        {
            var webClient = forPublish
                ? new NonStreamBufferingWebClient(ServerUri, Username, Password)
                : new WebClientWithCredentials(ServerUri, Username, Password);
            return new PanoramaRequestHelper(webClient);
        }

        // Copied from SkypSupport.cs. Build the string that shows download progress.
        private static string GetDownloadedSize(long downloaded, long fileSize)
        {
            var formatProvider = new FileSizeFormatProvider();
            return fileSize > 0
                ? string.Format(@"{0} / {1}", string.Format(formatProvider, @"{0:fs1}", downloaded),
                    string.Format(formatProvider, @"{0:fs1}", fileSize))
                : string.Format(formatProvider, @"{0:fs1}", downloaded);
        }

        public string DownloadStringAsync(Uri queryUri, CancellationToken cancelToken)
        {
            string data = null;
            Exception error = null;
            using (var webClient = new WebClientWithCredentials(ServerUri, Username, Password))
            {
                bool finishedDownloading = false;
                webClient.DownloadStringAsync(queryUri);
                webClient.DownloadStringCompleted += (sender, e) =>
                {
                    error = e.Error;
                    if (error == null)
                        data = e.Result;
                    finishedDownloading = true;
                };
                while (!finishedDownloading)
                {
                    if (cancelToken.IsCancellationRequested)
                        webClient.CancelAsync();
                }
            }

            if (error != null)
                throw error;
            return data;
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

        public virtual void ValidateFolder(string folderPath, FolderPermission? permission, bool checkTargetedMs = true)
        {
            throw new InvalidOperationException();
        }

        public virtual JToken GetInfoForFolders(string folder)
        {
            throw new InvalidOperationException();
        }

        public virtual void DownloadFile(string fileUrl, string fileName, long fileSize, string realName,
            IProgressMonitor pm, IProgressStatus progressStatus)
        {
            throw new InvalidOperationException();
        }

        public virtual Uri SendZipFile(string folderPath, string zipFilePath, IProgressMonitor progressMonitor)
        {
            throw new InvalidOperationException();
        }

        public virtual JObject SupportedVersionsJson()
        {
            throw new InvalidOperationException();
        }

        public IRequestHelper GetRequestHelper(bool forPublish = false)
        {
            throw new InvalidOperationException();
        }
    }
}
