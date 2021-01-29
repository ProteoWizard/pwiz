/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class CrosslinkNeutralLossTest : AbstractFunctionalTest
    {
        const double DELTA = 0.000001;

        [TestMethod]
        public void TestCrosslinkNeutralLosses()
        {
            TestFilesZip = @"TestFunctional\CrosslinkNeutralLossTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const string crosslinkerName = "Hydrolysis";
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("CrosslinkNeutralLossTest.sky")));
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Modifications;
            });
            var editModListDlg = ShowEditStaticModsDlg(peptideSettingsUi);
            // Define a crosslinker which is a water loss. In this way, two peptides can be joined end to end
            // and will have the same chemical formula as a single concatenated peptide
            RunDlg<EditStaticModDlg>(editModListDlg.AddItem, editStaticModDlg=> {
                {
                    editStaticModDlg.Modification = new StaticMod(crosslinkerName, null, null, "-H2O");
                    editStaticModDlg.IsCrosslinker = true;
                    editStaticModDlg.OkDialog();
                }
            });
            RunDlg<EditStaticModDlg>(editModListDlg.AddItem, editStaticModDlg =>
            {
                editStaticModDlg.Modification = ChangeLossesToIncludeAlways(UniMod.GetModification("Oxidation (M)", true));
                editStaticModDlg.OkDialog();
            });
            RunDlg<EditStaticModDlg>(editModListDlg.AddItem, editStaticModDlg =>
            {
                editStaticModDlg.Modification = ChangeLossesToIncludeAlways(UniMod.GetModification("Phospho (ST)", true));
                editStaticModDlg.OkDialog();
            });

            OkDialog(editModListDlg, editModListDlg.OkDialog);
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            RunDlg<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg, pasteDlg =>
            {
                // Insert two peptides which are equivalent to each other.
                // The first peptide is composed of two short peptides concatenated with the hydrolysis crosslinker.
                SetClipboardText(@"AMNFS[Phospho (ST)]GSPGAV-STSPT[Phospho (ST)]QSFM[Oxidation (M)]NTLPR-[Hydrolysis@11,1]
AMNFS[Phospho (ST)]GSPGAVSTSPT[Phospho (ST)]QSFM[Oxidation (M)]NTLPR");
                pasteDlg.PastePeptides();
                pasteDlg.OkDialog();
            });
            AssertEx.Serializable(SkylineWindow.Document);
            var crosslinkedPeptide = SkylineWindow.Document.Peptides.First();
            var flatPeptide = SkylineWindow.Document.Peptides.Skip(1).First();
            Assert.IsNotNull(crosslinkedPeptide.ExplicitMods);
            Assert.IsNotNull(flatPeptide.ExplicitMods);
            Assert.IsTrue(crosslinkedPeptide.ExplicitMods.HasCrosslinks);
            Assert.IsFalse(flatPeptide.ExplicitMods.HasCrosslinks);
            Assert.AreEqual(1, crosslinkedPeptide.TransitionGroupCount);
            Assert.AreEqual(1, flatPeptide.TransitionGroupCount);

            var flatPrecursor = flatPeptide.TransitionGroups.First();
            var crosslinkedPrecursor = crosslinkedPeptide.TransitionGroups.First();
            Assert.AreEqual(flatPrecursor.PrecursorMz, crosslinkedPrecursor.PrecursorMz, DELTA);
            var flatTransitionNames = flatPrecursor.Transitions.Select(tran =>
                    tran.ComplexFragmentIon.GetTargetsTreeLabel() +
                    Transition.GetChargeIndicator(tran.Transition.Adduct))
                .ToList();
            Assert.AreEqual(flatPrecursor.TransitionCount, flatTransitionNames.Count);
            var crosslinkedTransitionNames = crosslinkedPrecursor.Transitions.Select(tran =>
                    tran.ComplexFragmentIon.GetTargetsTreeLabel() +
                    Transition.GetChargeIndicator(tran.Transition.Adduct))
                .ToList();
            Assert.AreEqual(crosslinkedPrecursor.TransitionCount, crosslinkedTransitionNames.Count);

            foreach (var transitionDocNode in flatPrecursor.Transitions)
            {
                // AMNFSGSPGAV(11)-STSPTQSFMNTLPR(14)
                IonChain complexFragmentIonName = null;
                switch (transitionDocNode.Transition.IonType)
                {
                    case IonType.precursor:
                        complexFragmentIonName = IonChain.FromIons(IonOrdinal.Precursor, IonOrdinal.Precursor);
                        break;
                    case IonType.b:
                        if (transitionDocNode.Transition.Ordinal == 11)
                        {
                            continue;
                        }
                        if (transitionDocNode.Transition.Ordinal <= 11)
                        {
                            complexFragmentIonName = IonChain.FromIons(IonOrdinal.B(transitionDocNode.Transition.Ordinal), IonOrdinal.Empty);
                        }
                        else
                        {
                            complexFragmentIonName = IonChain.FromIons(IonOrdinal.Precursor, IonOrdinal.B(transitionDocNode.Transition.Ordinal - 11));
                        }
                        break;
                    case IonType.y:
                        if (transitionDocNode.Transition.Ordinal == 14)
                        {
                            continue;
                        }

                        if (transitionDocNode.Transition.Ordinal < 14)
                        {
                            complexFragmentIonName = 
                                IonChain.FromIons(IonOrdinal.Empty, IonOrdinal.Y(transitionDocNode.Transition.Ordinal));
                        }
                        else
                        {
                            complexFragmentIonName = 
                                IonChain.FromIons(IonOrdinal.Y(transitionDocNode.Transition.Ordinal - 14), IonOrdinal.Precursor);
                        }
                        break;
                }
                Assert.IsNotNull(complexFragmentIonName);

                if (transitionDocNode.Transition.IonType != IonType.precursor && transitionDocNode.Losses != null &&
                    transitionDocNode.Losses.Losses.Count > 1)
                {
                    continue;
                }

                var matchingTransitions = crosslinkedPrecursor.Transitions.Where(tran =>
                    complexFragmentIonName.Equals(tran.ComplexFragmentIon.GetName()) 
                    && Equals(transitionDocNode.Losses, tran.Losses)
                    && Equals(transitionDocNode.Transition.Adduct, tran.Transition.Adduct)).ToList();
                AssertEx.AreEqual(1, matchingTransitions.Count);
                AssertEx.AreEqual(transitionDocNode.Mz, matchingTransitions[0].Mz, DELTA);
            }
        }

        private StaticMod ChangeLossesToIncludeAlways(StaticMod staticMod)
        {
            if (!staticMod.HasLoss)
            {
                return staticMod;
            }

            return staticMod.ChangeLosses(staticMod.Losses.Select(loss => loss.ChangeInclusion(LossInclusion.Always))
                .ToList());
        }
    }
}
