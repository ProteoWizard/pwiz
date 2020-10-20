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
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class CrosslinkingTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestCrosslinking()
        {
            TestFilesZip = @"TestFunctional\CrosslinkingTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("CrosslinkingTest.sky")));
            WaitForDocumentLoaded();
            const string crosslinkerName = "DSS";
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(()=>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Digest;
                peptideSettingsUi.MaxMissedCleavages = 2;
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Modifications;
            });
            var editModListDlg = ShowEditStaticModsDlg(peptideSettingsUi);
            var editStaticModDlg = ShowDialog<EditStaticModDlg>(editModListDlg.AddItem);
            RunUI(()=> {
            {
                editStaticModDlg.Modification = new StaticMod(crosslinkerName, "K", null, "C8H10O2");
                editStaticModDlg.IsCrosslinker = true;
            } });

            OkDialog(editStaticModDlg, editStaticModDlg.OkDialog);
            OkDialog(editModListDlg, editModListDlg.OkDialog);
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            var transitionSettingsUi = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(()=>
            {
                transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.Filter;
                transitionSettingsUi.PrecursorCharges = "4,5";
                transitionSettingsUi.ProductCharges = "4,3,2,1";
                transitionSettingsUi.FragmentTypes = "y";
            });
            
            OkDialog(transitionSettingsUi, transitionSettingsUi.OkDialog);
            RunUI(()=>
            {
                SkylineWindow.Paste("LCVLHEKTPVSEK");
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo(1, 0);
            });

            var modifyPeptideDlg = ShowDialog<EditPepModsDlg>(SkylineWindow.ModifyPeptide);
            var editCrosslinkModDlg = ShowDialog<EditLinkedPeptideDlg>(() =>
            {
                modifyPeptideDlg.SelectModification(IsotopeLabelType.light, 6, crosslinkerName);
            });
            RunUI(() =>
            {
                editCrosslinkModDlg.PeptideSequence = "CASIQKFGER";
                editCrosslinkModDlg.AttachmentOrdinal = 6;
            });
            OkDialog(editCrosslinkModDlg, editCrosslinkModDlg.OkDialog);
            OkDialog(modifyPeptideDlg, modifyPeptideDlg.OkDialog);
            RunUI(()=>SkylineWindow.ShowGraphSpectrum(true));
            WaitForGraphs();
            var graphSpectrum = FindOpenForm<GraphSpectrum>();
            Assert.IsNotNull(graphSpectrum.AvailableSpectra);
            var availableSpectra = graphSpectrum.AvailableSpectra.ToList();
            Assert.AreEqual(1, availableSpectra.Count);
            RunDlg<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg, pasteDlg=>
            {
                SetClipboardText("KSAPATGGVKKPHR-VTIAQGGVLPNIQAVLLPKK-[+138.0681@11,19]");
                pasteDlg.PastePeptides();
                pasteDlg.OkDialog();
            });
            RunUI(() =>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo(2, 3);
            });
            WaitForGraphs();
            graphSpectrum = FindOpenForm<GraphSpectrum>();
            Assert.IsNotNull(graphSpectrum.AvailableSpectra);
            availableSpectra = graphSpectrum.AvailableSpectra.ToList();
            Assert.AreEqual(1, availableSpectra.Count);
            RunUI(()=>SkylineWindow.SaveDocument());
        }
    }
}
