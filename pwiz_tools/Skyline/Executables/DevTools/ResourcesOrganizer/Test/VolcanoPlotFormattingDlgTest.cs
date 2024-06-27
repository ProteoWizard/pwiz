using ResourcesOrganizer.ResourcesModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    [TestClass]
    public class VolcanoPlotFormattingDlgTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestVolcanoPlotFormattingDlg()
        {
            string runDirectory = TestContext.TestRunDirectory!;
            SaveManifestResources(typeof(VolcanoPlotFormattingDlgTest), runDirectory);
            string filename = "VolcanoPlotFormattingDlg.resx";
            var oldResourcesFile = ResourcesFile.Read(Path.Combine(runDirectory, "v23." + filename), filename);
            var oldDatabase = ResourcesDatabase.Empty;
            oldDatabase = oldDatabase with { ResourcesFiles = oldDatabase.ResourcesFiles.SetItem(filename, oldResourcesFile) };
            var newResourcesFile = ResourcesFile.Read(Path.Combine(runDirectory, "v24." + filename), filename);
            var newDatabase = ResourcesDatabase.Empty;
            newDatabase = newDatabase with { ResourcesFiles = newDatabase.ResourcesFiles.SetItem(filename, newResourcesFile) };
            var withImportedTranslations = newDatabase.ImportTranslations(oldDatabase, Languages, out _, out _);

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
