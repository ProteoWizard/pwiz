using ResourcesOrganizer;
using Program = ResourcesOrganizer.Program;

namespace Test
{
    [TestClass]
    public class ImportLocalizationCsvTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestImportLocalizationCsv()
        {
            string folder = TestContext.TestRunDirectory!;
            SaveManifestResources(typeof(ImportLocalizationCsvTest), folder);
            Environment.CurrentDirectory = TestContext.TestRunDirectory!;
            Assert.AreEqual(0, Program.DoAdd(new AddVerb() { DbFile = "resources.db", Path= ["."] }));
            Assert.AreEqual(0, Program.DoExportLocalizationCsv(new ExportLocalizationCsv()
            {
                DbFile = "resources.db", Output = "localization.csv"
            }));

        }
    }
}
