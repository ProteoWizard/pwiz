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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Newtonsoft.Json.Linq;
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

        private CacheFormatVersion GetSupportedSkydVersion()
        {
            var serverVersionsJson = PanoramaClient.SupportedVersionsJson();
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

        public ShareType DecideShareTypeVersion(SrmDocument document, ShareType shareType)
        {
            var cacheVersion = GetDocumentCacheVersion(document);

            if (!cacheVersion.HasValue)
            {
                // The document may not have any chromatogram data.
                return shareType;
            }

            var supportedSkylineVersion = GetSupportedVersionForCacheFormat(cacheVersion);
            CacheFormatVersion supportedVersion = supportedSkylineVersion.CacheFormatVersion;
            if (supportedVersion >= cacheVersion.Value)
            {
                return shareType;
            }
            
            return shareType.ChangeSkylineVersion(supportedSkylineVersion);
        }

        private SkylineVersion GetSupportedVersionForCacheFormat(CacheFormatVersion? cacheVersion)
        {
            var skydVersion = GetSupportedSkydVersion();
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

        public ShareType GetShareType(SrmDocument document,
            string documentFilePath, DocumentFormat? fileFormatOnDisk, Control parent, ref bool cancelled)
        {
            var cacheVersion = GetDocumentCacheVersion(document);
            var supportedSkylineVersion = GetSupportedVersionForCacheFormat(cacheVersion);
            
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
                var isCanceled = false;
                using (var waitDlg = new LongWaitDlg())
                {
                    waitDlg.Text = UtilResources.PublishDocumentDlg_UploadSharedZipFile_Uploading_File;
                    waitDlg.PerformWork(parent, 1000, longWaitBroker =>
                    {
                        result = PanoramaClient.SendZipFile(folderPath,
                            zipFilePath, longWaitBroker);
                        if (longWaitBroker.IsCanceled)
                            isCanceled = true;
                    });
                }
                if (!isCanceled) // if user not canceled 
                {
                    _uploadedDocumentUri = result;
                    var message = UtilResources.AbstractPanoramaPublishClient_UploadSharedZipFile_Upload_succeeded__would_you_like_to_view_the_file_in_Panorama_;
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
                    string message;
                    if (panoramaEx.JobCancelled)
                    {
                        message = UtilResources
                            .AbstractPanoramaPublishClient_UploadSharedZipFile_Document_import_was_cancelled_on_the_server__Would_you_like_to_go_to_Panorama_;
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(panoramaEx.Error))
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
                    }

                    if (MultiButtonMsgDlg.Show(parent, message, MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false)
                        == DialogResult.Yes)
                        Process.Start(panoramaEx.JobUrl.ToString());
                }
                
                else
                {
                    MessageDlg.ShowWithException(parent, x.Message, x);
                }

                return;
            }

            // Change PanoramaUrl setting to the successful url used
            var uriString = PanoramaClient.ServerUri + folderPath;
            uriString = Uri.EscapeUriString(uriString);
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
