using System;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Databinding;
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
            WaitForGraphs();
            var candidatePeakForm = WaitForOpenForm<CandidatePeakForm>();
            Assert.IsNotNull(candidatePeakForm);
            WaitForConditionUI(() => candidatePeakForm.IsComplete && candidatePeakForm.RowCount > 0);
            VerifyFeatureScores();
        }

        private void VerifyFeatureScores()
        {
            var candidatePeakForm = FindOpenForm<CandidatePeakForm>();
            WaitForConditionUI(() => candidatePeakForm.IsComplete);
            var colIntensity =
                FindFeatureColumn(candidatePeakForm.DataboundGridControl, typeof(MQuestIntensityCalc));
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
