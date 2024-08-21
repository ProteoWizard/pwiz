/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests importing results where one of the SRM chromatograms has zero points in it
    /// </summary>
    [TestClass]
    public class ZeroLengthSrmChromatogramTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestZeroLengthSrmChromatogram()
        {
            TestFilesZip = @"TestFunctional\ZeroLengthSrmChromatogramTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ZeroLengthSrmChromatogramTest.sky"));
            });
            ImportResultsFile(TestFilesDir.GetTestPath("ZeroLengthSrmChromatogram.mzML"));
            VerifyChromatograms(false);

            // Set the "Explicit Retention Time" on the peptide and verify that chromatograms can still be imported without error
            // and that the transition with a zero-length chromatogram ends up with a missing chromatogram
            RunUI(() =>
            {
                SkylineWindow.ModifyDocument("Set explicit retention time", SetExplicitRetentionTime);
            });

            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                manageResultsDlg.SelectedChromatograms = SkylineWindow.Document.MeasuredResults.Chromatograms;
                manageResultsDlg.ReimportResults();
                manageResultsDlg.OkDialog();
            });
            WaitForDocumentLoaded();
            VerifyChromatograms(true);
        }
        
        /// <summary>
        /// Sets the Explicit Retention Time on all the PeptideDocNode's to 27.
        /// </summary>
        private SrmDocument SetExplicitRetentionTime(SrmDocument document)
        {
            return (SrmDocument)document.ChangeChildren(document.MoleculeGroups.Select(peptideGroupDocNode =>
                    peptideGroupDocNode.ChangeChildren(peptideGroupDocNode.Molecules.Select(peptideDocNode =>
                        peptideDocNode.ChangeExplicitRetentionTime(27)).Cast<DocNode>().ToList())).Cast<DocNode>()
                .ToList());
        }

        /// <summary>
        /// Verify that all the transitions that are supposed to have a chromatogram
        /// do have one.
        /// </summary>
        private void VerifyChromatograms(bool zeroLengthChromatogramShouldBeMissing)
        {
            var document = SkylineWindow.Document;
            float tolerance = (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            var measuredResults = document.Settings.MeasuredResults;
            Assert.IsNotNull(measuredResults);
            var peptideDocNode = document.Molecules.Single();
            var transitionGroupDocNode = peptideDocNode.TransitionGroups.Single();
            var chromatogramSet = measuredResults.Chromatograms.Single();
            Assert.IsTrue(measuredResults.TryLoadChromatogram(chromatogramSet, peptideDocNode, transitionGroupDocNode,
                tolerance, out var chromatogramGroupInfos));
            Assert.IsNotNull(chromatogramGroupInfos);
            Assert.AreEqual(1, chromatogramGroupInfos.Length);
            var chromatogramGroupInfo = chromatogramGroupInfos[0];

            // Verify that all the transitions in the document have a chromatogram
            for (int iTransition = 0; iTransition < transitionGroupDocNode.TransitionCount; iTransition++)
            {
                var transitionDocNode = transitionGroupDocNode.Transitions.ElementAt(iTransition);
                var chromatogramInfo = chromatogramGroupInfo.GetTransitionInfo(transitionDocNode, tolerance);

                // In this data, the last transition's chromatogram is the one that has zero points in it
                bool expectedZeroLength = iTransition == transitionGroupDocNode.TransitionCount - 1;

                if (zeroLengthChromatogramShouldBeMissing && expectedZeroLength)
                {
                    Assert.IsNull(chromatogramInfo);
                    continue;
                }
                Assert.IsNotNull(chromatogramInfo);
                Assert.AreNotEqual(0, chromatogramInfo.TimeIntensities.NumPoints);
                Assert.IsNotNull(chromatogramInfo.RawTimes);
                if (expectedZeroLength)
                {
                    Assert.AreEqual(0, chromatogramInfo.RawTimes.Count);
                }
                else
                {
                    Assert.AreNotEqual(0, chromatogramInfo.RawTimes.Count);
                }
            }
        }
    }
}
