/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
 *
 * Copyright 2025 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using TestRunnerLib;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// Compares captured screenshots against existing local screenshots from the git repository.
    /// </summary>
    public class ScreenshotComparer
    {
        public const double DEFAULT_THRESHOLD_PERCENT = 15.0;

        private readonly string _tutorialPath;
        private readonly double _thresholdPercent;

        public List<ComparisonResult> Results { get; } = new List<ComparisonResult>();

        public ScreenshotComparer(string tutorialPath, double thresholdPercent = DEFAULT_THRESHOLD_PERCENT)
        {
            _tutorialPath = tutorialPath;
            _thresholdPercent = thresholdPercent;
        }

        /// <summary>
        /// Load existing screenshot from local git files.
        /// </summary>
        private ScreenshotInfo LoadExistingScreenshot(int screenshotNum)
        {
            var filePath = Path.Combine(_tutorialPath, $"s-{screenshotNum:D2}.png");
            if (!File.Exists(filePath))
                return null;
            var bytes = File.ReadAllBytes(filePath);
            var ms = new MemoryStream(bytes);
            return new ScreenshotInfo(ms);
        }

        /// <summary>
        /// Saves a bitmap to a MemoryStream as PNG format.
        /// </summary>
        private static MemoryStream SaveToMemory(Bitmap bmp)
        {
            var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms;
        }

        /// <summary>
        /// Compare captured screenshot against existing local screenshot.
        /// </summary>
        public ComparisonResult Compare(int screenshotNum, Bitmap capturedScreenshot)
        {
            var existing = LoadExistingScreenshot(screenshotNum);
            if (existing == null)
            {
                var result = new ComparisonResult(screenshotNum,
                    newImage: capturedScreenshot,
                    error: "Existing screenshot not found");
                Results.Add(result);
                return result;
            }

            var capturedInfo = new ScreenshotInfo(SaveToMemory(capturedScreenshot), capturedScreenshot);
            const int COLOR_TOLERANCE = 3; // Allow minor color differences
            var diff = new ScreenshotDiff(existing, capturedInfo, Color.FromArgb(128, 255, 0, 0), COLOR_TOLERANCE);

            var comparisonResult = new ComparisonResult(
                screenshotNum,
                passed: !diff.ExceedsThreshold(_thresholdPercent),
                diffPercentage: diff.DiffPercentage,
                diffPercentageWithoutTitleBar: diff.DiffPercentage,
                diffPixelCount: diff.PixelCount,
                originalImage: existing.Image,
                newImage: capturedScreenshot,
                diffImage: diff.HighlightedImage,
                dominantColorPairs: diff.DominantColorPairs
            );
            Results.Add(comparisonResult);
            return comparisonResult;
        }


        /// <summary>
        /// Save original, new, and diff images to output folder for any comparison with differences.
        /// </summary>
        public void SaveComparisonImages(string outputFolder)
        {
            Directory.CreateDirectory(outputFolder);
            foreach (var r in Results.Where(r => r.DiffPercentage > 0))
            {
                var prefix = $"s-{r.ScreenshotNumber:D2}";

                if (r.OriginalImage != null)
                {
                    var originalPath = Path.Combine(outputFolder, $"{prefix}-original.png");
                    r.OriginalImage.Save(originalPath, ImageFormat.Png);
                }

                if (r.NewImage != null)
                {
                    var newPath = Path.Combine(outputFolder, $"{prefix}-new.png");
                    r.NewImage.Save(newPath, ImageFormat.Png);
                }

                if (r.DiffImage != null)
                {
                    var diffPath = Path.Combine(outputFolder, $"{prefix}-diff.png");
                    r.DiffImage.Save(diffPath, ImageFormat.Png);
                }
            }
        }

        /// <summary>
        /// Gets the list of failed results (those exceeding threshold or with errors).
        /// </summary>
        public List<ComparisonResult> GetFailures()
        {
            return Results.Where(r => !r.Passed).ToList();
        }

        /// <summary>
        /// Finalizes the comparison results and adds them to the global results accumulator.
        /// </summary>
        /// <param name="outputFolder">Base folder to save images to</param>
        /// <param name="testName">Name of the test (used as subdirectory name)</param>
        public void FinalizeResults(string outputFolder, string testName)
        {
            string testOutputFolder = null;
            bool hasDiffs = Results.Any(r => r.DiffPercentage > 0);

            if (hasDiffs && !string.IsNullOrEmpty(outputFolder))
            {
                testOutputFolder = Path.Combine(outputFolder, testName ?? "Unknown");
                SaveComparisonImages(testOutputFolder);
            }

            // Convert to lightweight results for the accumulator
            var lightweightResults = Results.Select(r => new ScreenshotResult(
                r.ScreenshotNumber, r.Passed, r.DiffPercentage,
                r.DiffPercentageWithoutTitleBar, r.DiffPixelCount, r.Error,
                r.DominantColorPairs.OrderByDescending(kvp => kvp.Key)
                    .Select(kvp => $"({kvp.Value.Key.R},{kvp.Value.Key.G},{kvp.Value.Key.B}) -> ({kvp.Value.Value.R},{kvp.Value.Value.G},{kvp.Value.Value.B}): {kvp.Key:F1}%")
                    .ToList())).ToList();

            ScreenshotComparisonResults.AddTestResults(testName ?? "Unknown", _tutorialPath,
                _thresholdPercent, lightweightResults, testOutputFolder);
        }
    }

    /// <summary>
    /// Result of comparing a captured screenshot against an existing screenshot.
    /// </summary>
    public class ComparisonResult
    {
        public int ScreenshotNumber { get; }
        public bool Success => string.IsNullOrEmpty(Error);
        public bool Passed { get; }
        public double DiffPercentage { get; }
        public double DiffPercentageWithoutTitleBar { get; }
        public int DiffPixelCount { get; }
        public Bitmap OriginalImage { get; }
        public Bitmap NewImage { get; }
        public Bitmap DiffImage { get; }
        public string Error { get; }
        public Dictionary<float, KeyValuePair<Color, Color>> DominantColorPairs { get; }

        public ComparisonResult(int num, bool passed = false, double diffPercentage = 0,
            double diffPercentageWithoutTitleBar = 0, int diffPixelCount = 0,
            Bitmap originalImage = null, Bitmap newImage = null,
            Bitmap diffImage = null, string error = null,
            Dictionary<float, KeyValuePair<Color, Color>> dominantColorPairs = null)
        {
            ScreenshotNumber = num;
            Passed = passed && string.IsNullOrEmpty(error);
            DiffPercentage = diffPercentage;
            DiffPercentageWithoutTitleBar = diffPercentageWithoutTitleBar;
            DiffPixelCount = diffPixelCount;
            OriginalImage = originalImage;
            NewImage = newImage;
            DiffImage = diffImage;
            Error = error;
            DominantColorPairs = dominantColorPairs ?? new Dictionary<float, KeyValuePair<Color, Color>>();
        }
    }
}
