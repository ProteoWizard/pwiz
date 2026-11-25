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
using System.Windows.Forms;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Newtonsoft.Json.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.PanoramaClient;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Properties;
using TextUtil = pwiz.Skyline.Util.Extensions.TextUtil;

namespace pwiz.Skyline.Util
{
    public interface IPanoramaPublishClient
    {
        void UploadSharedZipFile(Control parent, string zipFilePath, string folderPath);
        
        ShareType GetShareType(SrmDocument document, string documentFilePath,
            DocumentFormat? fileFormatOnDisk, Control parent, ref bool cancelled);

        Uri UploadedDocumentUri { get; }

        IPanoramaClient PanoramaClient { get; }
    }

    public abstract class AbstractPanoramaPublishClient : IPanoramaPublishClient
    {
        private Uri _uploadedDocumentUri;

        public Uri UploadedDocumentUri
        {
            get { return _uploadedDocumentUri; }
        }

        public abstract IPanoramaClient PanoramaClient { get; }

        public ShareType DecideShareTypeVersion(SrmDocument document, ShareType shareType)
        {
            return new PanoramaShareVersions(PanoramaClient).DecideShareTypeVersion(document, shareType);
        }

        public ShareType GetShareType(SrmDocument document,
            string documentFilePath, DocumentFormat? fileFormatOnDisk, Control parent, ref bool cancelled)
        {
            var cacheVersion = PanoramaShareVersions.GetDocumentCacheVersion(document);
            var supportedSkylineVersion = new PanoramaShareVersions(PanoramaClient).GetSupportedVersionForCacheFormat(cacheVersion);
            
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

        public void UploadSharedZipFile(Control parent, string zipFilePath, string folderPath)
        {
            Uri result = null;
            try
            {
                using var waitDlg = new LongWaitDlg();
                waitDlg.Text = UtilResources.PublishDocumentDlg_UploadSharedZipFile_Uploading_File;
                IProgressStatus progressStatus = new ProgressStatus(SkylineResources.PanoramaPublishHelper_PublishDocToPanorama_Uploading_document_to_Panorama);
                var status = waitDlg.PerformWork(parent, 1000, progressMonitor =>
                {
                    result = PanoramaClient.SendZipFile(folderPath, zipFilePath, progressMonitor, progressStatus);
                });
                if (status.IsCanceled)
                    return;

                _uploadedDocumentUri = result;
                var message = UtilResources.AbstractPanoramaPublishClient_UploadSharedZipFile_Upload_succeeded__would_you_like_to_view_the_file_in_Panorama_;
                if (MultiButtonMsgDlg.Show(parent, message, MultiButtonMsgDlg.BUTTON_YES,
                        MultiButtonMsgDlg.BUTTON_NO, false)
                    == DialogResult.Yes)
                {
                    ProcessEx.OpenInShell(result.ToString());
                }
            }
            catch (Exception x)
            {
                if (ExceptionUtil.IsProgrammingDefect(x))
                    throw;

                if (x.InnerException is PanoramaImportErrorException panoramaEx)
                {
                    string message;
                    if (panoramaEx.JobCancelled)
                    {
                        message = UtilResources
                            .AbstractPanoramaPublishClient_UploadSharedZipFile_Document_import_was_cancelled_on_the_server__Would_you_like_to_go_to_Panorama_;
                    }
                    else if (string.IsNullOrWhiteSpace(panoramaEx.Error))
                    {
                        message = TextUtil.SpaceSeparate(
                            string.Format(UtilResources.AbstractPanoramaPublishClient_UploadSharedZipFile_An_import_error_occurred_on_the_Panorama_server__0__,
                                panoramaEx.ServerUrl),
                            UtilResources
                                .AbstractPanoramaPublishClient_UploadSharedZipFile_Would_you_like_to_go_to_Panorama_
                        );
                    }
                    else
                    {
                        message = TextUtil.LineSeparate(
                            string.Format(UtilResources.AbstractPanoramaPublishClient_UploadSharedZipFile_An_import_error_occurred_on_the_Panorama_server__0__,
                                panoramaEx.ServerUrl),
                            string.Format(Resources.Error___0_, panoramaEx.Error),
                            string.Empty,
                            UtilResources
                                .AbstractPanoramaPublishClient_UploadSharedZipFile_Would_you_like_to_go_to_Panorama_);
                    }

                    if (MultiButtonMsgDlg.Show(parent, message, MultiButtonMsgDlg.BUTTON_YES,
                            MultiButtonMsgDlg.BUTTON_NO, false)
                        == DialogResult.Yes)
                    {
                        ProcessEx.OpenInShell(panoramaEx.JobUrl.ToString());
                    }
                }
                
                else
                {
                    MessageDlg.ShowWithException(parent, x.Message, x);
                }

                return;
            }

            // Change PanoramaUrl setting to the successful url used
            var uriString = PanoramaClient.ServerUri + folderPath;
            // EscapeDataString is NOT the replacement here: this escapes a whole absolute URI, and
            // percent-encoding its "://" would make the IsWellFormedUriString check below fail.
            // Retiring this properly means composing the Uri instead of escaping a built string.
#pragma warning disable SYSLIB0013
            uriString = Uri.EscapeUriString(uriString);
#pragma warning restore SYSLIB0013
            var window = parent as SkylineWindow;
            if (window != null && Uri.IsWellFormedUriString(uriString, UriKind.Absolute)) // cant do Uri.isWellFormed because of port and ip
            {
                window.ChangeDocPanoramaUri(new Uri(uriString));
            }
        }
    }

    public class WebPanoramaPublishClient : AbstractPanoramaPublishClient
    {
        private WebPanoramaClient _panoramaClient;

        public WebPanoramaPublishClient(Uri serverUri, string username, string password)
        {
            _panoramaClient = new WebPanoramaClient(serverUri, username, password);
        }

        public override IPanoramaClient PanoramaClient => _panoramaClient;

        public static IPanoramaPublishClient Create(PanoramaServer server)
        {
            return new WebPanoramaPublishClient(server.URI, server.Username, server.Password);
        }
    }
}
