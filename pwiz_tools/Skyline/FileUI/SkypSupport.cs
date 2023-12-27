using System;
using System.Collections.Generic;
using System.Net;
using System.Windows.Forms;
using pwiz.PanoramaClient;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using Thread = System.Threading.Thread;

namespace pwiz.Skyline.FileUI
{
    public class SkypSupport
    {
        private SkylineWindow _skyline;

        public DownloadClientCreator DownloadClientCreator { private get; set; }

        public SkypSupport(SkylineWindow skyline)
        {
            _skyline = skyline;
            DownloadClientCreator = new DownloadClientCreator();
        }

        public bool Open(string skypPath, IEnumerable<Server> servers, FormEx parentWindow = null)
        {
            Assume.IsFalse(string.IsNullOrEmpty(skypPath));
            
            SkypFile skyp;

            try
            {
                skyp = SkypFile.Create(skypPath!, servers); // Read the skyp file
            }
            catch (Exception e)
            {
                var message = TextUtil.LineSeparate(Resources.SkypSupport_Open_Failure_opening_skyp_file_, e.Message);
                MessageDlg.ShowWithException(parentWindow ?? _skyline, message, e);
                return false;
            }

            try
            {
                using (var longWaitDlg = new LongWaitDlg())
                {
                    longWaitDlg.Text = Resources.SkypSupport_Open_Downloading_Skyline_Document_Archive;
                    var progressStaus = longWaitDlg.PerformWork(parentWindow ?? _skyline, 1000, progressMonitor => Download(skyp, progressMonitor));
                    if (longWaitDlg.IsCanceled)
                        return false;
                    
                    if (progressStaus.IsError)
                    {
                        var exception = progressStaus.ErrorException;
                        var skypEx = exception as SkypDownloadException;
                        if (skypEx == null)
                        {
                            ShowDownloadError(parentWindow, skyp, exception);
                            return false;
                        }
                        
                        if (skypEx.Unauthorized())
                        {
                            return skyp.ServerMatch == null
                                ? AddServerAndOpen(skyp, skypEx.Message, parentWindow)  // Server not saved in Skyline. Offer to add the server.
                                : EditServerAndOpen(skyp, skypEx.Message, parentWindow); // Server saved in Skyline but credentials are invalid. Offer to edit server credentials.
                        }
                        else if (skypEx.Forbidden() && skyp.UsernameMismatch())
                        {
                            // Server is saved in Skyline but the user in the saved credentials does not have enough permissions. 
                            // The downloading user in the skyp file is different from saved user.  Offer to edit server credentials. 
                            return EditServerAndOpen(skyp, skypEx.Message, parentWindow);
                        }
                        
                        MessageDlg.ShowWithException(parentWindow ?? _skyline, skypEx.Message, skypEx);
                        return false;
                    }
                }
                
                return _skyline.OpenSharedFile(skyp.DownloadPath);
            }
            catch (Exception e)
            {
                ShowDownloadError(parentWindow, skyp, e);
                return false;
            }
        }

        private void ShowDownloadError(FormEx parentWindow, SkypFile skyp, Exception exception)
        {
            var message = TextUtil.LineSeparate(
                string.Format(
                    Resources
                        .SkypSupport_ShowDownloadError_There_was_an_error_downloading_the_Skyline_document__0__from__1__,
                    skyp.GetSkylineDocName(), skyp.GetDocUrlNoName()),
                exception.Message);
            MessageDlg.ShowWithException(parentWindow ?? _skyline, message, exception);
        }

        private bool AddServerAndOpen(SkypFile skyp, string message, FormEx parentWindow)
        {
            using (var alertDlg = new AlertDlg(message, MessageBoxButtons.OKCancel))
            {
                if (alertDlg.ShowDialog(parentWindow ?? _skyline) == DialogResult.OK)
                {
                    var allServers = Settings.Default.ServerList;
                    // We did not find a matching server saved in Skyline so create a Server object from the sky.zip WebDAV URL
                    // and the downloading user's username from the skyp file.
                    var skylineDocServer = skyp.GetSkylineDocServer();
                    var newServer = allServers.EditItem(parentWindow, skylineDocServer, allServers, false);
                    if (newServer == null)
                        return false;
                    allServers.Add(newServer); // server should not have the same Uri as an existing server in the list. EditServerDlg takes care of that.

                    return Open(skyp.SkypPath, allServers, parentWindow);
                }

                return false;
            }
        }

