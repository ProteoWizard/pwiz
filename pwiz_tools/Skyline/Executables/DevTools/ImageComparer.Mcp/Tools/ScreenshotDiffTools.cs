/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.6) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using ImageComparer.Core;
using ModelContextProtocol.Server;

namespace ImageComparer.Mcp.Tools
{
    [McpServerToolType]
    public static class ScreenshotDiffTools
    {
        /// <summary>
        /// Default highlight color for diff visualization (semi-transparent red).
        /// </summary>
        private static readonly Color HIGHLIGHT_COLOR = Color.FromArgb(128, 255, 0, 0);

        private const int DEFAULT_AMPLIFY_RADIUS = 5;

        /// <summary>
        /// Normalize forward slashes to backslashes at the MCP API boundary.
        /// MCP clients must use forward slashes in JSON (backslashes are JSON escape characters),
        /// but internal code expects Windows-style backslash paths.
        /// </summary>
        private static string NormalizePath(string path) => path?.Replace('/', '\\');

        [McpServerTool(Name = "list_changed_screenshots"),
         Description("Scan tutorial screenshot folders for files that differ from git HEAD. Returns a markdown summary of changed screenshots with pixel counts and dimensions.")]
        public static string ListChangedScreenshots(
            [Description("Full path to the Tutorials directory (e.g., C:\\proj\\pwiz\\pwiz_tools\\Skyline\\Documentation\\Tutorials)")] string tutorialsPath,
            [Description("Minimum pixel difference to report (default 0 = all changes)")] int minPixelDiff = 0)
        {
            tutorialsPath = NormalizePath(tutorialsPath);

            if (!Directory.Exists(tutorialsPath))
                return $"Error: Directory not found: {tutorialsPath}";

            var changedFiles = GitFileHelper.GetChangedFilePaths(tutorialsPath)
                .Where(ScreenshotFile.IsMatch)
                .ToList();

            if (changedFiles.Count == 0)
                return "No changed screenshots found.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Changed Screenshots ({changedFiles.Count} files)");
            sb.AppendLine();

            // Group by tutorial name
            var grouped = changedFiles
                .Select(f => new ScreenshotFile(f))
                .Where(sf => !sf.IsEmpty)
                .GroupBy(sf => sf.Name)
                .OrderBy(g => g.Key);

            int totalDiffs = 0;
            foreach (var group in grouped)
            {
                sb.AppendLine($"## {group.Key}");
                sb.AppendLine();
                sb.AppendLine("| Screenshot | Locale | Path |");
                sb.AppendLine("|---|---|---|");

                foreach (var file in group.OrderBy(f => f.Locale).ThenBy(f => f.Number))
                {
                    var label = file.IsCover ? "cover" : $"s-{file.Number:D2}";
                    sb.AppendLine($"| {label} | {file.Locale} | {file.Path} |");
                    totalDiffs++;
                }
                sb.AppendLine();
            }

            sb.AppendLine($"**Total: {totalDiffs} changed screenshots**");
            sb.AppendLine();
            sb.AppendLine("Use `generate_diff_image` to inspect individual screenshots or `generate_diff_report` to generate diffs for all.");
            return sb.ToString();
        }

