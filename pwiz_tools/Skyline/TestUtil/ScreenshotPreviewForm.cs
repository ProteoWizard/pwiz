using System;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil.Properties;
using pwiz.Skyline;
using static System.String;

namespace pwiz.SkylineTestUtil
{
    public partial class ScreenshotPreviewForm : Form
    {
        private static readonly int SCREENSHOT_MAX_WIDTH = 800; //doubled as side by side
        private static readonly int SCREENSHOT_MAX_HEIGHT = 800;
        private readonly ScreenshotManager _screenshotManager;
        private readonly object _pauseLock;
        private readonly PauseAndContinueForm _pauseAndContinueForm;

        private string _description;
        private string _link;
        private Control _screenshotControl;
        private string _fileToSave;
        private bool _fullScreen;
        private Func<Bitmap, Bitmap> _processShot;
        private Bitmap _newScreenshot;

        public ScreenshotPreviewForm(PauseAndContinueForm pauseAndContinueForm, ScreenshotManager screenshotManager, object pauseLock)
        {
            InitializeComponent();
            _pauseAndContinueForm = pauseAndContinueForm;
            _screenshotManager = screenshotManager;
            _pauseLock = pauseLock;

            //subscribe to paint events to resize components when needed
            oldScreenshotPictureBox.Paint += (sender, e) => ResizeComponents();
            newScreenshotPictureBox.Paint += (sender, e) => ResizeComponents();
        }

        //To be called by the pause and continue form when switching modes or entering new pause
        public void Show(string description, string link, Control screenshotControl, string fileToSave, bool fullScreen, Func<Bitmap, Bitmap> processShot, bool delayForScreenshot)
        {
            _description = description;
            _link = link;
            _screenshotControl = screenshotControl;
            _fileToSave = fileToSave;
            _fullScreen = fullScreen;
            _processShot = processShot;

            //This cannot block until the command is finished as it would block the caller's thread
            BeginInvoke((Action)(() => RefreshAndShow(delayForScreenshot)));
        }

        private async void RefreshAndShow(bool delayForScreenshot)
        {
            RefreshOldScreenshot();
            await RefreshNewScreenshot(delayForScreenshot);
            UpdateDescriptionLabel();
            EnableControls();

            if (!Visible) Show();
            this.SetForegroundWindow();
        }

        private void RefreshOldScreenshot()
        {
            Bitmap oldScreenshot = LoadScreenshot(_fileToSave);
            SetPreviewImage(oldScreenshot, oldScreenshotPictureBox);
        }

        private Bitmap LoadScreenshot(string file)
        {
            try
            {
                var existingImageBytes = File.ReadAllBytes(_fileToSave);
                var existingImageMemoryStream = new MemoryStream(existingImageBytes);
                return new Bitmap(existingImageMemoryStream);
            }
            catch (Exception e)
            {
                MessageDlg.ShowException(this, e);
                return Resources.DiskFailure;
            }
        }

        private async Task RefreshNewScreenshot(bool delay)
        {
            if(delay) await Task.Delay(1000);
            ScreenshotManager.ActivateScreenshotForm(_screenshotControl);

            _newScreenshot = _screenshotManager.TakeShot(_screenshotControl, _fullScreen, null, _processShot);
            SetPreviewImage(_newScreenshot, newScreenshotPictureBox);
        }

        private void SetPreviewImage(Bitmap screenshot, PictureBox previewBox)
        {
            var screenshotSize = CalculateBitmapSize(screenshot, previewBox);
            var resizedScreenshot = new Bitmap(screenshot, screenshotSize);

            previewBox.Image = resizedScreenshot;
        }

        private Size CalculateBitmapSize(Bitmap bitmap, PictureBox previewBox)
        {
            var startingSize = bitmap.Size;
            var scaledHeight = (double)SCREENSHOT_MAX_HEIGHT / startingSize.Height;
            var scaledWidth = (double)SCREENSHOT_MAX_WIDTH / startingSize.Width;

            //if auto size is disabled we will size the images to fit the current window
            if (!autoSizeWindowCheckbox.Checked)
            {
                scaledHeight = (double)previewBox.Height / startingSize.Height;
                scaledWidth = (double)previewBox.Width / startingSize.Width;
            }

            //If  constraints are not breached then use existing size
            if (scaledHeight >= 1 && scaledWidth >= 1)
            {
                return startingSize;
            }

            var scale = Math.Min(scaledHeight, scaledWidth);
            return new Size((int)(startingSize.Width * scale), (int)(startingSize.Height * scale));
        }

