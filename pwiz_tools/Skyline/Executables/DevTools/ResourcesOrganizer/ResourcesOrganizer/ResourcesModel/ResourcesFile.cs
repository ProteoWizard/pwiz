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
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Xml.Linq;

namespace ResourcesOrganizer.ResourcesModel
{
    public record ResourcesFile(string RelativePath)
    {
        public static readonly XName XmlSpace = XName.Get("space", "http://www.w3.org/XML/1998/namespace");
        private ImmutableList<ResourceEntry> _entries = [];

        public ImmutableList<ResourceEntry> Entries
        {
            get
            {
                return _entries;
            }
            init
            {
                foreach (var entry in value)
                {
                    if (entry.Invariant.File != null && entry.Invariant.File != RelativePath)
                    {
                        string message = string.Format("File {0} in entry {1} should be {2}", entry.Invariant.File,
                            entry.Invariant.Name, RelativePath);
                        throw new ArgumentException(message);
                    }
                }
                _entries = value;
            }
        }

        public ResourceEntry? FindEntry(string name)
        {
            return Entries.FirstOrDefault(entry => entry.Name == name);
        }
        public string XmlContent { get; init; } = string.Empty;

        public static ResourcesFile Read(string absolutePath, string relativePath)
        {
            var entries = new List<ResourceEntry>();
            var entriesIndex = new Dictionary<string, int>();
            var otherNodes = new List<XNode>();
            var document = XDocument.Load(absolutePath);
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
                if (!key.IsLocalizableText)
                {
                    key = key with { File = relativePath };
                }
                if (entriesIndex.ContainsKey(key.Name))
                {
                    Console.Error.WriteLine("Duplicate name {0} in file {1}", key.Name, relativePath);
                    continue;
                }

                var entry = new ResourceEntry(key.Name, key)
                {
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

            var baseName = Path.GetFileNameWithoutExtension(absolutePath);
            var baseExtension = Path.GetExtension(absolutePath);
            foreach (var file in Directory.GetFiles(Path.GetDirectoryName(absolutePath)!))
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
                    string? comment = element.Element("comment")?.Value;
                    var entry = entries[entryIndex];
                    entry = entry with
                    {
                        LocalizedValues = entry.LocalizedValues.SetItem(language,
                            new LocalizedValue(element.Element("value")!.Value,
                                LocalizationIssue.ParseComment(comment)))
                    };
                    entries[entryIndex] = entry;
                }
            }

            return new ResourcesFile(relativePath)
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
                    string? localizedText = entry.GetLocalizedText(language);
                    if (overrideAll && localizedText == null)
                    {
                        localizedText = entry.Invariant.Value;
                    }
                    if (localizedText == null)
                    {
                        continue;
                    }

                    string? comment = entry.GetComment(language);
                    var data = new XElement("data");
                    data.SetAttributeValue("name", entry.Name);
                    if (entry.XmlSpace != null)
                    {
                        data.SetAttributeValue(XmlSpace, entry.XmlSpace);
                    }
                    data.Add(new XElement("value", localizedText));

                    if (comment != null)
                    {
                        data.Add(new XElement("comment", comment));
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
            document.Root.ReplaceNodes(newNodes.Cast<object>().ToArray());
            return document;
        }

        public ResourcesFile ImportLocalizationRecords(string language, ILookup<string, LocalizationCsvRecord> records, out int matchCount, out int changeCount)
        {
            matchCount = 0;
            changeCount = 0;
            var entries = Entries.ToList();
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                foreach (var record in records[entry.Invariant.Name!])
                {
                    if (!string.IsNullOrEmpty(record.File) && record.File != RelativePath)
                    {
                        continue;
                    }

                    if (record.English != entry.Invariant.Value)
                    {
                        continue;
                    }


                    matchCount++;

                    LocalizedValue localizedValue = new LocalizedValue(string.IsNullOrEmpty(record.Translation)
                        ? entry.Invariant.Value
                        : record.Translation, LocalizationIssue.FromCsvRecord(record));
                    if (string.IsNullOrEmpty(localizedValue.Value))
                    {
                        localizedValue = localizedValue with { Value = entry.Invariant.Value };
                    }
                    entry = entry with { LocalizedValues = entry.LocalizedValues.SetItem(language, localizedValue) };

                    if (!Equals(entries[i].GetTranslation(language), entry.GetTranslation(language)))
                    {
                        changeCount++;
                    }

                    entries[i] = entry;
                }
            }

            return this with { Entries = entries.ToImmutableList() };
        }
    }
}
