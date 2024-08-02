using System.Collections.Immutable;
using HtmlAgilityPack;
using ResourcesOrganizer.ResourcesModel;

namespace Test
{
    [TestClass]
    public class HtmlMergeTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestHtmlMerge()
        {
            string folder = SaveManifestResourcesWithSubfolders(typeof(HtmlMergeTest), "en", "ja", "zh-CHS");
            var htmlFile = HtmlFile.ReadFolder(folder, "Skyline Data Independent Acquisition");
            Assert.IsNotNull(htmlFile);
            foreach (var subfolder in new[] { "en", "ja", "zh-CHS" })
            {
                var language = "en" == subfolder ? null : subfolder;
                var normalizedHtmlDoc = new HtmlDocument();
                normalizedHtmlDoc.Load(Path.Combine(folder, subfolder, "index.html"));
                normalizedHtmlDoc.Save(Path.Combine(folder, subfolder, "Normalized.html"));
                htmlFile.ExportHtmlDocument(language).Save(Path.Combine(folder, subfolder, "RoundTrip.html"));
            }

            var database = new ResourcesDatabase()
                { ResourcesFiles = ImmutableDictionary<string, LocalizableFile>.Empty.Add("DIA", htmlFile) };
            database.ExportLocalizationCsv(Path.Combine(folder, "Localization_ja.csv"), "ja", out int jaEntryCount);
            Assert.AreNotEqual(0, jaEntryCount);
            database.ExportLocalizationCsv(Path.Combine(folder, "Localization_zh-CHS.csv"), "zh-CHS", out int zhEntryCount);
            Assert.AreNotEqual(0, zhEntryCount);
        }
    }
}
