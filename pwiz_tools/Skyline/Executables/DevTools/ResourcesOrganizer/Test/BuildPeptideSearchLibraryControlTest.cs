using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            string runDirectory = TestContext.TestRunDirectory!;
            SaveManifestResources(typeof(BuildPeptideSearchLibraryControlTest), runDirectory);
            var oldResourcesFile =
                ResourcesFile.Read(Path.Combine(runDirectory, "v23." + fileName), fileName);
            var oldDatabase =
                ResourcesDatabase.Empty.AddFile(oldResourcesFile);
            var newResourcesFile = ResourcesFile.Read(Path.Combine(runDirectory, "v24." + fileName), fileName);
            var newDatabase = ResourcesDatabase.Empty.AddFile(newResourcesFile);
            var withImportedTranslations = newDatabase.ImportTranslations(oldDatabase, Languages, out _, out _);
            var fileWithImported = withImportedTranslations.ResourcesFiles.Values.Single();
            var entry = fileWithImported.FindEntry("btnAddFile.Location");
            Assert.IsNotNull(entry);
            var localizedValue = entry.GetTranslation("ja");
            Assert.IsNotNull(localizedValue);
            Assert.IsNotNull(localizedValue.IssueType);
            var exportedDoc = fileWithImported.ExportResx("ja", true);
            var dataItem = exportedDoc.Root!.Elements("data").Single(el => el.Attribute("name")?.Value == entry.Name);
            var comment = dataItem.Elements("comment").SingleOrDefault();
            Assert.IsNotNull(comment);
        }
    }
}
