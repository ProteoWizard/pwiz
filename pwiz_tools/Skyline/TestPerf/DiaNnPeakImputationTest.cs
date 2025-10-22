/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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

using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
    public class DiaNnPeakImputationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestDiaNnPeakImputation()
        {
            TestFilesZip = @"https://panoramaweb.org/_webdav/MacCoss/software/%40files/perftests/DiaNnPeakImputationTest.zip";
            TestFilesPersistent = new []{@"MZML\"};
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                peptideSettingsUi.ImputeMissingPeaks = true;
                peptideSettingsUi.OkDialog();
            });

            RunUI(()=>SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("PeakImputationTest.sky")));
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);
            RunUI(() =>
            {
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = ImportPeptideSearchDlg.Workflow.dia;
                importPeptideSearchDlg.BuildPepSearchLibControl.UseExistingLibrary = true;
                importPeptideSearchDlg.BuildPepSearchLibControl.ExistingLibraryPath =
                    TestFilesDir.GetTestPath("DiaNnPeakImputationTest.blib");
            });

            WaitForDocumentLoaded();
            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            RunUI(() =>
            {
                importPeptideSearchDlg.ClickNextButton();
            });

            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);
            var importResults = importPeptideSearchDlg.ImportResultsControl as ImportResultsDIAControl;
            Assert.IsNotNull(importResults);
            RunDlg<OpenDataSourceDialog>(
                () => importResults.Browse(Path.Combine(TestFilesDir.PersistentFilesDir, "MZML")), openDataSourceDlg =>
                {
                    openDataSourceDlg.SelectAllFileType(DataSourceUtil.EXT_MZML);
                    openDataSourceDlg.Open();
                });
            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            RunDlg<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton(),
                importResultsNameDlg => importResultsNameDlg.YesDialog());
            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage ==
                                     ImportPeptideSearchDlg.Pages.match_modifications_page);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));
            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.transition_settings_page);
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                Assert.AreEqual(ImportPeptideSearchDlg.Pages.full_scan_settings_page,
                    importPeptideSearchDlg.CurrentPage);
            });
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                Assert.AreEqual(ImportPeptideSearchDlg.Pages.import_fasta_page, importPeptideSearchDlg.CurrentPage);
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(TestFilesDir.GetTestPath("target_protein_sequences.fasta"));
            });
            RunLongDlg<AssociateProteinsDlg>(()=>importPeptideSearchDlg.ClickNextButton(), associateProteinsDlg =>
            {
                WaitForConditionUI(() => associateProteinsDlg.IsOkEnabled);
            }, associateProteinsDlg=>associateProteinsDlg.OkDialog());

            WaitForDocumentLoaded(WAIT_TIME * 4);
            var imputedPeptidesWithMissingResults = SkylineWindow.Document.Molecules.Where(AnyMissingResults).ToList();
            Assert.AreEqual(0, imputedPeptidesWithMissingResults.Count);
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                peptideSettingsUi.ImputeMissingPeaks = false;
                peptideSettingsUi.OkDialog();
            });
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults,
                manageResultsDlg =>
                {
                    manageResultsDlg.SelectedChromatograms = SkylineWindow.Document.MeasuredResults.Chromatograms;
                    manageResultsDlg.ReimportResults();
                    manageResultsDlg.OkDialog();
                });
            WaitForDocumentLoaded();
            var peptidesWithMissingResults = SkylineWindow.Document.Molecules.Where(AnyMissingResults).ToList();
            Assert.AreNotEqual(0, peptidesWithMissingResults.Count);
            var peptidesWithResults = SkylineWindow.Document.Molecules.Where(pep => !AnyMissingResults(pep)).ToList();
            Assert.AreNotEqual(0, peptidesWithResults.Count);
        }

        private bool AnyMissingResults(PeptideDocNode peptideDocNode)
        {
            if (peptideDocNode.Results == null)
            {
                return true;
            }

            if (peptideDocNode.Results.Any(chromInfoList => chromInfoList.ElementAtOrDefault(0)?.RetentionTime == null))
            {
                return true;
            }

            return false;
        }
    }
}
