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
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that <see cref="GroupComparisonDef.NormalizationMethod"/> with the value <see cref="NormalizeOption.CALIBRATED"/>
    /// yields the correct fold change numbers.
    /// </summary>
    [TestClass]
    public class BatchCalibrationGroupComparisonTest : AbstractFunctionalTest
    {
        private const string GROUP_COMPARISON_NAME = "AD vs HC";
        [TestMethod]
        public void TestBatchCalibrationGroupComparison()
        {
            TestFilesZip = @"TestFunctional\BatchCalibrationGroupComparisonTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("BatchCalibrationGroupComparisonTest.sky")));
            AnnotationDef conditionAnnotation =
                SkylineWindow.Document.Settings.DataSettings.AnnotationDefs.FirstOrDefault(def =>
                    def.Name == "Condition");
            Assert.IsNotNull(conditionAnnotation);
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Quantification;
                peptideSettingsUi.QuantRegressionFit = RegressionFit.LINEAR_THROUGH_ZERO;
                peptideSettingsUi.QuantNormalizationMethod = NormalizationMethod.TIC;
                peptideSettingsUi.QuantMsLevel = 2;
                peptideSettingsUi.OkDialog();
            });
            // First, define the group comparison with the normalization method "Default" which means
            // use the normalization option from quantification settings (i.e. TIC)
            RunDlg<EditGroupComparisonDlg>(SkylineWindow.AddGroupComparison, editGroupComparisonDlg =>
            {
                editGroupComparisonDlg.TextBoxName.Text = GROUP_COMPARISON_NAME;
                editGroupComparisonDlg.GroupComparisonDef = editGroupComparisonDlg.GroupComparisonDef
                    .ChangeNormalizeOption(NormalizeOption.DEFAULT)
                    .ChangeMsLevel(MsLevelOption.DEFAULT)
                    .ChangeControlAnnotation(conditionAnnotation.Name)
                    .ChangeControlValue("AD")
                    .ChangeCaseValue("HC"); 
                Assert.AreEqual(MsLevelOption.DEFAULT, editGroupComparisonDlg.GroupComparisonDef.MsLevel);
                Assert.AreEqual(NormalizeOption.DEFAULT, editGroupComparisonDlg.GroupComparisonDef.NormalizationMethod);
                editGroupComparisonDlg.OkDialog();
            });
            RunUI(()=>
            {
                SkylineWindow.ShowGroupComparisonWindow(GROUP_COMPARISON_NAME);
                SkylineWindow.ShowDocumentGrid(true);
            });
            var documentGridForm = FindOpenForm<DocumentGridForm>();
            RunDlg<ViewEditor>(documentGridForm.NavBar.CustomizeView, viewEditor =>
            {
                viewEditor.ViewName = "Peptide Quantities";
                viewEditor.ChooseColumnsTab.RemoveColumns(0, viewEditor.ChooseColumnsTab.ColumnCount);
                var ppPeptide = PropertyPath.Root
                    .Property(nameof(SkylineDocument.Proteins)).LookupAllItems()
                    .Property(nameof(Protein.Peptides)).LookupAllItems();
                viewEditor.ChooseColumnsTab.AddColumn(ppPeptide);
                var ppPeptideQuantification = ppPeptide
                    .Property(nameof(Peptide.Results)).DictionaryValues()
                    .Property(nameof(PeptideResult.Quantification));
                viewEditor.ChooseColumnsTab.AddColumn(ppPeptideQuantification.Property(nameof(QuantificationResult.NormalizedArea)));
                viewEditor.ChooseColumnsTab.AddColumn(ppPeptideQuantification.Property(nameof(QuantificationResult.CalculatedConcentration)));
                var ppReplicate = PropertyPath.Root.Property(nameof(SkylineDocument.Replicates)).LookupAllItems();
                viewEditor.ChooseColumnsTab.AddColumn(ppReplicate);
                viewEditor.ChooseColumnsTab.AddColumn(
                    ppReplicate.Property(AnnotationDef.GetColumnName(conditionAnnotation.Name)));
                viewEditor.OkDialog();
            });
            WaitForConditionUI(() => documentGridForm.IsComplete);
            var groupComparisonForm = FindOpenForm<FoldChangeGrid>();
            WaitForConditionUI(() => groupComparisonForm.DataboundGridControl.IsComplete);
            // Verify that the fold change numbers are what we expect from doing a linear regression on the Normalized Area values
            RunUI(() =>
            {
                var foldChangeRows = groupComparisonForm.DataboundGridControl.BindingListSource.OfType<RowItem>()
                    .Select(rowItem => (FoldChangeBindingSource.FoldChangeRow)rowItem.Value).ToList();
                Assert.AreEqual(foldChangeRows.Count, SkylineWindow.Document.MoleculeCount);
                foreach (var row in foldChangeRows)
                {
                    var controlValues = row.Peptide.Results.Values.Where(peptideResult =>
                            "AD".Equals(peptideResult.GetResultFile().Replicate.GetAnnotation(conditionAnnotation)))
                        .Select(peptideResult => peptideResult.Quantification.Value.NormalizedArea).OfType<double>().ToList();
                    var caseValues = row.Peptide.Results.Values.Where(peptideResult =>
                            "HC".Equals(peptideResult.GetResultFile().Replicate.GetAnnotation(conditionAnnotation)))
                        .Select(peptideResult => peptideResult.Quantification.Value.NormalizedArea).OfType<double>().ToList();
                    var expectedFoldChange = CalculateFoldChange(controlValues, caseValues);
                    Assert.AreEqual(expectedFoldChange, row.FoldChangeResult.FoldChange, 1e-4, "Peptide: {0}", row.Peptide);
                }
            });
            // Change the group comparison so that it uses the calibrated values
            RunUI(() =>
            {
                groupComparisonForm.ShowChangeSettings();
                var editGroupComparisonForm = FindOpenForm<EditGroupComparisonDlg>();
                editGroupComparisonForm.NormalizeOption = NormalizeOption.CALIBRATED;
            });
            // Verify that the fold change is what we expect from the calibrated values on the PeptideResult's
            WaitForConditionUI(() =>
                groupComparisonForm.FoldChangeBindingSource.GroupComparisonModel.PercentComplete == 100 &&
                groupComparisonForm.DataboundGridControl.IsComplete);
            RunUI(() =>
            {
                var foldChangeRows = groupComparisonForm.DataboundGridControl.BindingListSource.OfType<RowItem>()
                    .Select(rowItem => (FoldChangeBindingSource.FoldChangeRow)rowItem.Value).ToList();
                Assert.AreEqual(foldChangeRows.Count, SkylineWindow.Document.MoleculeCount);
                foreach (var row in foldChangeRows)
                {
                    var controlValues = row.Peptide.Results.Values.Where(peptideResult =>
                            "AD".Equals(peptideResult.GetResultFile().Replicate.GetAnnotation(conditionAnnotation)))
                        .Select(peptideResult => peptideResult.Quantification.Value.CalculatedConcentration).OfType<double>().ToList();
                    var caseValues = row.Peptide.Results.Values.Where(peptideResult =>
                            "HC".Equals(peptideResult.GetResultFile().Replicate.GetAnnotation(conditionAnnotation)))
                        .Select(peptideResult => peptideResult.Quantification.Value.CalculatedConcentration).OfType<double>().ToList();
                    var expectedFoldChange = CalculateFoldChange(controlValues, caseValues);
                    Assert.AreEqual(expectedFoldChange, row.FoldChangeResult.FoldChange, 1e-4, "Peptide: {0}", row.Peptide);
                }
            });
        }

        /// <summary>
        /// Calculates the fold change from the given values for the case and controls.
        /// </summary>
        private double CalculateFoldChange(IList<double> controlValues, IList<double> caseValues)
        {
            // The x values are either 0 or 1 depending on whether it's a control or a case
            var xValues = new List<double>();
            xValues.AddRange(Enumerable.Repeat(0.0, controlValues.Count));
            xValues.AddRange(Enumerable.Repeat(1.0, caseValues.Count));
            // The y values are the log10 of the values
            var yValues = new List<double>();
            yValues.AddRange(controlValues.Select(Math.Log10));
            yValues.AddRange(caseValues.Select(Math.Log10));
            
            var slope = Statistics.Slope(new Statistics(yValues), new Statistics(xValues));
            // The fold change is 10 to the power of the slope
            return Math.Pow(10, slope);
        }
    }
}
