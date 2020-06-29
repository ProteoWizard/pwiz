/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that manually chosen peak boundaries are preserved when doing a reimport.
    /// </summary>
    [TestClass]
    public class ReimportPeakBoundariesTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestReimportPeakBoundaries()
        {
            TestFilesZip = @"TestFunctional\ReimportPeakBoundariesTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const double originalPeakTime = 31.3;
            const double newPeakTime = 30.7;
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("160109_Mix1_calcurve.sky")));
            ImportResultsFile(TestFilesDir.GetTestPath("160109_Mix1_calcurve_073.mzML"));
            var transitionIdPath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Transitions, 1);
            RunUI(()=>SkylineWindow.SequenceTree.SelectedPath  =  transitionIdPath);
            WaitForGraphs();

            // Verify that the initial chosen peak boundary is at originalPeakTime
            var transitionDocNode = SkylineWindow.Document.FindNode(transitionIdPath) as TransitionDocNode;
            Assert.IsNotNull(transitionDocNode);
            Assert.IsNotNull(transitionDocNode.Results);
            Assert.AreEqual(1, transitionDocNode.Results.Count);
            var transitionChromInfo = transitionDocNode.Results[0].First();
            Assert.AreEqual(originalPeakTime, transitionChromInfo.RetentionTime, .1);
            Assert.AreEqual(UserSet.FALSE, transitionChromInfo.UserSet);

            // Manually choose the peak at newPeakTime
            var graphChromatogram = SkylineWindow.GraphChromatograms.First();
            var chromGroupInfo = graphChromatogram.ChromGroupInfos[0];
            var chromatogramInfo =
                chromGroupInfo.TransitionPointSets.FirstOrDefault(chrom =>
                    Math.Abs(transitionDocNode.Mz - chrom.ProductMz) < 0.055);
            Assert.IsNotNull(chromatogramInfo);
            var newPeak = chromatogramInfo.Peaks.FirstOrDefault(peak => Math.Abs(newPeakTime - peak.RetentionTime) < .1);

            RunUI(() =>
            {
                var transitionGroupDocNode =
                    SkylineWindow.Document.FindNode(transitionIdPath.Parent) as TransitionGroupDocNode;
                Assert.IsNotNull(transitionGroupDocNode);
                graphChromatogram.FirePickedPeak(transitionGroupDocNode, transitionDocNode, new ScaledRetentionTime(newPeak.RetentionTime));
            });
            transitionDocNode = SkylineWindow.Document.FindNode(transitionIdPath) as TransitionDocNode;
            Assert.IsNotNull(transitionDocNode);
            Assert.AreEqual(1, transitionDocNode.Results.Count);
            transitionChromInfo = transitionDocNode.Results[0].First();
            Assert.AreEqual(newPeakTime, transitionChromInfo.RetentionTime, .1);
            Assert.AreEqual(UserSet.TRUE, transitionChromInfo.UserSet);
            
            // Do a reimport, and make sure that the manually chosen peak boundary remains
            var doc = SkylineWindow.Document;
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
            {
                dlg.SelectedChromatograms = SkylineWindow.Document.MeasuredResults.Chromatograms.ToArray();
                dlg.ReimportResults();
                dlg.OkDialog();
            });
            WaitForDocumentChange(doc);
            WaitForDocumentLoaded();
            transitionDocNode = SkylineWindow.Document.FindNode(transitionIdPath) as TransitionDocNode;
            Assert.IsNotNull(transitionDocNode);
            Assert.AreEqual(1, transitionDocNode.Results.Count);
            transitionChromInfo = transitionDocNode.Results[0].First();
            Assert.AreEqual(newPeakTime, transitionChromInfo.RetentionTime, .1);
            Assert.AreEqual(UserSet.TRUE, transitionChromInfo.UserSet);

            // Remove the replicate from the document and import it again.
            doc = SkylineWindow.Document;
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
            {
                dlg.RemoveAllReplicates();
                dlg.OkDialog();
            });
            WaitForDocumentChange(doc);
            RunUI(()=>SkylineWindow.SaveDocument());
            Assert.IsFalse(SkylineWindow.Document.Settings.HasResults);
            ImportResultsFile(TestFilesDir.GetTestPath("160109_Mix1_calcurve_073.mzML"));

            // Verify that the chosen peak has gone back to originalPeakTime
            transitionDocNode = SkylineWindow.Document.FindNode(transitionIdPath) as TransitionDocNode;
            Assert.IsNotNull(transitionDocNode);
            Assert.IsNotNull(transitionDocNode.Results);
            Assert.AreEqual(1, transitionDocNode.Results.Count);
            transitionChromInfo = transitionDocNode.Results[0].First();
            Assert.AreEqual(originalPeakTime, transitionChromInfo.RetentionTime, .1);
            Assert.AreEqual(UserSet.FALSE, transitionChromInfo.UserSet);
        }
    }
}
