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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class NeutralLossModificationDisplayTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestNeutralLossModificationDisplay()
        {
            RunFunctionalTest();
        }
        protected override void DoTest()
        {
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                var enzyme = Settings.Default.EnzymeList.FirstOrDefault(e => e.Name == "Trypsin/P");
                Assert.IsNotNull(enzyme);
                peptideSettingsUi.ComboEnzymeSelected = enzyme.ToString();
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Filter;
                peptideSettingsUi.TextExcludeAAs = 0;
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Modifications;
            });
            var editModListDlg =
                ShowDialog<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(peptideSettingsUi.EditStaticMods);
            RunDlg<EditStaticModDlg>(editModListDlg.AddItem, editStaticModDlg =>
            {
                editStaticModDlg.Modification = UniMod.GetModification("Water Loss (D, E, S, T)", true);
                editStaticModDlg.OkDialog();
            });
            RunDlg<EditStaticModDlg>(editModListDlg.AddItem, editStaticModDlg =>
            {
                editStaticModDlg.Modification = UniMod.GetModification("Oxidation (M)", true).ChangeVariable(true);
                editStaticModDlg.OkDialog();
            });
            RunDlg<EditStaticModDlg>(editModListDlg.AddItem, editStaticModDlg =>
            {
                editStaticModDlg.Modification = UniMod.GetModification("Phospho (ST)", true);
                editStaticModDlg.OkDialog();
            });
            OkDialog(editModListDlg, editModListDlg.OkDialog);
            RunUI(() =>
            {
                peptideSettingsUi.PickedStaticMods = peptideSettingsUi.PickedStaticMods
                    .Append("Water Loss (D, E, S, T)")
                    .Append("Oxidation (M)").ToArray();
            });
            editModListDlg =
                ShowDialog<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(peptideSettingsUi.EditHeavyMods);
            RunDlg<EditStaticModDlg>(editModListDlg.AddItem, editStaticModDlg =>
            {
                editStaticModDlg.Modification = UniMod.GetModification("Label:13C(6) (K)", false);
                editStaticModDlg.OkDialog();
            });
            OkDialog(editModListDlg, editModListDlg.OkDialog);
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            RunDlg<PasteDlg>(SkylineWindow.ShowPasteProteinsDlg, pasteDlg =>
            {
                SetClipboardText("MyProtein\t\tPEPTIDEK PEPTIDECK PEPTIDEMK PEPTIDECMK PEPTIDER");
                pasteDlg.PasteProteins();
                pasteDlg.OkDialog();
            });
            Assert.AreEqual(7, SkylineWindow.Document.PeptideCount);
            CollectionAssert.AreEqual(new[]
            {
                "-.PEPTIDEK.P [1, 8]",
                "K.PEPTIDECK.P [9, 17]",
                "K.PEPTIDEMK.P [18, 26]",
                "K.PEPTIDEMK.P [18, 26]",
                "K.PEPTIDECMK.P [27, 36]",
                "K.PEPTIDECMK.P [27, 36]",
                "K.PEPTIDER.- [37, 44]",
            }, GetPeptideDisplayTexts());
            RunUI(()=>SkylineWindow.SetModifiedSequenceDisplayOption(DisplayModificationOption.THREE_LETTER_CODE));
            CollectionAssert.AreEqual(new[]
            {
                "-.PEPTIDEK.P [1, 8]",
                "K.PEPTIDEC[CAM]K.P [9, 17]",
                "K.PEPTIDEMK.P [18, 26]",
                "K.PEPTIDEM[Oxi]K.P [18, 26]",
                "K.PEPTIDEC[CAM]MK.P [27, 36]",
                "K.PEPTIDEC[CAM]M[Oxi]K.P [27, 36]",
                "K.PEPTIDER.- [37, 44]",
            }, GetPeptideDisplayTexts());
            RunUI(()=>SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Molecules, 5));
            RunDlg<EditPepModsDlg>(SkylineWindow.ModifyPeptide, editPepModsDlg =>
            {
                editPepModsDlg.SetModification(3, IsotopeLabelType.light, "Phospho (ST)");
                editPepModsDlg.IsCreateCopy = true;
                editPepModsDlg.OkDialog();
            });
            CollectionAssert.AreEqual(new[]
            {
                "-.PEPTIDEK.P [1, 8]",
                "K.PEPTIDEC[CAM]K.P [9, 17]",
                "K.PEPTIDEMK.P [18, 26]",
                "K.PEPTIDEM[Oxi]K.P [18, 26]",
                "K.PEPTIDEC[CAM]MK.P [27, 36]",
                "K.PEPTIDEC[CAM]M[Oxi]K.P [27, 36]",
                "K.PEPT[Pho]IDEC[CAM]M[Oxi]K.P [27, 36]",
                "K.PEPTIDER.- [37, 44]",
            }, GetPeptideDisplayTexts());

        }

        private List<string> GetPeptideDisplayTexts()
        {
            var texts = new List<string>();
            RunUI(() =>
            {
                using (var g = SkylineWindow.CreateGraphics())
                {
                    var modFontHolder = new ModFontHolder(SkylineWindow);
                    foreach (var node in SkylineWindow.SequenceTree.Nodes.OfType<PeptideGroupTreeNode>()
                        .SelectMany(protein => protein.Nodes.OfType<PeptideTreeNode>()))
                    {
                        var textSequences = PeptideTreeNode.CreateTextSequences(node.DocNode,
                            SkylineWindow.Document.Settings, node.Text, g, modFontHolder);
                        var text = string.Concat(textSequences.Select(seq => seq.Text));
                        texts.Add(text);
                    }
                }
            });
            return texts;
        }
    }
}
