/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.SkylineTestUtil;
using System;
using System.Linq;
using System.Windows.Forms;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that the "Original Score" value matches the "Model Score" value in the Candidate Peaks grid.
    /// </summary>
    [TestClass]
    public class OriginalPeakScoreTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestOriginalPeakScore()
        {
            TestFilesZip = @"TestFunctional\OriginalPeakScoreTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("OriginalPeakScoreTest.sky")));
            WaitForDocumentLoaded();

            // Perform "Rescore"
            RunLongDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                RunDlg<RescoreResultsDlg>(manageResultsDlg.Rescore, dlg => dlg.RescoreToFile(TestFilesDir.GetTestPath("RescoredDocument.sky")));
            }, manageResultsDlg => { });
            WaitForDocumentLoaded();

            // Verify that for each TransitionGroupChromInfo, the "OriginalPeak.Score" value matches the "Model Score" seen in the Candidate Peaks grid.
            RunUI(() =>
            {
                SkylineWindow.ShowCandidatePeaks();
            });
            var candidatePeakForm = FindOpenForm<CandidatePeakForm>();
            var document = SkylineWindow.Document;
            int transitionGroupCount = document.MoleculeTransitionGroupCount;
            for (int iTransitionGroup = 0; iTransitionGroup < transitionGroupCount; iTransitionGroup++)
            {
                var transitionGroupIdentityPath =
                    document.GetPathTo((int)SrmDocument.Level.TransitionGroups, iTransitionGroup);
                var transitionGroupDocNode =
                    (TransitionGroupDocNode)SkylineWindow.Document.FindNode(transitionGroupIdentityPath);
                RunUI(() => SkylineWindow.SelectedPath = transitionGroupIdentityPath);
                for (int replicateIndex = 0; replicateIndex < document.MeasuredResults.Chromatograms.Count; replicateIndex++)
                {
                    RunUI(() => SkylineWindow.SelectedResultsIndex = replicateIndex);
                    WaitForConditionUI(() => candidatePeakForm.IsComplete);
                    var transitionGroupChromInfo = transitionGroupDocNode.Results[replicateIndex].First();
                    RunUI(() =>
                    {
                        var colChosen =
                            candidatePeakForm.DataboundGridControl.FindColumn(
                                PropertyPath.Root.Property(nameof(CandidatePeakGroup.Chosen)));
                        var colModelScore = candidatePeakForm.DataboundGridControl.FindColumn(PropertyPath.Root
                            .Property(nameof(CandidatePeakGroup.PeakScores))
                            .Property(nameof(PeakGroupScore.ModelScore)));
                        DataGridViewRow chosenRow = candidatePeakForm.DataGridView.Rows.OfType<DataGridViewRow>().SingleOrDefault(row => true.Equals(row.Cells[colChosen.Index].Value));
                        Assert.IsNotNull(chosenRow, "Unable to find chosen peak");
                        var modelScore = Convert.ToSingle(chosenRow.Cells[colModelScore.Index].Value);
                        Assert.AreEqual(modelScore, transitionGroupChromInfo.OriginalPeak.Score);
                    });
                }
            }
        }
    }
}
