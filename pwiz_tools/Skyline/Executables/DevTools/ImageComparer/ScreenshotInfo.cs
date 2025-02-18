﻿/*
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
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ImageComparer
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

    internal class ScreenshotFile
    {
        private static readonly Regex PATTERN = new Regex(@"\\([a-zA-Z0-9\-]+)\\(\w\w-?[A-Z]*)\\s-(\d\d)\.png");

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
        public string RelativePath => $"{Name}/{Locale}/s-{Number:D2}.png";

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

    internal class ScreenshotDiff
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
                            255,    // Combined pixel colors always sum to 255
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