/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>
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
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ImageComparer.Properties;

namespace ImageComparer
{

    public partial class ImageComparerWindow : Form
    {
        private const int SCREENSHOT_MAX_WIDTH = 800; // doubled as side by side
        private const int SCREENSHOT_MAX_HEIGHT = 800;

        // these members should only be accessed in a block which locks on _lock (is this necessary for all?)
        #region synchronized members
        private readonly object _lock = new object();
        private ScreenshotFile _fileToShow;
        private OldScreenshot _oldScreenshot;
        private OldScreenshot _newScreenshot;
        private ScreenshotDiff _diff;
        #endregion

        private RichTextBox _rtfDiff;

        public ImageComparerWindow()
        {
            _oldScreenshot = new OldScreenshot();
            _newScreenshot = new OldScreenshot();

            InitializeComponent();
            
            Icon = Resources.camera;
            toolStripPickColorButton.SelectedColor = HighlightColor;

            _defaultImageSourceTipText = helpTip.GetToolTip(buttonImageSource);

            UpdateImageSourceButtons();

            // Unfortunately there is not enough information about the image sizes to
            // the starting location right here, but this is better than using the Windows default
            labelOldSize.Text = labelNewSize.Text = string.Empty;
            StartPosition = FormStartPosition.Manual;

            var savedLocation = Settings.Default.FormLocation;
            if (!savedLocation.IsEmpty)
                Location = Settings.Default.FormLocation;
            toolStripAutoSize.Checked = !Settings.Default.ManualSize;
            if (Settings.Default.ManualSize)
            {
                var savedSize = Settings.Default.FormSize;
                if (!savedSize.IsEmpty)
                    Size = savedSize;
            }
            ForceOnScreen();
            if (Settings.Default.FormMaximized)
                WindowState = FormWindowState.Maximized;
        }

        private readonly string _defaultImageSourceTipText; // Store for later

        protected override void OnShown(EventArgs e)
        {
            _autoResizeComplete = true;
            base.OnShown(e);

            OpenFolder(Settings.Default.LastOpenFolder);
        }

        protected override void OnClosed(EventArgs e)
        {
            Settings.Default.Save();

            base.OnClosed(e);
        }

        private bool HasBackgroundWork { get { lock(_lock) { return !IsOldLoaded || !IsNewLoaded; } } }
        private bool IsOldLoaded { get { lock (_lock) { return _oldScreenshot.IsCurrent(_fileToShow, OldImageSource); } } }
        private bool IsNewLoaded { get { lock (_lock) { return _newScreenshot.IsCurrent(_fileToShow, ImageSource.disk); } } }

        private void RefreshScreenshots()
        {
            lock (_lock)
            {
                _oldScreenshot.FileLoaded = null;
                _newScreenshot.FileLoaded = null;
            }

            FormStateChanged();
        }

        private void RefreshAll()
        {
            var previousSelection = toolStripFileList.SelectedItem as ScreenshotFile;

            OpenFolder(Settings.Default.LastOpenFolder);

            int restoredIndex = FindBestSelectionIndex(previousSelection);
            if (toolStripFileList.SelectedIndex != restoredIndex)
                toolStripFileList.SelectedIndex = restoredIndex;
        }

        /// <summary>
        /// Finds the best index to select after a refresh, using hierarchical matching:
        /// 1. Exact match, 2. Closest number in same tutorial/locale, 3. Start of same tutorial, 4. Beginning
        /// </summary>
        private int FindBestSelectionIndex(ScreenshotFile previous)
        {
            if (previous == null || toolStripFileList.Items.Count == 0)
                return 0;

            int? closestInTutorialLocale = null;
            int closestNumberDiff = int.MaxValue;
            int? tutorialStart = null;

            for (int i = 0; i < toolStripFileList.Items.Count; i++)
            {
                if (!(toolStripFileList.Items[i] is ScreenshotFile sf))
                    continue;

                // 1. Exact match - return immediately
                if (sf.RelativePath == previous.RelativePath)
                    return i;

                // 2. Closest number within same tutorial/locale
                if (sf.Name == previous.Name && sf.Locale == previous.Locale)
                {
                    int diff = Math.Abs(sf.Number - previous.Number);
                    if (diff < closestNumberDiff)
                    {
                        closestNumberDiff = diff;
                        closestInTutorialLocale = i;
                    }
                }

                // 3. Start of same tutorial (any locale)
                if (sf.Name == previous.Name && tutorialStart == null)
                    tutorialStart = i;
            }

            return closestInTutorialLocale ?? tutorialStart ?? 0;
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
                UpdateToolStrip();
                UpdatePreviewImages();
            }

