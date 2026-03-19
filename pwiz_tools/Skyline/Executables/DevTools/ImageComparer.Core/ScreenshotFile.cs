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
using System.Text.RegularExpressions;

namespace ImageComparer.Core
{
    public class ScreenshotFile
    {
        // Supports locale codes like "en", "ja", "zh-CHS"
        private static readonly Regex PATTERN = new Regex(@"\\([a-zA-Z0-9\-]+)\\(\w\w-?[A-Z]*)\\s-(\d\d)\.png");
        private static readonly Regex PATTERN_COVER = new Regex(@"\\([a-zA-Z0-9\-]+)\\(\w\w-?[A-Z]*)\\cover\.png");

        public static bool IsMatch(string filePath)
        {
            return PATTERN.Match(filePath).Success || PATTERN_COVER.Match(filePath).Success;
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
                IsCover = false;
            }
            else
            {
                match = PATTERN_COVER.Match(filePath);
                if (match.Success)
                {
                    Name = match.Groups[1].Value;
                    Locale = match.Groups[2].Value;
                    Number = 0;
                    IsCover = true;
                }
            }
        }

        public string Path { get; }
        public string Name { get; }
        public string Locale { get; }
        public int Number { get; }
        public bool IsCover { get; }

        public bool IsEmpty => string.IsNullOrEmpty(Name);

        private const string BASE_URL = "https://skyline.ms/tutorials/25-1";
        public string UrlInTutorial => IsCover ? $"{BASE_URL}/{Name}/{Locale}/index.html" : $"{BASE_URL}/{Name}/{Locale}/index.html#s-{Number:D2}";
        public string UrlToDownload => $"{BASE_URL}/{RelativePath}";
        public string RelativePath => IsCover ? $"{Name}/{Locale}/cover.png" : $"{Name}/{Locale}/s-{Number:D2}.png";

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
        /// Format: {Name}-{Locale}-s-{Number}-diff-{pixelCount}px.png or {Name}-{Locale}-cover-diff-{pixelCount}px.png
        /// </summary>
        public string GetDiffFileName(int pixelCount)
        {
            return IsCover
                ? $"{Name}-{Locale}-cover-diff-{pixelCount}px.png"
                : $"{Name}-{Locale}-s-{Number:D2}-diff-{pixelCount}px.png";
        }

        /// <summary>
        /// Gets the path to the ai\.tmp folder relative to this screenshot's location.
        /// Navigates up from the Tutorials folder to find the repository root.
        /// </summary>
        public string GetAiTmpFolder()
        {
            // Path is like: ...\pwiz_tools\Skyline\Documentation\Tutorials\{Name}\{Locale}\s-{Number}.png
            // Navigate up to find pwiz_tools, then its parent is the git repo root (e.g., C:\proj\pwiz).
            // The ai\.tmp folder is one level above that (e.g., C:\proj\ai\.tmp).
            var dir = System.IO.Path.GetDirectoryName(Path);
            while (dir != null)
            {
                var parent = System.IO.Path.GetDirectoryName(dir);
                if (parent != null && System.IO.Path.GetFileName(dir) == "pwiz_tools")
                {
                    // parent = repo root (e.g., C:\proj\pwiz)
                    var projectRoot = System.IO.Path.GetDirectoryName(parent);
                    if (projectRoot != null)
                        return System.IO.Path.Combine(projectRoot, "ai", ".tmp");
                }
                dir = parent;
            }
            return null;
        }
    }
}
