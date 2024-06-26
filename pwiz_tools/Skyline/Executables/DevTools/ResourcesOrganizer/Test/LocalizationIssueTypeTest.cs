using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NHibernate.Transaction;
using ResourcesOrganizer.ResourcesModel;

namespace Test
{
    [TestClass]
    public class LocalizationIssueTypeTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestEnglishTextChanged()
        {
            List<string> languages = ["ja", "zh-CHS"];
            string runDirectory = TestContext.TestRunDirectory!;
            SaveManifestResources(typeof(LocalizationIssueTypeTest), runDirectory);
            var oldResourcesFile = ResourcesFile.Read(Path.Combine(runDirectory, "v23.AboutDlg.resx"));
            var oldDatabase = ResourcesDatabase.Empty;
            oldDatabase = oldDatabase with { ResourcesFiles = oldDatabase.ResourcesFiles.SetItem("AboutDlg.resx", oldResourcesFile) };
            var newResourcesFile = ResourcesFile.Read(Path.Combine(runDirectory, "v24.AboutDlg.resx"));
            var newDatabase = ResourcesDatabase.Empty;
            newDatabase = newDatabase with { ResourcesFiles = newDatabase.ResourcesFiles.SetItem("AboutDlg.resx", newResourcesFile) };
            var withImportedTranslations = newDatabase.ImportTranslations(oldDatabase, languages);
            var resourceEntry = withImportedTranslations.ResourcesFiles.Values.Single().Entries
                .FirstOrDefault(entry => entry.Name == "textBox1.Text");
            Assert.IsNotNull(resourceEntry);
            var v23Entry = oldResourcesFile.Entries.FirstOrDefault(entry => entry.Name == resourceEntry.Name);
            Assert.IsNotNull(v23Entry);
            foreach (var language in languages)
            {
                var localizedValue = resourceEntry.GetTranslation(language);
                Assert.IsNotNull(localizedValue);
                Assert.AreEqual(LocalizationIssueType.EnglishTextChanged, localizedValue.IssueType);
                Assert.IsNotNull(localizedValue.ImportedValue);
                var currentLocalizedValue = localizedValue.IssueType!.GetLocalizedText(resourceEntry, localizedValue);
                var v23LocalizedValue = v23Entry.GetTranslation(language);
                Assert.IsNotNull(v23LocalizedValue);
                Assert.AreEqual(v23LocalizedValue.OriginalValue, currentLocalizedValue);
                Assert.AreEqual(localizedValue.OriginalInvariantValue, v23Entry.Invariant.Value);
            }

            var importedTranslationPath = Path.Combine(TestContext.TestRunDirectory!, "importTranslations.db");
            VerifyRoundTrip(withImportedTranslations, importedTranslationPath);
        }
    }
}
