/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    /// <summary>
    /// Runs the PRM With an Orbitrap Mass Spec tutorial
    /// </summary>
    [TestClass]
    public class OrbiPrmTutorialTest : AbstractFunctionalTestEx
    {
        private const string EXT_ZIP = ".zip";

        [TestMethod]
        public void TestOrbiPrmTutorial()
        {
//            IsPauseForScreenShots = true;
//            RunPerfTests = true;
//            IsCoverShotMode = true;
            CoverShotName = "PRM-Obitrap";

            LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/PRM-Orbitrap-21_2.pdf";

            TestFilesZipPaths = new[]
            {
                @"http://skyline.ms/tutorials/PRM-Orbi.zip",
                @"TestPerf\OrbiPrmViews.zip",
            };

            const string rootName = "PRM-Orbi";
            string libraryDir = Path.Combine(rootName, "Heavy Library");
            string dataDir = Path.Combine(rootName, "PRM data");
            string samplesDir = Path.Combine(dataDir, "Samples");
            string standardsDir = Path.Combine(dataDir, "Standards");
            TestFilesPersistent = new[]
            {
                Path.Combine(libraryDir, "heavy-01.mzXML"),
                Path.Combine(libraryDir, "heavy-01.pep.xml"),
                Path.Combine(libraryDir, "heavy-02.mzXML"),
                Path.Combine(libraryDir, "heavy-02.pep.xml"),
                Path.Combine(samplesDir, "G1_rep1.mzXML"),
                Path.Combine(samplesDir, "G1_rep2.mzXML"),
                Path.Combine(samplesDir, "G1_rep3.mzXML"),
                Path.Combine(samplesDir, "G2M_rep1.mzXML"),
                Path.Combine(samplesDir, "G2M_rep2.mzXML"),
                Path.Combine(samplesDir, "G2M_rep3.mzXML"),
                Path.Combine(samplesDir, "S_rep1.mzXML"),
                Path.Combine(samplesDir, "S_rep2.mzXML"),
                Path.Combine(samplesDir, "S_rep3.mzXML"),
                Path.Combine(standardsDir, "heavy-PRM.mzXML"),
            };

            RunFunctionalTest();            
        }

        private string DataPath => TestFilesDirs.Last().PersistentFilesDir;

        private string GetTestPath(string relativePath)
        {
            return TestFilesDirs.First().GetTestPath(Path.Combine("PRM-Orbi", relativePath));
        }

        private const string HEAVY_LIBRARY = "heavy";
        private const string SHOTGUN_LIBRARY = "shotgun";

        private const string HEAVY_K = "Label:13C(6)15N(2) (C-term K)";
        private const string HEAVY_R = "Label:13C(6)15N(4) (C-term R)";

        protected override void DoTest()
        {
            // Have to open the start page from within Skyline to ensure audit logging starts up correctly
            var startPage = ShowDialog<StartPage>(SkylineWindow.OpenStartPage);

            PauseForScreenShot<StartPage>("Import Peptide List icon", 1);

            var startPageSettings = ShowDialog<StartPageSettingsUI>(() =>
                startPage.ClickWizardAction(Resources.SkylineStartup_SkylineStartup_Import_Peptide_List));

            RunUI(() => startPageSettings.IsIntegrateAll = true);
            PauseForScreenShot<StartPageSettingsUI>("Settings form", 2);

            RunDlg<MessageDlg>(startPageSettings.ResetDefaults, dlg => dlg.OkDialog());

            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(startPageSettings.ShowPeptideSettingsUI);

            RunUI(() =>
            {
                peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Digest;
                peptideSettingsUI.MissedCleavages = 1;
                peptideSettingsUI.ComboPeptideUniquenessConstraintSelected =
                    PeptideFilter.PeptideUniquenessConstraint.protein;
            });

            // Creating a Background Proteome File, p. 2-3
            const string protdbName = "mouse-proteome";
            string protdbPath = GetTestPath(protdbName + ProteomeDb.EXT_PROTDB);
            FileEx.SafeDelete(protdbPath);
            var proteomeDlg = ShowDialog<BuildBackgroundProteomeDlg>(peptideSettingsUI.ShowBuildBackgroundProteomeDlg);
            RunUI(() =>
            {
                proteomeDlg.BackgroundProteomeName = protdbName;
                proteomeDlg.CreateDb(protdbPath);
            });
            var messageRepeats =
                ShowDialog<MessageDlg>(() => proteomeDlg.AddFastaFile(GetTestPath("uniprot-mouse.fasta")));

            PauseForScreenShot("Repeats message", 3);

            OkDialog(messageRepeats, messageRepeats.OkDialog);

            // RunUI(proteomeDlg.SelToEndBackgroundProteomePath);
            // PauseForScreenShot<BuildBackgroundProteomeDlg>("Edit Background Proteome form", 5);

            OkDialog(proteomeDlg, proteomeDlg.OkDialog);

            PauseForScreenShot<PeptideSettingsUI.DigestionTab>("Peptide Settings - Digestion tab", 4);

            RunUI(() =>
            {
                peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                peptideSettingsUI.TimeWindow = 5;
            });

            PauseForScreenShot<PeptideSettingsUI.PredictionTab>("Peptide Settings - Prediction tab", 5);

            RunUI(() =>
            {
                peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Filter;
                peptideSettingsUI.TextMinLength = 7;
                peptideSettingsUI.TextMaxLength = 26;
                peptideSettingsUI.TextExcludeAAs = 0;
            });

            PauseForScreenShot<PeptideSettingsUI.FilterTab>("Peptide Settings - Filter tab", 7);

            RunUI(() =>
            {
                peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Library;
            });

            RunDlg<BuildLibraryDlg>(peptideSettingsUI.ShowBuildLibraryDlg, buildLibraryDlg =>
            {
                buildLibraryDlg.LibraryPath = GetTestPath(@"Heavy Library") + "\\";
                buildLibraryDlg.LibraryName = HEAVY_LIBRARY;
                buildLibraryDlg.LibraryCutoff = 0.9;
                buildLibraryDlg.OkWizardPage();
                IList<string> inputPaths = new List<string>
                {
                    GetTestPath(Path.Combine("Heavy Library", "heavy-01.pep.xml")),
                    GetTestPath(Path.Combine("Heavy Library", "heavy-02.pep.xml")),
                };
                buildLibraryDlg.AddInputFiles(inputPaths);
                buildLibraryDlg.OkWizardPage();
            });

            var editListUI =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUI.EditLibraryList);
            var addLibUI = ShowDialog<EditLibraryDlg>(editListUI.AddItem);
            RunUI(() => addLibUI.LibrarySpec =
                new BiblioSpecLibSpec(SHOTGUN_LIBRARY, GetTestPath(@"Shotgun Library\shotgun.blib")));
            OkDialog(addLibUI, addLibUI.OkDialog);
            RunUI(() => editListUI.MoveItemUp());   // Put shotgun at the top
            OkDialog(editListUI, editListUI.OkDialog);

            RunUI(() =>
            {
                peptideSettingsUI.PickedLibraries = new[] { SHOTGUN_LIBRARY, HEAVY_LIBRARY };
            });

            PauseForScreenShot<PeptideSettingsUI.LibraryTab>("Peptide Settings - Library tab", 8);

            RunUI(() =>
            {
                peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Modifications;
            });

            var modHeavyK = new StaticMod(HEAVY_K, "K", ModTerminus.C, false, null, LabelAtoms.C13 | LabelAtoms.N15, // Not L10N
                RelativeRT.Matching, null, null, null);
            AddHeavyMod(modHeavyK, peptideSettingsUI, "Edit Isotope Modification form", 9);
            var modHeavyR = new StaticMod(HEAVY_R, "R", ModTerminus.C, false, null, LabelAtoms.C13 | LabelAtoms.N15, // Not L10N
                RelativeRT.Matching, null, null, null);
            AddHeavyMod(modHeavyR, peptideSettingsUI, "Edit Isotope Modification form", 9);
            RunUI(() => peptideSettingsUI.PickedHeavyMods = new[] { HEAVY_K, HEAVY_R });

            PauseForScreenShot<PeptideSettingsUI.LibraryTab>("Peptide Settings - Modifications tab", 10);

            // TODO: Remove this after Nick's fix
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            var peptideSettingsUI2 = ShowDialog<PeptideSettingsUI>(startPageSettings.ShowPeptideSettingsUI);

            RunUI(() =>
            {
                peptideSettingsUI2.SelectedTab = PeptideSettingsUI.TABS.Quantification;
                peptideSettingsUI2.QuantNormalizationMethod = NormalizationMethod.FromIsotopeLabelTypeName("heavy");
                peptideSettingsUI2.QuantMsLevel = 2;
                peptideSettingsUI2.QuantUnits = "fmol";
            });

            PauseForScreenShot<PeptideSettingsUI.LibraryTab>("Peptide Settings - Quantification tab", 10);

            using (new WaitDocumentChange(null, true))
            {
                OkDialog(peptideSettingsUI2, peptideSettingsUI2.OkDialog);
            }

            var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(startPageSettings.ShowTransitionSettingsUI);

            RunUI(() =>
            {
                transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Prediction;
            });

            PauseForScreenShot<TransitionSettingsUI.PredictionTab>("Transition Settings - Prediction tab", 11);

            RunUI(() =>
            {
                transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Filter;
                transitionSettingsUI.PrecursorCharges = "2, 3";
                transitionSettingsUI.ProductCharges = "1, 2";
                transitionSettingsUI.FragmentTypes = "y, b";
                transitionSettingsUI.RangeFrom = Resources.TransitionFilter_FragmentStartFinders_ion_1;
                transitionSettingsUI.RangeTo = Resources.TransitionFilter_FragmentEndFinders_last_ion;
                transitionSettingsUI.SpecialIons = Array.Empty<string>();
                transitionSettingsUI.ExclusionWindow = 5;
            });

            PauseForScreenShot<TransitionSettingsUI.FilterTab> ("Transition Settings - Filter tab", 12);

            RunUI(() =>
            {
                transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Library;
                transitionSettingsUI.IonMatchTolerance = 0.05;
                transitionSettingsUI.IonCount = 10;
                transitionSettingsUI.MinIonCount = 3;
                transitionSettingsUI.Filtered = true;
            });

            PauseForScreenShot<TransitionSettingsUI.LibraryTab>("Transition Settings - Library tab", 13);

            RunUI(() =>
            {
                transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Instrument;
                transitionSettingsUI.MinMz = 340;
                transitionSettingsUI.MaxMz = 1200;
            });

            PauseForScreenShot<TransitionSettingsUI.InstrumentTab> ("Transition Settings - Instrument tab", 14);

            RunUI(() =>
            {
                transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                transitionSettingsUI.AcquisitionMethod = FullScanAcquisitionMethod.PRM;
                transitionSettingsUI.ProductMassAnalyzer = FullScanMassAnalyzerType.centroided;
                transitionSettingsUI.ProductRes = 10;
                transitionSettingsUI.RetentionTimeFilterType = RetentionTimeFilterType.none;
            });

            PauseForScreenShot<TransitionSettingsUI.FullScanTab> ("Transition Settings - Full-Scan tab", 15);

            OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);

            using (new CheckDocumentState(19, 31, 106, 882, null, true))
            {
                var pasteDlg = ShowDialog<PasteDlg>(startPageSettings.OkDialog);
                RunUI(() =>
                {
                    SetClipboardText(GetPeptideList());
                    pasteDlg.PastePeptides();
                });
                PauseForScreenShot<PasteDlg.ProteinListTab>("Insert Peptide List", 16); // Not L10N
                OkDialog(pasteDlg, pasteDlg.OkDialog);
            }

            RunUI(SkylineWindow.ExpandPrecursors);
            SelectNode(SrmDocument.Level.Molecules, 0);

            RestoreViewOnScreen(17);
            RunUI(() => SkylineWindow.Size = new Size(1100, 657));
            PauseForScreenShot<SkylineWindow>("Main window with peptide selected and Library Match view", 17);

            string documentFile = TestContext.GetTestPath("PRM_Scheduled" + SrmDocument.EXT);
            RunUI(() => SkylineWindow.SaveDocument(documentFile));
        }

        private string GetPeptideList()
        {
            var sb = new StringBuilder();
            // Skip the header line and then read all the modified sequences from the 3rd column (index 2)
            foreach (var line in File.ReadAllLines(GetTestPath("target_peptides.csv")).Skip(1))
                sb.AppendLine(line.ParseCsvFields()[2]);
            return sb.ToString();
        }
    }
}