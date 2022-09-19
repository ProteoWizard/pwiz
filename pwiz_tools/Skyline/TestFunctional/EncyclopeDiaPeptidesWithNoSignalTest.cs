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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests a scenario where the .elib contains peak boundaries for a peptide, but
    /// no spectrum or retention time detection information.
    /// </summary>
    [TestClass]
    public class EncyclopeDiaPeptidesWithNoSignalTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestEncyclopeDiaPeptidesWithNoSignal()
        {
            TestFilesZip = @"TestFunctional\EncyclopeDiaPeptidesWithNoSignalTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            string fastaFilePath = TestFilesDir.GetTestPath(@"proteins.fasta");
            RunUI(()=>SkylineWindow.SaveDocument(TestFilesDir.GetTestPath(@"EncyclopeDiaMissingPeptidesTest.sky")));

            // Use the Import Peptide Search wizard to import the peptides from the "quantreport.elib"
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsDlg =>
            {
                peptideSettingsDlg.SelectedTab = PeptideSettingsUI.TABS.Digest;
                peptideSettingsDlg.MaxMissedCleavages = 1;
                peptideSettingsDlg.SelectedTab = PeptideSettingsUI.TABS.Filter;
                peptideSettingsDlg.TextMinLength = 4;
                peptideSettingsDlg.TextExcludeAAs = 0;
                peptideSettingsDlg.OkDialog();
            });
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);
            RunUI(() =>
            {
                var buildPepSearchCtrl = importPeptideSearchDlg.BuildPepSearchLibControl;
                buildPepSearchCtrl.WorkflowType = ImportPeptideSearchDlg.Workflow.dia;
                buildPepSearchCtrl.UseExistingLibrary = true;
                buildPepSearchCtrl.ExistingLibraryPath = TestFilesDir.GetTestPath("quantreport.elib");
                // Go to the "Extract Chromatograms" page
                importPeptideSearchDlg.ClickNextButton();
            });
            // Go to the "Add Modifications" page and ignore the message about not having selected any chromatogram files
            RunDlg<MultiButtonMsgDlg>(()=>importPeptideSearchDlg.ClickNextButton(), multiButtonMsgDlg=>multiButtonMsgDlg.ClickYes());
            RunUI(() =>
            {
                // Go to the Transition Settings page
                importPeptideSearchDlg.ClickNextButton();
                importPeptideSearchDlg.TransitionSettingsControl.MinIonCount = 0;
                importPeptideSearchDlg.TransitionSettingsControl.IonCount = 6;
                // Go to the Full Scan Settings page
                importPeptideSearchDlg.ClickNextButton();
                // Go to the Import FASTA page
                importPeptideSearchDlg.ClickNextButton();
                
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(fastaFilePath);
            });
            var peptidesPerProteinDlg = ShowDialog<AssociateProteinsDlg>(() => importPeptideSearchDlg.ClickNextButton());
            WaitForConditionUI(() => peptidesPerProteinDlg.DocumentFinalCalculated);
            OkDialog(peptidesPerProteinDlg, peptidesPerProteinDlg.OkDialog);

            // The peptide "AGYAHFLNVQGR" was not detected in "quantreport.elib", so Skyline is not supposed to find a spectrum in
            // the library, but the library does have peak boundaries.
            PeptideLibraryKey noSignalPeptide = new PeptideLibraryKey("AGYAHFLNVQGR", 3);
            PeptideLibraryKey goodPeptideInQuantLibrary = new PeptideLibraryKey("AGYTDKVVIGMDVAASEFFR", 3);
            Assert.IsNull(FindPeptideWithSequence(SkylineWindow.Document, noSignalPeptide.UnmodifiedSequence));
            Assert.IsNotNull(FindPeptideWithSequence(SkylineWindow.Document, goodPeptideInQuantLibrary.UnmodifiedSequence));

            var quantLibrary = SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.First();
            Assert.IsInstanceOfType(quantLibrary, typeof(EncyclopeDiaLibrary));

            var noSignalRetentionTimes = GetAllRetentionTimes(quantLibrary, noSignalPeptide);
            Assert.AreEqual(0, noSignalRetentionTimes.Count);
            var noSignalPeakBounds = GetAllExplicitPeakBounds(quantLibrary, noSignalPeptide.Target);
            Assert.AreNotEqual(0, noSignalPeakBounds.Count);
            CollectionAssert.DoesNotContain(noSignalPeakBounds, null);

            var goodPeptideRetentionTimes = GetAllRetentionTimes(quantLibrary, goodPeptideInQuantLibrary);
            Assert.AreNotEqual(0, goodPeptideRetentionTimes);
            var goodPeptidePeakBounds = GetAllExplicitPeakBounds(quantLibrary, goodPeptideInQuantLibrary.Target);
            Assert.AreNotEqual(0, goodPeptidePeakBounds.Count);
            CollectionAssert.DoesNotContain(goodPeptidePeakBounds, null);

            // Add the library "chromlib.elib" and make sure it appears before "quantreport.elib"
            bool peptideSettingsClosed = false;
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(()=>
            {
                SkylineWindow.ShowPeptideSettingsUI();
                peptideSettingsClosed = true;
            });
            RunUI(() =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Library;
            });
            var libListDlg =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUi.EditLibraryList);
            const string chromLibName = "chromlib";
            RunDlg<EditLibraryDlg>(libListDlg.AddItem, editLibraryDlg =>
            {
                editLibraryDlg.LibraryName = chromLibName;
                editLibraryDlg.LibraryPath = TestFilesDir.GetTestPath("chromlib.elib");
                editLibraryDlg.OkDialog();
            });
            RunUI(() =>
            {
                libListDlg.MoveItemUp();
            });
            OkDialog(libListDlg, libListDlg.OkDialog);
            RunUI(() =>
            {
                Assert.AreEqual(2, peptideSettingsUi.AvailableLibraries.Length);
                Assert.AreEqual(chromLibName, peptideSettingsUi.AvailableLibraries[0]);
                peptideSettingsUi.PickedLibraries = peptideSettingsUi.AvailableLibraries;
            });
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            WaitForConditionUI(() => peptideSettingsClosed);
            var chromLib = SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries[0];
            Assert.IsNotNull(chromLib);
            Assert.AreEqual(chromLibName, chromLib.Name);

            var noSignalRetentionTimesInChromLib = GetAllRetentionTimes(chromLib, noSignalPeptide);
            Assert.AreNotEqual(0, noSignalRetentionTimesInChromLib);

            // The peptide "AGYAHFLNVQGR" was detected in chromlib.elib, so it should now appear when we import the FASTA file
            RunUI(()=>SkylineWindow.ImportFastaFile(fastaFilePath));
            Assert.IsNotNull(FindPeptideWithSequence(SkylineWindow.Document, noSignalPeptide.UnmodifiedSequence));
        }

        private List<double> GetAllRetentionTimes(Library library, LibraryKey libraryKey)
        {
            var result = new List<double>();
            foreach (var filePath in library.LibraryFiles.FilePaths)
            {
                var msDataFilePath = new MsDataFilePath(filePath);
                if (library.TryGetRetentionTimes(libraryKey, msDataFilePath, out double[] retentionTimes))
                {
                    result.AddRange(retentionTimes);
                }
            }

            return result;
        }

        private List<ExplicitPeakBounds> GetAllExplicitPeakBounds(Library library, Target target)
        {
            var result = new List<ExplicitPeakBounds>();
            foreach (var filePath in library.LibraryFiles.FilePaths)
            {
                var msDataFilePath = new MsDataFilePath(filePath);
                var peakBounds = library.GetExplicitPeakBounds(msDataFilePath, new[] { target });
                if (peakBounds != null)
                {
                    result.Add(peakBounds);
                }
            }

            return result;
        }

        private PeptideDocNode FindPeptideWithSequence(SrmDocument document, string sequence)
        {
            return document.Peptides.FirstOrDefault(pep => pep.Peptide.Sequence == sequence);
        }
    }
}
