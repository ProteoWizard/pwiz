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
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
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
            // Set true to look at tutorial screenshots.
//            IsPauseForScreenShots = true;
//            IsCoverShotMode = true;
            CoverShotName = "LibraryExplorer";

            LinkPdf = "https://skyline.ms/labkey/_webdav/home/software/Skyline/%40files/tutorials/LibraryExplorer-20_2.pdf";

            TestFilesZipPaths = new[]
            {
                @"https://skyline.ms/tutorials/LibraryExplorer.zip",
                @"TestTutorial\LibraryExplorerViews.zip"
            };
            RunFunctionalTest();
        }

        private string GetTestPath(string relativePath)
        {
            return TestFilesDirs[0].GetTestPath(Path.Combine(@"LibraryExplorer", relativePath));
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
                    new BiblioSpecLibSpec("Experiment 15N", GetTestPath(@"labeled_15N.blib"));
                editLibraryDlg.OkDialog();
            });
            OkDialog(editListUI, editListUI.OkDialog);
            RunUI(() =>
                      {
                          peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Library;
                          peptideSettingsUI.PickedLibraries = new[] {"Experiment 15N"};
                      });
            PauseForScreenShot<PeptideSettingsUI.LibraryTab>("Peptide Settings - Library tab", 3);

            // Modifications Tab in peptideSttingsUI to check "Carbamidomethyl Cysteine"
            const string carbamidoName = StaticModList.DEFAULT_NAME;
            RunUI(() =>
                      {
                          peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Modifications;
                          peptideSettingsUI.PickedStaticMods = new[] {carbamidoName};
                      });
            PauseForScreenShot<PeptideSettingsUI.ModificationsTab>("Peptide Settings - Modifications tab", 4);

            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);

            Assert.IsTrue(WaitForCondition(() =>
                SkylineWindow.Document.Settings.PeptideSettings.Libraries.IsLoaded &&
                SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Count > 0));

            // Go to view menu and click Spectral Libraries
            ViewLibraryDlg viewLibraryDlg = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            var matchedPepsDlg = WaitForOpenForm<AddModificationsDlg>();
            OkDialog(matchedPepsDlg, matchedPepsDlg.CancelDialog);
            PauseForScreenShot<ViewLibraryDlg>("Library Explorer", 5);

            // Types text in Peptide textbox in the Spectral Library Explorer Window
            RunUI(() =>
            {
                viewLibraryDlg.FilterString = "Q"; // Not L10N
                Assert.AreEqual(7, viewLibraryDlg.PeptideDisplayCount);
            });
            PauseForScreenShot("Library Explorere filtered for peptides beginning with Q", 6);

            RunUI(() =>
            {
                viewLibraryDlg.FilterString = "I"; // Not L10N
                viewLibraryDlg.SetObservedMzValues(true);
                Assert.AreEqual(1, viewLibraryDlg.PeptideDisplayCount);
            });
            
            // Click B and 2 buttons on the right side of Spectral Library Explorer Window to show b-ions
            RunUI(() =>
            {
                Assert.AreEqual(11, viewLibraryDlg.GraphItem.IonLabels.Count());
                viewLibraryDlg.GraphSettings.ShowBIons = true;
                viewLibraryDlg.GraphSettings.ShowCharge2 = true;
                Assert.AreEqual(35, viewLibraryDlg.GraphItem.IonLabels.Count());
            });
            PauseForScreenShot<ViewLibraryDlg>("Library Explorer showing ISERT peptide with b and charge 2 ions", 7);
            
            // Right click on spectrum chart and select Observed m/z Values
            RunUI(() =>
            {
                if (!Settings.Default.ShowObservedMz)
                {
                    if (!Settings.Default.ShowIonMz)
                    {
                        var labelsBefore = viewLibraryDlg.GraphItem.IonLabels.ToArray();
                        Assert.IsFalse(labelsBefore.Contains(label => label.Contains("\n"))); // Not L10N 
                    }
                    viewLibraryDlg.SetObservedMzValues(true);
                }
                var labelsAfter = viewLibraryDlg.GraphItem.IonLabels;
                Assert.IsTrue(labelsAfter.Contains(label => label.Contains("\n"))); // Not L10N
            });
            
            // Clearing Peptide Textbox...
            RunUI(() =>
            {
                viewLibraryDlg.FilterString = string.Empty;
                Assert.AreEqual(43, viewLibraryDlg.PeptideDisplayCount);
            });

            // Matching Modifications p. 7
            
            // Settings > Peptides Settings
            var peptideSettingsUI1 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            const string glnPyroGluName = "Gln->pyro-Glu (N-term Q)"; // Not L10N
            var glnPyroGlu = UniMod.GetModification(glnPyroGluName, out _);
            glnPyroGlu = glnPyroGlu.ChangeVariable(true);
            AddStaticMod(glnPyroGlu, peptideSettingsUI1, "Edit Structural Modification form", 8);

            RunUI(() => peptideSettingsUI1.PickedStaticMods = new[] {carbamidoName, glnPyroGluName});
            const string label15NName = "Label:15N"; // Not L10N
            var mod15N = UniMod.GetModification(label15NName, out _);
            AddHeavyMod(mod15N, peptideSettingsUI1, "Edit Structural Modification form", 9);
            RunUI(() => peptideSettingsUI1.PickedHeavyMods = new[] { label15NName });
            PauseForScreenShot<PeptideSettingsUI.ModificationsTab>("Peptide Settings - Modificatoins tab", 10);

            OkDialog(peptideSettingsUI1, peptideSettingsUI1.OkDialog);

            Assert.IsTrue(WaitForCondition(() =>
                SkylineWindow.Document.Settings.PeptideSettings.Modifications.StaticModifications.Count > 0 &&
                SkylineWindow.Document.Settings.PeptideSettings.Modifications.AllHeavyModifications.Any()));
            PauseForScreenShot("Peptide list clipped from Library Explorer", 11);

            if (IsCoverShotMode)
            {
                RestoreCoverViewOnScreen(false);
                RunUI(() =>
                {
                    viewLibraryDlg.SetBounds(SkylineWindow.Left, SkylineWindow.Top, SkylineWindow.Width, SkylineWindow.Height);
                });
                TakeCoverShot();

                OkDialog(viewLibraryDlg, viewLibraryDlg.CancelDialog);
                return;
            }
            // Adding Library Peptides to the Document p. 11

            // Adding AEVNGLAAQGKYEGSGEDGGAAAQSLYIANHAY
            var docInitial = WaitForProteinMetadataBackgroundLoaderCompletedUI();
            const string peptideSequence1 = "AEVNGLAAQGKYEGSGEDGGAAAQSLYIANHAY"; // Not L10N
            RunUI(() =>
            {
                viewLibraryDlg.FilterString = peptideSequence1;
                Assert.AreEqual(1, viewLibraryDlg.PeptideDisplayCount);
            });
            {
                var msgDlg = ShowDialog<FilterMatchedPeptidesDlg>(viewLibraryDlg.AddPeptide);
                PauseForScreenShot<FilterMatchedPeptidesDlg>("Filter peptides form", 11);

                OkDialog(msgDlg, msgDlg.OkDialog);
            }
            
            var docAdd1 = WaitForDocumentChange(docInitial);
            Assert.AreEqual(1, docAdd1.PeptideCount);
            Assert.AreEqual(peptideSequence1, docAdd1.Peptides.ToArray()[0].Peptide.Sequence);


            // Adding DNAGAATEEFIK++ (no ok dialog)
            const string peptideSequence2 = "DNAGAATEEFIK"; // Not L10N
            RunUI(() =>
            {
                viewLibraryDlg.FilterString = peptideSequence2 + "++"; // Not L10N
                Assert.AreEqual(2, viewLibraryDlg.PeptideDisplayCount);
            });
            RunUI(viewLibraryDlg.AddPeptide);

            var docAdd2 = WaitForDocumentChange(docAdd1);
            Assert.AreEqual(2, docAdd2.PeptideCount);
            Assert.AreEqual(peptideSequence2, docAdd2.Peptides.ToArray()[1].Peptide.Sequence);

            // Edit > ExpandAll > Peptides
            RunUI(() =>
            {
                SkylineWindow.ExpandPeptides();
                SkylineWindow.Size = new Size(918, 553);
            });
            RestoreViewOnScreen(12);
            PauseForScreenShot("Main window", 12);

            // Adding DNAGAATEEFIKR++ (has ok)
            const string peptideSequence3 = "DNAGAATEEFIKR"; // Not L10N
            RunUI(() =>
            {
                viewLibraryDlg.FilterString = peptideSequence3 + "++"; // Not L10N
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
                viewLibraryDlg.FilterString = peptideSequence3 + "+++"; // Not L10N
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
            RunUI(() => SkylineWindow.SaveDocument(GetTestPath(@"15N_library_peptides.sky")));
            RunUI(() => SkylineWindow.NewDocument());

            // Neutral Losses p. 13
            PeptideSettingsUI settingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
                      {
                          settingsUI.SelectedTab = PeptideSettingsUI.TABS.Modifications;
                          settingsUI.PickedStaticMods = new[] { carbamidoName }; // Not L10N
                          settingsUI.PickedHeavyMods = new string[0];
                          settingsUI.PickedLibraries = new string[0];
                      });
            PauseForScreenShot<PeptideSettingsUI.ModificationsTab>("Peptide Settings - Modifications tab", 14);

            var editListUI1 =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(settingsUI.EditLibraryList);
            const string humanPhosphoLibName = "Human Phospho"; // Not L10N
            RunDlg<EditLibraryDlg>(editListUI1.AddItem, editLibraryDlg =>
            {
                editLibraryDlg.LibrarySpec =
                    new BiblioSpecLibSpec(humanPhosphoLibName, GetTestPath(@"phospho.blib"));
                editLibraryDlg.OkDialog();
            });
            OkDialog(editListUI1, editListUI1.OkDialog);
            RunUI(() =>
                      {
                          settingsUI.SelectedTab = PeptideSettingsUI.TABS.Library;
                          settingsUI.PickedLibraries = new[] {humanPhosphoLibName};
                      });
            PauseForScreenShot<PeptideSettingsUI.LibraryTab>("Peptide Settings - Library tab", 15);

            {
                var msgDlg = ShowDialog<MultiButtonMsgDlg>(() => settingsUI.ShowViewLibraryDlg());
                PauseForScreenShot<MultiButtonMsgDlg>("Save changes", 15);

                OkDialog(msgDlg, msgDlg.Btn0Click);
            }

            Assert.IsTrue(WaitForCondition(() =>
                SkylineWindow.Document.Settings.PeptideSettings.Modifications.StaticModifications.Count == 1 &&
                !SkylineWindow.Document.Settings.PeptideSettings.Modifications.AllHeavyModifications.Any() &&
                SkylineWindow.Document.Settings.PeptideSettings.Libraries.IsLoaded &&
                SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Count > 0));

            // Ignore modification matching form
            var matchedPepModsDlg = WaitForOpenForm<AddModificationsDlg>();
            OkDialog(matchedPepModsDlg,matchedPepModsDlg.CancelDialog);

            var viewLibraryDlg1 = WaitForOpenForm<ViewLibraryDlg>();
            const int countLabels1 = 15;
            const int countLabels2 = 18;
            RunUI(() =>
            {
                viewLibraryDlg1.FilterString = "AISS"; // Not L10N
                Assert.AreEqual(2, viewLibraryDlg1.PeptideDisplayCount);
                Assert.AreEqual(countLabels1, viewLibraryDlg1.GraphItem.IonLabels.Count());
            });
            PauseForScreenShot<GraphSpectrum>("Spectrum graph metafile", 16);   // p. 16, figure 1a
            RunUI(() =>
            {
                viewLibraryDlg1.SelectedIndex = 1;
                Assert.AreEqual(countLabels2, viewLibraryDlg1.GraphItem.IonLabels.Count());
            });
            PauseForScreenShot<GraphSpectrum>("Spectrum graph metafile", 16);   // p. 16, figure 1b

            docInitial = SkylineWindow.Document;

            var peptideSettingsUI2 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            const string phosphoModName = "Phospho (ST)"; // Not L10N 
            var phosphoSt = new StaticMod(phosphoModName, "S, T", null, true, "HPO3", LabelAtoms.None, // Not L10N
                                          RelativeRT.Unknown, null, null, new[] { new FragmentLoss("H3PO4"), }); // Not L10N
            AddStaticMod(phosphoSt, peptideSettingsUI2, "Edit Structural Modification form", 17);

            // Check Phospho (ST) and Carbamidomethyl Cysteine
            RunUI(() => peptideSettingsUI2.PickedStaticMods = new[] { phosphoModName, carbamidoName }); // Not L10N
            OkDialog(peptideSettingsUI2, peptideSettingsUI2.OkDialog);

            var docPhospho = WaitForDocumentChange(docInitial);
            Assert.IsTrue(docPhospho.Settings.PeptideSettings.Modifications.StaticModifications.Count == 2);

            string lossText = Math.Round(-phosphoSt.Losses[0].MonoisotopicMass, 1).ToString(LocalizationHelper.CurrentCulture);
            const int countLossLabels1 = 12;
            const int countLossLabels2 = 15;
            const int countPrecursors1 = 1;
            const int countPrecursors2 = 2;
            
            RunUI(() =>
            {
                // New ions should be labeled, because of the added modification with loss
                viewLibraryDlg1.SelectedIndex = 0;
                Assert.AreEqual(countLabels1 + countLossLabels1, viewLibraryDlg1.GraphItem.IonLabels.Count());

                viewLibraryDlg1.GraphSettings.ShowPrecursorIon = true;

                // Make sure the precursor -98 ion was added
                var labelsPhospho = viewLibraryDlg1.GraphItem.IonLabels.ToList();
                Assert.AreEqual(countLossLabels1 + 1, labelsPhospho.Count(label => label.Contains(lossText)));
                Assert.IsTrue(labelsPhospho.Contains(label => label.Contains(string.Format("{0} {1}", IonType.precursor.GetLocalizedString(), lossText)))); 
                Assert.AreEqual(countLabels1 + countLossLabels1 + countPrecursors1, labelsPhospho.Count);
            });
            PauseForScreenShot<GraphSpectrum>("Spectrum graph metafile", 18);   // p. 18, figure 1a.

            RunUI(() =>
            {
                viewLibraryDlg1.SelectedIndex = 1;
                var labelsPhospho = viewLibraryDlg1.GraphItem.IonLabels.ToList();
                Assert.AreEqual(countLossLabels2 + 1, labelsPhospho.Count(label => label.Contains(lossText)));
                Assert.IsTrue(labelsPhospho.Contains(label => label.Contains(string.Format("{0} {1}", IonType.precursor.GetLocalizedString(), lossText))));
                Assert.AreEqual(countLabels2 + countLossLabels2 + countPrecursors2, labelsPhospho.Count);
            });
            PauseForScreenShot<GraphSpectrum>("Spectrum graph metafile", 18);   // p. 18, figure 1b.

            // Matching Library Peptides to Proteins p. 18
            var peptideSettingsUI3 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var buildBackgroundProteomeDlg =
                ShowDialog<BuildBackgroundProteomeDlg>(peptideSettingsUI3.ShowBuildBackgroundProteomeDlg);
            RunUI(() =>
            {
                buildBackgroundProteomeDlg.BackgroundProteomePath = GetTestPath(@"human.protdb");
                buildBackgroundProteomeDlg.BackgroundProteomeName = "Human (mini)";
            });
            PauseForScreenShot<BuildBackgroundProteomeDlg>("Edit Background Proteome", 19);   // p. 19

            OkDialog(buildBackgroundProteomeDlg, buildBackgroundProteomeDlg.OkDialog);
            
            // Select Max Missed Cleavages to be 2 
            RunUI(() =>
            {
                peptideSettingsUI3.MissedCleavages = 2;
            });
            OkDialog(peptideSettingsUI3, peptideSettingsUI3.OkDialog);
            
            WaitForBackgroundProteomeLoaderCompleted(); // wait for protDB to populate protein metadata

            RunUI(() =>
            {
                viewLibraryDlg1.FilterString = string.Empty;
                Assert.AreEqual(100, viewLibraryDlg1.PeptideDisplayCount);
            });

            // Check Associate Proteins check box on Spectral Library Explorer form
            RunUI(() =>
            {
                viewLibraryDlg1.AssociateMatchingProteins = true;
            });

            docInitial = WaitForProteinMetadataBackgroundLoaderCompletedUI();

            var confirmUpgrade = ShowDialog<AlertDlg>(viewLibraryDlg1.AddAllPeptides);
            // Add everything in the library to the document.
            var filterMatchedPeptidesDlg = 
                ShowDialog<FilterMatchedPeptidesDlg>(confirmUpgrade.ClickYes);
            
            RunUI(() =>
                      {
                          filterMatchedPeptidesDlg.DuplicateProteinsFilter = 
                              BackgroundProteome.DuplicateProteinsFilter.FirstOccurence;
                          filterMatchedPeptidesDlg.AddUnmatched = true;
                          filterMatchedPeptidesDlg.AddFiltered = true;

                          // Checks that Peptides were added with the correct buttons selected...
                          Assert.AreEqual(163, filterMatchedPeptidesDlg.DuplicateMatchesCount);
                          Assert.AreEqual(2, filterMatchedPeptidesDlg.UnmatchedCount);
                      });
            PauseForScreenShot<FilterMatchedPeptidesDlg>("Filter Peptides", 20);   // p. 20, figure 1

            {
                var msgDlg = ShowDialog<MultiButtonMsgDlg>(filterMatchedPeptidesDlg.OkDialog);
                PauseForScreenShot("Message form", 20);   // p. 20, figure 2

                OkDialog(msgDlg, msgDlg.Btn1Click);
            }
            OkDialog(viewLibraryDlg1, viewLibraryDlg1.CancelDialog);

            var docProteins = WaitForDocumentChange(docInitial);

            AssertEx.IsDocumentState(docProteins, null, 250, 346, 347, 1041);

            RestoreViewOnScreen(21);
            PauseForScreenShot("Main window", 21);

            OkDialog(viewLibraryDlg, viewLibraryDlg.Close);
        }
    }
}
