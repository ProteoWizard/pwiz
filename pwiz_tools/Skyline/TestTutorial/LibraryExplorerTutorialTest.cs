/*
 * Original author: Mimi Fung <mfung03 .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.SkylineTestTutorial
{
    /// <summary>
    /// Testing the tutorial for Skyline Library Explorer
    /// </summary>
    [TestClass]
    public class LibraryExplorerTutorialTest : AbstractFunctionalTest
    { 
        [TestMethod]
        public void TestLibraryExplorerTutorial()
        {
            TestFilesZip = @"https://skyline.gs.washington.edu/tutorials/LibraryExplorer.zip";
            RunFunctionalTest();
        }
        protected override void DoTest()
        {
            // Exploring a Library,  p. 1
            PeptideSettingsUI peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var editListUI =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUI.EditLibraryList);
            RunDlg<EditLibraryDlg>(editListUI.AddItem, editLibraryDlg =>
            {
                editLibraryDlg.LibrarySpec =
                    new BiblioSpecLibSpec("Experiment 15N",
                        TestFilesDir.GetTestPath(@"LibraryExplorer\labeled_15N.blib"));
                editLibraryDlg.OkDialog();
            });
            OkDialog(editListUI, editListUI.OkDialog);
            RunUI(() => peptideSettingsUI.PickedLibraries = new[] { "Experiment 15N" });

            // Modifications Tab in peptideSttingsUI to check "carbamidomethyl cysteine"
            RunUI(() => peptideSettingsUI.PickedStaticMods = new[] { "Carbamidomethyl Cysteine" });
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);

            Assert.IsTrue(WaitForCondition(() =>
                SkylineWindow.Document.Settings.PeptideSettings.Libraries.IsLoaded &&
                SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Count > 0));

            // Go to view menu and click Spectral Libraries
            ViewLibraryDlg viewLibraryDlg = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            var matchedPepsDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(matchedPepsDlg.BtnCancelClick);

            // Types text in Peptide textbox in the Spectral Library Explorer Window
            RunUI(() =>
            {
                viewLibraryDlg.FilterString = "Q";
                Assert.AreEqual(7, viewLibraryDlg.PeptideDisplayCount);
            });

            RunUI(() =>
            {
                viewLibraryDlg.FilterString = "l";
                Assert.AreEqual(3, viewLibraryDlg.PeptideDisplayCount);
            });
            
            // Click B and 2 buttons on the right side of Spectral Library Explorer Window to show b-ions
            RunUI(() =>
            {
                Assert.AreEqual(11, viewLibraryDlg.GraphItem.IonLabels.Count());
                viewLibraryDlg.GraphSettings.ShowBIons = true;
                viewLibraryDlg.GraphSettings.ShowCharge2 = true;
                Assert.AreEqual(24, viewLibraryDlg.GraphItem.IonLabels.Count());
            });
            
            // Right click on spectrum chart and select Observed m/z Values
            RunUI(() =>
            {
                if (!Settings.Default.ShowObservedMz)
                {
                    if (!Settings.Default.ShowIonMz)
                    {
                        var labelsBefore = viewLibraryDlg.GraphItem.IonLabels.ToArray();
                        Assert.IsFalse(labelsBefore.Contains(label => label.Contains("\n")));
                    }
                    viewLibraryDlg.SetObservedMzValues(true);
                }
                var labelsAfter = viewLibraryDlg.GraphItem.IonLabels;
                Assert.IsTrue(labelsAfter.Contains(label => label.Contains("\n")));
            });
            
            // Clearing Peptide Textbox...
            RunUI(() =>
            {
                viewLibraryDlg.FilterString = "";
                Assert.AreEqual(43, viewLibraryDlg.PeptideDisplayCount);
            });

            // Matching Modifications p. 7
            
            // Settings > Peptides Settings
            var peptideSettingsUI1 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            const string glnPyroGluName = "Gln->pyro-Glu";
            var glnPyroGlu = new StaticMod(glnPyroGluName, "Q", null, true, "-NH3", LabelAtoms.None,
                                          RelativeRT.Unknown, null, null, null);
            AddStaticMod(glnPyroGlu, peptideSettingsUI1);
            RunUI(() => peptideSettingsUI1.PickedStaticMods = new[] {glnPyroGluName});
            const string label15NName = "Label:15N";
            var mod15N = new StaticMod(label15NName, null, null, false, null, LabelAtoms.N15,
                              RelativeRT.Matching, null, null, null);
            AddHeavyMod(mod15N, peptideSettingsUI1);
            RunUI(() => peptideSettingsUI1.PickedHeavyMods = new[] { label15NName });
            OkDialog(peptideSettingsUI1, peptideSettingsUI1.OkDialog);

            Assert.IsTrue(WaitForCondition(() =>
                SkylineWindow.Document.Settings.PeptideSettings.Modifications.StaticModifications.Count > 0 &&
                SkylineWindow.Document.Settings.PeptideSettings.Modifications.HeavyModifications.Count > 0));

            // Adding Library Peptides to the Document p. 11

            // Adding AEVNGLAAQGKYEGSGEDGGAAAQSLYIANHAY
            var docInitial = SkylineWindow.Document;
            const string peptideSequence1 = "AEVNGLAAQGKYEGSGEDGGAAAQSLYIANHAY";
            RunUI(() =>
            {
                viewLibraryDlg.FilterString = peptideSequence1;
                Assert.AreEqual(1, viewLibraryDlg.PeptideDisplayCount);
            });
            RunDlg<FilterMatchedPeptidesDlg>(viewLibraryDlg.AddPeptide, msgDlg => msgDlg.OkDialog());
            
            var docAdd1 = WaitForDocumentChange(docInitial);
            Assert.AreEqual(1, docAdd1.PeptideCount);
            Assert.AreEqual(peptideSequence1, docAdd1.Peptides.ToArray()[0].Peptide.Sequence);


            // Adding DNAGAATEEFIK++ (no ok dialog)
            const string peptideSequence2 = "DNAGAATEEFIK";
            RunUI(() =>
            {
                viewLibraryDlg.FilterString = peptideSequence2 + "++";
                Assert.AreEqual(2, viewLibraryDlg.PeptideDisplayCount);
            });
            RunUI(viewLibraryDlg.AddPeptide);

            var docAdd2 = WaitForDocumentChange(docAdd1);
            Assert.AreEqual(2, docAdd2.PeptideCount);
            Assert.AreEqual(peptideSequence2, docAdd2.Peptides.ToArray()[1].Peptide.Sequence);

            // Edit > ExpandAll > Peptides
            RunUI(() => SkylineWindow.ExpandPeptides());

            // Adding DNAGAATEEFIKR++ (has ok)
            const string peptideSequence3 = "DNAGAATEEFIKR";
            RunUI(() =>
            {
                viewLibraryDlg.FilterString = peptideSequence3 + "++";
                Assert.AreEqual(2, viewLibraryDlg.PeptideDisplayCount);
            });

            RunDlg<FilterMatchedPeptidesDlg>(viewLibraryDlg.AddPeptide, msgDlg => msgDlg.OkDialog());
            var docAdd3 = WaitForDocumentChange(docAdd2);
            Assert.AreEqual(3, docAdd3.PeptideCount);
            Assert.AreEqual(2, docAdd3.Peptides.ToArray()[2].Children.Count);
            Assert.AreEqual(peptideSequence3, docAdd3.Peptides.ToArray()[2].Peptide.Sequence);

            // Adding DNAGAATEEFIKR+++ (has ok)
            RunUI(() =>
            {
                viewLibraryDlg.FilterString = peptideSequence3 + "+++";
                Assert.AreEqual(1, viewLibraryDlg.PeptideDisplayCount);
            });
            WaitForGraphs();

            RunDlg<FilterMatchedPeptidesDlg>(viewLibraryDlg.AddPeptide, msgDlg => msgDlg.OkDialog());
            var docAdd4 = WaitForDocumentChange(docAdd3);
            // peptideSequence4 is considered a sub of peptideSequence3
            Assert.AreEqual(3, docAdd4.PeptideCount);
            Assert.AreEqual(4, docAdd4.Peptides.ToArray()[2].Children.Count);

            // Close the Library Explorer dialog
            OkDialog(viewLibraryDlg, viewLibraryDlg.CancelDialog);

            // Save current document as 15N_library_peptides.sky
            RunUI(() => SkylineWindow.SaveDocument(TestFilesDir.GetTestPath(@"LibraryExplorer\15N_library_peptides.sky")));
            RunUI(() => SkylineWindow.NewDocument());

            // Neutral Losses p. 13
            PeptideSettingsUI settingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
                      {
                          settingsUI.PickedStaticMods = new[] { "Carbamidomethyl Cysteine" };
                          settingsUI.PickedHeavyMods = new string[0];
                          settingsUI.PickedLibraries = new string[0];
                      });
            var editListUI1 =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(settingsUI.EditLibraryList);
            const string humanPhosphoLibName = "Human Phospho";
            RunDlg<EditLibraryDlg>(editListUI1.AddItem, editLibraryDlg =>
            {
                editLibraryDlg.LibrarySpec =
                    new BiblioSpecLibSpec(humanPhosphoLibName,
                        TestFilesDir.GetTestPath(@"LibraryExplorer\phospho.blib"));
                editLibraryDlg.OkDialog();
            });
            OkDialog(editListUI1, editListUI1.OkDialog);
            RunUI(() => settingsUI.PickedLibraries = new[] { humanPhosphoLibName });
            OkDialog(settingsUI, settingsUI.OkDialog);

            Assert.IsTrue(WaitForCondition(() =>
                SkylineWindow.Document.Settings.PeptideSettings.Modifications.StaticModifications.Count == 1 &&
                SkylineWindow.Document.Settings.PeptideSettings.Modifications.HeavyModifications.Count == 0 &&
                SkylineWindow.Document.Settings.PeptideSettings.Libraries.IsLoaded &&
                SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Count > 0));

            ViewLibraryDlg viewLibraryDlg1 = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            var matchedPepModsDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(matchedPepModsDlg.BtnCancelClick);
            
            const int countLabels1 = 15;
            const int countLabels2 = 18;
            RunUI(() =>
            {
                viewLibraryDlg1.FilterString = "AISS";
                Assert.AreEqual(2, viewLibraryDlg1.PeptideDisplayCount);
                Assert.AreEqual(countLabels1, viewLibraryDlg1.GraphItem.IonLabels.Count());
                viewLibraryDlg1.SelectedIndex = 1;
                Assert.AreEqual(countLabels2, viewLibraryDlg1.GraphItem.IonLabels.Count());
            });

            docInitial = SkylineWindow.Document;

            var peptideSettingsUI2 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            const string phosphoModName = "Phospho ST";
            var phosphoSt = new StaticMod(phosphoModName, "S, T", null, true, "HPO3", LabelAtoms.None,
                                          RelativeRT.Unknown, null, null, new[] { new FragmentLoss("H3PO4"), });
            AddStaticMod(phosphoSt, peptideSettingsUI2);

            // Check Phospho (ST) and Carbamidomethyl Cysteine
            RunUI(() => peptideSettingsUI2.PickedStaticMods = new[] { phosphoModName, "Carbamidomethyl Cysteine" });
            OkDialog(peptideSettingsUI2, peptideSettingsUI2.OkDialog);

            var docPhospho = WaitForDocumentChange(docInitial);
            Assert.IsTrue(docPhospho.Settings.PeptideSettings.Modifications.StaticModifications.Count == 2);

            string lossText = Math.Round(-phosphoSt.Losses[0].MonoisotopicMass, 1).ToString(CultureInfo.CurrentCulture);
            const int countLossLabels1 = 12;
            const int countLossLabels2 = 15;
            
            RunUI(() =>
            {
                // New ions should be labeled, because of the added modification with loss
                var labelsPhospho = viewLibraryDlg1.GraphItem.IonLabels;
                Assert.AreEqual(countLabels2 + countLossLabels2, labelsPhospho.Count());
                Assert.AreEqual(countLossLabels2, labelsPhospho.Count(label => label.Contains(lossText)));
                viewLibraryDlg1.SelectedIndex = 0;
                Assert.AreEqual(countLabels1 + countLossLabels1, viewLibraryDlg1.GraphItem.IonLabels.Count());

                viewLibraryDlg1.GraphSettings.ShowPrecursorIon = true;

                // Make sure the precursor -98 ion was added
                var labelsFinal = viewLibraryDlg1.GraphItem.IonLabels;
                Assert.IsTrue(labelsFinal.Contains(label => label.Contains("precursor " + lossText)));
                Assert.AreEqual(countLabels1 + countLossLabels1 + 1, labelsFinal.Count());
            });

            // Matching Library Peptides to Proteins p. 18
            var peptideSettingsUI3 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var buildBackgroundProteomeDlg =
                ShowDialog<BuildBackgroundProteomeDlg>(peptideSettingsUI3.ShowBuildBackgroundProteomeDlg);
            RunUI(() =>
            {
                buildBackgroundProteomeDlg.BuildNew = true;
                buildBackgroundProteomeDlg.BackgroundProteomePath =
                    TestFilesDir.GetTestPath(@"LibraryExplorer\human.protdb");
                buildBackgroundProteomeDlg.BackgroundProteomeName = "Human (mini)";
            });
            OkDialog(buildBackgroundProteomeDlg, buildBackgroundProteomeDlg.OkDialog);
            
            // Select Max Missed Cleavages to be 2 
            RunUI(() =>
            {
                peptideSettingsUI3.MissedCleavages = 2;
            });
            OkDialog(peptideSettingsUI3, peptideSettingsUI3.OkDialog);
            
            Assert.IsTrue(WaitForCondition(() =>
            {
                var peptideSettings = Program.ActiveDocument.Settings.PeptideSettings;
                var backgroundProteome = peptideSettings.BackgroundProteome;
                return (backgroundProteome.GetDigestion(peptideSettings) != null);
            }));

            RunUI(() =>
            {
                viewLibraryDlg1.FilterString = "";
                Assert.AreEqual(100, viewLibraryDlg1.PeptideDisplayCount);
            });

            // Check Associate Proteins check box on Spectral Library Explorer form
            RunUI(() =>
            {
                viewLibraryDlg1.AssociateMatchingProteins = true;
            });

            docInitial = SkylineWindow.Document;

            // Add everything in the library to the document.
            var filterMatchedPeptidesDlg = 
                ShowDialog<FilterMatchedPeptidesDlg>(viewLibraryDlg1.AddAllPeptides);
            
            RunUI(() =>
                      {
                          filterMatchedPeptidesDlg.DuplicateProteinsFilter = 
                              BackgroundProteome.DuplicateProteinsFilter.FirstOccurence;
                          filterMatchedPeptidesDlg.AddUnmatched = true;
                          filterMatchedPeptidesDlg.AddFiltered = true;
                      });

            // Checks that Peptides were added with the correct buttons selected...
            Assert.AreEqual(163, filterMatchedPeptidesDlg.DuplicateMatchesCount);
            Assert.AreEqual(2, filterMatchedPeptidesDlg.UnmatchedCount);
            Assert.IsTrue(filterMatchedPeptidesDlg.AddUnmatched);
            Assert.IsTrue(filterMatchedPeptidesDlg.AddFiltered);
            Assert.AreEqual("FirstOccurence", filterMatchedPeptidesDlg.DuplicateProteinsFilter.ToString());

            RunDlg<MultiButtonMsgDlg>(filterMatchedPeptidesDlg.OkDialog, 
                msgDlg => msgDlg.Btn1Click());
            OkDialog(viewLibraryDlg1, viewLibraryDlg1.CancelDialog);

            var docProteins = WaitForDocumentChange(docInitial);

            AssertEx.IsDocumentState(docProteins, null, 250, 346, 347, 1034);
        }
    }
}