        private void LoadUpcomingOldScreenshot()
        {
            string pattern = @"(?<=s-)(\d+)";
            var regex = new Regex(pattern);

            var match = regex.Match(_fileToSave);
            if (match.Success)
            {
                int screenshotCount = int.Parse(match.Value);
                int incrementedCount = screenshotCount + 1;
                string nextOldScreenshotFile = regex.Replace(_fileToSave, incrementedCount.ToString());

                if (File.Exists(nextOldScreenshotFile))
                {
                    Bitmap nextOldScreenshot = LoadScreenshot(nextOldScreenshotFile);
                    SetPreviewImage(nextOldScreenshot, oldScreenshotPictureBox);
                }
            }
        }

        private bool IsSharingSkylineScreen()
        {
            var skylineWindow = Program.MainWindow;
            var skylineScreen = skylineWindow.Invoke((Func<Screen>)(() => Screen.FromControl(skylineWindow)));
            return Screen.FromControl(this).Equals(skylineScreen);
        }

        private void ResizeComponents()
        {
            previewSplitContainer.SplitterDistance = previewSplitContainer.Width / 2;

            if (autoSizeWindowCheckbox.Checked && oldScreenshotPictureBox.Image != null)
            {
                var newScreenshotImage = newScreenshotPictureBox.Image != null ? newScreenshotPictureBox.Image : oldScreenshotPictureBox.Image;
                var minFormWidth = newScreenshotImage.Width + newScreenshotImage.Width;
                var minFormHeight = Math.Max(newScreenshotImage.Height, oldScreenshotPictureBox.Image.Height) + previewFlowLayoutControlPanel.Height + oldScreenshotLabelPanel.Height;
                if (ClientSize.Width < minFormWidth || ClientSize.Height < minFormHeight)
                {
                    ClientSize = new Size(minFormWidth, minFormHeight);
                }
            }
        }

        private void DisableControls()
        {
            continueBtn.Enabled = false;
            saveScreenshotBtn.Enabled = false;
            saveScreenshotAndContinueBtn.Enabled = false;
            refreshBtn.Enabled = false;
            autoSizeWindowCheckbox.Enabled = false;
        }

        private void EnableControls()
        {
            continueBtn.Enabled = true;
            saveScreenshotBtn.Enabled = true;
            saveScreenshotAndContinueBtn.Enabled = true;
            refreshBtn.Enabled = true;
            autoSizeWindowCheckbox.Enabled = true;
        }
        private void UpdateDescriptionLabel()
        {
            descriptionLinkLabel.Text = _description;
            descriptionLinkLabel.LinkColor = Color.Blue;
            descriptionLinkLabel.LinkBehavior = LinkBehavior.AlwaysUnderline;
        }

        private void UpdateWaitingDescriptionLabel()
        {
            descriptionLinkLabel.Text = "Waiting on Skyline for next screenshot...";
            descriptionLinkLabel.LinkColor = descriptionLinkLabel.ForeColor;
            descriptionLinkLabel.LinkBehavior = LinkBehavior.NeverUnderline;
        }

        private void Continue()
        {
            if (Visible && IsSharingSkylineScreen())
            {
                Hide();
            }
            else
            {
                DisableControls();
                UpdateWaitingDescriptionLabel();
                newScreenshotPictureBox.Image = null;
                oldScreenshotPictureBox.Image = null;
                LoadUpcomingOldScreenshot();
            }

            // Start the tests again
            lock (_pauseLock)
            {
                Monitor.PulseAll(_pauseLock);
            }
        }

        private bool SaveScreenshot()
        {
            if (FileEx.IsWriteLocked(_fileToSave))
            {
                MessageDlg.Show(this, TextUtil.LineSeparate(string.Format("The file {0} is locked.", _fileToSave),
                    "Check that it is not open in another program such as TortoiseIDiff."));
                return false;
            }
            try
            {
                _newScreenshot.Save(_fileToSave);
                return true;
            }
            catch (Exception e)
            {
                MessageDlg.ShowException(this, e);
                return false;
            }
        }

        private void GotoLink()
        {
            if (!IsNullOrEmpty(_link))
            {
                WebHelpers.OpenLink(_link);
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }    // Don't take activation away from SkylineWindow
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                _pauseAndContinueForm.SwitchToPauseAndContinue();
            }
        }

        private void continueBtn_Click(object sender, EventArgs e)
        {
            Continue();
        }

        private void saveScreenshotBtn_Click(object sender, EventArgs e)
        {
            SaveScreenshot();
        }

        private void saveScreenshotAndContinueBtn_Click(object sender, EventArgs e)
        {
            if (SaveScreenshot())
                Continue();
        }

        private async void refreshBtn_Click(object sender, EventArgs e)
        {
            RefreshOldScreenshot();
            await RefreshNewScreenshot(false);
            this.SetForegroundWindow();
        }

        private void descriptionLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            GotoLink();
        }
    }
}
