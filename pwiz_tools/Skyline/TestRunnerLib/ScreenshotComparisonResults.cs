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
using System.Linq;
using System.Text;

namespace TestRunnerLib
{
    /// <summary>
    /// Static class to hold screenshot comparison results that can be accessed by TestRunner
    /// without requiring a reference to TestUtil.
    /// </summary>
    public static class ScreenshotComparisonResults
    {
        private static readonly List<TestScreenshotResults> _testResults = new List<TestScreenshotResults>();

        /// <summary>
        /// Folder where diff images were saved, or null if none were saved.
        /// </summary>
        public static string DiffImagesFolder { get; set; }

        /// <summary>
        /// Whether screenshot comparison mode was active.
        /// </summary>
        public static bool IsActive { get; set; }

        /// <summary>
        /// Total number of screenshots that passed comparison across all tests.
        /// </summary>
        public static int PassedCount => _testResults.Sum(t => t.PassedCount);

        /// <summary>
        /// Total number of screenshots that failed comparison across all tests.
        /// </summary>
        public static int FailedCount => _testResults.Sum(t => t.FailedCount);

        /// <summary>
        /// Adds results for a single test.
        /// </summary>
        public static void AddTestResults(string testName, string tutorialPath, double thresholdPercent,
            List<ScreenshotResult> results, string diffImagesFolder)
        {
            IsActive = true;
            _testResults.Add(new TestScreenshotResults(testName, tutorialPath, thresholdPercent, results));
            if (!string.IsNullOrEmpty(diffImagesFolder))
                DiffImagesFolder = diffImagesFolder;
        }

        /// <summary>
        /// Generates the combined report for all tests.
        /// </summary>
        public static string Report
        {
            get
            {
                if (_testResults.Count == 0)
                    return null;

                var sb = new StringBuilder();
                sb.AppendLine("Screenshot Comparison Report");
                sb.AppendLine("============================");
                sb.AppendLine();

                foreach (var test in _testResults.OrderBy(t => t.TestName))
                {
                    sb.AppendLine($"Test: {test.TestName}");
                    sb.AppendLine($"  Tutorial Path: {test.TutorialPath}");
                    sb.AppendLine($"  Threshold: {test.ThresholdPercent}% (excluding title bar)");
                    sb.AppendLine($"  Screenshots: {test.Results.Count}");

                    foreach (var r in test.Results.OrderBy(r => r.ScreenshotNumber))
                    {
                        if (!string.IsNullOrEmpty(r.Error))
                        {
                            sb.AppendLine($"    s-{r.ScreenshotNumber:D2}.png: ERROR - {r.Error} ***");
                        }
                        else
                        {
                            var status = r.Passed ? "PASS" : "FAIL";
                            var marker = r.Passed ? "" : " ***";
                            sb.AppendLine($"    s-{r.ScreenshotNumber:D2}.png: {status} ({r.DiffPercentageWithoutTitleBar:F2}% diff){marker}");
                        }
                    }

                    sb.AppendLine($"  Test Summary: {test.PassedCount} passed, {test.FailedCount} failed");
                    sb.AppendLine();
                }

                sb.AppendLine($"Overall Summary: {PassedCount} passed, {FailedCount} failed across {_testResults.Count} tests");
                return sb.ToString();
            }
        }

        /// <summary>
        /// Clears all results.
        /// </summary>
        public static void Clear()
        {
            _testResults.Clear();
            DiffImagesFolder = null;
            IsActive = false;
        }
    }

    /// <summary>
    /// Results for a single test's screenshot comparisons.
    /// </summary>
    public class TestScreenshotResults
    {
        public string TestName { get; }
        public string TutorialPath { get; }
        public double ThresholdPercent { get; }
        public List<ScreenshotResult> Results { get; }

        public int PassedCount => Results.Count(r => r.Passed);
        public int FailedCount => Results.Count - PassedCount;

        public TestScreenshotResults(string testName, string tutorialPath, double thresholdPercent,
            List<ScreenshotResult> results)
        {
            TestName = testName;
            TutorialPath = tutorialPath;
            ThresholdPercent = thresholdPercent;
            Results = results;
        }
    }

    /// <summary>
    /// Lightweight result class for screenshot comparison (no bitmap references).
    /// </summary>
    public class ScreenshotResult
    {
        public int ScreenshotNumber { get; }
        public bool Passed { get; }
        public double DiffPercentage { get; }
        public double DiffPercentageWithoutTitleBar { get; }
        public int DiffPixelCount { get; }
        public string Error { get; }

        public ScreenshotResult(int num, bool passed, double diffPercentage,
            double diffPercentageWithoutTitleBar, int diffPixelCount, string error)
        {
            ScreenshotNumber = num;
            Passed = passed;
            DiffPercentage = diffPercentage;
            DiffPercentageWithoutTitleBar = diffPercentageWithoutTitleBar;
            DiffPixelCount = diffPixelCount;
            Error = error;
        }
    }
}
