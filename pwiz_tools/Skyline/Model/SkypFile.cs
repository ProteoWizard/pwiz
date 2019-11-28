using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using Path = System.IO.Path;

namespace pwiz.Skyline.Model
{
    public class SkypFile
    {
        public const string EXT = ".skyp";

        public static string FILTER_SKYP => TextUtil.FileDialogFilter(Resources.SkypFile_FILTER_SKYP_Skyline_Document_Pointer, EXT);

        public string SkypPath { get; private set; }
        public Uri SkylineDocUri { get; private set; }
        public Server Server { get; private set; }
        public string DownloadPath { get; private set; }
        
        public static SkypFile Create(string skypPath, IEnumerable<Server> servers)
        {
            if (string.IsNullOrEmpty(skypPath))
            {
                return null;
            }
            var skyp = new SkypFile
            {
                SkypPath = skypPath,
                SkylineDocUri = GetSkyFileUrl(skypPath),
            };
            skyp.Server = servers?.FirstOrDefault(server => server.URI.Host.Equals(skyp.SkylineDocUri.Host));

            var downloadDir = Path.GetDirectoryName(skyp.SkypPath);
            skyp.DownloadPath = GetNonExistentPath(downloadDir ?? string.Empty, skyp.GetSkylineDocName());

            return skyp;
        }
    
        private static Uri GetSkyFileUrl(string skypPath)
        {
            using (var reader = new StreamReader(skypPath))
            {
                return GetSkyFileUrl(reader);
            }
        }

        public static Uri GetSkyFileUrl(TextReader reader)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    var urlString = line;
                    if (!urlString.EndsWith(SrmDocumentSharing.EXT))
                    {
                        // This is not a shared Skyline zip.
                        throw new InvalidDataException(string.Format(
                            Resources
                                .SkypFile_GetSkyFileUrl_Expected_the_URL_of_a_shared_Skyline_document_archive___0___in_the_skyp_file__Found__1__instead_,
                            SrmDocumentSharing.EXT_SKY_ZIP, urlString));
                    }

                    var validUrl = Uri.TryCreate(urlString, UriKind.Absolute, out var skyFileUri)
                                   && (skyFileUri.Scheme == Uri.UriSchemeHttp || skyFileUri.Scheme == Uri.UriSchemeHttps);
                    if (!validUrl)
                    {
                        throw new InvalidDataException(string.Format(
                            Resources.SkypFile_GetSkyFileUrl__0__is_not_a_valid_URL_on_a_Panorama_server_, urlString));
                    }

