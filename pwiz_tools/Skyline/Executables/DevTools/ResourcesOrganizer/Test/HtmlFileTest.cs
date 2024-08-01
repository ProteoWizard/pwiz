using HtmlAgilityPack;
using ResourcesOrganizer.ResourcesModel;

namespace Test
{
    [TestClass]
    public class HtmlFileTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestHtmlFile()
        {
            string folder = SaveManifestResources(typeof(HtmlFileTest));
            string htmlFileName = "Skyline Custom Reports.html";
            var localizedFolders = new Dictionary<string, string>
            {
                { "ja", Path.Combine(folder, "Japanese") },
                { "zh-CHS", Path.Combine(folder, "Chinese") }
            };
            foreach (var localizedFolder in localizedFolders)
            {
                Directory.CreateDirectory(localizedFolder.Value);
                var folderName = Path.GetFileName(localizedFolder.Value);
                string prefix = folderName + ".";
                foreach (var file in Directory.EnumerateFiles(folder))
                {
                    var fileName = Path.GetFileName(file);
                    if (fileName.StartsWith(prefix))
                    {
                        File.Move(file, Path.Combine(localizedFolder.Value, fileName.Substring(prefix.Length)));
                    }
                }
            }

            var htmlFile = HtmlFile.Read(Path.Combine(folder, htmlFileName), htmlFileName, localizedFolders);
            foreach (var entry in localizedFolders.Prepend(new KeyValuePair<string?, string>(null, folder)))
            {
                var normalizedHtmlDoc = new HtmlDocument();
                normalizedHtmlDoc.Load(Path.Combine(entry.Value, htmlFileName));
                normalizedHtmlDoc.Save(Path.Combine(entry.Value, "Normalized.html"));
                htmlFile.ExportHtmlDocument(entry.Key).Save(Path.Combine(entry.Value, "RoundTrip.html"));
            }
        }
    }
}
