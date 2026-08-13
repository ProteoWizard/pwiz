/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    /// <summary>
    /// Sister of <see cref="DiannSearchTutorialTest"/> that exercises Skyline's DIA-NN
    /// wrapper on the Navarro et al. 2016 LFQbench HYE124 dataset (PXD002952), specifically
    /// the TripleTOF 6600 / 64-variable-window subset that was the headline configuration
    /// in the paper. Outputs from this run feed directly into the same LFQbench R pipeline
    /// the Navarro paper used (<c>Skyline/scripts/generateReport.R</c>), so the
    /// per-species plots are visually + statistically comparable to the paper figures.
    ///
    /// <para>Not auto-run because the .wiff.scan sidecars are ~6 GB each — total ~36 GB
    /// download. Invoke manually:
    /// <code>pwsh -File ./ai/scripts/Skyline/Run-Tests.ps1 -TestName TestDiannSearchLFQbench -EnableInternet</code>
    /// </para>
    /// </summary>
    [TestClass]
    public class DiannSearchLFQbenchTest : AbstractFunctionalTestEx
    {
        // HYE124 (Navarro et al. 2016) sample composition — see also generateReport.R:
        //   A: HUMAN 65 / YEAST 30 / ECOLI 5
        //   B: HUMAN 65 / YEAST 15 / ECOLI 20
        // Yields log2(B/A) = 0 / −1 / +2 — exactly the ProteoBench AIF design.
        private static readonly Dictionary<string, double> ExpectedLog2FcBySpecies = new Dictionary<string, double>
        {
            { @"HUMAN",  0.0 },
            { @"YEAST", -1.0 },
            { @"ECOLI",  2.0 },
        };
        private const double LOG2FC_TOLERANCE = 0.30;
        private const int MIN_QUANTIFIED_PEPTIDES_PER_SPECIES = 50;

        // HYE124_TTOF6600_64var — the Navarro paper's headline configuration (TripleTOF
        // 6600 with 64 variable-width SWATH windows). Files alternate A/B by run number:
        //   008, 010, 012 → sample A (3 technical reps)
        //   009, 011, 013 → sample B (3 technical reps)
        // .wiff is metadata-only (14 MB); the actual signal is in the .wiff.scan sidecar
        // (~6 GB each, DIA-NN reads both). HTRMS files are Spectronaut-specific, skip.
        private const string PRIDE_BASE = @"https://ftp.pride.ebi.ac.uk/pride/data/archive/2016/09/PXD002952/";
        private static readonly string[] WIFF_STEMS =
        {
            @"HYE124_TTOF6600_64var_lgillet_I150211_008",  // A1
            @"HYE124_TTOF6600_64var_lgillet_I150211_010",  // A2
            @"HYE124_TTOF6600_64var_lgillet_I150211_012",  // A3
            @"HYE124_TTOF6600_64var_lgillet_I150211_009",  // B1
            @"HYE124_TTOF6600_64var_lgillet_I150211_011",  // B2
            @"HYE124_TTOF6600_64var_lgillet_I150211_013",  // B3
        };
        // First three indices are sample A, last three are B — matches Navarro's
        // process_hye_samples.R config used to drive LFQbench in the paper.
        private static readonly HashSet<string> SampleAStems = new HashSet<string>(WIFF_STEMS.Take(3));

        // FASTA used by the Navarro paper — UniProt SwissProt Human + Yeast + E.coli + iRT
        // standards + reversed decoys, all in one file (no separate decoy generation needed).
        private const string FASTA_FILENAME = @"napedro_3mixed_human_yeast_ecoli_20140403_iRT_reverse.fasta";

        // ~36 GB on disk — hardcoded to D:\ on Matt's workstation; override via env var
        // SKYLINE_LFQBENCH_CACHE_DIR if anywhere else.
        private static readonly string CacheDir =
            Environment.GetEnvironmentVariable(@"SKYLINE_LFQBENCH_CACHE_DIR")
            ?? @"D:\SkylinePerfTests\LFQbenchHYE124_64var";

        private static readonly string DocDir = CacheDir + @"-doc";

        private string[] WiffPaths => WIFF_STEMS.Select(s => Path.Combine(CacheDir, s + @".wiff")).ToArray();

        // [TestMethod] intentionally disabled — this ~36 GB LFQbench run is not part of the
        // routine suite; it's invoked manually for the LFQbench poster only. Uncomment to run.
        //[TestMethod]
        [NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        [NoLeakTesting(TestExclusionReason.EXCESSIVE_TIME)]
        [NoNightlyTesting(@"~36 GB dataset download (.wiff.scan sidecars); manually invoked for the LFQbench poster only")]
        public void TestDiannSearchLFQbench()
        {
            EnsureBenchmarkFilesDownloaded();
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            PrepareDocument(@"DiannSearchLFQbench.sky");

            var realDiannPath = ResolveDiannBinary();
            AssertEx.IsTrue(File.Exists(realDiannPath),
                $@"DIA-NN binary not available at {realDiannPath}");

            DiannSearchDlg searchDlg;
            try
            {
                DiannHelpers.RegisteredDiannPathOverride = () => realDiannPath;
                SkylineWindow.BeginInvoke(new Action(SkylineWindow.ShowDiannSearchDlg));
                var useExisting = TryWaitForOpenForm<MultiButtonMsgDlg>(5000);
                if (useExisting != null)
                    OkDialog(useExisting, () => useExisting.DialogResult = DialogResult.Yes);
                searchDlg = WaitForOpenForm<DiannSearchDlg>();
            }
            finally
            {
                DiannHelpers.RegisteredDiannPathOverride = null;
            }

            ConfigureAndRunSearch(searchDlg);
            ImportToSkylineDocument(searchDlg);
            // Volcano + box plot reuse the same per-peptide log2FC computation; the
            // ClassifySpecies suffix match (_HUMAN, _YEAST, _ECOLI) works for both
            // FASTAs since both use UniProt SwissProt suffix conventions.
        }

        private void ConfigureAndRunSearch(DiannSearchDlg searchDlg)
        {
            string fasta = Path.Combine(CacheDir, FASTA_FILENAME);

            WaitForConditionUI(() => searchDlg.CurrentPage == DiannSearchDlg.Pages.data_files_page);
            RunUI(() => searchDlg.DataFileResults.FoundResultsFiles = WiffPaths
                .Select(p => new ImportPeptideSearch.FoundResultsFile(Path.GetFileName(p), p)).ToArray());
            RunUI(searchDlg.NextPage);

            WaitForConditionUI(() => searchDlg.CurrentPage == DiannSearchDlg.Pages.fasta_page);
            RunUI(() => searchDlg.ImportFastaControl.SetFastaContent(fasta));
            RunUI(searchDlg.NextPage); // → modifications

            WaitForConditionUI(() => searchDlg.CurrentPage == DiannSearchDlg.Pages.modifications_page);
            RunUI(searchDlg.NextPage); // → search settings

            WaitForConditionUI(() => searchDlg.CurrentPage == DiannSearchDlg.Pages.search_settings_page);
            RunUI(() =>
            {
                // Navarro paper recipe: Trypsin/P, 1 missed cleavage, FDR 0.01, charges 1-4.
                // The fasta has decoys already, so let DIA-NN's predictor + library use them.
                searchDlg.Ms1Tolerance = 0;
                searchDlg.Ms2Tolerance = 0;
                searchDlg.QValueThreshold = 0.01;
                searchDlg.Threads = Environment.ProcessorCount;
                var addl = searchDlg.DiannSearchConfig.AdditionalSettings;
                addl[@"MinPepLen"].Value = 6;
                addl[@"MinPrecursorCharge"].Value = 1;
                addl[@"MaxPrecursorCharge"].Value = 4;
                // Re-runs leverage the persisted .quant cache via --use-quant.
                searchDlg.DiannSearchConfig.ReuseCachedLibrary = true;
                searchDlg.DiannSearchConfig.ReuseQuantFiles = true;
            });
            RunUI(() =>
            {
                searchDlg.ImportFastaControl.Enzyme =
                    Settings.Default.EnzymeList.FirstOrDefault(e => e.Name == @"Trypsin/P")
                    ?? Settings.Default.EnzymeList.First();
                searchDlg.ImportFastaControl.MaxMissedCleavages = 1;
            });
            RunUI(searchDlg.NextPage); // → run

            WaitForConditionUI(() => searchDlg.CurrentPage == DiannSearchDlg.Pages.run_page);
            try
            {
                bool? searchSucceeded = null;
                searchDlg.SearchControl.SearchFinished += success => searchSucceeded = success;
                // 2-hour ceiling — TripleTOF 6600 SWATH on 6 files runs ~30 min on 24 cores
                // after first-pass cache warm-up, longer on a fresh machine.
                WaitForConditionUI(2 * 60 * 60 * 1000, () => searchSucceeded.HasValue);
                RunUI(() => AssertEx.IsTrue(searchSucceeded.Value, searchDlg.SearchControl.LogText));
            }
            finally
            {
                File.WriteAllText(@"DiannSearchLFQbench_DiannLog.txt", searchDlg.SearchControl.LogText);
            }
            AssertEx.IsTrue(File.Exists(searchDlg.SearchControl.OutputSpecLibPath));
        }

        private void ImportToSkylineDocument(DiannSearchDlg searchDlg)
        {
            string docBlibPath = BiblioSpecLiteSpec.GetLibraryFileName(SkylineWindow.DocumentFilePath);
            if (File.Exists(docBlibPath))
                File.Delete(docBlibPath);

            var importDlg = ShowDialog<ImportPeptideSearchDlg>(searchDlg.NextPage);
            WaitForDocumentLoaded();
            WaitForConditionUI(() => importDlg.BuildPepSearchLibControl.Grid.ScoreTypesLoaded);

            WaitForConditionUI(() => importDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
            RunUI(() => AssertEx.IsTrue(importDlg.ClickNextButton()));

            WaitForConditionUI(() => importDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);
            var removeSuffix = ShowDialog<ImportResultsNameDlg>(() => importDlg.ClickNextButton());
            OkDialog(removeSuffix, () => removeSuffix.YesDialog());
            WaitForDocumentLoaded();

            WaitForConditionUI(() => importDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
            RunUI(() => importDlg.MatchModificationsControl.ChangeAll(true));
            RunUI(() => AssertEx.IsTrue(importDlg.ClickNextButton()));

            WaitForConditionUI(() => importDlg.CurrentPage == ImportPeptideSearchDlg.Pages.transition_settings_page);
            RunUI(() => AssertEx.IsTrue(importDlg.ClickNextButton()));

            WaitForConditionUI(() => importDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
            RunUI(() => importDlg.FullScanSettingsControl.IsolationSchemeName =
                PropertiesResources.IsolationSchemeList_GetDefaults_Results_only);
            RunUI(() => AssertEx.IsTrue(importDlg.ClickNextButton()));

            WaitForConditionUI(() => importDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
            RunUI(() =>
            {
                importDlg.ImportFastaControl.DecoyGenerationEnabled = false;
                importDlg.ImportFastaControl.AutoTrain = false;
            });

            var associate = ShowDialog<AssociateProteinsDlg>(importDlg.ClickNextButtonNoCheck);
            WaitForConditionUI(() => associate.DocumentFinalCalculated);
            const int chromExtractionTimeoutMs = 60 * 60 * 1000;
            using (new WaitDocumentChange(null, true, chromExtractionTimeoutMs))
            {
                OkDialog(associate, associate.OkDialog);
            }
            WaitForDocumentLoaded(chromExtractionTimeoutMs);
            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void PrepareDocument(string documentFile)
        {
            RunUI(SkylineWindow.NewDocument);
            RunUI(() => SkylineWindow.ModifyDocument(@"Set default settings",
                doc => doc.ChangeSettings(SrmSettingsList.GetDefault())));
            Directory.CreateDirectory(DocDir);
            foreach (var stale in Directory.EnumerateFiles(DocDir, @"DiannSearchLFQbench.*"))
                File.Delete(stale);
            RunUI(() => SkylineWindow.SaveDocument(Path.Combine(DocDir, documentFile)));
        }

        private static string ResolveDiannBinary()
        {
            var envOverride = Environment.GetEnvironmentVariable(@"SKYLINE_PERF_DIANN_EXE");
            if (!string.IsNullOrEmpty(envOverride) && File.Exists(envOverride))
                return envOverride;
            if (Settings.Default.SearchToolList.ContainsKey(SearchToolType.DIANN))
            {
                var candidate = Settings.Default.SearchToolList[SearchToolType.DIANN];
                if (File.Exists(candidate.Path)) return candidate.Path;
            }
            var progress = new pwiz.Common.SystemUtil.SilentProgressMonitor();
            AssertEx.IsTrue(pwiz.Skyline.Util.SimpleFileDownloader.DownloadRequiredFiles(
                DiannHelpers.FilesToDownload, progress));
            return DiannHelpers.DiannBinary;
        }

        /// <summary>
        /// Download both the .wiff and the .wiff.scan sidecar for every input file, plus
        /// the matching FASTA. The .wiff is small (14 MB metadata) but useless without its
        /// scan sidecar (~6 GB); DIA-NN reads both.
        /// </summary>
        private void EnsureBenchmarkFilesDownloaded()
        {
            Directory.CreateDirectory(CacheDir);

            var fastaPath = Path.Combine(CacheDir, FASTA_FILENAME);
            if (!File.Exists(fastaPath) || new FileInfo(fastaPath).Length == 0)
                DownloadFile(PRIDE_BASE + FASTA_FILENAME, fastaPath);

            foreach (var stem in WIFF_STEMS)
            {
                foreach (var ext in new[] { @".wiff", @".wiff.scan" })
                {
                    var target = Path.Combine(CacheDir, stem + ext);
                    long minSize = ext == @".wiff" ? 1_000_000 : 100_000_000;
                    if (File.Exists(target) && new FileInfo(target).Length > minSize)
                        continue;
                    DownloadFile(PRIDE_BASE + stem + ext, target);
                }
            }
        }

        private static void DownloadFile(string url, string targetPath)
        {
            var tmp = targetPath + @".part";
            using (var client = new WebClient())
                client.DownloadFile(url, tmp);
            if (File.Exists(targetPath)) File.Delete(targetPath);
            File.Move(tmp, targetPath);
        }
    }
}