        [McpServerTool(Name = "generate_diff_image"),
         Description("Generate a diff image for a specific screenshot, comparing current file against git HEAD. Saves the diff image to ai/.tmp/ and returns the path. Claude can then read the saved image to review the visual differences.")]
        public static string GenerateDiffImage(
            [Description("Full path to the screenshot PNG file")] string screenshotPath,
            [Description("Diff visualization mode: 'highlighted' (diff pixels blended on original), 'diff_only' (diff pixels on white), 'amplified' (expanded diff regions on original), 'amplified_diff_only' (expanded diff on white). Default: highlighted")] string mode = "highlighted",
            [Description("Amplification radius (1-10) for amplified modes. Default: 5")] int amplifyRadius = DEFAULT_AMPLIFY_RADIUS,
            [Description("Highlight color as hex RGB (e.g., 'FF0000' for red, '00FF00' for green). Default: FF0000")] string highlightColor = null,
            [Description("Highlight opacity 0-255 (0=transparent, 255=opaque). Default: 128")] int highlightAlpha = 128)
        {
            screenshotPath = NormalizePath(screenshotPath);

            if (!File.Exists(screenshotPath))
                return $"Error: File not found: {screenshotPath}";

            var file = new ScreenshotFile(screenshotPath);
            if (file.IsEmpty)
                return $"Error: Not a valid tutorial screenshot path: {screenshotPath}";

            try
            {
                // Parse highlight color
                var color = HIGHLIGHT_COLOR;
                if (!string.IsNullOrEmpty(highlightColor))
                {
                    try
                    {
                        var hex = highlightColor.TrimStart('#');
                        if (hex.Length != 6)
                            return $"Error: Invalid color '{highlightColor}'. Use 6 hex digits like 'FF0000'.";
                        var r = Convert.ToInt32(hex.Substring(0, 2), 16);
                        var g = Convert.ToInt32(hex.Substring(2, 2), 16);
                        var b = Convert.ToInt32(hex.Substring(4, 2), 16);
                        color = Color.FromArgb(Math.Max(0, Math.Min(255, highlightAlpha)), r, g, b);
                    }
                    catch
                    {
                        return $"Error: Invalid color '{highlightColor}'. Use hex RGB like 'FF0000'.";
                    }
                }
                else if (highlightAlpha != 128)
                {
                    // Custom alpha with default red color
                    color = Color.FromArgb(Math.Max(0, Math.Min(255, highlightAlpha)), 255, 0, 0);
                }

                // Load current file from disk
                var newBytes = File.ReadAllBytes(screenshotPath);
                var newMs = new MemoryStream(newBytes);
                var newInfo = new ScreenshotInfo(newMs);

                // Load git HEAD version
                var oldBytes = GitFileHelper.GetGitFileBinaryContent(screenshotPath);
                var oldMs = new MemoryStream(oldBytes);
                var oldInfo = new ScreenshotInfo(oldMs);

                var diff = new ScreenshotDiff(oldInfo, newInfo, color);

                if (!diff.IsDiff)
                    return "No differences detected between current file and git HEAD.";

                if (diff.SizesDiffer)
                    return $"Size changed: {diff.SizeOld.Width}x{diff.SizeOld.Height} -> {diff.SizeNew.Width}x{diff.SizeNew.Height}. Cannot generate pixel diff for different-sized images.";

                // Get the appropriate diff image
                Bitmap diffImage = GetDiffImage(diff, mode, amplifyRadius);
                if (diffImage == null)
                    return $"Error: Could not generate diff image in mode '{mode}'.";

                // Save to ai/.tmp/
                var aiTmpFolder = file.GetAiTmpFolder();
                if (aiTmpFolder == null)
                    return "Error: Could not locate ai/.tmp folder from screenshot path.";

                Directory.CreateDirectory(aiTmpFolder);

                var modePrefix = mode == "highlighted" ? "" : $"-{mode.Replace("_", "")}";
                var fileName = file.IsCover
                    ? $"{file.Name}-{file.Locale}-cover{modePrefix}-diff-{diff.PixelCount}px.png"
                    : $"{file.Name}-{file.Locale}-s-{file.Number:D2}{modePrefix}-diff-{diff.PixelCount}px.png";
                var fullPath = Path.Combine(aiTmpFolder, fileName);

                diffImage.Save(fullPath, ImageFormat.Png);

                return $"Diff image saved: {fullPath}\nPixels changed: {diff.PixelCount}\nMode: {mode}";
            }
            catch (Exception ex)
            {
                return $"Error generating diff: {ex.Message}";
            }
        }

