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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class RemoveNeutralLossModificationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRemoveNeutralLossModification()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var waterLossModification = UniMod.GetModification("Water Loss (D, E, S, T)", true);
            Assert.IsNotNull(waterLossModification);
            var phosphoModification = UniMod.GetModification("Phospho (ST)", true);
            Assert.IsNotNull(phosphoModification);

            // Insert the peptide "ELVISK"
            RunUI(() =>
            {
                SkylineWindow.Paste("ELVISK");
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
            });

            // Add some modifications to the peptide settings
            RunLongDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                RunUI(() => { peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Modifications; });
                RunDlg<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(peptideSettingsUi.EditStaticMods,
                    editMods =>
                    {
                        editMods.AddItem(waterLossModification);
                        editMods.OkDialog();
                    });
                RunDlg<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(peptideSettingsUi.EditStaticMods,
                    editMods =>
                    {
                        editMods.AddItem(phosphoModification);
                        editMods.OkDialog();
                    });
            }, peptideSettingsUi => peptideSettingsUi.OkDialog());
            // Apply the Phospho modification to the "S" amino acid on the peptide and the Water Loss to the "E"
            RunDlg<EditPepModsDlg>(SkylineWindow.ModifyPeptide, editPepModsDlg =>
            {
                editPepModsDlg.SetModification(4, IsotopeLabelType.light, phosphoModification.Name);
                editPepModsDlg.SetModification(0, IsotopeLabelType.light, waterLossModification.Name);
                editPepModsDlg.OkDialog();
            });
            RunUI(() =>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.TransitionGroups, 0);
            });
            // Add all of the phospho neutral loss modifications
            RunDlg<PopupPickList>(SkylineWindow.ShowPickChildrenInTest, dlg =>
            {
                dlg.ApplyFilter(false);
                dlg.ToggleFind();
                dlg.SearchString = "-98";
                dlg.SelectAll = true;
                dlg.OnOk();
            });
            Assert.AreNotEqual(0, GetLossesForMod(phosphoModification).Count());
            Assert.AreEqual(0, GetLossesForMod(waterLossModification).Count());
            // Add all of the water loss modifications
            RunDlg<PopupPickList>(SkylineWindow.ShowPickChildrenInTest, dlg =>
            {
                dlg.ApplyFilter(false);
                dlg.ToggleFind();
                dlg.SearchString = "-18";
                dlg.SelectAll = true;
                dlg.OnOk();
            });
            Assert.AreNotEqual(0, GetLossesForMod(phosphoModification).Count());
            Assert.AreNotEqual(0, GetLossesForMod(waterLossModification).Count());
            // Remove the phospho modification from settings
            RunLongDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI,
                peptideSettingsUi =>
                {
                    RunUI(() => { peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Modifications; });
                    RunDlg<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(peptideSettingsUi.EditStaticMods,
                        editMods =>
                        {
                            editMods.SelectItem(phosphoModification.Name);
                            editMods.RemoveItem();
                            editMods.OkDialog();
                        });
                }, peptideSettingsUi => peptideSettingsUi.OkDialog());
            AssertEx.Serializable(SkylineWindow.Document);
            Assert.AreEqual(0, GetLossesForMod(phosphoModification).Count());
            Assert.AreNotEqual(0, GetLossesForMod(waterLossModification).Count());
        }

        private IEnumerable<TransitionLoss> GetLossesForMod(StaticMod staticMod)
        {
            return SkylineWindow.Document.MoleculeTransitions.Where(transition => null != transition.Losses)
                .SelectMany(transition => transition.Losses.Losses)
                .Where(transitionLoss => transitionLoss.PrecursorMod.Name == staticMod.Name);
        }
    }
}
