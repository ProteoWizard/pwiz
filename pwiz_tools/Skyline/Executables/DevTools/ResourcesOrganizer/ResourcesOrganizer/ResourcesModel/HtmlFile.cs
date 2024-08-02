using System.Collections.Immutable;
using System.Data.SQLite;
using System.Runtime.InteropServices;
using System.Text;
using F23.StringSimilarity;
using HtmlAgilityPack;

namespace ResourcesOrganizer.ResourcesModel
{
    public record HtmlFile(string RelativePath) : LocalizableFile(RelativePath)
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
            }
        }

        public HtmlFile ReadEnglish(string path)
        {
            var doc = ParseHtmlDocument(path);
            var entries = new List<ResourceEntry>();
            foreach (var el in ListLocalizableElements(doc.DocumentNode))
            {
                if (!ContainsLocalizableText(el))
                {
                    continue;
                }
                var key = new InvariantResourceKey()
                {
                    File = RelativePath,
                    Name = el.XPath,
                    Value = el.InnerHtml
                };
                var resourceEntry = new ResourceEntry(el.XPath, key);
                entries.Add(resourceEntry);
            }
            foreach (var entry in entries)
            {
                doc.DocumentNode.SelectSingleNode(entry.Name).InnerHtml = string.Empty;
            }

            return this with
            {
                Entries = entries.ToImmutableList(),
                XmlContent = doc.DocumentNode.OuterHtml
            };
        }

        public HtmlFile ReadLocalized(string language, HtmlDocument document)
        {
            var entries = Entries.ToList();
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var localizedNode = document.DocumentNode.SelectSingleNode(entry.Name);
                if (localizedNode == null)
                {
                    Console.Out.WriteLine("Unable to find {0} for language {1} in {2}", entry.Name, language, RelativePath);
                    continue;
                }

                entries[i] = entry with
                {
                    LocalizedValues =
                    entry.LocalizedValues.SetItem(language, new LocalizedValue(localizedNode.InnerHtml))
                };
            }

            return this with { Entries = entries.ToImmutableList() };
        }

        public HtmlFile MergeLocalized(HtmlFile localized)
        {
            if (localized.RelativePath != RelativePath)
            {
                throw new ArgumentException(string.Format("{0} should be {1}", localized.RelativePath, RelativePath));
            }
            var entries = Entries.ToList();
            var indexByXPath = Enumerable.Range(0, entries.Count).ToDictionary(i => entries[i].Name);
            var withoutXPathIndexes = entries.ToLookup(entry => RemoveIndexesFromXPath(entry.Name));
            foreach (var language in localized.Entries.SelectMany(entry => entry.LocalizedValues.Keys).Distinct())
            {
                var unmatched = new List<ResourceEntry>();
                foreach (var newEntry in localized.Entries)
                {
                    var translation = newEntry.GetTranslation(language);
                    if (translation == null)
                    {
                        continue;
                    }
                    if (!indexByXPath.TryGetValue(newEntry.Name, out int index) ||
                        entries[index].Invariant.Value != newEntry.Invariant.Value)
                    {
                        unmatched.Add(newEntry);
                        continue;
                    }

                    var entry = entries[index];
                    entry = entry with { LocalizedValues = entry.LocalizedValues.SetItem(language, translation) };
                    entries[index] = entry;
                }

                foreach (var unmatchedGroup in unmatched.GroupBy(newEntry => RemoveIndexesFromXPath(newEntry.Name)))
                {
                    var candidates = withoutXPathIndexes[RemoveIndexesFromXPath(unmatchedGroup.Key)].ToList();
                    var needFuzzyMatch = new List<ResourceEntry>();
                    var normalizedCandidates =
                        candidates.ToLookup(candidate => NormalizeWhitespace(candidate.Invariant.Value));
                    foreach (var identicalGroup in unmatchedGroup.GroupBy(newEntry =>
                                 NormalizeWhitespace(newEntry.Invariant.Value)))
                    {
                        var list = identicalGroup.ToList();
                        var exactMatches = normalizedCandidates[identicalGroup.Key].ToList();
                        if (exactMatches.Count == 0)
                        {
                            needFuzzyMatch.AddRange(list);
                            continue;
                        }
                        if (list.Count != exactMatches.Count)
                        {
                            Console.Error.WriteLine("Wrong number of entries for {0}:{1}", unmatchedGroup.Key, identicalGroup.Key);
                            continue;
                        }

                        for (int i = 0; i < list.Count; i++)
                        {
                            var index = indexByXPath[exactMatches[i].Name];
                            var entry = entries[index];
                            entry = entry with
                            {
                                LocalizedValues =
                                entry.LocalizedValues.SetItem(language, list[i].GetTranslation(language)!)
                            };
                            entries[index] = entry;
                        }
                    }
                    var bestMatches = new List<Tuple<int, ResourceEntry, ResourceEntry>>();
                    foreach (var newEntry in needFuzzyMatch)
                    {
                        var normalizedTarget = NormalizeWhitespace(newEntry.Invariant.Value);
                        var bestMatch = FindBestMatch(normalizedTarget, normalizedCandidates.Select(group=>group.Key));
                        if (bestMatch != null)
                        {
                            bestMatches.Add(Tuple.Create(bestMatch.Item1, newEntry, normalizedCandidates[bestMatch.Item2].First()));
                        }
                    }

                    foreach (var bestMatchTuple in bestMatches.OrderBy(tuple => tuple.Item1))
                    {
                        int index = indexByXPath[bestMatchTuple.Item3.Name];
                        var entry = entries[index];
                        if (entry.LocalizedValues.ContainsKey(language))
                        {
                            continue;
                        }
                        var newEntry = bestMatchTuple.Item2;
                        var newTranslation = newEntry.LocalizedValues[language];
                        LocalizationIssue? issue = null;
                        if (!Equals(NormalizeWhitespace(newEntry.Invariant.Value), NormalizeWhitespace(entry.Invariant.Value)))
                        {
                            issue = new EnglishTextChanged(newEntry.Invariant.Value, newTranslation.Value);
                        }
                        entry = entry with
                        {
                            LocalizedValues = entry.LocalizedValues.SetItem(language,
                                new LocalizedValue(newTranslation.Value, issue))
                        };

                        entries[index] = entry;
                    }
                }
            }
            return this with { Entries = entries.ToImmutableList() };
        }

        private static Levenshtein _levenshtein = new Levenshtein();
        private Tuple<int, string>? FindBestMatch(string target, IEnumerable<string> candidates)
        {
            int bestDistance = int.MaxValue;
            string? bestMatch = null;
            foreach (var candidate in candidates)
            {
                var distance = (int) _levenshtein.Distance(target, candidate, bestDistance);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestMatch = candidate;
                }
            }

            return bestMatch == null ? null : Tuple.Create(bestDistance, bestMatch);
        }

        public static HtmlFile? ReadFolder(string absolutePath, string relativePath)
        {
            var englishPath = Path.Combine(absolutePath, "en", "index.html");
            if (!File.Exists(englishPath))
            {
                return null;
            }

            var htmlFile = new HtmlFile(relativePath);
            htmlFile = htmlFile.ReadEnglish(englishPath);
            foreach (var subFolder in Directory.GetDirectories(absolutePath))
            {
                string language = Path.GetFileName(subFolder);
                if (language == "en")
                {
                    continue;
                }

                var localizedFile = Path.Combine(subFolder, "index.html");
                if (!File.Exists(localizedFile))
                {
                    continue;
                }

                var localizedDoc = new HtmlDocument();
                localizedDoc.Load(localizedFile);
                var baseFile = Path.Combine(subFolder, "invariant.html");
                if (File.Exists(baseFile))
                {
                    var baseHtmlFile = new HtmlFile(relativePath).ReadEnglish(baseFile);
                    var incompatibilities = baseHtmlFile.DescribeIncompatibilities(localizedDoc);
                    if (incompatibilities != null)
                    {
                        Console.Error.WriteLine("Incompatibilities between {0} and {1}: {2}", baseFile, localizedFile, incompatibilities);
                        continue;
                    }
                    baseHtmlFile = baseHtmlFile.ReadLocalized(language, localizedDoc);
                    htmlFile = htmlFile.MergeLocalized(baseHtmlFile);
                }
                else
                {
                    var incompatibilities = htmlFile.DescribeIncompatibilities(localizedDoc);
                    if (incompatibilities != null)
                    {
                        Console.Error.WriteLine("Incompatibilities between {0} and {1}: {2}", absolutePath, localizedFile, incompatibilities);
                        continue;
                    }
                    htmlFile = htmlFile.ReadLocalized(language, localizedDoc);
                }
            }

            return htmlFile;
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

        private static IEnumerable<HtmlNode> ListAllElements(HtmlNode node)
        {
            string strippedXPath = RemoveIndexesFromXPath(node.XPath)!;
            if (LocalizableXPaths.Contains(strippedXPath))
            {
                return new[] { node };
            }

            return node.ChildNodes.SelectMany(ListAllElements);
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

        public HtmlDocument ExportHtmlDocument(string? language)
        {
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(XmlContent);
            foreach (var entry in Entries)
            {
                var el = htmlDocument.DocumentNode.SelectSingleNode(entry.Name);
                if (el == null)
                {
                    Console.Out.WriteLine("Unable to find {0}", entry.Name);
                    continue;
                }

                string? innerHtml = null;
                if (language != null)
                {
                    var translation = entry.GetTranslation(language);
                    if (translation?.Issue == null)
                    {
                        innerHtml = translation?.Value;
                    }
                }

                innerHtml ??= entry.Invariant.Value;
                el.InnerHtml = innerHtml;
            }

            return htmlDocument;
        }

        public override string ExportFile(string? language, bool overrideAll)
        {
            using var stringWriter = new StringWriter();
            ExportHtmlDocument(language).Save(stringWriter);
            return stringWriter.ToString();
        }

        private static HtmlDocument ParseHtmlDocument(string path)
        {
            var htmlDocument = new HtmlDocument();
            htmlDocument.Load(path);
            return htmlDocument;
        }

        private bool ContainsLocalizableText(HtmlNode node)
        {
            return node.InnerText.Any(ch => !char.IsWhiteSpace(ch));
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

        public static readonly string FILE_TYPE = "html";
        public override string FileType
        {
            get { return FILE_TYPE; }
        }

        private string? DescribeIncompatibilities(HtmlDocument localizedDocument)
        {
            var invariantKeys = Entries.Select(entry => entry.Name).ToHashSet();
            var localizedNodes = ListLocalizableElements(localizedDocument.DocumentNode)
                .ToDictionary(node => node.XPath);
            var extraKeys = localizedNodes
                .Where(entry => !invariantKeys.Contains(entry.Key) && ContainsLocalizableText(entry.Value))
                .Select(entry => entry.Key).ToList();
            var missingKeys = invariantKeys.Except(localizedNodes.Keys).ToList();
            if (missingKeys.Count == 0)
            {
                if (extraKeys.Count == 0)
                {
                    return null;
                }

                if (extraKeys.Count == 1)
                {
                    return string.Format("Extra node: {0}", extraKeys.Single());
                }

                return string.Format("{0} extra nodes", extraKeys.Count);
            }

            if (extraKeys.Count == 0)
            {
                if (missingKeys.Count == 1)
                {
                    return string.Format("Missing node {0}", missingKeys.Single());
                }

                return string.Format("{0} missing nodes", missingKeys.Count);
            }

            return string.Format("{0} missing nodes and {1} extra nodes", missingKeys.Count,
                extraKeys.Count);
        }
    }
}