        private bool EditServerAndOpen(SkypFile skyp, string message, FormEx parentWindow)
        {
            using (var alertDlg = new AlertDlg(message, MessageBoxButtons.OKCancel))
            {
                alertDlg.ShowDialog(parentWindow ?? _skyline);
                if (alertDlg.DialogResult == DialogResult.OK)
                {
                    var allServers = Settings.Default.ServerList;
                    var skypServerMatch = skyp.ServerMatch;

                    var editedServer = allServers.EditCredentials(parentWindow, skypServerMatch, allServers,
                        skyp.DownloadingUser ?? skypServerMatch.Username, // Use the downloading username from the skyp file, if present
                        string.Empty);
                    
                    if (editedServer == null)
                        return false;

                    var idx = allServers.IndexOf(skypServerMatch);
                    allServers[idx] = editedServer;
                    
                    return Open(skyp.SkypPath, allServers, parentWindow);
                }

                return false;
            }
        }


        private void Download(SkypFile skyp, IProgressMonitor progressMonitor)
        {
            var progressStatus =
                new ProgressStatus(string.Format(Resources.SkypSupport_Download_Downloading__0__from__1_, skyp.GetSkylineDocName(), skyp.GetDocUrlNoName()));
            progressMonitor.UpdateProgress(progressStatus);

            var downloadClient = DownloadClientCreator.Create(progressMonitor, progressStatus);

            downloadClient.Download(skyp);

            if (progressMonitor.IsCanceled || downloadClient.IsError)
            {
                FileEx.SafeDelete(skyp.DownloadPath, true);
            }
        }
    }

    public class SkypDownloadException : Exception
    {
        public HttpStatusCode? StatusCode { get; }

        public SkypDownloadException(string message, HttpStatusCode? statusCode, Exception e) : base(message, e)
        {
            StatusCode = statusCode;
        }

        public bool Unauthorized()
        {
            return Unauthorized(StatusCode);
        }

        public static bool Unauthorized(HttpStatusCode? statusCode)
        {
            return statusCode is HttpStatusCode.Unauthorized; // 401 -  No credentials provided or invalid credentials
        }

        public bool Forbidden()
        {
            return Forbidden(StatusCode);
        }

        public static bool Forbidden(HttpStatusCode? statusCode)
        {
            return statusCode is HttpStatusCode.Forbidden; // 403 - Valid credentials but not enough permissions
        }

        public static SkypDownloadException Create(SkypFile skyp, Exception e)
        {
            var statusCode = GetErrorStatusCode(e);
            var message = GetMessage(skyp, e, statusCode);
            return new SkypDownloadException(message, statusCode, e);
        }

        private static HttpStatusCode? GetErrorStatusCode(Exception e)
        {
            var webException = e as WebException;
            if (webException != null)
            {
                if (webException.Status == WebExceptionStatus.ProtocolError)
                {
                    var response = webException.Response as HttpWebResponse;
                    if (response != null)
                    {
                        return response.StatusCode;
                    }
                }
            }

            return null;
        }

        public static string GetMessage(SkypFile skyp, Exception ex, HttpStatusCode? statusCode)
        {
            var message =
                string.Format(
                    Resources.SkypSupport_ShowDownloadError_There_was_an_error_downloading_the_Skyline_document__0__from__1__,
                    skyp.GetSkylineDocName(), skyp.GetDocUrlNoName());

            if (ex != null)
            {
                var exceptionMsg = ex.Message;
                message = TextUtil.LineSeparate(message, exceptionMsg);
            }
           
            var serverName = skyp.GetServerName();

            if (Unauthorized(statusCode)) // 401 -  No credentials provided or invalid credentials
            {
                if (skyp.ServerMatch == null)
                {
                    message = BuildMessage(message,
                        string.Format(
                            Resources
                                .SkypDownloadException_GetMessage_Would_you_like_to_add__0__as_a_Panorama_server_in_Skyline_,
                            serverName));
                }
                else if (skyp.UsernameMismatch())
                {
                    // User that downloaded the skyp file is not the same as the username in the saved server credentials.
                    message = BuildInvalidCredsMessage(message, serverName,
                        string.Format(
                            Resources
                                .SkypDownloadException_GetMessage_The_skyp_file_was_downloaded_by_the_user__0___Credentials_saved_in_Skyline_for_this_server_are_for_the_user__1__,
                            skyp.DownloadingUser, skyp.ServerMatch.Username));
                }
                else
                {
                    // The skyp file either does not have a DownloadingUser, or the username in the skyp is the same as the username in the saved server credentials.
                    message = BuildInvalidCredsMessage(message, serverName);
                }
            }
            else if (Forbidden(statusCode)) // 403 - Valid credentials but not enough permissions
            {
                if (skyp.UsernameMismatch() && skyp.ServerMatch != null)
                {
                    message = BuildMessage(true, message,
                        string.Format(
                            Resources.SkypDownloadException_GetMessage_Credentials_saved_in_Skyline_for_the_Panorama_server__0__are_for_the_user__1___This_user_does_not_have_permissions_to_download_the_file__The_skyp_file_was_downloaded_by__2__,
                            serverName, skyp.ServerMatch.Username, skyp.DownloadingUser));
                }
                else
                {
                    message = BuildMessage(message, string.Format(Resources.SkypSupport_Download_You_do_not_have_permissions_to_download_this_file_from__0__,serverName));
                }
            }

            return message;
        }

