/*
 * Original authors: Eduardo Armendariz <wardough .at. uw.edu>,
 *                   Brendan MacLean <brendanx .at. uw.edu>
 *                   MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Net.Http;
using System.Threading;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil.Properties;
using pwiz.Skyline.Controls;
using TestRunnerLib.PInvoke;

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
        private const string PAUSE_TIP_TEXT = "Pause (Shift-F5)";

        private const int SCREENSHOT_MAX_WIDTH = 800; // doubled as side by side
        private const int SCREENSHOT_MAX_HEIGHT = 800;

        private readonly ScreenshotManager _screenshotManager;
        private readonly IPauseTestController _pauseTestController;

        private Thread _screenshotPreviewThread;
        private readonly ManualResetEvent _screenshotPreviewHandleReadyEvent = new ManualResetEvent(false);

        private readonly MultiFormActivator _activator;

        // these members should only be accessed in a block which locks on _lock (is this necessary for all?)
        #region synchronized members
        private readonly object _lock = new object();
        private int _screenshotNum;
        private string _description;
        private ScreenshotFile _fileToShow;
        private string _fileToSave; // May be different from the file to show for a language that doesn't exist yet
        private ScreenshotValues _screenshotValues;
        private OldScreenshot _oldScreenshot;
        private NewScreenshot _newScreenshot;
        private ScreenshotDiff _diff;
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

        private RichTextBox _rtfDiff;

        public ScreenshotPreviewForm(IPauseTestController pauseTestController, ScreenshotManager screenshotManager)
        {
            _pauseTestController = pauseTestController;
            _screenshotManager = screenshotManager;
            _oldScreenshot = new OldScreenshot();
            _newScreenshot = new NewScreenshot();

            InitializeComponent();
            
            Icon = Resources.camera;
            toolStripPickColorButton.SelectedColor = HighlightColor;

            _defaultContinueTipText = toolStripContinue.ToolTipText;
            _defaultImageSourceTipText = helpTip.GetToolTip(buttonImageSource);

            _activator = new MultiFormActivator();

            UpdateImageSourceButtons();

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
                toolStripAutoSize.Checked = false;
                var savedSize = TestUtilSettings.Default.PreviewFormSize;
                if (!savedSize.IsEmpty)
                    Size = savedSize;
                ForceOnScreen();
                if (TestUtilSettings.Default.PreviewFormMaximized)
                    WindowState = FormWindowState.Maximized;
            }
        }

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
                _fileToSave = _pauseTestController.FileToSave;
                _screenshotValues = new ScreenshotValues(_pauseTestController.ScreenshotControl,
                    _pauseTestController.FullScreen, _pauseTestController.ProcessShot);
                if (!Equals(_fileToShow?.Path, _pauseTestController.FileToShow))
                {
                    _fileToShow = new ScreenshotFile(_pauseTestController.FileToShow);
                    _oldScreenshot.FileLoaded = null;
                }

                // If there is not yet any progress, create a single step progress instance that is complete
                _nextScreenshotProgress ??= new NextScreenshotProgress(_screenshotNum - 1, _screenshotNum);
                _nextScreenshotProgress.CurrentNum = _screenshotNum;

                if (_nextScreenshotProgress.IsReadyForScreenshot)
                {
                    _newScreenshot.IsTaken = false;
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

            // Ideally, this would use FormUtil.OpenForms, but this works pretty well and including
            // all open forms gets tricky with cross-thread operations and choosing top level forms
            var activationForm = ActivationForm;
            _activator.Reset(this, activationForm);
            if (activationForm is FloatingWindow)
                _activator.AddForm(activationForm.Owner);   // Add the SkylineWindow too

            FormStateChangedBackground();
        }

        private Form ActivationForm
        {
            get
            {
                var parentForm = _pauseTestController.ScreenshotControl.FindForm();
                if (parentForm is DockableForm dockableForm)    // A dockable form is never the top level
                    parentForm = dockableForm.Pane.FindForm();
                return parentForm;
            }
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
        private bool IsLoaded { get { lock (_lock) { return _oldScreenshot.IsCurrent(_fileToShow, OldImageSource); } } }
        private bool IsWaiting { get { lock (_lock) { return _nextScreenshotProgress is { IsReadyForScreenshot: false }; } } }
        private bool IsScreenshotTaken { get { lock (_lock) { return _newScreenshot.IsTaken; } } }

        private void RefreshScreenshots()
        {
            lock (_lock)
            {
                _oldScreenshot.FileLoaded = null;
                _newScreenshot.IsTaken = false;
            }

            FormStateChanged();
        }

        private void RefreshOldScreenshot()
        {
            lock (_lock)
            {
                _oldScreenshot.FileLoaded = null;
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
                UpdateScreenshotsBackground();
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
                UpdateImageSourceButtons();
                helpTip.SetToolTip(oldScreenshotLabel, _oldScreenshot.FileLoaded);
                SetPreviewSize(labelOldSize, _oldScreenshot);
                SetPreviewImage(oldScreenshotPictureBox, _oldScreenshot, _diff);
                helpTip.SetToolTip(newScreenshotLabel, _newScreenshot.IsTaken ? _description : null);
                SetPreviewSize(labelNewSize, _newScreenshot);
                SetPreviewImage(newScreenshotPictureBox, _newScreenshot);
                ShowImageDiff();
            }
        }

        private void ShowImageDiff()
        {
            bool showOldPictureBox = true;
            if (_diff == null)
            {
                pictureMatching.Visible = false;
                labelOldSize.Left = pictureMatching.Left;
                labelOldSize.ForeColor = Color.Black;
            }
            else
            {
                pictureMatching.Visible = true;
                labelOldSize.Left = pictureMatching.Right;
                bool matching = !_diff.IsDiff;
                var bmpDiff = matching ? Skyline.Properties.Resources.Peak : Skyline.Properties.Resources.NoPeak;
                bmpDiff.MakeTransparent(Color.White);
                pictureMatching.Image = bmpDiff;
                if (matching)
                {
                    labelOldSize.ForeColor = Color.Green;
                }
                else
                {
                    labelOldSize.ForeColor = Color.Red;
                    labelOldSize.Text += _diff.DiffText;
                    bool imagesMatch = !_diff.SizesDiffer && !_diff.PixelsDiffer;
                    if (imagesMatch)
                    {
                        _diff.ShowBinaryDiff(EnsureBinaryDiffControl());
                    }

                    showOldPictureBox = !imagesMatch;
                }
            }
            oldScreenshotPictureBox.Visible = showOldPictureBox;
            if (_rtfDiff != null)
                _rtfDiff.Visible = !showOldPictureBox;
        }

        private RichTextBox EnsureBinaryDiffControl()
        {
            if (_rtfDiff == null)
            {
                _rtfDiff = new RichTextBox
                {
                    Dock = DockStyle.Fill,
                    Font = new Font("Courier New", 10), // Use monospaced font for better alignment
                    ReadOnly = true,
                    WordWrap = false,
                    TabIndex = oldScreenshotPictureBox.TabIndex
                };
                var controlList = oldScreenshotPictureBox.Parent.Controls;
                controlList.Add(_rtfDiff);
                controlList.SetChildIndex(_rtfDiff, controlList.IndexOf(oldScreenshotPictureBox));
            }

            return _rtfDiff;
        }

        private static void SetPreviewSize(Label labelSize, ScreenshotInfo screenshot)
        {
            var image = screenshot.Image;
            if (image == null || screenshot.IsPlaceholder)
                labelSize.Text = string.Empty;
            else
            {
                lock (image)
                {
                    labelSize.Text = $@"{image.Width} x {image.Height}px";
                }
            }
        }

        private void UpdateTools()
        {
            UpdateProgress();
            UpdateToolStrip();
        }

        private void UpdateToolStrip()
        {
            // Update the description
            toolStripDescription.Text =
                toolStripDescription.ToolTipText =
                    _description;
            toolStripGotoWeb.Enabled = _fileToShow != null;

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

        private void UpdateNextTextBox(ToolStripTextBox textBox, ToolStripLabel label)
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

        private void UpdateScreenshotsBackground()
        {
            var imageSource = OldImageSource;   // Get this value now
            var highlightColor = HighlightColor;
            ActionUtil.RunAsync(() => UpdateScreenshotsAsync(imageSource, highlightColor), "Preview update async");
        }

        private void UpdateScreenshotsAsync(ImageSource oldImageSource, Color highlightColor)
        {
            Assume.IsTrue(InvokeRequired);  // Expecting this to run on a background thread. Use ActionUtil.RunAsync()

            ScreenshotFile fileToShow;
            ScreenshotInfo oldScreenshot;
            string fileLoaded;

            lock (_lock)
            {
                fileToShow = _fileToShow;
                fileLoaded = _oldScreenshot.FileLoaded;
                oldScreenshot = new ScreenshotInfo(_oldScreenshot);
            }

            string oldFileDescription = fileToShow.GetDescription(oldImageSource);
            if (!Equals(fileLoaded, oldFileDescription))
            {
                oldScreenshot = LoadScreenshot(fileToShow, oldImageSource);
                fileLoaded = oldFileDescription;
            }

            lock (_lock)
            {
                if (!Equals(fileToShow, _fileToShow))
                    return;
                _oldScreenshot = new OldScreenshot(oldScreenshot, fileLoaded);
                _diff = null;
            }

            lock (_screenshotManager)
            {
                ScreenshotInfo newScreenshot;
                bool shotTaken, waitingForScreenshot;
                ScreenshotValues screenshotValues;
                ScreenshotDiff diff = null;

                lock (_lock)
                {
                    screenshotValues = _screenshotValues;
                    shotTaken = _newScreenshot.IsTaken;
                    newScreenshot = new ScreenshotInfo(_newScreenshot);
                    waitingForScreenshot = IsWaiting;
                }

                // Only take a new screenshot when the test is ready
                if (waitingForScreenshot)
                {
                    newScreenshot = new ScreenshotInfo(Resources.progress);
                }
                else
                {
                    if (!shotTaken)
                    {
                        newScreenshot = TakeScreenshot(screenshotValues);
                        shotTaken = true;
                    }

                    if (!newScreenshot.IsPlaceholder || !oldScreenshot.IsPlaceholder)
                    {
                        diff = DiffScreenshots(oldScreenshot, newScreenshot, highlightColor);
                    }
                }

                lock (_lock)
                {
                    _newScreenshot = new NewScreenshot(newScreenshot, shotTaken);
                    _diff = diff;
                    if (shotTaken)
                        _nextScreenshotProgress = null;    // Done waiting for the next screenshot
                }
            }

            FormStateChangedBackground();
        }

        private ScreenshotDiff DiffScreenshots(ScreenshotInfo oldScreenshot, ScreenshotInfo newScreenshot, Color highlightColor)
        {
            try
            {
                return new ScreenshotDiff(oldScreenshot, newScreenshot, highlightColor);
            }
            catch (Exception e)
            {
                BeginInvoke((Action)(() => ShowMessageWithException(
                    "Failed to diff bitmaps.", e)));
                return null;
            }
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

        private ScreenshotInfo TakeScreenshot(ScreenshotValues values)
        {
            var noScreenshot = new ScreenshotInfo(Resources.noscreenshot);
            if (Equals(values, ScreenshotValues.Empty))
            {
                return noScreenshot;
            }

            var control = values.Control;
            try
            {
                _screenshotManager.ActivateScreenshotForm(control);
                var newScreenshot = _screenshotManager.TakeShot(control, values.FullScreen, null, values.ProcessShot);
                return new ScreenshotInfo(_screenshotManager.SaveToMemory(newScreenshot), newScreenshot);
            }
            catch (Exception e)
            {
                BeginInvoke((Action)(() => ShowMessageWithException("Failed attempting to take a screenshot.", e)));
                return noScreenshot;
            }
        }

        private ScreenshotInfo LoadScreenshot(ScreenshotFile file, ImageSource source)
        {
            try
            {
                byte[] imageBytes;
                switch (source)
                {
                    case ImageSource.git:
                        imageBytes = GitFileHelper.GetGitFileBinaryContent(file.Path);
                        break;
                    case ImageSource.disk:
                        imageBytes = File.ReadAllBytes(file.Path);
                        break;
                    case ImageSource.web:
                    default:
                    {
                        using var httpClient = new HttpClient();
                        using var fileSaverTemp = new FileSaver(file.Path);  // Temporary. Never saved
                        var response = httpClient.GetAsync(file.UrlToDownload).Result;
                        response.EnsureSuccessStatusCode();
                        using (var contentStream = response.Content.ReadAsStreamAsync().Result)
                        using (var fileStream = new FileStream(fileSaverTemp.SafeName, FileMode.Create,
                                   FileAccess.Write, FileShare.None))
                        {
                            contentStream.CopyTo(fileStream);
                        }
                        imageBytes = File.ReadAllBytes(fileSaverTemp.SafeName);
                    }
                        break;
                }
                var ms = new MemoryStream(imageBytes);
                return new ScreenshotInfo(ms);
            }
            catch (Exception e)
            {
                BeginInvoke((Action)(() => ShowMessageWithException(
                    string.Format("Failed to load a bitmap from {0}.", file.GetDescription(source)), e)));
                var failureBmp = Resources.DiskFailure;
                failureBmp.MakeTransparent(Color.White);
                return new ScreenshotInfo(failureBmp);
            }
        }

        private void SetPreviewImage(PictureBox previewBox, ScreenshotInfo screenshot, ScreenshotDiff diff = null)
        {
            var baseImage = GetDisplayImage(diff, screenshot);
            var newImage = baseImage;
            if (baseImage != null && !screenshot.IsPlaceholder)
            {
                lock (baseImage)
                {
                    var containerSize = !toolStripAutoSize.Checked ? previewBox.Size : Size.Empty;
                    var screenshotSize = CalcBitmapSize(screenshot, containerSize);
                    // Always make a copy to avoid having PictureBox lock the bitmap
                    // which can cause issues with future image diffs
                    newImage = new Bitmap(baseImage, screenshotSize);
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

        /// <summary>
        /// Gets the appropriate image to display based on diff-only and amplification settings.
        /// </summary>
        private Bitmap GetDisplayImage(ScreenshotDiff diff, ScreenshotInfo screenshot)
        {
            if (diff == null)
                return screenshot.Image;

            int amplifyRadius = GetAmplifyRadius();
            bool diffOnly = toolStripDiffOnly.Checked;

            if (amplifyRadius > 0)
            {
                // Amplified view - fall back to normal view if no diff pixels
                var amplifiedImage = diffOnly
                    ? diff.CreateAmplifiedDiffOnlyImage(amplifyRadius)
                    : diff.CreateAmplifiedImage(amplifyRadius);
                if (amplifiedImage != null)
                    return amplifiedImage;
                // Fall through to non-amplified handling when no diff pixels
            }

            if (diffOnly)
            {
                // Diff-only view - show white rectangle if no diff pixels
                return diff.DiffOnlyImage ?? CreateWhiteImage(screenshot.ImageSize);
            }

            // Normal highlighted view
            return diff.HighlightedImage ?? screenshot.Image;
        }

        private static Bitmap CreateWhiteImage(Size size)
        {
            var result = new Bitmap(size.Width, size.Height);
            using (var g = Graphics.FromImage(result))
            {
                g.Clear(Color.White);
            }
            return result;
        }

        private int GetAmplifyRadius()
        {
            return toolStripAmplify.Checked ? 5 : 0;
        }

        private Size CalcBitmapSize(ScreenshotInfo screenshot, Size containerSize)
        {
            var startingSize = Size.Empty;
            if (screenshot == null)
                return startingSize;

            startingSize = screenshot.ImageSize;
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

            if (!toolStripAutoSize.Checked)
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
                screenshotTaken = _newScreenshot.IsTaken;
                stopAtNexScreenshot = _nextScreenshotProgress != null &&
                                      _nextScreenshotProgress.StopNum - _screenshotNum == 0;
            }

            if (!screenshotTaken)
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
                var minFormHeight = Math.Max(newImageSize.Height, oldImageSize.Height) +
                                    toolStrip.Height +
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
            if (Equals(toolStripTextBoxNext.Text, END_TEST_TEXT))
                nextShot = FindLastShot(minNext);
            else
            {

                var helper = new MessageBoxHelper(this);
                if (!helper.ValidateNumberTextBox(toolStripTextBoxNext.TextBox, minNext, null, out nextShot))
                {
                    ResetAndFocusNext(minNext);
                    return;
                }

                string nextScreenshotFile = _screenshotManager.ScreenshotSourceFile(nextShot);
                if (!File.Exists(nextScreenshotFile))
                {
                    helper.ShowTextBoxError(toolStripTextBoxNext.TextBox, "Invalid {0} value {1}. Screenshot file {2} does not exist.", null, nextShot, nextScreenshotFile);
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
            toolStripTextBoxNext.Text = nextValue.ToString();
            toolStripTextBoxNext.SelectAll();
            toolStripTextBoxNext.Focus();
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
                _fileToShow = new ScreenshotFile(_screenshotManager.ScreenshotSourceFile(_screenshotNum));
                _fileToSave = _screenshotManager.ScreenshotDestFile(_screenshotNum);
                _newScreenshot.IsTaken = false;
                _diff = null;
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

            // The MultiFormActivator can change test behavior. So, it needs to be cleared
            // before continuing the test.
            _activator.Clear();
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
            string filePath = _fileToSave;
            if (File.Exists(filePath) && !IsWritable(filePath))
            {
                ShowMessage(LineSeparate(string.Format("The file {0} is locked.", _fileToSave),
                    "Check that it is not open in another program such as TortoiseIDiff."));
                return false;
            }
            try
            {
                string screenshotDir = Path.GetDirectoryName(filePath) ?? string.Empty;
                Assume.IsFalse(string.IsNullOrEmpty(screenshotDir));    // Because ReSharper complains about possible null
                Directory.CreateDirectory(screenshotDir);

                _newScreenshot.Image.Save(filePath);
                return true;
            }
            catch (Exception e)
            {
                PreviewMessageDlg.ShowWithException(this, string.Format("Failed to save screenshot {0}", filePath), e);
                return false;
            }
        }

        private void Revert()
        {
            string filePath = _fileToSave;
            if (File.Exists(filePath) && !IsWritable(filePath))
            {
                ShowMessage(LineSeparate(string.Format("The file {0} is locked.", filePath),
                    "Check that it is not open in another program such as TortoiseIDiff."));
                return;
            }
            try
            {
                GitFileHelper.RevertFileToHead(filePath);
                RefreshOldScreenshot();
            }
            catch (Exception e)
            {
                ShowMessageWithException(string.Format("Failed to revert screenshot {0}", filePath), e);
            }
        }

        private void SaveAndContinue()
        {
            if (SaveScreenshot())
                Continue();
        }

        private void SaveAndStay()
        {
            if (SaveScreenshot())
                RefreshOldScreenshot();
        }

        private void SaveDiffImage()
        {
            try
            {
                ScreenshotFile file;
                ScreenshotDiff diff;
                lock (_lock)
                {
                    file = _fileToShow;
                    diff = _diff;
                }

                if (file == null)
                {
                    ShowMessage("No screenshot selected.");
                    return;
                }

                if (diff?.HighlightedImage == null)
                {
                    ShowMessage("No diff image available to save.");
                    return;
                }

                var aiTmpFolder = file.GetAiTmpFolder();
                if (aiTmpFolder == null)
                {
                    ShowMessage(@"Could not locate ai\.tmp folder.");
                    return;
                }

                // Ensure the folder exists
                if (!Directory.Exists(aiTmpFolder))
                {
                    Directory.CreateDirectory(aiTmpFolder);
                }

                var fileName = file.GetDiffFileName(diff.PixelCount);
                var fullPath = Path.Combine(aiTmpFolder, fileName);

                lock (diff.HighlightedImage)
                {
                    diff.HighlightedImage.Save(fullPath, ImageFormat.Png);
                }

                ShowMessage($"Diff image saved to:\n{fullPath}");
            }
            catch (Exception e)
            {
                ShowMessageWithException("Failed to save diff image.", e);
            }
        }

        private void GotoLink()
        {
            string urlInTutorial;
            lock (_lock)
            {
                urlInTutorial = _fileToShow?.UrlInTutorial;
            }
            if (!string.IsNullOrEmpty(urlInTutorial))
            {
                OpenLink(urlInTutorial);
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
                if (toolStripAutoSize.Checked && _autoResizeComplete)
                {
                    // The user is resizing the form
                    toolStripAutoSize.Checked = false;
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

            TestUtilSettings.Default.ManualSizePreview = !toolStripAutoSize.Checked;
            if (WindowState == FormWindowState.Normal)
                TestUtilSettings.Default.PreviewFormLocation = Location;
            if (WindowState == FormWindowState.Normal)
                TestUtilSettings.Default.PreviewFormSize = Size;
            TestUtilSettings.Default.PreviewFormMaximized =
                (WindowState == FormWindowState.Maximized);
        }

        private Color HighlightColor
        {
            get => Color.FromArgb(TestUtilSettings.Default.ImageDiffAlpha,
                TestUtilSettings.Default.ImageDiffColor.R,
                TestUtilSettings.Default.ImageDiffColor.G,
                TestUtilSettings.Default.ImageDiffColor.B);

            set
            {
                TestUtilSettings.Default.ImageDiffAlpha = value.A;
                TestUtilSettings.Default.ImageDiffColor = Color.FromArgb(value.R, value.G, value.B);
            }
        }

        private static readonly ImageSource[] OLD_IMAGE_SOURCES = { ImageSource.disk, ImageSource.git, ImageSource.web };

        private ImageSource OldImageSource
        {
            get => OLD_IMAGE_SOURCES[TestUtilSettings.Default.OldImageSource % OLD_IMAGE_SOURCES.Length];
            set
            {
                for (int i = 0; i < OLD_IMAGE_SOURCES.Length; i++)
                {
                    if (OLD_IMAGE_SOURCES[i] == value)
                        TestUtilSettings.Default.OldImageSource = i;
                }
            }
        }

        private void NextOldImageSource()
        {
            OldImageSource = OLD_IMAGE_SOURCES[(TestUtilSettings.Default.OldImageSource + 1) % OLD_IMAGE_SOURCES.Length];

            UpdateImageSourceButtons();

            lock (_lock)
            {
                _oldScreenshot = new OldScreenshot(_oldScreenshot, null, OldImageSource);
            }

            FormStateChanged();
        }

        private void UpdateImageSourceButtons()
        {
            if (_defaultImageSourceTipText == null)
            {
                return;
            }
            switch (OldImageSource)
            {
                case ImageSource.web:
                    buttonImageSource.Image = Resources.websource;
                    helpTip.SetToolTip(buttonImageSource,
                        LineSeparate(_defaultImageSourceTipText.Split('\n').First(),
                            "Current: Web"));
                    break;
                case ImageSource.git:
                    buttonImageSource.Image = Resources.gitsource;
                    helpTip.SetToolTip(buttonImageSource,
                        LineSeparate(_defaultImageSourceTipText.Split('\n').First(),
                            "Current: Git HEAD"));
                    break;
                case ImageSource.disk:
                default:
                    bool? changed = null;
                    var filePath = _fileToSave;
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            changed = GitFileHelper.IsModified(filePath);
                        }
                        catch (Exception)
                        {
                            // Ignore and leave changed null
                        }
                    }

                    if (!changed.HasValue)
                    {
                        buttonImageSource.Image = Resources.fileunknown;
                    }
                    else
                    {
                        buttonImageSource.Image = changed.Value ? Resources.filechanged : Resources.fileunchanged;
                    }
                    helpTip.SetToolTip(buttonImageSource, _defaultImageSourceTipText);
                    break;
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
            SaveAndStay();
        }

        private void toolStripSaveAndContinue_Click(object sender, EventArgs e)
        {
            SaveAndContinue();
        }

        private void toolStripRevert_Click(object sender, EventArgs e)
        {
            Revert();
        }

        private void toolStripGotoWeb_Click(object sender, EventArgs e)
        {
            GotoLink();
        }

        private void toolStripAutoSize_CheckedChanged(object sender, EventArgs e)
        {
            if (toolStripAutoSize.Checked)
                ResizeComponents();

            StoreFormBoundsState();
        }

        private void toolStripPickColorButton_ColorChanged(object sender, EventArgs e)
        {
            if (HighlightColor != toolStripPickColorButton.SelectedColor)
            {
                HighlightColor = toolStripPickColorButton.SelectedColor;
                if (oldScreenshotPictureBox.Visible)
                    UpdateScreenshotsBackground();
            }
        }

        private void buttonImageSource_Click(object sender, EventArgs e)
        {
            // Update menu check marks
            menuItemDisk.Checked = OldImageSource == ImageSource.disk;
            menuItemGit.Checked = OldImageSource == ImageSource.git;
            menuItemWeb.Checked = OldImageSource == ImageSource.web;

            // Show context menu below the button
            contextMenuImageSource.Show(buttonImageSource, new Point(0, buttonImageSource.Height));
        }

        private void menuItemDisk_Click(object sender, EventArgs e)
        {
            SetOldImageSource(ImageSource.disk);
        }

        private void menuItemGit_Click(object sender, EventArgs e)
        {
            SetOldImageSource(ImageSource.git);
        }

        private void menuItemWeb_Click(object sender, EventArgs e)
        {
            SetOldImageSource(ImageSource.web);
        }

        private void SetOldImageSource(ImageSource source)
        {
            if (OldImageSource == source)
                return;

            OldImageSource = source;
            UpdateImageSourceButtons();

            lock (_lock)
            {
                _oldScreenshot = new OldScreenshot(_oldScreenshot, null, OldImageSource);
            }

            FormStateChanged();
        }

        private void toolStripDiffOnly_CheckedChanged(object sender, EventArgs e)
        {
            UpdatePreviewImages();
        }

        private void toolStripAmplify_CheckedChanged(object sender, EventArgs e)
        {
            UpdatePreviewImages();
        }

        private void ScreenshotPreviewForm_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    if (toolStripTextBoxNext.Focused)
                        Focus();
                    else
                        Close();
                    e.Handled = true;
                    break;
                case Keys.Tab:
                    if (e.Control)
                    {
                        NextOldImageSource();
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
                        if (e.Alt)
                        {
                            // Ctrl+Alt+S: Save diff image to ai\.tmp
                            SaveDiffImage();
                        }
                        else if (IsComplete)
                        {
                            SaveAndStay();
                        }
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
                        Bitmap imageToCopy;
                        lock (_lock)
                        {
                            if (e.Alt)
                            {
                                // Ctrl+Alt+C: Copy diff image
                                imageToCopy = _diff?.HighlightedImage;
                                if (imageToCopy == null)
                                {
                                    ShowMessage("No diff image available to copy.");
                                    e.Handled = true;
                                    break;
                                }
                            }
                            else
                            {
                                imageToCopy = e.Shift ? _oldScreenshot.Image : _newScreenshot.Image;
                            }
                        }
                        CopyBitmap(imageToCopy);
                        e.Handled = true;
                    }
                    break;
                case Keys.Z:
                    if (e.Control)
                    {
                        Revert();
                        e.Handled = true;
                    }
                    break;
                case Keys.D:
                    toolStripDiffOnly.Checked = !toolStripDiffOnly.Checked;
                    e.Handled = true;
                    break;
                case Keys.A:
                    toolStripAmplify.Checked = !toolStripAmplify.Checked;
                    e.Handled = true;
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

        public static bool IsWritable(string path)
        {
            return FileEx.IsWritable(path);
        }

        private string LineSeparate(params string[] lines)
        {
            return TextUtil.LineSeparate(lines);
        }

        public static void OpenLink(string link)
        {
            WebHelpers.OpenLink(link);
        }

        public void ForceOnScreen()
        {
            FormEx.ForceOnScreen(this);
        }

        private void ShowMessage(string message)
        {
            PreviewMessageDlg.Show(this, message);
        }

        private void ShowMessageWithException(string message, Exception e)
        {
            PreviewMessageDlg.ShowWithException(this, message, e);
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
