using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil.Properties;

namespace pwiz.SkylineTestUtil
{
    public partial class ScreenshotPreviewForm : Form
    {
        private static readonly int SCREENSHOT_MAX_WIDTH = 800; //doubled as side by side
        private static readonly int SCREENSHOT_MAX_HEIGHT = 800;
        private readonly ScreenshotManager _screenshotManager;
        private readonly object _pauseLock;
        private readonly PauseAndContinueForm _pauseAndContinueForm;

        private Control _screenshotControl;
        private string _fileToSave;
        private bool _fullScreen;
        private Func<Bitmap, Bitmap> _processShot;
        private Bitmap _storedOldScreenshot;
        private Bitmap _storedNewScreenshot;
        
        public ScreenshotPreviewForm(PauseAndContinueForm pauseAndContinueForm, ScreenshotManager screenshotManager, object pauseLock)
        {
            InitializeComponent();
            _pauseAndContinueForm = pauseAndContinueForm;
            _screenshotManager = screenshotManager;
            _pauseLock = pauseLock;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;    // This form doesn't close, it just switches to the PauseAndContinue form
            _pauseAndContinueForm.SwitchToPauseAndContinue();
        }

        public void UpdateViewState(string description, Control screenshotControl, string fileToSave, bool fullScreen, Func<Bitmap, Bitmap> processShot)
        {
            _screenshotControl = screenshotControl;
            _fileToSave = fileToSave;
            _fullScreen = fullScreen;
            _processShot = processShot;

            Text = description;
            RefreshScreenshots();
        }

        private void RefreshScreenshots()
        {
            ScreenshotManager.ActivateScreenshotForm(_screenshotControl);

            _storedNewScreenshot = _screenshotManager.TakeShot(_screenshotControl, _fullScreen,null, _processShot);
            try
            {
                var existingImageBytes = File.ReadAllBytes(_fileToSave);
                var existingImageMemoryStream = new MemoryStream(existingImageBytes);
                _storedOldScreenshot = new Bitmap(existingImageMemoryStream);
            }
            catch (Exception e)
            {
                MessageDlg.ShowException(this, e);
                _storedOldScreenshot = Resources.DiskFailure;
            }
            SetPreviewImages(_storedNewScreenshot, _storedOldScreenshot);
            this.SetForegroundWindow();
        }

        private void SetPreviewImages(Bitmap newScreenshot, Bitmap oldScreenShot)
        {
            var newScreenshotSize = CalculateBitmapSize(newScreenshot);
            var oldScreenshotSize = CalculateBitmapSize(oldScreenShot);
            var newScreenshotBitmap = new Bitmap(newScreenshot, newScreenshotSize);
            var oldScreenshotBitmap = new Bitmap(oldScreenShot, oldScreenshotSize);

            oldScreenshotPictureBox.Image = oldScreenshotBitmap;
            newScreenshotPictureBox.Image = newScreenshotBitmap;

            previewSplitContainer.SplitterDistance = previewSplitContainer.Width / 2;

            var minFormWidth = newScreenshot.Width + oldScreenShot.Width;
            var minFormHeight = Math.Max(newScreenshot.Height, oldScreenShot.Height);
            if (autoSizeWindowCheckbox.Checked && (ClientSize.Width < minFormWidth || ClientSize.Height < minFormHeight))
            {
                ClientSize = new Size(minFormWidth, minFormHeight);
            }
        }

        private static Size CalculateBitmapSize(Bitmap bitmap)
        {
            var startingSize = bitmap.Size;
            var scaledHeight = (double)SCREENSHOT_MAX_HEIGHT / startingSize.Height;
            var scaledWidth = (double)SCREENSHOT_MAX_WIDTH / startingSize.Width;

            //If  constraints are not breached then use existing size
            if (scaledHeight >= 1 && scaledWidth >= 1)
            {
                return startingSize;
            }

            var scale = Math.Min(scaledHeight, scaledWidth);
            return new Size((int)(startingSize.Width * scale), (int)(startingSize.Height * scale));
        }

        private void Continue()
        {
            Hide();

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
                _storedNewScreenshot.Save(_fileToSave);
                return true;
            }
            catch (Exception e)
            {
                MessageDlg.ShowException(this, e);
                return false;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }    // Don't take activation away from SkylineWindow
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

        private void refreshBtn_Click(object sender, EventArgs e)
        {
            RefreshScreenshots();
        }

        private void closePreviewBtn_Click(object sender, EventArgs e)
        {
            _pauseAndContinueForm.SwitchToPauseAndContinue();
        }
    }
}
