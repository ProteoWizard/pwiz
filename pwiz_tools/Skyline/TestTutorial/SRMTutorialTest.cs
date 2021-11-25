/*
 * Original author: Max Horowitz-Gelb <maxhg .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    [TestClass]
    public class SrmTutorialTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestSrmTutorialLegacy()
        {
            //Set true to look at tutorial screenshots
            //IsPauseForScreenShots = true;
            TestFilesZipPaths = new[]
            {
                @"https://skyline.gs.washington.edu/tutorials/SrmTutorialTest.zip",
                @"TestTutorial\SRMViews.zip"
            };
            RunFunctionalTest();
        }

        [TestMethod]
        // This isn't a real tutorial test - just exploiting a convenient framework for testing Associate Proteins
        public void TestSrmTutorialLegacyWithAssociateProteins()
        {
            _exerciseAssociateProteins = true;
            TestSrmTutorialLegacy();
        }

        private bool _exerciseAssociateProteins;

        private string GetTestPath(string relativePath)
        {
            const string folder = "USB";
            return TestFilesDirs[0].GetTestPath(Path.Combine(folder, relativePath));
        }

        protected override void DoTest()
        {
            LinkPdf = "http://targetedproteomics.ethz.ch/tutorials2014/Tutorial-1_Settings.pdf";

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
            RunUI(() => Assert.IsTrue(backProteomeDlg.StatusText.Contains(3982.ToString(CultureInfo.CurrentCulture))));
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
            PauseForScreenShot("Isotope modification 1", 3);
            OkDialog(addDlgOne, addDlgOne.OkDialog);
            var addDlgTwo = ShowDialog<EditStaticModDlg>(editHeavyModListDlg.AddItem);
            RunUI(() => addDlgTwo.SetModification("Label:13C(6)15N(4) (C-term R)"));
            PauseForScreenShot("Isotope modification 2", 3);
            OkDialog(addDlgTwo, addDlgTwo.OkDialog);
            OkDialog(editHeavyModListDlg, editHeavyModListDlg.OkDialog);
            RunUI(() =>
            {
                pepSettings.SetIsotopeModifications(0, true);
                pepSettings.SetIsotopeModifications(1, true);
            });
            RunUI(() => pepSettings.SelectedTab = PeptideSettingsUI.TABS.Digest);
            PauseForScreenShot("Digestion tab", 4);
            RunUI(() => pepSettings.SelectedTab = PeptideSettingsUI.TABS.Prediction);
            PauseForScreenShot("Prediction tab", 4);
            RunUI(() => pepSettings.SelectedTab = PeptideSettingsUI.TABS.Filter);
            PauseForScreenShot("Filter tab", 4);
            RunUI(() => pepSettings.SelectedTab = PeptideSettingsUI.TABS.Library);
            PauseForScreenShot("Library tab", 4);
            RunUI(() => pepSettings.SelectedTab = PeptideSettingsUI.TABS.Modifications);
            PauseForScreenShot("Modifications tab", 4);

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

            RunUI(() => transitionDlg.SelectedTab = TransitionSettingsUI.TABS.Prediction);
            PauseForScreenShot("Prediction Tab", 7);
            RunUI(() => transitionDlg.SelectedTab = TransitionSettingsUI.TABS.Filter);
            PauseForScreenShot("Filter Tab", 7);
            RunUI(() => transitionDlg.SelectedTab = TransitionSettingsUI.TABS.Library);
            PauseForScreenShot("Library Tab", 7);
            RunUI(() => transitionDlg.SelectedTab = TransitionSettingsUI.TABS.Instrument);
            PauseForScreenShot("Instrument Tab", 7);
            var docBeforeTransitionSettings = SkylineWindow.Document;
            OkDialog(transitionDlg, transitionDlg.OkDialog);
            WaitForDocumentChangeLoaded(docBeforeTransitionSettings);
            RunUI(() => SkylineWindow.SaveDocument(GetTestPath("Tutorial-1_Settings\\SRMcourse_20140210_Settings.sky")));


            //Tutorial 2
            LinkPdf = "http://targetedproteomics.ethz.ch//tutorials2014/Tutorial-2_TransitionList.pdf";

            RunUI(() => { });
            SetExcelFileClipboardText(GetTestPath(@"Tutorial-2_TransitionList\\target_peptides.xlsx"), "Sheet1", 1,
                false);
            var docBeforePaste = SkylineWindow.Document;
            var peptidePasteDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg);
            var matchingDlg = ShowDialog<FilterMatchedPeptidesDlg>(peptidePasteDlg.PastePeptides);
            PauseForScreenShot("Filter Peptides", 1);
            OkDialog(matchingDlg, matchingDlg.OkDialog);
            OkDialog(peptidePasteDlg, peptidePasteDlg.OkDialog);

            int expectedTrans = TransitionGroup.IsAvoidMismatchedIsotopeTransitions ? 324 : 331;
            var docAfterPaste = WaitForDocumentChange(docBeforePaste);
            AssertEx.IsDocumentState(docAfterPaste, null, 10, 30, 68, expectedTrans);
            RunUI(() =>
            {
                SkylineWindow.ExpandProteins();
                SkylineWindow.ExpandPeptides();
                SkylineWindow.ExpandPrecursors();
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].FirstNode.FirstNode;
            });
            PauseForScreenShot("Skyline Window", 2);

            // Test min ion count setting
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, dlg =>
            {
                dlg.MinIonCount = 3;
                dlg.OkDialog();
            });
            var docMinIons = WaitForDocumentChange(docAfterPaste);
            AssertEx.IsDocumentState(docMinIons, null, 10, 30, 66, 320);
            RunUI(() => SkylineWindow.Undo());
            docMinIons = WaitForDocumentChange(docMinIons);
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, dlg =>
            {
                dlg.MinIonCount = 4;
                dlg.OkDialog();
            });
            docMinIons = WaitForDocumentChange(docMinIons);
            AssertEx.IsDocumentState(docMinIons, null, 10, 30, 64, 314);
            RunUI(() => SkylineWindow.Undo());
            docMinIons = WaitForDocumentChange(docMinIons);
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, dlg =>
            {
                dlg.MinIonCount = 5;
                dlg.OkDialog();
            });
            docMinIons = WaitForDocumentChange(docMinIons);
            AssertEx.IsDocumentState(docMinIons, null, 10, 30, 58, 290);
            RunUI(() => SkylineWindow.Undo());
            WaitForDocumentChange(docMinIons);

            var exportDlg = ShowDialog<ExportMethodDlg>(SkylineWindow.ShowExportTransitionListDlg);
            RunUI(() =>
            {
                exportDlg.InstrumentType = "AB SCIEX";
                exportDlg.ExportStrategy = ExportStrategy.Single;
                exportDlg.DwellTime = 20;
            });
            PauseForScreenShot("Export Transition List", 3);
            var declusteringWarningDlg = ShowDialog<MultiButtonMsgDlg>(
                () => exportDlg.OkDialog(GetTestPath("Tutorial-2_TransitionList\\SRMcourse_20140210_MtbProteomeLib_TransList.csv")));
            PauseForScreenShot("Decluster Window", 3);
            OkDialog(declusteringWarningDlg, declusteringWarningDlg.Btn1Click);
            WaitForClosedForm(exportDlg);

            //Tutorial 3
            LinkPdf = "http://www.srmcourse.ch/tutorials2014/Tutorial-3_Library.pdf";
            var pepSettings2 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => pepSettings2.SelectedTab = PeptideSettingsUI.TABS.Library);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(pepSettings2.ShowBuildLibraryDlg);
            RunUI(() =>
            {
                buildLibraryDlg.LibraryPath = GetTestPath("Skyline");
                buildLibraryDlg.LibraryName = "Mtb_hDP_20140210";
                buildLibraryDlg.LibraryCutoff = 0.9;
            });
            PauseForScreenShot("Build Library Window", 2);
            RunUI(() =>
            {
                buildLibraryDlg.OkWizardPage();
                buildLibraryDlg.AddDirectory(GetTestPath("Tutorial-3_Library"));
            });
            PauseForScreenShot("Build Library Window Next", 2);
            OkDialog(buildLibraryDlg, buildLibraryDlg.OkWizardPage);
            RunUI(() =>
            {
                pepSettings2.SetLibraryChecked(0, true);
                pepSettings2.SetLibraryChecked(1, true);
            });
            OkDialog(pepSettings2, pepSettings2.OkDialog);
            WaitForLibrary(20213);
            WaitForCondition(() => SkylineWindow.Document.PeptideTransitionCount != expectedTrans);
            int expectedTransAfter = TransitionGroup.IsAvoidMismatchedIsotopeTransitions ? 464 : 470;
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 10, 30, 96, expectedTransAfter);

            var libraryExpl = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            var messageWarning = WaitForOpenForm<AddModificationsDlg>();
            RunUI(() => messageWarning.OkDialogAll());
            PauseForScreenShot("Spectral Library Explorer Window", 3);
            OkDialog(libraryExpl, libraryExpl.Close);

            var exportDlg2 = ShowDialog<ExportMethodDlg>(SkylineWindow.ShowExportTransitionListDlg);
            RunUI(() =>
            {
                exportDlg2.InstrumentType = "AB SCIEX";
                exportDlg2.ExportStrategy = ExportStrategy.Buckets;
                exportDlg2.MaxTransitions = 150;
                exportDlg2.DwellTime = 20;
                exportDlg2.IgnoreProteins = true;
            });
            var declusteringWarningDlg2 = ShowDialog<MultiButtonMsgDlg>(() =>
                exportDlg2.OkDialog(GetTestPath("Tutorial-3_Library\\SRMcourse_20140210_MtbProteomeLib_hDP_TransList.csv")));
            OkDialog(declusteringWarningDlg2, declusteringWarningDlg2.Btn1Click);
            WaitForClosedForm(exportDlg2);

            //Tutorial 4-A
            LinkPdf = "http://targetedproteomics.ethz.ch/tutorials2014/Tutorial-4_Parameters.pdf";

            SrmDocument smallDocument = SkylineWindow.Document;
            PeptideGroupDocNode[] newProteins = smallDocument.PeptideGroups.Take(0).ToArray();
            smallDocument = (SrmDocument) smallDocument.ChangeChildren(newProteins);

            Assert.IsTrue(SkylineWindow.SetDocument(smallDocument, SkylineWindow.Document)); // TODO: Must be a better way to do this

            var transitionSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettings.SelectedTab = TransitionSettingsUI.TABS.Prediction;
                transitionSettings.RegressionCEName = "ABI QTrap 4000";
            });
            var editCollisionEnergy = ShowDialog<EditCEDlg>(() =>
                transitionSettings.RegressionCEName = Resources.SettingsListComboDriver_Edit_current);
            RunUI(() =>
            {
                editCollisionEnergy.StepSize = 2;
                editCollisionEnergy.StepCount = 5;
            });
            PauseForScreenShot("Edit Collision Energy Equation Window", 2);
            OkDialog(editCollisionEnergy, editCollisionEnergy.OkDialog);
            OkDialog(transitionSettings, transitionSettings.OkDialog);
            
            // This is a convenient place to test associating proteins
            if (_exerciseAssociateProteins)
            {
                TestAssociateProteins();
            }

            SetExcelFileClipboardText(GetTestPath("Tutorial-4_Parameters\\transition_list_for_CEO.xlsx"), "Sheet1", 3,
                false);

            var importDialog3 = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            string impliedLabeled = GetExcelFileText(GetTestPath("Tutorial-4_Parameters\\transition_list_for_CEO.xlsx"), "Sheet1", 3,
                false);
            var col4Dlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDialog3.textBox1.Text = impliedLabeled);
            col4Dlg.SetSelectedColumnTypes(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence, 
                Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z);
            RunUI(() => {
                col4Dlg.checkBoxAssociateProteins.Checked = true;
            });

            OkDialog(col4Dlg, col4Dlg.OkDialog);

            AssertEx.IsDocumentState(SkylineWindow.Document, null, 10, 30, 30, 143);

            
            var exportDlg3 = ShowDialog<ExportMethodDlg>(SkylineWindow.ShowExportTransitionListDlg);
            RunUI(() =>
            {
                exportDlg3.InstrumentType = "AB SCIEX";
                exportDlg3.ExportStrategy = ExportStrategy.Single;
                exportDlg3.OptimizeType = ExportOptimize.CE;
                exportDlg3.MethodType = ExportMethodType.Standard;
                exportDlg2.DwellTime = 20;
            });
            PauseForScreenShot("Export Transition List", 3);
            var defaultWaringDlg = ShowDialog<MultiButtonMsgDlg>(
                () => exportDlg3.OkDialog(GetTestPath("Tutorial-4_Parameters\\SRMcourse_20140211_Parameters_CEO.csv")));
            OkDialog(defaultWaringDlg, defaultWaringDlg.Btn1Click);
            WaitForClosedForm(exportDlg3);

            var paths = new string[4];
            for (int i = 0; i < paths.Length; i ++)
            {
                paths[i] = GetTestPath("Tutorial-4_Parameters\\CEO_" + (i + 1) + ".wiff");
            }

            ImportResults("CEO", paths, ExportOptimize.CE);

            RestoreViewOnScreen(43);
            RunUI(() => SkylineWindow.ShowChromatogramLegends(false));
            PauseForScreenShot("Skyline Window", 3);

            ImportResults("", new[]
            {
                GetTestPath("Tutorial-4_Parameters\\CE_plus10.wiff"),
                GetTestPath("Tutorial-4_Parameters\\CE_minus10.wiff")
            }, ExportOptimize.NONE, false);

            RunUI(() =>
            {
                SkylineWindow.ShowAllTransitions();
                SkylineWindow.NormalizeAreaGraphTo(NormalizeOption.MAXIMUM);
            });
            RestoreViewOnScreen(44);
            PauseForScreenShot("Skyline Window", 4);

            var transitionSettings2 = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() => { transitionSettings2.SelectedTab = TransitionSettingsUI.TABS.Prediction; });
            var addCollisionEnergyDlg = ShowDialog<EditCEDlg>(transitionSettings2.AddToCEList);
            RunUI(() =>
            {
                addCollisionEnergyDlg.UseCurrentData();
                addCollisionEnergyDlg.RegressionName = "SRMcourse_20140211_Parameters_custom-CE-equation";
            });
            var equationGraphDlg = ShowDialog<GraphRegression>(addCollisionEnergyDlg.ShowGraph);
            PauseForScreenShot("Collision Energy Equation Graph", 6);
            OkDialog(equationGraphDlg, equationGraphDlg.CloseDialog);
            OkDialog(addCollisionEnergyDlg, addCollisionEnergyDlg.OkDialog);
            RunUI(() =>
            {
                transitionSettings2.RegressionCEName = "SRMcourse_20140211_Parameters_custom-CE-equation";
                transitionSettings2.SelectedTab = TransitionSettingsUI.TABS.Instrument;
                transitionSettings2.MZMatchTolerance = 0.01;
            });
            PauseForScreenShot("Instrument Tab", 7);
            OkDialog(transitionSettings2, transitionSettings2.OkDialog);

            //Tutorial 4-B
            var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            RunUI(manageResultsDlg.RemoveAllReplicates);
            OkDialog(manageResultsDlg, manageResultsDlg.OkDialog);
            string[] paths2 =
            {
                GetTestPath(string.Format("Tutorial-4_Parameters\\unscheduled_dwell{0}ms.wiff", 10)),
                GetTestPath(string.Format("Tutorial-4_Parameters\\unscheduled_dwell{0}ms.wiff", 20)),
                GetTestPath(string.Format("Tutorial-4_Parameters\\unscheduled_dwell{0}ms.wiff", 40)),
                GetTestPath(string.Format("Tutorial-4_Parameters\\unscheduled_dwell{0}ms.wiff", 60)),
                GetTestPath(string.Format("Tutorial-4_Parameters\\unscheduled_dwell{0}ms.wiff", 100))
            };

            ImportResults("", paths2, null, false);
            RestoreViewOnScreen(48);
            RunUI(() => SkylineWindow.AutoZoomBestPeak());
            PauseForScreenShot("Skyline Window", 8);
        }

        private void ImportResults(string prefix, string[] paths, string optimization, bool addNew = true)
        {
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            RunUI(() =>
            {
                if (addNew)
                {
                    importResultsDlg.RadioAddNewChecked = true;
                    importResultsDlg.ReplicateName = prefix;
                    importResultsDlg.OptimizationName = optimization;
                }
                var files = new MsDataFileUri[paths.Length];
                for (int i = 0; i < paths.Length; i++)
                {
                    files[i] = new MsDataFilePath(GetTestPath(paths[i]));
                }
                var keyPair = new KeyValuePair<string, MsDataFileUri[]>(prefix, files);
                KeyValuePair<string, MsDataFileUri[]>[] pathHolder = addNew
                    ? new[] {keyPair}
                    : importResultsDlg.GetDataSourcePathsFileReplicates(files);
                importResultsDlg.NamedPathSets = pathHolder;
            });
            if (addNew)
                OkDialog(importResultsDlg, importResultsDlg.OkDialog);
            else
            {
                var keepPrefixDlg = ShowDialog<ImportResultsNameDlg>(importResultsDlg.OkDialog);
                RunUI(keepPrefixDlg.NoDialog);
                WaitForClosedForm(keepPrefixDlg);
                WaitForClosedForm(importResultsDlg);
            }
            WaitForDocumentLoaded();
            WaitForClosedForm<AllChromatogramsGraph>();
        }

        private void TestAssociateProteins()
        {
            // Try importing a transition list with a peptide matching multiple proteins
            var multipleMatches = "VTTSTGASYSYDR, 709.327105, 1217.530841\n" +
                                     "VTTSTGASYSYD, 709.327105, 1116.483162\n" +
                                     "AADD, 391.14600, 391.14600\n" +
                                     "VTTSTGASYSYDR, 709.327105, 928.403455";
            for (var i = 0; i < 2; i++)
            {
                ImportTransitions(multipleMatches,
                    BackgroundProteome.DuplicateProteinsFilter.AddToAll);
                AssertEx.IsDocumentState(SkylineWindow.Document, null, 68, 68, 69);
                RunUI(() => SkylineWindow.Undo());
                // Paste the same list, but this time select "No duplicates" on peptides with multiple matches
                ImportTransitions(multipleMatches,
                    BackgroundProteome.DuplicateProteinsFilter.NoDuplicates);
                AssertEx.IsDocumentState(SkylineWindow.Document, null, 1, 1, 2);
                RunUI(() => SkylineWindow.Undo());
                // Paste the same list, but this time select "Use first occurrence" on peptides with multiple matches
                ImportTransitions(multipleMatches,
                    BackgroundProteome.DuplicateProteinsFilter.FirstOccurence);
                AssertEx.IsDocumentState(SkylineWindow.Document, null, 2, 2, 3);
                RunUI(() => SkylineWindow.Undo());
                // Now add headers and do everything again
                multipleMatches = multipleMatches.Insert(0, "Peptide Modified Sequence, Precursor m/z, Product m/z\n");
            }
            RunUI(() => SkylineWindow.Undo());
            // Try importing a transition list with a transition that does not match anything from the 
            // background proteome
            var noMatchesCSV = "VTTSTGASYSYDR, 709.327105, 1217.530841\n" +
                               "VTTSTGADRAAAA, 1191.596, 1191.596\n" +
                               "VTTSTGASYSYDR, 709.327105, 1029.451134";
            ImportTransitions(noMatchesCSV, BackgroundProteome.DuplicateProteinsFilter.AddToAll,  true, false);
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 2, 2, 3);
            RunUI(() => SkylineWindow.Undo());
            //TestProteinReassignmentMessage();

        }

        private void ImportTransitions(string transitions,
            BackgroundProteome.DuplicateProteinsFilter filter, bool addUnmatched = true, bool expectError = true)
        {
            // Paste into the targets window
            var importDialog = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            var colDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDialog.textBox1.Text = transitions);

            // FilterMatchedPeptidesDlg should appear
            var filterMatchedDlg =
                ShowDialog<FilterMatchedPeptidesDlg>(() => colDlg.checkBoxAssociateProteins.Checked = true);
            RunUI(() =>
            {
                filterMatchedDlg.AddUnmatched = addUnmatched;
                filterMatchedDlg.DuplicateProteinsFilter = filter;
                filterMatchedDlg.OkDialog();
            });

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
            var colDlg =
                ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDlg.textBox1.Text = protColumnTSV);

            // Test our warning when the user associates proteins and then tries to reassign the protein name column
            RunUI(() =>
            {
                colDlg.checkBoxAssociateProteins.Checked = true;
                var messageDlg = ShowDialog<MessageDlg>(() =>
                {
                    RunUI(() =>
                    {
                        colDlg.SetSelectedColumnTypes(null, null, null, null, Resources
                            .ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name);
                    });
                });
                Assert.IsNotNull(messageDlg);
                RunUI(() =>
                {
                    messageDlg.CancelButton.PerformClick();
                    colDlg.CancelButton.PerformClick();
                });

            });
        }
    }
}
