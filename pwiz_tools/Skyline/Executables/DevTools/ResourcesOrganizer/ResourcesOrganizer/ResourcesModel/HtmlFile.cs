using System.Collections.Immutable;
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

        public static HtmlFile Read(string absolutePath, string relativePath, IDictionary<string, string> localizedFolders)
        {
            var entries = new List<ResourceEntry>();
            var doc = new HtmlDocument();
            doc.Load(absolutePath);
            foreach (var xPath in LocalizableXPaths)
            {
                var nodes = doc.DocumentNode.SelectNodes(xPath);
                if (nodes == null)
                {
                    continue;
                }
                foreach (var el in nodes)
                {
                    var key = new InvariantResourceKey()
                    {
                        File = relativePath,
                        Name = el.XPath,
                        Value = el.InnerHtml
                    };
                    var resourceEntry = new ResourceEntry(el.XPath, key);
                    entries.Add(resourceEntry);
                }
            }

            string fileName = Path.GetFileName(absolutePath);
            foreach (var localizedFolder in localizedFolders)
            {
                var localizedPath = Path.Combine(localizedFolder.Value, Path.GetFileName(fileName));
                if (!File.Exists(localizedPath))
                {
                    continue;
                }

                var localizedDoc = new HtmlDocument();
                localizedDoc.Load(localizedPath);
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    var el = localizedDoc.DocumentNode.SelectSingleNode(entry.Name);
                    if (el == null)
                    {
                        Console.Out.WriteLine("Unable to find {0} in {1}", entry.Name, localizedPath);
                        continue;
                    }

                    entry = entry with
                    {
                        LocalizedValues =
                        entry.LocalizedValues.SetItem(localizedFolder.Key, new LocalizedValue(el.InnerHtml))
                    };
                    entries[i] = entry;
                }
            }

            foreach (var entry in entries)
            {
                doc.DocumentNode.SelectSingleNode(entry.Name).InnerHtml = string.Empty;
            }

            return new HtmlFile(relativePath)
            {
                Entries = entries.ToImmutableList(),
                XmlContent = doc.DocumentNode.OuterHtml
            };
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
                    innerHtml = entry.GetLocalizedText(language);
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
    }
}
