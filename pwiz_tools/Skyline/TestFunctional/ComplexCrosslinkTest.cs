/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ComplexCrosslinkTest : AbstractFunctionalTest
    {
        // ReSharper disable IdentifierTypo
        public const string DISULFIDE = "disulfide";
        public const string PHOSPHO_MOD_NAME = "Phospho (ST)";
        public const string HEAVY_CHAIN = "GPSVFPLAPCSR";
        public const string LIGHT_CHAIN = "SFNRGEC";
        public const string HINGE = "CCVECPPCPAPPVAGPSVFLFPPKPK";
        // ReSharper restore IdentifierTypo
        [TestMethod]
        public void TestComplexCrosslinks()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Modifications;
            });
            var editModListDlg = ShowEditStaticModsDlg(peptideSettingsUi);
            var editStaticModDlg = ShowDialog<EditStaticModDlg>(editModListDlg.AddItem);
            RunUI(() => {
                {
                    editStaticModDlg.Modification = new StaticMod(DISULFIDE, "C", null, "-H2");
                    editStaticModDlg.IsCrosslinker = true;
                }
            });

            OkDialog(editStaticModDlg, editStaticModDlg.OkDialog);
            OkDialog(editModListDlg, editModListDlg.OkDialog);
            AddStaticMod(new StaticMod(PHOSPHO_MOD_NAME, "S, T", null, "HPO3")
                .ChangeLosses(new[] {new FragmentLoss("H3PO4")}), peptideSettingsUi);
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            RunDlg<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg, pasteDlg =>
            {
                SetClipboardText(string.Join(Environment.NewLine, HEAVY_CHAIN, LIGHT_CHAIN, HINGE));
                pasteDlg.PastePeptides();
                pasteDlg.OkDialog();
            });
            RunUI(()=>SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Molecules, 0));
            var editPepModsDlg = ShowDialog<EditPepModsDlg>(SkylineWindow.ModifyPeptide);
            var linkedPeptidesDlg = ShowDialog<EditLinkedPeptidesDlg>(
                () => editPepModsDlg.SetModification(9, IsotopeLabelType.light, DISULFIDE));
            var peptidesGridTester = new GridTester(linkedPeptidesDlg.LinkedPeptidesGrid);
            peptidesGridTester.SetCellValue(0, linkedPeptidesDlg.PeptideSequenceColumn, LIGHT_CHAIN);
            peptidesGridTester.SetCellValue(1, linkedPeptidesDlg.PeptideSequenceColumn, HINGE);
            peptidesGridTester.SetCellValue(2, linkedPeptidesDlg.PeptideSequenceColumn, HINGE);
            peptidesGridTester.SetCellValue(3, linkedPeptidesDlg.PeptideSequenceColumn, LIGHT_CHAIN);
            peptidesGridTester.SetCellValue(4, linkedPeptidesDlg.PeptideSequenceColumn, HEAVY_CHAIN);
            peptidesGridTester.MoveToCell(0, 0);
            RunUI(() =>
            {
                var firstRow = linkedPeptidesDlg.CrosslinksGrid.Rows[0];
                Assert.AreEqual(DISULFIDE, firstRow.Cells[linkedPeptidesDlg.CrosslinkerColumn.Index].Value);
                Assert.AreEqual(0, firstRow.Cells[linkedPeptidesDlg.GetPeptideIndexColumn(0).Index].Value);
                Assert.AreEqual(9, firstRow.Cells[linkedPeptidesDlg.GetAaIndexColumn(0).Index].Value);
            });
            SetCrosslinker(linkedPeptidesDlg, 0, 0, 9, 2, 1); // Link from GPSVFPLAPC*SR - CC*VECPPCPAPPVAGPSVFLFPPKPK
            var nestedModsDlg  = ShowNestedDlg<EditPepModsDlg>(()=>linkedPeptidesDlg.EditLinkedModifications(1));
            Assert.IsFalse(nestedModsDlg.GetComboBox(IsotopeLabelType.light, 1).Enabled);
            OkDialog(nestedModsDlg, nestedModsDlg.OkDialog);
            SetCrosslinker(linkedPeptidesDlg, 1, 1, 6, 2, 0); // Link from SFNRGEC* - C*CVECPPCPAPPVAGPSVFLFPPKPK
            SetCrosslinker(linkedPeptidesDlg, 2, 2, 4, 3, 4); // Link between both CCVEC*PPCPAPPVAGPSVFLFPPKPK
            SetCrosslinker(linkedPeptidesDlg, 3, 2, 7, 3, 7); // Link between both CCVECPPC*PAPPVAGPSVFLFPPKPK
            SetCrosslinker(linkedPeptidesDlg, 4, 3, 0, 4, 6); // Link from C*CVECPPCPAPPVAGPSVFLFPPKPK - SFNRGEC*
            // Try to OK this dialog, but get an error about not all peptides being connected
            RunDlg<AlertDlg>(() => linkedPeptidesDlg.OkDialog(), alertDlg =>
            {
                Assert.AreEqual(Resources.EditLinkedPeptidesDlg_OkDialog_Some_crosslinked_peptides_are_not_connected_,
                    alertDlg.Message);
                alertDlg.OkDialog();
            });
            SetCrosslinker(linkedPeptidesDlg, 5, 3, 1, 5, 9);// Link from CC*VECPPCPAPPVAGPSVFLFPPKPK - GPSVFPLAPC*SR
            OkDialog(linkedPeptidesDlg, linkedPeptidesDlg.OkDialog);
            OkDialog(editPepModsDlg, editPepModsDlg.OkDialog);
        }

        public void SetCrosslinker(EditLinkedPeptidesDlg dlg, int rowIndex, int peptide1, int aaIndex1, int peptide2, int aaIndex2)
        {
            CrosslinkingTest.SetCrosslinker(dlg, rowIndex, DISULFIDE, new CrosslinkSite(peptide1, aaIndex1), new CrosslinkSite(peptide2, aaIndex2));
        }
    }
}
