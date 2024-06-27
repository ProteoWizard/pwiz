using ResourcesOrganizer;
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
            string runDirectory = TestContext.TestRunDirectory!;
            SaveManifestResources(typeof(LocalizationIssueTypeTest), runDirectory);
            var oldResourcesFile = ResourcesFile.Read(Path.Combine(runDirectory, "v23.AboutDlg.resx"));
            var oldDatabase = ResourcesDatabase.Empty;
            oldDatabase = oldDatabase with { ResourcesFiles = oldDatabase.ResourcesFiles.SetItem("AboutDlg.resx", oldResourcesFile) };
            var newResourcesFile = ResourcesFile.Read(Path.Combine(runDirectory, "v24.AboutDlg.resx"));
            var newDatabase = ResourcesDatabase.Empty;
            newDatabase = newDatabase with { ResourcesFiles = newDatabase.ResourcesFiles.SetItem("AboutDlg.resx", newResourcesFile) };
            var withImportedTranslations = newDatabase.ImportTranslations(oldDatabase, languages, out _, out _);
            var importedTranslationPath = Path.Combine(TestContext.TestRunDirectory!, "importTranslations.db");
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
                Assert.AreEqual(LocalizationIssueType.EnglishTextChanged, localizedValue.IssueType);
                Assert.IsNotNull(localizedValue.ReviewedValue);
                var currentLocalizedValue = localizedValue.IssueType!.GetLocalizedText(copyrightEntry, localizedValue);
                var v23LocalizedValue = v23Entry.GetTranslation(language);
                Assert.IsNotNull(v23LocalizedValue);
                Assert.AreEqual(v23LocalizedValue.OriginalValue, currentLocalizedValue);
                Assert.AreEqual(localizedValue.ReviewedInvariantValue, v23Entry.Invariant.Value);
            }

            var exportedPath = Path.Combine(runDirectory, "exported.resx");
            
            ExportFile(withImportedTranslations.ResourcesFiles.Values.Single(), exportedPath);
            var roundTrip = ResourcesFile.Read(exportedPath);
            Assert.IsNotNull(roundTrip);
            var roundTripCopyrightEntry = roundTrip.FindEntry(copyrightEntry.Name);
            Assert.IsNotNull(roundTripCopyrightEntry);
            foreach (var language in languages)
            {
                var localizedValue = roundTripCopyrightEntry.GetTranslation(language);
                Assert.IsNotNull(localizedValue);
                Assert.AreEqual(LocalizationIssueType.EnglishTextChanged, localizedValue.IssueType);
                var v23LocalizedValue = v23Entry.GetTranslation(language);
                Assert.IsNotNull(v23LocalizedValue);
                Assert.IsNull(localizedValue.ReviewedValue);
                Assert.AreEqual(v23LocalizedValue.OriginalValue, localizedValue.OriginalValue);
                Assert.AreEqual(localizedValue.ReviewedInvariantValue, v23Entry.Invariant.Value);
            }

            var roundTripSoftwareVersion = roundTrip.FindEntry("textSoftwareVersion.Text");
            Assert.IsNotNull(roundTripSoftwareVersion);
            foreach (var language in languages)
            {
                var localizedValue = roundTripSoftwareVersion.GetTranslation(language);
                Assert.IsNotNull(localizedValue);
                Assert.AreEqual(LocalizationIssueType.NewResource, localizedValue.IssueType);
                Assert.AreEqual(roundTripSoftwareVersion.Invariant.Value, localizedValue.CurrentValue);
            }
        }
    }
}
