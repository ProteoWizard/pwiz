using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using pwiz.Common.Database;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.SkylineTestUtil;
using pwiz.Skyline.Model.Prosit.Models;
using pwiz.Skyline.Properties;

namespace pwiz.SkylineTestConnected
{
    [TestClass]
    public class PrositBuildLibraryFromCsvTest : AbstractUnitTestEx
    {
        const string TEST_ZIP_PATH = @"Test\EncyclopeDiaHelpersTest.zip";

        [TestMethod]
        public void TestPrositBuildLibraryFromCsv()
        {
            if (!PrositConfigTest.HasPrositServer())
            {
                return;
            }

            Settings.Default.PrositIntensityModel = PrositIntensityModel.Models.First();
            Settings.Default.PrositRetentionTimeModel = PrositRetentionTimeModel.Models.First();

            TestFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            string prositCsvTestFilepath = TestFilesDir.GetTestPath("pan_human_library_690to705-z3_nce33.csv");
            string prositBlibOutputFilepath = TestFilesDir.GetTestPath("pan_human_library_690to705-z3_nce33-output.blib");
            string prositTsvExpectedFilepath = TestFilesDir.GetTestPath("pan_human_library_690to705-z3_nce33-expected.tsv");

            var pm = new CommandProgressMonitor(new StringWriter(), new ProgressStatus());
            IProgressStatus status = new ProgressStatus();
            var prositOutput = PrositHelpers.PredictBatchesFromPrositCsv(prositCsvTestFilepath, pm, ref status, CancellationToken.None);
            PrositHelpers.ExportPrositSpectraToBlib(prositOutput, prositBlibOutputFilepath, pm, ref status);

            AssertEx.IsTrue(File.Exists(prositBlibOutputFilepath));
            AssertEx.NoDiff(File.ReadAllText(prositTsvExpectedFilepath),
                string.Join("\n", SqliteOperations.DumpTable(prositBlibOutputFilepath, "RefSpectra")),
                null, new Dictionary<int, double>
                {
                    {5, 0.000001},  // m/z
                    {18, 0.0001}    // retention time
                });
        }
    }
}
