using HtmlAgilityPack;
using ResourcesOrganizer.DataModel;
using ResourcesOrganizer.ResourcesModel;

namespace Test
{
    [TestClass]
    public class HtmlFileTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestHtmlFile()
        {
            string folder = SaveManifestResourcesWithSubfolders(typeof(HtmlFileTest), "en", "ja", "zh-CHS");
            var htmlFile = HtmlFile.ReadFolder(folder, "Skyline Custom Reports");
            Assert.IsNotNull(htmlFile);
            foreach (var subfolder in new[] { "en", "ja", "zh-CHS" })
            {
                var language = "en" == subfolder ? null : subfolder;
                var normalizedHtmlDoc = new HtmlDocument();
                normalizedHtmlDoc.Load(Path.Combine(folder, subfolder, "index.html"));
                normalizedHtmlDoc.Save(Path.Combine(folder, subfolder, "Normalized.html"));
                htmlFile.ExportHtmlDocument(language).Save(Path.Combine(folder, subfolder, "RoundTrip.html"));
            }
        }
    }
}
