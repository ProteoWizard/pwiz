/*
 * Original authors: Brendan MacLean <brendanx .at. uw.edu>
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
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
            OpenFolder(Settings.Default.LastOpenFolder);
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

                            _diff.ShowBinaryDiff(_rtfDiff);
                        }

                        oldScreenshotPictureBox.Visible = !imagesMatch;
                        if (_rtfDiff != null)
                            _rtfDiff.Visible = imagesMatch;
                    }
                }
            }
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
            // TODO: Pre-calculate diff to know whether revert is appropriate
            // toolStripRevert.Enabled = (_pixelDiffCount ?? 0) != 0;
            toolStripGotoWeb.Enabled = _fileToShow != null;
        }

        private void UpdateScreenshotsAsync(ImageSource oldImageSource, Color highlightColor)
        {
            ScreenshotFile fileToShow;
            ScreenshotInfo oldScreenshot;
            string oldFileLoaded;

            lock (_lock)
            {
                fileToShow = _fileToShow;
                if (!Equals(fileToShow, _fileToShow))
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
                            using var webClient = new WebClient();
                            using var fileSaverTemp = new FileSaver(file.Path);  // Temporary. Never saved
                            webClient.DownloadFile(file.UrlToDownload, fileSaverTemp.SafeName);
                            imageBytes = File.ReadAllBytes(fileSaverTemp.SafeName);
                        }
                        break;
                }
                var ms = new MemoryStream(imageBytes);
                return new ScreenshotInfo(ms);
            }
            catch (Exception e)
            {
                this.BeginInvoke((Action) (() => ShowMessageWithException(
                    string.Format("Failed to load a bitmap from {0}.", file.GetDescription(source)), e)));
                var failureBmp = Resources.DiskFailure;
                failureBmp.MakeTransparent(Color.White);
                return new ScreenshotInfo(failureBmp);
            }
        }

        private void SetPreviewImage(PictureBox previewBox, ScreenshotInfo screenshot, ScreenshotDiff diff = null)
        {
            var baseImage = diff?.HighlightedImage ?? screenshot.Image;
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
                var listScreenshots = changedFiles.Where(ScreenshotFile.IsMatch).Select(f => new ScreenshotFile(f)).ToArray();
                comboBox.BeginUpdate();
                comboBox.DataSource = listScreenshots;
                comboBox.DisplayMember = "RelativePath";
                comboBox.EndUpdate();
            }
            if (toolStripFileList.Items.Count > 0)
            {
                if (toolStripFileList.SelectedIndex != 0)
                    toolStripFileList.SelectedIndex = 0;
            }
            else
            {
                ShowMessageWithException(string.Format("No changed PNG files found in {0}", folderPath), null);
            }
            
            FormStateChanged();
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
                ShowMessageWithException(LineSeparate(string.Format("The file {0} is locked.", filePath),
                    "Check that it is not open in another program such as TortoiseIDiff."), null);
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

        private string LineSeparate(params string[] lines)
        {
            return LineSeparate(lines.ToList());
        }

        private string LineSeparate(IEnumerable<string> lines)
        {
            var sb = new StringBuilder();
            foreach (var line in lines)
                sb.AppendLine(line.Trim());
            return sb.ToString();
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
            if (_fileToShow is { IsEmpty: false })
            {
                OpenLink(_fileToShow.UrlInTutorial);
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

            Settings.Default.ManualSize = !toolStripAutoSize.Checked;
            if (WindowState == FormWindowState.Normal)
                Settings.Default.FormLocation = Location;
            if (WindowState == FormWindowState.Normal)
                Settings.Default.FormSize = Size;
            Settings.Default.FormMaximized =
                (WindowState == FormWindowState.Maximized);
        }

        private enum ImageSource { disk, web, git }

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

        private void NextOldImageSource()
        {
            OldImageSource = OLD_IMAGE_SOURCES[(Settings.Default.OldImageSource + 1) % OLD_IMAGE_SOURCES.Length];

            UpdateImageSourceButtons();

            lock (_lock)
            {
                _oldScreenshot = new OldScreenshot(_oldScreenshot, null, OldImageSource);
            }

            FormStateChanged();
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
            NextOldImageSource();
        }

        private void toolStripPickColorButton_ColorChanged(object sender, EventArgs e)
        {
            HighlightColor = toolStripPickColorButton.SelectedColor;
            if (oldScreenshotPictureBox.Visible)
                UpdateScreenshotsAsync(OldImageSource, HighlightColor); // On foreground thread because the screenshots are already loaded - just updating the diff
        }

        private void ScreenshotPreviewForm_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Tab:
                    if (e.Control)
                    {
                        NextOldImageSource();
                        e.Handled = true;
                    }
                    break;
                case Keys.F11:
                    if (e.Shift)
                    {
                        Previous();
                    }
                    else
                    {
                        Next();
                    }
                    e.Handled = true;
                    break;
                case Keys.Down:
                case Keys.Right:
                    if (e.Control)
                    {
                        Next();
                        e.Handled = true;
                    }
                    break;
                case Keys.Up:
                case Keys.Left:
                    if (e.Control)
                    {
                        Previous();
                        e.Handled = true;
                    }
                    break;
                case Keys.PageDown:
                    Next();
                    e.Handled = true;
                    break;
                case Keys.PageUp:
                    Previous();
                    e.Handled = true;
                    break;
                case Keys.Z:
                    if (e.Control)
                    {
                        Revert();
                        e.Handled = true;
                    }
                    break;
                case Keys.F12:
                    Revert();
                    e.Handled = true;
                    break;
                case Keys.F5:
                    RefreshAll();
                    e.Handled = true;
                    return;
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
                            imageToCopy = e.Shift ? _oldScreenshot.Image : _newScreenshot.Image;
                        }
                        CopyBitmap(imageToCopy);
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
                ShowMessageWithException("Failed clipboard operation.", e);
            }
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

        // ReSharper disable once UnusedParameter.Local
        private void ShowMessageWithException(string message, Exception e)
        {
            // TODO: Make exception stack trace available somehow
            MessageBox.Show(this, message);
        }

        private static Screen GetScreen(int x, int y)
        {
            return Screen.FromPoint(new Point(x, y));
        }

        private class ScreenshotFile
        {
            private static readonly Regex PATTERN = new Regex(@"\\(\w+)\\(\w\w)\\s-(\d\d)\.png");

            public static bool IsMatch(string filePath)
            {
                return PATTERN.Match(filePath).Success;
            }

            public ScreenshotFile(string filePath)
            {
                Path = filePath;

                var match = PATTERN.Match(filePath);
                if (match.Success)
                {
                    Name = match.Groups[1].Value;
                    Locale = match.Groups[2].Value;
                    Number = int.Parse(match.Groups[3].Value);
                }
            }

            public string Path { get; }
            private string Name { get; }
            private string Locale { get; }
            private int Number { get; }

            public bool IsEmpty => string.IsNullOrEmpty(Name);

            private const string BASE_URL = "https://skyline.ms/tutorials/24-1";
            public string UrlInTutorial => $"{BASE_URL}/{Name}/{Locale}/index.html#s-{Number}";
            public string UrlToDownload => $"{BASE_URL}/{RelativePath}";
            // RelativePath is used for ComboBox display
            // ReSharper disable once MemberCanBePrivate.Local
            public string RelativePath => $"{Name}/{Locale}/s-{Number}.png";

            public string GetDescription(ImageSource source)
            {
                switch (source)
                {
                    case ImageSource.git:
                        return $"Git HEAD: {RelativePath}";
                    case ImageSource.web:
                        return UrlToDownload;
                    case ImageSource.disk:
                    default:
                        return Path;
                }
            }
        }

        private class ScreenshotInfo
        {
            protected ScreenshotInfo()
            {
            }

            public ScreenshotInfo(Bitmap image)
            {
                Image = image;
                ImageSize = image?.Size ?? Size.Empty;
            }

            public ScreenshotInfo(MemoryStream ms)
                : this(new Bitmap(ms))
            {
                Memory = ms;
            }

            public ScreenshotInfo(ScreenshotInfo info)
            {
                Image = info.Image;
                Memory = info.Memory;
                ImageSize = info.ImageSize;
            }

            public Bitmap Image { get; }
            public MemoryStream Memory { get; }
            public Size ImageSize { get; }
            public bool IsPlaceholder => Memory == null;
        }

        private class OldScreenshot : ScreenshotInfo
        {
            public OldScreenshot()
            {
            }

            public OldScreenshot(ScreenshotInfo info, string fileLoaded, ImageSource source = ImageSource.disk)
                : base(info)
            {
                FileLoaded = fileLoaded;
                Source = source;
            }

            public string FileLoaded { get; set; }

            // ReSharper disable once UnusedAutoPropertyAccessor.Local
            public ImageSource Source { get; private set; }

            public bool IsCurrent(ScreenshotFile screenshot, ImageSource currentSource)
            {
                return Equals(FileLoaded, screenshot?.GetDescription(currentSource));
            }
        }

        private class ScreenshotDiff
        {
            private readonly Size _sizeOld;
            private readonly byte[] _memoryOld;
            private readonly Size _sizeNew;
            private readonly byte[] _memoryNew;

            public ScreenshotDiff(ScreenshotInfo oldScreenshot, ScreenshotInfo newScreenshot, Color highlightColor)
            {
                _sizeOld = oldScreenshot.ImageSize;
                _memoryOld = oldScreenshot.Memory?.ToArray();
                _sizeNew = newScreenshot.ImageSize;
                _memoryNew = newScreenshot.Memory?.ToArray();

                if (!SizesDiffer)
                {
                    lock (oldScreenshot.Image)
                    {
                        lock (newScreenshot.Image)
                        {
                            CalcHighlightImage(oldScreenshot.Image, newScreenshot.Image, highlightColor);
                        }
                    }
                }
            }

            public bool IsDiff => SizesDiffer || PixelsDiffer || BytesDiffer;
            public bool SizesDiffer => !Equals(_sizeOld, _sizeNew);
            public bool PixelsDiffer => PixelCount != 0;
            public bool BytesDiffer => _memoryOld.Length != _memoryNew.Length || !_memoryOld.SequenceEqual(_memoryNew);
            public Bitmap HighlightedImage { get; private set; }
            public int PixelCount { get; private set; }

            public string DiffText
            {
                get
                {
                    if (SizesDiffer)
                        return $@" ({_sizeNew.Width - _sizeOld.Width} x {_sizeNew.Height - _sizeOld.Height}px)";
                    if (PixelsDiffer)
                        return $@" ({PixelCount} pixels)";
                    if (BytesDiffer)
                    {
                        if (_memoryOld.Length != _memoryNew.Length)
                            return $" ({_memoryNew.Length - _memoryOld.Length} bytes)";
                        int startIndex = 0, diffCount = 0;
                        for (int i = 0; i < _memoryOld.Length; i++)
                        {
                            if (_memoryOld[i] != _memoryNew[i])
                            {
                                if (diffCount == 0)
                                    startIndex = i;
                                diffCount++;
                            }
                        }

                        return $@" (at {startIndex}, diff {diffCount} bytes)";
                    }
                    return string.Empty;
                }
            }

            private void CalcHighlightImage(Bitmap bmpOld, Bitmap bmpNew, Color highlightColor)
            {
                var result = new Bitmap(bmpOld.Width, bmpOld.Height);
                var alpha = highlightColor.A;

                PixelCount = 0;
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
                            PixelCount++;
                        }
                        else
                        {
                            result.SetPixel(x, y, pixel1);
                        }
                    }
                }

                HighlightedImage = PixelCount > 0 ? result : bmpOld;
            }

            public void ShowBinaryDiff(RichTextBox richTextBox)
            {
                ShowDiff(richTextBox, _memoryOld, _memoryNew);

            }
            private static void ShowDiff(RichTextBox richTextBox, byte[] array1, byte[] array2)
            {
                richTextBox.Clear();

                // Determine the longer array length
                int maxLength = Math.Max(array1.Length, array2.Length);
                int linesShown = 0;
                for (int i = 0; i < maxLength; i += 16)
                {
                    if (ArraysMatch(i, 16, array1, array2))
                    {
                        continue;
                    }

                    ShowLineDiff(richTextBox, i, 16, array1, array2, Color.Red, "<<");
                    ShowLineDiff(richTextBox, i, 16, array2, array1, Color.Green, ">>");

                    if (++linesShown >= 4)
                    {
                        // Stop after 4 lines
                        AppendText(richTextBox, "...", Color.Black);
                        break;
                    }
                }
            }

            private static bool ArraysMatch(int startIndex, int len, byte[] array1, byte[] array2)
            {
                for (int i = startIndex; i < startIndex + len; i++)
                {
                    if (i >= array1.Length && i >= array2.Length)
                        return true;
                    if (i >= array1.Length || i >= array2.Length)
                        return false;
                    if (array1[i] != array2[i])
                        return false;
                }
                return true;
            }

            private static void ShowLineDiff(RichTextBox richTextBox, int startIndex, int lineLen, byte[] arrayShow, byte[] arrayCompare, Color color, string prefix)
            {
                AppendText(richTextBox, $"{prefix} {startIndex:X8} ", Color.Black); // address

                for (int n = 0; n < lineLen; n++)
                {
                    if (n == lineLen/2)
                        AppendText(richTextBox, "  ", Color.Black);

                    int i = startIndex + n;
                    if (i < arrayShow.Length && i < arrayCompare.Length && arrayShow[i] == arrayCompare[i])
                    {
                        // Bytes are the same, display them in black
                        AppendText(richTextBox, $"{arrayShow[i]:X2} ", Color.Black);
                    }
                    else if (i < arrayShow.Length)
                    {
                        // Bytes differ
                        AppendText(richTextBox, $"{arrayShow[i]:X2} ", color);
                    }
                    else
                    {
                        // Bytes missing
                        AppendText(richTextBox, "-- ", color);
                    }
                }
                AppendText(richTextBox, "    ", Color.Black);
                for (int n = 0; n < lineLen; n++)
                {
                    int i = startIndex + n;
                    if (i < arrayShow.Length && i < arrayCompare.Length && arrayShow[i] == arrayCompare[i])
                    {
                        // Bytes are the same, display them in black
                        AppendText(richTextBox, GetChar(arrayShow[i]), Color.Black);
                    }
                    else if (i < arrayShow.Length)
                    {
                        // Bytes differ
                        AppendText(richTextBox, GetChar(arrayShow[i]), color);
                    }
                    else
                    {
                        // Bytes missing
                        AppendText(richTextBox, "^", color);
                    }
                }
                AppendText(richTextBox, Environment.NewLine, Color.Black);
            }

            /// <summary>
            /// If a byte is printable ASCII the character as text is returned otherwise a period.
            /// </summary>
            private static string GetChar(byte b)
            {
                return (b >= 0x20 && b <= 0x7E ? (char)b : '.').ToString();
            }

            private static void AppendText(RichTextBox richTextBox, string text, Color color)
            {
                richTextBox.SelectionStart = richTextBox.TextLength;
                richTextBox.SelectionLength = 0;
                richTextBox.SelectionColor = color;
                richTextBox.AppendText(text);
                richTextBox.SelectionColor = richTextBox.ForeColor;
            }
        }
    }
}
