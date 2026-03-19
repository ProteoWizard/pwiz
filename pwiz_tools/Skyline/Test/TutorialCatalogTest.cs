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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class TutorialCatalogTest : AbstractUnitTest
    {
        /// <summary>
        /// Validates that every tutorial in TutorialCatalog has corresponding
        /// invariant resource strings in the .resx files. Catches missing entries
        /// at test time rather than at runtime.
        /// </summary>
        [TestMethod]
        public void TestTutorialCatalogResources()
        {
            var invariant = CultureInfo.InvariantCulture;
            var textRes = TutorialTextResources.ResourceManager;
            var linkRes = TutorialLinkResources.ResourceManager;
            var imageRes = TutorialImageResources.ResourceManager;

            var errors = new List<string>();

            // Validate section display names
            foreach (var section in TutorialCatalog.SectionOrder)
            {
                string sectionName = textRes.GetString(section, invariant);
                if (string.IsNullOrEmpty(sectionName))
                    errors.Add(string.Format("Missing TutorialTextResources entry: {0}", section));
            }

            // Validate each tutorial entry
            foreach (var t in TutorialCatalog.Tutorials)
            {
                string prefix = t.ResourcePrefix;

                // TutorialTextResources: Caption and Description
                string caption = textRes.GetString(prefix + @"_Caption", invariant);
                if (string.IsNullOrEmpty(caption))
                    errors.Add(string.Format("Missing TutorialTextResources: {0}_Caption", prefix));

                string description = textRes.GetString(prefix + @"_Description", invariant);
                if (string.IsNullOrEmpty(description))
                    errors.Add(string.Format("Missing TutorialTextResources: {0}_Description", prefix));

                // TutorialLinkResources: zip and pdf URLs
                string zipUrl = linkRes.GetString(prefix + @"_zip", invariant);
                if (string.IsNullOrEmpty(zipUrl))
                    errors.Add(string.Format("Missing TutorialLinkResources: {0}_zip", prefix));

                string pdfUrl = linkRes.GetString(prefix + @"_pdf", invariant);
                if (string.IsNullOrEmpty(pdfUrl))
                    errors.Add(string.Format("Missing TutorialLinkResources: {0}_pdf", prefix));

                // TutorialImageResources: start icon
                object icon = imageRes.GetObject(prefix + @"_start", invariant);
                if (icon == null)
                    errors.Add(string.Format("Missing TutorialImageResources: {0}_start", prefix));

                // Section must be one of the defined sections
                if (!TutorialCatalog.SectionOrder.Contains(t.Section))
                    errors.Add(string.Format("Tutorial {0} has unknown section: {1}", prefix, t.Section));
            }

            // Verify no duplicate folder names
            var folderNames = TutorialCatalog.Tutorials.Select(t => t.FolderName).ToList();
            var duplicateFolders = folderNames.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            foreach (var dup in duplicateFolders)
                errors.Add(string.Format("Duplicate folder name in TutorialCatalog: {0}", dup));

            // Verify no duplicate resource prefixes
            var prefixes = TutorialCatalog.Tutorials.Select(t => t.ResourcePrefix).ToList();
            var duplicatePrefixes = prefixes.GroupBy(p => p).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            foreach (var dup in duplicatePrefixes)
                errors.Add(string.Format("Duplicate resource prefix in TutorialCatalog: {0}", dup));

            if (errors.Count > 0)
                Assert.Fail(string.Join("\n", errors));
        }

        /// <summary>
        /// Validates that FormatCatalog produces non-empty output with expected structure.
        /// </summary>
        [TestMethod]
        public void TestTutorialCatalogFormat()
        {
            string catalog = JsonTutorialCatalog.FormatCatalog();
            Assert.IsFalse(string.IsNullOrEmpty(catalog));

            var lines = catalog.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            Assert.AreEqual(TutorialCatalog.Tutorials.Length, lines.Length,
                "FormatCatalog line count should match tutorial count");

            // Each line should have 6 tab-separated fields
            foreach (var line in lines)
            {
                var fields = line.Split('\t');
                Assert.AreEqual(6, fields.Length,
                    string.Format("Expected 6 tab-separated fields, got {0} in: {1}", fields.Length, line.TrimEnd()));
            }
        }

        /// <summary>
        /// Validates that ConvertHtmlToMarkdown handles basic tutorial HTML patterns.
        /// </summary>
        [TestMethod]
        public void TestConvertHtmlToMarkdown()
        {
            const string html = @"<html><head><script>alert('x');</script></head>
<body>
<h1 class=""document-title"">Test Tutorial</h1>
<p>This is a <b>bold</b> and <i>italic</i> paragraph.</p>
<h2>Getting Started</h2>
<p>Click <a href=""https://example.com"">here</a>.</p>
<img src=""s-01.png"" />
</body></html>";

            string markdown = JsonTutorialCatalog.ConvertHtmlToMarkdown(html);

            // Verify script was stripped
            Assert.IsFalse(markdown.Contains("alert"));

            // Verify heading conversion
            Assert.IsTrue(markdown.Contains("# Test Tutorial"));
            Assert.IsTrue(markdown.Contains("## Getting Started"));

            // Verify bold/italic
            Assert.IsTrue(markdown.Contains("**bold**"));
            Assert.IsTrue(markdown.Contains("*italic*"));

            // Verify link conversion
            Assert.IsTrue(markdown.Contains("[here](https://example.com)"));

            // Verify image conversion
            Assert.IsTrue(markdown.Contains("[Screenshot: s-01.png]"));
        }
    }
}
