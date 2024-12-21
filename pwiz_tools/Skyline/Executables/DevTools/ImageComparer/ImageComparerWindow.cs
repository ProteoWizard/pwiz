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
using System.Drawing.Imaging;
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
        private bool? _oldAndNewMatch;
        #endregion

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
                var threadUpdateScreenshots = new Thread(() => UpdateScreenshotsAsync(imageSource));
                threadUpdateScreenshots.Start();
            }
        }

        private void UpdatePreviewImages()
        {
            lock (_lock)
            {
                helpTip.SetToolTip(oldScreenshotLabel, _oldScreenshot.FileLoaded);
                SetPreviewSize(labelOldSize, _oldScreenshot);
                SetPreviewImage(oldScreenshotPictureBox, _oldScreenshot);
                helpTip.SetToolTip(newScreenshotLabel, _newScreenshot.FileLoaded);
                SetPreviewSize(labelNewSize, _newScreenshot);
                SetPreviewImage(newScreenshotPictureBox, _newScreenshot);
                if (!_oldAndNewMatch.HasValue)
                {
                    pictureMatching.Visible = false;
                    labelOldSize.Left = pictureMatching.Left;
                }
                else
                {
                    pictureMatching.Visible = true;
                    labelOldSize.Left = pictureMatching.Right;
                    var bmpDiff = _oldAndNewMatch.Value ? Resources.Peak : Resources.NoPeak;
                    bmpDiff.MakeTransparent(Color.White);
                    pictureMatching.Image = bmpDiff;
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
                    labelSize.Text = $@"{image.Width} x {image.Height}px";
                }
            }
        }

        private void UpdateToolStrip()
        {
            toolStripPrevious.Enabled = toolStripFileList.SelectedIndex > 0;
            toolStripNext.Enabled = toolStripFileList.SelectedIndex < toolStripFileList.Items.Count - 1;
            toolStripRevert.Enabled = !(_oldAndNewMatch ?? true);
            toolStripGotoWeb.Enabled = _fileToShow != null;
        }

        private void UpdateScreenshotsAsync(ImageSource oldImageSource)
        {
            ScreenshotFile fileToShow;
            ScreenshotInfo oldScreenshot;
            string oldFileLoaded;

            lock (_lock)
            {
                fileToShow = _fileToShow;
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
            }

            ScreenshotInfo newScreenshot;
            string newFileLoaded;
            bool? oldAndNewMatch = null;

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

            if (!newScreenshot.IsPlaceholder && !oldScreenshot.IsPlaceholder)
            {
                Bitmap diffImage;
                bool imageChanged;
                lock (oldScreenshot.Image)
                {
                    lock (newScreenshot.Image)
                    {
                        diffImage = DiffImages(oldScreenshot.Image, newScreenshot.Image);
                        imageChanged = !ReferenceEquals(diffImage, _oldScreenshot.Image);
                    }
                }

                if (imageChanged || oldScreenshot.ImageSize != newScreenshot.ImageSize)
                {
                    oldAndNewMatch = false;
                }
                else
                {
                    oldAndNewMatch = true;
                }

                oldScreenshot = new ScreenshotInfo(diffImage);
            }

            lock (_lock)
            {
                if (!Equals(fileToShow, _fileToShow))
                    return;

                _newScreenshot = new OldScreenshot(newScreenshot, newFileDescription);
                _oldScreenshot = new OldScreenshot(oldScreenshot, oldFileDescription);
                _oldAndNewMatch = oldAndNewMatch;
            }
            
            FormStateChangedBackground();
        }

        private Bitmap DiffImages(Bitmap bmpOld, Bitmap bmpNew)
        {
            if (bmpNew == null || bmpOld == null || bmpNew.Size != bmpOld.Size)
                return bmpOld;

            try
            {
                return HighlightDifferences(bmpOld, bmpNew, Color.Red);
            }
            catch (Exception e)
            {
                this.BeginInvoke((Action)(() => ShowMessageWithException(
                    "Failed to diff bitmaps.", e)));
                return bmpOld;
            }
        }

        // ReSharper disable once UnusedParameter.Local
        private void ShowMessageWithException(string message, Exception e)
        {
            // TODO: Make exception stack trace available somehow
            MessageBox.Show(this, message);
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
                return new ScreenshotInfo(new Bitmap(ms));
            }
            catch (Exception e)
            {
                this.BeginInvoke((Action) (() => ShowMessageWithException(
                    string.Format("Failed to load a bitmap from {0}.", file.GetDescription(source)), e)));
                var failureBmp = Resources.DiskFailure;
                failureBmp.MakeTransparent(Color.White);
                return new ScreenshotInfo(failureBmp, true);
            }
        }

        private void SetPreviewImage(PictureBox previewBox, ScreenshotInfo screenshot)
        {
            var newImage = screenshot.Image;
            if (newImage != null && !screenshot.IsPlaceholder)
            {
                lock (newImage)
                {
                    var containerSize = !toolStripAutoSize.Checked ? previewBox.Size : Size.Empty;
                    var screenshotSize = CalcBitmapSize(screenshot, containerSize);
                    // Always make a copy to avoid having PictureBox lock the bitmap
                    // which can cause issues with future image diffs
                    newImage = new Bitmap(screenshot.Image, screenshotSize);
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

        private void toolStripRevert_Click(object sender, EventArgs e)
        {
            Revert();
        }

        // private void toolStripRefresh_Click(object sender, EventArgs e)
        // {
        //     RefreshScreenshots();
        // }

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
                    if (e.Control)
                    {
                        Next();
                        e.Handled = true;
                    }
                    break;
                case Keys.Up:
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
            public ScreenshotInfo(Bitmap image = null, bool isPlaceholder = false)
            {
                Image = image;
                ImageSize = image?.Size ?? Size.Empty;
                IsPlaceholder = isPlaceholder;
            }

            public ScreenshotInfo(ScreenshotInfo info)
            {
                Image = info.Image;
                ImageSize = info.ImageSize;
                IsPlaceholder = info.IsPlaceholder;
            }

            public Bitmap Image { get; private set; }
            public Size ImageSize { get; private set; }
            public bool IsPlaceholder { get; private set; }
        }

        private class OldScreenshot : ScreenshotInfo
        {
            public OldScreenshot(Bitmap image = null, bool isPlaceholder = false, string fileLoaded = null, ImageSource source = ImageSource.disk)
                : base(image, isPlaceholder)
            {
                FileLoaded = fileLoaded;
                Source = source;
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

        private void toolStripFileList_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetPath(toolStripFileList.SelectedItem as ScreenshotFile);
        }
    }
}
