/*
 * Original author: Brian Pratt <bspratt .at. protein.ms>,
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

using System.Drawing;
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
    public class AssociateProteinsTest : AbstractFunctionalTestEx
    {

        [TestMethod]
        // Borrows heavily from SrmTutorialTest, but this isn't a real tutorial test - just exploiting 
        // a convenient framework for testing Associate Proteins
        public void TestPasteTransitionListAssociateProteins()
        {
            TestFilesZipPaths = new[]
            {
                @"https://skyline.gs.washington.edu/tutorials/SrmTutorialTest.zip",
                @"TestTutorial\SRMViews.zip"
            };
            RunFunctionalTest();
        }


        private string GetTestPath(string relativePath)
        {
            const string folder = "USB";
            return TestFilesDirs[0].GetTestPath(Path.Combine(folder, relativePath));
        }

        protected override void DoTest()
        {

            //Tutorial 1
            string fastaFile = GetTestPath("Tutorial-1_Settings/TubercuList_v2-6.fasta.txt");
            WaitForCondition(() => File.Exists(fastaFile));
            var pepSettings = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                SkylineWindow.Size = new Size(1600, 800);
                pepSettings.SelectedTab = PeptideSettingsUI.TABS.Digest;
                pepSettings.ComboEnzymeSelected = "Trypsin [KR | P]";
                pepSettings.MaxMissedCleavages = 0;
            });

            var backProteomeDlg = ShowDialog<BuildBackgroundProteomeDlg>(pepSettings.AddBackgroundProteome);
            RunUI(() =>
            {
                backProteomeDlg.BackgroundProteomePath = GetTestPath("Skyline");
                backProteomeDlg.BackgroundProteomeName = "TubercuList_v2-6";
            });
            AddFastaToBackgroundProteome(backProteomeDlg, fastaFile, 40);
            RunUI(() => AssertEx.IsTrue(backProteomeDlg.StatusText.Contains(3982.ToString(CultureInfo.CurrentCulture))));
            OkDialog(backProteomeDlg, backProteomeDlg.OkDialog);

            RunUI(() => pepSettings.SelectedTab = PeptideSettingsUI.TABS.Prediction);

            RunUI(() =>
            {
                pepSettings.SelectedTab = PeptideSettingsUI.TABS.Filter;
                pepSettings.TextMinLength = 7;
                pepSettings.TextMaxLength = 25;
                pepSettings.TextExcludeAAs = 0;
                pepSettings.AutoSelectMatchingPeptides = true;
            });

            RunUI(() => pepSettings.SelectedTab = PeptideSettingsUI.TABS.Library);
            var editListDlg =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(pepSettings.EditLibraryList);
            var editLibraryDlg = ShowDialog<EditLibraryDlg>(editListDlg.AddItem);
            RunUI(() =>
            {
                editLibraryDlg.LibraryName = "Mtb_proteome_library";
                editLibraryDlg.LibraryPath = GetTestPath("Skyline\\Mtb_DirtyPeptides_QT_filtered_cons.sptxt");
            });
            OkDialog(editLibraryDlg, editLibraryDlg.OkDialog);
            OkDialog(editListDlg, editListDlg.OkDialog);
            RunUI(() => pepSettings.SetLibraryChecked(0, true));

            RunUI(() => { pepSettings.SelectedTab = PeptideSettingsUI.TABS.Modifications; });
            var editHeavyModListDlg =
                ShowDialog<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(pepSettings.EditHeavyMods);
            var addDlgOne = ShowDialog<EditStaticModDlg>(editHeavyModListDlg.AddItem);
            RunUI(() => addDlgOne.SetModification("Label:13C(6)15N(2) (C-term K)"));
            OkDialog(addDlgOne, addDlgOne.OkDialog);
            var addDlgTwo = ShowDialog<EditStaticModDlg>(editHeavyModListDlg.AddItem);
            RunUI(() => addDlgTwo.SetModification("Label:13C(6)15N(4) (C-term R)"));
            OkDialog(addDlgTwo, addDlgTwo.OkDialog);
            OkDialog(editHeavyModListDlg, editHeavyModListDlg.OkDialog);
            RunUI(() =>
            {
                pepSettings.SetIsotopeModifications(0, true);
                pepSettings.SetIsotopeModifications(1, true);
            });

            var docBeforePeptideSettings = SkylineWindow.Document;
            OkDialog(pepSettings, pepSettings.OkDialog);
            WaitForDocumentChangeLoaded(docBeforePeptideSettings);

            var transitionDlg = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionDlg.SelectedTab = TransitionSettingsUI.TABS.Prediction;
                transitionDlg.PrecursorMassType = MassType.Monoisotopic;
                transitionDlg.FragmentMassType = MassType.Monoisotopic;
                transitionDlg.RegressionCEName = "SCIEX";
            });

            RunUI(() =>
            {
                transitionDlg.SelectedTab = TransitionSettingsUI.TABS.Filter;
                transitionDlg.PrecursorCharges = "2,3".ToString(CultureInfo.CurrentCulture);
                transitionDlg.ProductCharges = "1,2".ToString(CultureInfo.CurrentCulture);
                transitionDlg.FragmentTypes = "y";
                transitionDlg.RangeFrom = Resources.TransitionFilter_FragmentStartFinders_ion_1;
                transitionDlg.RangeTo = Resources.TransitionFilter_FragmentEndFinders_last_ion;
                transitionDlg.SetListAlwaysAdd(0, false);
                transitionDlg.ExclusionWindow = 5;
                transitionDlg.SetAutoSelect = true;
            });

            RunUI(() =>
            {
                transitionDlg.SelectedTab = TransitionSettingsUI.TABS.Library;
                transitionDlg.IonMatchTolerance = 1.0;
                transitionDlg.UseLibraryPick = true;
                transitionDlg.IonCount = 5;
                transitionDlg.Filtered = true;
            });

            RunUI(() =>
            {
                transitionDlg.SelectedTab = TransitionSettingsUI.TABS.Instrument;
                transitionDlg.MinMz = 300;
                transitionDlg.MaxMz = 1250;
            });

            var docBeforeTransitionSettings = SkylineWindow.Document;
            OkDialog(transitionDlg, transitionDlg.OkDialog);
            WaitForDocumentChangeLoaded(docBeforeTransitionSettings);

            TestProteinReassignmentMessage();

            // Try importing a transition list with a peptide matching multiple proteins
            var multipleMatches = "VTTSTGASYSYDR, 709.327105, 1217.530841\n" +
                                  "VTTSTGASYSYDR, 709.327105, 1116.483162\n" +
                                  "AADD, 391.14600, 391.14600\n" +
                                  "VTTSTGASYSYDR, 709.327105, 928.403455";
            for (var i = 0; i < 2; i++)
            {
                ImportTransitions(multipleMatches, BackgroundProteome.DuplicateProteinsFilter.AddToAll, true);
                AssertEx.IsDocumentState(SkylineWindow.Document, null, 68, 68, 70);
                RunUI(() => SkylineWindow.Undo());
                // Do that again without Associate Proteins
                ImportTransitions(multipleMatches, BackgroundProteome.DuplicateProteinsFilter.AddToAll, false);
                AssertEx.IsDocumentState(SkylineWindow.Document, null, 1, 3, 4);
                RunUI(() => SkylineWindow.Undo());
                // Paste the same list, but this time select "No duplicates" on peptides with multiple matches
                ImportTransitions(multipleMatches, BackgroundProteome.DuplicateProteinsFilter.NoDuplicates, true);
                AssertEx.IsDocumentState(SkylineWindow.Document, null, 1, 1, 3);
                RunUI(() => SkylineWindow.Undo());
                // Paste the same list, but this time select "Use first occurrence" on peptides with multiple matches
                ImportTransitions(multipleMatches, BackgroundProteome.DuplicateProteinsFilter.FirstOccurence, true);
                AssertEx.IsDocumentState(SkylineWindow.Document, null, 2, 2, 4);
                RunUI(() => SkylineWindow.Undo());
                // Now add headers and do everything again
                multipleMatches = multipleMatches.Insert(0, "Peptide Modified Sequence, Precursor m/z, Product m/z\n");
            }
            // Try importing a transition list with a transition that does not match anything from the 
            // background proteome
            var noMatchesCSV = "VTTSTGASYSYDR, 709.327105, 1217.530841\n" +
                               "VTTSTGADRAAAA, 1191.596, 1191.596\n" +
                               "VTTSTGASYSYDR, 709.327105, 1029.451134";
            ImportTransitions(noMatchesCSV, BackgroundProteome.DuplicateProteinsFilter.AddToAll, true);
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 2, 2, 3);

        }

        
        private void ImportTransitions(string transitions,
            BackgroundProteome.DuplicateProteinsFilter filter, 
            bool associateProteins,
            bool addUnmatched = true, bool expectError = false)
        {
            // Paste into the targets window
            var importDialog = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            var colDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDialog.TransitionListText = transitions);

            if (associateProteins)
            {
                // FilterMatchedPeptidesDlg should appear
                var filterMatchedDlg =
                    ShowDialog<FilterMatchedPeptidesDlg>(() => colDlg.checkBoxAssociateProteins.Checked = true);
                RunUI(() =>
                {
                    filterMatchedDlg.AddUnmatched = addUnmatched;
                    filterMatchedDlg.DuplicateProteinsFilter = filter;
                    filterMatchedDlg.OkDialog();
                });
            }
            if (expectError)
            {
                // Ignore error dialog
                var colErrorDlg = ShowDialog<ImportTransitionListErrorDlg>(() => colDlg.OkDialog());
                OkDialog(colErrorDlg, colErrorDlg.AcceptButton.PerformClick);
            }
            else
            {
                RunUI(() =>colDlg.OkDialog());
            }

            WaitForDocumentChange(SkylineWindow.Document);
            WaitForClosedForm(importDialog);
        }

        private void TestProteinReassignmentMessage()
        {
            // Try importing a list with a protein name column
            var protColumnTSV = "VTTSTGASYSYDR, 709.327105, 1217.530841, Rv1812c_Rv1812c\n" +
                                "VTTSTGASYSYDR, 709.327105, 1116.483162, Rv1812c_Rv1812c\n" +
                                "VTTSTGASYSYDR, 709.327105, 1029.451134, Rv1812c_Rv1812c";
            var importDlg = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            var colDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDlg.TransitionListText = protColumnTSV);
            var proteinIndex = 0;
            // Test our warning when the user associates proteins and then tries to reassign the protein name column
            RunUI(() =>
            {
                colDlg.checkBoxAssociateProteins.Checked = false;
                AssertEx.AreEqual(4, colDlg.ComboBoxes.Count);
                proteinIndex = colDlg.ComboBoxes[3].FindStringExact(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name);
                colDlg.ComboBoxes[3].SelectedIndex = proteinIndex; // Declare protein name
            });
            RunUI(() =>
            {
                colDlg.checkBoxAssociateProteins.Checked = true; // This should override the declared protein name
            });
            WaitForConditionUI(() => colDlg.ComboBoxes.Count == 5); // Associated protein column appears
            WaitForConditionUI(() => colDlg.ComboBoxes[4].SelectedIndex == 0); // Former protein column goes to "Ignore Column"
            // Try to set protein column back to "protein name" and you get an error message since Associate Proteins handles that
            var messageDlg = ShowDialog<MessageDlg>(() =>
            {
                colDlg.ComboBoxes[4].SelectedIndex = proteinIndex;
            });
            OkDialog(messageDlg, messageDlg.CancelDialog); // Cancel, and column selection should be left alone 
            WaitForConditionUI(() => colDlg.ComboBoxes[0].SelectedIndex == proteinIndex);
            WaitForConditionUI(() => colDlg.ComboBoxes[4].SelectedIndex == 0); // Former protein column goes back to "Ignore Column"
            RunUI(() =>  colDlg.OkDialog() );
            WaitForDocumentChange(SkylineWindow.Document);
            WaitForClosedForm(importDlg);
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 1, 1, 3);
            RunUI(() => SkylineWindow.Undo());
        }
    }
}
