/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>
 *                   MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.6) <noreply .at. anthropic.com>
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
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;

namespace ImageComparer.Core
{
    public class ScreenshotDiff
    {
        private readonly Size _sizeOld;
        private readonly Size _sizeNew;
        private readonly List<Point> _diffPixels = new List<Point>();
        private readonly Color _highlightColor;

        public ScreenshotDiff(ScreenshotInfo oldScreenshot, ScreenshotInfo newScreenshot, Color highlightColor)
        {
            _sizeOld = oldScreenshot.ImageSize;
            MemoryOld = oldScreenshot.Memory?.ToArray();
            _sizeNew = newScreenshot.ImageSize;
            MemoryNew = newScreenshot.Memory?.ToArray();
            _highlightColor = highlightColor;

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
        public bool BytesDiffer => MemoryOld.Length != MemoryNew.Length || !MemoryOld.SequenceEqual(MemoryNew);
        public Bitmap HighlightedImage { get; private set; }
        public Bitmap DiffOnlyImage { get; private set; }
        public int PixelCount { get; private set; }
        public Size SizeOld => _sizeOld;
        public Size SizeNew => _sizeNew;

        /// <summary>
        /// Raw bytes of the old image (for binary diff display in UI consumers).
        /// </summary>
        public byte[] MemoryOld { get; }

        /// <summary>
        /// Raw bytes of the new image (for binary diff display in UI consumers).
        /// </summary>
        public byte[] MemoryNew { get; }

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
                    if (MemoryOld.Length != MemoryNew.Length)
                        return $" ({MemoryNew.Length - MemoryOld.Length} bytes)";
                    int startIndex = 0, diffCount = 0;
                    for (int i = 0; i < MemoryOld.Length; i++)
                    {
                        if (MemoryOld[i] != MemoryNew[i])
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
            var diffOnly = new Bitmap(bmpOld.Width, bmpOld.Height);
            var alpha = highlightColor.A;

            // Fill diff-only image with white background
            using (var g = Graphics.FromImage(diffOnly))
            {
                g.Clear(Color.White);
            }

            // Pre-compute the diff-only color by blending highlight with white background
            // (avoids semi-transparent pixels that cause checkerboard artifacts in saved PNGs)
            var diffOnlyColor = BlendWithWhite(highlightColor);

            _diffPixels.Clear();
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
                        diffOnly.SetPixel(x, y, diffOnlyColor);
                        _diffPixels.Add(new Point(x, y));
                        PixelCount++;
                    }
                    else
                    {
                        result.SetPixel(x, y, pixel1);
                    }
                }
            }

            HighlightedImage = PixelCount > 0 ? result : bmpOld;
            DiffOnlyImage = PixelCount > 0 ? diffOnly : null;
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

            var result = new Bitmap(HighlightedImage.Width, HighlightedImage.Height, PixelFormat.Format32bppArgb);

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

            var result = new Bitmap(DiffOnlyImage.Width, DiffOnlyImage.Height, PixelFormat.Format32bppArgb);

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
            var amplifiedColor = BlendWithWhite(_highlightColor);
            foreach (var point in amplifiedPixels)
            {
                result.SetPixel(point.X, point.Y, amplifiedColor);
            }

            return result;
        }

        /// <summary>
        /// Blend a semi-transparent color with a white background, producing a fully opaque color.
        /// Avoids checkerboard artifacts when saving diff-only images as PNG.
        /// </summary>
        private static Color BlendWithWhite(Color color)
        {
            var alpha = color.A;
            return Color.FromArgb(
                255,
                color.R * alpha / 255 + 255 * (255 - alpha) / 255,
                color.G * alpha / 255 + 255 * (255 - alpha) / 255,
                color.B * alpha / 255 + 255 * (255 - alpha) / 255
            );
        }
    }
}
