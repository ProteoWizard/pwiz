﻿/*
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Newtonsoft.Json.Linq;
using pwiz.PanoramaClient;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Properties;
using TextUtil = pwiz.Skyline.Util.Extensions.TextUtil;

namespace pwiz.Skyline.Util
{
    [XmlRoot("server")]
    public sealed class Server : PanoramaServer, IKeyContainer<string>, IXmlSerializable
    {
        public Server(string uriText, string username, string password)
            : this(new Uri(uriText), username, password)
        {
        }

        public Server(Uri uri, string username, string password) : base(uri, username, password)
        {
        }

        public string GetKey()
        {
            return URI + (HasUserAccount() ? string.Empty : UtilResources.Server_GetKey___anonymous_);
        }


        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private Server()
        {
        }

        private enum ATTR
        {
            username,
            password,
            password_encrypted,
            uri
        }

        public static Server Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new Server());
        }

        private void Validate()
        {
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            Username = reader.GetAttribute(ATTR.username) ?? string.Empty;
            string encryptedPassword = reader.GetAttribute(ATTR.password_encrypted);
            if (encryptedPassword != null)
            {
                try
                {
                    Password = TextUtil.DecryptString(encryptedPassword);
                }
                catch (Exception)
                {
                    Password = string.Empty;
                }
            }
            else
            {
                Password = reader.GetAttribute(ATTR.password) ?? string.Empty;
            }
            string uriText = reader.GetAttribute(ATTR.uri);
            if (string.IsNullOrEmpty(uriText))
            {
                throw new InvalidDataException(UtilResources.Server_ReadXml_A_Panorama_server_must_be_specified);
            }
            try
            {
                URI = new Uri(uriText);
            }
            catch (UriFormatException)
            {
                throw new InvalidDataException(UtilResources.Server_ReadXml_Server_URL_is_corrupt);
            }
            // Consume tag
            reader.Read();

            Validate();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            writer.WriteAttributeString(ATTR.username, Username);
            if (!string.IsNullOrEmpty(Password))
            {
                writer.WriteAttributeString(ATTR.password_encrypted, TextUtil.EncryptString(Password));
            }
            writer.WriteAttribute(ATTR.uri, URI);
        }
        #endregion

        #region object overrides

        private bool Equals(Server other)
        {
            return string.Equals(Username, other.Username) &&
                string.Equals(Password, other.Password) &&
                Equals(URI, other.URI);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is Server && Equals((Server)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (Username != null ? Username.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Password != null ? Password.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (URI != null ? URI.GetHashCode() : 0);
                return hashCode;
            }
        }

        #endregion
    }

    

    public interface IPanoramaPublishClient
    {
        JToken GetInfoForFolders(PanoramaServer server, string folder);

        Uri SendZipFile(PanoramaServer server, string folderPath, string zipFilePath, IProgressMonitor progressMonitor);
        JObject SupportedVersionsJson(PanoramaServer server);
        void UploadSharedZipFile(Control parent, PanoramaServer server, string zipFilePath, string folderPath);
        ShareType DecideShareTypeVersion(FolderInformation folderInfo, SrmDocument document, ShareType shareType);
        ShareType GetShareType(FolderInformation folderInfo, SrmDocument document, string documentFilePath,
            DocumentFormat? fileFormatOnDisk, Control parent, ref bool cancelled);

        Uri UploadedDocumentUri { get; }
    }

    public abstract class AbstractPanoramaPublishClient :IPanoramaPublishClient
    {
        public abstract JToken GetInfoForFolders(PanoramaServer server, string folder);

        public abstract Uri SendZipFile(PanoramaServer server, string folderPath, string zipFilePath, IProgressMonitor progressMonitor);
        public abstract JObject SupportedVersionsJson(PanoramaServer server);

        private Uri _uploadedDocumentUri;

        public Uri UploadedDocumentUri
        {
            get { return _uploadedDocumentUri; }
        }

        public CacheFormatVersion GetSupportedSkydVersion(FolderInformation folderInfo)
        {
            var serverVersionsJson = SupportedVersionsJson(folderInfo.Server);
            if (serverVersionsJson == null)
            {
                // There was an error getting the server-supported skyd version for some reason.
                // Perhaps this is an older server that did not understand the request, or
                // the returned JSON was malformed. Let the document upload continue.
                return CacheFormatVersion.CURRENT;
            }

            JToken serverSkydVersion;
            if (serverVersionsJson.TryGetValue(@"SKYD_version", out serverSkydVersion))
            {
                int version;
                if (int.TryParse(serverSkydVersion.Value<string>(), out version))
                {
                    return (CacheFormatVersion) version;
                }
            }

            return CacheFormatVersion.CURRENT;
        }

        public ShareType DecideShareTypeVersion(FolderInformation folderInfo, SrmDocument document, ShareType shareType)
        {
            var cacheVersion = GetDocumentCacheVersion(document);

            if (!cacheVersion.HasValue)
            {
                // The document may not have any chromatogram data.
                return shareType;
            }

            var supportedSkylineVersion = GetSupportedVersionForCacheFormat(folderInfo, cacheVersion);
            CacheFormatVersion supportedVersion = supportedSkylineVersion.CacheFormatVersion;
            if (supportedVersion >= cacheVersion.Value)
            {
                return shareType;
            }
            
            return shareType.ChangeSkylineVersion(supportedSkylineVersion);
        }


        private SkylineVersion GetSupportedVersionForCacheFormat(FolderInformation folderInfo, CacheFormatVersion? cacheVersion)
        {
            var skydVersion = GetSupportedSkydVersion(folderInfo);
            SkylineVersion skylineVersion;
            if (!cacheVersion.HasValue || skydVersion >= cacheVersion)
            {
                // Either the document does not have any chromatograms or the server supports the document's cache version. 
                // Since the cache version does not change when the document is shared, it can be shared as the latest Skyline
                // version even if the cache version associated with that version is higher than what the server supports. 
                // Example scenario:
                // Document cache version is 14; max version supported by server is 14; current Skyline version is associated
                // with cache version 15. In this case the document can be shared as the current Skyline version even though
                // the cache version associated with the current version is higher than what the server supports. When the document
                // is shared the cache format of the document will remain at 14. Only the document format (.sky XML) will change.
                skylineVersion = SkylineVersion.SupportedForSharing().First();
            }
            else
            {
                // The server does not support the document's cache version.
                // Find the highest Skyline version consistent with the cache version supported by the server.
                skylineVersion = SkylineVersion.SupportedForSharing().FirstOrDefault(ver => ver.CacheFormatVersion <= skydVersion);
                if (skylineVersion == null)
                {
                    throw new PanoramaServerException(string.Format(
                        Resources.PublishDocumentDlg_ServerSupportsSkydVersion_, (int)cacheVersion.Value));
                }
            }

            return skylineVersion;
        }

        private static CacheFormatVersion? GetDocumentCacheVersion(SrmDocument document)
        {
            var settings = document.Settings;
            Assume.IsTrue(document.IsLoaded);
            return settings.HasResults ? settings.MeasuredResults.CacheVersion : null;
        }

        public ShareType GetShareType(FolderInformation folderInfo, SrmDocument document,
            string documentFilePath, DocumentFormat? fileFormatOnDisk, Control parent, ref bool cancelled)
        {
            var cacheVersion = GetDocumentCacheVersion(document);
            var supportedSkylineVersion = GetSupportedVersionForCacheFormat(folderInfo, cacheVersion);
            
            using (var dlgType = new ShareTypeDlg(document, documentFilePath, fileFormatOnDisk, supportedSkylineVersion, 
                       false)) // Don't offer to include mass spec data in .sky.zip - Panorama isn't expecting that
            {
                if (dlgType.ShowDialog(parent) == DialogResult.Cancel)
                {
                    cancelled = true;
                    return null;
                }
                else
                {
                    return dlgType.ShareType;
                }
            }
        }

        public void UploadSharedZipFile(Control parent, PanoramaServer server, string zipFilePath, string folderPath)
        {
            Uri result = null;

            if (server == null)
                return;
            try
            {
                var isCanceled = false;
                using (var waitDlg = new LongWaitDlg())
                {
                    waitDlg.Text = UtilResources.PublishDocumentDlg_UploadSharedZipFile_Uploading_File;
                    waitDlg.PerformWork(parent, 1000, longWaitBroker =>
                    {
                        result = SendZipFile(server, folderPath,
                            zipFilePath, longWaitBroker);
                        if (longWaitBroker.IsCanceled)
                            isCanceled = true;
                    });
                }
                if (!isCanceled) // if user not canceled 
                {
                    _uploadedDocumentUri = result;
                    String message = UtilResources.AbstractPanoramaPublishClient_UploadSharedZipFile_Upload_succeeded__would_you_like_to_view_the_file_in_Panorama_;
                    if (MultiButtonMsgDlg.Show(parent, message, MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false)
                        == DialogResult.Yes)
                        Process.Start(result.ToString());
                }
            }
            catch (Exception x)
            {
                var panoramaEx = x.InnerException as PanoramaImportErrorException;
                if (panoramaEx != null)
                {
                    var message = panoramaEx.JobCancelled
                        ? UtilResources.AbstractPanoramaPublishClient_UploadSharedZipFile_Document_import_was_cancelled_on_the_server__Would_you_like_to_go_to_Panorama_
                        : UtilResources
                            .AbstractPanoramaPublishClient_UploadSharedZipFile_An_error_occured_while_uploading_to_Panorama__would_you_like_to_go_to_Panorama_;

                    if (MultiButtonMsgDlg.Show(parent, message, MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false)
                        == DialogResult.Yes)
                        Process.Start(panoramaEx.JobUrl.ToString());
                }
                else
                {
                    MessageDlg.ShowException(parent, x);
                }
            }

            // Change PanoramaUrl setting to the successful url used
            var uriString = server.URI + folderPath;
            uriString = Uri.EscapeUriString(uriString);
            var window = parent as SkylineWindow;
            if (window != null && Uri.IsWellFormedUriString(uriString, UriKind.Absolute)) // cant do Uri.isWellFormed because of port and ip
            {
                window.ChangeDocPanoramaUri(new Uri(uriString));
            }
        }
    }

    class WebPanoramaPublishClient : AbstractPanoramaPublishClient
    {
        private WebClientWithCredentials _webClient;
        private IProgressMonitor _progressMonitor;
        private IProgressStatus _progressStatus;

        private readonly Regex _runningStatusRegex = new Regex(@"RUNNING, (\d+)%");
        private int _waitTime = 1;

        public override JToken GetInfoForFolders(PanoramaServer server, string folder)
        {
            return new WebPanoramaClient(server.URI, server.Username, server.Password).GetInfoForFolders(folder);
        }

        public override Uri  SendZipFile(PanoramaServer server, string folderPath, string zipFilePath, IProgressMonitor progressMonitor)
        {
            _progressMonitor = progressMonitor;
            _progressStatus = new ProgressStatus(String.Empty);
            var zipFileName = Path.GetFileName(zipFilePath) ?? string.Empty;

            // Upload zip file to pipeline folder.
            using (_webClient = new NonStreamBufferingWebClient(server.URI, server.Username, server.Password))
            {
                _webClient.UploadProgressChanged += webClient_UploadProgressChanged;
                _webClient.UploadFileCompleted += webClient_UploadFileCompleted;

                var webDav = PanoramaUtil.Call(server.URI, @"pipeline", folderPath, @"getPipelineContainer", true);
                JObject jsonWebDavInfo = _webClient.Get(webDav);

                string webDavUrl = (string)jsonWebDavInfo[@"webDavURL"];

                // Upload Url minus the name of the zip file.
                var baseUploadUri = new Uri(server.URI, webDavUrl); // webDavUrl will start with the context path (e.g. /labkey/...). We will get the correct URL even if serverUri has a context path.
                // Use Uri.EscapeDataString instead of Uri.EscapleUriString.  
                // The latter will not escape characters such as '+' or '#' 
                var escapedZipFileName = Uri.EscapeDataString(zipFileName);
                var tmpUploadUri = new Uri(baseUploadUri, escapedZipFileName + @".part");
                var uploadUri = new Uri(baseUploadUri, escapedZipFileName);

                lock (this)
                {
                    // Write to a temp file first. This will be renamed after a successful upload or deleted if the upload is canceled.
                    // Add a "Temporary" header so that LabKey marks this as a temporary file.
                    // https://www.labkey.org/issues/home/Developer/issues/details.view?issueId=19220
                    _webClient.Headers.Add(@"Temporary", @"T");
                    _webClient.UploadFileAsync(tmpUploadUri, @"PUT", PathEx.SafePath(zipFilePath));

                    // Wait for the upload to complete
                    Monitor.Wait(this);
                }

                if (progressMonitor.IsCanceled)
                {
                    // Delete the temporary file on the server
                    progressMonitor.UpdateProgress(
                        _progressStatus =
                            _progressStatus.ChangeMessage(
                                UtilResources.WebPanoramaPublishClient_SendZipFile_Deleting_temporary_file_on_server));
                    DeleteTempZipFile(tmpUploadUri, server.AuthHeader);
                    return null;
                }
                // Remove the "Temporary" header added while uploading the file
                _webClient.Headers.Remove(@"Temporary");

                // Make sure the temporary file was uploaded to the server
                ConfirmFileOnServer(tmpUploadUri, server.AuthHeader);

                // Rename the temporary file
                _progressStatus = _progressStatus.ChangeMessage(
                    UtilResources.WebPanoramaPublishClient_SendZipFile_Renaming_temporary_file_on_server);
                progressMonitor.UpdateProgress(_progressStatus);

                RenameTempZipFile(tmpUploadUri, uploadUri, server.AuthHeader);

                _progressStatus = _progressStatus.ChangeMessage(UtilResources.WebPanoramaPublishClient_SendZipFile_Waiting_for_data_import_completion___);
                progressMonitor.UpdateProgress(_progressStatus = _progressStatus.ChangePercentComplete(-1));

                // Data must be completely uploaded before we can import.
                Uri importUrl = PanoramaUtil.Call(server.URI, @"targetedms", folderPath, @"skylineDocUploadApi");

                // Need to tell server which uploaded file to import.
                var dataImportInformation = new NameValueCollection
                                                {
                                                    // For now, we only have one root that user can upload to
                                                    {@"path", @"./"},
                                                    {@"file", zipFileName}
                                                };
                
                JToken importResponse = _webClient.Post(importUrl, dataImportInformation);

                // ID to check import status.
                var details = importResponse[@"UploadedJobDetails"];
                int rowId = (int)details[0][@"RowId"];
                Uri statusUri = PanoramaUtil.Call(server.URI, @"query", folderPath, @"selectRows",
                    @"query.queryName=job&schemaName=pipeline&query.rowId~eq=" + rowId);
                // Wait for import to finish before returning.
                var startTime = DateTime.Now;
                while (true)
                {
                    if (progressMonitor.IsCanceled)
                        return null;

                    JToken jStatusResponse = _webClient.Get(statusUri);
                    JToken rows = jStatusResponse[@"rows"];
                    var row = rows.FirstOrDefault(r => (int)r[@"RowId"] == rowId);
                    if (row == null)
                        continue;

                    var status = new ImportStatus((string) row[@"Status"]);
                    if (status.IsComplete)
                    {
                        progressMonitor.UpdateProgress(_progressStatus.Complete());
                        return new Uri(server.URI, (string)row[@"_labkeyurl_Description"]);
                    }
                  
                    else if (status.IsError || status.IsCancelled)
                    {
                        var jobUrl = new Uri(server.URI, (string) row[@"_labkeyurl_RowId"]);
                        var e = new PanoramaImportErrorException(server.URI, jobUrl, status.IsCancelled);
                        progressMonitor.UpdateProgress(
                            _progressStatus = _progressStatus.ChangeErrorException(e));
                        throw e;
                    }

                    updateProgressAndWait(status, progressMonitor, _progressStatus, startTime);
                }
            }
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

        private void updateProgressAndWait(ImportStatus jobStatus, IProgressMonitor progressMonitor, IProgressStatus status, DateTime startTime)
        {
            var match = _runningStatusRegex.Match(jobStatus.StatusString);
            if (match.Success)
            {
                var currentProgress = _progressStatus.PercentComplete;

                if (int.TryParse(match.Groups[1].Value, out var progress))
                {
                    progress = Math.Max(progress, currentProgress);
                    _progressStatus = _progressStatus.ChangeMessage(string.Format(UtilResources.WebPanoramaPublishClient_updateProgressAndWait_Importing_data___0___complete_, progress));
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
                    _progressStatus.ChangeMessage(string.Format(UtilResources.WebPanoramaPublishClient_SendZipFile_Status_on_server_is___0_, jobStatus.StatusString)));
            }

            else if (!_progressStatus.Message.Equals(UtilResources
                .WebPanoramaPublishClient_SendZipFile_Waiting_for_data_import_completion___))
            {
                // Import is running now. Reset the progress message in case it had been set to something else (e.g. "Import Waiting") in a previous iteration.  
                progressMonitor.UpdateProgress(_progressStatus =
                    _progressStatus.ChangeMessage(UtilResources
                        .WebPanoramaPublishClient_SendZipFile_Waiting_for_data_import_completion___));
            }

            // This is probably an older server (pre LK19.3) that does not include the progress percent in the status.
            // Wait between 1 and 5 seconds before checking status again.
            var elapsed = (DateTime.Now - startTime).TotalMinutes;
            var sleepTime = elapsed > 5 ? 5 * 1000 : (int)(Math.Max(1, elapsed % 5) * 1000);
            Thread.Sleep(sleepTime);
        }

        public override JObject SupportedVersionsJson(PanoramaServer server)
        {
            var uri = PanoramaUtil.Call(server.URI, @"targetedms", null, @"getMaxSupportedVersions");

            string supportedVersionsJson;

            using (var webClient = new WebClientWithCredentials(server.URI, server.Username, server.Password))
            {
                try
                {
                    supportedVersionsJson = webClient.DownloadString(uri);
                }
                catch (WebException)
                {
                    // An exception will be thrown if the response code is not 200 (Success).
                    // We may be communicating with an older server that does not understand the request.
                    return null;
                }
            }
            try
            {
                return JObject.Parse(supportedVersionsJson);
            }
            catch (Exception)
            {
                // If there was an error in parsing the JSON.
                return null;
            }
        }

        private class NonStreamBufferingWebClient : WebClientWithCredentials
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

        private void RenameTempZipFile(Uri sourceUri, Uri destUri, string authHeader)
        {
            var request = (HttpWebRequest)WebRequest.Create(sourceUri);

            // Destination URI.  
            // NOTE: Do not use Uri.ToString since it does not return the escaped version.
            request.Headers.Add(@"Destination", destUri.AbsoluteUri);

            // If a file already exists at the destination URI, it will not be overwritten.  
            // The server would return a 412 Precondition Failed status code.
            request.Headers.Add(@"Overwrite", @"F");

            DoRequest(request,
                @"MOVE",
                authHeader,
                UtilResources.WebPanoramaPublishClient_RenameTempZipFile_Error_renaming_temporary_zip_file__,
                5 // Try up to 5 times.
                );
        }

        private static void DoRequest(HttpWebRequest request, string method, string authHeader, string errorMessage, int maxTryCount = 1)
        {
            request.Method = method;
            request.Headers.Add(HttpRequestHeader.Authorization, authHeader);

            try
            {
                using (request.GetResponse())
                {
                }
            }
            // An exception will be thrown if the response code is not 200 (Success).
            catch (WebException x)
            {
                if (--maxTryCount > 0)
                {
                    DoRequest(request, method, authHeader, errorMessage, maxTryCount);
                }

                var msg = x.Message;
                if (x.InnerException != null)
                {
                    msg += @". Inner Exception: " + x.InnerException.Message;
                }
                throw new Exception(
                    TextUtil.LineSeparate(errorMessage, msg), x);
            }
        }

        private void DeleteTempZipFile(Uri sourceUri, string authHeader)
        {
            var request = (HttpWebRequest)WebRequest.Create(sourceUri.ToString());

            DoRequest(request,
                @"DELETE",
                authHeader,
                UtilResources.WebPanoramaPublishClient_DeleteTempZipFile_Error_deleting_temporary_zip_file__);
        }

        private void ConfirmFileOnServer(Uri sourceUri, string authHeader)
        {
            var request = (HttpWebRequest)WebRequest.Create(sourceUri);

            // Do a HEAD request to check if the file exists on the server.
            DoRequest(request,
                @"HEAD",
                authHeader,
                UtilResources.WebPanoramaPublishClient_ConfirmFileOnServer_File_was_not_uploaded_to_the_server__Please_try_again__or_if_the_problem_persists__please_contact_your_Panorama_server_administrator_
                );
        }

        public void webClient_UploadProgressChanged(object sender, UploadProgressChangedEventArgs e)
        {
            var message = string.Format(FileSize.FormatProvider,
                UtilResources.WebPanoramaPublishClient_webClient_UploadProgressChanged_Uploaded__0_fs__of__1_fs_,
                e.BytesSent, e.TotalBytesToSend);
            _progressStatus = _progressStatus.ChangeMessage(message).ChangePercentComplete(e.ProgressPercentage);
            _progressMonitor.UpdateProgress(_progressStatus);
            if (_progressMonitor.IsCanceled)
                _webClient.CancelAsync();
        }

        private void webClient_UploadFileCompleted(object sender, UploadFileCompletedEventArgs e)
        {
            lock (this)
            {
                Monitor.PulseAll(this);
            }
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
}
