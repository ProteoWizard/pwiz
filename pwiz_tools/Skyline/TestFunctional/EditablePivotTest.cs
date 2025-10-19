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

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class EditablePivotTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestEditablePivot()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.Filter;
                transitionSettingsUi.PrecursorCharges = string.Join(TextUtil.SEPARATOR_CSV.ToString(), new[] { 1, 2 });
                transitionSettingsUi.RangeFrom = TransitionFilter.StartFragmentFinder.ION_1.ToString();
                transitionSettingsUi.OkDialog();
            });
            DocumentGridForm documentGrid = null;
            RunUI(()=>
            {
                SkylineWindow.Paste(TextUtil.LineSeparate("ELVIS", "LIVES", "ELVISLIVES"));
                SkylineWindow.ShowDocumentGrid(true);
                documentGrid = FindOpenForm<DocumentGridForm>();
                Assert.IsNotNull(documentGrid);
                documentGrid.DataboundGridControl.ChooseView(ViewGroup.BUILT_IN.Id.ViewName(Resources.SkylineViewContext_GetDocumentGridRowSources_Precursors));
            });
            WaitForCondition(() => documentGrid.IsComplete);

            // Test aggregate operations "Count Distinct" and "Median"
            RunDlg<PivotEditor>(()=>documentGrid.NavBar.ShowPivotDialog(false), pivotEditor =>
            {
                SelectPropertyPaths(pivotEditor,
                    PropertyPath.Root,
                    PropertyPath.Root.Property(nameof(Precursor.Peptide)),
                    PropertyPath.Root.Property(nameof(Precursor.Charge)),
                    PropertyPath.Root.Property(nameof(Precursor.IsotopeLabelType)),
                    PropertyPath.Root.Property(nameof(Precursor.Mz))
                );
                pivotEditor.SelectAggregateOperation(AggregateOperation.Count);
                pivotEditor.AddValue();
                pivotEditor.SelectAggregateOperation(AggregateOperation.CountDistinct);
                pivotEditor.AddValue();
                SelectPropertyPaths(pivotEditor, PropertyPath.Root.Property(nameof(Precursor.Charge)),
                    PropertyPath.Root.Property(nameof(Precursor.Mz)));
                pivotEditor.SelectAggregateOperation(AggregateOperation.Median);
                pivotEditor.AddValue();
                pivotEditor.OkDialog();
            });
            WaitForCondition(() => documentGrid.IsComplete);
            Assert.AreEqual(1, documentGrid.RowCount);
            var medianPrecursorMz = new Statistics(SkylineWindow.Document.MoleculePrecursorPairs
                .Select(p => SequenceMassCalc.PersistentMZ(p.NodeGroup.PrecursorMz.RawValue))).Median();
            var expectedValues = new object[]
            {
                6, 6, 6, 6, 6, // count precursor, peptide, charge, isotope label type, mz
                6, // count distinct precursor
                3, // count distinct peptide
                2, // count distinct charge
                1, // count distinct isotope label type
                4, // count distinct mz
                1.5, // median charge
                medianPrecursorMz
            };
            RunUI(() =>
            {
                var dataGridView = documentGrid.DataGridView;
                Assert.AreEqual(expectedValues.Length, documentGrid.ColumnCount);
                for (int i = 0; i < expectedValues.Length; i++)
                {
                    var value = dataGridView.Rows[0].Cells[i].Value;
                    Assert.AreEqual(expectedValues[i], value, "Mismatch in column {0} at position {1}", dataGridView.Columns[i].HeaderText, i);
                }

                documentGrid.DataboundGridControl.ChooseView(
                    ViewGroup.BUILT_IN.Id.ViewName(Resources.SkylineViewContext_GetDocumentGridRowSources_Transitions));
            });
            WaitForCondition(() => documentGrid.IsComplete);
            RunDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor =>
            {
                viewEditor.ViewName = "Editable Pivot Test";
                var ppProteins = PropertyPath.Root.Property(nameof(SkylineDocument.Proteins)).LookupAllItems();
                var ppPeptides = ppProteins.Property(nameof(Protein.Peptides)).LookupAllItems();
                var ppPrecursors = ppPeptides.Property(nameof(Skyline.Model.Databinding.Entities.Peptide.Precursors)).LookupAllItems();
                //var ppTransitions = ppPrecursors.Property(nameof(Precursor.Transitions)).LookupAllItems();
                viewEditor.ChooseColumnsTab.RemoveColumns(0, viewEditor.ChooseColumnsTab.ColumnCount);
                foreach (var pp in new[]
                         {
                             ppPeptides,
                             ppPeptides.Property(nameof(Skyline.Model.Databinding.Entities.Peptide.Note)),
                             ppPeptides.Property(nameof(Skyline.Model.Databinding.Entities.Peptide.AutoSelectPrecursors)),
                             ppPrecursors.Property(nameof(Precursor.Mz)),
                         })
                {
                    viewEditor.ChooseColumnsTab.AddColumn(pp);
                }
                viewEditor.OkDialog();
            });
            WaitForCondition(() => documentGrid.IsComplete);

            // Create a pivot which groups on "Peptide" and make sure that Peptide's columns "Note" and "AutoSelectPrecursors" are both editable
            RunDlg<PivotEditor>(()=>documentGrid.NavBar.ShowPivotDialog(false), pivotEditor =>
            {
                var ppPeptide = PropertyPath.Root.Property(nameof(Precursor.Peptide));
                SelectPropertyPaths(pivotEditor,

                    ppPeptide,
                    ppPeptide.Property(nameof(Skyline.Model.Databinding.Entities.Peptide.Note)),
                    ppPeptide.Property(nameof(Skyline.Model.Databinding.Entities.Peptide.AutoSelectPrecursors))
                );
                pivotEditor.AddRowHeader();
                SelectPropertyPaths(pivotEditor, PropertyPath.Root.Property(nameof(Precursor.Mz)));
                pivotEditor.SelectAggregateOperation(AggregateOperation.Median);
                pivotEditor.AddValue();
                pivotEditor.OkDialog();
            });
            WaitForCondition(() => documentGrid.IsComplete);

            // Paste some values into the "Note" and "AutoSelectPrecursors" columns
            RunUI(() =>
            {
                var dataGridView = documentGrid.DataGridView;
                Assert.AreEqual(3, documentGrid.RowCount);
                SetClipboardText(TextUtil.LineSeparate("Peptide1\ttrue", "Peptide2\tfalse", "Peptide3\ttrue"));
                dataGridView.CurrentCell = dataGridView.Rows[0].Cells[1];
                dataGridView.SendPaste();
                var peptides = SkylineWindow.Document.Peptides.ToList();
                Assert.AreEqual("Peptide1", peptides[0].Note);
                Assert.IsTrue(peptides[0].AutoManageChildren);
                Assert.AreEqual("Peptide2", peptides[1].Note);
                Assert.IsFalse(peptides[1].AutoManageChildren);
                Assert.AreEqual("Peptide3", peptides[2].Note);
                Assert.IsTrue(peptides[2].AutoManageChildren);
            });
        }

        private void SelectPropertyPaths(PivotEditor pivotEditor, params PropertyPath[] properties)
        {
            var propertyPathIndexes = pivotEditor.AvailableProperties
                .Select((prop, index) => Tuple.Create((prop as ColumnPropertyDescriptor)?.PropertyPath, index))
                .Where(tuple => tuple.Item1 != null).ToDictionary(tuple => tuple.Item1, tuple => tuple.Item2);
            pivotEditor.AvailableColumnList.SelectedIndices.Clear();
            foreach (var propertyPath in properties)
            {
                Assert.IsTrue(propertyPathIndexes.TryGetValue(propertyPath, out var index), "Unable to find property {0}", propertyPath);
                pivotEditor.AvailableColumnList.SelectedIndices.Add(index);
            }
        }
    }
}
