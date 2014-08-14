/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    /// <summary>
    /// Verify consistent import of Agilent IMS data as we work on various code optimizations.
    /// </summary>
    [TestClass]
    public class PerfDecoysMatchTest : AbstractFunctionalTest
    {
        private static readonly string[] DDA_FILES = {"S_1.RAW", "S_2.RAW", "S_3.RAW", "S_4.RAW"};
        private static readonly string[] DIA_FILES = {"20130311_DIA_Pit01.raw", "20130311_DIA_Pit02.raw"};

        private static readonly string[] IRT_FILES =
        {
            "napedro_L120417_010_SW.wiff",
            "napedro_L120417_010_SW.wiff.mtd",
            "napedro_L120417_010_SW.wiff.scan"
        };

        [TestMethod] 
        public void DecoysMatchTest()
        {
            TestFilesZip = "http://proteome.gs.washington.edu/software/test/skyline-perf/DecoyMatch.zip";
            TestFilesPersistent = new[] {DDA_FILES, DIA_FILES, IRT_FILES}.SelectMany(l => l).ToArray();

            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // DDA with IDs and alignment
            string filePathDda = TestFilesDir.GetTestPath(@"DDA\Bereman_5proteins_spikein_decoys.sky.zip");
            RunUI(() => SkylineWindow.OpenSharedFile(filePathDda));
            var doc = WaitForDocumentLoaded();
            int expectedDecoys = RefinementSettings.SuggestDecoyCount(doc);
            RunDlg<GenerateDecoysDlg>(SkylineWindow.ShowGenerateDecoysDlg, decoysDlg =>
            {
                decoysDlg.DecoysMethod = DecoyGeneration.SHUFFLE_SEQUENCE;
                decoysDlg.OkDialog();
            });
            doc = WaitForDocumentChange(doc);
            Assert.AreEqual(expectedDecoys, doc.Peptides.Count(p => p.IsDecoy));
            ImportResultsFiles(DDA_FILES.Select(s => GetDataFile("DDA", s)).Select(MsDataFileUri.Parse));
            RunUI(() =>
            {
                SkylineWindow.SaveDocument();
                SkylineWindow.ArrangeGraphsTiled();
                SkylineWindow.ShowAlignedPeptideIDTimes(true);
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.SelectedNode.FirstNode;
            });
            // Random nature of decoys makes it hard to make this very deterministic
            // But eventually we should see some peptides without IDs of their own
            // All peptides should have aligned IDs, if they don't have a single ID,
            // since it might be the only ID
            int countWithIds = 0, countTotal = 0;
            while (countWithIds < 20 || countTotal - 4 < countWithIds)
            {
                WaitForGraphs();
                RunUI(() =>
                {
                    foreach (var graphChromatogram in SkylineWindow.GraphChromatograms)
                    {
                        countTotal++;
                        int countIds = graphChromatogram.RetentionMsMs != null
                            ? graphChromatogram.RetentionMsMs.Length
                            : 0;
                        if (countIds != 1)
                        {
                            Assert.IsNotNull(graphChromatogram.AlignedRetentionMsMs);
                            Assert.AreNotEqual(0, graphChromatogram.AlignedRetentionMsMs.Length);
                        }
                        if (countIds > 0)
                        {
                            ValidateTimeRange(graphChromatogram, graphChromatogram.RetentionMsMs, 6, 90);
                            countWithIds++;
                        }
                        else
                        {
                            ValidateTimeRange(graphChromatogram, graphChromatogram.AlignedRetentionMsMs, 6, 90);
                        }
                    }
                    var nextNode = SkylineWindow.SequenceTree.SelectedNode.NextNode;
                    if (nextNode != null)
                        SkylineWindow.SequenceTree.SelectedNode = nextNode;
                });
            }

            // DIA with DDA IDs
            string filePathDia = TestFilesDir.GetTestPath(@"DIA\Hoofnagle_QE_DIA_targeted_decoys.sky.zip");
            RunUI(() => SkylineWindow.OpenSharedFile(filePathDia));
            doc = WaitForDocumentLoaded();
            expectedDecoys = RefinementSettings.SuggestDecoyCount(doc);
            RunDlg<GenerateDecoysDlg>(SkylineWindow.ShowGenerateDecoysDlg, decoysDlg =>
            {
                decoysDlg.DecoysMethod = DecoyGeneration.REVERSE_SEQUENCE;
                decoysDlg.OkDialog();
            });
            doc = WaitForDocumentChange(doc);
            Assert.AreEqual(expectedDecoys, doc.Peptides.Count(p => p.IsDecoy));
            ImportResultsFiles(DIA_FILES.Select(s => GetDataFile("DIA", s)).Select(MsDataFileUri.Parse));
            RunUI(() =>
            {
                SkylineWindow.SaveDocument();
                SkylineWindow.ArrangeGraphsTiled();
                SkylineWindow.ShowOtherRunPeptideIDTimes(true);
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.SelectedNode.FirstNode;
            });
            for (int i = 0; i < 6; i++)
            {
                WaitForGraphs();
                RunUI(() =>
                {
                    foreach (var graphChromatogram in SkylineWindow.GraphChromatograms)
                    {
                        Assert.IsNull(graphChromatogram.RetentionMsMs);
                        Assert.IsNull(graphChromatogram.AlignedRetentionMsMs);
                        Assert.IsNotNull(graphChromatogram.UnalignedRetentionMsMs);
                        Assert.AreNotEqual(0, graphChromatogram.UnalignedRetentionMsMs.Length);
                        ValidateTimeRange(graphChromatogram, graphChromatogram.UnalignedRetentionMsMs, 8, 120);
                    }
                    SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.SelectedNode.NextNode;
                });
            }

            // DIA with IRT values
            string filePathDiaIrt = TestFilesDir.GetTestPath(@"DIA-iRT\AQUA4_Human_picked_napedro2_full_rescore_iRT_decoys.sky.zip");
            RunUI(() => SkylineWindow.OpenSharedFile(filePathDiaIrt));
            doc = WaitForDocumentLoaded();
            expectedDecoys = RefinementSettings.SuggestDecoyCount(doc);
            RunDlg<GenerateDecoysDlg>(SkylineWindow.ShowGenerateDecoysDlg, decoysDlg =>
            {
                decoysDlg.DecoysMethod = DecoyGeneration.SHUFFLE_SEQUENCE;
                decoysDlg.OkDialog();
            });
            doc = WaitForDocumentChange(doc);
            Assert.AreEqual(expectedDecoys, doc.Peptides.Count(p => p.IsDecoy));
            ImportResultsFile(GetDataFile("DIA-iRT", IRT_FILES[0]));
            RunUI(() =>
            {
                SkylineWindow.SaveDocument();
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.SelectedNode.FirstNode;
            });

            for (int i = 0; i < 6; i++)
            {
                WaitForGraphs();
                RunUI(() =>
                {
                    foreach (var graphChromatogram in SkylineWindow.GraphChromatograms)
                    {
                        Assert.IsNull(graphChromatogram.RetentionMsMs);
                        Assert.IsNull(graphChromatogram.AlignedRetentionMsMs);
                        Assert.IsNull(graphChromatogram.UnalignedRetentionMsMs);
                        Assert.IsTrue(graphChromatogram.PredictedRT.HasValue);
                        ValidateTimeRange(graphChromatogram, new []{graphChromatogram.PredictedRT.Value}, 8, 135);
                    }
                    SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.SelectedNode.NextNode;
                });
            }
        }

        private void ValidateTimeRange(GraphChromatogram graphChromatogram, double[] msMsRTs, double range, double gradientLength)
        {
            var statMsMsRTs = new Statistics(msMsRTs);
            double minRT = double.MaxValue, maxRT = double.MinValue;
            foreach (var curve in graphChromatogram.CurveList)
            {
                minRT = Math.Min(minRT, curve.Points[0].X);
                maxRT = Math.Max(maxRT, curve.Points[curve.NPts - 1].X);
            }
            if (statMsMsRTs.Min() < range/2)
            {
                Assert.AreEqual(minRT, 0.0, 1);
                minRT = statMsMsRTs.Min() - range/2;
            }
            if (statMsMsRTs.Max() > gradientLength - range/2)
            {
                Assert.AreEqual(maxRT, gradientLength, 1);
                maxRT = statMsMsRTs.Max() + range/2;
            }
            double msMsRtsRange = statMsMsRTs.Range();
            double rangeActual = maxRT - minRT - msMsRtsRange;
            if (Math.Abs(range - rangeActual) > 0.2)
            {
                // Some cases with very wide range of scans may not have enough gradient
                // to accomodate the desired range.
                string message = string.Format("Expected: {0}, Actual: {1}, RangeIds: {2}, Min: {3}, Max: {4}, MinIds: {5}, MaxIds: {6}",
                    range, rangeActual, msMsRtsRange, minRT, maxRT, statMsMsRTs.Min(), statMsMsRTs.Max());
                Assert.IsTrue(msMsRtsRange > 10, message);
                Assert.IsTrue(rangeActual > range - 1, message);
            }
        }

        private string GetDataFile(string dirName, string fileName)
        {
            return TestFilesDir.GetTestPath(Path.Combine(dirName, fileName));
        }
    }
}