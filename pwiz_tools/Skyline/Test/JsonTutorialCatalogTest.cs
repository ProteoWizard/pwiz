/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// The tutorial catalog's image naming for the MCP: a tutorial page references its own images by file name and
    /// the shared Getting Started images as "../../shared/...", and the markdown placeholder and the fetch must
    /// agree on a name that reaches both - without any network access.
    /// </summary>
    [TestClass]
    public class JsonTutorialCatalogTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestJsonTutorialCatalogImageNames()
        {
            // The placeholder name keeps a shared image's path and strips a tutorial image to its file name.
            AssertEx.AreEqual(@"shared/en/skyline-blank-document.png",
                JsonTutorialCatalog.TutorialImageName(@"../../shared/en/skyline-blank-document.png"));
            AssertEx.AreEqual(@"shared/protein-icon.png",
                JsonTutorialCatalog.TutorialImageName(@"../../shared/protein-icon.png"));
            AssertEx.AreEqual(@"s-01.png", JsonTutorialCatalog.TutorialImageName(@"s-01.png"));
            AssertEx.AreEqual(@"s-01.png", JsonTutorialCatalog.TutorialImageName(@"./s-01.png"));

            string markdown = JsonTutorialCatalog.ConvertHtmlToMarkdown(
                @"<html><body><p>Look: <img src=""../../shared/en/proteomics-interface.png"" /> and <img src=""s-02.png""/></p></body></html>");
            AssertEx.Contains(markdown, @"[Screenshot: shared/en/proteomics-interface.png]");
            AssertEx.Contains(markdown, @"[Screenshot: s-02.png]");

            // A bare name is looked for in the tutorial's own folder, then the shared images for the language,
            // then the shared images common to every language; a shared name is exactly that path.
            var tutorial = TutorialCatalog.Tutorials.First();
            var urls = JsonTutorialCatalog.GetTutorialImageUrls(tutorial, @"en", @"s-01.png");
            AssertEx.AreEqual(3, urls.Length);
            AssertEx.IsTrue(urls[0].EndsWith(@"/" + tutorial.FolderName + @"/en/s-01.png"), urls[0]);
            AssertEx.IsTrue(urls[1].EndsWith(@"/shared/en/s-01.png"), urls[1]);
            AssertEx.IsTrue(urls[2].EndsWith(@"/shared/s-01.png"), urls[2]);
            urls = JsonTutorialCatalog.GetTutorialImageUrls(tutorial, @"en", @"shared/protein-icon.png");
            AssertEx.AreEqual(1, urls.Length);
            AssertEx.IsTrue(urls[0].EndsWith(@"/Tutorials/shared/protein-icon.png"), urls[0]);

            // Only those two shapes are accepted: no other folder, no backslash, no parent-folder escape.
            JsonTutorialCatalog.ValidateTutorialImageFilename(@"s-01.png");
            JsonTutorialCatalog.ValidateTutorialImageFilename(@"shared/en/skyline-blank-document.png");
            foreach (string bad in new[] { @"en/s-01.png", @"shared\en\x.png", @"shared/../en/x.png", @"../shared/x.png", string.Empty })
                AssertEx.ThrowsException<ArgumentException>(() => JsonTutorialCatalog.ValidateTutorialImageFilename(bad));
        }
    }
}
