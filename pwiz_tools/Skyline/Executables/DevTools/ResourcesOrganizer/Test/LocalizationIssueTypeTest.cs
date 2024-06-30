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
    public class LocalizationIssueTypeTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestEnglishTextChanged()
        {
            var languages = Languages;
            string resxFileName = "AboutDlg.resx";
            var runDirectory = SaveManifestResources(typeof(LocalizationIssueTypeTest));
            var oldResourcesFile = ResourcesFile.Read(Path.Combine(runDirectory, "v23." + resxFileName), resxFileName);
            var oldDatabase = ResourcesDatabase.Empty;
            oldDatabase = oldDatabase with { ResourcesFiles = oldDatabase.ResourcesFiles.SetItem(resxFileName, oldResourcesFile) };
            var newResourcesFile = ResourcesFile.Read(Path.Combine(runDirectory, "v24." + resxFileName), resxFileName);
            var newDatabase = ResourcesDatabase.Empty;
            newDatabase = newDatabase with { ResourcesFiles = newDatabase.ResourcesFiles.SetItem(resxFileName, newResourcesFile) };
            var withImportedTranslations = newDatabase.ImportLastVersion(oldDatabase, languages, out _, out _);
            var importedTranslationPath = Path.Combine(runDirectory, "importTranslations.db");
            VerifyRoundTrip(withImportedTranslations, importedTranslationPath);

            var copyrightEntry = withImportedTranslations.ResourcesFiles.Values.Single()
                .FindEntry("textBox1.Text");
            Assert.IsNotNull(copyrightEntry);
            var v23Entry = oldResourcesFile.FindEntry(copyrightEntry.Name);
            Assert.IsNotNull(v23Entry);
            foreach (var language in languages)
            {
                var localizedValue = copyrightEntry.GetTranslation(language);
                Assert.IsNotNull(localizedValue);
                var englishTextChanged = localizedValue.Issue as EnglishTextChanged;
                Assert.IsNotNull(englishTextChanged);
                Assert.AreEqual(copyrightEntry.Invariant.Value, localizedValue.Value);
                var v23LocalizedValue = v23Entry.GetTranslation(language);
                Assert.IsNotNull(v23LocalizedValue);
                Assert.AreEqual(v23LocalizedValue.Value, englishTextChanged.ReviewedLocalizedValue);
                Assert.AreEqual(englishTextChanged.ReviewedInvariantValue, v23Entry.Invariant.Value);
            }

            var exportedPath = Path.Combine(runDirectory, "exported.resx");
            
            ExportFile(withImportedTranslations.ResourcesFiles.Values.Single(), exportedPath);
            var roundTrip = ResourcesFile.Read(exportedPath, resxFileName);
            Assert.IsNotNull(roundTrip);
            var roundTripCopyrightEntry = roundTrip.FindEntry(copyrightEntry.Name);
            Assert.IsNotNull(roundTripCopyrightEntry);
            foreach (var language in languages)
            {
                var localizedValue = roundTripCopyrightEntry.GetTranslation(language);
                Assert.IsNotNull(localizedValue);
                var englishTextChanged = localizedValue.Issue as EnglishTextChanged;
                Assert.IsNotNull(englishTextChanged);
                var v23LocalizedValue = v23Entry.GetTranslation(language);
                Assert.IsNotNull(v23LocalizedValue);
                Assert.AreEqual(roundTripCopyrightEntry.Invariant.Value, localizedValue.Value);
                Assert.AreEqual(v23LocalizedValue.Value, englishTextChanged.ReviewedLocalizedValue);
                Assert.AreEqual(v23Entry.Invariant.Value, englishTextChanged.ReviewedInvariantValue);
            }

            var roundTripSoftwareVersion = roundTrip.FindEntry("textSoftwareVersion.Text");
            Assert.IsNotNull(roundTripSoftwareVersion);
            foreach (var language in languages)
            {
                var localizedValue = roundTripSoftwareVersion.GetTranslation(language);
                Assert.IsNotNull(localizedValue);
                Assert.AreEqual(LocalizationIssue.NewResource, localizedValue.Issue);
                Assert.AreEqual(roundTripSoftwareVersion.Invariant.Value, localizedValue.Value);
            }
        }
    }
}
