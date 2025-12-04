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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using HtmlAgilityPack;
using Ionic.Zip;

namespace TutorialLocalization
{
    public class TutorialsLocalizer
    {
        private Dictionary<string, List<LocalizationRecord>> _localizationRecords;
        private Dictionary<string, List<LocalizationRecord>> _localizationRecordOverrides;
        public TutorialsLocalizer(ZipFile zipFile)
        {
            ZipFile = zipFile;
            _localizationRecords = new Dictionary<string, List<LocalizationRecord>>();
            _localizationRecordOverrides = new Dictionary<string, List<LocalizationRecord>>();
        }

        public ZipFile ZipFile { get; }

        public void AddTutorialFolder(string folderPath, string relativePath)
        {
            if (relativePath == "shared")
            {
                AddFilesRecursive(folderPath, relativePath);
                return;
            }
            var englishPath = Path.Combine(folderPath, "en");
            if (!Directory.Exists(englishPath))
            {
                return;
            }

            var englishDoc = ReadHtmlDocument(Path.Combine(englishPath, "index.html"));
            if (englishDoc == null)
            {
                return;
            }
            AddFilesInFolder(englishPath, Path.Combine(relativePath, "en"));
            foreach (var folder in Directory.GetDirectories(folderPath))
            {
                var folderName = Path.GetFileName(folder);
                if (folderName == "en")
                {
                    continue;
                }

                var tutorialLocalizer = new TutorialLocalizer(this, folderPath, relativePath, folderName);
                tutorialLocalizer.Localize();
            }
        }

        private void AddFilesRecursive(string folderPath, string relativePath)
        {
            AddFilesInFolder(folderPath, relativePath);
            foreach (var subFolder in Directory.GetDirectories(folderPath))
            {
                AddFilesInFolder(subFolder, Path.Combine(relativePath, Path.GetFileName(subFolder)));
            }
        }

        public void SaveLocalizationCsvFiles()
        {
            foreach (var entry in _localizationRecords)
            {
                var csvText = LocalizationRecordsToString(entry.Value);
                ZipFile.AddEntry("localization_" + entry.Key + ".csv", ToUtf8Bytes(csvText));
            }
        }

        public void AddFilesInFolder(string folderPath, string relativePath)
        {
            foreach (var file in Directory.GetFiles(folderPath))
            {
                var fileName = Path.GetFileName(file);
                var entryName = Path.Combine(relativePath, fileName);
                if (ZipFile.ContainsEntry(entryName))
                {
                    continue;
                }
                ZipFile.AddFile(file, relativePath);
            }
        }

        public HtmlDocument ReadHtmlDocument(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var htmlDocument = new HtmlDocument();
            htmlDocument.Load(path, Encoding.UTF8);
            return htmlDocument;
        }

        public void AddHtmlDocument(HtmlDocument htmlDocument, string relativePath)
        {
            ZipFile.AddEntry(relativePath, ToUtf8Bytes(GetHtml(htmlDocument)));
        }

        public void AddFileBytes(byte[] bytes, string relativePath)
        {
            ZipFile.AddEntry(relativePath, bytes);
        }

        /// <summary>
        /// Gets the HTML for the document. In order to be consistent with the files that
        /// are already in the repository, empty tags in the &lt;head> end in ">" but
        /// empty tags in the &lt;body> end in "/>".
        /// </summary>
        public static string GetHtml(HtmlDocument document)
        {
            var stringWriter = new StringWriter();
            document.Save(stringWriter);
            var document2 = new HtmlDocument();
            document2.LoadHtml(stringWriter.ToString());
            var parts = new List<string> { "<html>", string.Empty };
            // Empty nodes (e.g. "link") in the head should end in ">"
            document2.OptionWriteEmptyNodes = false;
            parts.Add(document2.DocumentNode.SelectSingleNode("//head").OuterHtml);
            parts.Add(string.Empty);
            // Empty nodes (e.g. "img") in the body should end in "/>"
            document2.OptionWriteEmptyNodes = true;
            parts.Add(document2.DocumentNode.SelectSingleNode("//body").OuterHtml);
            parts.Add(string.Empty);
            parts.Add("</html>");
            return string.Join(Environment.NewLine, parts);
        }

        public void AddLocalizationRecord(string language, LocalizationRecord record)
        {
            if (!_localizationRecords.TryGetValue(language, out var list))
            {
                list = new List<LocalizationRecord>();
                _localizationRecords.Add(language, list);
            }
            list.Add(record);
        }

        public string LocalizationRecordsToString(IEnumerable<LocalizationRecord> records)
        {
            var stringWriter = new StringWriter();
            var csvWriter = new CsvWriter(stringWriter, CultureInfo.InvariantCulture);
            csvWriter.WriteRecords(records);
            return stringWriter.ToString();
        }

        public byte[] ToUtf8Bytes(string str)
        {
            var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream, new UTF8Encoding(false)))
            {
                writer.Write(str);
            }
            return memoryStream.ToArray();
        }

        public void ReadLocalizationCsvFiles(string folderPath)
        {
            foreach (var file in Directory.GetFiles(folderPath))
            {
                string prefix = "localization_";
                string suffix = ".csv";
                string fileName = Path.GetFileName(file);
                if (fileName.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase) &&
                    fileName.EndsWith(suffix, StringComparison.InvariantCultureIgnoreCase))
                {
                    var language = fileName.Substring(prefix.Length, fileName.Length - prefix.Length - suffix.Length);
                    using var fileReader = File.OpenText(file);
                    using var csvReader = new CsvReader(fileReader, CultureInfo.InvariantCulture);
                    if (!_localizationRecordOverrides.TryGetValue(language, out var list))
                    {
                        list = new List<LocalizationRecord>();
                        _localizationRecordOverrides[language] = list;
                    }
                    list.AddRange(csvReader.GetRecords<LocalizationRecord>());
                }
            }
        }

        public IEnumerable<LocalizationRecord> GetLocalizationRecordOverrides(string language, string tutorial)
        {
            if (!_localizationRecordOverrides.TryGetValue(language, out var list))
            {
                return Array.Empty<LocalizationRecord>();
            }

            return list.Where(r => r.TutorialName == tutorial);
        }
    }
}
