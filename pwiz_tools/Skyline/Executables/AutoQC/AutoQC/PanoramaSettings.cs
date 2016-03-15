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
using AutoQC.Properties;

namespace AutoQC
{
    public class PanoramaSettings
    {
        
        public bool PublishToPanorama { get; set; }
        public string PanoramaServerUrl { get; set; }
        public string PanoramaUserEmail { get; set; }
        public string PanoramaPassword { get; set; }
        public string PanoramaFolder { get; set; }

        public static PanoramaSettings InitializeFromDefaults()
        {
            var settings = new PanoramaSettings()
            {
                PanoramaServerUrl = Settings.Default.PanoramaUrl,
                PanoramaUserEmail = Settings.Default.PanoramaUserEmail,
                PanoramaPassword = Settings.Default.PanoramaPassword,
                PanoramaFolder = Settings.Default.PanoramaFolder,
                PublishToPanorama = Settings.Default.PublishToPanorama,
            };

            return settings;
        }

        public void Save()
        {
            Settings.Default.PanoramaUrl = PanoramaServerUrl;
            Settings.Default.PanoramaUserEmail = PanoramaUserEmail;
            Settings.Default.PanoramaPassword = PanoramaPassword;
            Settings.Default.PanoramaFolder = PanoramaFolder;
            Settings.Default.PublishToPanorama = PublishToPanorama;
        }
 
    }
    public class PanoramaSettingsTab: SettingsTab
    {
        public static readonly byte[] entropy = Encoding.Unicode.GetBytes("Encrypt Panorama password");

        public PanoramaSettings Settings { get; set; }

        public Uri PanoramaServerUri { get; private set; }

        public PanoramaSettingsTab(IAppControl appControl, IAutoQCLogger logger)
            : base(appControl, logger)
        {
            Settings = new PanoramaSettings();
        }

        public override void InitializeFromDefaultSettings()
        {
            Settings = PanoramaSettings.InitializeFromDefaults();
            _appControl.SetUIPanoramaSettings(Settings);
            if (!Settings.PublishToPanorama)
            {
                _appControl.DisablePanoramaSettings();
            }
        }

        public override bool IsSelected()
        {
            return Settings.PublishToPanorama;
        }

