using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using AutoQC.Properties;

namespace AutoQC
{
    public class PanoramaSettings: TabSettings
    {
        static readonly byte[] entropy = System.Text.Encoding.Unicode.GetBytes("Encrypt Panorama password");

        private bool PublishToPanorama { get; set; }
        private string PanoramaServerUrl { get; set; }
        private string PanoramaUserName { get; set; }
        private string PanoramaPassword { get; set; }
        private string PanoramaFolder { get; set; }

        public override void InitializeFromDefaultSettings()
        {
            PanoramaServerUrl = Settings.Default.PanoramaUrl;
            MainForm.textPanoramaUrl.Text = PanoramaServerUrl;

            PanoramaUserName = Settings.Default.PanoramaUserEmail;
            MainForm.textPanoramaEmail.Text = PanoramaUserName;

            PanoramaPassword = Settings.Default.PanoramaPassword;
            MainForm.textPanoramaPasswd.Text = PanoramaPassword;

            PanoramaFolder = Settings.Default.PanoramaFolder; 
            MainForm.textPanoramaFolder.Text = PanoramaFolder;

            PublishToPanorama = Settings.Default.PublishToPanorama;
            MainForm.cbPublishToPanorama.Checked = PublishToPanorama;

            if (!PublishToPanorama)
            {
                MainForm.groupBoxPanorama.Enabled = false;
            }
        }

        public override bool IsSelected()
        {
            return PublishToPanorama;
        }

        public override bool ValidateSettings()
        {
            if (!MainForm.cbPublishToPanorama.Checked)
            {
                LogOutput("Will NOT publish Skyline document to Panorama.");
                return true;
            }

            LogOutput("Validating Panorama settings...");
            var error = false;
            var panoramaUrl = MainForm.textPanoramaUrl.Text;
            Uri serverUri;
            try
            {
                serverUri = new Uri(PanoramaUtil.ServerNameToUrl(panoramaUrl));
            }
            catch (UriFormatException)
            {
                LogErrorOutput("Panorama server name is invalid.");
                return false;
            }

            var panoramaEmail = MainForm.textPanoramaEmail.Text;
            var panoramaPasswd = MainForm.textPanoramaPasswd.Text;
            var panoramaFolder = MainForm.textPanoramaFolder.Text;

            if (string.IsNullOrWhiteSpace(panoramaEmail))
            {
                LogErrorOutput("Please specify a Panorama user name.");
                error = true;
            }
            if (string.IsNullOrWhiteSpace(panoramaPasswd))
            {
                LogErrorOutput("Please specify a Panorama user password.");
                error = true;
            }
            else
            {
                if (!panoramaPasswd.Equals(PanoramaPassword))
                {
                    // Encrypt the password
                    try
                    {
                        panoramaPasswd = EncryptPassword(panoramaPasswd);
                        MainForm.RunUI(() => MainForm.textPanoramaPasswd.Text = panoramaPasswd);
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                    PanoramaPassword = panoramaPasswd;
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

            var panoramaClient = new WebPanoramaClient(serverUri);
            try
            {
                PanoramaUtil.VerifyServerInformation(panoramaClient, serverUri, panoramaEmail, DecryptPassword(panoramaPasswd));
            }
            catch (Exception ex)
            {
                LogErrorOutput(ex.Message);
                return false;
            }

            try
            {
                PanoramaUtil.VerifyFolder(panoramaClient, new Server(serverUri, panoramaEmail, DecryptPassword(panoramaPasswd)), MainForm.textPanoramaFolder.Text);
            }
            catch (Exception ex)
            {
                LogErrorOutput(ex.Message);
                return false;
            }

            return true;
        }

        public override void SaveSettings()
        {
            PanoramaServerUrl = MainForm.textPanoramaUrl.Text;
            Settings.Default.PanoramaUrl = PanoramaServerUrl;

            PanoramaUserName = MainForm.textPanoramaEmail.Text;
            Settings.Default.PanoramaUserEmail = PanoramaUserName;

            PanoramaPassword = MainForm.textPanoramaPasswd.Text; 
            Settings.Default.PanoramaPassword = PanoramaPassword;

            PanoramaFolder = MainForm.textPanoramaFolder.Text;
            Settings.Default.PanoramaFolder = PanoramaFolder;

            PublishToPanorama = MainForm.cbPublishToPanorama.Checked;
            Settings.Default.PublishToPanorama = PublishToPanorama;
        }

        public override IEnumerable<string> SkylineRunnerArgs(ImportContext importContext, bool toPrint = false)
        {
            if (!IsSelected() || (importContext.ImportExisting() && !importContext.ImportingLast()))
            {
                // Upload to Panorama if this is the last file being added to the Skyline document.
                return Enumerable.Empty<string>();
            }

            var passwdArg = toPrint ? "" : string.Format("--panorama-password=\"{0}\"", DecryptPassword(PanoramaPassword));
            var uploadArgs = string.Format(
                    " --panorama-server=\"{0}\" --panorama-folder=\"{1}\" --panorama-username=\"{2}\" {3}",
                    PanoramaServerUrl,
                    PanoramaFolder,
                    PanoramaUserName,
                    passwdArg);
            return new List<string> { uploadArgs };
        }

        public string EncryptPassword(String password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return string.Empty;
            }

            try
            {
                byte[] encrypted = ProtectedData.Protect(
                    System.Text.Encoding.Unicode.GetBytes(password),
                    entropy,
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
                    Convert.FromBase64String(encryptedPassword),
                    entropy,
                    DataProtectionScope.CurrentUser);
                return System.Text.Encoding.Unicode.GetString(decrypted);
            }
            catch (Exception e)
            {
                LogErrorOutput("Error decrypting password.");
                LogErrorOutput(e.Message);   
            }
            return string.Empty;
        }
    }
}