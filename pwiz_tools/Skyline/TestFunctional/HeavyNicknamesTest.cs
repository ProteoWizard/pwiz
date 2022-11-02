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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that Skyline can handle users defining their molecules or modifications using either D, H', T, or H".
    /// </summary>
    [TestClass]
    public class HeavyNicknamesTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestHeavyNicknames()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Test adding a peptide that has a modification with a "D" in its chemical formula
            const string modName = "Nethylmaleimide-2H(5)";
            var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(()=>peptideSettingsDlg.SelectedTab = PeptideSettingsUI.TABS.Modifications);
            var modsListDlg = ShowDialog<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(peptideSettingsDlg.EditHeavyMods);
            var editModDlg = ShowDialog<EditStaticModDlg>(modsListDlg.AddItem);

            RunUI(() => { editModDlg.Modification = new StaticMod(modName, "C", null, "H2D5C6NO2"); });
            OkDialog(editModDlg, editModDlg.OkDialog);
            OkDialog(modsListDlg, modsListDlg.OkDialog);
            RunUI(()=>peptideSettingsDlg.PickedHeavyMods = peptideSettingsDlg.PickedHeavyMods.Append(modName).ToArray());
            OkDialog(peptideSettingsDlg, peptideSettingsDlg.OkDialog);
            RunDlg<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg, pasteDlg =>
            {
                SetClipboardText("PEPTIDEWITHCYSTEINE\tProtein1");
                pasteDlg.PastePeptides();
                pasteDlg.OkDialog();
            });
            var peptideDocNode = SkylineWindow.Document.Molecules.FirstOrDefault();
            Assert.IsNotNull(peptideDocNode);
            Assert.AreEqual(2, peptideDocNode.TransitionGroupCount);
            Assert.AreEqual(1167.5152, peptideDocNode.TransitionGroups.First().PrecursorMz, .001);
            Assert.AreEqual(1232.5547, peptideDocNode.TransitionGroups.Last().PrecursorMz, .001);

            // Insert a molecule group with one molecule in it.
            RunUI(()=>SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.mixed));
            const string moleculeGroupName = "MyMoleculeGroup";
            
            var importDialog3 = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            
            var colDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDialog3.TransitionListText = moleculeGroupName + @",MyMolecule,MyTransition,H10O10,H10O10,1,1");

            RunUI(() => {
                colDlg.radioMolecule.PerformClick();
                colDlg.SetSelectedColumnTypes(
                    Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_List_Name,
                    Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Name,
                    Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Formula,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Charge);
            });

            OkDialog(colDlg, colDlg.OkDialog);

            var myMoleculeGroup =
                SkylineWindow.Document.MoleculeGroups.FirstOrDefault(group => group.Name == moleculeGroupName);
            //Assert.IsNotNull(myMoleculeGroup);
            const string nameHPrime = "H-prime";
            const string nameDeuterium = "Deuterium";
            const string nameHDoublePrime = "H-double-prime";
            const string nameTritium = "Tritium";

            // Add a molecule which has H' in its formula
            RunUI(() => SkylineWindow.SelectedPath = new IdentityPath(myMoleculeGroup?.Id));
            RunDlg<EditCustomMoleculeDlg>(SkylineWindow.AddSmallMolecule, dlg =>
            {
                dlg.NameText = nameHPrime;
                dlg.FormulaBox.Formula = "H'10C10[M+H]";
                dlg.OkDialog();
            });

            // Add a molecule that has D in its formula
            RunUI(() => SkylineWindow.SelectedPath = new IdentityPath(myMoleculeGroup?.Id));
            RunDlg<EditCustomMoleculeDlg>(SkylineWindow.AddSmallMolecule, dlg =>
            {
                dlg.NameText = nameDeuterium;
                dlg.FormulaBox.Formula = "D10C10[M+H]";
                dlg.OkDialog();
            });

            // Add a molecule that has H" in its formula
            RunUI(() => SkylineWindow.SelectedPath = new IdentityPath(myMoleculeGroup?.Id));
            RunDlg<EditCustomMoleculeDlg>(SkylineWindow.AddSmallMolecule, dlg =>
            {
                dlg.NameText = nameHDoublePrime;
                dlg.FormulaBox.Formula = "H\"10C10[M+H]";
                dlg.OkDialog();
            });

            // Add a molecule that has T in its formula
            RunDlg<EditCustomMoleculeDlg>(SkylineWindow.AddSmallMolecule, dlg =>
            {
                dlg.NameText = nameTritium;
                dlg.FormulaBox.Formula = "T10C10[M+H]";
                dlg.OkDialog();
            });
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUI =>
            {
                transitionSettingsUI.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                transitionSettingsUI.Peaks = 2;
                transitionSettingsUI.OkDialog();
            });

            // Verify that the molecules with H' and D have the same precursor masses
            var hPrime = SkylineWindow.Document.Molecules.FirstOrDefault(mol =>
                mol?.CustomMolecule?.Name == nameHPrime);
            var deuterium = SkylineWindow.Document.Molecules.FirstOrDefault(mol =>
                mol?.CustomMolecule?.Name == nameDeuterium);
            Assert.IsNotNull(hPrime);
            Assert.IsNotNull(deuterium);
            Assert.AreEqual(1, hPrime.TransitionGroupCount);
            Assert.AreEqual(1, deuterium.TransitionGroupCount);
            Assert.AreEqual(2, hPrime.TransitionGroups.First().TransitionCount);
            Assert.AreEqual(2, deuterium.TransitionGroups.First().TransitionCount);
            CollectionAssert.AreEqual(hPrime.TransitionGroups.First().Transitions.Select(t => t.Mz).ToList(),
                deuterium.TransitionGroups.First().Transitions.Select(t => t.Mz).ToList());

            // Verify that the molecules with H" and T have the same precursor masses
            var hDoublePrime = SkylineWindow.Document.Molecules.FirstOrDefault(mol => 
                mol?.CustomMolecule?.Name == nameHDoublePrime);
            var tritium = SkylineWindow.Document.Molecules.FirstOrDefault(mol =>
                mol?.CustomMolecule?.Name == nameTritium);
            Assert.IsNotNull(hDoublePrime);
            Assert.IsNotNull(tritium);
            Assert.AreEqual(1, hDoublePrime.TransitionGroupCount);
            Assert.AreEqual(1, tritium.TransitionGroupCount);
            Assert.AreEqual(2, hDoublePrime.TransitionGroups.First().TransitionCount);
            Assert.AreEqual(2, tritium.TransitionGroups.First().TransitionCount);
            CollectionAssert.AreEqual(hDoublePrime.TransitionGroups.First().Transitions.Select(t => t.Mz).ToList(),
                tritium.TransitionGroups.First().Transitions.Select(t => t.Mz).ToList());

            // Change the isotope enrichment of H' to .95. (The default value was .98).
            var transitionSettingsUi = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(()=>transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.FullScan);
            RunDlg<EditIsotopeEnrichmentDlg>(transitionSettingsUi.AddToEnrichmentsList, dlg =>
            {
                var defaultEnrichments = IsotopeEnrichmentsList.DEFAULT;
                var newEnrichments = defaultEnrichments.ChangeEnrichment(new IsotopeEnrichmentItem(BioMassCalc.H2, .95));
                Assert.AreNotEqual(newEnrichments, defaultEnrichments);
                newEnrichments = (IsotopeEnrichments) newEnrichments.ChangeName("NewEnrichments");
                dlg.Enrichments = newEnrichments;
                dlg.OkDialog();
            });
            OkDialog(transitionSettingsUi, transitionSettingsUi.OkDialog);

            // Verify that the formulas using H' and D both got new calculated masses.
            var hPrime2 = SkylineWindow.Document.Molecules.First(mol =>
                mol?.CustomMolecule?.Name == nameHPrime);
            var deuterium2 = SkylineWindow.Document.Molecules.First(mol =>
                mol?.CustomMolecule?.Name == nameDeuterium);
            CollectionAssert.AreEqual(hPrime2.TransitionGroups.First().Transitions.Select(t => t.Mz).ToList(),
                deuterium2.TransitionGroups.First().Transitions.Select(t => t.Mz).ToList());
            CollectionAssert.AreNotEqual(hPrime.TransitionGroups.First().Transitions.Select(t => t.Mz).ToList(),
                hPrime2.TransitionGroups.First().Transitions.Select(t => t.Mz).ToList());
        }
    }
}
