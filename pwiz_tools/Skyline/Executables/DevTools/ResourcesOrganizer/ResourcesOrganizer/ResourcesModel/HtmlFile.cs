using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.RegularExpressions;
using F23.StringSimilarity;
using HtmlAgilityPack;
using NHibernate.Transform;

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

                foreach (var unmatchedGroup in unmatched.GroupBy(newEntry=>RemoveIndexesFromXPath(newEntry.Name)))
                {
                    var candidates = withoutXPathIndexes[RemoveIndexesFromXPath(unmatchedGroup.Key)].ToList();
                    var bestMatches = new List<Tuple<double, ResourceEntry, ResourceEntry>>();
                    foreach (var newEntry in unmatchedGroup)
                    {
                        var candidatesWithDistance = candidates.Select(candidate =>
                            Tuple.Create(Distance(newEntry.Invariant.Value, candidate.Invariant.Value), newEntry, candidate))
                            .OrderBy(tuple => tuple.Item1);
                        var bestMatch = candidatesWithDistance.FirstOrDefault();
                        if (bestMatch != null)
                        {
                            bestMatches.Add(bestMatch);
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
                        LocalizationIssue issue = null;
                        if (!Equals(newEntry.Invariant.Value, entry.Invariant.Value))
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

        private static double Distance(string target, string candidate)
        {
            return new Levenshtein().Distance(target, candidate);
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

                List<ResourceEntry> localizedEntries;
                var localizedDoc = new HtmlDocument();
                localizedDoc.Load(localizedFile);
                var baseFile = Path.Combine(subFolder, "index.source.html");
                if (File.Exists(baseFile))
                {
                    var baseHtmlFile = new HtmlFile(relativePath).ReadEnglish(baseFile);
                    baseHtmlFile = baseHtmlFile.ReadLocalized(language, localizedDoc);
                    htmlFile = htmlFile.MergeLocalized(baseHtmlFile);
                }
                else
                {
                    htmlFile = htmlFile.ReadLocalized(language, localizedDoc);
                }
            }

            return htmlFile;
        }

        private static void AddLocalizedEntries(List<ResourceEntry> entries,
            string language, HtmlDocument localizedDoc)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var node = localizedDoc.DocumentNode.SelectSingleNode(entry.Name);
                if (node == null)
                {
                    Console.Out.WriteLine("Unable to find {0} in {1}", entry.Name, language);
                    continue;
                }
                entries[i] = entry with
                {
                    LocalizedValues =
                    entry.LocalizedValues.SetItem(language, new LocalizedValue(node.InnerHtml))
                };
            }
        }

        private static void MergeLocalized(List<ResourceEntry> invariants, IEnumerable<ResourceEntry> newEntries)
        {
            var list = invariants.ToList();
            var exactIndex = Enumerable.Range(0, list.Count).ToDictionary(i => list[i].Invariant with { File = null });
            var withoutXPathIndexes = Enumerable.Range(0, list.Count).ToDictionary(i =>
                list[i].Invariant with { File = null, Name = RemoveIndexesFromXPath(list[i].Invariant.Name) });
            var withoutText = Enumerable.Range(0, list.Count)
                .ToDictionary(i => list[i].Invariant with { Value = string.Empty });
            foreach (var newEntry in newEntries)
            {
                var key = newEntry.Invariant with { File = null };
                foreach (var localizedEntry in newEntry.LocalizedValues)
                {
                    LocalizationIssue? localizationIssue = null;
                    if (!exactIndex.TryGetValue(key, out int index))
                    {
                        if (!withoutXPathIndexes.TryGetValue(key with { Name = RemoveIndexesFromXPath(key.Name) }, out index))
                        {
                            if (!withoutText.TryGetValue(key with { Value = string.Empty }, out index))
                            {
                                continue;
                            }

                            localizationIssue =
                                new EnglishTextChanged(newEntry.Invariant.Value, localizedEntry.Value.Value);
                        }
                    }

                    var matchingEntry = list[index];
                    foreach (var localizedValue in newEntry.LocalizedValues)
                    {
                        matchingEntry = matchingEntry with
                        {
                            LocalizedValues =
                            matchingEntry.LocalizedValues.SetItem(localizedValue.Key, localizedValue.Value with { Issue = localizationIssue})
                        };
                    }
                    list[index] = matchingEntry;
                }
            }
        }

        private IEnumerable<ResourceEntry> ReadInvariantEntries(HtmlDocument doc, string relativePath)
        {
            foreach (var el in ListLocalizableElements(doc.DocumentNode))
            {
                var key = new InvariantResourceKey()
                {
                    File = relativePath,
                    Name = el.XPath,
                    Value = el.InnerHtml
                };
                var resourceEntry = new ResourceEntry(el.XPath, key);
                yield return resourceEntry;
            }
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

        private static readonly Regex RegexXpathIndex =
            new Regex("\\[\\d+\\]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static string? RemoveIndexesFromXPath(string? xPath)
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

        public override LocalizableFile ImportLocalizationRecords(string language, ILookup<string, LocalizationCsvRecord> records, out int matchCount, out int changeCount)
        {
            throw new NotImplementedException();
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
    }
}
