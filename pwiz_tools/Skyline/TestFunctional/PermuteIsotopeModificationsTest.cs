/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that all transitions get a chromatogram even when there are
    /// multiple precursors with the same precursor m/z but with different label types
    /// and product m/z's.
    /// Data came from this support request:
    /// https://skyline.ms/announcements/home/support/thread.view?rowId=52220
    /// </summary>
    [TestClass]
    public class PermuteIsotopeModificationsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPermuteIsotopeModifications()
        {
            TestFilesZip = @"TestFunctional\PermuteIsotopeModificationsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("PermuteIsotopeModificationsTest.sky")));
            // This document contains two peptides
            Assert.AreEqual(2, SkylineWindow.Document.MoleculeCount);
            Assert.AreEqual(2, SkylineWindow.Document.MoleculeTransitionGroupCount);

            // Do "Permute Isotope Modifications" and choose to do the complete 
            RunDlg<PermuteIsotopeModificationsDlg>(SkylineWindow.ShowPermuteIsotopeModificationsDlg, true, dlg =>
            {
                dlg.SimplePermutation = false;
                dlg.OkDialog();
            });

            // Each of the peptides in the document has 2 lysine, which will result in 4 isotope permutations
            Assert.AreEqual(2, SkylineWindow.Document.MoleculeCount);
            Assert.AreEqual(8, SkylineWindow.Document.MoleculeTransitionGroupCount);

            ImportResultsFile(TestFilesDir.GetTestPath("PermuteLabels.mzML"));
            var measuredResults = SkylineWindow.Document.Settings.MeasuredResults;
            Assert.IsNotNull(measuredResults);
            Assert.AreEqual(1, measuredResults.Chromatograms.Count);

            var chromatogramSet = measuredResults.Chromatograms[0];
            foreach (var peptideDocNode in SkylineWindow.Document.Molecules)
            {
                Assert.AreEqual(4, peptideDocNode.TransitionGroupCount);
                var precursorGroupings = peptideDocNode.TransitionGroups.GroupBy(tg => tg.PrecursorMz).ToList();
                Assert.AreEqual(3, precursorGroupings.Count);
                foreach (var precursorGrouping in precursorGroupings)
                {
                    var ms2mzs = precursorGrouping.SelectMany(p => p.GetMsMsTransitions(true)).Select(t => t.Mz)
                        .ToHashSet();
                    Assert.IsTrue(measuredResults.TryLoadChromatogram(chromatogramSet, peptideDocNode,
                        precursorGrouping.First(), .05f, out var chromGroupInfos));
                    Assert.AreEqual(1, chromGroupInfos.Length);
                    var chromatogramGroupInfo = chromGroupInfos[0];
                    var ms2Chromatograms =
                        chromatogramGroupInfo.TransitionPointSets.Where(c => c.Source == ChromSource.fragment).ToList();
                    Assert.AreEqual(ms2mzs.Count, ms2Chromatograms.Count);
                    var ms2ChromMzs = ms2Chromatograms.Select(c => c.ProductMz).ToHashSet();
                    AssertEx.AreEqual(ms2mzs.Count, ms2ChromMzs.Count);
                    CollectionAssert.AreEquivalent(ms2mzs.ToList(), ms2ChromMzs.ToList());
                }
            }
        }
    }
}
