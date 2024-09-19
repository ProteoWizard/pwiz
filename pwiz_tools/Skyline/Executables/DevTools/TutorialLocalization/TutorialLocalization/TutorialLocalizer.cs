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
using System.IO;
using System.Linq;
using System.Text;
using F23.StringSimilarity;
using HtmlAgilityPack;

namespace TutorialLocalization
{
    public class TutorialLocalizer
    {
        private static IEnumerable<string> LocalizableXPaths
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

        public TutorialLocalizer(TutorialsLocalizer localizer, string rootFolder, string relativePath, string language)
        {
            TutorialsLocalizer = localizer;
            RootFolder = rootFolder;
            RelativePath = relativePath;
            Language = language;
        }

        public TutorialsLocalizer TutorialsLocalizer { get; }
        public string RootFolder { get; }
        public string RelativePath { get; }
        public string Language { get; }


        public void Localize()
        {
            var mergedDocument = ReadEnglishDocument();
            var localizedFolder = Path.Combine(RootFolder, Language);
            var invariantDoc = TutorialsLocalizer.ReadHtmlDocument(Path.Combine(localizedFolder, "invariant.html"));
            var localizedDoc = TutorialsLocalizer.ReadHtmlDocument(Path.Combine(localizedFolder, "index.html"));
            if (mergedDocument == null || invariantDoc == null || localizedDoc == null)
            {
                return;
            }

            var translatedStrings = new HashSet<string>();
            var localizationRecords = new List<LocalizationRecord>();
            var exactLocalizedValues = new Dictionary<StringKey, string>();
            foreach (var invariantEl in ListLocalizableElements(invariantDoc.DocumentNode))
            {
                var localizedEl = localizedDoc.DocumentNode.SelectSingleNode(invariantEl.XPath);
                if (localizedEl != null)
                {
                    exactLocalizedValues[ExactStringKey(invariantEl)] = localizedEl.InnerHtml;
                }
            }
            var normalizedLocalizedValues = new Dictionary<StringKey, string>();
            foreach (var group in exactLocalizedValues.GroupBy(kvp=>new StringKey(RemoveIndexesFromXPath(kvp.Key.XPath), kvp.Key.EnglishText), kvp=>kvp.Value))
            {
                if (group.Select(NormalizeWhitespace).Distinct().Count() == 1)
                {
                    normalizedLocalizedValues.Add(group.Key, group.First());
                }
            }

            var withoutXPathIndexes =
                exactLocalizedValues.ToLookup(kvp => RemoveIndexesFromXPath(kvp.Key.XPath), kvp => kvp.Key);

            foreach (var el in ListLocalizableElements(mergedDocument.DocumentNode))
            {
                if (!ContainsLocalizableText(el))
                {
                    continue;
                }
                string localizedValue;
                if (exactLocalizedValues.TryGetValue(ExactStringKey(el), out localizedValue) || normalizedLocalizedValues.TryGetValue(NormalizedStringKey(el), out localizedValue))
                {
                    translatedStrings.Add(NormalizeWhitespace(el.InnerHtml));
                    el.InnerHtml = localizedValue;
                }
                else
                {
                    localizationRecords.Add(new LocalizationRecord(RelativePath, el.XPath, el.InnerHtml));
                }
            }

            for (int iLocalizationRecord = 0; iLocalizationRecord < localizationRecords.Count; iLocalizationRecord++)
            {
                var localizationRecord = localizationRecords[iLocalizationRecord];
                var candidates = withoutXPathIndexes[RemoveIndexesFromXPath(localizationRecord.XPath)].ToList();
                var bestMatch = FindBestMatch(localizationRecord.English, candidates.Select(stringKey => stringKey.EnglishText));
                if (bestMatch != null)
                {
                    var bestCandidates = candidates.Where(candidate=>candidate.EnglishText == bestMatch.Item2).ToList();
                    var localizedValues = bestCandidates.Select(candidate => exactLocalizedValues[candidate])
                        .Select(NormalizeWhitespace).Distinct().ToList();
                    if (localizedValues.Count > 1)
                    {
                        var originalLocalized =
                            "Ambiguous: " + string.Join(Environment.NewLine + "OR: ", localizedValues);
                        localizationRecord =
                            localizationRecord.ChangeOriginalEnglish(bestCandidates[0].EnglishText, originalLocalized);
                    }
                    else if (!translatedStrings.Contains(bestCandidates[0].EnglishText))
                    {
                        localizationRecord = localizationRecord.ChangeOriginalEnglish(bestMatch.Item2,
                            localizedValues[0]);
                    }
                }
                TutorialsLocalizer.AddLocalizationRecord(Language, localizationRecord);
            }

            TutorialsLocalizer.AddHtmlDocument(ReadEnglishDocument(), Path.Combine(RelativePath, Language, "invariant.html"));
            TutorialsLocalizer.AddHtmlDocument(mergedDocument, Path.Combine(RelativePath, Language, "index.html"));
            TutorialsLocalizer.AddFilesInFolder(Path.Combine(RootFolder, Language), Path.Combine(RelativePath, Language));
        }

        private HtmlDocument ReadEnglishDocument()
        {
            return TutorialsLocalizer.ReadHtmlDocument(Path.Combine(RootFolder, "en", "index.html"));
        }

        private static string RemoveIndexesFromXPath(string xPath)
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

        private sealed class StringKey
        {
            public StringKey(string xPath, string englishText)
            {
                XPath = xPath;
                EnglishText = englishText;
            }

            public string XPath { get; }
            public string EnglishText { get; }

            private bool Equals(StringKey other)
            {
                return XPath == other.XPath && EnglishText == other.EnglishText;
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((StringKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (XPath.GetHashCode() * 397) ^ EnglishText.GetHashCode();
                }
            }

            public override string ToString()
            {
                return XPath + ":" + EnglishText;
            }
        }

        private string NormalizeWhitespace(string text)
        {
            var stringBuilder = new StringBuilder();
            bool whitespace = true;
            foreach (var ch in text)
            {
                if (char.IsWhiteSpace(ch))
                {
                    whitespace = true;
                }
                else
                {
                    if (whitespace && stringBuilder.Length > 0)
                    {
                        stringBuilder.Append(" ");
                    }
                    whitespace = false;
                    stringBuilder.Append(ch);
                }
            }
            return stringBuilder.ToString();
        }

        private StringKey NormalizedStringKey(HtmlNode htmlNode)
        {
            return new StringKey(RemoveIndexesFromXPath(htmlNode.XPath), NormalizeWhitespace(htmlNode.InnerHtml));
        }

        private StringKey ExactStringKey(HtmlNode htmlNode)
        {
            return new StringKey(htmlNode.XPath, NormalizeWhitespace(htmlNode.InnerHtml));
        }

        private static Levenshtein _levenshtein = new Levenshtein();
        private Tuple<int, string> FindBestMatch(string target, IEnumerable<string> candidates)
        {
            int bestDistance = int.MaxValue;
            string bestMatch = null;
            foreach (var candidate in candidates)
            {
                var distance = (int)_levenshtein.Distance(target, candidate, bestDistance);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestMatch = candidate;
                }
            }

            return bestMatch == null ? null : Tuple.Create(bestDistance, bestMatch);
        }
        private bool ContainsLocalizableText(HtmlNode node)
        {
            return node.InnerText.Any(ch => !char.IsWhiteSpace(ch));
        }
    }
}
