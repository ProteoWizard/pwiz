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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;
using Transition = pwiz.Skyline.Model.Databinding.Entities.Transition;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that custom formats that the user have applied with the <see cref="ChooseFormatDlg"/>
    /// are respected when using the Copy All button or File > Export > Report.
    /// </summary>
    [TestClass]
    public class DocumentGridExportTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestDocumentGridExport()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.Paste("ELVISK");
                SkylineWindow.ShowDocumentGrid(true);
            });
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() =>
            {
                documentGrid.DataboundGridControl.ChooseView(
                    ViewGroup.BUILT_IN.Id.ViewName(Resources.SkylineViewContext_GetDocumentGridRowSources_Precursors));
            });
            WaitForCondition(() => documentGrid.IsComplete);
            const string chargeFormat = "0.#%";
            const string precursorMzFormat = "$0.00";
            SetFormat(documentGrid, PropertyPath.Root.Property(nameof(Precursor.Charge)), chargeFormat);
            SetFormat(documentGrid, PropertyPath.Root.Property(nameof(Precursor.Mz)), precursorMzFormat);
            RunUI(() =>
            {
                documentGrid.NavBar.ViewContext.CopyAll(documentGrid.NavBar, documentGrid.BindingListSource);
            });
            
            var clipboardText = ClipboardEx.GetText();
            var tsvReader = new DsvFileReader(new StringReader(clipboardText), '\t');
            Assert.IsNotNull(tsvReader.ReadLine());
            var firstPrecursor = SkylineWindow.Document.Molecules.First().TransitionGroups.First();
            Assert.AreEqual(firstPrecursor.PrecursorCharge.ToString(chargeFormat, CultureInfo.CurrentCulture), tsvReader.GetFieldByName(ColumnCaptions.PrecursorCharge));
            Assert.AreEqual(firstPrecursor.PrecursorMz.ToString(precursorMzFormat, CultureInfo.CurrentCulture), tsvReader.GetFieldByName(ColumnCaptions.PrecursorMz));
            var csvFilePath = TestContext.GetTestPath("DocumentGridExportTest.csv");
            RunUI(()=>documentGrid.NavBar.ViewContext.ExportToFile(documentGrid.NavBar, documentGrid.BindingListSource, csvFilePath, TextUtil.CsvSeparator));
            var csvReader = new CsvFileReader(csvFilePath);
            Assert.IsNotNull(csvReader.ReadLine());
            Assert.AreEqual(firstPrecursor.PrecursorCharge.ToString(chargeFormat, CultureInfo.CurrentCulture), csvReader.GetFieldByName(ColumnCaptions.PrecursorCharge));
            Assert.AreEqual(firstPrecursor.PrecursorMz.ToString(precursorMzFormat, CultureInfo.CurrentCulture), csvReader.GetFieldByName(ColumnCaptions.PrecursorMz));
            csvReader.Dispose();

            const string reportName = "My Report";
            RunDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor =>
            {
                viewEditor.ChooseColumnsTab.RemoveColumns(0, viewEditor.ChooseColumnsTab.ColumnCount);
                PropertyPath ppPeptides = PropertyPath.Root.Property(nameof(SkylineDocument.Proteins)).LookupAllItems().Property(nameof(Protein.Peptides)).LookupAllItems();
                PropertyPath ppPrecursors = ppPeptides.Property(nameof(Peptide.Precursors)).LookupAllItems();
                PropertyPath ppTransitions = ppPrecursors.Property(nameof(Precursor.Transitions)).LookupAllItems();
                viewEditor.ChooseColumnsTab.AddColumn(ppTransitions.Property(nameof(Transition.ProductMz)));
                viewEditor.ChooseColumnsTab.AddColumn(ppTransitions.Property(nameof(Transition.ProductNeutralMass)));
                viewEditor.ViewName = reportName;
                viewEditor.OkDialog();
            });
            WaitForCondition(() => documentGrid.IsComplete);

            const string productMzFormat = "0.0000E+0";
            const string productNeutralMassFormat = "0,0.0000";
            SetFormat(documentGrid, PropertyPath.Root.Property(nameof(Transition.ProductMz)), productMzFormat);
            SetFormat(documentGrid, PropertyPath.Root.Property(nameof(Transition.ProductNeutralMass)), productNeutralMassFormat);
            WaitForCondition(() => documentGrid.IsComplete);

            const string layoutName = "My Layout";
            RunDlg<NameLayoutForm>(()=>documentGrid.NavBar.RememberCurrentLayout(), nameLayoutForm =>
            {
                nameLayoutForm.LayoutName = layoutName;
                nameLayoutForm.MakeDefault = true;
                nameLayoutForm.OkDialog();
            });

            RunDlg<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog, exportReportDlg=>
            {
                Assert.IsFalse(exportReportDlg.InvariantLanguage);
                exportReportDlg.ReportName = reportName;
                exportReportDlg.OkDialog(csvFilePath, TextUtil.CsvSeparator);
            });
            var firstTransition = firstPrecursor.Transitions.First();
            csvReader = new CsvFileReader(csvFilePath);
            Assert.IsNotNull(csvReader.ReadLine());
            Assert.AreEqual(SequenceMassCalc.PersistentMZ(firstTransition.Mz).ToString(productMzFormat, CultureInfo.CurrentCulture), csvReader.GetFieldByName(ColumnCaptions.ProductMz));
            Assert.AreEqual(firstTransition.GetMoleculePersistentNeutralMass().ToString(productNeutralMassFormat, CultureInfo.CurrentCulture), csvReader.GetFieldByName(ColumnCaptions.ProductNeutralMass));
            csvReader.Dispose();
            RunDlg<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog, exportReportDlg =>
            {
                exportReportDlg.SetUseInvariantLanguage(true);
                exportReportDlg.ReportName = reportName;
                exportReportDlg.OkDialog(csvFilePath, TextUtil.CsvSeparator);
            });
            csvReader = new CsvFileReader(csvFilePath);
            Assert.IsNotNull(csvReader.ReadLine());

            // When outputting as the "Invariant" format, numbers are always formatted using "Round Trip" format.
            AssertEx.AreEqual(SequenceMassCalc.PersistentMZ(firstTransition.Mz).ToString(Formats.RoundTrip, CultureInfo.InvariantCulture), 
                csvReader.GetFieldByName(nameof(Transition.ProductMz)));
            AssertEx.AreEqual(firstTransition.GetMoleculePersistentNeutralMass().ToString(Formats.RoundTrip, CultureInfo.InvariantCulture), 
                csvReader.GetFieldByName(nameof(Transition.ProductNeutralMass)));
            csvReader.Dispose();
        }

        private void SetFormat(DataboundGridForm gridForm, PropertyPath propertyPath, string format)
        {
            RunDlg<ChooseFormatDlg>(
                () => gridForm.DataboundGridControl.ShowFormatDialog(gridForm.FindColumn(propertyPath)),
                chooseFormatDlg =>
                {
                    chooseFormatDlg.FormatText = format;
                    chooseFormatDlg.DialogResult = DialogResult.OK;
                });
        }
    }
}
