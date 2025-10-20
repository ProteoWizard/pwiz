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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that when a replicate has multiple injections, the Candidate Peaks window shows
    /// the peaks for an appropriate file.
    /// </summary>
    [TestClass]
    public class MultiInjectCandidatePeakTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMultiInjectCandidatePeak()
        {
            TestFilesZip = @"TestFunctional\MultiInjectCandidatePeakTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("MultiInjectCandidatePeakTest.sky"));
                SkylineWindow.ShowCandidatePeaks();
                SkylineWindow.SelectedResultsIndex = 0;
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
            });

            var firstGraphChrom = SkylineWindow.GetGraphChrom(SkylineWindow.Document.MeasuredResults.Chromatograms[0].Name);
            Assert.IsTrue(firstGraphChrom.Visible);
            var candidatePeakForm = FindOpenForm<CandidatePeakForm>();
            Assert.IsNotNull(candidatePeakForm);
            // Verify that the file shown in the candidate peaks window is the same as the file chosen in the chromatogram graph
            foreach (var moleculeGroup in SkylineWindow.Document.MoleculeGroups)
            {
                foreach (var molecule in moleculeGroup.Molecules)
                {
                    var moleculeIdentityPath = new IdentityPath(moleculeGroup.PeptideGroup, molecule.Peptide);
                    RunUI(() => SkylineWindow.SelectedPath = moleculeIdentityPath);
                    WaitForGraphs();
                    var candidatePeaks = GetCandidatePeaks(candidatePeakForm).ToList();
                    Assert.AreEqual(moleculeIdentityPath, candidatePeaks.Select(peak=>peak.GetPrecursorResult().Precursor.Peptide.IdentityPath).Distinct().Single());
                    int fileCount = firstGraphChrom.Files.Count;
                    if (fileCount == 1)
                    {
                        Assert.AreSame(UniqueFileId(candidatePeaks), CallUI(firstGraphChrom.GetChromFileInfoId));
                    }
                    else
                    {
                        for (int i = 0; i < fileCount; i++)
                        {
                            RunUI(() => firstGraphChrom.SelectedFileIndex = i);
                            
                            candidatePeaks = GetCandidatePeaks(candidatePeakForm).ToList();
                            WaitForGraphs();
                            var candidatePeakFileId = UniqueFileId(candidatePeaks);
                            var graphFileId = CallUI(firstGraphChrom.GetChromFileInfoId);
                            Assert.AreSame(candidatePeakFileId, graphFileId);
                        }
                    }
                }
            }
            // Close all the chromatogram windows 
            RunUI(() =>
            {
                foreach (var graphChrom in SkylineWindow.GraphChromatograms.ToList())
                {
                    graphChrom.Close();
                }

                SkylineWindow.SelectedResultsIndex = 0;
                firstGraphChrom.Close();
                Assert.AreEqual(0, SkylineWindow.SelectedResultsIndex);
                foreach (var graphChrom in SkylineWindow.GraphChromatograms)
                {
                    Assert.IsFalse(graphChrom.Visible);
                }
            });
            foreach (var moleculeGroup in SkylineWindow.Document.MoleculeGroups)
            {
                foreach (var molecule in moleculeGroup.Molecules)
                {
                    var moleculeIdentityPath = new IdentityPath(moleculeGroup.PeptideGroup, molecule.Peptide);
                    RunUI(() => SkylineWindow.SelectedPath = moleculeIdentityPath);
                    WaitForGraphs();
                    var candidatePeaks = GetCandidatePeaks(candidatePeakForm).ToList();
                    Assert.AreEqual(moleculeIdentityPath, candidatePeaks.Select(peak => peak.GetPrecursorResult().Precursor.Peptide.IdentityPath).Distinct().Single());
                    Assert.IsNotNull(UniqueFileId(candidatePeaks));
                    foreach (var candidatePeak in candidatePeaks)
                    {
                        Assert.AreEqual(candidatePeak.GetPrecursorResult().Precursor.Peptide.IdentityPath, moleculeIdentityPath);
                    }
                }
            }
        }

        private IEnumerable<CandidatePeakGroup> GetCandidatePeaks(CandidatePeakForm candidatePeakForm)
        {
            WaitForConditionUI(() => candidatePeakForm.IsComplete);
            return CallUI(()=>candidatePeakForm.DataboundGridControl.BindingListSource.OfType<RowItem>()
                .Select(rowItem => (CandidatePeakGroup)rowItem.Value));
        }

        private ChromFileInfoId UniqueFileId(IEnumerable<CandidatePeakGroup> peaks)
        {
            return peaks.Select(peak => ReferenceValue.Of(peak.GetPrecursorResult().GetResultFile().ChromFileInfoId))
                .Distinct()
                .SingleOrDefault().Value;
        }
    }
}
