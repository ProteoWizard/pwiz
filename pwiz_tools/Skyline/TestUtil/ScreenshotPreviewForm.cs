using System;
using System.Drawing;
using System.Windows.Forms;

namespace pwiz.SkylineTestUtil
{
    public partial class ScreenshotPreviewForm : Form
    {
        private static readonly int SCREENSHOT_MAX_WIDTH = 800; //doubled as side by side
        private static readonly int SCREENSHOT_MAX_HEIGHT = 800;

        public ScreenshotPreviewForm()
        {
            InitializeComponent();
        }


        public void ShowScreenshotPreview(Bitmap newScreenshot, Bitmap oldScreenShot)
        {
            var newScreenshotSize = CalculateBitmapSize(newScreenshot);
            var oldScreenshotSize = CalculateBitmapSize(oldScreenShot);
            var newScreenshotBitmap = new Bitmap(newScreenshot, newScreenshotSize);
            var oldScreenshotBitmap = new Bitmap(oldScreenShot, oldScreenshotSize);
            SetPreviewImages(newScreenshotBitmap, oldScreenshotBitmap);
            this.Show();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            e.Cancel = true;
            this.Hide();

            base.OnFormClosing(e);
        }

        private void SetPreviewImages(Bitmap newScreenshot, Bitmap oldScreenShot)
        {
            newScreenshotPictureBox.Image = newScreenshot;
            oldScreenshotPictureBox.Image = oldScreenShot;

            splitContainer1.SplitterDistance = splitContainer1.Width / 2;

            var minFormWidth = newScreenshot.Width + oldScreenShot.Width;
            var minFormHeight = Math.Max(newScreenshot.Height, oldScreenShot.Height);
            if (ClientSize.Width < minFormWidth || ClientSize.Height < minFormHeight)
            {
                ClientSize = new Size(minFormWidth, minFormHeight);
            }

        }

        private Size CalculateBitmapSize(Bitmap bitmap)
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
    }
}
