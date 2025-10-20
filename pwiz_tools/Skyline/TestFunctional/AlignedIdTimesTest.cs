/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that the expected number of ID lines are displayed on the chromatogram graph
    /// for each replicate when the user choose "Peptide ID Times > Aligned" and
    /// "Peptide ID Times > From Other Runs".
    /// </summary>
    [TestClass]
    public class AlignedIdTimesTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAlignedIdTimes()
        {
            TestFilesZip = @"TestFunctional\AlignedIdTimesTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("OneProtein.sky"));
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
                SkylineWindow.ArrangeGraphs(DisplayGraphsType.Tiled);
                SkylineWindow.ShowAlignedPeptideIDTimes(true);
                SkylineWindow.ShowPeptideIDTimes(true);
                SkylineWindow.ShowOtherRunPeptideIDTimes(true);
            });
            WaitForGraphs();
            var documentRetentionTimes = SkylineWindow.Document.Settings.DocumentRetentionTimes;

            // Replicate "REF-DIRP2-028_051" only has retention times for one peptide so it has no alignments to the other replicates
#if false
            var fileAlignment51 =
            documentRetentionTimes.FileAlignments.Find("TRX_Phase2_Pelt-P04_Ast_Neo_REF-DIRP2-028_051");
            Assert.IsNotNull(fileAlignment51);
            Assert.AreEqual(0, fileAlignment51.RetentionTimeAlignments.Count);

            RunUI(() =>
            {
                foreach (var graphChromatogram in SkylineWindow.GraphChromatograms)
                {
                    var alignedTimes = GetAlignedTimes(graphChromatogram);
                    var unalignedTimes = GetUnalignedTimes(graphChromatogram);
                    if (graphChromatogram.NameSet == "REF-DIRP2-028_051")
                    {
                        Assert.AreEqual(0, alignedTimes.Count);
                        Assert.AreNotEqual(0, unalignedTimes.Count);
                    }
                    else
                    {
                        Assert.AreNotEqual(0, alignedTimes.Count);
                        Assert.AreEqual(1, unalignedTimes.Count);
                    }
                }
            });

#endif
        }

        private IList<double> GetAlignedTimes(GraphChromatogram graphChromatogram)
        {
            return graphChromatogram.GraphItems.SelectMany(item => item.AlignedRetentionMsMs ?? Array.Empty<double>()).ToList();
        }

        private IList<double> GetUnalignedTimes(GraphChromatogram graphChromatogram)
        {
            return graphChromatogram.GraphItems.SelectMany(item => item.UnalignedRetentionMsMs ?? Array.Empty<double>()).ToList();
        }
    }
}
