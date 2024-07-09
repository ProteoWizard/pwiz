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
using ResourcesOrganizer.ResourcesModel;

namespace Test
{
    [TestClass]
    public class VolcanoPlotFormattingDlgTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestVolcanoPlotFormattingDlg()
        {
            string runDirectory = SaveManifestResources(typeof(VolcanoPlotFormattingDlgTest));
            string filename = "VolcanoPlotFormattingDlg.resx";
            var oldResourcesFile = ResourcesFile.Read(Path.Combine(runDirectory, "v23." + filename), filename);
            var oldDatabase = ResourcesDatabase.Empty;
            oldDatabase = oldDatabase with { ResourcesFiles = oldDatabase.ResourcesFiles.SetItem(filename, oldResourcesFile) };
            var newResourcesFile = ResourcesFile.Read(Path.Combine(runDirectory, "v24." + filename), filename);
            var newDatabase = ResourcesDatabase.Empty;
            newDatabase = newDatabase with { ResourcesFiles = newDatabase.ResourcesFiles.SetItem(filename, newResourcesFile) };
            var withImportedTranslations = newDatabase.ImportLastVersion(oldDatabase, Languages, out _, out _);

            var entryName = "button1.Location";
            var importedFile = withImportedTranslations.ResourcesFiles.Values.Single();
            var importedEntry = importedFile.FindEntry(entryName);
            Assert.IsNotNull(importedEntry);
            var newEntry = newResourcesFile.FindEntry(entryName);
            Assert.IsNotNull(newEntry);
            foreach (var language in Languages)
            {
                Assert.IsNotNull(newEntry.GetLocalizedText(language));
                var localizedText = importedEntry.GetLocalizedText(language);
                Assert.IsNotNull(localizedText);
                Assert.AreEqual(localizedText, newEntry.Invariant.Value);
            }

            var exportedPath = Path.Combine(runDirectory, "exported.resx");
            ExportFile(importedFile, exportedPath);
            var roundTrip = ResourcesFile.Read(exportedPath, importedFile.RelativePath);
            var roundTripEntry = roundTrip.FindEntry(entryName);
            Assert.IsNotNull(roundTripEntry);
        }
    }
}
