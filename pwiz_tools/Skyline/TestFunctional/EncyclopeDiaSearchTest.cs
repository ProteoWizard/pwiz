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
using pwiz.Skyline.Util;
using System;
using System.Globalization;

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

        private bool IsRecordMode => false;
        private int[] FinalTargetCounts = { 15, 15, 15, 60 };

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
                // use CurrentCulture to simulate user entering value in additional settings dialog
                searchDlg.SetAdditionalSetting("PercolatorTrainingFDR", Convert.ToString(0.15, CultureInfo.CurrentCulture));
                searchDlg.SetAdditionalSetting("PercolatorThreshold", Convert.ToString(0.15, CultureInfo.CurrentCulture));
                searchDlg.SetAdditionalSetting("MinNumOfQuantitativePeaks", "0");
                searchDlg.SetAdditionalSetting("NumberOfQuantitativePeaks", "0");
                searchDlg.SetAdditionalSetting("V2scoring", "false");
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
                importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled = false;
                importPeptideSearchDlg.ImportFastaControl.AutoTrain = false;
            });

            // Finish wizard and the associate proteins dialog is shown.
            var emptyProteinsDlg = ShowDialog<AssociateProteinsDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
            WaitForConditionUI(() => emptyProteinsDlg.DocumentFinalCalculated);
            RunUI(() =>
            {
                int proteinCount, peptideCount, precursorCount, transitionCount;
                emptyProteinsDlg.NewTargetsFinalSync(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                ValidateTargets(ref FinalTargetCounts, proteinCount, peptideCount, precursorCount, transitionCount, @"FinalTargetCounts");
            });

            using (new WaitDocumentChange(null, true))
            {
                OkDialog(emptyProteinsDlg, emptyProteinsDlg.OkDialog);
            }

            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void ValidateTargets(ref int[] targetCounts, int proteinCount, int peptideCount, int precursorCount, int transitionCount, string propName)
        {
            if (IsRecordMode)
            {
                targetCounts[0] = proteinCount;
                targetCounts[1] = peptideCount;
                targetCounts[2] = precursorCount;
                targetCounts[3] = transitionCount;
                Console.WriteLine(@"{0} = new[] {{ {1}, {2}, {3}, {4} }},", propName, proteinCount, peptideCount, precursorCount, transitionCount);
                return;
            }

            var targetCountsActual = new[] { proteinCount, peptideCount, precursorCount, transitionCount };
            if (!ArrayUtil.EqualsDeep(targetCounts, targetCountsActual))
            {
                Assert.Fail("Expected target counts <{0}> do not match actual <{1}>.",
                    string.Join(", ", targetCounts),
                    string.Join(", ", targetCountsActual));
            }
        }

        private void PrepareDocument(string documentFile)
        {
            RunUI(SkylineWindow.NewDocument);
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings", doc => doc.ChangeSettings(SrmSettingsList.GetDefault())));
            RunUI(() => SkylineWindow.ModifyDocument("Set min ions",
                doc => doc.ChangeSettings(doc.Settings.ChangeTransitionSettings(
                    doc.Settings.TransitionSettings.ChangeLibraries(doc.Settings.TransitionSettings.Libraries
                        .ChangeMinIonCount(1))))));
            RunUI(() => SkylineWindow.SaveDocument(TestFilesDir.GetTestPath(documentFile)));
        }
    }
}
