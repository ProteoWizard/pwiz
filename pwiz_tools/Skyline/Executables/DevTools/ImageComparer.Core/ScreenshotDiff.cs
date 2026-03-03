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
    /// <summary>
    /// Configuration options for color-tolerant screenshot comparison.
    /// All options default to exact-match behavior for backward compatibility.
    /// </summary>
    public class DiffOptions
    {
        /// <summary>
        /// Per-channel color tolerance. Pixels whose R, G, and B channels
        /// all differ by at most this value are considered matching.
        /// Default: 0 (exact match only).
        /// </summary>
        public int ColorTolerance { get; set; }

        /// <summary>
        /// When true, known system color mappings (e.g., Win10 white 255,255,255
        /// to Win11 light gray 243,243,243) are treated as matching.
        /// </summary>
        public bool UseColorMappings { get; set; }

        /// <summary>
        /// When true, pixels in rounded corner zones of the image are excluded
        /// from the diff. Accounts for Win11 rounded window corners.
        /// </summary>
        public bool ExcludeCorners { get; set; }

        /// <summary>
        /// The radius (in pixels) of corner exclusion zones.
        /// Only used when ExcludeCorners is true. Default: 8.
        /// </summary>
        public int CornerRadius { get; set; } = 8;

        /// <summary>
        /// Default options: exact pixel comparison with no filtering.
        /// </summary>
        public static readonly DiffOptions Default = new DiffOptions();
    }

    public class ScreenshotDiff
    {
        private readonly Size _sizeOld;
        private readonly Size _sizeNew;
        private readonly List<Point> _diffPixels = new List<Point>();
        private readonly Color _highlightColor;
        private readonly DiffOptions _options;

        #region Allowed color mappings (Win10 <-> Win11 system colors)

        /// <summary>
        /// Known system color mappings treated as equivalent. Bidirectional.
        /// Ported from PR #3861 ScreenshotInfo.cs.
        /// </summary>
        private static readonly HashSet<long> _allowedColorMappings = BuildAllowedColorMappings(
            new[]
            {
                new[] { 255, 255, 255, 243, 243, 243 }, // White -> Win11 light gray background
                new[] { 255, 255, 255, 249, 249, 249 }, // White -> Win11 near-white background
                new[] { 225, 225, 225, 253, 253, 253 }, // Gray -> Win11 near-white border/control
            }
        );

        private static HashSet<long> BuildAllowedColorMappings(int[][] mappings)
        {
            var set = new HashSet<long>();
            foreach (var m in mappings)
            {
                // Add both directions
                set.Add(ColorPairKey(m[0], m[1], m[2], m[3], m[4], m[5]));
                set.Add(ColorPairKey(m[3], m[4], m[5], m[0], m[1], m[2]));
            }
            return set;
        }

        private static long ColorPairKey(long r1, long g1, long b1, long r2, long g2, long b2)
        {
            return (r1 << 40) | (g1 << 32) | (b1 << 24) |
                   (r2 << 16) | (g2 << 8) | b2;
        }

        /// <summary>
        /// Three-tier color match: exact equality, allowed mapping, per-channel tolerance.
        /// </summary>
        private static bool ColorsMatch(Color c1, Color c2, int tolerance, bool useColorMappings)
        {
            if (c1 == c2)
                return true;
            if (useColorMappings &&
                _allowedColorMappings.Contains(ColorPairKey(c1.R, c1.G, c1.B, c2.R, c2.G, c2.B)))
                return true;
            if (tolerance <= 0)
                return false;
            return Math.Abs(c1.R - c2.R) <= tolerance &&
                   Math.Abs(c1.G - c2.G) <= tolerance &&
                   Math.Abs(c1.B - c2.B) <= tolerance;
        }

        /// <summary>
        /// Returns true if the pixel at (x, y) falls within a rounded corner exclusion zone.
        /// Uses a quarter-circle test: excluded if within radius of a corner AND outside the arc.
        /// </summary>
        private static bool IsInCornerExclusionZone(int x, int y, int width, int height, int radius)
        {
            // Top-left
            if (x < radius && y < radius)
            {
                int dx = radius - x;
                int dy = radius - y;
                if (dx * dx + dy * dy > radius * radius)
                    return true;
            }
            // Top-right
            if (x >= width - radius && y < radius)
            {
                int dx = x - (width - 1 - radius);
                int dy = radius - y;
                if (dx * dx + dy * dy > radius * radius)
                    return true;
            }
            // Bottom-left
            if (x < radius && y >= height - radius)
            {
                int dx = radius - x;
                int dy = y - (height - 1 - radius);
                if (dx * dx + dy * dy > radius * radius)
                    return true;
            }
            // Bottom-right
            if (x >= width - radius && y >= height - radius)
            {
                int dx = x - (width - 1 - radius);
                int dy = y - (height - 1 - radius);
                if (dx * dx + dy * dy > radius * radius)
                    return true;
            }
            return false;
        }

        #endregion

        public ScreenshotDiff(ScreenshotInfo oldScreenshot, ScreenshotInfo newScreenshot, Color highlightColor)
            : this(oldScreenshot, newScreenshot, highlightColor, DiffOptions.Default)
        {
        }

        public ScreenshotDiff(ScreenshotInfo oldScreenshot, ScreenshotInfo newScreenshot,
            Color highlightColor, DiffOptions options)
        {
            _sizeOld = oldScreenshot.ImageSize;
            MemoryOld = oldScreenshot.Memory?.ToArray();
            _sizeNew = newScreenshot.ImageSize;
            MemoryNew = newScreenshot.Memory?.ToArray();
            _highlightColor = highlightColor;
            _options = options ?? DiffOptions.Default;

            if (!SizesDiffer)
            {
                lock (oldScreenshot.Image)
                {
                    lock (newScreenshot.Image)
                    {
                        CalcHighlightImage(oldScreenshot.Image, newScreenshot.Image, highlightColor, _options);
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

        /// <summary>
        /// Number of pixels that differ after applying all filtering (tolerance, mappings, corners).
        /// </summary>
        public int PixelCount { get; private set; }

        /// <summary>
        /// Total pixels that differ at the exact level, before any filtering.
        /// </summary>
        public int RawPixelCount { get; private set; }

        /// <summary>
        /// Pixels excluded by corner zone filtering.
        /// </summary>
        public int CornerExcludedCount { get; private set; }

        /// <summary>
        /// Pixels excluded by color tolerance or color mapping matching.
        /// </summary>
        public int ColorMatchedCount { get; private set; }

        /// <summary>
        /// Color pairs (old -> new) that account for more than 50% of raw differing pixels.
        /// Key is the percentage of raw diff pixels; value is the (old, new) color pair.
        /// </summary>
        public Dictionary<float, KeyValuePair<Color, Color>> DominantColorPairs { get; private set; }
            = new Dictionary<float, KeyValuePair<Color, Color>>();

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

        private void CalcHighlightImage(Bitmap bmpOld, Bitmap bmpNew, Color highlightColor, DiffOptions options)
        {
            var result = new Bitmap(bmpOld.Width, bmpOld.Height);
            var diffOnly = new Bitmap(bmpOld.Width, bmpOld.Height);
            var alpha = highlightColor.A;
            int width = bmpOld.Width;
            int height = bmpOld.Height;

            // Fill diff-only image with white background
            using (var g = Graphics.FromImage(diffOnly))
            {
                g.Clear(Color.White);
            }

            // Pre-compute the diff-only color by blending highlight with white background
            // (avoids semi-transparent pixels that cause checkerboard artifacts in saved PNGs)
            var diffOnlyColor = BlendWithWhite(highlightColor);

            int tolerance = options.ColorTolerance;
            bool useMappings = options.UseColorMappings;
            bool excludeCorners = options.ExcludeCorners;
            int cornerRadius = options.CornerRadius;
            var colorPairCounts = new Dictionary<long, int>();

            _diffPixels.Clear();
            PixelCount = 0;
            RawPixelCount = 0;
            CornerExcludedCount = 0;
            ColorMatchedCount = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var pixel1 = bmpOld.GetPixel(x, y);
                    var pixel2 = bmpNew.GetPixel(x, y);

                    if (pixel1 == pixel2)
                    {
                        result.SetPixel(x, y, pixel1);
                        continue;
                    }

                    // Pixels differ at exact level
                    RawPixelCount++;

                    // Track color pair frequency for dominant pair analysis
                    long pairKey = ColorPairKey(pixel1.R, pixel1.G, pixel1.B, pixel2.R, pixel2.G, pixel2.B);
                    if (colorPairCounts.ContainsKey(pairKey))
                        colorPairCounts[pairKey]++;
                    else
                        colorPairCounts[pairKey] = 1;

                    // Check corner exclusion (cheap geometric test)
                    if (excludeCorners && IsInCornerExclusionZone(x, y, width, height, cornerRadius))
                    {
                        CornerExcludedCount++;
                        result.SetPixel(x, y, pixel1);
                        continue;
                    }

                    // Check color tolerance/mappings
                    if (ColorsMatch(pixel1, pixel2, tolerance, useMappings))
                    {
                        ColorMatchedCount++;
                        result.SetPixel(x, y, pixel1);
                        continue;
                    }

                    // This is a real (unfiltered) difference
                    var blendedColor = Color.FromArgb(
                        255,
                        highlightColor.R * alpha / 255 + pixel1.R * (255 - alpha) / 255,
                        highlightColor.G * alpha / 255 + pixel1.G * (255 - alpha) / 255,
                        highlightColor.B * alpha / 255 + pixel1.B * (255 - alpha) / 255
                    );
                    result.SetPixel(x, y, blendedColor);
                    diffOnly.SetPixel(x, y, diffOnlyColor);
                    _diffPixels.Add(new Point(x, y));
                    PixelCount++;
                }
            }

            HighlightedImage = PixelCount > 0 ? result : bmpOld;
            DiffOnlyImage = PixelCount > 0 ? diffOnly : null;

            // Compute dominant color pairs (those accounting for >50% of raw diff pixels)
            if (RawPixelCount > 0)
            {
                double threshold = RawPixelCount * 0.5;
                var dominant = new Dictionary<float, KeyValuePair<Color, Color>>();
                foreach (var kvp in colorPairCounts)
                {
                    if (kvp.Value > threshold)
                    {
                        float pct = (float)(100.0 * kvp.Value / RawPixelCount);
                        var oldColor = Color.FromArgb(255,
                            (int)((kvp.Key >> 40) & 0xFF),
                            (int)((kvp.Key >> 32) & 0xFF),
                            (int)((kvp.Key >> 24) & 0xFF));
                        var newColor = Color.FromArgb(255,
                            (int)((kvp.Key >> 16) & 0xFF),
                            (int)((kvp.Key >> 8) & 0xFF),
                            (int)(kvp.Key & 0xFF));
                        dominant[pct] = new KeyValuePair<Color, Color>(oldColor, newColor);
                    }
                }
                DominantColorPairs = dominant;
            }
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