                    return skyFileUri;
                }
            }

            throw new InvalidDataException(string.Format(
                Resources.SkypFile_GetSkyFileUrl_File_does_not_contain_the_URL_of_a_shared_Skyline_archive_file___0___on_a_Panorama_server_,
                SrmDocumentSharing.EXT_SKY_ZIP));
        }

        public string GetSkylineDocName()
        {
            return SkylineDocUri != null ? Path.GetFileName(SkylineDocUri.AbsolutePath) : null;
        }

        public static string GetNonExistentPath(string dir, string sharedSkyFile)
        {
            if (string.IsNullOrEmpty(sharedSkyFile))
            {
                throw new ArgumentException(Resources
                    .SkypFile_GetNonExistentPath_Name_of_shared_Skyline_archive_cannot_be_null_or_empty_);
            }

            var count = 1;
            var isSkyZip = PathEx.HasExtension(sharedSkyFile, SrmDocumentSharing.EXT_SKY_ZIP);
            var ext = isSkyZip ? SrmDocumentSharing.EXT_SKY_ZIP : SrmDocumentSharing.EXT;
            var fileNameNoExt = isSkyZip
                ? sharedSkyFile.Substring(0, sharedSkyFile.Length - SrmDocumentSharing.EXT_SKY_ZIP.Length)
                : Path.GetFileNameWithoutExtension(sharedSkyFile);

            var path = Path.Combine(dir, sharedSkyFile);
            while (File.Exists(path) || Directory.Exists(Path.Combine(dir, SrmDocumentSharing.ExtractDir(path))))
            {
                // Add a suffix if a file with the given name already exists OR
                // a directory that would be created when opening this file exists.
                sharedSkyFile = fileNameNoExt + @"(" + count + @")" + ext;
                path = Path.Combine(dir, sharedSkyFile);
                count++;
            }
            return Path.Combine(dir, sharedSkyFile);
        }
    }

    public class SkypSupport
    {
        private SkylineWindow _skyline;

        public IDownloadClient DownloadClient { get; set; }

        public const string ERROR401 = "(401) Unauthorized";
        public const string ERROR403 = "(403) Forbidden";

        public SkypSupport(SkylineWindow skyline)
        {
            _skyline = skyline;
        }

        public bool Open(string skypPath, IEnumerable<Server> servers, FormEx parentWindow = null)
        {
            try
            {
                var skyp = SkypFile.Create(skypPath, servers);
                
                using (var longWaitDlg = new LongWaitDlg
                {
                    Text = Resources.SkypSupport_Open_Downloading_Skyline_Document_Archive,
                })
                {
                    longWaitDlg.PerformWork(parentWindow ?? _skyline, 1000, progressMonitor => Download(skyp, progressMonitor, parentWindow));
                    if (longWaitDlg.IsCanceled)
                        return false;
                }
                return _skyline.OpenSharedFile(skyp.DownloadPath);
            }
            catch (Exception e)
            {
                var message = TextUtil.LineSeparate(Resources.SkypSupport_Open_Failure_opening_skyp_file_, e.Message);
                MessageDlg.ShowWithException(parentWindow ?? _skyline, message, e);
                return false;
            }
        }


        private void Download(SkypFile skyp, IProgressMonitor progressMonitor, FormEx parentWindow = null)
        {
            var progressStatus =
                new ProgressStatus(string.Format(Resources.SkypSupport_Download_Downloading__0_, skyp.SkylineDocUri));
            progressMonitor.UpdateProgress(progressStatus);

            if (DownloadClient == null)
            {
                DownloadClient = new WebDownloadClient(progressMonitor, progressStatus);
            }

            DownloadClient.Download(skyp.SkylineDocUri, skyp.DownloadPath, skyp.Server?.Username, skyp.Server?.Password);

            if (progressMonitor.IsCanceled || DownloadClient.IsError)
            {
                FileEx.SafeDelete(skyp.DownloadPath, true);
            }
            if (DownloadClient.IsError)
            {
                var message =
                    string.Format(
                        Resources
                            .SkypSupport_Download_There_was_an_error_downloading_the_Skyline_document_specified_in_the_skyp_file___0__,
                        skyp.SkylineDocUri);

                if (DownloadClient.Error != null)
                {
                    var exceptionMsg = DownloadClient.Error.Message;
                    message = TextUtil.LineSeparate(message, exceptionMsg);

                    if (exceptionMsg.Contains(ERROR401))
                    {
                        message = TextUtil.LineSeparate(message,
                            string.Format(
                                Resources
                                    .SkypSupport_Download_You_may_have_to_add__0__as_a_Panorama_server_from_the_Tools___Options_menu_in_Skyline_,
                                skyp.SkylineDocUri.Host));
                    }
                    else if (exceptionMsg.Contains(ERROR403))
                    {
                        message = TextUtil.LineSeparate(message,
                            string.Format(
                                Resources.SkypSupport_Download_You_do_not_have_permissions_to_download_this_file_from__0__,
                                skyp.SkylineDocUri.Host));
                    }
                }

                throw new Exception(message, DownloadClient.Error);
            }
        }     
    }

    public class WebDownloadClient : IDownloadClient
    {
        private IProgressMonitor ProgressMonitor { get; }
        private IProgressStatus ProgressStatus { get; set; }
        private bool DownloadComplete { get; set; }

        public bool IsCancelled => ProgressMonitor != null && ProgressMonitor.IsCanceled;
        public bool IsError => ProgressStatus != null && ProgressStatus.IsError;
        public Exception Error => ProgressStatus?.ErrorException;

        public WebDownloadClient(IProgressMonitor progressMonitor, ProgressStatus progressStatus)
        {
            ProgressMonitor = progressMonitor;
            ProgressStatus = progressStatus;
        }

        public void Download(Uri remoteUri, string downloadPath, string username, string password)
        {
            using (var wc = new UTF8WebClient())
            {
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    wc.Headers.Add(HttpRequestHeader.Authorization, Server.GetBasicAuthHeader(username, password));
                }

                wc.DownloadProgressChanged += wc_DownloadProgressChanged;
                wc.DownloadFileCompleted += wc_DownloadFileCompleted;

                wc.DownloadFileAsync(remoteUri, downloadPath);

                while (!DownloadComplete)
                {
                    if (ProgressMonitor.IsCanceled)
                    {
                        wc.CancelAsync();
                    }
                }
            }
        }

        private void wc_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Error != null && !ProgressMonitor.IsCanceled)
            {
                ProgressMonitor.UpdateProgress(ProgressStatus = ProgressStatus.ChangeErrorException(e.Error));
            }

            DownloadComplete = true;
        }

        private void wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            ProgressMonitor.UpdateProgress(ProgressStatus = ProgressStatus.ChangePercentComplete(e.ProgressPercentage));
        }
    }

    public interface IDownloadClient
    {
        void Download(Uri remoteUri, string downloadPath, string username, string password);

        bool IsCancelled { get; }
        bool IsError { get; }
        Exception Error { get; }
    }
}