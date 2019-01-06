/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests changing "AutoSelectPeptides", "AutoSelectPrecursors", and "AutoSelectTransitions" in the Document Grid.
    /// </summary>
    [TestClass]
    public class AutoSelectTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAutoSelect()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunDlg<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg, pasteDlg=>
            {
                SetClipboardText(TextUtil.LineSeparate("ELVIS\tProtein1", "LIVES\tProtein2"));
                pasteDlg.PastePeptides();
                pasteDlg.OkDialog();
            });
            RunUI(()=>SkylineWindow.ShowDocumentGrid(true));

            VerifyAutoSelectPeptides();
            VerifyAutoSelectPrecursors();
            VerifyAutoSelectTransitions();
        }

        private void VerifyAutoSelectPeptides()
        {
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() => documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Proteins));
            RunDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor =>
            {
                viewEditor.ChooseColumnsTab.AddColumn(PropertyPath.Root.Property(nameof(SkylineDocument.Proteins))
                    .LookupAllItems().Property(nameof(Protein.AutoSelectPeptides)));
                viewEditor.ViewName = "MyProteins";
                viewEditor.OkDialog();
            });
            WaitForConditionUI(() => documentGrid.IsComplete);
            Assert.AreEqual(2, documentGrid.RowCount);

            // Turn Auto Manage Children on for the first PeptideGroup, and off for the second PeptideGroup
            RunUI(() =>
            {
                var dataGridView = documentGrid.DataGridView;
                var colAutoSelect =
                    documentGrid.FindColumn(PropertyPath.Root.Property(nameof(Protein.AutoSelectPeptides)));
                dataGridView.CurrentCell = dataGridView.Rows[0].Cells[colAutoSelect.Index];
                SetClipboardText(TextUtil.LineSeparate(true.ToString(), false.ToString()));
                dataGridView.SendPaste();
            });
            CollectionAssert.AreEqual(new[] { true, false },
                SkylineWindow.Document.MoleculeGroups.Select(m => m.AutoManageChildren).ToArray());

            // Add a variable modification which will cause the modified variants to get added to the PeptideGroup that has AutoManageChildren turned on
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Modifications;
            });
            const string phosphoModName = "Phospho (ST)";
            AddStaticMod(phosphoModName, true, peptideSettingsUi);
            RunUI(() => peptideSettingsUi.PickedStaticMods =
                peptideSettingsUi.PickedStaticMods.Append(phosphoModName).ToArray());
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            Assert.AreEqual(2, SkylineWindow.Document.MoleculeGroups.First().Children.Count);
            Assert.AreEqual(1, SkylineWindow.Document.MoleculeGroups.Skip(1).First().Children.Count);

            // Now, set the auto manage children on the second PeptideGroup, and make sure it gets the expected new child
            RunUI(() =>
            {
                var dataGridView = documentGrid.DataGridView;
                var colAutoSelect =
                    documentGrid.FindColumn(PropertyPath.Root.Property(nameof(Protein.AutoSelectPeptides)));
                dataGridView.CurrentCell = dataGridView.Rows[0].Cells[colAutoSelect.Index];
                SetClipboardText(TextUtil.LineSeparate(true.ToString(), true.ToString()));
                dataGridView.SendPaste();
            });
            Assert.AreEqual(2, SkylineWindow.Document.MoleculeGroups.Skip(1).First().Children.Count);

        }

        private void VerifyAutoSelectPrecursors()
        {
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() => documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Peptides));
            RunDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor =>
            {
                viewEditor.ChooseColumnsTab.AddColumn(PropertyPath.Root
                    .Property(nameof(SkylineDocument.Proteins)).LookupAllItems()
                    .Property(nameof(Protein.Peptides)).LookupAllItems()
                    .Property(nameof(Peptide.AutoSelectPrecursors)));
                viewEditor.ViewName = "MyPeptides";
                viewEditor.OkDialog();
            });
            WaitForConditionUI(() => documentGrid.IsComplete);
            Assert.AreEqual(4, documentGrid.RowCount);
            RunUI(() =>
            {
                var dataGridView = documentGrid.DataGridView;
                var colAutoSelect =
                    documentGrid.FindColumn(PropertyPath.Root.Property(nameof(Peptide.AutoSelectPrecursors)));
                dataGridView.CurrentCell = dataGridView.Rows[0].Cells[colAutoSelect.Index];
                SetClipboardText(string.Join(Environment.NewLine, true, false, false, true));
                dataGridView.SendPaste();
            });
            CollectionAssert.AreEqual(new[] { true, false, false, true },
                SkylineWindow.Document.Molecules.Select(m => m.AutoManageChildren).ToArray());
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI,
                transitionSettingsUi =>
                {
                    transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.Filter;
                    transitionSettingsUi.PrecursorCharges = "1, 2";
                    transitionSettingsUi.RangeFrom = Resources.TransitionFilter_FragmentStartFinders_ion_1;
                    transitionSettingsUi.RangeTo = Resources.TransitionFilter_FragmentEndFinders_3_ions;
                    transitionSettingsUi.OkDialog();
                });
            CollectionAssert.AreEqual(new[] { 2, 1, 1, 2 }, SkylineWindow.Document.Molecules.Select(m => m.Children.Count).ToArray());
            RunUI(() =>
            {
                var dataGridView = documentGrid.DataGridView;
                var colAutoSelect =
                    documentGrid.FindColumn(PropertyPath.Root.Property(nameof(Peptide.AutoSelectPrecursors)));
                dataGridView.CurrentCell = dataGridView.Rows[0].Cells[colAutoSelect.Index];
                SetClipboardText(string.Join(Environment.NewLine, true, true, true, true));
                dataGridView.SendPaste();
            });
            CollectionAssert.AreEqual(new[] { 2, 2, 2, 2 }, SkylineWindow.Document.Molecules.Select(m => m.Children.Count).ToArray());
            CollectionAssert.AreEqual(new[] {3, 3, 3, 3, 3, 3, 3, 3},
                SkylineWindow.Document.MoleculeTransitionGroups.Select(m => m.Children.Count).ToArray());
        }

        private void VerifyAutoSelectTransitions()
        {
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() => documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Precursors));
            RunDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor =>
            {
                viewEditor.ChooseColumnsTab.AddColumn(PropertyPath.Root
                    .Property(nameof(SkylineDocument.Proteins)).LookupAllItems()
                    .Property(nameof(Protein.Peptides)).LookupAllItems()
                    .Property(nameof(Peptide.Precursors)).LookupAllItems()
                    .Property(nameof(Precursor.AutoSelectTransitions)));
                viewEditor.ViewName = "MyPrecursors";
                viewEditor.OkDialog();
            });
            WaitForConditionUI(() => documentGrid.IsComplete);
            Assert.AreEqual(8, documentGrid.RowCount);
            RunUI(() =>
            {
                var dataGridView = documentGrid.DataGridView;
                var colAutoSelect =
                    documentGrid.FindColumn(PropertyPath.Root.Property(nameof(Precursor.AutoSelectTransitions)));
                dataGridView.CurrentCell = dataGridView.Rows[0].Cells[colAutoSelect.Index];
                SetClipboardText(string.Join(Environment.NewLine, true, false, true, true, false, false, false, true));
                dataGridView.SendPaste();
            });
            CollectionAssert.AreEqual(new[] { 3, 3, 3, 3, 3, 3, 3, 3 },
                SkylineWindow.Document.MoleculeTransitionGroups.Select(m => m.Children.Count).ToArray());
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI,
                transitionSettingsUi =>
                {
                    transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.Filter;
                    transitionSettingsUi.RangeTo = Resources.TransitionFilter_FragmentEndFinders_4_ions;
                    transitionSettingsUi.OkDialog();
                });
            CollectionAssert.AreEqual(new[] { 4, 3, 4, 4, 3, 3, 3, 4 },
                SkylineWindow.Document.MoleculeTransitionGroups.Select(m => m.Children.Count).ToArray());
            RunUI(() =>
            {
                var dataGridView = documentGrid.DataGridView;
                var colAutoSelect =
                    documentGrid.FindColumn(PropertyPath.Root.Property(nameof(Precursor.AutoSelectTransitions)));
                dataGridView.CurrentCell = dataGridView.Rows[0].Cells[colAutoSelect.Index];
                SetClipboardText(string.Join(Environment.NewLine, true, false, true, true, true, true, false, true));
                dataGridView.SendPaste();
            });
            CollectionAssert.AreEqual(new[] { 4, 3, 4, 4, 4, 4, 3, 4 },
                SkylineWindow.Document.MoleculeTransitionGroups.Select(m => m.Children.Count).ToArray());
        }
    }
}
