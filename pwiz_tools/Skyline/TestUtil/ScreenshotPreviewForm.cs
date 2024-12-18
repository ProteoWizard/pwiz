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
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil.Properties;
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
        string ImageUrl { get; }
        string FileToShow { get; }
        string FileToSave { get; }
        Control ScreenshotControl { get; }
        bool FullScreen { get; }
        Func<Bitmap, Bitmap> ProcessShot { get; }
    }

    public partial class ScreenshotPreviewForm : Form
    {
        private const string END_TEST_TEXT = "End";
        private const string PAUSE_TEXT = "&Pause";
        private const string PAUSE_TIP_TEXT = "Pause (Shift-F5)";

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
        private string _imageUrl;
        private string _fileToShow;
        private string _fileToSave;
        private ScreenshotValues _screenshotValues;
        private Bitmap _oldScreenshot;
        private string _fileLoaded;
        private Bitmap _newScreenshot;
        private bool _screenshotTaken;
        private bool? _oldAndNewMatch;
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
            public bool IsReadyForScreenshot => StopNum == CurrentNum;
        }
        #endregion

        public ScreenshotPreviewForm(IPauseTestController pauseTestController, ScreenshotManager screenshotManager)
        {
            _pauseTestController = pauseTestController;
            _screenshotManager = screenshotManager;

            InitializeComponent();
            
            Icon = Resources.camera;

            _defaultContinueText = continueBtn.Text;
            _defaultContinueTipText = toolStripContinue.ToolTipText;
            _defaultImageSourceTipText = helpTip.GetToolTip(buttonImageSource);

            UpdateImageSourceButtons();

            if (TestUtilSettings.Default.ShowTextButtons)
                toolStrip.Visible = false;
            else
                splitBar.Visible = false;

            // Unfortunately there is not enough information about the image sizes to
            // the starting location right here, but this is better than using the Windows default
            labelOldSize.Text = labelNewSize.Text = string.Empty;
            StartPosition = FormStartPosition.Manual;
            var savedLocation = TestUtilSettings.Default.PreviewFormLocation;
            if (!TestUtilSettings.Default.ManualSizePreview)
            {
                Location = Equals(_screenshotManager.GetScreenshotScreen(), Screen.FromPoint(savedLocation))
                    ? GetBestLocation()
                    : savedLocation;
            }
            else
            {
                Location = savedLocation;
                autoSizeWindowCheckbox.Checked = false;
                var savedSize = TestUtilSettings.Default.PreviewFormSize;
                if (!savedSize.IsEmpty)
                    Size = savedSize;
                FormEx.ForceOnScreen(this);
                if (TestUtilSettings.Default.PreviewFormMaximized)
                    WindowState = FormWindowState.Maximized;
            }
        }

        private readonly string _defaultContinueText;    // Store for later
        private readonly string _defaultContinueTipText; // Store for later
        private readonly string _defaultImageSourceTipText; // Store for later

        /// <summary>
        /// To be called by the <see cref="IPauseTestController"/> when switching modes or entering new pause.
        /// </summary>
        public void ShowOrUpdate()
        {
            lock (_lock)
            {
                _screenshotNum = _pauseTestController.ScreenshotNum;
                _description = _pauseTestController.Description;
                _linkUrl = _pauseTestController.LinkUrl;
                _imageUrl = _pauseTestController.ImageUrl;
                _fileToSave = _pauseTestController.FileToSave;
                _screenshotValues = new ScreenshotValues(_pauseTestController.ScreenshotControl,
                    _pauseTestController.FullScreen, _pauseTestController.ProcessShot);
                if (!Equals(_fileToShow, _pauseTestController.FileToShow))
                {
                    _fileToShow = _pauseTestController.FileToShow;
                    _fileLoaded = null;
                }

                // If there is not yet any progress, create a single step progress instance that is complete
                _nextScreenshotProgress ??= new NextScreenshotProgress(_screenshotNum - 1, _screenshotNum);
                _nextScreenshotProgress.CurrentNum = _screenshotNum;

                if (_nextScreenshotProgress.IsReadyForScreenshot)
                {
                    _screenshotTaken = false;
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
                }
            }

            // The wait below must happen outside to lock above or risk deadlocking
            _screenshotPreviewHandleReadyEvent.WaitOne(); // Block until the handle is created

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
        // CONSIDER: This doesn't really cover the case where the current thing is loaded but not from the current source
        private bool IsLoaded { get { lock (_lock) { return Equals(_fileLoaded, _fileToShow) || Equals(_fileLoaded, _imageUrl); } } }
        private bool IsWaiting { get { lock (_lock) { return _nextScreenshotProgress is { IsReadyForScreenshot: false }; } } }
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
                UpdateTools();
                UpdatePreviewImages();
            }

            if (IsLoaded)
            {
                ResizeComponents();
            }

            if (HasBackgroundWork)
            {
                bool showWebImage = TestUtilSettings.Default.ShowWebImage;
                ActionUtil.RunAsync(() => UpdateScreenshotsAsync(showWebImage));
            }
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
                helpTip.SetToolTip(oldScreenshotLabel, _fileLoaded);
                bool isPlaceholder = string.IsNullOrEmpty(_fileLoaded);
                SetPreviewSize(labelOldSize, !isPlaceholder ? _oldScreenshot : null);
                SetPreviewImage(oldScreenshotPictureBox, _oldScreenshot, isPlaceholder);
                helpTip.SetToolTip(newScreenshotLabel, _screenshotTaken ? _description : null);
                SetPreviewSize(labelNewSize, _screenshotTaken ? _newScreenshot : null);
                SetPreviewImage(newScreenshotPictureBox, _newScreenshot, !_screenshotTaken);
                if (!_oldAndNewMatch.HasValue)
                {
                    pictureMatching.Visible = false;
                    labelOldSize.Left = pictureMatching.Left;
                }
                else
                {
                    pictureMatching.Visible = true;
                    labelOldSize.Left = pictureMatching.Right;
                    var bmpDiff = _oldAndNewMatch.Value
                        ? Skyline.Properties.Resources.Peak
                        : Skyline.Properties.Resources.NoPeak;
                    bmpDiff.MakeTransparent(Color.White);
                    pictureMatching.Image = bmpDiff;
                }
            }
        }

        private static void SetPreviewSize(Label labelSize, Bitmap screenshot)
        {
            if (screenshot == null)
                labelSize.Text = string.Empty;
            else
            {
                lock (screenshot)
                {
                    labelSize.Text = $@"{screenshot.Width} x {screenshot.Height}px";
                }
            }
        }

        private void UpdateTools()
        {
            UpdateProgress();
            UpdateToolbar();
            UpdateToolStrip();
        }

        private void UpdateToolStrip()
        {
            // Update the description
            toolStripDescription.Text =
                toolStripDescription.ToolTipText =
                    _description;
            toolStripGotoWeb.Enabled = _linkUrl != null;

            // Update next text box
            UpdateNextTextBox(toolStripTextBoxNext, toolStripLabelNext);

            // Update the buttons
            var continueButtonImage = Resources.continue_test;
            var continueTipText = _defaultContinueTipText;
            if (progressBar.Visible)
            {
                if (_nextScreenshotProgress.StopNum - _screenshotNum > 1)
                {
                    continueButtonImage = Resources.pause;
                    continueTipText = PAUSE_TIP_TEXT;
                }
                else
                {
                    continueButtonImage = null;
                    continueTipText = null;
                }
            }

            toolStripContinue.Image = continueButtonImage ?? Resources.continue_test;
            toolStripContinue.ToolTipText = continueTipText ?? _defaultContinueTipText;
            toolStripContinue.Enabled = continueButtonImage != null;

            toolStripRefresh.Enabled = !progressBar.Visible;
            toolStripSave.Enabled = toolStripSaveAndContinue.Enabled = IsScreenshotTaken;
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

            // Update next text box
            UpdateNextTextBox(textBoxNext, labelNext);

            // Update the buttons
            string continueButtonText = _defaultContinueText;
            if (progressBar.Visible)
            {
                continueButtonText = _nextScreenshotProgress.StopNum - _screenshotNum > 1 ? PAUSE_TEXT : null;
            }

            continueBtn.Text = continueButtonText ?? _defaultContinueText;
            continueBtn.Enabled = continueButtonText != null;

            refreshBtn.Enabled = !progressBar.Visible;
            saveScreenshotBtn.Enabled = saveScreenshotAndContinueBtn.Enabled = IsScreenshotTaken;
        }

        private void UpdateNextTextBox(TextControl textBox, EnableControl label)
        {
            if (progressBar.Visible)
            {
                textBox.Enabled = label.Enabled = false;
                textBox.Text = _nextScreenshotProgress.StopNum.ToString();
            }
            else
            {
                int nextScreenshot = _screenshotNum + 1;
                bool nextExists = File.Exists(_screenshotManager.ScreenshotSourceFile(nextScreenshot));

                textBox.Text = nextExists ? nextScreenshot.ToString() : END_TEST_TEXT;
                textBox.Enabled = nextExists;
                label.Enabled = true;
            }
        }

        #region Helper classes for Next text boxes
        private abstract class EnableControl
        {
            protected EnableControl(object control)
            {
                Control = control;
            }

            protected object Control { get; }

            public abstract bool Enabled { set; }
            public static implicit operator EnableControl(Label c) => new LabelControl(c);
            public static implicit operator EnableControl(ToolStripLabel c) => new ToolStripLabelControl(c);
        }

        private abstract class TextControl : EnableControl
        {
            protected TextControl(object control) : base(control) { }

            public abstract string Text { set; }
            public abstract void FocusAll();

            public static implicit operator TextControl(TextBox c) => new TextBoxControl(c);
            public static implicit operator TextControl(ToolStripTextBox c) => new ToolStripTextBoxControl(c);
        }

        private class TextBoxControl : TextControl
        {
            public TextBoxControl(TextBox control) : base(control) { }
            private TextBox TextBox => (TextBox)Control;
            public override string Text { set => TextBox.Text = value; }
            public override bool Enabled { set => TextBox.Enabled = value; }
            public override void FocusAll()
            {
                TextBox.SelectAll();
                TextBox.Focus();
            }
        }

        private class LabelControl : EnableControl
        {
            public LabelControl(Label control) : base(control) { }
            private Label Label => (Label)Control;
            public override bool Enabled { set => Label.Enabled = value; }
        }

        private class ToolStripTextBoxControl : TextControl
        {
            public ToolStripTextBoxControl(ToolStripTextBox control) : base(control) { }
            private ToolStripTextBox TextBox => (ToolStripTextBox)Control;
            public override string Text { set => TextBox.Text = value; }
            public override bool Enabled { set => TextBox.Enabled = value; }
            public override void FocusAll()
            {
                TextBox.SelectAll();
                TextBox.Focus();
            }
        }

        private class ToolStripLabelControl : EnableControl
        {
            public ToolStripLabelControl(ToolStripLabel control) : base(control) { }
            private ToolStripLabel Label => (ToolStripLabel)Control;
            public override bool Enabled { set => Label.Enabled = value; }
        }
        #endregion

        private void UpdateProgress()
        {
            // Update the progress bar
            if (_nextScreenshotProgress == null)
            {
                progressBar.Visible = progressBar.Enabled = false;
            }
            else
            {
                if (_nextScreenshotProgress.PercentDone == 100 || _nextScreenshotProgress.TotalToNext > 1)
                {
                    progressBar.Style = ProgressBarStyle.Continuous;
                    progressBar.Value = _nextScreenshotProgress.PercentDone;
                }
                else
                {
                    progressBar.Style = ProgressBarStyle.Marquee;
                }

                if (!progressBar.Visible)
                {
                    progressBar.Visible = progressBar.Enabled = true;
                    progressBar.Left = newScreenshotLabel.Left;
                    progressBar.Width = newScreenshotLabel.Width;
                }
            }
        }

        private void UpdateScreenshotsAsync(bool showWebImage)
        {
            Assume.IsTrue(InvokeRequired);  // Expecting this to run on a background thread. Use ActionUtil.RunAsync()

            Bitmap oldScreenshot;
            string fileToShow, imageUrl, fileLoaded;

            lock (_lock)
            {
                fileToShow = _fileToShow;
                fileLoaded = _fileLoaded;
                imageUrl = showWebImage ? _imageUrl : null;
                oldScreenshot = _oldScreenshot;
            }

            string fileToLoad = imageUrl ?? fileToShow;
            if (!Equals(fileLoaded, fileToLoad))
            {
                oldScreenshot = LoadScreenshot(fileToShow, imageUrl);
                fileLoaded = fileToLoad;
            }

            lock (_lock)
            {
                _oldScreenshot = oldScreenshot;
                _fileLoaded = fileLoaded;
            }

            lock (_screenshotManager)
            {
                Bitmap newScreenshot;
                bool shotTaken, waitingForScreenshot;
                bool? oldAndNewMatch = null;
                ScreenshotValues screenshotValues;

                lock (_lock)
                {
                    screenshotValues = _screenshotValues;
                    shotTaken = _screenshotTaken;
                    newScreenshot = _newScreenshot;
                    waitingForScreenshot = IsWaiting;
                }

                // Only take a new screenshot when the test is ready
                if (waitingForScreenshot)
                {
                    newScreenshot = Resources.progress;
                }
                else
                {
                    if (!shotTaken)
                    {
                        newScreenshot = TakeScreenshot(screenshotValues);
                        shotTaken = true;
                    }

                    Bitmap diffImage;
                    lock (oldScreenshot)
                    {
                        lock (newScreenshot)
                        {
                            diffImage = DiffScreenshots(oldScreenshot, newScreenshot);
                        }
                    }
                    if (!ReferenceEquals(diffImage, _oldScreenshot))
                    {
                        oldAndNewMatch = false;
                    }
                    else if (oldScreenshot == null || oldScreenshot.Size != newScreenshot.Size)
                    {
                        oldAndNewMatch = false;
                    }
                    else if (!ReferenceEquals(newScreenshot, Resources.noscreenshot))
                    {
                        oldAndNewMatch = true;
                    }
                    oldScreenshot = diffImage;
                }

                lock (_lock)
                {
                    _newScreenshot = newScreenshot;
                    _oldScreenshot = oldScreenshot;
                    _oldAndNewMatch = oldAndNewMatch;
                    _screenshotTaken = shotTaken;
                    if (shotTaken)
                        _nextScreenshotProgress = null;    // Done waiting for the next screenshot
                }
            }

            FormStateChangedBackground();
        }

        private Bitmap DiffScreenshots(Bitmap bmpOld, Bitmap bmpNew)
        {
            if (bmpNew == null || bmpOld == null || bmpNew.Size != bmpOld.Size)
                return bmpOld;

            try
            {
                return HighlightDifferences(bmpOld, bmpNew, Color.Red);
            }
            catch (Exception e)
            {
                this.BeginInvoke((Action)(() => PreviewMessageDlg.ShowWithException(this,
                    "Failed to diff bitmaps.", e)));
                return bmpOld;
            }
        }

        public static Bitmap HighlightDifferences(Bitmap bmpOld, Bitmap bmpNew, Color highlightColor, int alpha = 128)
        {
            var result = new Bitmap(bmpOld.Width, bmpOld.Height);

            bool diffFound = false;
            for (int y = 0; y < bmpOld.Height; y++)
            {
                for (int x = 0; x < bmpOld.Width; x++)
                {
                    var pixel1 = bmpOld.GetPixel(x, y);
                    var pixel2 = bmpNew.GetPixel(x, y);

                    if (pixel1 != pixel2)
                    {
                        var blendedColor = Color.FromArgb(
                            alpha,
                            highlightColor.R * alpha / 255 + pixel1.R * (255 - alpha) / 255,
                            highlightColor.G * alpha / 255 + pixel1.G * (255 - alpha) / 255,
                            highlightColor.B * alpha / 255 + pixel1.B * (255 - alpha) / 255
                        );
                        result.SetPixel(x, y, blendedColor);
                        diffFound = true;
                    }
                    else
                    {
                        result.SetPixel(x, y, pixel1);
                    }
                }
            }

            return diffFound ? result : bmpOld;
        }

        private struct ScreenshotValues
        {
            public static readonly ScreenshotValues Empty = new ScreenshotValues(null, false, null);

            public ScreenshotValues(Control control, bool fullScreen, Func<Bitmap, Bitmap> processShot)
            {
                Control = control;
                FullScreen = fullScreen;
                ProcessShot = processShot;
            }

            public Control Control { get; }
            public bool FullScreen { get; }
            public Func<Bitmap, Bitmap> ProcessShot { get; }
        }

        private Bitmap TakeScreenshot(ScreenshotValues values)
        {
            if (Equals(values, ScreenshotValues.Empty))
            {
                return Resources.noscreenshot;
            }

            var control = values.Control;
            try
            {
                _screenshotManager.ActivateScreenshotForm(control);
                return _screenshotManager.TakeShot(control, values.FullScreen, null, values.ProcessShot);
            }
            catch (Exception e)
            {
                this.BeginInvoke((Action)(() => PreviewMessageDlg.ShowWithException(this,
                    "Failed attempting to take a screenshot.", e)));
                return Resources.noscreenshot;
            }
        }

        private Bitmap LoadScreenshot(string file, string uri)
        {
            try
            {
                byte[] imageBytes;
                if (uri == null)
                    imageBytes = File.ReadAllBytes(file);
                else
                {
                    using var webClient = new WebClient();
                    using var fileSaverTemp = new FileSaver(file);  // Temporary. Never saved
                    webClient.DownloadFile(uri, fileSaverTemp.SafeName);
                    imageBytes = File.ReadAllBytes(fileSaverTemp.SafeName);
                }
                var existingImageMemoryStream = new MemoryStream(imageBytes);
                return new Bitmap(existingImageMemoryStream);
            }
            catch (Exception e)
            {
                this.BeginInvoke((Action) (() => PreviewMessageDlg.ShowWithException(this, 
                    string.Format("Failed to load a bitmap from {0}.", uri ?? file), e)));
                var failureBmp = Resources.DiskFailure;
                failureBmp.MakeTransparent(Color.White);
                return failureBmp;
            }
        }

        private void SetPreviewImage(PictureBox previewBox, Bitmap screenshot, bool isPlaceHolder)
        {
            var newImage = screenshot;
            if (screenshot != null && !isPlaceHolder)
            {
                lock (screenshot)
                {
                    var containerSize = !autoSizeWindowCheckbox.Checked ? previewBox.Size : Size.Empty;
                    var screenshotSize = CalcBitmapSize(screenshot, containerSize);
                    // Always make a copy to avoid having PictureBox lock the bitmap
                    // which can cause issues with future image diffs
                    newImage = new Bitmap(screenshot, screenshotSize);
                }
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

        private void ResizeComponents()
        {
            if (WindowState == FormWindowState.Minimized)
                return;

            try
            {
                // Just to be extra safe about splitter panel proportions which should be 50/50
                // This line makes it a hard rule of this form
                previewSplitContainer.SplitterDistance = previewSplitContainer.Width / 2;
            }
            catch (InvalidOperationException)
            {
                // Do nothing. This can happen when the window is minimized.
            }

            if (!autoSizeWindowCheckbox.Checked)
                return;

            var autoSize = CalcAutoSize();
            if (autoSize.IsEmpty || ClientSize == autoSize)
                return;

            _autoResizeComplete = false;
            WindowState = FormWindowState.Normal;   // Make sure the window is not maximized
            ClientSize = autoSize;
            _autoResizeComplete = true;

            if (_screenshotManager.GetScreenshotScreen().Equals(Screen.FromControl(this)))
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
                if (_screenshotManager.IsOverlappingScreenshot(Bounds))
                    Hide();
                else if (!stopAtNexScreenshot)
                    Show();
            }
        }

        private const int WINDOW_SPACING = 20;

        private Point GetBestLocation()
        {
            var boundsRect = Bounds;
            var screenshotRect = _screenshotManager.GetScreenshotBounds();
            var screenBounds = _screenshotManager.GetScreenshotScreenBounds();
            var rightLocation = new Point(screenshotRect.Right + WINDOW_SPACING, screenshotRect.Top);
            boundsRect.Location = rightLocation;
            var onscreenRight = Rectangle.Intersect(boundsRect, screenBounds);
            var belowLocation = new Point(screenshotRect.Left, screenshotRect.Bottom + WINDOW_SPACING);
            boundsRect.Location = belowLocation;
            var onscreenBelow = Rectangle.Intersect(boundsRect, screenBounds);
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
                // May want this calculation to be possible before the form is fully shown. That means
                // it cannot be based on the Visible property, since it is false when the form is not visible
                bool showToolbar = TestUtilSettings.Default.ShowTextButtons;
                var minFormHeight = Math.Max(newImageSize.Height, oldImageSize.Height) +
                                    (showToolbar ? splitBar.Height : toolStrip.Height) + 
                                    oldScreenshotLabelPanel.Height + CELL_PADDING * 2;
                var minClientSize = MinimumSize - (Size - ClientSize);
                minFormHeight = Math.Max(minFormHeight, minClientSize.Height);

                return new Size(minFormWidth, minFormHeight);
            }
        }

        private void Continue()
        {
            int minNext, curScreenshot;
            lock (_lock)
            {
                curScreenshot = _screenshotNum;
                minNext = _screenshotNum + 1;
            }

            // textBoxNext and toolStripTextBoxNext are forced to have the same values for Text.
            // So, textBoxNext can be used for checking the value but focus and selection need
            // to go to the visible control in the case of an error.
            int nextShot;
            if (Equals(textBoxNext.Text, END_TEST_TEXT))
                nextShot = FindLastShot(minNext);
            else
            {

                var helper = new MessageBoxHelper(this);
                if (!helper.ValidateNumberTextBox(textBoxNext, minNext, null, out nextShot))
                {
                    ResetAndFocusNext(minNext);
                    return;
                }

                string nextScreenshotFile = _screenshotManager.ScreenshotSourceFile(nextShot);
                if (!File.Exists(nextScreenshotFile))
                {
                    helper.ShowTextBoxError(textBoxNext, "Invalid {0} value {1}. Screenshot file {2} does not exist.", null, nextShot, nextScreenshotFile);
                    // Must be too big. So find the largest valid number.
                    nextShot = FindLastShot(minNext);
                    ResetAndFocusNext(nextShot);
                    return;
                }
            }


            lock (_lock)
            {
                _nextScreenshotProgress = new NextScreenshotProgress(curScreenshot, nextShot);
                IncrementScreenshot();
            }

            ContinueInternal();
        }

        private void ResetAndFocusNext(int nextValue)
        {
            TextControl textControl;
            if (textBoxNext.Visible)
                textControl = textBoxNext;
            else
                textControl = toolStripTextBoxNext;
            textControl.Text = nextValue.ToString();
            textControl.FocusAll();
        }

        /// <summary>
        /// Start from a given screenshot and walk forward until the file for a given number
        /// is found not to exist.
        /// </summary>
        /// <param name="startFrom">A screenshot number known to exist</param>
        /// <returns>The last number found to exist without interruption in the series</returns>
        int FindLastShot(int startFrom)
        {
            while (File.Exists(_screenshotManager.ScreenshotSourceFile(startFrom + 1)))
                startFrom++;
            return startFrom;
        }

        private void IncrementScreenshot()
        {
            lock (_lock)
            {
                _screenshotNum++;
                // Since it is not yet known what the description will be use ... and the link to the next screenshot
                // CONSIDER: Make this a ScreenShotInfo class?
                _description = _screenshotManager.ScreenshotDescription(_screenshotNum, "...");
                _fileToShow = _screenshotManager.ScreenshotSourceFile(_screenshotNum);
                _fileToSave = _screenshotManager.ScreenshotDestFile(_screenshotNum);
                _imageUrl = _screenshotManager.ScreenshotImgUrl(_screenshotNum);
                _linkUrl = _screenshotManager.ScreenshotUrl(_screenshotNum);
                _screenshotTaken = false;
            }
        }

        private void ContinueInternal()
        {
            if (Visible && _screenshotManager.IsOverlappingScreenshot(Bounds))
            {
                Hide();
            }
            else if (File.Exists(_screenshotManager.ScreenshotSourceFile(_screenshotNum)))
            {
                FormStateChanged();
            }

            _pauseTestController.Continue();
        }

        private void PauseAtNextScreenshot()
        {
            bool stateChanged = false;
            lock (_lock)
            {
                if (_nextScreenshotProgress != null && _nextScreenshotProgress.StopNum != _screenshotNum)
                {
                    _nextScreenshotProgress = new NextScreenshotProgress(_screenshotNum, _screenshotNum + 1);
                    stateChanged = true;
                }
            }

            if (stateChanged)
            {
                FormStateChanged();
            }
        }

        private bool SaveScreenshot()
        {
            if (File.Exists(_fileToSave) && !FileEx.IsWritable(_fileToSave))
            {
                PreviewMessageDlg.Show(this, TextUtil.LineSeparate(string.Format("The file {0} is locked.", _fileToSave),
                    "Check that it is not open in another program such as TortoiseIDiff."));
                return false;
            }
            try
            {
                string screenshotDir = Path.GetDirectoryName(_fileToSave) ?? string.Empty;
                Assume.IsFalse(string.IsNullOrEmpty(screenshotDir));    // Because ReSharper complains about possible null
                Directory.CreateDirectory(screenshotDir);

                _newScreenshot.Save(_fileToSave);
                return true;
            }
            catch (Exception e)
            {
                PreviewMessageDlg.ShowWithException(this, string.Format("Failed to save screenshot {0}", _fileToSave), e);
                return false;
            }
        }

        private void SaveAndContinue()
        {
            if (SaveScreenshot())
                Continue();
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

        protected override void OnMove(EventArgs e)
        {
            StoreFormBoundsState();
            base.OnMove(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e); // Let default re-layout happen

            if (WindowState != FormWindowState.Minimized)
            {
                if (autoSizeWindowCheckbox.Checked && _autoResizeComplete)
                {
                    // The user is resizing the form
                    autoSizeWindowCheckbox.Checked = false;
                }

                StoreFormBoundsState();

                ResizeComponents();
                UpdatePreviewImages();
            }
        }

        private void StoreFormBoundsState()
        {
            // Only store sizing information when not automatically resizing the form
            if (!_autoResizeComplete)
                return;

            TestUtilSettings.Default.ManualSizePreview = !autoSizeWindowCheckbox.Checked;
            if (WindowState == FormWindowState.Normal)
                TestUtilSettings.Default.PreviewFormLocation = Location;
            if (WindowState == FormWindowState.Normal)
                TestUtilSettings.Default.PreviewFormSize = Size;
            TestUtilSettings.Default.PreviewFormMaximized =
                (WindowState == FormWindowState.Maximized);
        }

        private void ToggleToolStrip()
        {
            TestUtilSettings.Default.ShowTextButtons = splitBar.Visible = toolStrip.Visible;
            toolStrip.Visible = !toolStrip.Visible;
            if (toolStrip.Visible)
                UpdateToolStrip();
            else
                UpdateToolbar();
            // Since the two are not exactly the same height, the form may need to be
            // resized if it is in auto-size mode.
            ResizeComponents();
        }

        private void ToggleImageSource()
        {
            TestUtilSettings.Default.ShowWebImage = !TestUtilSettings.Default.ShowWebImage;

            UpdateImageSourceButtons();

            lock (_lock)
            {
                _fileLoaded = null;
            }

            FormStateChanged();
        }

        private void UpdateImageSourceButtons()
        {
            if (TestUtilSettings.Default.ShowWebImage)
            {
                buttonImageSource.Image = Resources.websource;
                helpTip.SetToolTip(buttonImageSource,
                    TextUtil.LineSeparate(_defaultImageSourceTipText.ReadLines().First(),
                        "Current: Web"));
            }
            else
            {
                buttonImageSource.Image = Resources.save;
                helpTip.SetToolTip(buttonImageSource, _defaultImageSourceTipText);
            }
        }

        private void continueBtn_Click(object sender, EventArgs e)
        {
            if (IsWaiting)
                PauseAtNextScreenshot();
            else
                Continue();
        }

        private void saveScreenshotBtn_Click(object sender, EventArgs e)
        {
            SaveScreenshot();
        }

        private void saveScreenshotAndContinueBtn_Click(object sender, EventArgs e)
        {
            SaveAndContinue();
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
            if (!toolStrip.Visible || toolStripAutoSize.Checked != autoSizeWindowCheckbox.Checked)
            {
                toolStripAutoSize.Checked = autoSizeWindowCheckbox.Checked;
                if (autoSizeWindowCheckbox.Checked)
                    ResizeComponents();

                StoreFormBoundsState();
            }
        }

        private void textBoxNext_KeyDown(object sender, KeyEventArgs e)
        {
            // CONSIDER: Move to Form KeyPreview?
            if (e.KeyCode == Keys.Enter && ActiveForm == this)
            {
                Continue();
            }
        }

        private void toolStripContinue_Click(object sender, EventArgs e)
        {
            if (IsWaiting)
                PauseAtNextScreenshot();
            else
                Continue();
        }

        private void toolStripRefresh_Click(object sender, EventArgs e)
        {
            RefreshScreenshots();
        }

        private void toolStripSave_Click(object sender, EventArgs e)
        {
            SaveScreenshot();
        }

        private void toolStripSaveAndContinue_Click(object sender, EventArgs e)
        {
            SaveAndContinue();
        }

        private void toolStripGotoWeb_Click(object sender, EventArgs e)
        {
            GotoLink();
        }

        private void toolStripAutoSize_CheckedChanged(object sender, EventArgs e)
        {
            if (!splitBar.Visible || autoSizeWindowCheckbox.Checked != toolStripAutoSize.Checked)
            {
                autoSizeWindowCheckbox.Checked = toolStripAutoSize.Checked;
                if (toolStripAutoSize.Checked)
                    ResizeComponents();

                StoreFormBoundsState();
            }
        }

        private void toolStripSwitchToToolbar_Click(object sender, EventArgs e)
        {
            ToggleToolStrip();
        }

        private void buttonSwitchToToolStrip_Click(object sender, EventArgs e)
        {
            ToggleToolStrip();
        }

        private void textBoxNext_TextChanged(object sender, EventArgs e)
        {
            if (!Equals(textBoxNext.Text, toolStripTextBoxNext.Text))
                toolStripTextBoxNext.Text = textBoxNext.Text;
        }

        private void toolStripTextBoxNext_TextChanged(object sender, EventArgs e)
        {
            if (!Equals(textBoxNext.Text, toolStripTextBoxNext.Text))
                textBoxNext.Text = toolStripTextBoxNext.Text;
        }

        private void buttonImageSource_Click(object sender, EventArgs e)
        {
            ToggleImageSource();
        }

        private void ScreenshotPreviewForm_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    if (textBoxNext.Focused || toolStripTextBoxNext.Focused)
                        Focus();
                    else
                        Close();
                    e.Handled = true;
                    break;
                case Keys.Tab:
                    if (e.Control)
                    {
                        ToggleImageSource();
                        e.Handled = true;
                    }
                    break;
                case Keys.F5:
                    if (e.Shift)
                    {
                        if (IsWaiting)
                            PauseAtNextScreenshot();
                    }
                    else
                    {
                        if (IsComplete)
                            Continue();
                    }
                    e.Handled = true;
                    break;
                case Keys.F6:
                    if (IsComplete)
                        SaveAndContinue();
                    e.Handled = true;
                    break;
                case Keys.S:
                    if (e.Control)
                    {
                        if (IsComplete)
                            SaveScreenshot();
                        e.Handled = true;
                    }
                    break;
                case Keys.R:
                    if (e.Control)
                    {
                        RefreshScreenshots();
                        e.Handled = true;
                    }
                    break;
                case Keys.G:
                    if (e.Control)
                    {
                        GotoLink();
                        e.Handled = true;
                    }
                    break;
                case Keys.C:
                    if (e.Control)
                    {
                        if (e.Shift)
                            CopyBitmap(_oldScreenshot);
                        else
                            CopyBitmap(_newScreenshot);
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void CopyBitmap(Bitmap bitmap)
        {
            try
            {
                Clipboard.SetImage(bitmap);
            }
            catch (Exception e)
            {
                PreviewMessageDlg.ShowWithException(this, "Failed clipboard operation.", e);
            }
        }

        /// <summary>
        /// Avoid creating a type that may be recognized by a test and cause it to fail.
        /// </summary>
        private class PreviewMessageDlg : AlertDlg
        {
            public static void Show(IWin32Window parent, string message)
            {
                new PreviewMessageDlg(message).ShowAndDispose(parent);
            }

            public static void ShowWithException(IWin32Window parent, string message, Exception exception)
            {
                new PreviewMessageDlg(message) { Exception = exception }.ShowAndDispose(parent);
            }

            private PreviewMessageDlg(string message) : base(message, MessageBoxButtons.OK)
            {
                GetModeUIHelper().IgnoreModeUI = true; // No "peptide"->"molecule" translation
            }
        }
    }
}
