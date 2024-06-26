using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Xml.Linq;

namespace ResourcesOrganizer.ResourcesModel
{
    public record ResourcesFile
    {
        public static readonly XName XmlSpace = XName.Get("space", "http://www.w3.org/XML/1998/namespace");
        public ImmutableList<ResourceEntry> Entries { get; init; } = [];

        public ResourceEntry? FindEntry(string name)
        {
            return Entries.FirstOrDefault(entry => entry.Name == name);
        }
        public string XmlContent { get; init; } = string.Empty;
        public static ResourcesFile Read(string filePath)
        {
            var entries = new List<ResourceEntry>();
            var entriesIndex = new Dictionary<string, int>();
            var otherNodes = new List<XNode>();
            var document = XDocument.Load(filePath);
            foreach (var node in document.Root!.Nodes())
            {
                if (!(node is XElement element) || element.Name != "data")
                {
                    if (PreserveNode(node))
                    {
                        otherNodes.Add(node);
                    }
                    continue;
                }

                string? mimeType = (string?)element.Attribute("mimetype");
                string? xmlSpace = (string?)element.Attribute(XmlSpace);

                var key = new InvariantResourceKey
                {
                    Comment = element.Element("comment")?.Value,
                    Name = (string)element.Attribute("name")!,
                    Value = element.Element("value")!.Value,
                    Type = (string?)element.Attribute("type"),
                    MimeType = mimeType
                };
                if (entriesIndex.ContainsKey(key.Name))
                {
//                    Console.Error.WriteLine("Duplicate name {0} in file {1}", key.Name, filePath);
                    continue;
                }

                var entry = new ResourceEntry
                {
                    Name = key.Name,
                    Invariant = key,
                    XmlSpace = xmlSpace,
                    Position = otherNodes.Count
                };
                entriesIndex.Add(entry.Name, entries.Count);
                entries.Add(entry);
            }
            document.Root.RemoveAll();
            document.Root.Add(otherNodes.Cast<object>().ToArray());
            var stringWriter = new StringWriter();
            document.Save(stringWriter);
            var xmlContent = stringWriter.ToString();

            var baseName = Path.GetFileNameWithoutExtension(filePath);
            var baseExtension = Path.GetExtension(filePath);
            foreach (var file in Directory.GetFiles(Path.GetDirectoryName(filePath)!))
            {
                if (!baseExtension.Equals(Path.GetExtension(file), StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }
                var fileWithoutExtension = Path.GetFileNameWithoutExtension(file);
                var languageExtension = Path.GetExtension(fileWithoutExtension);
                if (string.IsNullOrEmpty(languageExtension))
                {
                    continue;
                }

                var language = languageExtension.Substring(1);
                if (!baseName.Equals(Path.GetFileNameWithoutExtension(fileWithoutExtension),
                        StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                foreach (var element in XDocument.Load(file).Root!.Elements("data"))
                {
                    var name = (string)element.Attribute("name")!;
                    
                    if (!entriesIndex.TryGetValue(name, out int entryIndex))
                    {
                        continue;
                    }
                    string problem = null;
                    string? comment = element.Element("comment")?.Value;
                    var entry = entries[entryIndex];
                    entry = entry with
                    {
                        LocalizedValues = entry.LocalizedValues.SetItem(language,
                            new LocalizedValue
                            {
                                OriginalValue = element.Element("value")!.Value,
                            })
                    };
                    entry = LocalizationIssueType.ParseComment(entry, language, comment);
                    entries[entryIndex] = entry;
                }
            }

            return new ResourcesFile
            {
                Entries = ImmutableList.CreateRange(entries),
                XmlContent = xmlContent
            };
        }

        public static bool PreserveNode(XNode node)
        {
            if (node is XComment)
            {
                return true;
            }

            return node is XElement;
        }

        [Pure]
        public ResourcesFile Add(ResourcesFile resourcesFile)
        {
            var entries = Entries.ToList();
            var entriesIndex = entries.Select(Tuple.Create<ResourceEntry, int>)
                .ToDictionary(tuple => tuple.Item1.Name, tuple => tuple.Item2);
            foreach (var newEntry in resourcesFile.Entries)
            {
                if (!entriesIndex.TryGetValue(newEntry.Name, out var index))
                {
                    entriesIndex.Add(newEntry.Name, entries.Count);
                    entries.Add(newEntry);
                    continue;
                }
                var existing = entries[index];
                if (!Equals(existing.Invariant, newEntry.Invariant))
                {
                    continue;
                }
                foreach (var localizedValue in newEntry.LocalizedValues)
                {
                    if (!existing.LocalizedValues.ContainsKey(localizedValue.Key))
                    {
                        existing = existing with
                        {
                            LocalizedValues = existing.LocalizedValues.Add(localizedValue.Key, localizedValue.Value)
                        };
                    }
                }

                entries[index] = existing;
            }

            return this with { Entries = ImmutableList.CreateRange(entries) };
        }

        public static bool IsInvariantResourceFile(string path)
        {
            var extension = Path.GetExtension(path);
            if (!".resx".Equals(extension, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var baseName = Path.GetFileNameWithoutExtension(path);
            var baseExtension = Path.GetExtension(baseName);
            if (string.IsNullOrEmpty(baseExtension))
            {
                return true;
            }

            if (baseExtension.Length <= 3 || baseExtension[3] == '-')
            {
                return false;
            }

            return true;
        }

        public XDocument ExportResx(string? language, bool overrideAll)
        {
            var document = XDocument.Load(new StringReader(XmlContent));
            var newNodes = document.Root!.Nodes().Where(PreserveNode).ToList();
            foreach (var entryGroup in Entries.GroupBy(entry=>entry.Position).OrderByDescending(group=>group.Key))
            {
                foreach (var entry in entryGroup.Reverse())
                {
                    List<string> comments = [];
                    if (entry.Invariant.Comment != null)
                    {
                        comments.Add(entry.Invariant.Comment);
                    }

                    string? localizedText = null;
                    if (!string.IsNullOrEmpty(language))
                    {
                        if (entry.LocalizedValues.TryGetValue(language, out var localizedValue))
                        {
                            var issueType = localizedValue.IssueType;
                            if (entry.Invariant.IsLocalizableText || (issueType != LocalizationIssueType.MissingTranslation && issueType != LocalizationIssueType.NewResource))
                            {
                                localizedText = localizedValue.IssueType?.GetLocalizedText(entry, localizedValue) ?? localizedValue.ImportedValue ?? localizedValue.OriginalValue;
                                var issueComment = localizedValue.IssueType?.FormatIssueAsComment(entry, localizedValue);
                                if (issueComment != null)
                                {
                                    comments.Add(issueComment);
                                }
                            }

                            if (localizedValue.OriginalValue == null)
                            {
                                if (!entry.Invariant.IsLocalizableText)
                                {
                                    continue;
                                }

                                if (localizedValue.IssueType == null)
                                {
                                    if (localizedValue.ImportedValue == null || localizedValue.ImportedValue == entry.Invariant.Value)
                                    {
                                        if (!overrideAll)
                                        {
                                            continue;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (!entry.Invariant.IsLocalizableText)
                            {
                                continue;
                            }
                        }
                    }
                    localizedText ??= entry.Invariant.Value;
                    var data = new XElement("data");
                    data.SetAttributeValue("name", entry.Name);
                    if (entry.XmlSpace != null)
                    {
                        data.SetAttributeValue(XmlSpace, entry.XmlSpace);
                    }
                    data.Add(new XElement("value", localizedText));

                    if (comments.Any())
                    {
                        data.Add(new XElement("comment", string.Join(TextUtil.NewLine, comments)));
                    }

                    if (entry.Invariant.Type != null)
                    {
                        data.SetAttributeValue("type", entry.Invariant.Type);
                    }
                    if (entry.Invariant.MimeType != null)
                    {
                        data.SetAttributeValue("mimetype", entry.Invariant.MimeType);
                    }

                    newNodes.Insert(entryGroup.Key, data);
                    if (string.IsNullOrEmpty(localizedText) && !string.IsNullOrEmpty(entry.Invariant.Value))
                    {
                        newNodes.Insert(entryGroup.Key, new XComment(" ReSharper disable once OverriddenWithEmptyValue "));
                    }
                }
            }
            document.Root.ReplaceAll(newNodes.Cast<object>().ToArray());
            return document;
        }
    }
}
