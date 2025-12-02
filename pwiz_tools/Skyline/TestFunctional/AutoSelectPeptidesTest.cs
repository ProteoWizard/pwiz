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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that operations such as Associate Proteins or deleting from the Document Grid
    /// set to false "Auto Manage Children" on the PeptideGroupDocNode.
    /// </summary>
    [TestClass]
    public class AutoSelectPeptidesTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAutoSelectPeptides()
        {
            TestFilesZip = @"TestFunctional\AutoSelectPeptidesTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Add one peptide to the document
            RunUI(()=>SkylineWindow.Paste("EEILSEMK"));
            Assert.AreEqual(1, SkylineWindow.Document.MoleculeGroupCount);
            Assert.AreEqual(1, SkylineWindow.Document.MoleculeCount);
            Assert.IsTrue(SkylineWindow.Document.MoleculeGroups.First().AutoManageChildren);

            // Add the Oxidation (M) variable modification
            var oxidation = UniMod.GetModification("Oxidation (M)", true);
            Assert.IsNotNull(oxidation);
            RunLongDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                RunUI(() => peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Modifications);
                RunLongDlg<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(peptideSettingsUi.EditStaticMods,
                    editListDlg =>
                    {
                        RunDlg<EditStaticModDlg>(editListDlg.AddItem, editStaticModDlg =>
                        {
                            editStaticModDlg.Modification = oxidation;
                            editStaticModDlg.OkDialog();
                        });
                    }, editListDlg =>editListDlg.OkDialog());
                RunUI(() =>
                {
                    peptideSettingsUi.PickedStaticMods =
                        peptideSettingsUi.PickedStaticMods.Append(oxidation.Name).ToArray();
                });
            }, peptideSettingsUi=>peptideSettingsUi.OkDialog());

            // The oxidized peptide should have been added to the Peptide List
            Assert.AreEqual(1, SkylineWindow.Document.MoleculeGroupCount);
            Assert.IsTrue(SkylineWindow.Document.MoleculeGroups.First().AutoManageChildren);
            Assert.IsTrue(SkylineWindow.Document.MoleculeGroups.First().IsPeptideList);
            Assert.AreEqual(2, SkylineWindow.Document.MoleculeCount);

            // Use the document grid to delete the unmodified peptide from the document
            DocumentGridForm documentGridForm = null;
            RunUI(() =>
            {
                SkylineWindow.ShowDocumentGrid(true);
                documentGridForm = FindOpenForm<DocumentGridForm>();
                Assert.IsNotNull(documentGridForm);
                documentGridForm.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Peptides);
            });
            WaitForCondition(() => documentGridForm.IsComplete);
            RunUI(()=>{
                Assert.AreEqual(2, documentGridForm.RowCount);
                documentGridForm.DataGridView.CurrentCell = documentGridForm.DataGridView.Rows[0].Cells[0];
            });
            RunDlg<MultiButtonMsgDlg>(() => documentGridForm.NavBar.ViewContext.Delete(),
                alertDlg => alertDlg.ClickOk());
            Assert.AreEqual(1, SkylineWindow.Document.MoleculeCount);
            Assert.IsFalse(SkylineWindow.Document.MoleculeGroups.First().AutoManageChildren);
            
            // Use the Associate Proteins dialog to move the peptide into Protein
            RunLongDlg<AssociateProteinsDlg>(SkylineWindow.ShowAssociateProteinsDlg, associateProteinsDlg =>
            {
                RunUI(()=>associateProteinsDlg.FastaFileName = TestFilesDir.GetTestPath("OneProtein.fasta"));
                WaitForCondition(() => associateProteinsDlg.IsComplete);
            }, associateProteinsDlg=>associateProteinsDlg.OkDialog());
            Assert.AreEqual(1, SkylineWindow.Document.MoleculeGroupCount);
            Assert.IsFalse(SkylineWindow.Document.MoleculeGroups.First().AutoManageChildren);
            Assert.IsFalse(SkylineWindow.Document.MoleculeGroups.First().IsPeptideList);

            // Use the Associate Proteins dialog with an unrelated FASTA file to move the peptide into a peptide list
            RunLongDlg<AssociateProteinsDlg>(SkylineWindow.ShowAssociateProteinsDlg, associateProteinsDlg =>
            {
                RunUI(() => associateProteinsDlg.FastaFileName = TestFilesDir.GetTestPath("UnrelatedProteins.fasta"));
                WaitForCondition(() => associateProteinsDlg.IsComplete);
            }, associateProteinsDlg => associateProteinsDlg.OkDialog());
            Assert.AreEqual(1, SkylineWindow.Document.MoleculeGroupCount);
            Assert.IsTrue(SkylineWindow.Document.MoleculeGroups.First().IsPeptideList);

            // Use the "Auto Select All Peptides" checkbox on the Refine Advanced dialog to add back the unmodified peptide
            RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, refineDlg =>
            {
                refineDlg.AutoPeptides = true;
                refineDlg.OkDialog();
            });
            Assert.IsTrue(SkylineWindow.Document.MoleculeGroups.First().AutoManageChildren);
            Assert.AreEqual(2, SkylineWindow.Document.MoleculeCount);

            // Use the Associate Proteins dialog to move the peptides into three separate proteins
            RunLongDlg<AssociateProteinsDlg>(SkylineWindow.ShowAssociateProteinsDlg, associateProteinsDlg =>
            {
                RunUI(()=>associateProteinsDlg.FastaFileName = TestFilesDir.GetTestPath("ThreeProteins.fasta"));
                WaitForCondition(() => associateProteinsDlg.IsComplete);
            }, associateProteinsDlg=>associateProteinsDlg.OkDialog());
            Assert.AreEqual(3, SkylineWindow.Document.MoleculeGroupCount);

            // Use the Associate Proteins dialog to move the peptides into one protein group
            RunLongDlg<AssociateProteinsDlg>(SkylineWindow.ShowAssociateProteinsDlg, associateProteinsDlg =>
            {
                RunUI(() =>
                {
                    associateProteinsDlg.FastaFileName = TestFilesDir.GetTestPath("ThreeProteins.fasta");
                    associateProteinsDlg.GroupProteins = true;
                });
                WaitForCondition(() => associateProteinsDlg.IsComplete);
            }, associateProteinsDlg => associateProteinsDlg.OkDialog());
            Assert.AreEqual(1, SkylineWindow.Document.MoleculeGroupCount);
            Assert.IsFalse(SkylineWindow.Document.MoleculeGroups.First().AutoManageChildren);
            var proteinMetadata = SkylineWindow.Document.MoleculeGroups.First().ProteinMetadata;
            Assert.IsInstanceOfType(proteinMetadata, typeof(ProteinGroupMetadata));
            var proteinGroupMetadata = (ProteinGroupMetadata)proteinMetadata;
            Assert.AreEqual(3, proteinGroupMetadata.ProteinMetadataList.Count);
        }
    }
}
