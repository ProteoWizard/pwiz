/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.ElementLocators.ExportAnnotations;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that properties can be exported and imported using "Export Annotations" and "Import Annotations" menu items.
    /// This test only tests <see cref="Peptide.StandardType"/>, <see cref="Peptide.NormalizationMethod"/> and <see cref="Replicate.SampleType"/>, since
    /// those are the only properties which needed a custom <see cref="ImportableAttribute.Formatter"/>.
    /// </summary>
    [TestClass]
    public class ExportPropertiesTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestExportProperties()
        {
            TestFilesZip = @"TestFunctional\ExportPropertiesTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ExportPropertiesTest.sky")));
            WaitForDocumentLoaded();
            var originalDocument = SkylineWindow.Document;
            var originalPropertiesFile = TestFilesDir.GetTestPath("OriginalProperties.csv");
            ExportDocumentProperties(originalPropertiesFile);
            Assert.AreEqual(originalDocument, SkylineWindow.Document);

            RunUI(()=>SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FindOpenForm<DocumentGridForm>();
            WaitForCondition(() => documentGrid.IsComplete);

            // Create a new report so that we can set the "Standard Type" and "Normalization Method" values on all of the peptides
            const string moleculePropertyReportName = "Molecule Properties Report";
            RunDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor =>
            {
                viewEditor.ChooseColumnsTab.RemoveColumns(0, viewEditor.ChooseColumnsTab.ColumnCount);
                var propertyPathMolecules = PropertyPath.Root
                    .Property(nameof(SkylineDocument.Proteins)).LookupAllItems()
                    .Property(nameof(Protein.Peptides)).LookupAllItems();
                viewEditor.ChooseColumnsTab.AddColumn(propertyPathMolecules);
                viewEditor.ChooseColumnsTab.AddColumn(propertyPathMolecules.Property(nameof(Peptide.StandardType)));
                viewEditor.ChooseColumnsTab.AddColumn(propertyPathMolecules.Property(nameof(Peptide.NormalizationMethod)));
                viewEditor.ViewName = moleculePropertyReportName;
                viewEditor.OkDialog();
            });
            WaitForCondition(() => documentGrid.IsComplete);
            var newStandardTypes = new[]
            {
                StandardType.GLOBAL_STANDARD,
                StandardType.QC,
                StandardType.SURROGATE_STANDARD
            };
            var newNormalizationMethods = new[]
            {
                NormalizationMethod.GLOBAL_STANDARDS,
                NormalizationMethod.EQUALIZE_MEDIANS,
                NormalizationMethod.FromIsotopeLabelTypeName(IsotopeLabelType.HEAVY_NAME)
            };
            Assert.AreEqual(newStandardTypes.Length, SkylineWindow.Document.MoleculeCount);
            Assert.AreEqual(newNormalizationMethods.Length, SkylineWindow.Document.MoleculeCount);
            RunUI(() =>
            {
                var colStandardType = documentGrid.FindColumn(PropertyPath.Root.Property(nameof(Peptide.StandardType)));
                Assert.IsNotNull(colStandardType);
                Assert.AreEqual(documentGrid.RowCount, newStandardTypes.Length);
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[0].Cells[colStandardType.Index];
                SetClipboardText(TextUtil.LineSeparate(newStandardTypes.Select(standardType=>standardType.ToString())));
                documentGrid.DataGridView.SendPaste();
                CollectionAssert.AreEqual(newStandardTypes, SkylineWindow.Document.Molecules.Select(mol=>mol.GlobalStandardType).ToList());


                var colNormalizationMethod =
                    documentGrid.FindColumn(PropertyPath.Root.Property(nameof(Peptide.NormalizationMethod)));
                Assert.IsNotNull(colNormalizationMethod);
                Assert.AreEqual(documentGrid.RowCount, newNormalizationMethods.Length);
                documentGrid.DataGridView.CurrentCell =
                    documentGrid.DataGridView.Rows[0].Cells[colNormalizationMethod.Index];
                SetClipboardText(TextUtil.LineSeparate(newNormalizationMethods.Select(normalizationMethod=>normalizationMethod.ToString())));
                documentGrid.DataGridView.SendPaste();
                CollectionAssert.AreEqual(newNormalizationMethods, SkylineWindow.Document.Molecules.Select(mol=>mol.NormalizationMethod).ToList());
            });

            // Set the "Sample Type" on the Replicates
            RunUI(() =>
            {
                documentGrid.DataboundGridControl.ChooseView(
                    ViewGroup.BUILT_IN.Id.ViewName(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates));
            });
            WaitForCondition(() => documentGrid.IsComplete);
            var newSampleTypes = new[] { SampleType.DOUBLE_BLANK, SampleType.STANDARD };
            Assert.AreEqual(newSampleTypes.Length, SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count);
            RunUI(() =>
            {
                var colSampleType = documentGrid.FindColumn(PropertyPath.Root.Property(nameof(Replicate.SampleType)));
                Assert.IsNotNull(colSampleType);
                Assert.AreEqual(documentGrid.RowCount, newSampleTypes.Length);
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[0].Cells[colSampleType.Index];
                SetClipboardText(TextUtil.LineSeparate(newSampleTypes.Select(sampleType=>sampleType.ToString())));
                documentGrid.DataGridView.SendPaste();
                CollectionAssert.AreEqual(newSampleTypes, SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Select(chrom=>chrom.SampleType).ToList());
            });
            var documentWithModifiedProperties = SkylineWindow.Document;
            Assert.AreNotEqual(originalDocument, documentWithModifiedProperties);

            var modifiedProperties = TestFilesDir.GetTestPath("ModifiedProperties.csv");
            ExportDocumentProperties(modifiedProperties);
            Assert.AreEqual(documentWithModifiedProperties, SkylineWindow.Document);

            // Reading back in the original properties CSV should get us back to the original document
            RunUI(()=>SkylineWindow.ImportAnnotations(originalPropertiesFile));
            Assert.AreEqual(originalDocument, SkylineWindow.Document);

            // Reading in the modified properties CSV should get us back to the modified document
            RunUI(()=>SkylineWindow.ImportAnnotations(modifiedProperties));
            Assert.AreEqual(documentWithModifiedProperties, SkylineWindow.Document);
        }

        private void ExportDocumentProperties(string filename)
        {
            var propertiesToExport = ImmutableList.ValueOf(new[]
                { nameof(Peptide.NormalizationMethod), nameof(Peptide.StandardType), nameof(Replicate.SampleType) });
            var typesToExport = new HashSet<Type>(new[] { typeof(MoleculeHandler), typeof(ReplicateHandler) });
            RunDlg<ExportAnnotationsDlg>(SkylineWindow.ShowExportAnnotationsDlg, exportAnnotationsDlg =>
            {
                exportAnnotationsDlg.SelectedProperties = propertiesToExport;
                exportAnnotationsDlg.SelectedHandlers =
                    exportAnnotationsDlg.Handlers.Where(handler => typesToExport.Contains(handler.GetType()));
                exportAnnotationsDlg.ExportAnnotations(filename);
                exportAnnotationsDlg.DialogResult = DialogResult.OK;
            });
        }
    }
}
