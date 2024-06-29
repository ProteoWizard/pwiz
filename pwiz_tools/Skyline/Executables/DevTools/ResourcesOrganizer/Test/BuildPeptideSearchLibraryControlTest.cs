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
    public class BuildPeptideSearchLibraryControlTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestBuildPeptideSearchLibraryControl()
        {
            const string fileName = "BuildPeptideSearchLibraryControl.resx";
            string runDirectory = SaveManifestResources(typeof(BuildPeptideSearchLibraryControlTest));
            var oldResourcesFile =
                ResourcesFile.Read(Path.Combine(runDirectory, "v23." + fileName), fileName);
            var oldDatabase =
                ResourcesDatabase.Empty.AddFile(oldResourcesFile);
            var newResourcesFile = ResourcesFile.Read(Path.Combine(runDirectory, "v24." + fileName), fileName);
            var newDatabase = ResourcesDatabase.Empty.AddFile(newResourcesFile);
            var withImportedTranslations = newDatabase.ImportLastVersion(oldDatabase, Languages, out _, out _);
            var fileWithImported = withImportedTranslations.ResourcesFiles.Values.Single();
            var entry = fileWithImported.FindEntry("btnAddFile.Location");
            Assert.IsNotNull(entry);
            var localizedValue = entry.GetTranslation("ja");
            Assert.IsNotNull(localizedValue);
            Assert.IsNotNull(localizedValue.Issue);
            var exportedDoc = fileWithImported.ExportResx("ja", true);
            var dataItem = exportedDoc.Root!.Elements("data").Single(el => el.Attribute("name")?.Value == entry.Name);
            var comment = dataItem.Elements("comment").SingleOrDefault();
            Assert.IsNotNull(comment);
        }
    }
}
