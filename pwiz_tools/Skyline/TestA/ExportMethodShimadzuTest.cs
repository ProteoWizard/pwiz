using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    [TestClass]
    public class ExportMethodShimadzuTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"TestA\ExportMethodShimadzuTest.zip";

        [TestMethod]
        public void TestExportMethodShimadzu()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            var docPath = testFilesDir.GetTestPath("bgal.sky");
            var doc = ResultsUtil.DeserializeDocument(docPath);

            var outPath = testFilesDir.GetTestPath("out.lcm");
            var templatePath = testFilesDir.GetTestPath("40.lcm");
            var exporter = new ShimadzuMethodExporter(doc) {RunLength = 30};
            exporter.ExportMethod(outPath, templatePath, null);

            Assert.AreEqual(540672, new FileInfo(outPath).Length);
        }
    }
}
