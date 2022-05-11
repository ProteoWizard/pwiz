/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class CandidatePeakTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestCandidatePeaks()
        {
            TestFilesZip = @"TestFunctional\CandidatePeakTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("CandidatePeakTestTargetsOnly.sky"));
                SkylineWindow.ShowCandidatePeaks();
            });
            SelectPeptide("SLDLIESLLR");
            var candidatePeakForm = WaitForOpenForm<CandidatePeakForm>();
            Assert.IsNotNull(candidatePeakForm);
            WaitForDocumentLoaded();
            WaitForGraphs();
            WaitForConditionUI(() => candidatePeakForm.IsComplete);
            const int detectedPeakCount = 10;
            Assert.AreEqual(detectedPeakCount, candidatePeakForm.RowCount);

            Assert.AreEqual("FullGradientModel", SkylineWindow.Document.Settings.PeptideSettings.Integration.PeakScoringModel.Name);
            VerifyFeatureScores(typeof(MQuestIntensityCalc),
                typeof(MQuestRetentionTimePredictionCalc),
                typeof(MQuestIntensityCorrelationCalc), typeof(MQuestWeightedShapeCalc),
                typeof(MQuestWeightedCoElutionCalc), typeof(LegacyUnforcedCountScoreCalc),
                typeof(NextGenSignalNoiseCalc), typeof(NextGenProductMassErrorCalc));
            var graphChromatogram = FindOpenForm<GraphChromatogram>();
            Assert.IsNotNull(graphChromatogram);

            // Use the chromatogram graph to integrate a peak from 60 to 65 minutes
            const double chosenPeakStartTime = 60;
            const double chosenPeakEndTime = 65;
            RunUI(() =>
            {
                var peptideDocNode = (PeptideDocNode)SkylineWindow.Document.FindNode(SkylineWindow.SelectedPath); 
                var groupPath = new IdentityPath(SkylineWindow.SelectedPath,
                    peptideDocNode.TransitionGroups.First().TransitionGroup);
                graphChromatogram.SimulateChangedPeakBounds(new List<ChangedPeakBoundsEventArgs>()
                {
                    new ChangedPeakBoundsEventArgs(groupPath, null, graphChromatogram.NameSet,
                        graphChromatogram.FilePath, new ScaledRetentionTime(chosenPeakStartTime),
                        new ScaledRetentionTime(chosenPeakEndTime), null, PeakBoundsChangeType.both)
                });
                peptideDocNode = (PeptideDocNode)SkylineWindow.Document.FindNode(SkylineWindow.SelectedPath);
                // Verify that the chosen peak boundaries were applied to all of the transition groups
                foreach (var transitionGroup in peptideDocNode.TransitionGroups)
                {
                    var transitionGroupChromInfo = transitionGroup.ChromInfos.FirstOrDefault();
                    Assert.IsNotNull(transitionGroupChromInfo);
                    Assert.IsNotNull(transitionGroupChromInfo.StartRetentionTime);
                    Assert.AreEqual(chosenPeakStartTime, transitionGroupChromInfo.StartRetentionTime.Value, 1);
                    Assert.IsNotNull(transitionGroupChromInfo.EndRetentionTime);
                    Assert.AreEqual(chosenPeakEndTime, transitionGroupChromInfo.EndRetentionTime.Value, 1);
                }
            });
            WaitForConditionUI(() => candidatePeakForm.IsComplete);
            RunUI(() =>
            {
                // Verify that there is now one extra row in the grid for the custom integrated peak
                var candidatePeakGroups = candidatePeakForm.BindingListSource.OfType<RowItem>()
                    .Select(item => item.Value).OfType<CandidatePeakGroup>().ToList();
                Assert.AreEqual(detectedPeakCount + 1, candidatePeakGroups.Count);
                var chosenPeak = candidatePeakGroups.FirstOrDefault(peak => peak.Chosen);
                Assert.IsNotNull(chosenPeak);
                // Uncheck the "chosen" box on the selected peak row, and the row will disappear
                chosenPeak.Chosen = false;
                Assert.AreEqual(chosenPeakStartTime, chosenPeak.PeakGroupStartTime, 1);
                Assert.AreEqual(chosenPeakEndTime, chosenPeakEndTime, 1);
            });
            WaitForConditionUI(() => candidatePeakForm.IsComplete);
            RunUI(() =>
            {
                var candidatePeakGroups = candidatePeakForm.BindingListSource.OfType<RowItem>()
                    .Select(item => item.Value).OfType<CandidatePeakGroup>().ToList();
                Assert.AreEqual(detectedPeakCount, candidatePeakGroups.Count);
                var chosenPeak = candidatePeakGroups.FirstOrDefault(peak => peak.Chosen);
                Assert.IsNull(chosenPeak);
            });

            // Reintegrate using the default peak scoring model
            RunDlg<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog, reintegrateDlg =>
                {
                    reintegrateDlg.OverwriteManual = true;
                    reintegrateDlg.ComboPeakScoringModelSelected = LegacyScoringModel.DEFAULT_NAME;
                    reintegrateDlg.OkDialog();
                }
            );
            WaitForConditionUI(() => candidatePeakForm.IsComplete);
            RunUI(() =>
            {
                var candidatePeakGroups = candidatePeakForm.BindingListSource.OfType<RowItem>()
                    .Select(item => item.Value).OfType<CandidatePeakGroup>().ToList();
                Assert.AreEqual(detectedPeakCount, candidatePeakGroups.Count);
                var chosenPeak = candidatePeakGroups.FirstOrDefault(peak => peak.Chosen);
                Assert.IsNotNull(chosenPeak);
            });
            VerifyFeatureScores(LegacyScoringModel.DEFAULT_MODEL.PeakFeatureCalculators.Select(calc => calc.GetType())
                .ToArray());
            int rowCount = candidatePeakForm.RowCount;
            for (int i = 0; i < rowCount; i++)
            {
                RunUI(() =>
                {
                    var candidatePeakGroup =
                        ((RowItem) candidatePeakForm.BindingListSource[i]).Value as CandidatePeakGroup;
                    Assert.IsNotNull(candidatePeakGroup);
                    var colChosen =
                        candidatePeakForm.DataboundGridControl.FindColumn(
                            PropertyPath.Root.Property(nameof(CandidatePeakGroup.Chosen)));
                    Assert.IsNotNull(colChosen);
                    candidatePeakForm.DataGridView.CurrentCell =
                        candidatePeakForm.DataGridView.Rows[i].Cells[colChosen.Index];
                    var checkBoxCell = candidatePeakForm.DataGridView.CurrentCell as DataGridViewCheckBoxCell;
                    Assert.IsNotNull(checkBoxCell);
                    candidatePeakForm.DataGridView.BeginEdit(false);
                    checkBoxCell.Value = true;
                    Assert.IsTrue(candidatePeakForm.DataGridView.EndEdit());
                    var peptideDocNode = (PeptideDocNode)SkylineWindow.Document.FindNode(SkylineWindow.SelectedPath);
                    foreach (var transitionGroup in peptideDocNode.TransitionGroups)
                    {
                        var transitionGroupChromInfo = transitionGroup.ChromInfos.FirstOrDefault();
                        Assert.IsNotNull(transitionGroupChromInfo);
                        Assert.IsNotNull(transitionGroupChromInfo.StartRetentionTime);
                        Assert.AreEqual(candidatePeakGroup.PeakGroupStartTime, transitionGroupChromInfo.StartRetentionTime.Value, 1);
                        Assert.IsNotNull(transitionGroupChromInfo.EndRetentionTime);
                        Assert.AreEqual(candidatePeakGroup.PeakGroupEndTime, transitionGroupChromInfo.EndRetentionTime.Value, 1);
                    }
                });
            }
        }

        /// <summary>
        /// Verify that the expected feature scores are present in the grid.
        /// Also, verifies that the Intensity score has the expected value.
        /// </summary>
        private void VerifyFeatureScores(params Type[] expectedFeatureScores)
        {
            var candidatePeakForm = FindOpenForm<CandidatePeakForm>();
            WaitForConditionUI(() => candidatePeakForm.IsComplete);
            foreach (var featureScore in expectedFeatureScores)
            {
                var column = FindFeatureColumn(candidatePeakForm.DataboundGridControl, featureScore);
                Assert.IsNotNull(column, "Unable to find column for feature {0}", featureScore);
            }

            Type intensityFeatureScore = expectedFeatureScores.FirstOrDefault(featureScore =>
                typeof(AbstractMQuestIntensityCalc).IsAssignableFrom(featureScore));
            Assert.IsNotNull(intensityFeatureScore);
            var colIntensity =
                FindFeatureColumn(candidatePeakForm.DataboundGridControl, intensityFeatureScore);
            Assert.IsNotNull(colIntensity);
            var document = SkylineWindow.Document;

            float tolerance = (float) document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            RunUI(() =>
            {
                var selectedPeptide =
                    document.FindNode(SkylineWindow.SelectedPath.GetPathTo((int)SrmDocument.Level.Molecules)) as
                        PeptideDocNode;
                if (selectedPeptide == null)
                {
                    return;
                }
                int replicateIndex = SkylineWindow.ComboResults.SelectedIndex;
                for (int iRow = 0; iRow < candidatePeakForm.RowCount; iRow++)
                {
                    var candidatePeakGroup =
                        (candidatePeakForm.BindingListSource[iRow] as RowItem)?.Value as CandidatePeakGroup;
                    Assert.IsNotNull(candidatePeakGroup);
                    var intensityScore =
                        candidatePeakForm.DataGridView.Rows[iRow].Cells[colIntensity.Index].Value as WeightedFeature;
                    Assert.IsNotNull(intensityScore);
                    Assert.IsNotNull(intensityScore.Score);
                    var peakIndex = candidatePeakGroup.GetCandidatePeakGroupData().PeakIndex;
                    double totalArea = 0.0;
                    foreach (var transitionGroup in selectedPeptide.TransitionGroups)
                    {
                        if (peakIndex.HasValue)
                        {
                            Assert.IsTrue(document.Settings.MeasuredResults.TryLoadChromatogram(
                                replicateIndex, selectedPeptide, transitionGroup, tolerance,
                                out var chromatogramGroupInfos));
                            Assert.AreEqual(1, chromatogramGroupInfos.Length);
                            foreach (var transition in transitionGroup.Transitions)
                            {
                                var chromatogramInfo = chromatogramGroupInfos[0]
                                    .GetTransitionInfo(transition, tolerance, TransformChrom.raw, null);
                                Assert.IsNotNull(chromatogramInfo);
                                var peak = chromatogramInfo.GetPeak(peakIndex.Value);
                                Assert.IsNotNull(peak);
                                totalArea += peak.Area;
                            }
                        }
                        else
                        {
                            totalArea += transitionGroup.Transitions.SelectMany(t => t.GetSafeChromInfo(replicateIndex))
                                .Sum(chromInfo => chromInfo.Area);
                        }
                    }
                    var expectedIntensityScore = Math.Max(0, Math.Log10(totalArea));
                    Assert.AreEqual(expectedIntensityScore, intensityScore.Score.Value, 1e-4);
                }
            });
        }

        private DataGridViewColumn FindFeatureColumn(DataboundGridControl databoundGridControl, Type featureType)
        {
            Assert.IsTrue(typeof(IPeakFeatureCalculator).IsAssignableFrom(featureType));
            PropertyPath ppWeightedFeature = PropertyPath.Root
                .Property(nameof(CandidatePeakGroup.PeakScores))
                .Property(nameof(PeakGroupScore.WeightedFeatures));
            var pivotKey = PivotKey.EMPTY.AppendValue(ppWeightedFeature.LookupAllItems(), FeatureKey.FromCalculatorType(featureType));
            return FindColumn(databoundGridControl, ppWeightedFeature.DictionaryValues(), pivotKey);
        }

        private DataGridViewColumn FindColumn(DataboundGridControl control, PropertyPath propertyPath, PivotKey pivotKey)
        {
            foreach (var property in control.BindingListSource.ItemProperties.OfType<ColumnPropertyDescriptor>())
            {
                if (!property.DisplayColumn.ColumnDescriptor.PropertyPath.Equals(propertyPath))
                {
                    continue;
                }

                if (pivotKey != null && !Equals(pivotKey, property.PivotKey))
                {
                    continue;
                }

                var column = control.DataGridView.Columns.OfType<DataGridViewColumn>()
                    .FirstOrDefault(col => col.DataPropertyName == property.Name);
                if (column != null)
                {
                    return column;
                }
            }

            return null;
        }

        private void SelectPeptide(string sequence)
        {
            foreach (var moleculeGroup in SkylineWindow.Document.MoleculeGroups)
            {
                foreach (var molecule in moleculeGroup.Molecules)
                {
                    if (sequence == molecule.Target.ToString())
                    {
                        RunUI(()=>SkylineWindow.SelectedPath = new IdentityPath(moleculeGroup.PeptideGroup, molecule.Peptide));
                        return;
                    }
                }
            }
            Assert.Fail("Unable to find peptide sequence {0}", sequence);
        }
    }
}
