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
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class CleavableCrosslinkTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestCleavableCrosslink()
        {
            TestFilesZip = @"TestFunctional\CleavableCrosslinkTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const string CROSSLINKER_NAME = "DSSO";
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Digest;
                peptideSettingsUi.MaxMissedCleavages = 3;
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Library;
            });
            // Library build
            var buildLibrary = ShowDialog<BuildLibraryDlg>(peptideSettingsUi.ShowBuildLibraryDlg);
            RunUI(() =>
            {
                buildLibrary.LibraryName = "MyLibrary";
                buildLibrary.LibraryPath = TestFilesDir.GetTestPath("MyLibrary.blib");
                buildLibrary.OkWizardPage();

                buildLibrary.InputFileNames = new[] { TestFilesDir.GetTestPath("CleavableCrosslinkTest.proxl.xml") };
            });
            WaitForConditionUI(() => buildLibrary.Grid.ScoreTypesLoaded);
            OkDialog(buildLibrary, buildLibrary.OkWizardPage);
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            WaitForDocumentLoaded();

            // Define a cleavable crosslinker named "DSSO"
            peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Modifications);
            var editModListDlg = ShowEditStaticModsDlg(peptideSettingsUi);
            var editStaticModDlg = ShowDialog<EditStaticModDlg>(editModListDlg.AddItem);
            RunUI(() => {
                {
                    editStaticModDlg.Modification = new StaticMod(CROSSLINKER_NAME, "K", null, "C6O3SH6");
                    editStaticModDlg.IsCrosslinker = true;
                }
            });
            // Add neutral losses for each of the cleavable products
            RunDlg<EditFragmentLossDlg>(editStaticModDlg.AddLoss, dlg =>
            {
                dlg.Loss = new FragmentLoss("C3O2SH4");
                dlg.OkDialog();
            });
            RunDlg<EditFragmentLossDlg>(editStaticModDlg.AddLoss, dlg =>
            {
                dlg.Loss = new FragmentLoss("C3OH2");
                dlg.OkDialog();
            });
            OkDialog(editStaticModDlg, editStaticModDlg.OkDialog);
            OkDialog(editModListDlg, editModListDlg.OkDialog);
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);

            // Change the transition settings so that this peptide will be acceptable
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.Filter;
                transitionSettingsUi.PrecursorCharges = "2,3,4,5";
                transitionSettingsUi.ProductCharges = "1,2,3,4";
                transitionSettingsUi.FragmentTypes = "p,y,b";
                transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.Library;
                transitionSettingsUi.IonCount = 20;
                transitionSettingsUi.OkDialog();
            });

            var spectralLibraryViewer = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            
            // Make sure we wait until ViewLibraryDialog._matcher is initialized in the "Shown" event
            WaitForConditionUI(() => spectralLibraryViewer.HasMatches);

            RunUI(()=>spectralLibraryViewer.AddPeptide());
            OkDialog(spectralLibraryViewer, spectralLibraryViewer.Close);
            VerifyExplicitModificationsAreInDocument(SkylineWindow.Document);

            // Add another Loss to the crosslinker modification and make sure the definition gets updated in all the PeptideDocNode's
            peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Modifications);
            editModListDlg = ShowEditStaticModsDlg(peptideSettingsUi);
            editModListDlg.SelectItem(CROSSLINKER_NAME);
            editStaticModDlg = ShowDialog<EditStaticModDlg>(editModListDlg.EditItem);

            RunDlg<EditFragmentLossDlg>(editStaticModDlg.AddLoss, dlg =>
            {
                dlg.Loss = new FragmentLoss("C3O2H4");
                dlg.OkDialog();
            });

            OkDialog(editStaticModDlg, editStaticModDlg.OkDialog);
            OkDialog(editModListDlg, editModListDlg.OkDialog);
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);

            VerifyExplicitModificationsAreInDocument(SkylineWindow.Document);
        }

        public void VerifyExplicitModificationsAreInDocument(SrmDocument document)
        {
            var staticMods = document.Settings.PeptideSettings.Modifications.StaticModifications.ToDictionary(mod=>mod.Name);
            var isotopeMods = document.Settings.PeptideSettings.Modifications.AllHeavyModifications.ToLookup(mod=>mod.Name);
            foreach (var peptide in document.Molecules)
            {
                foreach (var explicitStaticMod in ListExplicitStaticMods(peptide))
                {
                    Assert.IsTrue(staticMods.TryGetValue(explicitStaticMod.Name, out var staticMod), "Static modification {0} not found", explicitStaticMod.Name);
                    Assert.IsTrue(explicitStaticMod.Equivalent(staticMod), "Modification {0} in peptide {1} does not match document modification", explicitStaticMod, peptide.Peptide);
                }

                foreach (var explicitIsotopeMod in ListExplicitIsotopeModifications(peptide))
                {
                    var matchingIsotopeMods = isotopeMods[explicitIsotopeMod.Name].ToList();
                    Assert.AreNotEqual(0, matchingIsotopeMods.Count(), "Isotope modification {0} not found", explicitIsotopeMod);
                    foreach (var documentIsotopeMod in matchingIsotopeMods)
                    {
                        Assert.IsTrue(explicitIsotopeMod.Equivalent(documentIsotopeMod), "Isotope modification {0} in peptide {1} does not match document modificdation", explicitIsotopeMod, peptide.Peptide);
                    }
                }
            }
        }

        public IEnumerable<StaticMod> ListExplicitStaticMods(PeptideDocNode peptideDocNode)
        {
            var mods = new List<StaticMod>();
            if (peptideDocNode.ExplicitMods != null)
            {
                if (peptideDocNode.ExplicitMods.StaticModifications != null)
                {
                    mods.AddRange(peptideDocNode.ExplicitMods.StaticModifications.Select(mod=>mod.Modification));
                }
                mods.AddRange(peptideDocNode.ExplicitMods.CrosslinkStructure.Crosslinks.Select(crosslink=>crosslink.Crosslinker));
            }

            return mods;
        }

        public IEnumerable<StaticMod> ListExplicitIsotopeModifications(PeptideDocNode peptideDocNode)
        {
            var mods = new List<StaticMod>();
            if (peptideDocNode.ExplicitMods != null)
            {
                foreach (var typedModifications in peptideDocNode.ExplicitMods.GetHeavyModifications())
                {
                    if (typedModifications.Modifications != null)
                    {
                        mods.AddRange(typedModifications.Modifications.Select(mod=>mod.Modification));
                    }
                }
            }

            return mods;
        }
    }
}
