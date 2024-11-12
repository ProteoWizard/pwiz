/*
 * Original author: Eduardo Armendariz <wardough .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil.Properties;
using pwiz.Skyline;
using pwiz.Skyline.Controls;

namespace pwiz.SkylineTestUtil
{

    public interface IPauseTestController
    {
        void Continue();
        void ShowPauseForm();

        int ScreenshotNum { get; }
        string Description { get; }
        string LinkUrl { get; }
        string FileToSave { get; }
        Control ScreenshotControl { get; }
        bool FullScreen { get; }
        Func<Bitmap, Bitmap> ProcessShot { get; }
    }

    public partial class ScreenshotPreviewForm : Form
    {
        private const string WAITING_DESCRIPTION = @"Waiting on Skyline for next screenshot...";
        private const string END_TEST_TEXT = "End";

        private const int SCREENSHOT_MAX_WIDTH = 800; // doubled as side by side
        private const int SCREENSHOT_MAX_HEIGHT = 800;

        private readonly ScreenshotManager _screenshotManager;
        private readonly IPauseTestController _pauseTestController;

        private Thread _screenshotPreviewThread;
        private readonly ManualResetEvent _screenshotPreviewHandleReadyEvent = new ManualResetEvent(false);

        // these members should only be accessed in a block which locks on _lock (is this necessary for all?)
        #region synchronized members
        private readonly object _lock = new object();
        private int _screenshotNum;
        private string _description;
        private string _linkUrl;
        private string _fileToSave;
        private ScreenshotValues _screenshotValues;
        private Bitmap _oldScreenshot;
        private string _fileLoaded;
        private Bitmap _newScreenshot;
        private bool _screenshotTaken;
        private NextScreenshotProgress _nextScreenshotProgress;

        private class NextScreenshotProgress
        {
            public NextScreenshotProgress(int currentNum, int stopNum)
            {
                CurrentNum = currentNum;
                StopNum = stopNum;
                TotalToNext = stopNum - currentNum;
            }

            public int CurrentNum { get; set; }
            public int StopNum { get; }
            public int TotalToNext { get; }

            public int PercentDone => 100 * (CurrentNum - StopNum + TotalToNext) / TotalToNext;
        }
        #endregion

        public ScreenshotPreviewForm(IPauseTestController pauseTestController, ScreenshotManager screenshotManager)
        {
            InitializeComponent();
            
            Icon = Resources.camera;
            // Unfortunately there is not enough information about the image sizes to
            // the the starting location right here, but this is better than using the Windows default
            StartPosition = FormStartPosition.Manual;
            Location = GetBestLocation();

            _pauseTestController = pauseTestController;
            _screenshotManager = screenshotManager;
        }

        /// <summary>
        /// To be called by the <see cref="IPauseTestController"/> when switching modes or entering new pause.
        /// </summary>
        /// <param name="delayForScreenshot">True when test UI may need time to stabilize before a screenshot</param>
        public void Show(bool delayForScreenshot)
        {
            lock (_lock)
            {
                _screenshotNum = _pauseTestController.ScreenshotNum;
                _description = _pauseTestController.Description;
                _linkUrl = _pauseTestController.LinkUrl;
                _screenshotValues = new ScreenshotValues(_pauseTestController.ScreenshotControl,
                    _pauseTestController.FullScreen, _pauseTestController.ProcessShot, delayForScreenshot);
                if (!Equals(_fileToSave, _pauseTestController.FileToSave))
                {
                    _fileToSave = _pauseTestController.FileToSave;
                    _fileLoaded = null;
                }

                if (_nextScreenshotProgress == null || _nextScreenshotProgress.StopNum == _screenshotNum)
                {
                    _screenshotTaken = false;
                    _nextScreenshotProgress = null;    // Done waiting for the next screenshot
                }
                else
                {
                    IncrementScreenshot();
                    ActionUtil.RunAsync(_pauseTestController.Continue);
                }

                if (_screenshotPreviewThread == null)
                {
                    _screenshotPreviewThread = new Thread(() => Application.Run(this)) {Name = "Preview form"};
                    _screenshotPreviewThread.SetApartmentState(ApartmentState.STA);
                    _screenshotPreviewThread.Start();
                    _screenshotPreviewHandleReadyEvent.WaitOne(); // Block until the handle is created
                }
            }

            FormStateChangedBackground();
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }    // Don't take activation away from SkylineWindow
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _screenshotPreviewHandleReadyEvent.Set();
        }

        protected override void OnShown(EventArgs e)
        {
            _autoResizeComplete = true;
            base.OnShown(e);
        }

        /// <summary>
        /// Called by the <see cref="IPauseTestController"/> to block until the thread for this form is complete.
        /// </summary>
        public void WaitForCompletion()
        {
            lock (_lock)
            {
                if (_screenshotPreviewThread is { IsAlive: true })
                    _screenshotPreviewThread.Join();
            }
        }

        private bool HasBackgroundWork { get { lock(_lock) { return !IsLoaded || (!IsWaiting && !IsScreenshotTaken); } } }
        private bool IsComplete { get { lock (_lock) { return IsLoaded && !IsWaiting && IsScreenshotTaken; } } }
        private bool IsLoaded { get { lock (_lock) { return Equals(_fileLoaded, _fileToSave); } } }
        private bool IsWaiting { get { lock (_lock) { return _nextScreenshotProgress != null; } } }
        private bool IsScreenshotTaken { get { lock (_lock) { return _screenshotTaken; } } }

        private void RefreshScreenshots()
        {
            lock (_lock)
            {
                _fileLoaded = null;
                _screenshotTaken = false;
            }

            FormStateChanged();
        }

        private void FormStateChangedBackground()
        {
            if (InvokeRequired)
                BeginInvoke((Action)FormStateChanged);
            else
                FormStateChanged();
        }

        /// <summary>
        /// Updates the UI and begins any background work that still needs to be done.
        /// </summary>
        private void FormStateChanged()
        {
            lock (_lock)
            {
                UpdateToolbar();
                UpdatePreviewImages();
            }

            if (IsLoaded)
            {
                ResizeComponents();
            }

            if (HasBackgroundWork)
                ActionUtil.RunAsync(UpdateScreenshotsAsync);
            else if (IsComplete)
            {
                // State update has completed show the form and activate it
                if (!Visible)
                    Show();

                this.SetForegroundWindow();
            }
        }

        private void UpdatePreviewImages()
        {
            lock (_lock)
            {
                SetPreviewImage(oldScreenshotPictureBox, _oldScreenshot);
                SetPreviewImage(newScreenshotPictureBox, _newScreenshot);
            }
        }

        private void UpdateToolbar()
        {
            // Update the description
            descriptionLinkLabel.Text = _description;
            helpTip.SetToolTip(descriptionLinkLabel, _description);

            if (_linkUrl == null)
            {
                descriptionLinkLabel.LinkColor = descriptionLinkLabel.ForeColor;
                descriptionLinkLabel.LinkBehavior = LinkBehavior.NeverUnderline;
            }
            else
            {
                descriptionLinkLabel.LinkColor = Color.Blue;
                descriptionLinkLabel.LinkBehavior = LinkBehavior.AlwaysUnderline;
            }

            // Update the progress bar
            if (_nextScreenshotProgress != null)
            {
                if (_nextScreenshotProgress.TotalToNext > 1)
                {
                    progressBar.Style = ProgressBarStyle.Continuous;
                    _nextScreenshotProgress.CurrentNum = _screenshotNum;
                    progressBar.Value = _nextScreenshotProgress.PercentDone;
                }
                else
                {
                    progressBar.Style = ProgressBarStyle.Marquee;
                }

                if (!progressBar.Visible)
                {
                    progressBar.Visible = progressBar.Enabled = true;
                    progressBar.Left = descriptionLinkLabel.Left;
                    progressBar.Width = progressBar.Parent.Width - progressBar.Left - 10;
                    progressBar.CustomText = descriptionLinkLabel.Text;
                    descriptionLinkLabel.Visible = false;
                }

                textBoxNext.Enabled = labelNext.Enabled = false;
            }
            else
            {
                descriptionLinkLabel.Visible = true;
                progressBar.Visible = progressBar.Enabled = false;
                int nextScreenshot = _screenshotNum + 1;
                bool nextExists = File.Exists(_screenshotManager.ScreenshotFile(nextScreenshot));

                textBoxNext.Text = nextExists ? nextScreenshot.ToString() : END_TEST_TEXT;
                textBoxNext.Enabled = nextExists;
                labelNext.Enabled = true;
            }

            // Update the buttons
            // TODO: Enable continue button as "Stop" button
            continueBtn.Enabled = refreshBtn.Enabled = !progressBar.Visible;
            saveScreenshotBtn.Enabled = saveScreenshotAndContinueBtn.Enabled = IsScreenshotTaken;
        }

        private void UpdateScreenshotsAsync()
        {
            Assume.IsTrue(InvokeRequired);  // Expecting this to run on a background thread. Use ActionUtil.RunAsync()

            Bitmap oldScreenshot, newScreenshot;
            string fileToSave;
            string fileLoaded;
            bool shotTaken;
            ScreenshotValues screenshotValues;
            bool waitingForScreenshot;
            lock (_lock)
            {
                screenshotValues = _screenshotValues;
                fileToSave = _fileToSave;
                fileLoaded = _fileLoaded;
                shotTaken = _screenshotTaken;
                oldScreenshot = _oldScreenshot;
                newScreenshot = _newScreenshot;
                waitingForScreenshot = IsWaiting;
            }

            if (!Equals(fileLoaded, fileToSave))
            {
                oldScreenshot = LoadScreenshot(fileToSave);
                fileLoaded = fileToSave;
            }
            // Only take a new screenshot when the test is ready
            if (waitingForScreenshot)
            {
                newScreenshot = Resources.progress;
            }
            else if (!shotTaken)
            {
                newScreenshot = TakeScreenshot(screenshotValues);
                shotTaken = true;
            }

            lock (_lock)
            {
                _oldScreenshot = oldScreenshot;
                _fileLoaded = fileLoaded;
                _newScreenshot = newScreenshot;
                _screenshotTaken = shotTaken;
            }

            FormStateChangedBackground();
        }

        private struct ScreenshotValues
        {
            public static readonly ScreenshotValues Empty = new ScreenshotValues(null, false, null, false);

            public ScreenshotValues(Control control, bool fullScreen, Func<Bitmap, Bitmap> processShot, bool delay)
            {
                Control = control;
                FullScreen = fullScreen;
                ProcessShot = processShot;
                Delay = delay;
            }

            public Control Control { get; }
            public bool FullScreen { get; }
            public Func<Bitmap, Bitmap> ProcessShot { get; }
            public bool Delay { get; }
        }

        private Bitmap TakeScreenshot(ScreenshotValues values)
        {
            if (Equals(values, ScreenshotValues.Empty))
            {
                // CONSIDER: Show a placeholder bitmap?
                return null;
            }
            if (values.Delay)
                Thread.Sleep(1000);

            var control = values.Control;
            ScreenshotManager.ActivateScreenshotForm(control);
            return _screenshotManager.TakeShot(control, values.FullScreen, null, values.ProcessShot);
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
                this.Invoke((Action) (() => MessageDlg.ShowException(this, e)));
                var failureBmp = Resources.DiskFailure;
                failureBmp.MakeTransparent(Color.White);
                return failureBmp;
            }
        }

        private void SetPreviewImage(PictureBox previewBox, Bitmap screenshot)
        {
            var newImage = screenshot;
            if (screenshot != null)
            {
                var containerSize = !autoSizeWindowCheckbox.Checked ? previewBox.Size : Size.Empty;
                var screenshotSize = CalcBitmapSize(screenshot, containerSize);
                if (screenshotSize != screenshot.Size)
                    newImage = new Bitmap(screenshot, screenshotSize);
            }

            previewBox.Image = newImage;
            if (newImage != null && Equals(newImage.RawFormat, ImageFormat.Gif))
            {
                // Unfortunately the animated progress GIF has a white background
                // and it cannot be made transparent without removing the animation
                previewBox.BackColor = Color.White;
            }
            else
            {
                // The oldScreenshotPictureBox never gets a white background
                previewBox.BackColor = oldScreenshotPictureBox.BackColor;
            }
        }

        private Size CalcBitmapSize(Bitmap bitmap, Size containerSize)
        {
            if (bitmap == null)
                return Size.Empty;

            var startingSize = bitmap.Size;
            var scaledHeight = (double)SCREENSHOT_MAX_HEIGHT / startingSize.Height;
            var scaledWidth = (double)SCREENSHOT_MAX_WIDTH / startingSize.Width;

            // If a container size is specified then the bitmap is fit to it
            if (!containerSize.IsEmpty)
            {
                scaledHeight = (double)containerSize.Height / startingSize.Height;
                scaledWidth = (double)containerSize.Width / startingSize.Width;
            }

            // If  constraints are not breached then use existing size
            if (scaledHeight >= 1 && scaledWidth >= 1)
            {
                return startingSize;
            }

            var scale = Math.Min(scaledHeight, scaledWidth);
            return new Size((int)(startingSize.Width * scale), (int)(startingSize.Height * scale));
        }

        private bool IsOverlappingSkyline()
        {
            var skylineRect = GetSkylineBounds();
            return !Rectangle.Intersect(skylineRect, Bounds).IsEmpty;
        }

        private Rectangle GetSkylineBounds()
        {
            var skylineWindow = Program.MainWindow;
            return (Rectangle) skylineWindow.Invoke((Func<Rectangle>)(() => skylineWindow.Bounds));
        }

        private Rectangle GetSkylineScreenBounds()
        {
            return GetSkylineScreen().Bounds;
        }

        private Screen GetSkylineScreen()
        {
            var skylineWindow = Program.MainWindow;
            return (Screen)skylineWindow.Invoke((Func<Screen>)(() => Screen.FromControl(skylineWindow)));
        }

        private void ResizeComponents()
        {
            previewSplitContainer.SplitterDistance = previewSplitContainer.Width / 2;

            if (!autoSizeWindowCheckbox.Checked)
                return;

            var autoSize = CalcAutoSize();
            if (autoSize.IsEmpty || ClientSize == autoSize)
                return;

            _autoResizeComplete = false;
            ClientSize = autoSize;
            _autoResizeComplete = true;

            if (GetSkylineScreen().Equals(Screen.FromControl(this)))
            {
                Location = GetBestLocation();
            }
            FormEx.ForceOnScreen(this);

            bool screenshotTaken, stopAtNexScreenshot;
            lock (_lock)
            {
                screenshotTaken = _screenshotTaken;
                stopAtNexScreenshot = _nextScreenshotProgress != null &&
                                      _nextScreenshotProgress.StopNum - _screenshotNum == 0;
            }

            if (!_screenshotTaken)
            {
                if (IsOverlappingSkyline())
                    Hide();
                else if (!stopAtNexScreenshot)
                    Show();
            }
        }

        private const int WINDOW_SPACING = 20;

        private Point GetBestLocation()
        {
            var boundsRect = Bounds;
            var skylineRect = GetSkylineBounds();
            var skylineScreenBounds = GetSkylineScreenBounds();
            var rightLocation = new Point(skylineRect.Right + WINDOW_SPACING, skylineRect.Top);
            boundsRect.Location = rightLocation;
            var onscreenRight = Rectangle.Intersect(boundsRect, skylineScreenBounds);
            var belowLocation = new Point(skylineRect.Left, skylineRect.Bottom + WINDOW_SPACING);
            boundsRect.Location = belowLocation;
            var onscreenBelow = Rectangle.Intersect(boundsRect, skylineScreenBounds);
            return onscreenRight.Width * onscreenRight.Height < onscreenBelow.Width * onscreenBelow.Height
                ? belowLocation
                : rightLocation;
        }

        private const int CELL_PADDING = 5;

        private Size CalcAutoSize()
        {
            lock (_lock)
            {
                var newImageSize = CalcBitmapSize(_newScreenshot, Size.Empty);
                var oldImageSize = CalcBitmapSize(_oldScreenshot, Size.Empty);
                if (newImageSize.IsEmpty && oldImageSize.IsEmpty)
                    return Size.Empty;

                var minFormWidth = Math.Max(newImageSize.Width, oldImageSize.Width) * 2 + CELL_PADDING * 4;
                var minFormHeight = Math.Max(newImageSize.Height, oldImageSize.Height) +
                                    splitBar.Height + oldScreenshotLabelPanel.Height + CELL_PADDING * 2;

                return new Size(minFormWidth, minFormHeight);
            }
        }

        private void Continue()
        {
            int minNext;
            lock (_lock)
            {
                minNext = _screenshotNum + 1;
            }

            int FindLastShot(int i)
            {
                // Start from a given screenshot and walk forward until the file for a given number
                // is found not to exist.
                while (File.Exists(_screenshotManager.ScreenshotFile(i+1)))
                    i++;
                return i;
            }
            int nextShot;
            if (Equals(textBoxNext.Text, END_TEST_TEXT))
                nextShot = FindLastShot(minNext);
            else
            {

                var helper = new MessageBoxHelper(this);
                if (!helper.ValidateNumberTextBox(textBoxNext, minNext, null, out nextShot))
                {
                    textBoxNext.Text = minNext.ToString();
                    textBoxNext.SelectAll();
                    return;
                }

                string nextScreenshotFile = _screenshotManager.ScreenshotFile(nextShot);
                if (!File.Exists(nextScreenshotFile))
                {
                    helper.ShowTextBoxError(textBoxNext, "Invalid {0} value {1}. Screenshot file {2} does not exist.", null, nextShot, nextScreenshotFile);
                    // Must be too big. So find the largest valid number.
                    nextShot = FindLastShot(minNext);
                    textBoxNext.Text = nextShot.ToString();
                    textBoxNext.SelectAll();
                    return;
                }
            }


            lock (_lock)
            {
                _nextScreenshotProgress = new NextScreenshotProgress(_screenshotNum, nextShot);
                _description = WAITING_DESCRIPTION;
                IncrementScreenshot();
            }

            ContinueInternal();
        }

        private void IncrementScreenshot()
        {
            lock (_lock)
            {
                _screenshotNum++;
                _fileToSave = _screenshotManager.ScreenshotFile(_screenshotNum);
                _linkUrl = _screenshotManager.ScreenshotUrl(_screenshotNum);
                _screenshotTaken = false;
            }
        }

        private void ContinueInternal()
        {
            if (Visible && IsOverlappingSkyline())
            {
                Hide();
            }
            else if(File.Exists(_screenshotManager.ScreenshotFile(_screenshotNum)))
            {
                FormStateChanged();
            }

            _pauseTestController.Continue();
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
            if (!string.IsNullOrEmpty(_linkUrl))
            {
                WebHelpers.OpenLink(_linkUrl);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide(); // Hide on close
                _pauseTestController.ShowPauseForm();    // And show the pause form
            }
        }

        private bool _autoResizeComplete;

        protected override void OnResize(EventArgs e)
        {
            if (autoSizeWindowCheckbox.Checked && _autoResizeComplete)
            {
                // The user is resizing the form
                autoSizeWindowCheckbox.Checked = false;
            }

            ResizeComponents();
            UpdatePreviewImages();

            base.OnResize(e);
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

        private void descriptionLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            GotoLink();
        }

        private void autoSizeWindowCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            if (autoSizeWindowCheckbox.Checked)
                ResizeComponents();
        }

        private void textBoxNext_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && ActiveForm == this)
            {
                Continue();
            }
        }
    }
}
