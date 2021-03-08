﻿/*
 * Original author: Vagisha Sharma <vsharma .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using AutoQC.Properties;
using SharedBatch;

namespace AutoQC
{
    
    [XmlRoot("panorama_settings")]
    public class PanoramaSettings
    {
        public static bool GetDefaultPublishToPanorama() { return false; }

        public readonly bool PublishToPanorama;
        public readonly string PanoramaServerUrl;
        public readonly string PanoramaUserEmail;
        public readonly string PanoramaPassword;
        public readonly string PanoramaFolder;

        public Uri PanoramaServerUri;

        public PanoramaSettings()
        {
            PublishToPanorama = false;
        }

        public PanoramaSettings(bool publishToPanorama, string panoramaServerUrl, string panoramaUserEmail, string panoramaPassword, string panoramaFolder, Uri panoramaServerUri = null)
        {
            PublishToPanorama = publishToPanorama;
            PanoramaServerUrl = panoramaServerUrl;
            PanoramaUserEmail = panoramaUserEmail;
            PanoramaPassword = panoramaPassword;
            PanoramaFolder = panoramaFolder;
            PanoramaServerUri = panoramaServerUri;

            if (!PublishToPanorama)
                return;

            if (PanoramaServerUri == null)
            {
                try
                {
                    PanoramaServerUri = new Uri(PanoramaUtil.ServerNameToUrl(PanoramaServerUrl));
                }
                catch (UriFormatException)
                {
                    ProgramLog.Error(Resources.PanoramaSettings_PanoramaSettings_Panorama_server_name_is_invalid__Please_enter_a_different_Panorama_server_name_);
                }
            }
        }

        public virtual bool IsSelected()
        {
            return PublishToPanorama;
        }



        public void ValidateSettings()
        {
            if (!PublishToPanorama)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(PanoramaServerUrl))
            {
                throw new ArgumentException(string.Format(Resources.PanoramaSettings_ValidateSettings_The__0__cannot_be_empty__Please_specify_a__0__, 
                    Resources.PanoramaSettings_ValidateSettings_Panorama_server_Url));
            }

            if (string.IsNullOrWhiteSpace(PanoramaUserEmail))
            {
                throw new ArgumentException(string.Format(Resources.PanoramaSettings_ValidateSettings_The__0__cannot_be_empty__Please_specify_a__0__, 
                    Resources.PanoramaSettings_ValidateSettings_Panorama_login_email));
            }
            if (string.IsNullOrWhiteSpace(PanoramaPassword))
            {
                throw new ArgumentException(string.Format(Resources.PanoramaSettings_ValidateSettings_The__0__cannot_be_empty__Please_specify_a__0__, 
                    Resources.PanoramaSettings_ValidateSettings_Panorama_user_password));
            }
            
            if (string.IsNullOrWhiteSpace(PanoramaFolder))
            {
                throw new ArgumentException(string.Format(Resources.PanoramaSettings_ValidateSettings_The__0__cannot_be_empty__Please_specify_a__0__, 
                    Resources.PanoramaSettings_ValidateSettings_folder_on_the_Panorama_server));
            }

            var panoramaClient = new WebPanoramaClient(PanoramaServerUri);
            try
            {
                PanoramaUtil.VerifyServerInformation(panoramaClient, PanoramaUserEmail, PanoramaPassword);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(ex.Message);
            }
            try
            {
                PanoramaUtil.VerifyFolder(panoramaClient,
                    new Server(PanoramaServerUri, PanoramaUserEmail,
                        PanoramaPassword),
                    PanoramaFolder);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(ex.Message);
            }
        }

        public virtual string SkylineRunnerArgs(ImportContext importContext, bool toPrint = false)
        {
            if (!IsSelected() || (importContext.ImportExisting && !importContext.ImportingLast()))
            {
                // Do not upload to Panorama if we are importing existing documents and this is not the 
                // last file being imported.
                return string.Empty;
            }

            var passwdArg = toPrint ? string.Empty : string.Format("--panorama-password=\"{0}\"", PanoramaPassword);
            var uploadArgs = string.Format(
                    " --panorama-server=\"{0}\" --panorama-folder=\"{1}\" --panorama-username=\"{2}\" {3}",
                    PanoramaServerUrl,
                    PanoramaFolder,
                    PanoramaUserEmail,
                    passwdArg);
            return uploadArgs;
        }

        public virtual ProcessInfo RunBefore(ImportContext importContext)
        {
            return null;
        }

        public virtual ProcessInfo RunAfter(ImportContext importContext)
        {
            return null;
        }

        // Changed DataProtectionScope from LocalMachine to CurrentUser
        // https://stackoverflow.com/questions/19164926/data-protection-api-scope-localmachine-currentuser
        public static string EncryptPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return string.Empty;
            }

            try
            {
                var encrypted = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(password), null,
                    DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encrypted);
            }
            catch (Exception e)
            {
                ProgramLog.Error("Error encrypting password. ", e);
  
            }
            return string.Empty;
        }

        public static string DecryptPassword(string encryptedPassword)
        {
            if (string.IsNullOrEmpty(encryptedPassword))
            {
                return string.Empty;
            }
            try
            {
                byte[] decrypted = ProtectedData.Unprotect(
                    Convert.FromBase64String(encryptedPassword), null,
                    DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception e)
            {              
                ProgramLog.Error("Error decrypting password. ", e);
            }
            return string.Empty;
        }

        #region Implementation of IXmlSerializable interface

        private enum Attr
        {
            publish_to_panorama,
            panorama_server_url,
            panorama_user_email,
            panorama_user_password,
            panorama_folder
        };

        public XmlSchema GetSchema()
        {
            return null;
        }

        public static PanoramaSettings ReadXml(XmlReader reader)
        {
            var publishToPanorama = reader.GetBoolAttribute(Attr.publish_to_panorama);
            var panoramaServerUrl = reader.GetAttribute(Attr.panorama_server_url);
            var panoramaUserEmail = reader.GetAttribute(Attr.panorama_user_email);
            var panoramaPassword = DecryptPassword(reader.GetAttribute(Attr.panorama_user_password));
            var panoramaFolder = reader.GetAttribute(Attr.panorama_folder);
            var panoramaServerUri = publishToPanorama ? new Uri(PanoramaUtil.ServerNameToUrl(panoramaServerUrl)) : null;
            return new PanoramaSettings(publishToPanorama, panoramaServerUrl, panoramaUserEmail, panoramaPassword, panoramaFolder, panoramaServerUri);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("panorama_settings");
            writer.WriteAttribute(Attr.publish_to_panorama, PublishToPanorama);
            writer.WriteAttributeIfString(Attr.panorama_server_url, PanoramaServerUrl);
            writer.WriteAttributeIfString(Attr.panorama_user_email, PanoramaUserEmail);
            writer.WriteAttributeIfString(Attr.panorama_user_password, EncryptPassword(PanoramaPassword));
            writer.WriteAttributeIfString(Attr.panorama_folder, PanoramaFolder);
            writer.WriteEndElement();
        }
        #endregion

        #region Equality members

        protected bool Equals(PanoramaSettings other)
        {
            if (!PublishToPanorama && PublishToPanorama == other.PublishToPanorama)
                return true;

            return PublishToPanorama == other.PublishToPanorama
                   && string.Equals(PanoramaServerUrl, other.PanoramaServerUrl)
                   && string.Equals(PanoramaUserEmail, other.PanoramaUserEmail)
                   && string.Equals(PanoramaPassword, other.PanoramaPassword)
                   && string.Equals(PanoramaFolder, other.PanoramaFolder);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PanoramaSettings) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = PublishToPanorama.GetHashCode();
                hashCode = (hashCode*397) ^ (PanoramaServerUrl != null ? PanoramaServerUrl.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (PanoramaUserEmail != null ? PanoramaUserEmail.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (PanoramaPassword != null ? PanoramaPassword.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (PanoramaFolder != null ? PanoramaFolder.GetHashCode() : 0);
                return hashCode;
            }
        }

        #endregion

        public override string ToString()
        {
            var sb = new StringBuilder();
            if (PublishToPanorama)
            {
                sb.Append("Panorama server URL: ").AppendLine(PanoramaServerUrl);
                sb.Append("Panorama user email: ").AppendLine(PanoramaUserEmail);
                sb.Append("Panorama folder: ").AppendLine(PanoramaFolder);
            }
            else
            {
                sb.Append("Not publishing to a Panorama server.");
            }
            return sb.ToString();
        }
    }

    public class PanoramaPinger
    {
        private readonly PanoramaSettings _panoramaSettings;
        private readonly Logger _logger;
        private short _status; //1 = success; 2 = fail
        private Timer _timer;

        public PanoramaPinger(PanoramaSettings panoramaSettings, Logger logger)
        {
            _panoramaSettings = panoramaSettings;
            _logger = logger;
        }

        public void PingPanoramaServer()
        {
            var panoramaServerUri = _panoramaSettings.PanoramaServerUri;

            if (!_panoramaSettings.PublishToPanorama || panoramaServerUri == null) return;

            var panoramaClient = new WebPanoramaClient(panoramaServerUri);
            try
            {
                var success = panoramaClient.PingPanorama(_panoramaSettings.PanoramaFolder,
                    _panoramaSettings.PanoramaUserEmail,
                    _panoramaSettings.PanoramaPassword
                     );

                if (success && _status != 1)
                {
                    _logger.Log(Resources.PanoramaPinger_PingPanoramaServer_Successfully_pinged_Panorama_server_);
                    _status = 1;
                }
                if (!success && _status != 2)
                {
//                        _logger.LogErrorToFile("Error pinging Panorama server.  Please confirm that " + panoramaServerUri +
//                                 " is running LabKey Server 16.1 or higher.");
                    _status = 2;
                }
            }
            catch (Exception ex)
            {
                if (_status != 2)
                {
                    _logger.LogException(ex, Resources.PanoramaPinger_PingPanoramaServer_Error_pinging_Panorama_server_ + panoramaServerUri);
                    _status = 2;
                }
            }
        }

        public void Init()
        {
            _timer = new Timer(e => { PingPanoramaServer(); });
            _timer.Change(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5)); // Ping Panorama every 5 minutes.
        }

        public void Stop()
        {
            _timer.Dispose();
            _status = 0;
        }
    }
}