        public override bool ValidateSettings()
        {
            var panoramaSettingsUI = _appControl.GetUIPanoramaSettings();

            if (!panoramaSettingsUI.PublishToPanorama)
            {
                LogOutput("Will NOT publish Skyline documents to Panorama.");
                Settings.PublishToPanorama = false;
                return true;
            }

            LogOutput("Validating Panorama settings...");
            var error = false;
            var panoramaUrl = panoramaSettingsUI.PanoramaServerUrl;
            Uri serverUri = null;

            if (string.IsNullOrWhiteSpace(panoramaUrl))
            {
                LogErrorOutput("Please specify a Panorama server URL.");
                error = true;
            }
            else
            {
                try
                {
                    serverUri = new Uri(PanoramaUtil.ServerNameToUrl(panoramaUrl));
                }
                catch (UriFormatException)
                {
                    LogError("Panorama server name is invalid.");
                    return false;
                }  
            }
            
            var panoramaEmail = panoramaSettingsUI.PanoramaUserEmail;
            var panoramaPasswd = panoramaSettingsUI.PanoramaPassword;
            var panoramaFolder = panoramaSettingsUI.PanoramaFolder;

            if (string.IsNullOrWhiteSpace(panoramaEmail))
            {
                LogErrorOutput("Please specify a Panorama login name.");
                error = true;
            }
            if (string.IsNullOrWhiteSpace(panoramaPasswd))
            {
                LogErrorOutput("Please specify a Panorama user password.");
                error = true;
            }
            else
            {
                if (!panoramaPasswd.Equals(Settings.PanoramaPassword))
                {
                    // Encrypt the password
                    try
                    {
                        panoramaPasswd = EncryptPassword(panoramaPasswd);
                        panoramaSettingsUI.PanoramaPassword = panoramaPasswd;
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(panoramaFolder))
            {
                LogErrorOutput("Please specify a folder on the Panorama server.");
                error = true;
            }

            if (error)
            {
                return false;
            }

            // Verify that we can connect to the given Panorama server with the user's credentials.
            var panoramaClient = new WebPanoramaClient(serverUri);
            try
            {
                PanoramaUtil.VerifyServerInformation(panoramaClient, serverUri, panoramaEmail,
                    DecryptPassword(panoramaPasswd));
            }
            catch (Exception ex)
            {
                LogErrorOutput(ex.Message);
                return false;
            }

            try
            {
                PanoramaUtil.VerifyFolder(panoramaClient,
                    new Server(serverUri, panoramaEmail, DecryptPassword(panoramaPasswd)),
                    panoramaFolder);
            }
            catch (Exception ex)
            {
                LogErrorOutput(ex.Message);
                return false;
            }

            PanoramaServerUri = serverUri;
            Settings = panoramaSettingsUI;
            return true;
        }

        public override void SaveSettings()
        {
            Settings.Save();
        }

        public override void PrintSettings()
        {
            Logger.Log("Publish to Panorama: {0}", Settings.PublishToPanorama);
            if (Settings.PublishToPanorama)
            {
                Logger.Log("Panorama server: {0}", Settings.PanoramaServerUrl);
                Logger.Log("Panorama folder: {0}", Settings.PanoramaFolder);
                Logger.Log("Panorama user: {0}", Settings.PanoramaUserEmail);  
            }
        }

        public override string SkylineRunnerArgs(ImportContext importContext, bool toPrint = false)
        {
            if (!IsSelected() || (importContext.ImportExisting && !importContext.ImportingLast()))
            {
                // Do not upload to Panorama if this we are importing existing documents and this is not the 
                // last file being imported.
                return string.Empty;
            }

            var passwdArg = toPrint ? "" : string.Format("--panorama-password=\"{0}\"", DecryptPassword(Settings.PanoramaPassword));
            var uploadArgs = string.Format(
                    " --panorama-server=\"{0}\" --panorama-folder=\"{1}\" --panorama-username=\"{2}\" {3}",
                    Settings.PanoramaServerUrl,
                    Settings.PanoramaFolder,
                    Settings.PanoramaUserEmail,
                    passwdArg);
            return uploadArgs;
        }

        public override ProcessInfo RunBefore(ImportContext importContext)
        {
            return null;
        }

        public override ProcessInfo RunAfter(ImportContext importContext)
        {
            return null;
        }

        public string EncryptPassword(String password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return string.Empty;
            }

            try
            {
                var encrypted = ProtectedData.Protect(
                    Encoding.Unicode.GetBytes(password), entropy,
                    DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encrypted);
            }
            catch (Exception e)
            {
                LogErrorOutput("Error encrypting password.");
                LogErrorOutput(e.Message);
            }
            return string.Empty;
        }

        public string DecryptPassword(string encryptedPassword)
        {
            if (string.IsNullOrEmpty(encryptedPassword))
            {
                return string.Empty;
            }
            try
            {
                byte[] decrypted = ProtectedData.Unprotect(
                    Convert.FromBase64String(encryptedPassword), entropy,
                    DataProtectionScope.CurrentUser);
                return Encoding.Unicode.GetString(decrypted);
            }
            catch (Exception e)
            {
                LogErrorOutput("Error decrypting password.");
                LogErrorOutput(e.Message);   
            }
            return string.Empty;
        }
    }

    public class PanoramaPinger
    {
        private PanoramaSettingsTab _panoramaSettingsTab;
        private IAutoQCLogger _logger;
        private short _status; //1 = success; 2 = fail
        private Timer _timer;

        public PanoramaPinger(PanoramaSettingsTab panoramaSettingsTab, IAutoQCLogger logger)
        {
            _panoramaSettingsTab = panoramaSettingsTab;
            _logger = logger;
        }

        public void PingPanoramaServer()
        {
            var panoramaServerUri = _panoramaSettingsTab.PanoramaServerUri;
            if (_panoramaSettingsTab.IsSelected() &&  panoramaServerUri != null)
            {
                var panoramaClient = new WebPanoramaClient(panoramaServerUri);
                try
                {
                    var success = panoramaClient.PingPanorama(_panoramaSettingsTab.Settings.PanoramaFolder, 
                                                               _panoramaSettingsTab.Settings.PanoramaUserEmail,
                                                               _panoramaSettingsTab.DecryptPassword(_panoramaSettingsTab.Settings.PanoramaPassword
                                                              ));

                    if (success && _status != 1)
                    {
                        _logger.Log("Successfully pinged Panorama server.");
                        _status = 1;
                    }
                    if (!success && _status != 2)
                    {
                        _logger.LogErrorToFile("Error pinging Panorama server.  Please confirm that " + panoramaServerUri +
                                 " is running LabKey Server 16.1 or higher.");
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