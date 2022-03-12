using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class DdaPeakIntegrationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestDdaPeakIntegration()
        {
            TestFilesZip = @"TestFunctional\DdaPeakIntegrationTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("DdaIntegrationTest.sky"));
                SkylineWindow.SelectedPath =
                    SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.TransitionGroups, 0);

            });
            ImportResultsFile(TestFilesDir.GetTestPath("S_1_LLVVYPWTQR.mzML"));
            VerifyDdaPeakAreas(SkylineWindow.Document);
            var graphChromatogram = FindOpenForm<GraphChromatogram>();
            Assert.IsNotNull(graphChromatogram);
            WaitForGraphs();
            RunUI(() =>
            {
                var transitionGroup = SkylineWindow.Document.MoleculeTransitionGroups.First();
                graphChromatogram.FirePickedPeak(transitionGroup, null, new ScaledRetentionTime(46.464073181152344));
            });
            VerifyDdaPeakAreas(SkylineWindow.Document);
        }

        private void VerifyDdaPeakAreas(SrmDocument document)
        {
            var measuredResults = document.MeasuredResults;
            float tolerance = (float) document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            Assert.IsNotNull(measuredResults);
            foreach (var peptideDocNode in document.Molecules)
            {
                foreach (var transitionGroupDocNode in peptideDocNode.TransitionGroups)
                {
                    Assert.IsNotNull(transitionGroupDocNode.Results);
                    Assert.AreEqual(transitionGroupDocNode.Results.Count, measuredResults.Chromatograms.Count);
                    for (int iReplicate = 0; iReplicate < measuredResults.Chromatograms.Count; iReplicate++)
                    {
                        var chromatogramSet = measuredResults.Chromatograms[iReplicate];
                        Assert.IsTrue(measuredResults.TryLoadChromatogram(chromatogramSet, peptideDocNode, transitionGroupDocNode, tolerance, out var chromatogramGroupInfos));
                        Assert.AreEqual(1, chromatogramGroupInfos.Length);
                        var chromatogramGroupInfo = chromatogramGroupInfos[0];
                        foreach (var transitionDocNode in transitionGroupDocNode.Transitions)
                        {
                            var transitionChromInfos = transitionDocNode.GetSafeChromInfo(iReplicate);
                            Assert.AreEqual(1, transitionChromInfos.Count);
                            var transitionChromInfo = transitionChromInfos[0];
                            AssertEx.IsLessThanOrEqual(transitionChromInfo.StartRetentionTime, transitionChromInfo.RetentionTime);
                            AssertEx.IsGreaterThanOrEqual(transitionChromInfo.EndRetentionTime, transitionChromInfo.RetentionTime);
                            // TODO(nicksh): PointsAcrossPeak reports its value as "null" instead of "zero" for backwards compatibility reasons
                            // Re-enable this assert when that behavior is fixed (we never anticipated that someone would need to have a peak 
                            // with zero points in it, but this happens all the time with DDA MS2).
                            Assert.IsNotNull(transitionChromInfo.PointsAcrossPeak);

                            var chromatogramInfo = chromatogramGroupInfo.GetTransitionInfo(transitionDocNode, tolerance,
                                TransformChrom.raw, null);
                            Assert.IsNotNull(chromatogramInfo);
                            if (transitionDocNode.IsMs1)
                            {
                                continue;
                            }
                            AssertEx.AreEqual(0, transitionChromInfo.Fwhm);
                            AssertEx.AreEqual(transitionChromInfo.Area, transitionChromInfo.Height);
                            if (transitionChromInfo.PointsAcrossPeak > 0)
                            {
                                AssertEx.IsNotNull(transitionChromInfo.MassError);
                            }
                            else
                            {
                                AssertEx.IsNull(transitionChromInfo.MassError);
                                AssertEx.AreEqual(0, transitionChromInfo.Area);
                            }
                            Console.Out.Write("hello");
                        }
                    }
                }
            }
        }
    }
}
