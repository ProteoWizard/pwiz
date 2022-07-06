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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Summary description for LibraryExplorerTest
    /// </summary>
    [TestClass]
    public class NeutralLossLibraryTest : AbstractFunctionalTest
    {
        private const string TEXT_FASTA_SPROT =
            ">sp|P04075|ALDOA_HUMAN Fructose-bisphosphate aldolase A OS=Homo sapiens GN=ALDOA PE=1 SV=2\n" +
            "MPYQYPALTPEQKKELSDIAHRIVAPGKGILAADESTGSIAKRLQSIGTENTEENRRFYR\n" +
            "QLLLTADDRVNPCIGGVILFHETLYQKADDGRPFPQVIKSKGGVVGIKVDKGVVPLAGTN\n" +
            "GETTTQGLDGLSERCAQYKKDGADFAKWRCVLKIGEHTPSALAIMENANVLARYASICQQ\n" +
            "NGIVPIVEPEILPDGDHDLKRCQYVTEKVLAAVYKALSDHHIYLEGTLLKPNMVTPGHAC\n" +
            "TQKFSHEEIAMATVTALRRTVPPAVTGITFLSGGQSEEEASINLNAINKCPLLKPWALTF\n" +
            "SYGRALQASALKAWGGKKENLKAAQEEYVKRALANSLACQGKYTPSGQAGAAASESLFVS\n" +
            "NHAY*\n" +
            ">sp|Q9Y5C1|ANGL3_HUMAN Angiopoietin-related protein 3 OS=Homo sapiens GN=ANGPTL3 PE=1 SV=1\n" +
            "MFTIKLLLFIVPLVISSRIDQDNSSFDSLSPEPKSRFAMLDDVKILANGLLQLGHGLKDF\n" +
            "VHKTKGQINDIFQKLNIFDQSFYDLSLQTSEIKEEEKELRRTTYKLQVKNEEVKNMSLEL\n" +
            "NSKLESLLEEKILLQQKVKYLEEQLTNLIQNQPETPEHPEVTSLKTFVEKQDNSIKDLLQ\n" +
            "TVEDQYKQLNQQHSQIKEIENQLRRTSIQEPTEISLSSKPRAPRTTPFLQLNEIRNVKHD\n" +
            "GIPAECTTIYNRGEHTSGMYAIRPSNSQVFHVYCDVISGSPWTLIQHRIDGSQNFNETWE\n" +
            "NYKYGFGRLDGEFWLGLEKIYSIVKQSNYVLRIELEDWKDNKHYIEYSFYLGNHETNYTL\n" +
            "HLVAITGNVPNAIPENKDLVFSTWDHKAKGHFNCPEGYSGGWWWHDECGENNLNGKYNKP\n" +
            "RAKSKPERRRGLSWKSQNGRLYSIKSTKMLIHPTDSESFE*\n" +
            ">sp|Q13790|APOF_HUMAN Apolipoprotein F OS=Homo sapiens GN=APOF PE=1 SV=1\n" +
            "MIPVELLLCYLLLHPVDATSYGKQTNVLMHFPLSLESQTPSSDPLSCQFLHPKSLPGFSH\n" +
            "MAPLPKFLVSLALRNALEEAGCQADVWALQLQLYRQGGVNATQVLIQHLRGLQKGRSTER\n" +
            "NVSVEALASALQLLAREQQSTGRVGRSLPTEDCENEKEQAVHNVVQLLPGVGTFYNLGTA\n" +
            "LYYATQNCLGKARERGRDGAIDLGYDLLMTMAGMSGGPMGLAISAALKPALRSGVQQLIQ\n" +
            "YYQDQKDANISQPETTKEGLRAISDVSDLEETTTLASFISEVVSSAPYWGWAIIKSYDLD\n" +
            "PGAGSLEI*";

        [TestMethod]
        public void TestNeutralLossLibrary()
        {
            TestFilesZip = @"TestFunctional\NeutralLossLibraryTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Create the modification and library spec used in this test
            var phosphoLossMod = new StaticMod("Phospho Loss", "S, T", null, true, "HPO3",
                                              LabelAtoms.None, RelativeRT.Matching, null, null,  new[] { new FragmentLoss("H3PO4"), });
            var multipleLossMod = new StaticMod("Multiple Loss-only", "D", null, false, null,
                LabelAtoms.None, RelativeRT.Matching, null, null, new[]
                {
                    new FragmentLoss("NH3"),
                    new FragmentLoss(null, 20, 25)
                });
            var waterLossMod = new StaticMod("Water Loss-only", "D", null, false, null,
                LabelAtoms.None, RelativeRT.Matching, null, null, new[]
                {
                    new FragmentLoss("H2O")
                });
            var heavyKMod = new StaticMod("Heavy K", "K", ModTerminus.C, null, LabelAtoms.C13 | LabelAtoms.N15, null, null);
            var librarySpec = new BiblioSpecLiteSpec("Phospho Library",
                                                     TestFilesDir.GetTestPath("phospho_30882_v2.blib"));

            // Prepare settings for this test
            Settings.Default.StaticModList.Clear();
            Settings.Default.StaticModList.AddRange(StaticModList.GetDefaultsOn());
            Settings.Default.StaticModList.Add(phosphoLossMod);
            Settings.Default.StaticModList.Add(multipleLossMod);
            Settings.Default.StaticModList.Add(waterLossMod);
            Settings.Default.HeavyModList.Clear();
            Settings.Default.HeavyModList.Add(heavyKMod);
            Settings.Default.SpectralLibraryList.Clear();
            Settings.Default.SpectralLibraryList.Add(librarySpec);
            Settings.Default.ShowLosses = "H3PO4,H2O,NH3";

            // Prepare document settings for this test
            const int countIons = 6;
            var settings = SrmSettingsList.GetDefault()
                .ChangePeptideModifications(mods => mods.ChangeStaticModifications(new List<StaticMod>(mods.StaticModifications) { phosphoLossMod }))
                .ChangePeptideLibraries(lib => lib.ChangeLibrarySpecs(new[] { librarySpec }))
                .ChangeTransitionLibraries(tlib => tlib.ChangeIonCount(countIons));

            RunUI(() => SkylineWindow.ModifyDocument("Set test settings",
                                                     doc => doc.ChangeSettings(settings)));

            WaitForDocumentLoaded();

            // Add FASTA sequence
            RunUI(() => SkylineWindow.Paste(TEXT_FASTA_SPROT));

            var docLibLoss = SkylineWindow.Document;
            AssertEx.IsDocumentState(docLibLoss, null, 3, 4, 24);
            Assert.AreEqual(7, GetLossCount(docLibLoss, 1));

            string lossLabel = "-" + Math.Round(phosphoLossMod.Losses[0].MonoisotopicMass, 1);
            for (int i = 0; i < docLibLoss.PeptideTransitionGroupCount; i++)
            {
                var pathTranGroup = docLibLoss.GetPathTo((int) SrmDocument.Level.TransitionGroups, i);
                var nodeGroup = (TransitionGroupDocNode) docLibLoss.FindNode(pathTranGroup);
                if (!nodeGroup.Children.Contains(child => ((TransitionDocNode) child).HasLoss))
                    continue;

                // Select the transition groups that contain loss ions
                SelectNode(SrmDocument.Level.TransitionGroups, i);
                WaitForGraphs();

                // Make sure the spectrum graph contains some -98 ions
                RunUI(() => Assert.IsTrue(SkylineWindow.GraphSpectrum.IonLabels.Contains(label => label.Contains(lossLabel)),
                    string.Format("Missing loss labels in spectrum graph for {0}", nodeGroup.TransitionGroup.Peptide.Target)));

                // Make sure the transition tree nodes contain -98 ions
                RunUI(() => Assert.IsTrue(GetChildLabels(SkylineWindow.SelectedNode).Contains(label => label.Contains(lossLabel)),
                    string.Format("Missing loss labels in transition tree nodes for {0}", nodeGroup.TransitionGroup.Peptide.Target)));
            }

            // Make the settings significantly more complex
            // - 2 neutral losses
            // - a second modification with 3 potential neutral losses
            // - a heavy labeling modification
            // - only ions between precursor m/z and last ion
            // - allow both y- and b- ions
            // - no proline peaks
            // - only use filtered ions to match library
            RunUI(() => SkylineWindow.ModifyDocument("Set test settings", doc =>
                doc.ChangeSettings(doc.Settings.ChangePeptideModifications(mod =>
                        mod.ChangeMaxNeutralLosses(2)
                            .ChangeStaticModifications(new List<StaticMod>(mod.StaticModifications) {multipleLossMod, waterLossMod})
                            .ChangeModifications(IsotopeLabelType.heavy, new[] { heavyKMod }))
                    .ChangeTransitionFilter(filter =>
                        filter.ChangeFragmentRangeFirstName("m/z > precursor")
                            .ChangeFragmentRangeLastName("last ion")
                            .ChangePeptideIonTypes(new[] { IonType.y, IonType.b })
                            .ChangeMeasuredIons(new MeasuredIon[0]))
                    .ChangeTransitionLibraries(lib =>
                        lib.ChangePick(TransitionLibraryPick.filter)))));

            var docComplex = WaitForDocumentChange(docLibLoss);

            // The document has 2 variable mod peptides at the protein terminus
            Assert.AreEqual(docLibLoss.PeptideTransitionGroupCount*2 - 2, docComplex.PeptideTransitionGroupCount);
            Assert.AreEqual(4, GetLossCount(docComplex, 2));
            Assert.AreEqual(16, GetLossCount(docComplex, 1));
            foreach (var nodePep in docComplex.Peptides)
            {
                if (nodePep.Children.Count != 2)
                    continue;
                var nodeGroup1 = (TransitionGroupDocNode)nodePep.Children[0];
                var nodeGroup2 = (TransitionGroupDocNode)nodePep.Children[1];

                Assert.IsTrue(nodeGroup1.EquivalentChildren(nodeGroup2));
            }
            // CONSIDER: Can't check cloned because of libraries.  Maybe add a new method for this
            // AssertEx.Serializable(docComplex, AssertEx.DocumentCloned);

            SelectNode(SrmDocument.Level.Molecules, 1);
            WaitForGraphs();

            // Verify that the ion labels in the graph match those in the tree view
            RunUI(() =>
                      {
                          var nodePepSelected = (PeptideDocNode) docComplex.FindNode(SkylineWindow.SelectedPath);
                          string[] ionLabels = SkylineWindow.GraphSpectrum.IonLabels.ToArray();

                          foreach (TransitionGroupDocNode nodeGroup in nodePepSelected.Children)
                          {
                              var setSeenRanks = new HashSet<int>();
                              foreach (TransitionDocNode nodeTran in nodeGroup.Children)
                              {
                                  Assert.IsTrue(nodeTran.HasLibInfo);
                                  // Make sure the rank has not been seen
                                  int rankTran = nodeTran.LibInfo.Rank;
                                  Assert.IsFalse(setSeenRanks.Contains(rankTran));
                                  setSeenRanks.Add(rankTran);
                                  // And it is within the expected range
                                  Assert.IsTrue(1 <= rankTran && rankTran <= countIons);
                                  // Make sure there is a matching label in the spectrum graph
                                  var regexRank = nodeTran.HasLoss ? 
                                      new Regex(string.Format(@"(\w)(\d+) -([^ +]+).* \({0}\)", string.Format(Resources.AbstractSpectrumGraphItem_GetLabel_rank__0__, rankTran))) :
                                      new Regex(string.Format(@"(\w)(\d+).* \({0}\)", string.Format(Resources.AbstractSpectrumGraphItem_GetLabel_rank__0__, rankTran)));
                                  int iLabel = ionLabels.IndexOf(regexRank.IsMatch);
                                  Assert.IsTrue(iLabel != -1);
                                  var match = regexRank.Match(ionLabels[iLabel]);
                                  Assert.AreEqual(nodeTran.Transition.IonType.ToString(), match.Groups[1].Value);
                                  Assert.AreEqual(nodeTran.Transition.Ordinal, int.Parse(match.Groups[2].Value));
                                  if (nodeTran.HasLoss)
                                      Assert.AreEqual(nodeTran.Losses.Mass, double.Parse(match.Groups[3].Value), 0.1);
                              }
                          }
                      });

            // Make sure setting losses as included Never works
            {
                var docBeforeNever = SkylineWindow.Document;
                var peptideSettingsUINever = ShowDialog<PeptideSettingsUI>(() =>
                    SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Modifications));
                var editModsDlgNever = ShowEditStaticModsDlg(peptideSettingsUINever);
                RunUI(() => editModsDlgNever.SelectItem(phosphoLossMod.Name));
                var editModDlgNever = ShowDialog<EditStaticModDlg>(editModsDlgNever.EditItem);
                RunUI(() => editModDlgNever.LossSelectedIndex = 0);
                RunDlg<EditFragmentLossDlg>(editModDlgNever.EditLoss, dlg =>
                {
                    dlg.Inclusion = LossInclusion.Never;
                    dlg.OkDialog();
                });
                RunDlg<MultiButtonMsgDlg>(editModDlgNever.OkDialog, dlg => dlg.Btn1Click());
                OkDialog(editModsDlgNever, editModsDlgNever.OkDialog);
                OkDialog(peptideSettingsUINever, peptideSettingsUINever.OkDialog);
                var docAfterNever = WaitForDocumentChange(docBeforeNever);
                Assert.AreEqual(2, GetLossCount(docAfterNever, 1));

                RunUI(SkylineWindow.Undo);
            }
            TestLossAnnotationsControl();

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

        private static IEnumerable<string> GetChildLabels(TreeNode nodeTree)
        {
            nodeTree.Expand();
            return from TreeNode nodeChild in nodeTree.Nodes
                   select nodeChild.Text;
        }

        private void TestLossAnnotationsControl()
        {
            MenuControl<IonTypeSelectionPanel> menuControl = null;
            WaitForGraphs();
            RunUI(() =>
            {
                menuControl = new MenuControl<IonTypeSelectionPanel>(SkylineWindow.GraphSpectrumSettings,
                    SkylineWindow.DocumentUI.Settings.PeptideSettings);
                //Make sure losses are visible by default after the document load
                AssertLossLabelCount(12, 13);
                AssertLossControlButtonCount(menuControl, 13, 7);
            });
            RunUI(() =>
            {
                SkylineWindow.ShowLosses(new[] { "H2O" });
            });
            WaitForGraphs();
            RunUI(() =>
            {   //all losses in the document, but only H2O is visible
                menuControl.Update(SkylineWindow.GraphSpectrumSettings, SkylineWindow.DocumentUI.Settings.PeptideSettings);
                //Make sure all displayed losses are enabled
                AssertLossLabelCount(6,6);
                AssertLossControlButtonCount(menuControl, 13, 3);
            });

            //Change peptide mod settings
            var docBefore = SkylineWindow.Document;
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(() =>
                SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Modifications));
            RunUI(() => peptideSettingsUI.PickedStaticMods = new[] { "Water Loss-only" });
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            WaitForDocumentChange(docBefore);
            FindNode("K.SYDLDPGAGSLEI.");   //select a node without phosphate mod
            WaitForGraphs();
            RunUI(() =>
            {   //water loss only in the document and only H2O is visible
                menuControl.Update(SkylineWindow.GraphSpectrumSettings, SkylineWindow.DocumentUI.Settings.PeptideSettings);
                AssertLossLabelCount(9, 9);
                AssertLossControlButtonCount(menuControl, 10, 4);
            });

            docBefore = SkylineWindow.Document;
            peptideSettingsUI = ShowDialog<PeptideSettingsUI>(() =>
                SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Modifications));
            RunUI(() => peptideSettingsUI.PickedStaticMods = new string[] { });
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            WaitForDocumentChange(docBefore);
            WaitForGraphs();
            RunUI(() =>
            {
                menuControl.Update(SkylineWindow.GraphSpectrumSettings, SkylineWindow.DocumentUI.Settings.PeptideSettings);
                //No losses visible
                AssertLossLabelCount(0, 0);
                AssertLossControlButtonCount(menuControl, 8, 2);
            });

            RunUI(() => { menuControl.Dispose(); });

        }

        public static void AssertLossLabelCount(int lossLabelCount, int lossSpectrumPeaksCount)
        {
            //find all annotations with losses
            var lossLabelMatch = new Regex(@"(\D)(\d+) -([^ +]+).*");
            var ionLabelsWithLoss = SkylineWindow.GraphSpectrum.IonLabels
                .Select(label => lossLabelMatch.Match(label)).ToList().FindAll(m => m.Success).ToList();
            Assert.AreEqual(lossLabelCount, ionLabelsWithLoss.Count);

            //get matched peaks with losses from the currently displayed spectrum
            var showLossesProperty = SkylineWindow.GraphSpectrumSettings.ShowLosses;
            var ionsWithLosses = SkylineWindow.GraphSpectrum.DisplayedSpectrum.PeaksMatched
                .SelectMany(p => p.MatchedIons).ToList().FindAll(ion => ion.Losses != null && ion.HasVisibleLoss(showLossesProperty))
                .ToList();

            Assert.AreEqual(lossSpectrumPeaksCount, ionsWithLosses.Count);

            //match the peak to the spectrum annotations
            //make sure each label has a matching peak
            var matchedList = ionLabelsWithLoss.Select(label => new {
                label,
                ion = ionsWithLosses.Find(
                    ion => label.Groups[1].Value.Equals(ion.IonType.GetLocalizedString()) &&
                           label.Groups[2].Value.Equals(ion.Ordinal.ToString()) &&
                           Math.Abs(ion.Losses.Mass - double.Parse(label.Groups[3].Value)) < 0.1)
            });

            Assert.AreEqual(lossLabelCount, matchedList.Count());
        }
        public static void AssertLossControlButtonCount<T>(MenuControl<T> control, int buttonsTotal, int buttonsChecked) where T : Panel, IControlSize, new()
        {
            Assert.AreEqual(buttonsTotal, control.HostedControl.Controls.OfType<CheckBox>().Count());
            Assert.AreEqual(buttonsChecked, control.HostedControl.Controls.OfType<CheckBox>().ToList().FindAll(cb => cb.Checked).Count);
        }
    }
}