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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that removing a heavy modification from the peptide settings correctly removes it from
    /// all peptides that are using it.
    /// </summary>
    [TestClass]
    public class RemoveHeavyModificationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRemoveHeavyModification()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var heavyModification = UniMod.GetModification("Label:13C(6)15N(2) (C-term K)", false);
            Assert.IsNotNull(heavyModification);

            // Insert the peptide "ELVISK"
            RunUI(()=>
            {
                SkylineWindow.Paste("ELVISK");
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Molecules, 0);
            });

            // Add an explicit heavy modification to the peptide settings
            RunLongDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                RunUI(() => { peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Modifications; });
                RunDlg<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(peptideSettingsUi.EditHeavyMods,
                    editMods =>
                    {
                        editMods.AddItem(heavyModification);
                        editMods.OkDialog();
                    });
                OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            });

            // Apply a heavy modification to the last amino acid on the peptide
            RunDlg<EditPepModsDlg>(SkylineWindow.ModifyPeptide, editPepModsDlg =>
            {
                editPepModsDlg.SetModification(5, IsotopeLabelType.heavy, heavyModification.Name);
                editPepModsDlg.OkDialog();
            });
            
            // Generate decoys
            RunDlg<GenerateDecoysDlg>(() => SkylineWindow.ShowGenerateDecoysDlg(), decoysDlg =>
            {
                decoysDlg.OkDialog();
            });
            Assert.AreEqual(2, SkylineWindow.Document.MoleculeCount);
            Assert.AreEqual(4, SkylineWindow.Document.MoleculeTransitionGroupCount);

            // Remove the explicit heavy modification from the peptide settings
            RunLongDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                RunUI(() => { peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Modifications; });
                RunDlg<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(peptideSettingsUi.EditHeavyMods,
                    editMods =>
                    {
                        editMods.SelectItem(heavyModification.Name);
                        editMods.RemoveItem();
                        editMods.OkDialog();
                    });
                OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            });

            Assert.AreEqual(2, SkylineWindow.Document.MoleculeCount);
            Assert.AreEqual(2, SkylineWindow.Document.MoleculeTransitionGroupCount);

            // Make sure that the document can be saved and reloaded. This will verify that there
            // are no errors about undeclared explicit modifications
            AssertEx.Serializable(SkylineWindow.Document);
        }
    }
}
