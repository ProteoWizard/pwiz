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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class QualitativeIonRatioTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestQualitativeIonRatio()
        {
            TestFilesZip = @"TestFunctional\QualitativeIonRatioTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("QualitativeIonRatioTest.sky"));
                SkylineWindow.ShowDocumentGrid(true);
            });

            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() =>
            {
                documentGrid.DataboundGridControl.ChooseView(ViewGroup.BUILT_IN.Id
                    .ViewName(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates));
            });
            RunDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor=>
            {
                var columnsTab = viewEditor.ChooseColumnsTab;
                columnsTab.RemoveColumns(0, columnsTab.ColumnCount);
                PropertyPath ppPeptides = PropertyPath.Root.Property(nameof(SkylineDocument.Proteins)).LookupAllItems()
                    .Property(nameof(Protein.Peptides)).LookupAllItems();
                columnsTab.AddColumn(ppPeptides);
                columnsTab.AddColumn(PropertyPath.Root.Property(nameof(SkylineDocument.Replicates)).LookupAllItems());
                PropertyPath ppPrecursors = ppPeptides.Property(nameof(Peptide.Precursors)).LookupAllItems();
                columnsTab.AddColumn(ppPrecursors.Property(nameof(Precursor.TargetQualitativeIonRatio)));
                PropertyPath ppPrecursorResults =
                    ppPrecursors.Property(nameof(Precursor.Results)).DictionaryValues();
                PropertyPath ppQuantification = ppPrecursorResults.Property(nameof(PrecursorResult.PrecursorQuantification));
                columnsTab.AddColumn(ppQuantification.Property(nameof(PrecursorQuantificationResult.QualitativeIonRatio)));
                columnsTab.AddColumn(ppQuantification.Property(nameof(PrecursorQuantificationResult.QualitativeIonRatioStatus)));
                viewEditor.ViewName = "IonRatios";
                viewEditor.OkDialog();
            });
            WaitForConditionUI(() => documentGrid.IsComplete);
            PropertyPath ppTargetIonRatio = PropertyPath.Root.Property(nameof(Precursor.TargetQualitativeIonRatio));
            RunUI(() =>
            {
                var colTargetIonRatio = documentGrid.FindColumn(ppTargetIonRatio);
                Assert.IsNotNull(colTargetIonRatio);
                for (int i = 0; i < documentGrid.RowCount; i++)
                {
                    Assert.IsNull(documentGrid.DataGridView.Rows[i].Cells[colTargetIonRatio.Index].Value);
                }
            });
            var document = SkylineWindow.Document;
            var identityPathsToSelect = new List<IdentityPath>();
            // Select the first transition in each transition group and mark it quantitative
            foreach (var moleculeList in document.MoleculeGroups)
            {
                foreach (var molecule in moleculeList.Molecules)
                {
                    Assert.AreEqual(1, molecule.Children.Count);
                    var precursor = molecule.TransitionGroups.First();
                    Assert.AreEqual(2, precursor.Children.Count);
                    identityPathsToSelect.Add(new IdentityPath(moleculeList.PeptideGroup, molecule.Peptide,
                        precursor.TransitionGroup, precursor.Transitions.First().Transition));
                }
            }
            RunUI(()=>
            {
                SkylineWindow.SequenceTree.SelectedPaths = identityPathsToSelect;
                SkylineWindow.MarkQuantitative(false);
            });
            RunUI(() =>
            {
                var ppPrecursorResults = PropertyPath.Root.Property(nameof(Precursor.Results))
                    .DictionaryValues();
                var ppIonRatio = ppPrecursorResults
                    .Property(nameof(PrecursorResult.PrecursorQuantification))
                    .Property(nameof(PrecursorQuantificationResult.QualitativeIonRatio));
                var ppReplicate = ppPrecursorResults
                    .Property(nameof(PrecursorResult.PeptideResult))
                    .Property(nameof(PeptideResult.ResultFile))
                    .Property(nameof(ResultFile.Replicate));
                var colIonRatio = documentGrid.DataboundGridControl.FindColumn(ppIonRatio);
                var colPeptide = documentGrid.DataboundGridControl.FindColumn(PropertyPath.Root.Property(nameof(Precursor.Peptide)));
                var colReplicate = documentGrid.DataboundGridControl.FindColumn(ppReplicate);
                for (int iRow = 0; iRow < documentGrid.RowCount; iRow++)
                {
                    var row = documentGrid.DataGridView.Rows[iRow];
                    var replicate = (Replicate) row.Cells[colReplicate.Index].Value;
                    var peptide = (Peptide) row.Cells[colPeptide.Index].Value;
                    var actualIonRatio = (double?) row.Cells[colIonRatio.Index].Value;
                    var expectedIonRatio = CalculateIonRatio(replicate, peptide);
                    string message = string.Format("Peptide: {0} Replicate: {1}", peptide, replicate);
                    if (!expectedIonRatio.HasValue)
                    {
                        Assert.IsNull(actualIonRatio, message);
                    }
                    else
                    {
                        Assert.IsNotNull(actualIonRatio, message);
                        Assert.AreEqual(expectedIonRatio.Value, actualIonRatio.Value, message);
                    }
                }
            });
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.IonRatioThreshold = 20;
                peptideSettingsUi.OkDialog();
            });
        }

        public double? CalculateIonRatio(Replicate replicate, Peptide peptide)
        {
            var numerator = new List<double>();
            var denominator = new List<double>();
            foreach (var transition in peptide.Precursors.SelectMany(precursor=>precursor.Transitions))
            {
                var transitionDocNode = transition.DocNode;
                var chromInfo = transitionDocNode.Results[replicate.ReplicateIndex].FirstOrDefault();
                if (chromInfo == null || chromInfo.IsEmpty || chromInfo.IsTruncated.GetValueOrDefault())
                {
                    continue;
                }

                if (transition.Quantitative)
                {
                    denominator.Add(chromInfo.Area);
                }
                else
                {
                    numerator.Add(chromInfo.Area);
                }
            }

            if (numerator.Count == 0 || denominator.Count == 0)
            {
                return null;
            }

            return numerator.Sum() / denominator.Sum();
        }
    }
}