        private static string BuildMessage(string mainMessage, params string[] otherLines)
        {
            return BuildMessage(false, mainMessage, otherLines);
        }

        private static string BuildInvalidCredsMessage(string mainMessage, string serverName, params string[] otherLines)
        {
            var invalidCredsTxt = string.Format(
                Resources
                    .SkypDownloadException_GetMessage_Credentials_saved_in_Skyline_for_the_Panorama_server__0__are_invalid_,
                serverName);
            var otherMessages = new List<string> { invalidCredsTxt };
            otherMessages.AddRange(otherLines);
            return BuildMessage(true, mainMessage, otherMessages.ToArray());
        }

        private static string BuildMessage(bool addUpdateCredentialsText, string mainMessage, params string[] otherLines)
        {
            var allMessages = new List<string> { mainMessage };

            if (otherLines.Length > 0)
            {
                allMessages.Add(string.Empty);
                allMessages.AddRange(otherLines);
            }

            if (addUpdateCredentialsText)
            {
                allMessages.Add(string.Empty);
                allMessages.Add(Resources.SkypDownloadException_GetMessage_Would_you_like_to_update_the_credentials_);
            }

            return TextUtil.LineSeparate(allMessages);
        }
    }

    public class WebDownloadClient : IDownloadClient
    {
        private IProgressMonitor ProgressMonitor { get; }
        private IProgressStatus ProgressStatus { get; set; }

        public bool IsCancelled => ProgressMonitor != null && ProgressMonitor.IsCanceled;
        public bool IsError => ProgressStatus != null && ProgressStatus.IsError;
        public Exception Error => ProgressStatus?.ErrorException;

        public WebDownloadClient(IProgressMonitor progressMonitor, IProgressStatus progressStatus)
        {
            ProgressMonitor = progressMonitor;
            ProgressStatus = progressStatus;
        }

        public void Download(SkypFile skyp)
        {
            using (var wc = new UTF8WebClient())
            {
                if (skyp.HasCredentials())
                {
                    wc.Headers.Add(HttpRequestHeader.Authorization, PanoramaServer.GetBasicAuthHeader(skyp.ServerMatch.Username, skyp.ServerMatch.Password));
                }

                wc.DownloadProgressChanged += (s,e) =>
                {
                    // The Content-Length header is not set in the response from PanoramaWeb, so the ProgressPercentage remains 0
                    // during the download. If the skyp includes the file size, use that to calculate progress percentage.
                    var progressPercent = e.ProgressPercentage > 0 ? e.ProgressPercentage : -1;
                    
                    if (progressPercent == -1 && skyp.HasSize())
                    {
                        progressPercent = Math.Max(0, Math.Min(100, (int)(e.BytesReceived * 100 / skyp.Size)));
                    }

                    var downloaded = e.BytesReceived;
                    var message = TextUtil.LineSeparate(
                        string.Format(Resources.SkypSupport_Download_Downloading__0__from__1_, skyp.GetSkylineDocName(), skyp.GetDocUrlNoName()),
                        string.Empty,
                        GetDownloadedSize(downloaded, skyp.HasSize() ? (long)skyp.Size : 0));
                    ProgressStatus = ProgressStatus.ChangeMessage(message);
                    ProgressMonitor.UpdateProgress(ProgressStatus = ProgressStatus.ChangePercentComplete(progressPercent));
                };

                var downloadComplete = false;
                wc.DownloadFileCompleted += (s, e) =>
                {
                    if (e.Error != null && !ProgressMonitor.IsCanceled)
                    {
                        ProgressMonitor.UpdateProgress(ProgressStatus = ProgressStatus.ChangeErrorException(SkypDownloadException.Create(skyp, e.Error)));
                    }

                    downloadComplete = true;
                };

                wc.DownloadFileAsync(skyp.SkylineDocUri, skyp.DownloadPath);

                while (!downloadComplete)
                {
                   if (ProgressMonitor.IsCanceled)
                    {
                        wc.CancelAsync();
                    }

                    Thread.Sleep(100);
                }
            }
        }

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

    public interface IDownloadClient
    {
        void Download(SkypFile skyp);

        bool IsCancelled { get; }
        bool IsError { get; }
        Exception Error { get; }
    }

    public class DownloadClientCreator
    {
        public virtual IDownloadClient Create(IProgressMonitor progressMonitor, IProgressStatus progressStatus)
        {
            return new WebDownloadClient(progressMonitor, progressStatus);
        }
    }
}