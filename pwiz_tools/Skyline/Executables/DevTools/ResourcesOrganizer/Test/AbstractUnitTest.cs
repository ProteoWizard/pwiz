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

using NHibernate.Criterion;
using ResourcesOrganizer;
using ResourcesOrganizer.ResourcesModel;

namespace Test
{
    public abstract class AbstractUnitTest
    {
        public TestContext TestContext { get; set; }

        protected string SaveManifestResources(Type type)
        {
            var assembly = type.Assembly;
            const string suffix = ".testfile";
            string prefix = type.FullName + ".";
            var destination = FindUniqueName(TestContext.TestRunDirectory!, type.Name);
            Directory.CreateDirectory(destination);
            foreach (var manifestResourceName in assembly.GetManifestResourceNames())
            {
                if (manifestResourceName.StartsWith(prefix) && manifestResourceName.EndsWith(suffix))
                {
                    var target = manifestResourceName.Substring(prefix.Length,
                        manifestResourceName.Length - prefix.Length - suffix.Length);
                    using var stream = assembly.GetManifestResourceStream(manifestResourceName);
                    using var dest = File.OpenWrite(Path.Combine(destination, target));
                    stream!.CopyTo(dest);
                }
            }

            return destination;
        }

        protected void VerifyRoundTrip(ResourcesDatabase database, string path)
        {
            database.Save(path);
            var roundTrip = ResourcesDatabase.ReadDatabase(path);
            CollectionAssert.AreEquivalent(database.ResourcesFiles.Keys.ToList(),
                roundTrip.ResourcesFiles.Keys.ToList());
            foreach (var resourcesFileEntry in database.ResourcesFiles)
            {
                var resourcesFile = resourcesFileEntry.Value;
                Assert.IsTrue(roundTrip.ResourcesFiles.TryGetValue(resourcesFileEntry.Key, out var roundTripFile));
                Assert.AreEqual(resourcesFile.Entries.Count, roundTripFile.Entries.Count);
                for (int i = 0; i < resourcesFile.Entries.Count; i++)
                {
                    var entry = resourcesFile.Entries[i];
                    var roundTripEntry = roundTripFile.Entries[i];
                    if (!Equals(entry, roundTripEntry))
                    {
                        Assert.AreEqual(entry, roundTripEntry);
                    }
                }
            }
        }

        public IList<string> Languages {get
        {
            return new[]
            {
                "ja", "zh-CHS"
            };
        }}

        protected void ExportFile(ResourcesFile resourcesFile, string path)
        {
            File.WriteAllText(path, TextUtil.SerializeDocument(resourcesFile.ExportResx(null, false)), TextUtil.Utf8Encoding);
            foreach (var language in Languages)
            {
                var localizedPath = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "." + language + ".resx");
                File.WriteAllText(localizedPath, TextUtil.SerializeDocument(resourcesFile.ExportResx(language, false)), TextUtil.Utf8Encoding);
            }
        }

        protected string FindUniqueName(string folder, string name)
        {
            var path = Path.Combine(folder, name);
            int index = 0;
            while (File.Exists(path) || Directory.Exists(path))
            {
                path = Path.Combine(path, name + ++index);
            }
            return path;
        }
    }
}