            if (IsOldLoaded || IsNewLoaded)
            {
                ResizeComponents();
            }

            if (HasBackgroundWork)
            {
                var imageSource = OldImageSource;   // Get this value now
                var highlightColor = HighlightColor;
                var threadUpdateScreenshots = new Thread(() => UpdateScreenshotsAsync(imageSource, highlightColor));
                threadUpdateScreenshots.Start();
            }
        }

        private void UpdatePreviewImages()
        {
            lock (_lock)
            {
                helpTip.SetToolTip(oldScreenshotLabel, _oldScreenshot.FileLoaded);
                SetPreviewSize(labelOldSize, _oldScreenshot);
                SetPreviewImage(oldScreenshotPictureBox, _oldScreenshot, _diff);
                helpTip.SetToolTip(newScreenshotLabel, _newScreenshot.FileLoaded);
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
                var bmpDiff = matching ? Resources.Peak : Resources.NoPeak;
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
                    labelSize.Text = $@"{screenshot.ImageSize.Width} x {screenshot.ImageSize.Height}px";
                }
            }
        }

        private void UpdateToolStrip()
        {
            toolStripPrevious.Enabled = toolStripFileList.SelectedIndex > 0;
            toolStripNext.Enabled = toolStripFileList.SelectedIndex < toolStripFileList.Items.Count - 1;
            toolStripRevert.Enabled = _diff?.IsDiff ?? false;
            toolStripGotoWeb.Enabled = _fileToShow != null;
            UpdateChangeCount();
        }

        private void UpdateChangeCount()
        {
            int total = toolStripFileList.Items.Count;
            int current = toolStripFileList.SelectedIndex + 1;
            labelChangeCount.Text = total > 0 ? $@"{current}/{total} changes" : string.Empty;
        }

        private void UpdateScreenshotsAsync(ImageSource oldImageSource, Color highlightColor)
        {
            ScreenshotFile fileToShow;
            ScreenshotInfo oldScreenshot;
            string oldFileLoaded;

            lock (_lock)
            {
                fileToShow = _fileToShow;
                if (fileToShow == null || !Equals(fileToShow, _fileToShow))
                    return;
                oldFileLoaded = _oldScreenshot.FileLoaded;
                oldScreenshot = new ScreenshotInfo(_oldScreenshot);
            }

            string oldFileDescription = fileToShow.GetDescription(oldImageSource);
            if (!Equals(oldFileLoaded, oldFileDescription))
            {
                oldScreenshot = LoadScreenshot(fileToShow, oldImageSource);
                oldFileLoaded = oldFileDescription;
            }

            lock (_lock)
            {
                if (!Equals(fileToShow, _fileToShow))
                    return;
                _oldScreenshot = new OldScreenshot(oldScreenshot, oldFileLoaded);
                _diff = null;
            }

            ScreenshotInfo newScreenshot;
            string newFileLoaded;
            ScreenshotDiff diff = null;

            lock (_lock)
            {
                newFileLoaded = _newScreenshot.FileLoaded;
                newScreenshot = new ScreenshotInfo(_newScreenshot);
            }

            var newFileDescription = fileToShow.GetDescription(ImageSource.disk);
            if (!Equals(newFileLoaded, newFileDescription))
            {
                // New screenshot is the file on disk
                newScreenshot = LoadScreenshot(fileToShow, ImageSource.disk);
            }

            if (!newScreenshot.IsPlaceholder || !oldScreenshot.IsPlaceholder)
            {
                diff = DiffScreenshots(oldScreenshot, newScreenshot, highlightColor);
            }

            lock (_lock)
            {
                if (!Equals(fileToShow, _fileToShow))
                    return;

                _newScreenshot = new OldScreenshot(newScreenshot, newFileDescription);
                _diff = diff;
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
                BeginInvoke((Action) (() => ShowMessageWithException(
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
                var minFormHeight = Math.Max(newImageSize.Height, oldImageSize.Height) +
                                    toolStrip.Height + oldScreenshotLabelPanel.Height + CELL_PADDING * 2;
                var minClientSize = MinimumSize - (Size - ClientSize);
                minFormHeight = Math.Max(minFormHeight, minClientSize.Height);

                return new Size(minFormWidth, minFormHeight);
            }
        }

        private void SetPath(ScreenshotFile screenshotFile)
        {
            lock (_lock)
            {
                _fileToShow = screenshotFile;
            }

            FormStateChanged();
        }

        private string GetOpenFolder()
        {
            using var chooseFolder = new FolderBrowserDialog();
            chooseFolder.SelectedPath = Settings.Default.LastOpenFolder;
            if (chooseFolder.ShowDialog(this) == DialogResult.OK)
            {
                Settings.Default.LastOpenFolder = chooseFolder.SelectedPath;
                return chooseFolder.SelectedPath;
            }

            return null;
        }

        private void OpenFolder(string folderPath = null)
        {
            if (string.IsNullOrEmpty(folderPath))
            {
                folderPath = GetOpenFolder();
                if (string.IsNullOrEmpty(folderPath))
                    return;
            }

            var comboBox = toolStripFileList.ComboBox;
            if (comboBox == null)
                toolStripFileList.Items.Clear();
            else
            {
                var changedFiles = GitFileHelper.GetChangedFilePaths(folderPath);
                var listScreenshots = changedFiles.Where(ScreenshotFile.IsMatch)
                    .OrderBy(s => s, StringComparer.InvariantCultureIgnoreCase)
                    .Select(f => new ScreenshotFile(f)).ToArray();
                comboBox.BeginUpdate();
                comboBox.DataSource = listScreenshots;
                comboBox.DisplayMember = "RelativePath";
                comboBox.EndUpdate();
            }
            if (toolStripFileList.Items.Count > 0)
            {
                toolStripFileList.Enabled = true;
                if (toolStripFileList.SelectedIndex != 0)
                    toolStripFileList.SelectedIndex = 0;
                FormStateChanged();
            }
            else
            {
                toolStripFileList.Enabled = false;
                lock (_lock)
                {
                    _fileToShow = null;
                    _oldScreenshot = new OldScreenshot();
                    _newScreenshot = new OldScreenshot();
                    _diff = null;
                }
                FormStateChanged();
                ShowMessage(string.Format("No changed PNG files found in {0}", folderPath));
            }
        }

        private void Previous()
        {
            if (toolStripFileList.SelectedIndex > 0)
                toolStripFileList.SelectedIndex--;
        }

        private void Next()
        {
            if (toolStripFileList.SelectedIndex < toolStripFileList.Items.Count - 1)
                toolStripFileList.SelectedIndex++;
        }

        private void Revert()
        {
            string filePath = _fileToShow.Path;
            if (File.Exists(filePath) && !IsWritable(filePath))
            {
                ShowMessage(LineSeparate(string.Format("The file {0} is locked.", filePath),
                    "Check that it is not open in another program such as TortoiseIDiff."));
                return;
            }
            try
            {
                GitFileHelper.RevertFileToHead(filePath);
                RefreshScreenshots();
            }
            catch (Exception e)
            {
                ShowMessageWithException(string.Format("Failed to revert screenshot {0}", filePath), e);
            }
        }

        public static bool IsWritable(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
                return true;
            }
            catch (IOException)
            {
                // An IOException means the file is locked
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                // Or we lack permissions can also make it not possible to write to
                return false;
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

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Handle all keyboard shortcuts at the form level to prevent them from
            // being processed by child controls (e.g., combo box jumping to 'S' on Ctrl+S)
            switch (keyData)
            {
                // Navigation
                case Keys.Control | Keys.Tab:
                    NextOldImageSource();
                    return true;
                case Keys.Control | Keys.Down:
                case Keys.Control | Keys.Right:
                case Keys.PageDown:
                case Keys.F11:
                    Next();
                    return true;
                case Keys.Control | Keys.Up:
                case Keys.Control | Keys.Left:
                case Keys.PageUp:
                case Keys.Shift | Keys.F11:
                    Previous();
                    return true;

                // Actions
                case Keys.Control | Keys.Z:
                case Keys.F12:
                    Revert();
                    return true;
                case Keys.Control | Keys.R:
                    RefreshScreenshots();
                    return true;
                case Keys.F5:
                    RefreshAll();
                    return true;
                case Keys.Control | Keys.G:
                    GotoLink();
                    return true;

                // Clipboard
                case Keys.Control | Keys.C:
                    lock (_lock)
                    {
                        CopyBitmap(_newScreenshot.Image);
                    }
                    return true;
                case Keys.Control | Keys.Shift | Keys.C:
                    lock (_lock)
                    {
                        CopyBitmap(_oldScreenshot.Image);
                    }
                    return true;
                case Keys.Control | Keys.Alt | Keys.C:
                    lock (_lock)
                    {
                        var diffImage = _diff?.HighlightedImage;
                        if (diffImage == null)
                        {
                            ShowMessage("No diff image available.");
                            return true;
                        }
                        CopyBitmap(diffImage);
                    }
                    return true;
                case Keys.Control | Keys.S:
                    SaveDiffImage();
                    return true;
                case Keys.Control | Keys.V:
                    Paste();
                    return true;

                // Diff view toggles
                case Keys.D:
                    toolStripDiffOnly.Checked = !toolStripDiffOnly.Checked;
                    return true;
                case Keys.A:
                    toolStripAmplify.Checked = !toolStripAmplify.Checked;
                    return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void StoreFormBoundsState()
        {
            // Only store sizing information when not automatically resizing the form
            if (!_autoResizeComplete)
                return;

            Settings.Default.ManualSize = !toolStripAutoSize.Checked;
            if (WindowState == FormWindowState.Normal)
                Settings.Default.FormLocation = Location;
            if (WindowState == FormWindowState.Normal)
                Settings.Default.FormSize = Size;
            Settings.Default.FormMaximized =
                (WindowState == FormWindowState.Maximized);
        }

        private Color HighlightColor
        {
            get => Color.FromArgb(Settings.Default.ImageDiffAlpha,
                Settings.Default.ImageDiffColor.R,
                Settings.Default.ImageDiffColor.G,
                Settings.Default.ImageDiffColor.B);

            set
            {
                Settings.Default.ImageDiffAlpha = value.A;
                Settings.Default.ImageDiffColor = Color.FromArgb(value.R, value.G, value.B);
            }
        }

        private static readonly ImageSource[] OLD_IMAGE_SOURCES = { ImageSource.git, ImageSource.web };

        private ImageSource OldImageSource
        {
            get => OLD_IMAGE_SOURCES[Settings.Default.OldImageSource % OLD_IMAGE_SOURCES.Length];
            set
            {
                for (int i = 0; i < OLD_IMAGE_SOURCES.Length; i++)
                {
                    if (OLD_IMAGE_SOURCES[i] == value)
                        Settings.Default.OldImageSource = i;
                }
            }
        }

        private void NextOldImageSource()
        {
            OldImageSource = OLD_IMAGE_SOURCES[(Settings.Default.OldImageSource + 1) % OLD_IMAGE_SOURCES.Length];
            SetOldImageSource(OldImageSource);
        }

        private void UpdateImageSourceButtons()
        {
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
                    buttonImageSource.Image = Resources.save;
                    helpTip.SetToolTip(buttonImageSource, _defaultImageSourceTipText);
                    break;
            }
        }

        private void toolStripOpenFolder_Click(object sender, EventArgs e)
        {
            OpenFolder();
        }

        private void toolStripPrevious_Click(object sender, EventArgs e)
        {
            Previous();
        }

        private void toolStripNext_Click(object sender, EventArgs e)
        {
            Next();
        }

        private void toolStripFileList_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetPath(toolStripFileList.SelectedItem as ScreenshotFile);
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

        private void buttonImageSource_Click(object sender, EventArgs e)
        {
            // Update menu check marks
            menuItemGit.Checked = OldImageSource == ImageSource.git;
            menuItemWeb.Checked = OldImageSource == ImageSource.web;

            // Show context menu below the button
            contextMenuImageSource.Show(buttonImageSource, new Point(0, buttonImageSource.Height));
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

        private void toolStripPickColorButton_ColorChanged(object sender, EventArgs e)
        {
            HighlightColor = toolStripPickColorButton.SelectedColor;
            if (oldScreenshotPictureBox.Visible)
                UpdateScreenshotsAsync(OldImageSource, HighlightColor); // On foreground thread because the screenshots are already loaded - just updating the diff
        }

        private void toolStripDiffOnly_CheckedChanged(object sender, EventArgs e)
        {
            UpdatePreviewImages();
        }

        private void toolStripAmplify_CheckedChanged(object sender, EventArgs e)
        {
            UpdatePreviewImages();
        }

        private void Paste()
        {
            try
            {
                var image = Clipboard.GetImage();
                if (image == null)
                {
                    ShowMessage("No image found on the clipboard.");
                    return;
                }

                lock (_lock)
                {
                    image.Save(_fileToShow.Path, ImageFormat.Png);
                    _newScreenshot.FileLoaded = null;
                }

                FormStateChanged();
            }
            catch (Exception e)
            {
                ShowMessageWithException("Failed to save bitmap from clipboard.", e);
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
                ShowMessageWithException("Failed clipboard operation.", e);
            }
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
                    ShowMessage("Could not locate ai\\.tmp folder.");
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

        #region Local implementations for Skyline functions

        private string LineSeparate(params string[] lines)
        {
            var sb = new StringBuilder();
            foreach (var line in lines)
                sb.AppendLine(line.Trim());
            return sb.ToString();
        }

        public static void OpenLink(string link)
        {
            try
            {
                Process.Start(link);
            }
            catch (Exception)
            {
                throw new IOException(string.Format("Could not open web Browser to show link {0}", link));
            }
        }

        public void ForceOnScreen()
        {
            ForceOnScreen(this);
        }

        public static void ForceOnScreen(Form form)
        {
            var location = form.Location;
            location.X = Math.Max(GetScreen(form.Left, form.Top).WorkingArea.Left,
                Math.Min(location.X, GetScreen(form.Right, form.Top).WorkingArea.Right - form.Size.Width));
            location.Y = Math.Max(GetScreen(form.Left, form.Top).WorkingArea.Top,
                Math.Min(location.Y, GetScreen(form.Left, form.Bottom).WorkingArea.Bottom - form.Size.Height));
            form.Location = location;
        }

        private static Screen GetScreen(int x, int y)
        {
            return Screen.FromPoint(new Point(x, y));
        }

        private void ShowMessage(string message)
        {
            MessageBox.Show(this, message);
        }

        // ReSharper disable once UnusedParameter.Local
        private void ShowMessageWithException(string message, Exception e)
        {
            // TODO: Make exception stack trace available somehow
            ShowMessage(message);
        }

        #endregion
    }
}
