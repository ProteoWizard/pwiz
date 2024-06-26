using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ResourcesOrganizer.ResourcesModel
{
    public record ResourcesFile
    {
        public static readonly XmlWriterSettings XmlWriterSettings = new XmlWriterSettings()
        {
            Indent = true,
            Encoding = new UTF8Encoding(false, true),
            
        };
        public static readonly XName XmlSpace = XName.Get("space", "http://www.w3.org/XML/1998/namespace");
        public ImmutableList<ResourceEntry> Entries { get; init; } = [];
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
                    Type = (string?)element.Attribute("type")
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
                    MimeType = mimeType,
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
                    if (comment != null)
                    {
                        using var stringReader = new StringReader(comment);
                        while (stringReader.ReadLine() is { } line)
                        {
                            if (line.StartsWith(LocalizationComments.NeedsReviewPrefix))
                            {
                                problem = line.Substring(LocalizationComments.NeedsReviewPrefix.Length);
                                break;
                            }
                        }
                    }
                    var entry = entries[entryIndex];
                    entries[entryIndex] = entry with
                    {
                        LocalizedValues = entry.LocalizedValues.SetItem(language,
                            new LocalizedValue
                            {
                                OriginalValue = element.Element("value")!.Value,
                                Problem = problem
                            })
                    };
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

        public void ExportResx(Stream stream, string? language, bool overrideAll, bool includeProblems)
        {
            var document = XDocument.Load(new StringReader(XmlContent));
            var newNodes = document.Root!.Nodes().Where(PreserveNode).ToList();
            foreach (var entryGroup in Entries.GroupBy(entry=>entry.Position).OrderByDescending(group=>group.Key))
            {
                foreach (var entry in entryGroup.Reverse())
                {
                    string? localizedText = null;
                    string? problem = null;
                    if (!string.IsNullOrEmpty(language))
                    {
                        if (entry.LocalizedValues.TryGetValue(language, out var localizedValue))
                        {
                            if (includeProblems)
                            {
                                problem = localizedValue.Problem;
                                if (problem == LocalizationComments.EnglishTextChanged)
                                {
                                    // If old translated value was the same as old English, default to new English
                                    if (localizedValue.ImportedValue == localizedValue.OriginalInvariantValue)
                                    {
                                        localizedText = entry.Invariant.Value;
                                        problem = null;
                                    }
                                    else
                                    {
                                        var problemLines = new List<string>
                                        {
                                            problem,
                                            "Old English:" + localizedValue.OriginalInvariantValue,
                                            "Current English:" + entry.Invariant.Value,
                                            "Old Localized:" + localizedValue.ImportedValue
                                        };
                                        problem = string.Join(Environment.NewLine, problemLines);
                                    }
                                }

                            }
                            if (problem == null || includeProblems)
                            {
                                localizedText ??= localizedValue.ImportedValue ?? localizedValue.OriginalValue;
                            }

                            if (localizedValue.OriginalValue == null)
                            {
                                if (entry.Invariant.CanIgnore)
                                {
                                    continue;
                                }

                                if (localizedValue.ImportedValue == null || localizedValue.ImportedValue == entry.Invariant.Value)
                                {
                                    if (!overrideAll)
                                    {
                                        continue;
                                    }

                                    if (entry.Invariant.Type != null || entry.MimeType != null)
                                    {
                                        continue;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (entry.Invariant.CanIgnore)
                            {
                                continue;
                            }
                            if (entry.Invariant.Type != null || entry.MimeType != null)
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
                    List<string> comments = [];
                    if (entry.Invariant.Comment != null)
                    {
                        comments.Add(entry.Invariant.Comment);
                    }

                    if (problem == LocalizationComments.MissingTranslation ||
                        problem == LocalizationComments.NewResource)
                    {
                        if (entry.Invariant.CanIgnore || entry.Invariant.Type != null || entry.MimeType != null)
                        {
                            problem = null;
                        }
                    }
                    if (problem != null)
                    {
                        comments.Add(LocalizationComments.NeedsReviewPrefix + problem);
                    }
                    if (comments.Any())
                    {
                        data.Add(new XElement("comment", string.Join(Environment.NewLine, comments)));
                    }

                    if (entry.Invariant.Type != null)
                    {
                        data.SetAttributeValue("type", entry.Invariant.Type);
                    }
                    if (entry.MimeType != null)
                    {
                        data.SetAttributeValue("mimetype", entry.MimeType);
                    }

                    newNodes.Insert(entryGroup.Key, data);
                    if (string.IsNullOrEmpty(localizedText) && !string.IsNullOrEmpty(entry.Invariant.Value))
                    {
                        newNodes.Insert(entryGroup.Key, new XComment(" ReSharper disable once OverriddenWithEmptyValue "));
                    }
                }
            }
            document.Root.ReplaceAll(newNodes.Cast<object>().ToArray());

            using var xmlWriter = XmlWriter.Create(stream, XmlWriterSettings);
            document.Save(xmlWriter);
        }
    }
}
