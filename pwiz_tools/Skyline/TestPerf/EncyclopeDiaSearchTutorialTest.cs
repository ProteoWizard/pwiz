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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model.Prosit.Config;
using pwiz.Skyline.Model.Prosit.Models;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;
using System.Drawing;
using pwiz.Skyline;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Util;
using System;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;

namespace TestPerf
{
    [TestClass]
    public class EncyclopeDiaSearchTutorialTest : AbstractFunctionalTestEx
    {
        private AnalysisValues _analysisValues;

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestEncyclopeDiaSearchTutorial()
        {
            _analysisValues = new AnalysisValues
            {
                IsWholeProteome = false,
                NarrowWindowDiaFiles = new[]
                {
                    "23aug2017_hela_serum_timecourse_4mz_narrow_1.mzML",
                    //"23aug2017_hela_serum_timecourse_4mz_narrow_2.mzML",
                    //"23aug2017_hela_serum_timecourse_4mz_narrow_3.mzML",
                    //"23aug2017_hela_serum_timecourse_4mz_narrow_4.mzML",
                    //"23aug2017_hela_serum_timecourse_4mz_narrow_5.mzML",
                    //"23aug2017_hela_serum_timecourse_4mz_narrow_6.mzML",
                },
                WideWindowDiaFiles = new[]
                {
                    "23aug2017_hela_serum_timecourse_wide_1a.mzML",
                    "23aug2017_hela_serum_timecourse_wide_1b.mzML",
                    //"23aug2017_hela_serum_timecourse_wide_1c.mzML",
                    //"23aug2017_hela_serum_timecourse_wide_1d.mzML",
                    //"23aug2017_hela_serum_timecourse_wide_1e.mzML",
                    //"23aug2017_hela_serum_timecourse_wide_1f.mzML",
                },

                FinalTargetCounts = new[] { 554, 4886, 4886, 37834 },
                MassErrorStats = new[]
                {
                    new[] {0.0, 2.1},
                    new[] {0.0, 2.1},
                    new[] {-0.1, 2.2},
                },
                ChromatogramClickPoint = new PointF(19.1f, 24f)
            };

            RunTest();
        }

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE), NoNightlyTesting(TestExclusionReason.EXCESSIVE_TIME), Timeout(36000000)] // 10 hours
        public void TestEncyclopeDiaSearchTutorialFullFileset()
        {
            _analysisValues = new AnalysisValues
            {
                //IsWholeProteome = true,
                NarrowWindowDiaFiles = new[]
                {
                    "23aug2017_hela_serum_timecourse_4mz_narrow_1.mzML",
                    "23aug2017_hela_serum_timecourse_4mz_narrow_2.mzML",
                    "23aug2017_hela_serum_timecourse_4mz_narrow_3.mzML",
                    "23aug2017_hela_serum_timecourse_4mz_narrow_4.mzML",
                    "23aug2017_hela_serum_timecourse_4mz_narrow_5.mzML",
                    "23aug2017_hela_serum_timecourse_4mz_narrow_6.mzML",
                },
                WideWindowDiaFiles = new[]
                {
                    "23aug2017_hela_serum_timecourse_wide_1a.mzML",
                    "23aug2017_hela_serum_timecourse_wide_1b.mzML",
                    "23aug2017_hela_serum_timecourse_wide_1c.mzML",
                    "23aug2017_hela_serum_timecourse_wide_1d.mzML",
                    "23aug2017_hela_serum_timecourse_wide_1e.mzML",
                    "23aug2017_hela_serum_timecourse_wide_1f.mzML",
                },
                FinalTargetCounts = new[] { 559, 36128, 36128, 325152 },
                MassErrorStats = new[]
                {
                    new[] {0.3, 2.2},
                    new[] {0.3, 2.2},
                    new[] {0.3, 2.2},
                    new[] {0.3, 2.2},
                    new[] {0.4, 2.2},
                    new[] {0.3, 2.2},
                    new[] {0.3, 2.2},
                },
                ChromatogramClickPoint = new PointF(19.1f, 24f)
            };

            RunTest();
        }


        /// <summary>Change to true to write coefficient arrays.</summary>
        private bool IsRecordMode => false;

        /// <summary>Disable audit log comparison for FullFileset tests</summary>
        public override bool AuditLogCompareLogs => !TestContext.TestName.EndsWith("FullFileset");

        private void RunTest()
        {
            TestFilesZip = @"https://skyline.ms/tutorials/EncyclopeDiaSearchTutorial.zip";
            TestFilesPersistent = new[] { "23aug2017_hela_serum_timecourse", "z3_nce33-prosit" };

            RunFunctionalTest();

            Assert.IsFalse(IsRecordMode, "Set IsRecordMode to false before commit");   // Make sure this doesn't get committed as true
        }

        public static bool HasPrositServer()
        {
            return !string.IsNullOrEmpty(PrositConfig.GetPrositConfig().RootCertificate);
        }

        private class AnalysisValues
        {
            /// <summary>
            /// If true, all DiaFiles will be processed and searched with the full FASTA (FastaPathForSearch).
            /// If false, only first 2 DiaFiles will be processed and searched with targets-only FASTA (FastaPath).
            /// </summary>
            public bool IsWholeProteome;

            public string[] NarrowWindowDiaFiles;
            public string[] WideWindowDiaFiles;

            public int[] FinalTargetCounts;
            public PointF ChromatogramClickPoint;
            public double[][] MassErrorStats;

            public string FastaPath =>
                IsWholeProteome
                    ? "20220721-uniprot-sprot-human.fasta"
                    : "20230123-abundant-proteins.fasta";

            public string BlibPath =>
                IsWholeProteome
                    ? "20220721-uniprot-sprot-human-z3_nce33-prosit-7C7C51618B8D2289272F4E24498B7C.blib"
                    : "20230123-abundant-proteins-z3_nce33-prosit-53253019C6592C9A4D7B3FA95E6CBE.blib";

            public string PrositHash =>
                IsWholeProteome
                    ? "7C7C51618B8D2289272F4E24498B7C"
                    : "53253019C6592C9A4D7B3FA95E6CBE";
        }

        protected override void DoTest()
        {
            if (!HasPrositServer())
                return;

            PrepareDocument("EncyclopeDiaSearchTutorialTest.sky");
            string fastaFilepath = TestFilesDir.GetTestPath(_analysisValues.FastaPath);

            var searchDlg = ShowDialog<EncyclopeDiaSearchDlg>(SkylineWindow.ShowEncyclopeDiaSearchDlg);
            RunUI(() => searchDlg.ImportFastaControl.SetFastaContent(fastaFilepath));

            // copy expected blib to actual blib path so it will be re-used and Prosit won't be called
            string persistentBlibFilepath = TestFilesDir.GetTestPath(_analysisValues.BlibPath);
            string tempBlibFilepath = TestFilesDir.GetTestPath(fastaFilepath)
                .Replace(".fasta", $"-z3_nce33-prosit-{_analysisValues.PrositHash}.blib");
            FileEx.HardLinkOrCopyFile(persistentBlibFilepath, tempBlibFilepath);

            string persistentPath = Path.GetDirectoryName(persistentBlibFilepath)!;
            DirectoryEx.SafeDelete(Path.Combine(persistentPath, "elib_chrom"));
            DirectoryEx.SafeDelete(Path.Combine(persistentPath, "elib_quant"));

            if (Program.UseOriginalURLs)
                FileEx.SafeDelete(TestFilesDir.GetTestPath(_analysisValues.BlibPath), true);

            RunUI(searchDlg.NextPage); // now on Prosit settings

            Settings.Default.PrositIntensityModel = PrositIntensityModel.Models.First();
            Settings.Default.PrositRetentionTimeModel = PrositRetentionTimeModel.Models.First();
            RunUI(() =>
            {
                searchDlg.DefaultCharge = 3;
                searchDlg.DefaultNCE = 33;
                searchDlg.MinCharge = 2;
                searchDlg.MaxCharge = 3;
                //searchDlg.MinMz = 500;
                //searchDlg.MaxMz = 551;
                searchDlg.ImportFastaControl.MaxMissedCleavages = 2;
            });
            PauseForScreenShot(searchDlg);

            RunUI(searchDlg.NextPage); // now on narrow fractions
            var browseNarrowDlg = ShowDialog<OpenDataSourceDialog>(() => searchDlg.NarrowWindowResults.Browse());
            RunUI(() =>
            {
                browseNarrowDlg.CurrentDirectory = new MsDataFilePath(TestFilesDir.PersistentFilesDir);
                browseNarrowDlg.SelectAllFileType("mzML", s => _analysisValues.NarrowWindowDiaFiles.Contains(s));
            });
            PauseForScreenShot(searchDlg);
            OkDialog(browseNarrowDlg, browseNarrowDlg.Open);

            RunUI(searchDlg.NextPage); // now on wide fractions
            var browseWideDlg = ShowDialog<OpenDataSourceDialog>(() => searchDlg.WideWindowResults.Browse());
            RunUI(() =>
            {
                browseWideDlg.CurrentDirectory = new MsDataFilePath(TestFilesDir.PersistentFilesDir);
                browseWideDlg.SelectAllFileType("mzML", s => _analysisValues.WideWindowDiaFiles.Contains(s));
            });
            PauseForScreenShot(searchDlg);
            OkDialog(browseWideDlg, browseWideDlg.Open);

            RunUI(searchDlg.NextPage); // now on EncyclopeDia settings
            RunUI(() =>
            {
                // some things I tried to get full proteome, full fileset test to complete faster (they didn't work)
                //searchDlg.EncyclopeDiaFragmentTolerance = new MzTolerance(0.1, MzTolerance.Units.ppm);
                //searchDlg.EncyclopeDiaPrecursorTolerance = new MzTolerance(0.1, MzTolerance.Units.ppm);
                //searchDlg.SetAdditionalSetting("PercolatorTrainingSetSize", "50000");
                //searchDlg.SetAdditionalSetting("RtWindowInMin", "0.05");
                //searchDlg.SetAdditionalSetting("MinNumOfQuantitativePeaks", "20");
                //searchDlg.SetAdditionalSetting("FilterPeaklists", "true");
                //searchDlg.SetAdditionalSetting("NumberOfThreadsUsed", "16");
            });
            PauseForScreenShot(searchDlg);
            RunUI(searchDlg.NextPage); // start search

            var downloaderDlg = TryWaitForOpenForm<MultiButtonMsgDlg>(2000);
            if (downloaderDlg != null)
            {
                OkDialog(downloaderDlg, downloaderDlg.ClickYes);
                var waitDlg = WaitForOpenForm<LongWaitDlg>();
                WaitForClosedForm(waitDlg);
            }

            PauseForScreenShot(searchDlg);

            try
            {
                bool? searchSucceeded = null;
                searchDlg.SearchControl.SearchFinished += (success) => searchSucceeded = success;
                WaitForConditionUI(60000 * 120, () => searchSucceeded.HasValue);
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
                importPeptideSearchDlg.TransitionSettingsControl.MinIonCount = 3;
                importPeptideSearchDlg.TransitionSettingsControl.IonCount = 5;
                importPeptideSearchDlg.TransitionSettingsControl.IonMatchTolerance = 0.005;
                if (_analysisValues.IsWholeProteome)
                {
                    importPeptideSearchDlg.TransitionSettingsControl.MinIonCount = 1;
                    importPeptideSearchDlg.TransitionSettingsControl.IonCount = 1;
                    importPeptideSearchDlg.TransitionSettingsControl.PeptideIonTypes = new[] { IonType.y };
                }
                importPeptideSearchDlg.ClickNextButton();

                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);

                importPeptideSearchDlg.FullScanSettingsControl.SetRetentionTimeFilter(RetentionTimeFilterType.ms2_ids, 2);
                if (_analysisValues.IsWholeProteome)
                {
                    importPeptideSearchDlg.FullScanSettingsControl.PrecursorCharges = new[] { 2 };
                    importPeptideSearchDlg.FullScanSettingsControl.SetRetentionTimeFilter(RetentionTimeFilterType.ms2_ids, 1);
                }
                importPeptideSearchDlg.ClickNextButton();

                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled = false;
            });

            // Finish wizard and the associate proteins dialog is shown.
            var emptyProteinsDlg = ShowDialog<AssociateProteinsDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);

            WaitForConditionUI(() => emptyProteinsDlg.DocumentFinalCalculated);
            RunUI(() =>
            {
                int proteinCount, peptideCount, precursorCount, transitionCount;
                emptyProteinsDlg.NewTargetsFinalSync(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                ValidateTargets(ref _analysisValues.FinalTargetCounts, proteinCount, peptideCount, precursorCount, transitionCount, @"FinalTargetCounts");
                //emptyProteinsDlg.CancelDialog();
            });

            using (new WaitDocumentChange(null, true, 60000 * 120))
            {
                OkDialog(emptyProteinsDlg, emptyProteinsDlg.OkDialog);
            }
            //WaitForDocumentLoaded();

            //PauseForScreenShot<AllChromatogramsGraph>("Loading chromatograms window", 13, 30 * 1000); // 30 second timeout to avoid getting stuck
            //WaitForDocumentChangeLoaded(doc, 15 * 60 * 1000); // 15 minutes

            RunUI(() => SkylineWindow.SaveDocument());

            int screenshotPage = 0;
            const string proteinNameToSelect = "sp|P21333|FLNA_HUMAN";
            const string peptideToSelect = "VKVEPSHDASK";
            if (Equals(proteinNameToSelect, SkylineWindow.Document.MoleculeGroups.Skip(1).First().Name))
                SelectNode(SrmDocument.Level.MoleculeGroups, 1);
            else
                FindNode(proteinNameToSelect);

            RunUI(() =>
            {
                Assert.AreEqual(proteinNameToSelect, SkylineWindow.SelectedNode.Text);

                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.ShowRTReplicateGraph();
                SkylineWindow.Size = new Size(1226, 900);
            });
            //RestoreViewOnScreenNoSelChange(18);
            WaitForGraphs();
            screenshotPage++;   // Docking drag-drop image page
            PauseForScreenShot("Manual review window layout with protein selected", screenshotPage++);

            try
            {
                FindNode(peptideToSelect);
                WaitForGraphs();
                PauseForScreenShot("Manual review window layout with peptide selected", screenshotPage++);
            }
            catch (AssertFailedException)
            {
                if (!IsRecordMode)
                    throw;
                PauseAndContinueForm.Show($"Clicking the peptide ({peptideToSelect}) failed.\r\n" +
                                          "Pick a new peptide to select.");
            }

            RunUI(SkylineWindow.AutoZoomBestPeak);
            WaitForGraphs();
            PauseForScreenShot("Snip just one chromatogram pane", screenshotPage);

            try
            {
                ClickChromatogram(SkylineWindow.Document.MeasuredResults.Chromatograms[0].Name,
                    _analysisValues.ChromatogramClickPoint.X,
                    _analysisValues.ChromatogramClickPoint.Y);
            }
            catch (AssertFailedException)
            {
                if (!IsRecordMode)
                    throw;
                PauseAndContinueForm.Show($"Clicking the left-side chromatogram at ({_analysisValues.ChromatogramClickPoint.X}, {_analysisValues.ChromatogramClickPoint.Y}) failed.\r\n" +
                                          "Click on and record a new ChromatogramClickPoint at the peak of that chromatogram.");
            }

            PauseForScreenShot<GraphFullScan>("Full-Scan graph window - zoomed", screenshotPage++);
            

            RunUI(() => SkylineWindow.GraphFullScan.ZoomToSelection(false));
            WaitForGraphs();
            PauseForScreenShot<GraphFullScan>("Full-Scan graph window - unzoomed", screenshotPage++);

            RunUI(SkylineWindow.GraphFullScan.Close);
            RunUI(SkylineWindow.ShowMassErrorHistogramGraph);

            WaitForGraphs();
            Assert.IsTrue(SkylineWindow.GraphMassError.TryGetGraphPane(out MassErrorHistogramGraphPane massErrorPane));
            int massErrorStatsIndex = 0;
            if (IsRecordMode)
            {
                Console.WriteLine(@"MassErrorStats = new[]");
                Console.WriteLine(@"{");
            }
            ValidateMassErrors(massErrorPane, massErrorStatsIndex++);

            // CONSIDER: No way to specify mass error graph window in PauseForScreenShot or ShowDialog
            PauseForScreenShot("Mass errors histogram graph window", screenshotPage++);

            // Review single replicates
            RunUI(SkylineWindow.ShowSingleReplicate);
            foreach (var chromatogramSet in SkylineWindow.Document.MeasuredResults.Chromatograms)
            {
                RunUI(() => SkylineWindow.ActivateReplicate(chromatogramSet.Name));
                WaitForGraphs();
                ValidateMassErrors(massErrorPane, massErrorStatsIndex++);
            }
            if (IsRecordMode)
            {
                Console.WriteLine(@"},");
            }

            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void PrepareDocument(string documentFile)
        {
            RunUI(SkylineWindow.NewDocument);
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings", doc => doc.ChangeSettings(SrmSettingsList.GetDefault())));
            RunUI(() => SkylineWindow.SaveDocument(TestFilesDir.GetTestPath(documentFile)));
        }

        private void RestoreViewOnScreenNoSelChange(int pageName)
        {
            if (!Program.SkylineOffscreen)
            {
                RunUI(() =>
                {
                    var selectedPath = SkylineWindow.SelectedPath;
                    RestoreViewOnScreen(pageName);
                    SkylineWindow.SelectedPath = selectedPath;
                });
            }
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

        private void ValidateMassErrors(MassErrorHistogramGraphPane massErrorPane, int index)
        {
            double mean = massErrorPane.Mean, stdDev = massErrorPane.StdDev;
            if (IsRecordMode)
                Console.WriteLine(@"new[] {{{0:0.0}, {1:0.0}}},", mean, stdDev);  // Not L10N
            else
            {
                Assert.AreEqual(_analysisValues.MassErrorStats[index][0], mean, 0.05);
                Assert.AreEqual(_analysisValues.MassErrorStats[index][1], stdDev, 0.05);
            }
        }
    }
}
