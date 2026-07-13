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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    /// <summary>
    /// Tutorial-style perf test for the DIA-NN integration, driven by the published
    /// ProteoBench DIA-LFQ (AIF) reference dataset (PXD028735, Q Exactive HF-X,
    /// Human + Yeast + E.coli mix engineered to log2FC of 0 / -1 / +2 between
    /// Condition_A and Condition_B). Runs 4 files (2 replicates per condition — enough
    /// for the volcano plot's t-test), each shipped as a centroided mzML holding only the
    /// first half of the original ~150-min gradient (built from the raws by
    /// make-proteobench-subset-zip.bat, which is bundled inside the zip) to keep the
    /// run short, against the abbreviated HYE FASTA, ProteoBench recipe — walks through
    /// DiannSearchDlg +
    /// ImportPeptideSearchDlg with PauseForScreenShot at each page so the same body
    /// doubles as the basis for tutorial-doc screenshots.
    ///
    /// Data comes from ProteoBenchSubset.zip, which bundles not just the mzML + FASTA but
    /// also the DIA-NN cache (per-file `.quant` files + the predicted spectral library), all
    /// extracted once to a persistent dir. That makes EVERY run — including the first on a
    /// fresh machine — ~16 min (measured): the presearch is a no-op and the predictor build
    /// is skipped. The test still deletes the first file's cached .quant so the wizard runs a
    /// real DIA-NN search on it (the workflow the tutorial demonstrates), reusing the other
    /// three from cache. A fully cold run only happens if the whole cache is deleted (then
    /// <see cref="EnsureCachedPresearch"/> rebuilds it).
    /// </summary>
    [TestClass]
    public class DiannSearchTutorialTest : AbstractFunctionalTestEx
    {
        // Name used by AbstractFunctionalTest to route screenshots into
        // Documentation/Tutorials/<CoverShotName>/<lang>/ when run under -TakeScreenshots.
        // IsTutorial returns true because the test method name contains "Tutorial".
        private const string COVER_SHOT_NAME = @"DIA-NN-Search";

        // ProteoBench HYE log2FC design (Condition_B / Condition_A): Human 0, Yeast −1, E.coli +2.
        // We assert each species' median log2FC against the expected value with a tolerance that
        // absorbs DIA-NN run-to-run variance + Skyline's normalisation choices. The published
        // ProteoBench benchmarks typically see mean log2FC within ±0.2 of these values.
        private static readonly Dictionary<string, double> ExpectedLog2FcBySpecies = new Dictionary<string, double>
        {
            { @"HUMAN",  0.0 },
            { @"YEAST", -1.0 },
            { @"ECOLI",  2.0 },
        };
        private const double LOG2FC_TOLERANCE = 0.30;
        // We require at least this many quantified peptides per species before scoring;
        // anything less and the median is too noisy to be meaningful.
        private const int MIN_QUANTIFIED_PEPTIDES_PER_SPECIES = 50;

        // Tutorial data + DIA-NN cache ship as a reusable zip (other tutorials may use it too)
        // in the standard Skyline tutorials mirror: a single top-level ProteoBenchSubset/ folder
        // (matching the other tutorial zips), unzipped in place via TestFilesZipExtractHere so it
        // lands at …/Tutorials/ProteoBenchSubset/ (no double nesting). Contents: 4 first-half
        // mzML, the abbreviated HYE FASTA, the per-file .quant + predicted library, and
        // make-proteobench-subset-zip.bat (which rebuilds the mzML from the raw ProteoBench
        // PXD028735 files — it lives in the zip, not the repo).
        private const string ZIP_NAME = @"ProteoBenchSubset";

        // The mzML the wizard searches: each is only the FIRST HALF (0-75 min) of the
        // original ~150-min gradient, which roughly halves DIA-NN's per-file search time and
        // Skyline's chromatogram extraction. Two replicates per condition — the minimum that
        // still gives the volcano plot's t-test real variance. Filenames keep the
        // Condition_A / Condition_B tokens the replicate classification keys on, and are
        // marked persistent (see TestFilesPersistent) so DIA-NN's per-file `.quant` cache
        // survives next to them across runs.
        private static readonly string[] MZML_FILES =
        {
            @"LFQ_Orbitrap_AIF_Condition_A_Sample_Alpha_01.half.mzML",
            @"LFQ_Orbitrap_AIF_Condition_A_Sample_Alpha_02.half.mzML",
            @"LFQ_Orbitrap_AIF_Condition_B_Sample_Alpha_01.half.mzML",
            @"LFQ_Orbitrap_AIF_Condition_B_Sample_Alpha_02.half.mzML",
        };

        // Abbreviated FASTA — only the proteins identified by a previous full-FASTA run,
        // which cuts predicted-library build time. Bundled in the tutorial zip.
        private const string FASTA_FILENAME = @"ProteoBenchFASTA_MixedSpecies_HYE_identified.fasta";

        // DIA-NN's predicted spectral library. Also shipped in the tutorial zip (so the
        // ~15-20 min predictor build is skipped even on the first run) and seeded into
        // DocDir, where the wizard/presearch look for it via ReuseCachedLibrary.
        private const string PREDICTED_LIB_FILENAME = @"diann-predicted.predicted.speclib";

        // DIA-NN reads the mzML and writes its per-file `.quant` next to them, in the tutorial
        // zip's ProteoBenchSubset/ folder. With ExtractHere=true the framework unzips into the
        // mirror root, so PersistentFilesDir is the mirror (…/Tutorials) and the folder lands at
        // …/Tutorials/ProteoBenchSubset/ — a single level, no double nesting. Survives across
        // runs → fast repeat runs. Only valid after the zip is unzipped (inside DoTest and later).
        private string CacheDir => Path.Combine(TestFilesDirs[0].PersistentFilesDir, ZIP_NAME);

        // Stable Skyline doc directory — also receives DIA-NN intermediates (predicted
        // library, MBR output parquet). Kept out of the tutorial extraction dir so that dir
        // holds only the shipped inputs + the `.quant` cache. Stable across runs for reuse.
        private static readonly string DocDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"SkylinePerfTests", @"DiannPerfDoc-stable");

        // Full paths to the mzML the wizard searches.
        private string[] DiaSearchPaths => MZML_FILES.Select(f => Path.Combine(CacheDir, f)).ToArray();

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE),
         NoLeakTesting(TestExclusionReason.EXCESSIVE_TIME)]
        public void TestDiannSearchTutorial()
        {
            // Name contains "Tutorial" so AbstractFunctionalTest's IsTutorial returns true,
            // which routes PauseForScreenShot output into
            // Documentation/Tutorials/<CoverShotName>/<lang>/ when -TakeScreenshots is used.
            CoverShotName = COVER_SHOT_NAME;

            // Toggle on to interactively pause at each PauseForScreenShot (Pause/Continue
            // dialog appears so you can hand-tweak the screenshot subject before continuing).
            // IsPauseForScreenShots = true;
            // No published tutorial PDF yet — once one exists, set LinkPdf to its URL so the
            // tutorial-doc check on screenshot capture passes:
            // LinkPdf = @"https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/DIA-NN-Search.pdf";

            // Tutorial data + DIA-NN cache ship as a zip (single top-level ProteoBenchSubset/
            // folder) in the standard Skyline tutorials mirror. ExtractHere=true unzips that
            // folder in place (<mirror>/ProteoBenchSubset/) instead of nesting it under an extra
            // zip-name dir. Mark everything persistent so the ~4.8 GB extracts once and is reused
            // in place across runs (the .quant + predicted library keep every run warm).
            TestFilesZip = @"http://skyline.ms/tutorials/" + ZIP_NAME + @".zip";
            TestFilesZipExtractHere = new[] { true };
            TestFilesPersistent = MZML_FILES
                .Concat(MZML_FILES.Select(f => f + @".quant"))
                .Concat(new[] { FASTA_FILENAME, PREDICTED_LIB_FILENAME })
                .ToArray();
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Pull SkylineWindow above whatever shell/terminal launched the test so the
            // early screenshots aren't covered. Setting TopMost briefly bypasses Windows'
            // focus-stealing prevention; flipping it off restores normal z-order behaviour
            // so child dialogs from the wizard layer naturally on top.
            RunUI(() =>
            {
                SkylineWindow.WindowState = FormWindowState.Normal;
                SkylineWindow.TopMost = true;
                SkylineWindow.Activate();
                SkylineWindow.TopMost = false;
            });

            // The .quant files ship in the zip as the persistent baseline. The first file's
            // .quant is deleted so the wizard performs a real search (and removed again after
            // the run), so register it as an expected-missing file. If the cache was deleted
            // and a .quant is rebuilt by EnsureCachedPresearch, PotentialAdditional lets the
            // check treat it as an expected addition rather than an unexpected change. The check
            // names persistent entries relative to PersistentFilesDir's parent, so prefix with
            // the zip folder (ZIP_NAME) — e.g. "ProteoBenchSubset\<file>.quant".
            TestFilesDirs[0].PotentialAdditionalPersistentFileSet =
                new HashSet<string>(MZML_FILES.Select(f => Path.Combine(ZIP_NAME, f + @".quant")));
            TestFilesDirs[0].PotentialMissingPersistentFileSet =
                new HashSet<string> { Path.Combine(ZIP_NAME, MZML_FILES[0] + @".quant") };

            PrepareDocument(@"DiannSearchPerf.sky");

            var realDiannPath = ResolveDiannBinary();
            AssertEx.IsTrue(File.Exists(realDiannPath),
                $@"DIA-NN binary not available at {realDiannPath}");

            // The predicted library ships in the zip (extracted into CacheDir); copy it to
            // DocDir where DIA-NN looks for it, so the first run skips the predictor build.
            SeedPredictedLibraryFromZip();

            // Cache guard: the .quant files ship in the zip, so this is normally a no-op. It
            // only does real DIA-NN work if the cache was deleted and needs rebuilding.
            EnsureCachedPresearch(realDiannPath);

            // Delete the first file's cached .quant so the wizard runs a REAL DIA-NN search on
            // it (not just MBR re-quant from fully cached results) — the search the tutorial
            // demonstrates. The other 3 files reuse their shipped .quant. We remove this file
            // again after the run so the persistent dir never diverges from the shipped
            // baseline; it's registered in PotentialMissing (above) so the persistent-files
            // check tolerates it being gone, and it's re-extracted fresh from the zip next run.
            var forcedSearchQuant = DiaSearchPaths[0] + @".quant";
            if (File.Exists(forcedSearchQuant))
                File.Delete(forcedSearchQuant);

            DiannSearchDlg searchDlg;
            try
            {
                DiannHelpers.RegisteredDiannPathOverride = () => realDiannPath;
                // ShowDiannSearchDlg may show the "use existing or download" prompt before
                // opening DiannSearchDlg. The prompt is modal and blocks the UI thread, so
                // we BeginInvoke (fire-and-forget) and then poll for whichever dialog comes
                // up — MultiButtonMsgDlg first if it appears, then DiannSearchDlg.
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
            BuildAndShowVolcanoPlot();
            BuildAndShowSpeciesBoxPlot();
            AssertPerSpeciesLog2FoldChanges();

            // The wizard re-created the forced-search .quant; remove it so the persistent dir
            // matches the shipped baseline minus this one file (tolerated via PotentialMissing).
            if (File.Exists(forcedSearchQuant))
                File.Delete(forcedSearchQuant);
        }

        /// <summary>
        /// Set up a Condition annotation (A vs B), assign it to each replicate, define
        /// a Group Comparison, and open the volcano plot. Final tutorial screenshot
        /// captures the volcano — the standard plot for HYE-style benchmarks, showing
        /// the three species as separate clusters near their expected log2FC values.
        /// </summary>
        private void BuildAndShowVolcanoPlot()
        {
            const string annotation = @"Condition";
            const string comparison = @"Condition A vs B";

            // Build everything programmatically — no dialogs. The wizard-based path
            // (DocumentSettingsDlg + AddReplicateAnnotation + EditGroupComparisonDlg)
            // hangs under -TakeScreenshots: the foreground-focus grab during PauseForScreenShot
            // leaves the modal dialog in a state where subsequent OkDialog never acknowledges.
            RunUI(() =>
            {
                SkylineWindow.ModifyDocument(@"Configure Condition annotations + group comparison", doc =>
                {
                    // Replicate-level value-list annotation "Condition" with values A, B.
                    var annotationDef = new AnnotationDef(annotation,
                        AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.replicate),
                        AnnotationDef.AnnotationType.value_list, new[] { @"A", @"B" });

                    var oldData = doc.Settings.DataSettings;
                    var newAnnotationDefs = oldData.AnnotationDefs.ToList();
                    newAnnotationDefs.Add(annotationDef);

                    // A-vs-B group comparison, per-protein, no normalization (matches the
                    // log2(B/A) we already compute against ProteoBench expected values).
                    // Color rules set up-front: published HYE convention has HUMAN gray as
                    // baseline, YEAST blue (down), ECOLI red (up); contaminants get tan to
                    // distinguish from HUMAN (default unmatched colour is also gray).
                    // x_small points so dense clusters don't smear (Skyline's volcano plot
                    // stores colours as 6-hex `#RRGGBB`, so alpha-blending isn't available).
                    var colorRows = new[]
                    {
                        new MatchRgbHexColor(@"ProteinName: Cont_",
                            false, Color.FromArgb(210, 180, 140), PointSymbol.Circle, PointSize.x_small),
                        new MatchRgbHexColor(@"ProteinName: HUMAN",
                            false, Color.FromArgb(160, 160, 160), PointSymbol.Circle, PointSize.x_small),
                        new MatchRgbHexColor(@"ProteinName: YEAST",
                            false, Color.FromArgb(30, 144, 255), PointSymbol.Circle, PointSize.x_small),
                        new MatchRgbHexColor(@"ProteinName: ECOLI",
                            false, Color.FromArgb(220, 20, 60), PointSymbol.Circle, PointSize.x_small),
                    };
                    var groupComparisonDef = new GroupComparisonDef(comparison)
                        .ChangeControlAnnotation(annotation)
                        .ChangeControlValue(@"A")
                        .ChangeCaseValue(@"B")
                        .ChangePerProtein(true)
                        .ChangeNormalizationMethod(NormalizationMethod.NONE)
                        .ChangeColorRows(colorRows);
                    // Replace any pre-existing comparison with the same name (prior test
                    // runs persist the GroupComparisonDef in the saved doc).
                    var newGroupComparisonDefs = oldData.GroupComparisonDefs
                        .Where(g => g.Name != comparison).ToList();
                    newGroupComparisonDefs.Add(groupComparisonDef);

                    doc = doc.ChangeSettings(doc.Settings.ChangeDataSettings(
                        oldData.ChangeAnnotationDefs(newAnnotationDefs)
                               .ChangeGroupComparisonDefs(newGroupComparisonDefs)));

                    // Now that annotationDef exists on the doc, tag each replicate.
                    var oldResults = doc.Settings.MeasuredResults;
                    var newChromatograms = oldResults.Chromatograms.Select(c =>
                    {
                        bool isA = c.MSDataFilePaths.First().GetFileName()
                            .IndexOf(@"Condition_A", StringComparison.OrdinalIgnoreCase) >= 0;
                        var newAnnotations = c.Annotations.ChangeAnnotation(annotationDef, isA ? @"A" : @"B");
                        return c.ChangeAnnotations(newAnnotations);
                    }).ToArray();
                    doc = doc.ChangeMeasuredResults(oldResults.ChangeChromatograms(newChromatograms));

                    return doc;
                });
            });

            // Open the volcano plot for the Group Comparison we just defined.
            RunUI(() => SkylineWindow.ShowGroupComparisonWindow(comparison));
            var foldChangeGrid = WaitForOpenForm<FoldChangeGrid>();
            // The grid computes fold changes asynchronously. IsComplete only signals that
            // the column layout is up — we need to wait for actual rows to populate.
            WaitForConditionUI(5 * 60 * 1000,
                () => foldChangeGrid.DataboundGridControl.IsComplete
                      && foldChangeGrid.DataboundGridControl.RowCount > 0,
                () => $@"FoldChangeGrid still empty after wait. IsComplete={foldChangeGrid.DataboundGridControl.IsComplete}, RowCount={foldChangeGrid.DataboundGridControl.RowCount}");

            var volcanoPlot = ShowDialog<FoldChangeVolcanoPlot>(foldChangeGrid.ShowVolcanoPlot);
            RunUI(() => volcanoPlot.Parent.Parent.Size = new Size(900, 600));

            // Color rules are already baked into GroupComparisonDef.ColorRows (see step 1),
            // so the volcano plot picks them up automatically — no dialog walk required.
            // Wait for the coloured curves to render (CurveList grows as rules apply).
            WaitForConditionUI(30 * 1000, () => volcanoPlot.CurveList.Count >= 4);

            // Pin axis bounds to the HYE-relevant range. The data has outliers stretching
            // out to ±40 on the x axis; the engineered species ratios are −2 / 0 / +2, so
            // ±4 covers the real biology with a sensible margin. Y caps at 1.5 because
            // higher −log10(p) values are typically driven by Yeast/E.coli outliers and
            // squash the main clusters when the auto-scale takes over.
            RunUI(() =>
            {
                var pane = volcanoPlot.GraphControl.GraphPane;
                pane.XAxis.Scale.MinAuto = pane.XAxis.Scale.MaxAuto = false;
                pane.XAxis.Scale.Min = -4;
                pane.XAxis.Scale.Max = 4;
                pane.YAxis.Scale.MinAuto = pane.YAxis.Scale.MaxAuto = false;
                pane.YAxis.Scale.Min = 0;
                pane.YAxis.Scale.Max = 1.5;
                volcanoPlot.GraphControl.AxisChange();
                volcanoPlot.GraphControl.Invalidate();
            });

            PauseForScreenShot<FoldChangeVolcanoPlot>(
                @"Volcano plot – log2(B/A) (HYE expected: Human 0 grey, Yeast −1 blue, E.coli +2 red)");

            // DEBUG: save so we can inspect what actually landed in the document
            RunUI(() => SkylineWindow.SaveDocument());
        }

        /// <summary>
        /// Settings match ProteoBench's documented DIA-LFQ AIF recipe so the
        /// Skyline-driven DIA-NN run is directly comparable to ProteoBench's
        /// published DIA-NN reference for the same dataset.
        ///
        /// Screenshot ordering invariant: each PauseForScreenShot fires AFTER the
        /// wizard page is fully transitioned-to AND configured. NextPage is fired
        /// only after the snapshot. Without this, multiple consecutive captures land
        /// on the same window before the page transition has actually rendered.
        /// </summary>
        private void ConfigureAndRunSearch(DiannSearchDlg searchDlg)
        {
            string fasta = Path.Combine(CacheDir, FASTA_FILENAME);
            string[] diaPaths = DiaSearchPaths;

            // Page 1: data files. Already on this page when the dialog opens.
            WaitForConditionUI(() => searchDlg.CurrentPage == DiannSearchDlg.Pages.data_files_page);
            RunUI(() =>
            {
                searchDlg.DataFileResults.FoundResultsFiles = diaPaths
                    .Select(p => new ImportPeptideSearch.FoundResultsFile(Path.GetFileName(p), p)).ToArray();
            });
            PauseForScreenShot<DiannSearchDlg>(@"DIA-NN search – data files page (with files added)");
            RunUI(searchDlg.NextPage);

            // Page 2: FASTA.
            WaitForConditionUI(() => searchDlg.CurrentPage == DiannSearchDlg.Pages.fasta_page);
            RunUI(() => searchDlg.ImportFastaControl.SetFastaContent(fasta));
            // Default scroll position is at the start of the path — show the filename instead.
            RunUI(() => searchDlg.ImportFastaControl.ShowFastaPathFileName());
            PauseForScreenShot<DiannSearchDlg>(@"DIA-NN search – FASTA page (with file selected)");
            RunUI(searchDlg.NextPage); // → modifications

            // Page 3: modifications (we keep DIA-NN's defaults — Carbamidomethyl C fixed, Oxidation M variable).
            WaitForConditionUI(() => searchDlg.CurrentPage == DiannSearchDlg.Pages.modifications_page);
            PauseForScreenShot<DiannSearchDlg>(@"DIA-NN search – modifications page (defaults)");
            RunUI(searchDlg.NextPage); // → search settings

            // Page 4: search settings.
            WaitForConditionUI(() => searchDlg.CurrentPage == DiannSearchDlg.Pages.search_settings_page);
            RunUI(() =>
            {
                // ProteoBench recipe (https://proteobench.readthedocs.io/.../4-quant-lfq-ion-dia-aif/):
                // precursor m/z 400-1000, fragment 50-2000, charge 1-4, missed cleavages 1,
                // FDR 0.01, max var mods 1, min pep len 6. Mass accuracies left at 0 (auto),
                // which is what DIA-NN's own GUI defaults to for Orbitrap library-free.
                searchDlg.Ms1Tolerance = 0;
                searchDlg.Ms2Tolerance = 0;
                searchDlg.QValueThreshold = 0.01;
                searchDlg.Threads = Environment.ProcessorCount;
                var addl = searchDlg.DiannSearchConfig.AdditionalSettings;
                addl[@"MinPepLen"].Value = 6;
                addl[@"MinPrecursorCharge"].Value = 1;
                addl[@"MaxPrecursorCharge"].Value = 4;

                // Skip --predictor when the cached library is already on disk. The presearch
                // step populated it; without this flag DIA-NN would burn ~20 min rebuilding.
                searchDlg.DiannSearchConfig.ReuseCachedLibrary = true;
                // Tell DIA-NN to load any .quant files already next to the inputs (files 1..n-1
                // were quantified during the presearch step). Without this DIA-NN would
                // re-search every file from scratch despite --reanalyse seeing the cached
                // outputs on disk.
                searchDlg.DiannSearchConfig.ReuseQuantFiles = true;
            });
            // Match the ProteoBench enzyme (Trypsin/P) and missed cleavages.
            RunUI(() =>
            {
                searchDlg.ImportFastaControl.Enzyme =
                    Settings.Default.EnzymeList.FirstOrDefault(e => e.Name == @"Trypsin/P")
                    ?? Settings.Default.EnzymeList.First();
                searchDlg.ImportFastaControl.MaxMissedCleavages = 1;
            });
            PauseForScreenShot<DiannSearchDlg>(@"DIA-NN search – search settings page (ProteoBench recipe)");
            RunUI(searchDlg.NextPage); // → run

            // Page 5: run.
            WaitForConditionUI(() => searchDlg.CurrentPage == DiannSearchDlg.Pages.run_page);
            PauseForScreenShot<DiannSearchDlg>(@"DIA-NN search – run page (about to start)");
            try
            {
                bool? searchSucceeded = null;
                searchDlg.SearchControl.SearchFinished += success => searchSucceeded = success;
                // 120 min is a generous ceiling: a cached rerun searches only one file
                // (~a few min), while a cold first run populates the whole cache — bump if
                // that's expected on a fresh machine.
                WaitForConditionUI(120 * 60 * 1000, () => searchSucceeded.HasValue);
                RunUI(() => AssertEx.IsTrue(searchSucceeded.Value, searchDlg.SearchControl.LogText));
            }
            finally
            {
                File.WriteAllText(@"DiannSearchPerf_DiannLog.txt", searchDlg.SearchControl.LogText);
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

            // Spectral library page — wizard lands here after the search wraps up.
            WaitForConditionUI(() => importDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>(@"Spectral library page");
            RunUI(() => AssertEx.IsTrue(importDlg.ClickNextButton()));

            // Extract chromatograms page.
            WaitForConditionUI(() => importDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);
            PauseForScreenShot<ImportPeptideSearchDlg.ChromatogramsDiaPage>(@"Extract chromatograms page");
            // With >1 file the rename/prefix dialog pops up; accept defaults.
            if (MZML_FILES.Length > 1)
            {
                var removeSuffix = ShowDialog<ImportResultsNameDlg>(() => importDlg.ClickNextButton());
                PauseForScreenShot<ImportResultsNameDlg>(@"Import results names form");
                OkDialog(removeSuffix, () => removeSuffix.YesDialog());
            }
            else
            {
                RunUI(() => AssertEx.IsTrue(importDlg.ClickNextButton()));
            }
            WaitForDocumentLoaded();

            // Match modifications page.
            WaitForConditionUI(() => importDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
            RunUI(() => importDlg.MatchModificationsControl.ChangeAll(true));
            PauseForScreenShot<ImportPeptideSearchDlg.MatchModsPage>(@"Match modifications page (all matched)");
            RunUI(() => AssertEx.IsTrue(importDlg.ClickNextButton()));

            // Transition settings page (defaults).
            WaitForConditionUI(() => importDlg.CurrentPage == ImportPeptideSearchDlg.Pages.transition_settings_page);
            PauseForScreenShot<ImportPeptideSearchDlg.TransitionSettingsPage>(@"Transition settings page (defaults)");
            RunUI(() => AssertEx.IsTrue(importDlg.ClickNextButton()));

            // Full-scan settings page — pick the "Results only" isolation scheme.
            WaitForConditionUI(() => importDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
            RunUI(() => importDlg.FullScanSettingsControl.IsolationSchemeName =
                PropertiesResources.IsolationSchemeList_GetDefaults_Results_only);
            PauseForScreenShot<ImportPeptideSearchDlg.Ms2FullScanPage>(@"Full-scan settings page (Results only)");
            RunUI(() => AssertEx.IsTrue(importDlg.ClickNextButton()));

            // Import FASTA page.
            WaitForConditionUI(() => importDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
            RunUI(() =>
            {
                importDlg.ImportFastaControl.DecoyGenerationEnabled = false;
                importDlg.ImportFastaControl.AutoTrain = false;
            });
            // Show the filename portion of the FASTA path instead of the leftmost characters.
            RunUI(() => importDlg.ImportFastaControl.ShowFastaPathFileName());
            PauseForScreenShot<ImportPeptideSearchDlg.FastaPage>(@"Import FASTA page (with file selected)");

            var associate = ShowDialog<AssociateProteinsDlg>(importDlg.ClickNextButtonNoCheck);
            WaitForConditionUI(() => associate.DocumentFinalCalculated);
            PauseForScreenShot<AssociateProteinsDlg>(@"Associate proteins form (post-import summary)");
            // Chromatogram extraction on the ~0.9 GB half-gradient mzML files takes well over
            // the 180-second default — observed a few min per file on a 24-core box.
            // 30 min covers the 4-file run comfortably.
            const int chromExtractionTimeoutMs = 30 * 60 * 1000;
            using (new WaitDocumentChange(null, true, chromExtractionTimeoutMs))
            {
                OkDialog(associate, associate.OkDialog);
            }
            WaitForDocumentLoaded(chromExtractionTimeoutMs);
            RunUI(() => SkylineWindow.SaveDocument());
        }

        /// <summary>
        /// ProteoBench's HYE dataset mixes Human / Yeast / E.coli at engineered ratios
        /// so that Condition_B / Condition_A log2 fold-changes equal 0 / −1 / +2
        /// <summary>
        /// Per-species box plot — the other canonical HYE benchmark visualization. Reads
        /// the same per-peptide log2(B/A) values used by <see cref="AssertPerSpeciesLog2FoldChanges"/>,
        /// renders a box-and-whisker per species in a ZedGraph form, marks the expected
        /// value with a horizontal tick. Lets viewers see central tendency + spread per
        /// species at a glance — easier to read than the volcano for QC purposes.
        /// </summary>
        private void BuildAndShowSpeciesBoxPlot()
        {
            // Reuse the per-species log2FC values from the assertion routine's logic.
            var perSpecies = CollectPerSpeciesLog2FoldChanges();

            // Skyline doesn't have a built-in per-category distribution plot, and a custom
            // Form competes with PauseForScreenShot's screenshot machinery (form.Show is
            // non-modal — capture hangs). Render directly to a PNG via ZedGraph and drop
            // it in the screenshot folder next to the volcano. This bypasses
            // PauseForScreenShot entirely but produces a publication-quality plot.
            var pane = new ZedGraph.GraphPane(new System.Drawing.RectangleF(0, 0, 720, 480),
                @"Per-species log2(Condition B / A) — HYE benchmark", string.Empty, string.Empty);
            pane.Title.Text = @"log2 fold change distribution per species";
            pane.XAxis.Title.Text = @"Species";
            pane.YAxis.Title.Text = @"log2 (Condition B / Condition A)";
            pane.Legend.IsVisible = false;
            pane.XAxis.Type = ZedGraph.AxisType.Text;
            var labels = new[] { @"HUMAN", @"YEAST", @"ECOLI" };
            var colors = new Dictionary<string, Color>
            {
                { @"HUMAN", Color.FromArgb(160, 160, 160) },
                { @"YEAST", Color.FromArgb(30, 144, 255) },
                { @"ECOLI", Color.FromArgb(220, 20, 60) },
            };
            pane.XAxis.Scale.TextLabels = labels;
            pane.YAxis.Scale.Min = -4;
            pane.YAxis.Scale.Max = 4;
            pane.YAxis.Scale.MinAuto = pane.YAxis.Scale.MaxAuto = false;

            const double boxHalfWidth = 0.3;
            for (int i = 0; i < labels.Length; i++)
            {
                string species = labels[i];
                var values = perSpecies[species].OrderBy(v => v).ToArray();
                if (values.Length == 0) continue;
                double median = Quantile(values, 0.50);
                double q1     = Quantile(values, 0.25);
                double q3     = Quantile(values, 0.75);
                double whiskerLo = Quantile(values, 0.05);
                double whiskerHi = Quantile(values, 0.95);
                double x = i + 1; // ZedGraph text axis is 1-based

                // IQR box.
                var box = new ZedGraph.BoxObj(x - boxHalfWidth, q3, 2 * boxHalfWidth, q3 - q1,
                    Color.Black, colors[species])
                {
                    Location = { CoordinateFrame = ZedGraph.CoordType.AxisXYScale, AlignH = ZedGraph.AlignH.Left, AlignV = ZedGraph.AlignV.Top },
                    ZOrder = ZedGraph.ZOrder.E_BehindCurves,
                };
                pane.GraphObjList.Add(box);

                // Whiskers (vertical lines low→Q1 and Q3→high).
                pane.GraphObjList.Add(new ZedGraph.LineObj(Color.Black, x, whiskerLo, x, q1));
                pane.GraphObjList.Add(new ZedGraph.LineObj(Color.Black, x, q3, x, whiskerHi));
                // Whisker caps.
                pane.GraphObjList.Add(new ZedGraph.LineObj(Color.Black, x - boxHalfWidth * 0.6, whiskerLo, x + boxHalfWidth * 0.6, whiskerLo));
                pane.GraphObjList.Add(new ZedGraph.LineObj(Color.Black, x - boxHalfWidth * 0.6, whiskerHi, x + boxHalfWidth * 0.6, whiskerHi));
                // Median line.
                pane.GraphObjList.Add(new ZedGraph.LineObj(Color.Black, x - boxHalfWidth, median, x + boxHalfWidth, median) { Line = { Width = 2.5f } });
                // Expected log2FC marker (sign flipped because perSpecies uses log2(B/A) — same as ExpectedLog2FcBySpecies).
                double expected = ExpectedLog2FcBySpecies[species];
                pane.GraphObjList.Add(new ZedGraph.LineObj(Color.DarkGreen, x - boxHalfWidth * 1.2, expected, x + boxHalfWidth * 1.2, expected)
                    { Line = { Width = 2.0f, Style = System.Drawing.Drawing2D.DashStyle.Dash } });
            }

            // ZedGraph requires AxisChange to compute axis ranges; pass a Graphics object
            // since we're not attached to a control.
            using (var tmpBmp = new Bitmap(720, 480))
            using (var g = Graphics.FromImage(tmpBmp))
            {
                pane.AxisChange(g);
            }

            // Capture as the next sequential tutorial screenshot (s-15, immediately after the
            // s-14 volcano). Skyline has no built-in per-category distribution plot, and showing
            // a custom non-modal Form to screenshot it competes with the capture machinery and
            // hangs (see above). Instead, render the pane to a bitmap and hand it to the normal
            // PauseForScreenShot flow via processShot: it captures SkylineWindow but substitutes
            // our plot, saving it to the auto-numbered slot. PauseForScreenShot itself no-ops
            // unless screenshots are being recorded, so nothing is written on ordinary runs.
            using (var bmp = pane.GetImage(720, 480, 96))
            {
                PauseForScreenShot(SkylineWindow,
                    @"Per-species log2 fold-change distribution (HYE benchmark)",
                    processShot: shot => bmp);
            }
        }

        private static double Quantile(double[] sortedValues, double q)
        {
            if (sortedValues.Length == 0) return double.NaN;
            double pos = q * (sortedValues.Length - 1);
            int lo = (int)Math.Floor(pos), hi = (int)Math.Ceiling(pos);
            return lo == hi ? sortedValues[lo] : sortedValues[lo] + (pos - lo) * (sortedValues[hi] - sortedValues[lo]);
        }

        /// <summary>
        /// Shared helper for <see cref="AssertPerSpeciesLog2FoldChanges"/> and
        /// <see cref="BuildAndShowSpeciesBoxPlot"/> — extracts per-peptide log2(B/A) values
        /// grouped by species suffix from the imported document.
        /// </summary>
        private Dictionary<string, List<double>> CollectPerSpeciesLog2FoldChanges()
        {
            var perSpecies = new Dictionary<string, List<double>>(StringComparer.Ordinal);
            foreach (var key in ExpectedLog2FcBySpecies.Keys)
                perSpecies[key] = new List<double>();

            SrmDocument doc = null;
            RunUI(() => doc = SkylineWindow.DocumentUI);
            var measuredResults = doc.Settings.MeasuredResults;
            if (measuredResults == null) return perSpecies;
            var aIndices = ReplicateIndicesMatching(measuredResults, @"Condition_A");
            var bIndices = ReplicateIndicesMatching(measuredResults, @"Condition_B");
            foreach (var pepGroup in doc.MoleculeGroups)
            {
                string species = ClassifySpecies(pepGroup.Name);
                if (species == null) continue;
                foreach (PeptideDocNode peptide in pepGroup.Molecules)
                {
                    double? log2Fc = ComputePeptideLog2Fc(peptide, aIndices, bIndices);
                    if (log2Fc.HasValue) perSpecies[species].Add(log2Fc.Value);
                }
            }
            return perSpecies;
        }

        /// <summary>
        /// ProteoBench's HYE dataset mixes Human / Yeast / E.coli at engineered ratios
        /// so that Condition_B / Condition_A log2 fold-changes equal 0 / −1 / +2
        /// respectively. We compute per-peptide log2(B/A) from precursor peak areas,
        /// median over peptides per species, and assert the result is within
        /// <see cref="LOG2FC_TOLERANCE"/> of the expected. Loose bounds intentionally —
        /// DIA-NN's per-precursor quant has run-to-run noise and Skyline's import path
        /// applies its own peak-picking adjustments.
        /// </summary>
        private void AssertPerSpeciesLog2FoldChanges()
        {
            var perSpecies = CollectPerSpeciesLog2FoldChanges();

            var report = new System.Text.StringBuilder();
            report.AppendLine(@"Per-species median log2(Condition_B / Condition_A):");
            foreach (var kvp in ExpectedLog2FcBySpecies)
            {
                var values = perSpecies[kvp.Key];
                report.AppendLine($@"  {kvp.Key,-6}: n={values.Count,5}, expected {kvp.Value,+5:0.00}, " +
                                  (values.Count > 0
                                      ? $@"got {Median(values),+5:0.00} (mean {values.Average(),+5:0.00})"
                                      : @"got (no peptides)"));
            }
            string summary = report.ToString();
            // Surface the table in the test log even when the assertions pass.
            File.WriteAllText(@"DiannSearchPerf_Log2FC.txt", summary);

            foreach (var kvp in ExpectedLog2FcBySpecies)
            {
                var values = perSpecies[kvp.Key];
                AssertEx.IsTrue(values.Count >= MIN_QUANTIFIED_PEPTIDES_PER_SPECIES,
                    $@"{kvp.Key}: only {values.Count} quantified peptides (need >= {MIN_QUANTIFIED_PEPTIDES_PER_SPECIES}).{Environment.NewLine}{summary}");
                double median = Median(values);
                double delta = Math.Abs(median - kvp.Value);
                AssertEx.IsTrue(delta <= LOG2FC_TOLERANCE,
                    $@"{kvp.Key}: median log2FC {median:0.00} is {delta:0.00} away from expected {kvp.Value:0.00} (tolerance {LOG2FC_TOLERANCE:0.00}).{Environment.NewLine}{summary}");
            }
        }

        private static List<int> ReplicateIndicesMatching(MeasuredResults results, string token)
        {
            // ImportResultsNameDlg strips the longest common prefix from the replicate
            // names (e.g. files named LFQ_Orbitrap_AIF_Condition_A_Sample_Alpha_01.raw
            // become replicate "A_Sample_Alpha_01" — losing the "Condition_" anchor).
            // Match on the underlying file paths instead so classification is robust
            // regardless of the prefix-trim outcome.
            var matches = new List<int>();
            for (int i = 0; i < results.Chromatograms.Count; i++)
            {
                var chrom = results.Chromatograms[i];
                bool hit = chrom.MSDataFilePaths.Any(p =>
                    p.GetFileName().IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
                if (hit)
                    matches.Add(i);
            }
            return matches;
        }

        private static string ClassifySpecies(string proteinName)
        {
            // ProteoBench FASTA headers end in `_HUMAN`, `_YEAST`, `_ECOLI` (SwissProt
            // convention). Skip contaminants (`sp|Cont_…`) since they're not part of the
            // ratio design.
            if (string.IsNullOrEmpty(proteinName) || proteinName.IndexOf(@"Cont_", StringComparison.Ordinal) >= 0)
                return null;
            foreach (var species in ExpectedLog2FcBySpecies.Keys)
                if (proteinName.EndsWith(@"_" + species, StringComparison.Ordinal))
                    return species;
            return null;
        }

        private static double? ComputePeptideLog2Fc(PeptideDocNode peptide,
            IReadOnlyList<int> aIndices, IReadOnlyList<int> bIndices)
        {
            double aSum = 0, bSum = 0;
            int aCount = 0, bCount = 0;
            foreach (TransitionGroupDocNode tg in peptide.TransitionGroups)
            {
                if (tg.Results == null)
                    continue;
                AccumulateAreaSum(tg, aIndices, ref aSum, ref aCount);
                AccumulateAreaSum(tg, bIndices, ref bSum, ref bCount);
            }
            // Drop peptides that don't have at least one valid peak in each condition.
            if (aCount == 0 || bCount == 0 || aSum <= 0 || bSum <= 0)
                return null;
            return Math.Log(bSum / aSum, 2.0);
        }

        private static void AccumulateAreaSum(TransitionGroupDocNode tg,
            IReadOnlyList<int> replicateIndices, ref double sum, ref int count)
        {
            foreach (int idx in replicateIndices)
            {
                if (idx >= tg.Results.Count) continue;
                var infos = tg.Results[idx];
                if (infos.IsEmpty) continue;
                foreach (var info in infos)
                {
                    if (info?.Area > 0)
                    {
                        sum += info.Area.Value;
                        count++;
                    }
                }
            }
        }

        private static double Median(List<double> values)
        {
            if (values.Count == 0) return double.NaN;
            var sorted = values.OrderBy(v => v).ToArray();
            int mid = sorted.Length / 2;
            return sorted.Length % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];
        }

        private void PrepareDocument(string documentFile)
        {
            RunUI(SkylineWindow.NewDocument);
            RunUI(() => SkylineWindow.ModifyDocument(@"Set default settings",
                doc => doc.ChangeSettings(SrmSettingsList.GetDefault())));
            // The defaults give us Trypsin + 0 missed cleavages + charges 2–3, but DIA-NN
            // was run with Trypsin/P + 1 missed cleavage + charges 1–4. Without aligning
            // these, ImportPeptideSearchDlg's FASTA-vs-library intersection drops every
            // DIA-NN-identified peptide that crosses a K/R-P boundary, has a missed
            // cleavage, or has charge ∉ {2,3} — observed ~60 % precursor loss vs DIA-NN's
            // raw output. Match DIA-NN's digestion so Skyline keeps what DIA-NN found.
            RunUI(() => SkylineWindow.ModifyDocument(@"Align peptide settings with DIA-NN", doc =>
            {
                var trypsinP = Settings.Default.EnzymeList.FirstOrDefault(e => e.Name == @"Trypsin/P")
                               ?? Settings.Default.EnzymeList.First();
                var peptide = doc.Settings.PeptideSettings
                    .ChangeEnzyme(trypsinP)
                    .ChangeDigestSettings(new DigestSettings(1, false));
                var charges = pwiz.Skyline.Util.Adduct.ProtonatedFromCharges(1, 2, 3, 4);
                var oldFilter = doc.Settings.TransitionSettings.Filter;
                var newFilter = oldFilter.ChangePeptidePrecursorCharges(charges);
                // Default retention-time filter is ±5 min around every MS2 ID; across the 4
                // mzML files that's the bulk of the import wall-clock. The ms2_ids
                // filter uses the OBSERVED RT from each DIA-NN ID (not the prediction), so
                // the window only needs to cover the peak shape — DIA-NN's stats here show
                // peak FWHM ≈ 0.3 min, so ±0.5 min covers any real peak comfortably.
                var newFullScan = doc.Settings.TransitionSettings.FullScan
                    .ChangeRetentionTimeFilter(RetentionTimeFilterType.ms2_ids, 0.5);
                var transition = doc.Settings.TransitionSettings
                    .ChangeFilter(newFilter)
                    .ChangeFullScan(newFullScan);
                return doc.ChangeSettings(doc.Settings
                    .ChangePeptideSettings(peptide)
                    .ChangeTransitionSettings(transition));
            }));
            // Stable DocDir (not Guid-suffixed) so the cached predicted library + DIA-NN
            // search outputs persist for reuse across runs. Avoid Path.GetTempPath() — see
            // TestRunner's `~&TMP^` redirect; DIA-NN can't write there anyway.
            Directory.CreateDirectory(DocDir);
            // Wipe the previous .sky/.skyl/.sky.view but KEEP the library + parquet caches.
            foreach (var stale in Directory.EnumerateFiles(DocDir, @"DiannSearchPerf.*"))
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
                if (File.Exists(candidate.Path))
                    return candidate.Path;
            }

            var progress = new SilentProgressMonitor();
            AssertEx.IsTrue(pwiz.Skyline.Util.SimpleFileDownloader.DownloadRequiredFiles(
                DiannHelpers.FilesToDownload, progress));
            return DiannHelpers.DiannBinary;
        }

        #region DIA-NN cache management

        /// <summary>
        /// Copy the predicted spectral library that ships in the tutorial zip (extracted into
        /// <see cref="CacheDir"/>) to <see cref="DocDir"/>, where DIA-NN looks for it via
        /// ReuseCachedLibrary. This is what lets the first run on a fresh machine skip the
        /// ~15-20 min predictor build. If DocDir already has one (a previous run), keep it.
        /// </summary>
        private void SeedPredictedLibraryFromZip()
        {
            Directory.CreateDirectory(DocDir);
            var shipped = Path.Combine(CacheDir, PREDICTED_LIB_FILENAME);
            var target = Path.Combine(DocDir, PREDICTED_LIB_FILENAME);
            if (File.Exists(shipped) && !File.Exists(target))
                File.Copy(shipped, target);
        }

        /// <summary>
        /// Make sure DIA-NN's per-file `.quant` cache is populated for every input file,
        /// and that the predicted library is on disk in <see cref="DocDir"/>. The wizard's
        /// run uses DiannConfig.ReuseQuantFiles=true (`--use-quant`) which then skips the
        /// per-file search step entirely and goes straight to cross-run analysis. The
        /// per-file search code path is still exercised — by THIS method on cache misses.
        /// Normally a no-op because the `.quant` files ship in the tutorial zip; it only does
        /// real work if the cache was deleted (then it's the bulk of a fresh-search cost).
        /// </summary>
        private void EnsureCachedPresearch(string realDiannPath)
        {
            var diaPaths = DiaSearchPaths;
            string fasta = Path.Combine(CacheDir, FASTA_FILENAME);

            // If every input file already has a .quant alongside, skip the slow path.
            if (diaPaths.All(f => File.Exists(f + @".quant")))
                return;

            // Run DIA-NN against ALL inputs. This generates the predicted library in
            // DocDir (so the wizard can reuse it) AND a .quant file next to each mzML input.
            try
            {
                DiannHelpers.RegisteredDiannPathOverride = () => realDiannPath;
                var config = BuildPresearchConfig();
                IProgressStatus status = new ProgressStatus(@"DIA-NN presearch cache build");
                var progress = new SilentProgressMonitor();
                var enzyme = Settings.Default.EnzymeList.FirstOrDefault(e => e.Name == @"Trypsin/P")
                             ?? Settings.Default.EnzymeList.First();
                var fixedMods = new[] { UniMod.GetModification(@"Carbamidomethyl (C)", true) };
                var variableMods = new[] { UniMod.GetModification(@"Oxidation (M)", true).ChangeVariable(true) };

                DiannHelpers.RunSearch(diaPaths, fasta, DocDir, config,
                    progress, ref status, CancellationToken.None,
                    fixedMods, variableMods, enzyme);
            }
            finally
            {
                DiannHelpers.RegisteredDiannPathOverride = null;
            }

            // Sanity check: every input file should now have its .quant on disk.
            foreach (var path in diaPaths)
            {
                AssertEx.IsTrue(File.Exists(path + @".quant"),
                    $@"presearch did not produce .quant for {path}");
            }
        }

        private static DiannConfig BuildPresearchConfig()
        {
            var config = new DiannConfig
            {
                Ms1Accuracy = 0,
                Ms2Accuracy = 0,
                QValue = 0.01,
                Threads = Environment.ProcessorCount,
                MaxVarMods = 2,
                MaxMissedCleavages = 1,
                // Reuse the predicted library if a previous (possibly-failed) presearch
                // already built it — saves ~20 min.
                // Reuse .quant files too: when one is missing DIA-NN still searches that
                // file to create it, but when it exists we skip the redundant per-file
                // work. Lets us invalidate individual .quants without re-paying for the
                // others (e.g. after a FASTA change leaves some stale).
                ReuseCachedLibrary = true,
                ReuseQuantFiles = true,
            };
            config.AdditionalSettings[@"MinPepLen"].Value = 6;
            config.AdditionalSettings[@"MinPrecursorCharge"].Value = 1;
            config.AdditionalSettings[@"MaxPrecursorCharge"].Value = 4;
            config.ApplyAdditionalSettings();
            return config;
        }

        #endregion

    }
}