        [McpServerTool(Name = "generate_diff_report"),
         Description("Generate a full diff report for all changed screenshots in a tutorials directory. Saves diff images to ai/.tmp/ and returns a markdown report with paths to each diff image for Claude to review.")]
        public static string GenerateDiffReport(
            [Description("Full path to the Tutorials directory")] string tutorialsPath,
            [Description("Diff visualization mode: highlighted, diff_only, amplified, amplified_diff_only")] string mode = "highlighted",
            [Description("Minimum pixel difference to include (default 0)")] int minPixelDiff = 0,
            [Description("Amplification radius for amplified modes (default 5)")] int amplifyRadius = DEFAULT_AMPLIFY_RADIUS)
        {
            tutorialsPath = NormalizePath(tutorialsPath);

            if (!Directory.Exists(tutorialsPath))
                return $"Error: Directory not found: {tutorialsPath}";

            var changedFiles = GitFileHelper.GetChangedFilePaths(tutorialsPath)
                .Where(ScreenshotFile.IsMatch)
                .Select(f => new ScreenshotFile(f))
                .Where(sf => !sf.IsEmpty)
                .OrderBy(sf => sf.Name)
                .ThenBy(sf => sf.Locale)
                .ThenBy(sf => sf.Number)
                .ToList();

            if (changedFiles.Count == 0)
                return "No changed screenshots found.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Screenshot Diff Report");
            sb.AppendLine($"Mode: {mode} | Min pixels: {minPixelDiff}");
            sb.AppendLine();

            int processed = 0, skipped = 0, errors = 0;
            string currentTutorial = null;

            foreach (var file in changedFiles)
            {
                if (currentTutorial != file.Name)
                {
                    currentTutorial = file.Name;
                    sb.AppendLine($"## {currentTutorial}");
                    sb.AppendLine();
                }

                try
                {
                    var result = GenerateDiffImage(file.Path, mode, amplifyRadius);

                    if (result.StartsWith("No differences"))
                    {
                        skipped++;
                        continue;
                    }

                    if (result.StartsWith("Error"))
                    {
                        var label = file.IsCover ? "cover" : $"s-{file.Number:D2}";
                        sb.AppendLine($"- **{label}** ({file.Locale}): {result}");
                        errors++;
                        continue;
                    }

                    // Parse pixel count from result
                    var pixelLine = result.Split('\n').FirstOrDefault(l => l.StartsWith("Pixels changed:"));
                    var pixelCount = 0;
                    if (pixelLine != null)
                        int.TryParse(pixelLine.Replace("Pixels changed: ", ""), out pixelCount);

                    if (minPixelDiff > 0 && pixelCount < minPixelDiff)
                    {
                        skipped++;
                        continue;
                    }

                    var savedLine = result.Split('\n').FirstOrDefault(l => l.StartsWith("Diff image saved:"));
                    var label2 = file.IsCover ? "cover" : $"s-{file.Number:D2}";
                    sb.AppendLine($"- **{label2}** ({file.Locale}): {pixelCount} pixels changed");
                    if (savedLine != null)
                        sb.AppendLine($"  {savedLine}");
                    processed++;
                }
                catch (Exception ex)
                {
                    var label = file.IsCover ? "cover" : $"s-{file.Number:D2}";
                    sb.AppendLine($"- **{label}** ({file.Locale}): Error - {ex.Message}");
                    errors++;
                }
            }

            sb.AppendLine();
            sb.AppendLine($"**Summary**: {processed} diffs generated, {skipped} skipped, {errors} errors");
            return sb.ToString();
        }

        [McpServerTool(Name = "revert_screenshot"),
         Description("Revert a screenshot file to its git HEAD version.")]
        public static string RevertScreenshot(
            [Description("Full path to the screenshot PNG file to revert")] string screenshotPath)
        {
            screenshotPath = NormalizePath(screenshotPath);

            if (!File.Exists(screenshotPath))
                return $"Error: File not found: {screenshotPath}";

            try
            {
                GitFileHelper.RevertFileToHead(screenshotPath);
                return $"Reverted to git HEAD: {screenshotPath}";
            }
            catch (Exception ex)
            {
                return $"Error reverting: {ex.Message}";
            }
        }

        private static Bitmap GetDiffImage(ScreenshotDiff diff, string mode, int amplifyRadius)
        {
            switch (mode)
            {
                case "diff_only":
                    return diff.DiffOnlyImage;
                case "amplified":
                    return diff.CreateAmplifiedImage(amplifyRadius);
                case "amplified_diff_only":
                    return diff.CreateAmplifiedDiffOnlyImage(amplifyRadius);
                default:
                    return diff.HighlightedImage;
            }
        }

    }
}
