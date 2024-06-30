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
using ResourcesOrganizer;
using ResourcesOrganizer.ResourcesModel;
using Program = ResourcesOrganizer.Program;

namespace Test
{
    [TestClass]
    public class ImportLocalizationCsvTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestImportLocalizationCsv()
        {
            string folder = SaveManifestResources(typeof(ImportLocalizationCsvTest));
            Environment.CurrentDirectory = folder;
            Assert.AreEqual(0, Program.DoAdd(new AddVerb() { DbFile = "resources.db", Path= ["."] }));
            Assert.AreEqual(0, Program.DoExportLocalizationCsv(new ExportLocalizationCsv()));
            var originalDb = ResourcesDatabase.ReadDatabase("resources.db");
            var roundTrip = Program.ImportLocalizationCsv(originalDb, "localization.csv", ["ja", "zh-CHS"]);
            Assert.IsNotNull(roundTrip);
            foreach (var file in originalDb.ResourcesFiles)
            {
                var roundTripFile = roundTrip.ResourcesFiles[file.Key];
                Assert.AreEqual(file.Value.Entries.Count, roundTripFile.Entries.Count);
                for (int i = 0; i < file.Value.Entries.Count; i++)
                {
                    var entry = file.Value.Entries[i];
                    var roundTripEntry = roundTripFile.Entries[i];
                    if (!Equals(entry, roundTripEntry))
                    {
                        Assert.AreEqual(entry, roundTripEntry);
                    }
                    Assert.AreEqual(entry, roundTripEntry);
                }
            }
        }
    }
}
