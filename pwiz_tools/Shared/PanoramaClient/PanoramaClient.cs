using System;
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

        public abstract Uri ValidateUri(Uri serverUri, bool tryNewProtocol = true);
        public abstract PanoramaServer ValidateServerAndUser(Uri serverUri, string username, string password);
        public abstract PanoramaServer EnsureLogin(PanoramaServer pServer);

        public abstract void DownloadFile(string fileUrl, string fileName, long fileSize, string realName,
            IProgressMonitor pm, IProgressStatus progressStatus);

        public abstract IRequestHelper GetRequestHelper(bool forPublish = false);

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
                JToken response = requestHelper.Get(requestUri, string.Format("Error validating folder '{0}'", folderPath));

                if (permission != null && !PanoramaUtil.CheckFolderPermissions(response, (FolderPermission)permission))
                {
                    throw new PanoramaServerException(FolderState.nopermission, folderPath, ServerUri, requestUri, Username);
                }

                if (checkTargetedMs && !PanoramaUtil.HasTargetedMsModule(response))
                {
                    throw new PanoramaServerException(FolderState.notpanorama, folderPath, ServerUri, requestUri, Username);
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
                return requestHelper.Get(uri, string.Format("Error getting information for folder '{0}'", folder));
            }
        }

        public virtual Uri SendZipFile(string folderPath, string zipFilePath, IProgressMonitor progressMonitor)
        {
            _progressMonitor = progressMonitor;
            _progressStatus = new ProgressStatus(String.Empty);
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
                                "Deleting temporary file on server"));
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
                _progressStatus = _progressStatus.ChangeMessage("Waiting for data import completion...");
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
            var startTime = DateTime.Now;
            while (true)
            {
                if (progressMonitor.IsCanceled)
                    return null;

                JToken jStatusResponse = requestHelper.Get(statusUri, "Error getting the status of the document import job");
                JToken rows = jStatusResponse[@"rows"];
                var row = rows.FirstOrDefault(r => (int)r[@"RowId"] == pipelineJobRowId);
                if (row == null)
                    continue;

                var status = new ImportStatus((string)row[@"Status"]);
                if (status.IsComplete)
                {
                    progressMonitor.UpdateProgress(_progressStatus.Complete());
                    return new Uri(ServerUri, (string)row[@"_labkeyurl_Description"]);
                }

                else if (status.IsError || status.IsCancelled)
                {
                    var jobUrl = new Uri(ServerUri, (string)row[@"_labkeyurl_RowId"]);
                    var e = new PanoramaImportErrorException(ServerUri, jobUrl, status.IsCancelled);
                    progressMonitor.UpdateProgress(
                        _progressStatus = _progressStatus.ChangeErrorException(e));
                    throw e;
                }

                updateProgressAndWait(status, progressMonitor, startTime);
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

            JToken importResponse = requestHelper.Post(importUrl, dataImportInformation, "Error adding to the document import queue on the server.");


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
                requestHelper.RequestJsonResponse();
                requestHelper.AsyncUploadFile(tmpUploadUri, @"PUT",
                    PathEx.SafePath(zipFilePath));

                // Wait for the upload to complete
                Monitor.Wait(this);
            }

            if (uploadError != null)
            {
                // There was an error uploading the file.
                // uploadError gets set in webClient_UploadFileCompleted if there was an error in the LabKey JSON response.
                throw new PanoramaServerException("There was an error uploading the file.", tmpUploadUri, uploadError);
            }

            // Remove the "Temporary" header added while uploading the temporary file
            // _webClient.Headers.Remove(@"Temporary");
            requestHelper.RemoveHeader(@"Temporary");

            return tmpUploadUri;
        }

        private string GetWebDavPath(string folderPath, IRequestHelper requestHelper)
        {
            var getPipelineContainerUri = PanoramaUtil.GetPipelineContainerUrl(ServerUri, folderPath);

            var jsonResponse = requestHelper.Get(getPipelineContainerUri,
                string.Format("Error getting the WebDAV url for folder '{0}'", folderPath));
            var webDavUrl = (string)jsonResponse[@"webDavURL"];
            if (webDavUrl == null)
            {
                throw new PanoramaServerException(TextUtil.LineSeparate(
                    "Missing webDavURL in response.",
                    string.Format("URL: {0}", getPipelineContainerUri),
                    string.Format("Response: {0}", jsonResponse)));
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

        private void updateProgressAndWait(ImportStatus jobStatus, IProgressMonitor progressMonitor, DateTime startTime)
        {
            var match = _runningStatusRegex.Match(jobStatus.StatusString);
            if (match.Success)
            {
                var currentProgress = _progressStatus.PercentComplete;

                if (int.TryParse(match.Groups[1].Value, out var progress))
                {
                    progress = Math.Max(progress, currentProgress);
                    _progressStatus = _progressStatus.ChangeMessage(string.Format("Importing data. {0}% complete.", progress));
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

                    Thread.Sleep(_waitTime * 1000);
                    return;
                }
            }

            if (!jobStatus.IsRunning)
            {
                // Display the status since we don't recognize it.  This could be, for example, an "Import Waiting" status if another 
                // Skyline document is currently being imported on the server. 
                _progressMonitor.UpdateProgress(_progressStatus = _progressStatus =
                    _progressStatus.ChangeMessage(string.Format("Status on server is: {0}", jobStatus.StatusString)));
            }

            else if (!_progressStatus.Message.Equals("Waiting for data import completion..."))
            {
                // Import is running now. Reset the progress message in case it had been set to something else (e.g. "Import Waiting") in a previous iteration.  
                progressMonitor.UpdateProgress(_progressStatus =
                    _progressStatus.ChangeMessage("Waiting for data import completion..."));
            }

            // This is probably an older server (pre LK19.3) that does not include the progress percent in the status.
            // Wait between 1 and 5 seconds before checking status again.
            var elapsed = (DateTime.Now - startTime).TotalMinutes;
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
                "Error renaming temporary zip file: "
            );
        }

        private void DeleteTempZipFile(Uri sourceUri, string authHeader, IRequestHelper requestHelper)
        {
            var request = (HttpWebRequest)WebRequest.Create(sourceUri.ToString());

            requestHelper.DoRequest(request,
                @"DELETE",
                authHeader,
                "Error deleting temporary zip file: ");
        }

        private void ConfirmFileOnServer(Uri sourceUri, string authHeader, IRequestHelper requestHelper)
        {
            var request = (HttpWebRequest)WebRequest.Create(sourceUri);

            // Do a HEAD request to check if the file exists on the server.
            requestHelper.DoRequest(request,
                @"HEAD",
                authHeader,
                "File was not uploaded to the server. Please try again, or if the problem persists, please contact your Panorama server administrator."
                );
        }

        private void webClient_UploadProgressChanged(object sender, UploadProgressChangedEventArgs e,
            IRequestHelper requestHelper)
        {
            var message = e == null ? "Progress Updated" : string.Format(FileSize.FormatProvider,
                "Uploaded {0:fs} of {1:fs}",
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
                Monitor.PulseAll(this);
                uploadError = ParseUploadFileCompletedEventArgs(e);
            }
        }

        protected virtual LabKeyError ParseUploadFileCompletedEventArgs(UploadFileCompletedEventArgs e)
        {
            var serverResponse = e?.Result;
            return serverResponse != null ? PanoramaUtil.GetIfErrorInResponse(Encoding.UTF8.GetString(serverResponse)) : null;
        }

        public virtual JObject SupportedVersionsJson()
        {
            var uri = PanoramaUtil.Call(ServerUri, @"targetedms", null, @"getMaxSupportedVersions");

            using (var requestHelper = GetRequestHelper())
            {
                try
                {
                    return requestHelper.Get(uri, "Error getting the maximum supported version of Skyline documents on the server.");
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
    }

    public class WebPanoramaClient : AbstractPanoramaClient
    {
        public WebPanoramaClient(Uri serverUri, string username, string password) : base(serverUri, username, password)
        {
        }

        public override Uri ValidateUri(Uri uri, bool tryNewProtocol = true)
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
                var labkeyError = PanoramaUtil.GetIfErrorInResponse(response);
                // Invalid URL
                if (ex.Status == WebExceptionStatus.NameResolutionFailure)
                {
                    var responseUri = response?.ResponseUri;
                    throw new PanoramaServerException(ServerStateEnum.missing, uri,
                        (responseUri != null && !uri.Equals(responseUri) ? responseUri : null), labkeyError, ex);
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

                throw new PanoramaServerException(ServerStateEnum.unknown, ServerUri, uri, labkeyError, ex);
            }
        }

        public override PanoramaServer ValidateServerAndUser(Uri serverUri, string username, string password)
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

                var labkeyError = PanoramaUtil.GetIfErrorInResponse(response);
                throw new PanoramaServerException(UserStateEnum.unknown, ServerUri, PanoramaUtil.GetEnsureLoginUri(pServer), labkeyError, ex);
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
                        throw new PanoramaServerException(UserStateEnum.nonvalid, ServerUri, requestUri, response,
                            string.Format("Response received from server: {0} {1}", response.StatusCode, response.StatusDescription));
                    }

                    JObject jsonResponse = null;

                    if (!(PanoramaUtil.TryGetJsonResponse(response, ref jsonResponse)
                          && PanoramaUtil.IsValidEnsureLoginResponse(jsonResponse, pServer.Username)))
                    {
                        if (jsonResponse == null)
                        {
                            throw new PanoramaServerException(UserStateEnum.unknown, ServerUri, requestUri, response,
                                string.Format("Server did not return a valid JSON response. {0} is not a Panorama server.", ServerUri));
                        }
                        else
                        {
                            var jsonText = jsonResponse.ToString(Formatting.None);
                            jsonText = jsonText.Replace(@"{", @"{{"); // escape curly braces
                            throw new PanoramaServerException(UserStateEnum.unknown, ServerUri, requestUri,
                                response,
                                string.Format(Resources.PanoramaUtil_EnsureLogin_Unexpected_JSON_response_from_the_server___0_, jsonText));
                        }
                    }

                    return pServer;
                }
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                var labKeyError = PanoramaUtil.GetIfErrorInResponse(response);

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
                            throw new PanoramaServerException(UserStateEnum.nonvalid, ServerUri, requestUri, labKeyError, ex); // User cannot be authenticated
                        }
                    }

                    if (!pServer.HasUserAccount())
                    {
                        // We were not given a username / password. This means that the user wants anonymous access
                        // to the server. Since we got a 401 (Unauthorized) error, not a 404 (Not found), this means
                        // that the server is a Panorama server.
                        return pServer;
                    }

                    throw new PanoramaServerException(UserStateEnum.nonvalid, ServerUri, requestUri, labKeyError, ex); // User cannot be authenticated
                }

                throw;
            }
        }

        public override void ValidateFolder(Uri requestUri, string folderPath, FolderPermission? permission, bool checkTargetedMs = true)
        {
            try
            {
                base.ValidateFolder(requestUri, folderPath, permission, checkTargetedMs);
            }
            catch (WebException ex)
            {
                // TODO: WebException should be caught by IRequestHelper.Get(uri) in base.ValidateFolder and throws back as PanoramaServerException
                //       Remove the following code?
                var response = ex.Response as HttpWebResponse;
                var labkeyError = PanoramaUtil.GetIfErrorInResponse(response);
                if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new PanoramaServerException(FolderState.notfound, folderPath, ServerUri, requestUri, Username, labkeyError, ex);
                }
                else
                {
                    throw new PanoramaServerException(FolderState.unknown, folderPath, ServerUri,
                        requestUri, Username, labkeyError, ex);
                }
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
    }
}
