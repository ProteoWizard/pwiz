/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using HtmlAgilityPack;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class LocalizedHtmlTutorialsTest : AbstractUnitTest
    {

        [TestMethod]
        public void TestLocalizedTutorialHtml()
        {
            string tutorialRoot = GetHtmlTutorialsRoot();
            if (tutorialRoot == null)
            {
                return;
            }

            var failures = new List<Exception>();
            foreach (var folder in Directory.GetDirectories(tutorialRoot))
            {
                foreach (var language in new[] { "ja", "zh-CHS" })
                {
                    var languageFolder = Path.Combine(folder, language);
                    if (File.Exists(Path.Combine(languageFolder, "index.html"))
                        && File.Exists(Path.Combine(languageFolder, "invariant.html")))
                    {
                        try
                        {
                            VerifyInvariantMatchesLocalized(languageFolder);
                        }
                        catch (Exception ex)
                        {
                            failures.Add(ex);
                        }
                    }
                }
            }

            if (failures.Count > 0)
            {
                var exception = new AggregateException(string.Format("{0} failures", failures.Count), failures);
                Console.Out.WriteLine("Exceptions running {0}: {1}", nameof(TestLocalizedTutorialHtml), exception);
                throw exception;
            }
        }


        private string GetHtmlTutorialsRoot()
        {
            string codeBaseRoot = GetCodeBaseRoot();
            if (codeBaseRoot == null)
            {
                return null;
            }

            var path = Path.Combine(codeBaseRoot, "Documentation", "Tutorials");
            if (!Directory.Exists(path))
            {
                // Skip the test if the folder does not exist: the source code is probably not where it needs to be
                return null;
            }
            return path;
        }

        private string GetCodeBaseRoot()
        {
            var thisFile = new StackTrace(true).GetFrame(0).GetFileName();
            if (string.IsNullOrEmpty(thisFile))
            {
                return null;
            }
            // ReSharper disable once PossibleNullReferenceException
            return thisFile.Replace("\\Test\\LocalizedHtmlTutorialsTest.cs", string.Empty);
        }

        private void VerifyInvariantMatchesLocalized(string folder)
        {
            var invariantDoc = new HtmlDocument();
            invariantDoc.Load(Path.Combine(folder, "invariant.html"), Encoding.UTF8);
            var localizedDoc = new HtmlDocument();
            localizedDoc.Load(Path.Combine(folder, "index.html"), Encoding.UTF8);

            var invariantNodes = ListLocalizableElements(invariantDoc.DocumentNode).ToList();
            var localizedNodes = ListLocalizableElements(localizedDoc.DocumentNode).ToList();
            int minNodeCount = Math.Min(invariantNodes.Count, localizedNodes.Count);
            for (int i = 0; i < minNodeCount; i++)
            {
                var invariantNode = invariantNodes[i];
                var localizedNode = localizedNodes[i];
                if (!Equals(invariantNode.XPath, localizedNode.XPath))
                {
                    // Set a breakpoint below to stop where the comparison fails
                    Assert.AreEqual(invariantNode.XPath, localizedNode.XPath,
                        "node from invariant.html:{0}\r\ndoes not match node from index.html:{1}\r\nin folder:{2}",
                        invariantNode.InnerHtml, localizedNode.InnerHtml, folder);
                }
            }

            if (invariantNodes.Count > minNodeCount)
            {
                Assert.AreEqual(minNodeCount, invariantNodes.Count, "invariant.html has extra node XPath:{0}\r\nInnerHTML:{1}\r\nFolder:{2}",
                    invariantNodes[minNodeCount].XPath, invariantNodes[minNodeCount].InnerHtml, folder);
            }

            if (localizedNodes.Count > minNodeCount)
            {
                Assert.AreEqual(minNodeCount, localizedNodes.Count, "index.html has extra node XPath:{0}\r\nInnerHTML:{1}\r\nFolder:{2}",
                    localizedNodes[minNodeCount].XPath, localizedNodes[minNodeCount].InnerHtml, folder);
            }
        }

        public static IEnumerable<string> LocalizableXPaths
        {
            get
            {
                yield return "/html/head/title";
                yield return "/html/body/p";
                yield return "/html/body/h1";
                yield return "/html/body/h2";
                yield return "/html/body/ul/li";
                yield return "/html/body/ol/li";
                yield return "/html/body/table/tr/td";
                yield return "/html/body/div/table/tr/td";
            }
        }

        public bool ContainsLocalizableText(HtmlNode node)
        {
            return node.InnerText.Any(ch => !char.IsWhiteSpace(ch));
        }

        public static string RemoveIndexesFromXPath(string xPath)
        {
            StringBuilder stringBuilder = new StringBuilder();
            bool inBrackets = false;
            foreach (var ch in xPath)
            {
                if (inBrackets)
                {
                    if (ch == ']')
                    {
                        inBrackets = false;
                    }
                }
                else
                {
                    if (ch == '[')
                    {
                        inBrackets = true;
                    }
                    else
                    {
                        stringBuilder.Append(ch);
                    }
                }
            }

            return stringBuilder.ToString();
        }
        private static IEnumerable<HtmlNode> ListLocalizableElements(HtmlNode node)
        {
            string strippedXPath = RemoveIndexesFromXPath(node.XPath)!;
            if (LocalizableXPaths.Contains(strippedXPath))
            {
                return new[] { node };
            }

            return node.ChildNodes.SelectMany(ListLocalizableElements);
        }

    }
}
