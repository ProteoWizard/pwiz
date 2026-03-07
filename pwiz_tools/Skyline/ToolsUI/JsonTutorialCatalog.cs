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
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using SkylineTool;
using TUTORIAL = SkylineTool.JsonToolConstants.TUTORIAL;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// JSON/MCP serialization layer for the tutorial catalog. Handles GitHub fetching,
    /// HTML-to-markdown conversion, and JSON formatting for the JsonToolServer pipe.
    /// Wraps <see cref="TutorialCatalog"/> which owns the authoritative tutorial data.
    /// </summary>
    public static class JsonTutorialCatalog
    {
        private const string GITHUB_RAW_BASE = @"https://raw.githubusercontent.com/ProteoWizard/pwiz";
        private const string TUTORIALS_PATH = @"pwiz_tools/Skyline/Documentation/Tutorials";
        private const string NL = "\n";

        /// <summary>
        /// Format the full tutorial catalog as tab-separated text for MCP consumers.
        /// Uses invariant (English) strings throughout for LLM readability.
        /// </summary>
        public static string FormatCatalog()
        {
            var invariant = System.Globalization.CultureInfo.InvariantCulture;
            var textRes = TutorialTextResources.ResourceManager;
            var linkRes = TutorialLinkResources.ResourceManager;

            var sb = new StringBuilder();
            foreach (var t in TutorialCatalog.Tutorials)
            {
                if (sb.Length > 0)
                    sb.AppendLine();
                sb.Append(TutorialCatalog.GetSectionDisplayNameInvariant(t.Section)).Append(TextUtil.SEPARATOR_TSV);
                sb.Append(t.FolderName).Append(TextUtil.SEPARATOR_TSV);
                sb.Append(textRes.GetString(t.ResourcePrefix + @"_Caption", invariant) ?? t.ResourcePrefix).Append(TextUtil.SEPARATOR_TSV);
                sb.Append(textRes.GetString(t.ResourcePrefix + @"_Description", invariant) ?? string.Empty).Append(TextUtil.SEPARATOR_TSV);
                sb.Append(linkRes.GetString(t.ResourcePrefix + @"_pdf", invariant) ?? string.Empty).Append(TextUtil.SEPARATOR_TSV);
                sb.Append(linkRes.GetString(t.ResourcePrefix + @"_zip", invariant) ?? string.Empty);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Fetch a tutorial from GitHub, convert HTML to markdown, write to a file,
        /// and return JSON metadata with file path and table of contents.
        /// </summary>
        public static string FetchTutorial(string name, string language, string filePath = null)
        {
            var tutorial = TutorialCatalog.FindTutorial(name);
            if (tutorial == null)
            {
                var known = string.Join(@", ", TutorialCatalog.Tutorials.Select(t => t.FolderName));
                throw new ArgumentException(
                    $@"Unknown tutorial: {name}. Available tutorials: {known}");
            }

            var t = tutorial.Value;
            string url = string.Format(@"{0}/{1}/{2}/{3}/{4}/index.html",
                GITHUB_RAW_BASE, GetGitHash(), TUTORIALS_PATH, t.FolderName, language);

            // Fetch HTML
            string html;
            using (var client = new HttpClientWithProgress())
            {
                try
                {
                    html = client.DownloadString(url);
                }
                catch (Exception ex)
                {
                    throw new IOException(string.Format(
                        @"Failed to fetch tutorial from {0}: {1}" +
                        @"\nThe tutorial wiki page is available at: {2}",
                        url, ex.Message, t.WikiUrl), ex);
                }
            }

            // Convert to markdown
            string markdown = ConvertHtmlToMarkdown(html);

            // Build TOC from headings
            var toc = new JArray();
            var lines = markdown.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith(@"## "))
                    toc.Add(new JObject { [nameof(TUTORIAL.heading)] = line.Substring(3).Trim(), [nameof(TUTORIAL.level)] = 2, [nameof(TUTORIAL.line)] = i + 1 });
                else if (line.StartsWith(@"# "))
                    toc.Add(new JObject { [nameof(TUTORIAL.heading)] = line.Substring(2).Trim(), [nameof(TUTORIAL.level)] = 1, [nameof(TUTORIAL.line)] = i + 1 });
            }

            // Write to file
            filePath = filePath ?? GetTutorialFilePath(t.FolderName, language);
            DirectoryEx.CreateForFilePath(filePath);
            File.WriteAllText(filePath, markdown, Encoding.UTF8);

            var result = new JObject
            {
                [nameof(TUTORIAL.file_path)] = filePath.ToForwardSlashPath(),
                [nameof(TUTORIAL.title)] = t.Caption,
                [nameof(TUTORIAL.tutorial)] = t.FolderName,
                [nameof(TUTORIAL.language)] = language,
                [nameof(TUTORIAL.line_count)] = lines.Length,
                [nameof(TUTORIAL.toc)] = toc
            };
            return result.ToString();
        }

        /// <summary>
        /// Fetch a tutorial image from GitHub and save to a file.
        /// Returns JSON with file_path for the downloaded image.
        /// </summary>
        public static string FetchTutorialImage(string name, string imageFilename, string language, string filePath = null)
        {
            var tutorial = TutorialCatalog.FindTutorial(name);
            if (tutorial == null)
            {
                var known = string.Join(@", ", TutorialCatalog.Tutorials.Select(t => t.FolderName));
                throw new ArgumentException(
                    $@"Unknown tutorial: {name}. Available tutorials: {known}");
            }

            var t = tutorial.Value;

            // Validate image filename to prevent path traversal
            if (imageFilename.IndexOfAny(new[] { '\\', '/' }) >= 0 || imageFilename.Contains(@".."))
                throw new ArgumentException(@"Image filename must not contain path separators");

            string url = string.Format(@"{0}/{1}/{2}/{3}/{4}/{5}",
                GITHUB_RAW_BASE, GetGitHash(), TUTORIALS_PATH, t.FolderName, language, imageFilename);

            // Download image
            filePath = filePath ?? GetTutorialImageFilePath(t.FolderName, language, imageFilename);
            DirectoryEx.CreateForFilePath(filePath);

            using (var client = new HttpClientWithProgress())
            {
                try
                {
                    client.DownloadFile(url, filePath);
                }
                catch (Exception ex)
                {
                    throw new IOException(string.Format(
                        @"Failed to fetch tutorial image from {0}: {1}",
                        url, ex.Message), ex);
                }
            }

            var result = new JObject
            {
                [nameof(TUTORIAL.file_path)] = filePath.ToForwardSlashPath(),
                [nameof(TUTORIAL.tutorial)] = t.FolderName,
                [nameof(TUTORIAL.image)] = imageFilename,
                [nameof(TUTORIAL.language)] = language
            };
            return result.ToString();
        }

        /// <summary>
        /// Convert tutorial HTML to markdown suitable for LLM consumption.
        /// Tutorial HTML is well-structured with no dynamic content.
        /// </summary>
        internal static string ConvertHtmlToMarkdown(string html)
        {
            // Remove head, script, style blocks
            html = Regex.Replace(html, @"<head\b[^>]*>.*?</head>", string.Empty,
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<script\b[^>]*>.*?</script>", string.Empty,
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<style\b[^>]*>.*?</style>", string.Empty,
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // Convert headings before stripping other tags
            html = Regex.Replace(html, @"<h1[^>]*>(.*?)</h1>",
                m => NL + "# " + StripTags(m.Groups[1].Value) + NL,
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<h2[^>]*>(.*?)</h2>",
                m => NL + "## " + StripTags(m.Groups[1].Value) + NL,
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<h3[^>]*>(.*?)</h3>",
                m => NL + "### " + StripTags(m.Groups[1].Value) + NL,
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // Convert images to descriptive placeholders
            html = Regex.Replace(html, @"<img\b[^>]*\bsrc\s*=\s*""([^""]+)""[^>]*/?>",
                m =>
                {
                    string src = m.Groups[1].Value;
                    string filename = Path.GetFileName(src);
                    return @"[Screenshot: " + filename + @"]";
                },
                RegexOptions.IgnoreCase);

            // Convert links
            html = Regex.Replace(html, @"<a\b[^>]*\bhref\s*=\s*""([^""]+)""[^>]*>(.*?)</a>",
                m => @"[" + StripTags(m.Groups[2].Value) + @"](" + m.Groups[1].Value + @")",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // Convert bold/strong
            html = Regex.Replace(html, @"<(?:b|strong)\b[^>]*>(.*?)</(?:b|strong)>",
                m => @"**" + m.Groups[1].Value + @"**",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // Convert italic/em
            html = Regex.Replace(html, @"<(?:i|em)\b[^>]*>(.*?)</(?:i|em)>",
                m => @"*" + m.Groups[1].Value + @"*",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // Convert list items (before stripping ol/ul tags)
            html = Regex.Replace(html, @"<li\b[^>]*>(.*?)</li>",
                m => "- " + StripTags(m.Groups[1].Value).Trim() + NL,
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // Convert table cells to tab-separated values
            html = Regex.Replace(html, @"<t[dh]\b[^>]*>(.*?)</t[dh]>",
                m => StripTags(m.Groups[1].Value).Trim() + "\t",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<tr\b[^>]*>", NL, RegexOptions.IgnoreCase);

            // Convert paragraphs and line breaks
            html = Regex.Replace(html, @"<br\s*/?>", NL, RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<p\b[^>]*>", NL, RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</p>", NL, RegexOptions.IgnoreCase);

            // Strip all remaining HTML tags
            html = Regex.Replace(html, @"<[^>]+>", string.Empty);

            // Decode HTML entities
            html = WebUtility.HtmlDecode(html);

            // Normalize whitespace: collapse multiple blank lines, trim lines
            var sb = new StringBuilder();
            int blankCount = 0;
            foreach (string rawLine in html.Split('\n'))
            {
                string line = rawLine.TrimEnd();
                if (string.IsNullOrWhiteSpace(line))
                {
                    blankCount++;
                    if (blankCount <= 2)
                        sb.AppendLine();
                }
                else
                {
                    blankCount = 0;
                    sb.AppendLine(line);
                }
            }
            return sb.ToString().TrimStart();
        }

        private static string StripTags(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;
            string text = Regex.Replace(html, @"<[^>]+>", string.Empty);
            return WebUtility.HtmlDecode(text);
        }

        /// <summary>
        /// Extract git hash from Skyline version string (e.g., "26.1.1.061-6c3244bc0a" -> "6c3244bc0a").
        /// Falls back to "master" if version is unavailable.
        /// </summary>
        private static string GetGitHash()
        {
            string version = Install.Version ?? string.Empty;
            int dashIndex = version.LastIndexOf('-');
            return dashIndex >= 0 ? version.Substring(dashIndex + 1) : @"master";
        }

        private static string GetTutorialFilePath(string tutorialName, string language)
        {
            return Path.Combine(JsonUiService.GetMcpTmpDir(),
                string.Format(@"skyline-tutorial-{0}-{1}.md", tutorialName, language));
        }

        private static string GetTutorialImageFilePath(string tutorialName, string language, string imageFilename)
        {
            string dir = Path.Combine(JsonUiService.GetMcpTmpDir(), @"images", tutorialName, language);
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, imageFilename);
        }
    }
}
