/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that adding a loss on a modification which already has some losses
    /// correctly updates transitions whose loss indexes got shifted.
    /// </summary>
    [TestClass]
    public class EditNeutralLossesTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestEditNeutralLosses()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const string modificationName = "MyModification";
            // Define a modification with one loss
            RunLongDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
                {
                    RunUI(()=>peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Modifications);
                    RunLongDlg<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(peptideSettingsUi.EditStaticMods,
                        editStaticMods =>
                        {
                            RunLongDlg< EditStaticModDlg>(editStaticMods.AddItem, editStaticMod =>
                            {
                                RunUI(() =>
                                {
                                    editStaticMod.Modification =
                                        new StaticMod(modificationName, null, null, "H10O6");
                                });
                                RunDlg<EditFragmentLossDlg>(editStaticMod.AddLoss, editFragmentLossDlg =>
                                {
                                    editFragmentLossDlg.Loss = new FragmentLoss("H4O2", null, null, LossInclusion.Always);
                                    editFragmentLossDlg.OkDialog();
                                });
                            }, editStaticMod=>editStaticMod.OkDialog());
                        }, editStaticMods=>editStaticMods.OkDialog());
                }, peptideSettingsUi => peptideSettingsUi.OkDialog()
            );
            // Insert a peptide with that modification
            RunUI(() =>
            {
                SkylineWindow.Paste("PEPTIDE[106.05]");
            });
            var transition = SkylineWindow.Document.MoleculeTransitions.FirstOrDefault(t=>null != t.Losses);
            Assert.IsNotNull(transition);
            Assert.AreEqual(0, transition.Losses.Losses[0].LossIndex);
            Assert.AreEqual(1, transition.Losses.Losses[0].PrecursorMod.Losses.Count);
            AssertEx.Serializable(SkylineWindow.Document);

            // Edit the modification definition and add a loss with a smaller mass, causing the existing loss to be moved down the list
            RunLongDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
                {
                    RunLongDlg<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(peptideSettingsUi.EditStaticMods,
                        editStaticMods =>
                        {
                            RunUI(()=>editStaticMods.SelectItem(modificationName));
                            RunLongDlg<EditStaticModDlg>(editStaticMods.EditItem, editStaticMod =>
                            {
                                RunDlg<EditFragmentLossDlg>(editStaticMod.AddLoss, editFragmentLossDlg =>
                                {
                                    editFragmentLossDlg.Loss = new FragmentLoss("H2O", null, null, LossInclusion.Never);
                                    editFragmentLossDlg.OkDialog();
                                });
                            }, editStaticMod => editStaticMod.OkDialog());
                        }, editStaticMods => editStaticMods.OkDialog());
                }, 
                peptideSettingsUi =>
                {
                    peptideSettingsUi.OkDialog();
                });
            
            // Make sure that the LossIndex in the transition is the new correct value
            transition = SkylineWindow.Document.MoleculeTransitions.First(t => null != t.Losses);
            Assert.AreEqual(1, transition.Losses.Losses[0].LossIndex);
            Assert.AreEqual(2, transition.Losses.Losses[0].PrecursorMod.Losses.Count);

            // Make sure that the document can round-trip to XML
            AssertEx.Serializable(SkylineWindow.Document);
        }
    }
}
