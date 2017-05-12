/*
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

namespace AutoQC
{
    
    [XmlRoot("panorama_settings")]
    public class PanoramaSettings: IXmlSerializable, IConfigSettings
    {
        public static readonly byte[] entropy = Encoding.Unicode.GetBytes("Encrypt Panorama password");
        
        public bool PublishToPanorama { get; set; }
        public string PanoramaServerUrl { get; set; }
        public string PanoramaUserEmail { get; set; }
        public string PanoramaPassword { get; set; }
        public string PanoramaFolder { get; set; }

        public Uri PanoramaServerUri { get; private set; }

        public static PanoramaSettings GetDefault()
        {
            return new PanoramaSettings {PublishToPanorama = false};
        }

        public PanoramaSettings Clone()
        {
            return new PanoramaSettings
            {
                PublishToPanorama = PublishToPanorama,
                PanoramaServerUrl = PanoramaServerUrl,
                PanoramaFolder = PanoramaFolder
            };
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
                throw new ArgumentException("Please specify a Panorama server URL.");
            }
            try
            {
                PanoramaServerUri = new Uri(PanoramaUtil.ServerNameToUrl(PanoramaServerUrl));
            }
            catch (UriFormatException)
            {
                throw new ArgumentException("Panorama server name is invalid.");
            }

            if (string.IsNullOrWhiteSpace(PanoramaUserEmail))
            {
                throw new ArgumentException("Please specify a Panorama login email.");
            }
            if (string.IsNullOrWhiteSpace(PanoramaPassword))
            {
                throw new ArgumentException("Please specify a Panorama user password.");
            }
            
            if (string.IsNullOrWhiteSpace(PanoramaFolder))
            {
                throw new ArgumentException("Please specify a folder on the Panorama server.");
            }

            // Verify that we can connect to the given Panorama server with the user's credentials.
            var panoramaClient = new WebPanoramaClient(PanoramaServerUri);
            try
            {
                PanoramaUtil.VerifyServerInformation(panoramaClient, PanoramaServerUri, PanoramaUserEmail, PanoramaPassword);
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
                // Do not upload to Panorama if this we are importing existing documents and this is not the 
                // last file being imported.
                return string.Empty;
            }

            var passwdArg = toPrint ? "" : string.Format("--panorama-password=\"{0}\"", PanoramaPassword);
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

        public static string EncryptPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return string.Empty;
            }

            try
            {
                var encrypted = ProtectedData.Protect(
                    Encoding.Unicode.GetBytes(password), entropy,
                    DataProtectionScope.LocalMachine);
                return Convert.ToBase64String(encrypted);
            }
            catch (Exception e)
            {
                Program.LogError("Error encrypting password. " + e.Message);
  
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
                    Convert.FromBase64String(encryptedPassword), entropy,
                    DataProtectionScope.LocalMachine);
                return Encoding.Unicode.GetString(decrypted);
            }
            catch (Exception e)
            {
                Program.LogError("Error decrypting password. " + e.Message);      
            }
            return string.Empty;
        }

        #region Implementation of IXmlSerializable interface

        private enum ATTR
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

        public void ReadXml(XmlReader reader)
        {
            PublishToPanorama = reader.GetBoolAttribute(ATTR.publish_to_panorama);
            PanoramaServerUrl = reader.GetAttribute(ATTR.panorama_server_url);
            PanoramaUserEmail = reader.GetAttribute(ATTR.panorama_user_email);
            PanoramaPassword = DecryptPassword(reader.GetAttribute(ATTR.panorama_user_password));
            PanoramaFolder = reader.GetAttribute(ATTR.panorama_folder);

            if (PublishToPanorama)
            {
                PanoramaServerUri = new Uri(PanoramaUtil.ServerNameToUrl(PanoramaServerUrl));
            }
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("panorama_settings");
            if (PublishToPanorama)
            {
                writer.WriteAttribute(ATTR.publish_to_panorama, PublishToPanorama);
                writer.WriteAttributeIfString(ATTR.panorama_server_url, PanoramaServerUrl);
                writer.WriteAttributeIfString(ATTR.panorama_user_email, PanoramaUserEmail);
                writer.WriteAttributeIfString(ATTR.panorama_user_password, EncryptPassword(PanoramaPassword));
                writer.WriteAttributeIfString(ATTR.panorama_folder, PanoramaFolder);
            }
            writer.WriteEndElement();
        }
        #endregion

        #region Equality members

        protected bool Equals(PanoramaSettings other)
        {
            var equal = PublishToPanorama == other.PublishToPanorama 
                && string.Equals(PanoramaServerUrl, other.PanoramaServerUrl) 
                && string.Equals(PanoramaUserEmail, other.PanoramaUserEmail) 
                && string.Equals(PanoramaPassword, other.PanoramaPassword) 
                && string.Equals(PanoramaFolder, other.PanoramaFolder);

            return equal;
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
        private readonly IAutoQcLogger _logger;
        private short _status; //1 = success; 2 = fail
        private Timer _timer;

        public PanoramaPinger(PanoramaSettings panoramaSettings, IAutoQcLogger logger)
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
                    _logger.Log("Successfully pinged Panorama server.");
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
                    _logger.LogError("Error pinging Panorama server " + panoramaServerUri);
                    _logger.LogException(ex);
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