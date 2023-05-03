/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model.Prosit.Config;
using pwiz.Skyline.Model.Prosit.Models;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class EncyclopeDiaSearchTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestEncyclopeDiaSearch()
        {
            TestFilesZip = @"Test\EncyclopeDiaHelpersTest.zip";
            RunFunctionalTest();
        }

        public static bool HasPrositServer()
        {
            return !string.IsNullOrEmpty(PrositConfig.GetPrositConfig().RootCertificate);
        }

        protected override void DoTest()
        {
            if (!HasPrositServer())
                return;

            PrepareDocument("EncyclopeDiaSearchTest.sky");
            string fastaFilepath = TestFilesDir.GetTestPath("pan_human_library_690to705.fasta");

            var searchDlg = ShowDialog<EncyclopeDiaSearchDlg>(SkylineWindow.ShowEncyclopeDiaSearchDlg);
            RunUI(() => searchDlg.ImportFastaControl.SetFastaContent(fastaFilepath));
            //PauseTest();

            // if UseOriginalURLs, delete the blib so it will have to be freshly predicted from Prosit
            if (Program.UseOriginalURLs)
                File.Delete(TestFilesDir.GetTestPath("pan_human_library_690to705-z3_nce33-prosit-5950B898E945AE52AD86D9CE06220EE.blib"));

            RunUI(searchDlg.NextPage); // now on Prosit settings

            Settings.Default.PrositIntensityModel = PrositIntensityModel.Models.First();
            Settings.Default.PrositRetentionTimeModel = PrositRetentionTimeModel.Models.First();
            RunUI(() =>
            {
                searchDlg.DefaultCharge = 3;
                searchDlg.DefaultNCE = 33;
                searchDlg.MinCharge = 2;
                searchDlg.MaxCharge = 3;
                searchDlg.MinMz = 690;
                searchDlg.MaxMz = 705;
                searchDlg.ImportFastaControl.MaxMissedCleavages = 2;

                searchDlg.SetAdditionalSetting("PercolatorTrainingFDR", "0.2");
                searchDlg.SetAdditionalSetting("PercolatorThreshold", "0.2");
                searchDlg.SetAdditionalSetting("MinNumOfQuantitativePeaks", "0");
                searchDlg.SetAdditionalSetting("NumberOfQuantitativePeaks", "0");
            });

            RunUI(searchDlg.NextPage); // now on narrow fractions
            var browseNarrowDlg = ShowDialog<OpenDataSourceDialog>(() => searchDlg.NarrowWindowResults.Browse());
            RunUI(() =>
            {
                browseNarrowDlg.SelectFile("23aug2017_hela_serum_timecourse_4mz_narrow_3.mzML");
                browseNarrowDlg.SelectFile("23aug2017_hela_serum_timecourse_4mz_narrow_4.mzML");
            });
            OkDialog(browseNarrowDlg, browseNarrowDlg.Open);

            RunUI(searchDlg.NextPage); // now on wide fractions
            var browseWideDlg = ShowDialog<OpenDataSourceDialog>(() => searchDlg.WideWindowResults.Browse());
            RunUI(() =>
            {
                browseWideDlg.SelectFile("23aug2017_hela_serum_timecourse_wide_1d.mzML");
                browseWideDlg.SelectFile("23aug2017_hela_serum_timecourse_wide_1e.mzML");
            });
            OkDialog(browseWideDlg, browseWideDlg.Open);

            RunUI(searchDlg.NextPage); // now on EncyclopeDia settings
            RunUI(searchDlg.NextPage); // start search

            var downloaderDlg = TryWaitForOpenForm<MultiButtonMsgDlg>(2000);
            if (downloaderDlg != null)
            {
                OkDialog(downloaderDlg, downloaderDlg.ClickYes);
                var waitDlg = WaitForOpenForm<LongWaitDlg>();
                WaitForClosedForm(waitDlg);
            }

            try
            {
                bool? searchSucceeded = null;
                searchDlg.SearchControl.SearchFinished += (success) => searchSucceeded = success;
                WaitForConditionUI(60000, () => searchSucceeded.HasValue);
                RunUI(() => Assert.IsTrue(searchSucceeded.Value, searchDlg.SearchControl.LogText));
            }
            finally
            {
                File.WriteAllText("SearchControlLog.txt", searchDlg.SearchControl.LogText);
            }

            // now on Import Peptide Search wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(searchDlg.NextPage);
            WaitForDocumentLoaded();
            //PauseTest();

            // starts on chromatogram page because we're using existing library
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);
                //importPeptideSearchDlg.ClickNextButton();
            });

            // Remove prefix/suffix dialog pops up; accept default behavior
            var removeSuffix = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
            OkDialog(removeSuffix, () => removeSuffix.YesDialog());
            WaitForDocumentLoaded();

            RunUI(() =>
            {
                // modifications page is skipped

                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.transition_settings_page);
                importPeptideSearchDlg.ClickNextButton();

                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                importPeptideSearchDlg.ClickNextButton();

                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled = true;
            });

            // Finish wizard and the associate proteins dialog is shown.
            var emptyProteinsDlg = ShowDialog<AssociateProteinsDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
            WaitForConditionUI(() => emptyProteinsDlg.DocumentFinalCalculated);
            RunUI(() =>
            {
                int proteinCount, peptideCount, precursorCount, transitionCount;
                emptyProteinsDlg.NewTargetsFinalSync(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                Assert.AreEqual(5, proteinCount);
                Assert.AreEqual(8, peptideCount);
                Assert.AreEqual(8, precursorCount);
                Assert.AreEqual(72, transitionCount);
                //emptyProteinsDlg.CancelDialog();
            });
            //PauseTest();
            using (new WaitDocumentChange(null, true))
            {
                OkDialog(emptyProteinsDlg, emptyProteinsDlg.OkDialog);
            }
            //WaitForDocumentLoaded();

            //RunUI(importPeptideSearchDlg.CancelDialog);
            //WaitForClosedForm(importPeptideSearchDlg);
            //RunUI(searchDlg.CancelDialog);
            //WaitForClosedForm(searchDlg);

            //PauseTest();

            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void PrepareDocument(string documentFile)
        {
            RunUI(SkylineWindow.NewDocument);
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings", doc => doc.ChangeSettings(SrmSettingsList.GetDefault())));
            RunUI(() => SkylineWindow.SaveDocument(TestFilesDir.GetTestPath(documentFile)));
        }
    }
}
