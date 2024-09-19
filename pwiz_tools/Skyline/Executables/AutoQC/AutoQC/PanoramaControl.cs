using System;
using System.Windows.Forms;
using SharedBatch;

namespace AutoQC
{
    public partial class PanoramaControl : UserControl, IValidatorControl
    {

        // A control used by the InvalidConfigSetupForm to correct invalid panorama settings

        // Implements IValidatorControl:
        //    - GetVariable() returns the current path (_panoramaSettings)
        //    - IsValid() determines if _panoramaSettings is valid

        private PanoramaSettings _panoramaSettings;


        public PanoramaControl(PanoramaSettings panoramaSettings)
        {
            InitializeComponent();
            textPanoramaUrl.Text = panoramaSettings.PanoramaServerUrl;
            textPanoramaEmail.Text = panoramaSettings.PanoramaUserEmail;
            textPanoramaPasswd.Text = panoramaSettings.PanoramaPassword;
            textPanoramaFolder.Text = panoramaSettings.PanoramaFolder;
            _panoramaSettings = panoramaSettings;
        }

        public object GetVariable() => _panoramaSettings;

        public bool IsValid(out string errorMessage)
        {
            errorMessage = null;
            _panoramaSettings = new PanoramaSettings(_panoramaSettings.PublishToPanorama, textPanoramaUrl.Text,
                textPanoramaEmail.Text, textPanoramaPasswd.Text, textPanoramaFolder.Text);
            try
            {
                _panoramaSettings.ValidateSettings(true);
                return true;
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
                label_err_message.Text = errorMessage;
                return false;
            }
        }

        // For testing only
        public void SetInput(object panoramaObject)
        {
            var panoramaSettings = (PanoramaSettings) panoramaObject;
            textPanoramaUrl.Text = panoramaSettings.PanoramaServerUrl;
            textPanoramaEmail.Text = panoramaSettings.PanoramaUserEmail;
            textPanoramaPasswd.Text = panoramaSettings.PanoramaPassword;
            textPanoramaFolder.Text = panoramaSettings.PanoramaFolder;
        }
    }
}
