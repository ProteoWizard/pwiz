using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    /// <summary>
    /// Test CE Optimization cases where measured transitions are not symmetrical
    /// around the regression value.
    /// </summary>
    [TestClass]
    public class AsymCEOptTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"TestData\Results\AsymCEOpt.zip";
        private const string DOCUMENT_NAME = "skyline error2.sky";
        private const string RESULTS_NAME = "CB1_Step 2_CE_Sample 02.wiff";

        [TestMethod]
        public void TestAsymCEOpt()
        {
            DoTestAgilentCEOpt();
        }

        private void DoTestAgilentCEOpt()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = TestFilesDir.GetTestPath(DOCUMENT_NAME);
            string cachePath = ChromatogramCache.FinalPathForName(docPath, null);
            FileEx.SafeDelete(cachePath);
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);

            using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
            {
                // Import the .wiff file
                ExportImport(docContainer, TestFilesDir.GetTestPath(RESULTS_NAME));
            }
        }

        private void ExportImport(ResultsTestDocumentContainer docContainer, string resultsPath)
        {
            var optRegression = docContainer.Document.Settings.TransitionSettings.Prediction.CollisionEnergy;
            int optSteps = optRegression.StepCount*2 + 1;
            int optSteps1 = optSteps - 1;   // First precursor is below 10 volts CE for 1 step
            int optSteps2 = optSteps - 3;   // Second precursor is below 10 volts CE for 3 steps

            // First export
            var exporter = new AbiMassListExporter(docContainer.Document)
            {
                OptimizeType = ExportOptimize.CE,
                OptimizeStepCount = optRegression.StepCount,
                OptimizeStepSize = optRegression.StepSize
            };
            string tranListPath = Path.ChangeExtension(docContainer.DocumentFilePath, TextUtil.EXT_CSV);
            exporter.Export(tranListPath);
            // Make sure line count matches expectations for steps below 10 volts CE
            Assert.AreEqual(5*optSteps1 + 5*optSteps2, File.ReadAllLines(tranListPath).Length);

            // Then import
            var resultsUri = new MsDataFilePath(resultsPath);
            var chromSet = new ChromatogramSet("Optimize", new[] {resultsUri}, Annotations.EMPTY, optRegression);
            var measuredResults = new MeasuredResults(new[] { chromSet });

            docContainer.ChangeMeasuredResults(measuredResults, 2, optSteps1 + optSteps2, 86);

            // Check expected optimization data with missing values for steps below 10 volts CE
            int expectedMissingSteps = optSteps - optSteps1;
            var document = docContainer.Document;
            foreach (var nodePep in document.Molecules)
            {
                // Rebuilt from the .skyd, since a transition does not keep its chrom infos any
                // more and only the .skyd has the optimization steps.
                var moleculeResults = new MoleculeResults(document.Settings, nodePep);
                foreach (var nodeGroup in nodePep.TransitionGroups)
                {
                    foreach (var nodeTran in nodeGroup.Transitions)
                    {
                        var chromInfos = moleculeResults.GetTransitionChromInfos(nodeGroup.TransitionGroup,
                            nodeTran.Transition, 0);
                        Assert.AreEqual(optSteps, chromInfos.Count,
                            "Wrong number of optimization steps for transition Mz: {0}", nodeTran.Mz);
                        for (int i = 0; i < optSteps; i++)
                        {
                            if (i < expectedMissingSteps)
                                Assert.IsTrue(chromInfos[i].IsEmpty);
                            else
                                Assert.IsFalse(chromInfos[i].IsEmpty);
                        }
                    }

                    expectedMissingSteps = optSteps - optSteps2;
                }
            }
        }
    }
}