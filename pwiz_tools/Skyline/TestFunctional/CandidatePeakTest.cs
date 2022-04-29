using System;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
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
            WaitForConditionUI(() => candidatePeakForm.IsComplete);
            VerifyFeatureScores();
        }

        private void VerifyFeatureScores()
        {
            var candidatePeakForm = FindOpenForm<CandidatePeakForm>();
            WaitForConditionUI(() => candidatePeakForm.IsComplete);
            var colIntensity =
                FindFeatureColumn(candidatePeakForm.DataboundGridControl, typeof(MQuestDefaultIntensityCalc));
            Assert.IsNotNull(colIntensity);
        }

        private DataGridViewColumn FindFeatureColumn(DataboundGridControl databoundGridControl, Type featureType)
        {
            Assert.IsTrue(typeof(IPeakFeatureCalculator).IsAssignableFrom(featureType));
            PropertyPath ppWeightedFeature = PropertyPath.Root
                .Property(nameof(CandidatePeakGroup.PeakScores))
                .Property(nameof(PeakGroupScore.WeightedFeatures));
            var pivotKey = PivotKey.EMPTY.AppendValue(ppWeightedFeature.LookupAllItems(), featureType.FullName);
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
