/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Scoring;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// One run's training export (<c>--training-export</c>): each confidently identified target
    /// precursor with the observed intensities of its full b/y ladder and the interference
    /// evidence Osprey has for every ion (docs/22-training-export.md), written to
    /// <c>&lt;stem&gt;.training.parquet</c>. A product of <c>PerFileRescoring</c>: written while
    /// the run's spectra and reconciled boundaries are in hand when the flag is given up front,
    /// and from the run's own durable artifacts - the reconciled parquet, its q-value sidecar,
    /// calibration and spectra cache - when the flag is added to a finished run, so the two give
    /// the same file.
    ///
    /// <para>The q-values it selects on are the run's own, from one of two sidecars (see
    /// <see cref="ReadRunQ"/>): the second-pass run q the per-run second pass wrote in
    /// PerFileRescoring, or, for a run it did not write one for, the first-pass run q - which
    /// is the final answer when nothing in the analysis was re-scored, as for a single run.
    /// Experiment-level values exist only after SecondPassFDR and per-run files are write-once,
    /// so the export carries none; a consumer joins <c>&lt;blib&gt;.2nd-pass.fdr_experiment.bin</c>
    /// by entry_id.</para>
    /// </summary>
    internal static class TrainingExportWriter
    {
        /// <summary>The name the reconciled-parquet footer check gives the export.</summary>
        private static readonly string RECONCILED_CONSUMER = OspreyArgNames.Text(OspreyArgNames.TRAINING_EXPORT);

        /// <summary>Footer key naming the pass whose run q-values the export selected on.</summary>
        public const string KEY_RUN_Q_PASS = @"osprey.training_export.run_q_pass";

        /// <summary>
        /// One run: read its reconciled rows, run q-values, calibration and spectra; compute every
        /// exported precursor's evidence one isolation window at a time on up to
        /// <paramref name="maxThreads"/> threads; write the parquet; report the median-polish
        /// parity. <paramref name="spectra"/> and <paramref name="ms2Cal"/> are the run's, already
        /// loaded by the caller, or null to load them here.
        /// </summary>
        public static void ExportRun(string input, string output, IReadOnlyDictionary<uint, LibraryEntry> library,
            PipelineContext ctx, int maxThreads, SpectraWindowIndex spectra = null, MzCalibrationResult ms2Cal = null)
        {
            var sw = Stopwatch.StartNew();
            var config = ctx.Config;
            var settings = config.TrainingExport;
            double maxQ = settings.EffectiveMaxQ(config.RunFdr);
            double claimantQ = settings.EffectiveClaimantQ;
            string stem = Path.GetFileNameWithoutExtension(input);

            string reconciledPath = ParquetScoreCache.GetReconciledScoresPath(input);
            if (!File.Exists(reconciledPath))
            {
                throw new FileNotFoundException(string.Format(
                    OspreyTasksResources.TrainingExportWriter_ExportRun_The_reconciled_scores_file___0___is_missing__The_training_export_,
                    reconciledPath), reconciledPath);
            }
            // The footer check --task SecondPassFDR makes - this build's version, this search and
            // library, and a reconciled file - on every route, since an export written from disk
            // has nothing else vouching for the file. Another build is named apart: every other
            // stage skips by its validity key, which carries no build, so the generic "score the
            // file again" remedy would only skip again.
            string otherBuild = OtherBuild(reconciledPath);
            if (otherBuild != null)
            {
                throw new InvalidDataException(string.Format(
                    OspreyTasksResources.TrainingExportWriter_ExportRun__0__was_written_by_Osprey__1___not_this_build___2____Add__3__with_the_build_that_ran_the_analysis_,
                    reconciledPath, otherBuild, OspreyVersion.Current, RECONCILED_CONSUMER));
            }
            string footerError = ParquetScoreCache.ValidateScoresParquetGroup(new[] { reconciledPath }, config,
                OspreyVersion.Current, RECONCILED_CONSUMER);
            if (footerError != null)
                throw new InvalidDataException(footerError);
            // Read back from the file even when the caller has just written it, so an export
            // made while re-scoring and one made later from disk see the same rows.
            var rows = ParquetScoreCache.LoadTrainingExportRows(reconciledPath);
            var runQ = ReadRunQ(input, out var runQPass, out string runQPath);
            if (ms2Cal == null)
                ScoringTaskShared.LoadMassCalibrations(input, OspreyTasksResources.TrainingExportWriter_ExportRun_The_training_export, out ms2Cal, out _, out _);
            MzCalibration.CalibratedTolerance(ms2Cal, config.FragmentTolerance.Tolerance, config.FragmentTolerance.Unit,
                out double tolerance, out ToleranceUnit toleranceUnit);
            var searchConfig = config.ShallowClone();
            searchConfig.FragmentTolerance = new FragmentToleranceConfig { Tolerance = tolerance, Unit = toleranceUnit };
            ScoringPipeline.DoubleCountingTolerance(ms2Cal, config, out double ddcTolerance, out ToleranceUnit ddcUnit);
            var index = spectra ?? ScoringTaskShared.LoadSpectraForRescore(input, stem, OspreyTasksResources.TrainingExportWriter_ExportRun_The_training_export, false, ctx);
            double rtNeighborhood = ScoringPipeline.DoubleCountingRtNeighborhood(index.AllMs2Rts);
            var evidenceSettings = new TrainingEvidenceSettings
            {
                SearchConfig = searchConfig,
                WriteXics = settings.WriteXics,
            };

            // This run's targets that the library still describes; decoys are never exported.
            var targets = PairTargets(reconciledPath, rows, library, runQPath, runQ, out int nNoLibrary);
            int nToExport = targets.Count(t => t.RunQ <= maxQ);
            int nNoRunQ = targets.Count(t => double.IsNaN(t.RunQ));

            // Evidence, one isolation window at a time: every target is placed in the window
            // whose spectra hold its apex scan - the window it was scored in. Each window's ions
            // are judged against the m/z range its own spectra measured.
            var windows = index.IsolationWindows;
            var perWindow = new List<TrainingRecord>[windows.Count];
            var observed = new double[windows.Count][];
            // An export made later reads every window of the run from disk, where serial block reads
            // took a cold SEA-AD run from ~52 s to ~29 s on a spinning disk. Straight after the
            // rescore the caller passes the index whose windows it has just streamed; they are warm,
            // and there parallel LoadWindow was ~0.8 s per run faster.
            var provider = new StreamingWindowSpectraProvider(index, ms2Cal, serialBlockReads: spectra == null);
            // Reported because, read from disk, a run's windows take tens of seconds on cohort-scale
            // data, and silence that long reads as a hang. At the I/O cadence rather than the
            // rescore's 2 s: at 2 s this loop printed ~19 percent lines per SEA-AD run.
            int nDone = 0;
            using (var progress = new ProgressReporter(OspreyTasksResources.TrainingExportWriter_ExportRun_Exporting_isolation_windows,
                       windows.Count, @"  ", ProgressReporter.IO_INTERVAL_SECONDS))
            {
                Parallel.For(0, windows.Count, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, maxThreads) }, w =>
                {
                    perWindow[w] = ExportWindow(windows[w], provider, targets, maxQ, claimantQ, evidenceSettings,
                        rtNeighborhood, ddcTolerance, ddcUnit, out observed[w]);
                    progress.Report(Interlocked.Increment(ref nDone));
                });
            }

            var records = perWindow.Where(list => list != null).SelectMany(list => list)
                .OrderBy(r => r.EntryId).ToList();
            foreach (var record in records)
                Complete(record, library[record.EntryId], stem);
            int nFitted = records.Count(r => r.MpFitted);
            int nFittedParity = records.Count(r => r.MpFitted && r.MpCosineParity);
            int nParity = records.Count(r => r.MpCosineParity);
            int nEntrapment = records.Count(r => r.IsEntrapment);
            int nUnplaced = nToExport - records.Count;

            var source = SpectrumFileReader.TryReadSourceMetadata(input);
            var metadata = BuildMetadata(config, stem, index, observed, source, runQPass, ms2Cal, tolerance,
                toleranceUnit, ddcTolerance, ddcUnit, rtNeighborhood, maxQ, claimantQ, nParity, nFittedParity,
                nFitted, records.Count);
            TrainingExportParquet.Write(output, records, metadata, settings.WriteXics);
            sw.Stop();

            string pass = runQPass == FdrScoresSidecar.Pass.SecondPass
                ? OspreyTasksResources.TrainingExportWriter_ExportRun_second_pass
                : OspreyTasksResources.TrainingExportWriter_ExportRun_first_pass;
            ctx.LogInfo(LogTag.TRAIN_EXPORT, string.Format(
                OspreyTasksResources.TrainingExportWriter_ExportRun__0____1_N0__target_precursors_at_run_q____2___3_N0__entrapment__of__4_N0__reconciled_targets,
                stem, records.Count, maxQ.ToString(@"R", CultureInfo.InvariantCulture), nEntrapment, targets.Count, pass,
                sw.Elapsed.TotalSeconds));
            ctx.LogInfo(LogTag.TRAIN_EXPORT, string.Format(
                OspreyTasksResources.TrainingExportWriter_ExportRun__0___median_polish_cosine_reproduced_for__1_N0__of__2_N0__fitted_precursors,
                stem, nFittedParity, nFitted, records.Count - nFitted));
            if (nFittedParity != nFitted)
            {
                ctx.LogWarning(string.Format(
                    OspreyTasksResources.TrainingExportWriter_ExportRun__0____1_N0__of__2_N0__fitted_precursors_did_not_reproduce_the_scored_median_polish_cosine_,
                    stem, nFitted - nFittedParity, nFitted));
            }
            if (runQPass == FdrScoresSidecar.Pass.FirstPass)
            {
                ctx.LogWarning(string.Format(
                    OspreyTasksResources.TrainingExportWriter_ExportRun__0___selected_by_the_first_pass_run_q_values__this_run_s_second_pass__if_the_analysis_computes_one__comes_from__1__,
                    stem, SecondPassFdrTask.TASK_NAME));
            }
            if (nNoRunQ > 0)
            {
                ctx.LogWarning(CountText.Format(nNoRunQ,
                    OspreyTasksResources.TrainingExportWriter_ExportRun__1___1_reconciled_target_has_no_run_q_value_at_its_final_apex_in___2___and_could_not_be_selected_,
                    OspreyTasksResources.TrainingExportWriter_ExportRun__1____0_N0__reconciled_targets_have_no_run_q_value_at_their_final_apex_in___2___and_could_not_be_selected_,
                    stem, runQPath));
            }
            if (nUnplaced > 0)
            {
                ctx.LogWarning(CountText.Format(nUnplaced,
                    OspreyTasksResources.TrainingExportWriter_ExportRun__1___1_precursor_had_no_isolation_window_holding_its_apex_scan_and_was_not_exported_,
                    OspreyTasksResources.TrainingExportWriter_ExportRun__1____0_N0__precursors_had_no_isolation_window_holding_their_apex_scan_and_were_not_exported_,
                    stem));
            }
            if (nNoLibrary > 0)
            {
                ctx.LogWarning(CountText.Format(nNoLibrary,
                    OspreyTasksResources.TrainingExportWriter_ExportRun__1___1_reconciled_target_has_no_library_spectrum_and_was_skipped_,
                    OspreyTasksResources.TrainingExportWriter_ExportRun__1____0_N0__reconciled_targets_have_no_library_spectrum_and_were_skipped_,
                    stem));
            }
            if (source == null)
            {
                ctx.LogWarning(string.Format(
                    OspreyTasksResources.TrainingExportWriter_ExportRun__0___the_source_file___1___is_not_here_or_cannot_be_read__so_the_instrument_,
                    stem, input));
            }
        }

        /// <summary>
        /// The build that wrote <paramref name="reconciledPath"/> when it is not this one, else
        /// null - including when the footer cannot be read or names no search or library, which
        /// the full footer check reports in its own words.
        /// </summary>
        private static string OtherBuild(string reconciledPath)
        {
            Dictionary<string, string> footer;
            try
            {
                footer = ParquetScoreCache.LoadFooterMetadata(reconciledPath);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return null;
            }
            footer.TryGetValue(@"osprey.version", out string version);
            footer.TryGetValue(@"osprey.search_hash", out string search);
            footer.TryGetValue(@"osprey.library_hash", out string library);
            if (version == null || search == null || library == null)
                return null;
            // The file's own search and library as the expected ones, so only the build can differ.
            string error = ParquetScoreCache.CheckParquetMetadata(reconciledPath, version, search, library,
                search, library, OspreyVersion.Current);
            return error != null ? version : null;
        }

        /// <summary>
        /// The run q-values an export selects on, as records keyed by (entry_id, apex RT), from
        /// the sidecar <see cref="RunQPath"/> chooses, and which pass they are.
        /// </summary>
        internal static List<FdrScoreRecord> ReadRunQ(string input, out FdrScoresSidecar.Pass pass, out string path)
        {
            path = RunQPath(input, out pass);
            var records = new List<FdrScoreRecord>();
            if (!FdrScoresSidecar.ReadRecords(path, pass, records.Add))
            {
                throw new InvalidDataException(string.Format(
                    OspreyTasksResources.TrainingExportWriter_ReadRunQ_The_intermediate_file___0___is_missing_or_unreadable_, path));
            }
            return records;
        }

        /// <summary>
        /// Each reconciled target row with the library entry it names and its own run q record.
        /// <paramref name="nNoLibrary"/> counts the target rows whose library spectrum is gone,
        /// which are not returned.
        ///
        /// <para>A record is paired by (entry_id, apex RT), not entry_id alone: Stage 6 gap-fill
        /// can leave two reconciled rows of one entry_id in a run (one target scored in two
        /// overlapping isolation windows), and each must take the record of its own peak.
        /// Every writer of a q-value sidecar records the row's own apex RT, so the pair is exact
        /// whatever order the writer used. Two rows of one entry_id come from different windows,
        /// so different scans and apex RTs; a pair the key cannot separate is refused, never
        /// overwritten.</para>
        ///
        /// <para>A row whose modified sequence or charge is not its library entry's is refused
        /// too: the ids came from another library or another build, and exporting the pair
        /// would put one precursor's ladder on another's peak.</para>
        /// </summary>
        internal static List<Target> PairTargets(string reconciledPath, IReadOnlyList<FdrEntry> rows,
            IReadOnlyDictionary<uint, LibraryEntry> library, string runQPath,
            IReadOnlyList<FdrScoreRecord> runQRecords, out int nNoLibrary)
        {
            var runQ = new Dictionary<(uint EntryId, long ApexRtBits), FdrScoreRecord>(runQRecords.Count);
            foreach (var r in runQRecords)
            {
                if (!runQ.TryAdd(ObservationKey(r.EntryId, r.ApexRt), r))
                {
                    throw new InvalidDataException(string.Format(
                        OspreyTasksResources.TrainingExportWriter_PairTargets_The_intermediate_file___0___holds_two_records_for_precursor_candidate__1__at_apex_RT__2_,
                        runQPath, r.EntryId, r.ApexRt.ToString(@"R", CultureInfo.InvariantCulture)));
                }
            }
            var targets = new List<Target>();
            var paired = new HashSet<(uint EntryId, long ApexRtBits)>();
            nNoLibrary = 0;
            foreach (var row in rows)
            {
                if (row.IsDecoy)
                    continue;
                if (!library.TryGetValue(row.EntryId, out var entry))
                {
                    nNoLibrary++;
                    continue;
                }
                if (!string.Equals(row.ModifiedSequence, entry.ModifiedSequence, StringComparison.Ordinal) ||
                    row.Charge != entry.Charge)
                {
                    throw new InvalidDataException(string.Format(
                        OspreyTasksResources.TrainingExportWriter_PairTargets_The_reconciled_scores_file___0___names_precursor_candidate__1__as__2___3____,
                        reconciledPath, row.EntryId, row.ModifiedSequence, row.Charge, entry.ModifiedSequence, entry.Charge));
                }
                if (entry.IsSpectrumReleased)
                {
                    nNoLibrary++;
                    continue;
                }
                var key = ObservationKey(row.EntryId, row.ApexRt);
                FdrScoreRecord? record = null;
                if (runQ.TryGetValue(key, out var found))
                {
                    if (!paired.Add(key))
                    {
                        throw new InvalidDataException(string.Format(
                            OspreyTasksResources.TrainingExportWriter_PairTargets_The_reconciled_scores_file___0___holds_two_peaks_for_precursor_candidate__1__at_,
                            reconciledPath, row.EntryId, row.ApexRt.ToString(@"R", CultureInfo.InvariantCulture), runQPath));
                    }
                    record = found;
                }
                targets.Add(new Target(row, entry, record));
            }
            return targets;
        }

        /// <summary>
        /// The library's targets by entry id. The in-process map when an earlier stage of this
        /// run still holds it - the same artifact, already loaded - and a load from the library
        /// (its <c>.libcache</c>) otherwise, which is every export written from disk.
        ///
        /// <para>The load retains fragments only for the base_ids the first pass kept (the
        /// retained summary), as <c>--task SecondPassFDR</c> loads it: every row the export
        /// reads is a reconciled survivor, so no row it exports or compares against loses its
        /// spectrum. Without a readable summary it loads everything. A load indexes the targets
        /// alone, since decoys are never exported; the in-process map is used as it stands.</para>
        ///
        /// <para><paramref name="demandPipelineLibrary"/> takes the pipeline's own library even
        /// when nothing has loaded it yet - for an export-only arm followed by a SecondPassFDR
        /// that will load it anyway, so the process loads the library once rather than twice.</para>
        /// </summary>
        public static IReadOnlyDictionary<uint, LibraryEntry> ResolveTargets(PipelineContext ctx, bool demandPipelineLibrary)
        {
            if (demandPipelineLibrary)
                return ctx.Get<LibraryById>().Value;
            if (ctx.TryGet(out LibraryById loaded))
                return loaded.Value;
            var options = new LibraryLoadOptions
            {
                RetainFragmentsFor = ScoringTaskShared.ReadRetainedBaseIds(ctx.Config, out _),
            };
            var library = LibraryLoader.Load(ctx.Config, options, ctx, ctx.LogWarning, out string error);
            if (error != null || library == null || library.Count == 0)
            {
                ctx.LogError(error ?? OspreyTasksResources.TrainingExportWriter_ResolveTargets_The_library_is_empty_after_loading_);
                ctx.ExitCode = 1;
                return null;
            }
            var targets = new Dictionary<uint, LibraryEntry>(library.Count(e => !e.IsDecoy));
            foreach (var entry in library)
            {
                if (!entry.IsDecoy)
                    targets[entry.Id] = entry;
            }
            return targets;
        }

        /// <summary>
        /// The identities of the per-run artifacts one run's export reads - its reconciled
        /// parquet, the q-value sidecar it selects on, calibration and spectra cache (name, size,
        /// mtime; <c>absent</c> for a missing file) - so a rewritten input redoes that run's
        /// export and no other. Only the sidecar the export reads: for a run with no Stage 6
        /// work, or when PerFileRescoring had no readable saved first-pass model, SecondPassFDR
        /// writes the second-pass sidecar after the export, and a key that followed it would
        /// redo those exports on the next resume for a file none of them read.
        /// </summary>
        public static string RunInputIdentities(string input)
        {
            string runQPath = RunQPath(input, out var pass);
            return @";recon=" + IdentityOrAbsent(ParquetScoreCache.GetReconciledScoresPath(input))
                + @";runq=" + (pass == FdrScoresSidecar.Pass.SecondPass ? @"2:" : @"1:") + IdentityOrAbsent(runQPath)
                + @";calib=" + IdentityOrAbsent(CalibrationIO.CalibrationPathForInput(input, ArtifactPaths.ResolveOutputDir(input)))
                + @";spectra=" + IdentityOrAbsent(SpectraCache.GetCachePath(input));
        }

        /// <summary>The artifacts one run's export reads.</summary>
        public static IEnumerable<string> RunInputs(string input)
        {
            yield return ParquetScoreCache.GetReconciledScoresPath(input);
            yield return RunQPath(input, out _);
            yield return CalibrationIO.CalibrationPathForInput(input, ArtifactPaths.ResolveOutputDir(input));
            yield return SpectraCache.GetCachePath(input);
        }

        /// <summary>
        /// The q-value sidecar a run's export selects on, and which pass it is. The second-pass
        /// sidecar when the per-run second pass in PerFileRescoring wrote it; otherwise the
        /// first-pass sidecar - a run with no Stage 6 work, or a PerFileRescoring with no
        /// readable saved first-pass model, whose second pass SecondPassFDR computes after the
        /// export. Deciding by who wrote the file, which never changes once it is written, makes
        /// an export written while re-scoring and one written later from disk agree.
        ///
        /// <para>Who wrote it is the worker's stamp beside the sidecar (the test SecondPassFDR
        /// folds by, <see cref="Pass2FdrSidecar.HasWorkerStamp"/>) AND the worker's decoys file,
        /// which only the worker writes. The stamp alone is not proof: after a later
        /// PerFileRescoring run the driver stamps every declared output that exists, including a
        /// second-pass sidecar SecondPassFDR wrote, which would flip the export to that sidecar
        /// on the next invocation of the same command. The driver stamps files; it never
        /// creates one.</para>
        /// </summary>
        internal static string RunQPath(string input, out FdrScoresSidecar.Pass pass)
        {
            bool workerOwned = Pass2FdrSidecar.HasWorkerStamp(input) &&
                               File.Exists(Pass2CompetitionDecoys.PathFor(input));
            pass = workerOwned ? FdrScoresSidecar.Pass.SecondPass : FdrScoresSidecar.Pass.FirstPass;
            return workerOwned ? FdrScoresSidecar.Pass2Path(input) : FdrScoresSidecar.Pass1Path(input);
        }

        /// <summary>
        /// One isolation window: load its calibrated spectra once, place the targets whose
        /// apex scan it holds, and compute the evidence of those to export against the ones
        /// that claim peaks here. <paramref name="observedMzRange"/> is the m/z range its
        /// spectra measured, which its ions are judged against.
        /// </summary>
        private static List<TrainingRecord> ExportWindow(IsolationWindow window,
            StreamingWindowSpectraProvider provider, List<Target> targets, double maxQ, double claimantQ,
            TrainingEvidenceSettings settings, double rtNeighborhood, double ddcTolerance, ToleranceUnit ddcUnit,
            out double[] observedMzRange)
        {
            var result = new List<TrainingRecord>();
            int windowKey = (int)Math.Round(window.Center * 10.0);
            var evidenceWindow = new TrainingEvidenceWindow(provider.GetCalibratedWindow(windowKey));
            observedMzRange = evidenceWindow.ObservedMzRange;
            var members = targets.Where(t => window.Contains(t.Entry.PrecursorMz) &&
                                             evidenceWindow.TryGetScanIndex(t.Row.ScanNumber, out _)).ToList();
            if (members.Count == 0)
                return result;

            var windowSettings = settings.WithMs2ScanWindow(evidenceWindow.ObservedMzRange);
            var tolerance = windowSettings.SearchConfig.FragmentTolerance;
            var claimants = members.Where(t => t.RunQ <= claimantQ)
                .Select(t => new TrainingClaimant(t.Entry, t.Row, t.RunQ, t.Score, evidenceWindow, tolerance))
                .ToList();
            var neighbors = members.OrderBy(t => t.Row.ApexRt).ThenBy(t => t.Row.EntryId)
                .Select(t => (t.Entry, t.Row.ApexRt)).ToList();
            foreach (var target in members)
            {
                if (!(target.RunQ <= maxQ))
                    continue;
                var record = TrainingEvidence.Compute(target.Entry, target.Row, target.RunQ, target.Score,
                    evidenceWindow, claimants, windowSettings);
                record.RunPeptideQ = target.RunPeptideQ;
                record.DdcNeighborCount = TrainingEvidence.CountDoubleCountingNeighbors(target.Entry,
                    target.Row.ApexRt, neighbors, rtNeighborhood, ddcTolerance, ddcUnit);
                result.Add(record);
            }
            return result;
        }

        /// <summary>The run-independent fields: file and entrapment class.</summary>
        private static void Complete(TrainingRecord record, LibraryEntry entry, string stem)
        {
            record.FileName = stem;
            var kind = EntrapmentLibraryClassifier.Classify(entry.ProteinIds);
            record.IsEntrapment = kind == PeptideKind.PTarget || kind == PeptideKind.PDecoy;
            record.PeptideKind = PeptideKindName(kind);
        }

        /// <summary>The FDRBench manifest spelling of a peptide kind.</summary>
        private static string PeptideKindName(PeptideKind kind)
        {
            switch (kind)
            {
                case PeptideKind.PTarget:
                    return @"p_target";
                case PeptideKind.Decoy:
                    return @"decoy";
                case PeptideKind.PDecoy:
                    return @"p_decoy";
                default:
                    return @"target";
            }
        }

        private static Dictionary<string, string> BuildMetadata(OspreyConfig config, string stem,
            SpectraWindowIndex index, double[][] observedMzRanges, SourceRunMetadata source,
            FdrScoresSidecar.Pass runQPass, MzCalibrationResult ms2Cal, double tolerance,
            ToleranceUnit toleranceUnit, double ddcTolerance, ToleranceUnit ddcUnit, double rtNeighborhood,
            double maxQ, double claimantQ, int nParity, int nFittedParity, int nFitted, int nRows)
        {
            var ic = CultureInfo.InvariantCulture;
            string R(double v) => v.ToString(@"R", ic);
            string Unit(ToleranceUnit u) => u == ToleranceUnit.Ppm ? @"ppm" : @"Th";
            var rts = index.AllMs2Rts;
            var measured = observedMzRanges.Where(r => r != null).ToList();
            var metadata = new Dictionary<string, string>
            {
                [@"osprey.version"] = OspreyVersion.Current,
                [@"osprey.search_hash"] = config.Identity.SearchParameterHash(),
                [@"osprey.library_hash"] = config.Identity.LibraryIdentityHash(),
                [@"osprey.file_name"] = stem,
                [@"osprey.training_export.rows"] = nRows.ToString(ic),
                [@"osprey.training_export.max_q"] = R(maxQ),
                [@"osprey.training_export.claimant_q"] = R(claimantQ),
                [KEY_RUN_Q_PASS] = runQPass == FdrScoresSidecar.Pass.SecondPass ? @"2" : @"1",
                [@"osprey.training_export.xics"] = config.TrainingExport.WriteXics ? @"true" : @"false",
                [@"osprey.training_export.slot_order"] = @"slot = p*4 + t; t: 0 b z1, 1 b z2, 2 y z1, 3 y z2; position p holds b(p+1) and y(L-1-p)",
                [@"osprey.training_export.mp_cosine_parity"] = string.Format(ic, @"{0}/{1}", nParity, nRows),
                [@"osprey.training_export.mp_cosine_parity_fitted"] = string.Format(ic, @"{0}/{1}", nFittedParity, nFitted),
                [@"osprey.rt_min"] = rts.Count > 0 ? R(rts.Min()) : string.Empty,
                [@"osprey.rt_max"] = rts.Count > 0 ? R(rts.Max()) : string.Empty,
                [@"osprey.isolation_mz_min"] = index.IsolationWindows.Count > 0 ? R(index.IsolationWindows.Min(w => w.LowerBound)) : string.Empty,
                [@"osprey.isolation_mz_max"] = index.IsolationWindows.Count > 0 ? R(index.IsolationWindows.Max(w => w.UpperBound)) : string.Empty,
                [@"osprey.ms2_scan_window"] = measured.Count > 0
                    ? string.Format(ic, @"{0},{1}", R(measured.Min(r => r[0])), R(measured.Max(r => r[1])))
                    : string.Empty,
                [@"osprey.fragment_tolerance"] = R(tolerance),
                [@"osprey.fragment_tolerance_unit"] = Unit(toleranceUnit),
                [@"osprey.ms2_calibration.calibrated"] = ms2Cal.Calibrated ? @"true" : @"false",
                [@"osprey.ms2_calibration.mean"] = R(ms2Cal.Mean),
                [@"osprey.ms2_calibration.sd"] = R(ms2Cal.SD),
                [@"osprey.ms2_calibration.unit"] = ms2Cal.Unit ?? string.Empty,
                [@"osprey.ddc.tolerance"] = R(ddcTolerance),
                [@"osprey.ddc.tolerance_unit"] = Unit(ddcUnit),
                [@"osprey.ddc.rt_neighborhood"] = R(rtNeighborhood),
                [@"osprey.instrument_vendor"] = source?.InstrumentVendor ?? string.Empty,
                [@"osprey.instrument_model"] = source?.InstrumentModel ?? string.Empty,
                [@"osprey.source_ms2_sampled"] = (source?.NMs2Sampled ?? 0).ToString(ic),
                [@"osprey.dissociation_methods"] = source != null ? JsonConvert.SerializeObject(source.DissociationMethods) : string.Empty,
                [@"osprey.collision_energies"] = source != null ? JsonConvert.SerializeObject(source.CollisionEnergies) : string.Empty,
                [@"osprey.ms2_mass_analyzers"] = source != null ? JsonConvert.SerializeObject(source.MassAnalyzers) : string.Empty,
            };
            return metadata;
        }

        /// <summary>One observation's identity: its entry_id and the exact bits of its apex RT.</summary>
        private static (uint EntryId, long ApexRtBits) ObservationKey(uint entryId, double apexRt)
        {
            return (entryId, BitConverter.DoubleToInt64Bits(apexRt));
        }

        private static string IdentityOrAbsent(string path)
        {
            return File.Exists(path) ? SearchIdentity.FileIdentityTerm(path) : @"absent";
        }

        /// <summary>
        /// A reconciled target row with its library entry and the three run values the export
        /// reads from its q-value record. The row is the export projection's
        /// (<see cref="ParquetScoreCache.LoadTrainingExportRows"/>): scalars, PIN features and
        /// the reference XIC, with no CWT candidates or fragment arrays.
        /// </summary>
        internal sealed class Target
        {
            public Target(FdrEntry row, LibraryEntry entry, FdrScoreRecord? record)
            {
                Row = row;
                Entry = entry;
                RunQ = record?.RunPrecursorQvalue ?? double.NaN;
                RunPeptideQ = record?.RunPeptideQvalue ?? double.NaN;
                Score = record?.Score ?? double.NaN;
            }

            public FdrEntry Row { get; }
            public LibraryEntry Entry { get; }

            /// <summary>Run precursor q; NaN when the run has no record, which no threshold admits.</summary>
            public double RunQ { get; }
            public double RunPeptideQ { get; }
            public double Score { get; }
        }
    }
}
