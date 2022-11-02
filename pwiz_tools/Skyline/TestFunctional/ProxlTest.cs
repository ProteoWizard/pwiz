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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests creating a library (.blib) from .proxl.xml (crosslinking) search results.
    /// </summary>
    [TestClass]
    public class ProxlTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestProxl()
        {
            TestFilesZip = @"TestFunctional\ProxlTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const string crosslinkerName = "DSS";
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Digest;
                peptideSettingsUi.MaxMissedCleavages = 2;
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Modifications;
            });
            var editModListDlg = ShowEditStaticModsDlg(peptideSettingsUi);
            var editStaticModDlg = ShowDialog<EditStaticModDlg>(editModListDlg.AddItem);
            RunUI(() => {
                {
                    editStaticModDlg.Modification = new StaticMod(crosslinkerName, "K", null, "C8H10O2");
                    editStaticModDlg.IsCrosslinker = true;
                }
            });

            OkDialog(editStaticModDlg, editStaticModDlg.OkDialog);
            OkDialog(editModListDlg, editModListDlg.OkDialog);
            RunUI(() =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Library;
            });
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettingsUi.ShowBuildLibraryDlg);
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = "ProxlLibrary";
                buildLibraryDlg.LibraryPath = TestFilesDir.GetTestPath("MyProxlLibrary.blib");
                buildLibraryDlg.OkWizardPage();
                buildLibraryDlg.AddInputFiles(new []{TestFilesDir.GetTestPath("ProxlTest.proxl.xml") });
            });
            WaitForConditionUI(() => buildLibraryDlg.Grid.ScoreTypesLoaded);
            OkDialog(buildLibraryDlg, buildLibraryDlg.OkWizardPage);
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);

            var transitionSettingsUi = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.Filter;
                transitionSettingsUi.PrecursorCharges = "1,2,3,4,5";
                transitionSettingsUi.ProductCharges = "5,4,3,2,1";
                transitionSettingsUi.FragmentTypes = "y,b";
            });
            OkDialog(transitionSettingsUi, transitionSettingsUi.OkDialog);

            WaitForDocumentLoaded();
            var viewLibraryDlg = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            var addModificationsDlg = WaitForOpenForm<AddModificationsDlg>();
            OkDialog(addModificationsDlg, addModificationsDlg.OkDialogAll);
            var multiButtonMsgDlg = ShowDialog<MultiButtonMsgDlg>(viewLibraryDlg.AddAllPeptides);
            OkDialog(multiButtonMsgDlg, multiButtonMsgDlg.ClickOk);
            RunUI(viewLibraryDlg.Close);

            // Verify that looplink "HAVSEGTKAVTKYTSAK" got added
            var peptides =
                SkylineWindow.Document.Peptides.Where(peptide => peptide.Peptide.Sequence == "HAVSEGTKAVTKYTSAK").ToList();
            Assert.AreEqual(1, peptides.Count);
            Assert.AreNotEqual(null, peptides[0].ExplicitMods);
            Assert.AreEqual(1, peptides[0].ExplicitMods.CrosslinkStructure.Crosslinks.Count);
            Assert.AreEqual(0, peptides[0].ExplicitMods.CrosslinkStructure.LinkedPeptides.Count);
        }
    }
}
