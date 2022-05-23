/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Summary description for LibraryExplorerTest
    /// </summary>
    [TestClass]
    public class NeutralLossTest : AbstractFunctionalTest
    {
        private const string TEXT_FASTA_YEAST_7 =
            ">YAL007C ERP2 SGDID:S000000005, Chr I from 138347-137700, reverse complement, Verified ORF, \"Protein that forms a heterotrimeric complex with Erp1p, Emp24p, and Erv25p; member, along with Emp24p and Erv25p, of the p24 family involved in ER to Golgi transport and localized to COPII-coated vesicles\"\n" +
            "MIKSTIALPSFFIVLILALVNSVAASSSYAPVAISLPAFSKECLYYDMVTEDDSLAVGYQ\n" +
            "VLTGGNFEIDFDITAPDGSVITSEKQKKYSDFLLKSFGVGKYTFCFSNNYGTALKKVEIT\n" +
            "LEKEKTLTDEHEADVNNDDIIANNAVEEIDRNLNKITKTLNYLRAREWRNMSTVNSTESR\n" +
            "LTWLSILIIIIIAVISIAQVLLIQFLFTGRQKNYV*\n";

        [TestMethod]
        public void TestNeutralLoss()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Create modifications used in this test
            var aquaMod = new StaticMod("Aqua 13C", "K, R", ModTerminus.C, null, LabelAtoms.C13|LabelAtoms.N15, null, null);
            var phosphoLossMod = new StaticMod("Phospho Loss", "S, T, Y", null, false, "HPO3",
                LabelAtoms.None, RelativeRT.Matching, null, null,  new[] { new FragmentLoss("H3PO4"), });
            var multipleLossMod = new StaticMod("Multiple Loss-only", "A", null, false, null,
                LabelAtoms.None, RelativeRT.Matching, null, null, new[] { new FragmentLoss("NH3"), new FragmentLoss("H2O") });
            var explicitMassesLossMod = new StaticMod("Explicit Mass Loss", "N", null, false, null,
                LabelAtoms.None, RelativeRT.Matching, null, null, new[] { new FragmentLoss(null, 5, 10) });
            var variableLossMod = new StaticMod("Methionine Oxidized Loss", "M", null, true, "O",
                LabelAtoms.None, RelativeRT.Matching, null, null, new[] { new FragmentLoss("CH3O") });

            Settings.Default.HeavyModList.Clear();
            Settings.Default.HeavyModList.Add(aquaMod);
            Settings.Default.StaticModList.Clear();
            Settings.Default.StaticModList.AddRange(StaticModList.GetDefaultsOn());
            Settings.Default.StaticModList.Add(variableLossMod);

            // Bump up the max mz a bit
            var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Instrument;
                transitionSettingsUI.MaxMz = 1510; 
            });
            OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);

            // Bring up add modification dialog
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(() =>
                SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Modifications));
            var editModsDlg = ShowEditStaticModsDlg(peptideSettingsUI);
            var addModDlg = ShowAddModDlg(editModsDlg);
            // Try a few things with new loss UI on the phospho loss modification
            RunUI(() =>
            {
                addModDlg.Modification = phosphoLossMod;
                Assert.IsTrue(addModDlg.GetLossText(0).EndsWith(phosphoLossMod.Losses[0].Formula));
                addModDlg.LossSelectedIndex = 0;
                addModDlg.DeleteLoss();
            });
            // Add back the original loss with the new loss editor dialog
            RunDlg<EditFragmentLossDlg>(addModDlg.AddLoss, dlg =>
            {
                dlg.Loss = phosphoLossMod.Losses[0];
                dlg.OkDialog();
            });
            // Add a smaller formula loss with the new loss editor dialog
            var fragmentLossWater = new FragmentLoss("H2O");
            RunDlg<EditFragmentLossDlg>(addModDlg.AddLoss, dlg =>
            {
                dlg.Loss = fragmentLossWater;
                dlg.OkDialog();
            });
            // Add an explicit mass loss with the new loss editor dialog
            var fragmentLossConstants = new FragmentLoss(null, 20, 25);
            RunDlg<EditFragmentLossDlg>(addModDlg.AddLoss, dlg =>
            {
                dlg.Loss = fragmentLossConstants;
                dlg.OkDialog();
            });
            // Check the added losses, and remove all but the original loss
            RunUI(() =>
            {
                VerifyLossText(addModDlg.GetLossText(0), fragmentLossWater);
                VerifyLossText(addModDlg.GetLossText(1), fragmentLossConstants);
                VerifyLossText(addModDlg.GetLossText(2), phosphoLossMod.Losses[0]);
                addModDlg.LossSelectedIndex = 0;
                addModDlg.DeleteLoss();
                addModDlg.DeleteLoss();
            });
            OkDialog(addModDlg, addModDlg.OkDialog);
            // Add a loss modification with 2 losses and no formula
            RunDlg<EditStaticModDlg>(editModsDlg.AddItem, dlg =>
            {
                dlg.Modification = multipleLossMod;
                VerifyLossText(dlg.GetLossText(0), multipleLossMod.Losses[0]);
                VerifyLossText(dlg.GetLossText(1), multipleLossMod.Losses[1]);
                dlg.OkDialog();
            });
            // Add a loss modification with explicit numeric losses and no formula
            // starting with no losses
            var addExplicitLossDlg = ShowAddModDlg(editModsDlg);
            RunUI(() =>
            {
                addExplicitLossDlg.Modification = explicitMassesLossMod;
                addExplicitLossDlg.ShowLoss = true;
                VerifyLossText(addExplicitLossDlg.GetLossText(0), explicitMassesLossMod.Losses[0]);
                addExplicitLossDlg.LossSelectedIndex = 0;
                addExplicitLossDlg.DeleteLoss();
            });
            // Add the explicit numeric loss
            RunDlg<EditFragmentLossDlg>(addExplicitLossDlg.AddLoss, dlg =>
            {
                dlg.Loss = fragmentLossConstants;
                dlg.OkDialog();
            });
            OkDialog(addExplicitLossDlg, addExplicitLossDlg.OkDialog);
            OkDialog(editModsDlg, editModsDlg.OkDialog);
            // Check the phospho loss modification
            RunUI(() => peptideSettingsUI.PickedStaticMods =
                new List<string>(peptideSettingsUI.PickedStaticMods) { phosphoLossMod.Name }.ToArray());
            WaitForOpenForm<PeptideSettingsUI>(); // Show Modifications tab for form testing
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);

            // Add FASTA sequence
            RunUI(() => SkylineWindow.Paste(TEXT_FASTA_YEAST_7));
            // Losses not added by default without a library
            Assert.AreEqual(0, GetLossCount(SkylineWindow.Document, 1));

            // Make sure setting losses as included Always works
            {
                var docBeforeAlways = SkylineWindow.Document;
                var peptideSettingsUIAlways = ShowDialog<PeptideSettingsUI>(() =>
                    SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Modifications));
                var editModsDlgAlways = ShowEditStaticModsDlg(peptideSettingsUIAlways);
                RunUI(() => editModsDlgAlways.SelectItem(phosphoLossMod.Name));
                var editModDlgAlways = ShowDialog<EditStaticModDlg>(editModsDlgAlways.EditItem);
                RunUI(() => editModDlgAlways.LossSelectedIndex = 0);
                RunDlg<EditFragmentLossDlg>(editModDlgAlways.EditLoss, dlg =>
                {
                    dlg.Inclusion = LossInclusion.Always;
                    dlg.OkDialog();
                });
                OkDialog(editModDlgAlways, editModDlgAlways.OkDialog);
                OkDialog(editModsDlgAlways, editModsDlgAlways.OkDialog);
                OkDialog(peptideSettingsUIAlways, peptideSettingsUIAlways.OkDialog);
                var docAfterAlways = WaitForDocumentChange(docBeforeAlways);
                Assert.AreEqual(5, GetLossCount(docAfterAlways, 1));
                AssertEx.Serializable(docAfterAlways);
                // Undo and revert to original phospo loss modification definition
                RunUI(SkylineWindow.Undo);
                Settings.Default.StaticModList.SetValue(phosphoLossMod);
            }

            // Show losses in the pick list
            var docFasta = WaitForProteinMetadataBackgroundLoaderCompletedUI();
            SelectNode(SrmDocument.Level.TransitionGroups, 0);
            RunDlg<PopupPickList>(SkylineWindow.ShowPickChildrenInTest, dlg =>
            {
                dlg.ApplyFilter(false);
                var losses = GetLossGroups(dlg.ItemNames).ToArray();
                Assert.AreEqual(1, losses.Length);
                VerifyLossGroup(losses, 0, 98, 160);
                dlg.ToggleFind();
                dlg.SearchString = "-98";
                dlg.ToggleItem(0);
                dlg.ToggleItem(1);
                dlg.OnOk();
            });
            var docLoss = WaitForDocumentChange(docFasta);
            // Make sure two losses were added
            Assert.AreEqual(2, GetLossCount(docLoss, 1));

            // Allow 2 neutral losses per fragment
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, dlg =>
            {
                dlg.MaxNeutralLosses = 2;
                dlg.OkDialog();
            });

            var docLoss2 = WaitForDocumentChange(docLoss);

            // Make sure 2 phospho losses are now possible
            IGrouping<double, string>[] losses1 = null;
            RunDlg<PopupPickList>(SkylineWindow.ShowPickChildrenInTest, dlg =>
            {
                dlg.ApplyFilter(false);
                losses1 = GetLossGroups(dlg.ItemNames).ToArray();
                Assert.AreEqual(2, losses1.Length);
                VerifyLossGroup(losses1, 0, 98, 160);
                VerifyLossGroup(losses1, 1, 196, 139);
                dlg.OnCancel();
            });

            // Add a modification with multiple possible losses
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, dlg =>
            {
                dlg.PickedStaticMods = new List<string>(dlg.PickedStaticMods)
                    { multipleLossMod.Name }.ToArray();
                dlg.OkDialog();
            });

            var docLossMulti = WaitForDocumentChange(docLoss2);

            // Make sure all combinations of 2 except 2 of the multiple
            // losses are possible, since only one amino acid A exists in this
            // peptide.
            RunDlg<PopupPickList>(SkylineWindow.ShowPickChildrenInTest, dlg =>
            {
                dlg.ApplyFilter(false);
                var losses = GetLossGroups(dlg.ItemNames).ToArray();
                Assert.AreEqual(6, losses.Length);
                VerifyLossGroup(losses, 0, 17, 106);
                VerifyLossGroup(losses, 1, 18, 106);
                VerifyLossGroup(losses, 2, 98, 160);
                VerifyLossGroup(losses, 3, 115, 97);
                VerifyLossGroup(losses, 4, 116, 97);
                VerifyLossGroup(losses, 5, 196, 139);
                AssertEx.NoDiff(TextUtil.LineSeparate(losses1[0].ToArray()), TextUtil.LineSeparate(losses[2].ToArray()));
                AssertEx.NoDiff(TextUtil.LineSeparate(losses1[1].ToArray()), TextUtil.LineSeparate(losses[5].ToArray()));
                // Add new neutral loss transitions to the document
                dlg.ToggleFind();
                dlg.SearchString = "-17]";
                dlg.ToggleItem(0);
                dlg.SearchString = "-115]";
                dlg.ToggleItem(0);
                dlg.OnOk();
            });

            var docLossMultiAdded = WaitForDocumentChange(docLossMulti);
            Assert.AreEqual(1, GetLossCount(docLossMultiAdded, 2));

            // Add heavy modifications and make sure transition group is added with losses
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, dlg =>
            {
                dlg.PickedHeavyMods = new[] { aquaMod.Name };
                dlg.OkDialog();
            });

            var docLossMultiHeavy = WaitForDocumentChange(docLossMultiAdded);
            Assert.AreEqual(4, docLossMultiHeavy.PeptideTransitionGroupCount);
            Assert.AreEqual(8, GetLossCount(docLossMultiHeavy, 1));
            Assert.AreEqual(2, GetLossCount(docLossMultiHeavy, 2));

            // Make sure this document is serializable
            AssertEx.Serializable(docLossMultiHeavy, AssertEx.DocumentCloned);

            // Reset to just 1 neutral loss per fragment, and turn off heavy mod
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, dlg =>
            {
                dlg.PickedHeavyMods = new string[0];
                dlg.MaxNeutralLosses = 1;
                dlg.OkDialog();
            });

            var docLossMulti1 = WaitForDocumentChange(docLossMultiHeavy);
            Assert.AreEqual(3, GetLossCount(docLossMulti1, 1));
            Assert.AreEqual(0, GetLossCount(docLossMulti1, 2));

            // Remove all neutral loss modifications currently on the document
            // and add the constant numeric loss
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, dlg =>
            {
                dlg.PickedStaticMods = new[] { explicitMassesLossMod.Name };
                dlg.OkDialog();
            });

            var docNoLoss2 = WaitForDocumentChange(docLossMulti1);
            Assert.AreEqual(0, GetLossCount(docNoLoss2, 1));

            // Add a neutral loss transition with constant numeric loss
            RunDlg<PopupPickList>(SkylineWindow.ShowPickChildrenInTest, dlg =>
            {
                dlg.ApplyFilter(false);
                var losses = GetLossGroups(dlg.ItemNames).ToArray();
                Assert.AreEqual(1, losses.Length);
                VerifyLossGroup(losses, 0, 20, 113);
                // Add new neutral loss transitions to the document
                dlg.ToggleFind();
                dlg.SearchString = "-20";
                dlg.ToggleItem(0);
                dlg.OnOk();
            });

            // Verify that the expected loss was added to the document
            var docConstantMono = WaitForDocumentChange(docNoLoss2);
            Assert.AreEqual(1, GetLossCount(docConstantMono, 1));
            var pathTranLoss = docConstantMono.GetPathTo((int) SrmDocument.Level.Transitions, 0);
            var nodeTranLos = (TransitionDocNode) docConstantMono.FindNode(pathTranLoss);
            Assert.AreEqual(IonType.precursor.GetLocalizedString() + " -20", nodeTranLos.FragmentIonName);

            // Switch mass type to average
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, dlg =>
            {
                dlg.FragmentMassType = MassType.Average;
                dlg.OkDialog();
            });

            // Make sure neutral loss changes as expected
            var docConstantAverage = WaitForDocumentChange(docConstantMono);
            Assert.AreEqual(1, GetLossCount(docConstantAverage, 1));
            nodeTranLos = (TransitionDocNode)docConstantAverage.FindNode(pathTranLoss);
            Assert.AreEqual(IonType.precursor.GetLocalizedString() + " -25", nodeTranLos.FragmentIonName);

            // Make sure the resulting document is serializable
            AssertEx.Serializable(docConstantAverage, AssertEx.DocumentCloned);
        }

        private static void VerifyLossGroup(IGrouping<double, string>[] losses, int i,
            double lossMass, int count)
        {
            Assert.AreEqual(lossMass, losses[i].Key);
            Assert.AreEqual(count, losses[i].Count());
        }

        private static void VerifyLossText(string lossText, FragmentLoss loss)
        {
            if (loss.Formula != null)
                Assert.IsTrue(lossText.EndsWith(loss.Formula));
            Assert.IsTrue(lossText.StartsWith(string.Format("{0:F04}", loss.MonoisotopicMass)));
        }

        private static int GetLossCount(SrmDocument document, int minLosses)
        {
            int count = 0;
            foreach (var nodeTran in document.PeptideTransitions)
            {
                if (nodeTran.HasLoss && nodeTran.Losses.Losses.Count >= minLosses)
                    count++;
            }
            return count;
        }

        private static IEnumerable<IGrouping<double, string>> GetLossGroups(IEnumerable<string> itemNames)
        {
            Regex regexLoss = new Regex(@"-(\d+)\]");
            Regex regexPrecursorLoss = new Regex(string.Format("{0} -(\\d+)", IonType.precursor.GetLocalizedString()));

            return from name in itemNames
                   where GetLossMass(name, regexPrecursorLoss, regexLoss) > 0
                   group name by GetLossMass(name, regexPrecursorLoss, regexLoss) into g
                   select g;
        }

        private static double GetLossMass(string name, Regex regexPrecursorLoss, Regex regexProductLoss)
        {
            var regexLoss = (name.StartsWith(IonType.precursor.GetLocalizedString()) ? regexPrecursorLoss : regexProductLoss);
            var match = regexLoss.Match(name);
            if (!match.Success)
                return 0;
            return double.Parse(match.Groups[1].Value);
        }
    }
}