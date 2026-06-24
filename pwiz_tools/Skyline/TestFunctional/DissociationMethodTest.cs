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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Databinding.AuditLog;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;
using System.Globalization;
using System.Linq;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class DissociationMethodTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestDissociationMethod()
        {
            TestFilesZip = @"TestFunctional\DissociationMethodTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("DissociationMethodTest.sky"));
            });
            RunUI(() =>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 1);
            });
            RunDlg<EditSpectrumFilterDlg>(SkylineWindow.EditMenu.EditSpectrumFilter, dlg =>
            {
                var row = dlg.RowBindingList.AddNew();
                Assert.IsNotNull(row);
                row.Property = SpectrumClassColumn.DissociationMethod.GetLocalizedColumnName(CultureInfo.CurrentCulture);
                row.SetOperation(FilterOperations.OP_EQUALS);
                row.SetValue("CID");
                dlg.OkDialog();
            });
            RunUI(() =>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 2);
            });
            RunDlg<EditSpectrumFilterDlg>(SkylineWindow.EditMenu.EditSpectrumFilter, dlg =>
            {
                var row = dlg.RowBindingList.AddNew();
                Assert.IsNotNull(row);
                row.Property =
                    SpectrumClassColumn.DissociationMethod.GetLocalizedColumnName(CultureInfo.CurrentCulture);
                row.SetOperation(FilterOperations.OP_EQUALS);
                row.SetValue("HCD");
                dlg.OkDialog();
            });

            // A multi-criterion filter (HCD spectra are MS2, so this stays non-empty)
            RunUI(() =>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 3);
            });
            RunDlg<EditSpectrumFilterDlg>(SkylineWindow.EditMenu.EditSpectrumFilter, dlg =>
            {
                var row1 = dlg.RowBindingList.AddNew();
                Assert.IsNotNull(row1);
                row1.Property = SpectrumClassColumn.DissociationMethod.GetLocalizedColumnName(CultureInfo.CurrentCulture);
                row1.SetOperation(FilterOperations.OP_EQUALS);
                row1.SetValue("HCD");
                var row2 = dlg.RowBindingList.AddNew();
                Assert.IsNotNull(row2);
                row2.Property = SpectrumClassColumn.MsLevel.GetLocalizedColumnName(CultureInfo.CurrentCulture);
                row2.SetOperation(FilterOperations.OP_EQUALS);
                row2.SetValue("2");
                dlg.OkDialog();
            });

            // The multi-criterion filter on molecule 3 references both properties
            var molecules = SkylineWindow.Document.Molecules.ToArray();
            var mol3FilterText = molecules[3].TransitionGroups.First().SpectrumClassFilter.ToFilterString();
            AssertEx.Contains(mol3FilterText, nameof(SpectrumClass.DissociationMethod));
            AssertEx.Contains(mol3FilterText, nameof(SpectrumClass.MsLevel));

            // The document grid uses the custom Spectrum Filter column and shows the applied filter text
            var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
            RunUI(() => documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Precursors));
            WaitForConditionUI(() => documentGrid.IsComplete);
            RunDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor =>
            {
                Assert.IsTrue(viewEditor.ChooseColumnsTab.TrySelect(
                    PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.SpectrumFilter")));
                viewEditor.ChooseColumnsTab.AddSelectedColumn();
                viewEditor.ViewName = "SpectrumFilterTest";
                viewEditor.OkDialog();
            });
            // The SpectrumFilter property is rendered with our custom column type
            WaitForConditionUI(() => documentGrid.IsComplete && documentGrid.DataGridView.Columns
                .Cast<DataGridViewColumn>().Any(col => col is SpectrumFilterDataGridViewColumn));
            var expectedFilters = new[] { 1, 2, 3 }
                .Select(i => molecules[i].TransitionGroups.First().SpectrumClassFilter.ToFilterString())
                .ToHashSet();
            var actualFilters = new HashSet<string>();
            RunUI(() =>
            {
                var spectrumFilterColumn = documentGrid.DataGridView.Columns
                    .Cast<DataGridViewColumn>().First(col => col is SpectrumFilterDataGridViewColumn);
                foreach (DataGridViewRow gridRow in documentGrid.DataGridView.Rows)
                {
                    if (gridRow.Cells[spectrumFilterColumn.Index].Value is string value && !string.IsNullOrEmpty(value))
                    {
                        actualFilters.Add(value);
                    }
                }
            });
            AssertEx.IsTrue(expectedFilters.IsSubsetOf(actualFilters));
            RunUI(() => documentGrid.Close());

            // Regression: the Spectrum Filter edit icon must also work when the grid is rooted below
            // the Precursor level (e.g. a transition list), where each row's entity is a Transition
            // rather than a Precursor. The icon used to be inoperative in such views because the click
            // handler only recognized a Precursor row value.
            var transitionGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
            RunUI(() => transitionGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Transitions));
            WaitForConditionUI(() => transitionGrid.IsComplete);
            RunDlg<ViewEditor>(transitionGrid.NavBar.CustomizeView, viewEditor =>
            {
                Assert.IsTrue(viewEditor.ChooseColumnsTab.TrySelect(
                    PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.SpectrumFilter")));
                viewEditor.ChooseColumnsTab.AddSelectedColumn();
                viewEditor.ViewName = "SpectrumFilterTransitionTest";
                viewEditor.OkDialog();
            });
            WaitForConditionUI(() => transitionGrid.IsComplete && transitionGrid.DataGridView.Columns
                .Cast<DataGridViewColumn>().Any(col => col is SpectrumFilterDataGridViewColumn));

            // Find a transition row whose precursor has no filter yet, so the edit is unambiguous
            int editRowIndex = -1;
            IdentityPath editPrecursorPath = null;
            DataGridViewColumn transitionFilterColumn = null;
            RunUI(() =>
            {
                transitionFilterColumn = transitionGrid.DataGridView.Columns
                    .Cast<DataGridViewColumn>().First(col => col is SpectrumFilterDataGridViewColumn);
                var bindingListSource = transitionGrid.BindingListSource;
                for (int i = 0; i < bindingListSource.Count; i++)
                {
                    if (bindingListSource[i] is RowItem rowItem &&
                        rowItem.Value is pwiz.Skyline.Model.Databinding.Entities.Transition transition &&
                        string.IsNullOrEmpty(transition.Precursor.SpectrumFilter))
                    {
                        editRowIndex = i;
                        editPrecursorPath = transition.Precursor.IdentityPath;
                        transitionGrid.DataGridView.CurrentCell =
                            transitionGrid.DataGridView.Rows[i].Cells[transitionFilterColumn.Index];
                        break;
                    }
                }
            });
            Assert.AreNotEqual(-1, editRowIndex, @"expected a transition whose precursor has no spectrum filter");

            var docBeforeGridEdit = SkylineWindow.Document;
            var editFilterDlg = ShowDialog<EditSpectrumFilterDlg>(() =>
                ((TextImageCell)transitionGrid.DataGridView.Rows[editRowIndex].Cells[transitionFilterColumn.Index])
                .ClickImage(0));
            RunUI(() =>
            {
                var row = editFilterDlg.RowBindingList.AddNew();
                Assert.IsNotNull(row);
                row.Property = SpectrumClassColumn.MsLevel.GetLocalizedColumnName(CultureInfo.CurrentCulture);
                row.SetOperation(FilterOperations.OP_EQUALS);
                row.SetValue("1");
            });
            OkDialog(editFilterDlg, editFilterDlg.OkDialog);
            WaitForDocumentChange(docBeforeGridEdit);
            var docAfterGridEdit = SkylineWindow.Document;

            // The filter set through the transition-rooted grid landed on the clicked row's precursor
            RunUI(() =>
            {
                var editedGroup = (TransitionGroupDocNode)SkylineWindow.Document.FindNode(editPrecursorPath);
                var editedFilterText = editedGroup.SpectrumClassFilter.ToFilterString();
                AssertEx.IsFalse(string.IsNullOrEmpty(editedFilterText));
                AssertEx.Contains(editedFilterText, nameof(SpectrumClass.MsLevel));
            });
            RunUI(() => transitionGrid.Close());

            // Undo the grid edit so the chromatogram checks below run against the original filters
            RunUI(() => SkylineWindow.Undo());
            WaitForDocumentChange(docAfterGridEdit);
            RunUI(() => AssertEx.IsTrue(string.IsNullOrEmpty(
                ((TransitionGroupDocNode)SkylineWindow.Document.FindNode(editPrecursorPath))
                .SpectrumClassFilter.ToFilterString())));

            ImportResultsFile(TestFilesDir.GetTestPath("DissociationMethodTest.mzML"));
            var chromatogramPointCounts = new List<int>();
            var document = SkylineWindow.Document;
            var tolerance = (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            foreach (var molecule in document.Molecules)
            {
                Assert.IsTrue(document.MeasuredResults.TryLoadChromatogram(0, molecule, molecule.TransitionGroups.First(), tolerance,
                    out var chromatogramGroupInfos));
                Assert.AreEqual(1, chromatogramGroupInfos.Length);
                var numPoints = chromatogramGroupInfos[0].TimeIntensitiesGroup.TransitionTimeIntensities[0].NumPoints;
                Assert.AreNotEqual(0, numPoints);
                chromatogramPointCounts.Add(numPoints);
            }
            Assert.AreEqual(chromatogramPointCounts[0], chromatogramPointCounts[1] + chromatogramPointCounts[2]);
        }
    }
}
