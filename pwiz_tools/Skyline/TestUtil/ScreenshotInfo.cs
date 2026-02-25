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
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace pwiz.SkylineTestUtil
{
    public enum ImageSource { disk, web, git }

    internal class ScreenshotInfo
    {
        protected ScreenshotInfo()
        {
        }

        public ScreenshotInfo(Bitmap image)
        {
            Image = image;
            ImageSize = image?.Size ?? Size.Empty;
        }

        public ScreenshotInfo(MemoryStream ms, Bitmap bmp = null)
            : this(bmp ?? new Bitmap(ms))
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

    internal class OldScreenshot : ScreenshotInfo
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

    internal class NewScreenshot : ScreenshotInfo
    {
        public NewScreenshot()
        {
        }

        public NewScreenshot(ScreenshotInfo info, bool isTaken)
            : base(info)
        {
            IsTaken = isTaken;
        }

        public bool IsTaken { get; set; }
    }

    internal class ScreenshotFile
    {
        private static readonly Regex PATTERN = new Regex(@"\\([a-zA-Z0-9\-]+)\\(\w\w)\\s-(\d\d)\.png");

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
        internal string Name { get; }
        internal string Locale { get; }
        internal int Number { get; }

        public bool IsEmpty => string.IsNullOrEmpty(Name);

        private const string BASE_URL = "https://skyline.ms/tutorials/24-1";
        public string UrlInTutorial => $"{BASE_URL}/{Name}/{Locale}/index.html#s-{ScreenshotManager.PadScreenshotNum(Number)}";
        public string UrlToDownload => $"{BASE_URL}/{RelativePath}";
        // RelativePath is used for ComboBox display
        // ReSharper disable once MemberCanBePrivate.Local
        public string RelativePath => $"{Name}/{Locale}/s-{ScreenshotManager.PadScreenshotNum(Number)}.png";

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

        /// <summary>
        /// Generates a diff filename for saving to ai\.tmp folder.
        /// Format: {Name}-{Locale}-s-{Number}-diff-{pixelCount}px.png
        /// </summary>
        public string GetDiffFileName(int pixelCount)
        {
            return $"{Name}-{Locale}-s-{Number:D2}-diff-{pixelCount}px.png";
        }

        /// <summary>
        /// Gets the path to the ai\.tmp folder relative to this screenshot's location.
        /// Navigates up from the Tutorials folder to find the repository root.
        /// </summary>
        public string GetAiTmpFolder()
        {
            // Path is like: ...\pwiz_tools\Skyline\Documentation\Tutorials\{Name}\{Locale}\s-{Number}.png
            // Need to navigate up to repository root and then to ai\.tmp
            var dir = System.IO.Path.GetDirectoryName(Path);
            while (dir != null)
            {
                var parent = System.IO.Path.GetDirectoryName(dir);
                if (parent != null && System.IO.Path.GetFileName(dir) == "pwiz_tools")
                {
                    // Found pwiz_tools, parent is repository root
                    return System.IO.Path.Combine(parent, "ai", ".tmp");
                }
                dir = parent;
            }
            return null;
        }
    }

    internal class ScreenshotDiff
    {
        /// <summary>
        /// Default per-channel color tolerance. Pixels whose R, G, and B channels
        /// all differ by at most this value are considered matching.
        /// </summary>
        public const int DEFAULT_COLOR_TOLERANCE = 0;

        /// <summary>
        /// Known system color mappings that should be treated as equivalent.
        /// These arise from differences in Windows theme/system colors between
        /// the machine that captured reference screenshots and the current machine.
        /// Mappings are checked in both directions (old->new and new->old).
        /// </summary>
        private static readonly HashSet<long> _allowedColorMappings = BuildAllowedColorMappings(
            (255, 255, 255, 243, 243, 243),  // White -> light gray (system background)
            (255, 255, 255, 249, 249, 249),  // White -> near-white (system background)
            (225, 225, 225, 253, 253, 253)   // Gray -> near-white (system border/control color)
        );

        private static HashSet<long> BuildAllowedColorMappings(params (int r1, int g1, int b1, int r2, int g2, int b2)[] mappings)
        {
            var set = new HashSet<long>();
            foreach (var (r1, g1, b1, r2, g2, b2) in mappings)
            {
                // Add both directions
                set.Add(ColorPairKey(r1, g1, b1, r2, g2, b2));
                set.Add(ColorPairKey(r2, g2, b2, r1, g1, b1));
            }
            return set;
        }

        private static long ColorPairKey(long r1, long g1, long b1, long r2, long g2, long b2)
        {
            return (r1 << 40) | (g1 << 32) | (b1 << 24) |
                   (r2 << 16) | (g2 << 8) | b2;
        }

        private readonly Size _sizeOld;
        private readonly byte[] _memoryOld;
        private readonly Size _sizeNew;
        private readonly byte[] _memoryNew;
        private readonly List<Point> _diffPixels = new List<Point>();
        private readonly Color _highlightColor;

        public ScreenshotDiff(ScreenshotInfo oldScreenshot, ScreenshotInfo newScreenshot, Color highlightColor, int colorTolerance = DEFAULT_COLOR_TOLERANCE)
        {
            _sizeOld = oldScreenshot.ImageSize;
            _memoryOld = oldScreenshot.Memory?.ToArray();
            _sizeNew = newScreenshot.ImageSize;
            _memoryNew = newScreenshot.Memory?.ToArray();
            _highlightColor = highlightColor;

            if (!SizesDiffer)
            {
                lock (oldScreenshot.Image)
                {
                    lock (newScreenshot.Image)
                    {
                        CalcHighlightImage(oldScreenshot.Image, newScreenshot.Image, highlightColor, colorTolerance);
                    }
                }
            }
        }

        public bool IsDiff => SizesDiffer || PixelsDiffer || BytesDiffer;
        public bool SizesDiffer => !Equals(_sizeOld, _sizeNew);
        public Size SizeOld => _sizeOld;
        public Size SizeNew => _sizeNew;
        public bool PixelsDiffer => PixelCount != 0;
        public bool BytesDiffer => _memoryOld.Length != _memoryNew.Length || !_memoryOld.SequenceEqual(_memoryNew);
        public Bitmap HighlightedImage { get; private set; }
        public Bitmap DiffOnlyImage { get; private set; }
        public int PixelCount { get; private set; }

        /// <summary>
        /// Color pairs (old -> new) that account for more than 50% of all differing pixels.
        /// Key is the percentage of diff pixels for that pair, value is the (old, new) color pair.
        /// </summary>
        public Dictionary<float, KeyValuePair<Color, Color>> DominantColorPairs { get; private set; } =
            new Dictionary<float, KeyValuePair<Color, Color>>();

        /// <summary>
        /// Gets the percentage of pixels that differ between the two screenshots.
        /// Returns 100.0 if the sizes don't match.
        /// </summary>
        public double DiffPercentage
        {
            get
            {
                if (SizesDiffer)
                    return 100.0; // Completely different if sizes don't match
                int totalPixels = _sizeOld.Width * _sizeOld.Height;
                return totalPixels > 0 ? (100.0 * PixelCount / totalPixels) : 0;
            }
        }

        /// <summary>
        /// Checks if the difference percentage exceeds the given threshold.
        /// </summary>
        public bool ExceedsThreshold(double thresholdPercent)
        {
            return DiffPercentage > thresholdPercent;
        }

        public string DiffText
        {
            get
            {
                if (SizesDiffer)
                {
                    var diffWidth = PercentDiffText(_sizeNew.Width, _sizeOld.Width);
                    var diffHeight = PercentDiffText(_sizeNew.Height, _sizeOld.Height);
                    if (Equals(diffWidth, diffHeight))
                        return $@" ({diffWidth})";
                    else
                        return $@" ({diffWidth} x {diffHeight})";
                }
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

        private static string PercentDiffText(int newLength, int oldLength)
        {
            return Math.Round(100.0 * newLength / oldLength).ToString(CultureInfo.InvariantCulture) + "%";
        }

        private void CalcHighlightImage(Bitmap bmpOld, Bitmap bmpNew, Color highlightColor, int colorTolerance)
        {
            var result = new Bitmap(bmpOld.Width, bmpOld.Height);
            var diffOnly = new Bitmap(bmpOld.Width, bmpOld.Height);
            var alpha = highlightColor.A;
            var colorPairCounts = new Dictionary<long, int>();

            // Fill diff-only image with white background
            using (var g = Graphics.FromImage(diffOnly))
            {
                g.Clear(Color.White);
            }

            _diffPixels.Clear();
            PixelCount = 0;
            for (int y = 0; y < bmpOld.Height; y++)
            {
                for (int x = 0; x < bmpOld.Width; x++)
                {
                    var pixel1 = bmpOld.GetPixel(x, y);
                    var pixel2 = bmpNew.GetPixel(x, y);

                    if (!ColorsMatch(pixel1, pixel2, colorTolerance))
                    {
                        var blendedColor = Color.FromArgb(
                            255,    // Combined pixel colors always sum to 255
                            highlightColor.R * alpha / 255 + pixel1.R * (255 - alpha) / 255,
                            highlightColor.G * alpha / 255 + pixel1.G * (255 - alpha) / 255,
                            highlightColor.B * alpha / 255 + pixel1.B * (255 - alpha) / 255
                        );
                        result.SetPixel(x, y, blendedColor);
                        diffOnly.SetPixel(x, y, highlightColor);
                        _diffPixels.Add(new Point(x, y));
                        PixelCount++;

                        // Track color pair frequency: pack both RGB values into a single long key
                        long key = ((long)pixel1.R << 40) | ((long)pixel1.G << 32) | ((long)pixel1.B << 24) |
                                   ((long)pixel2.R << 16) | ((long)pixel2.G << 8) | pixel2.B;
                        if (colorPairCounts.ContainsKey(key))
                            colorPairCounts[key]++;
                        else
                            colorPairCounts[key] = 1;
                    }
                    else
                    {
                        result.SetPixel(x, y, pixel1);
                    }
                }
            }

            HighlightedImage = PixelCount > 0 ? result : bmpOld;
            DiffOnlyImage = PixelCount > 0 ? diffOnly : null;

            // Find color pairs that account for >50% of differing pixels
            if (PixelCount > 0)
            {
                double threshold = PixelCount * 0.5;
                DominantColorPairs = colorPairCounts
                    .Where(kvp => kvp.Value > threshold)
                    .ToDictionary(
                        kvp => (float)(100.0 * kvp.Value / PixelCount),
                        kvp => new KeyValuePair<Color, Color>(
                            Color.FromArgb((int)((kvp.Key >> 40) & 0xFF), (int)((kvp.Key >> 32) & 0xFF), (int)((kvp.Key >> 24) & 0xFF)),
                            Color.FromArgb((int)((kvp.Key >> 16) & 0xFF), (int)((kvp.Key >> 8) & 0xFF), (int)(kvp.Key & 0xFF))));
            }
        }

        /// <summary>
        /// Returns true if two colors match within the given per-channel tolerance,
        /// or if the color pair is in the allowed system color mappings.
        /// </summary>
        private static bool ColorsMatch(Color c1, Color c2, int tolerance)
        {
            if (c1 == c2)
                return true;
            if (_allowedColorMappings.Contains(ColorPairKey(c1.R, c1.G, c1.B, c2.R, c2.G, c2.B)))
                return true;
            if (tolerance <= 0)
                return false;
            return Math.Abs(c1.R - c2.R) <= tolerance &&
                   Math.Abs(c1.G - c2.G) <= tolerance &&
                   Math.Abs(c1.B - c2.B) <= tolerance;
        }

        /// <summary>
        /// Creates an amplified diff image where each diff pixel is expanded to a filled square.
        /// </summary>
        /// <param name="radius">The radius of the square (total size will be 2*radius+1)</param>
        /// <returns>Amplified diff image, or null if no diff pixels</returns>
        public Bitmap CreateAmplifiedImage(int radius)
        {
            if (_diffPixels.Count == 0 || HighlightedImage == null)
                return null;

            var result = new Bitmap(HighlightedImage.Width, HighlightedImage.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            // Start with the highlighted image as base
            using (var g = Graphics.FromImage(result))
            {
                lock (HighlightedImage)
                {
                    g.DrawImage(HighlightedImage, 0, 0);
                }
            }

            // Collect all unique pixels to highlight (avoids overlapping alpha)
            var amplifiedPixels = new HashSet<Point>();
            foreach (var point in _diffPixels)
            {
                int left = Math.Max(0, point.X - radius);
                int top = Math.Max(0, point.Y - radius);
                int right = Math.Min(result.Width - 1, point.X + radius);
                int bottom = Math.Min(result.Height - 1, point.Y + radius);
                for (int y = top; y <= bottom; y++)
                {
                    for (int x = left; x <= right; x++)
                    {
                        amplifiedPixels.Add(new Point(x, y));
                    }
                }
            }

            // Apply highlight color with alpha blending to each unique pixel once
            foreach (var point in amplifiedPixels)
            {
                var baseColor = result.GetPixel(point.X, point.Y);
                var alpha = _highlightColor.A;
                var blendedColor = Color.FromArgb(
                    255,
                    _highlightColor.R * alpha / 255 + baseColor.R * (255 - alpha) / 255,
                    _highlightColor.G * alpha / 255 + baseColor.G * (255 - alpha) / 255,
                    _highlightColor.B * alpha / 255 + baseColor.B * (255 - alpha) / 255
                );
                result.SetPixel(point.X, point.Y, blendedColor);
            }

            return result;
        }

        /// <summary>
        /// Creates an amplified diff-only image where each diff pixel is expanded to a filled square on white background.
        /// </summary>
        /// <param name="radius">The radius of the square (total size will be 2*radius+1)</param>
        /// <returns>Amplified diff-only image, or null if no diff pixels</returns>
        public Bitmap CreateAmplifiedDiffOnlyImage(int radius)
        {
            if (_diffPixels.Count == 0 || DiffOnlyImage == null)
                return null;

            var result = new Bitmap(DiffOnlyImage.Width, DiffOnlyImage.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (var g = Graphics.FromImage(result))
            {
                // Start with white background
                g.Clear(Color.White);
            }

            // Collect all unique pixels to highlight (avoids overlapping alpha)
            var amplifiedPixels = new HashSet<Point>();
            foreach (var point in _diffPixels)
            {
                int left = Math.Max(0, point.X - radius);
                int top = Math.Max(0, point.Y - radius);
                int right = Math.Min(result.Width - 1, point.X + radius);
                int bottom = Math.Min(result.Height - 1, point.Y + radius);
                for (int y = top; y <= bottom; y++)
                {
                    for (int x = left; x <= right; x++)
                    {
                        amplifiedPixels.Add(new Point(x, y));
                    }
                }
            }

            // Apply highlight color with alpha blending to white background for each unique pixel once
            var alpha = _highlightColor.A;
            var blendedColor = Color.FromArgb(
                255,
                _highlightColor.R * alpha / 255 + 255 * (255 - alpha) / 255,
                _highlightColor.G * alpha / 255 + 255 * (255 - alpha) / 255,
                _highlightColor.B * alpha / 255 + 255 * (255 - alpha) / 255
            );
            foreach (var point in amplifiedPixels)
            {
                result.SetPixel(point.X, point.Y, blendedColor);
            }

            return result;
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
                if (n == lineLen / 2)
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
