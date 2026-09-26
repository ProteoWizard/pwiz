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
using System.Threading.Tasks;
using Newtonsoft.Json;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Scoring;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// The optional fifth task (<c>--training-export</c>): for every run, write
    /// <c>&lt;stem&gt;.training.parquet</c> - each confidently identified target precursor with
    /// the observed intensities of its full b/y ladder and the interference evidence Osprey
    /// has for every ion (docs/22-training-export.md). It exists so a model trained on Osprey's
    /// identifications (CarafeSharp) never has to read raw data itself.
    ///
    /// <para>A fan-out over runs after <c>SecondPassFDR</c>, because it needs the final
    /// answer: the reconciled boundaries (Stage 6) and the second-pass q-values and PEP (Stage
    /// 7). It is not part of Stage 7 - a join that holds no spectra, and whose outputs would
    /// then re-run the whole join whenever an export setting changed - and it reads only
    /// artifacts, never in-process state, so a straight-through run, a pay-later run (the flag
    /// added to a finished directory runs this task alone) and an HPC node running
    /// <c>--task TrainingExport</c> do the identical work. Per run it holds that run's
    /// reconciled target rows and the spectra of the isolation windows in flight - one per
    /// worker thread, so up to <c>--threads</c> windows; its baseline is the library (its
    /// fragments retained only for the precursors the first pass kept) and the second pass's
    /// experiment sidecar, both O(library).</para>
    ///
    /// <para>With the option off the task is not in the run at all
    /// (<see cref="IsEnabled"/>): not run, not stamped, not logged.</para>
    /// </summary>
    internal sealed class TrainingExportTask : OspreyTask
    {
        /// <summary>
        /// This task's name, as a constant so the CLI selector, the validity stamp and the
        /// tests all spell it from here.
        /// </summary>
        public const string TASK_NAME = @"TrainingExport";

        /// <summary>The name the spectra-cache and calibration errors give this task.</summary>
        private const string CONSUMER = @"Training export";

        /// <summary>The name the reconciled-parquet footer check gives this task.</summary>
        private const string RECONCILED_CONSUMER = @"--training-export";

        public override string Name => TASK_NAME;

        /// <summary>One run at a time; never an experiment-wide score or product.</summary>
        public override bool IsPerFileWorker => true;

        /// <summary>
        /// Only when asked for, and never under a diagnostics-only render, which writes no
        /// artifact but its report.
        /// </summary>
        public override bool IsEnabled(OspreyConfig config)
        {
            return config.TrainingExport.Enabled && !config.DiagnosticsOnly;
        }

        /// <summary>Selecting the task by name asks for the export.</summary>
        public override void ApplySelection(OspreyConfig config)
        {
            config.TrainingExport.Enabled = true;
        }

        public override string DescribeOutput(OspreyConfig config)
        {
            return @"per-file .training.parquet (reads the finished second pass; no other artifact is written)";
        }

        public override IEnumerable<string> Inputs(PipelineContext ctx)
        {
            var config = ctx.Config;
            if (config.LibrarySource != null)
                yield return config.LibrarySource.Path;
            string experiment = ExperimentSidecarPath(config);
            if (!string.IsNullOrEmpty(experiment))
                yield return experiment;
            string retained = RetainedBaseIdSidecar.PathFor(config.OutputBlib, ScoringTaskShared.ArtifactSiblingPath(config));
            if (!string.IsNullOrEmpty(retained))
                yield return retained;
            if (config.InputFiles == null)
                yield break;
            foreach (var input in config.InputFiles)
            {
                foreach (var path in RunInputs(input))
                    yield return path;
            }
        }

        public override IEnumerable<string> Outputs(PipelineContext ctx)
        {
            if (ctx.Config.InputFiles == null)
                yield break;
            foreach (var input in ctx.Config.InputFiles)
                yield return TrainingExportParquet.PathFor(input);
        }

        /// <summary>
        /// The base key, the FDR sidecar format, the arms that change the q-values it exports
        /// (the same ones <c>SecondPassFDR</c> keys on), the IDENTITY of the second pass's
        /// experiment sidecar, and the export settings. The experiment sidecar stands in for
        /// the whole finished second pass: it is rewritten whenever Stage 7 re-runs, so an
        /// export of an older second pass is recomputed rather than adopted. Like every
        /// per-run key it names no cohort (P4) - an export does not depend on which other runs
        /// a node was handed - and no term differs between a straight-through run and a
        /// <c>--task TrainingExport</c> node, so each adopts the other's output.
        /// </summary>
        public override string ValidityKey(PipelineContext ctx)
        {
            var config = ctx.Config;
            var settings = config.TrainingExport;
            return base.ValidityKey(ctx)
                + @";fdrsidecar=" + FdrScoresSidecar.FormatVersion
                + OspreyEnvironment.ExperimentAggValidityKeySuffix()
                + OspreyEnvironment.Pass2QValueValidityKeySuffix()
                + OspreyEnvironment.TrainSampleValidityKeySuffix()
                + LibraryFragmentRelease.ValidityKeySuffix(ctx)
                + @";pass2exp=" + SearchIdentity.FileIdentityTerm(ExperimentSidecarPath(config))
                + string.Format(CultureInfo.InvariantCulture, @";trainexport={0};maxq={1:R};claimq={2:R};xics={3}",
                    TrainingExportParquet.FORMAT_VERSION, settings.EffectiveMaxQ(config.RunFdr),
                    settings.EffectiveClaimantQ, settings.WriteXics ? 1 : 0);
        }

        /// <summary>
        /// One run's export also reads that run's own artifacts - its reconciled parquet, its
        /// <c>.2nd-pass.fdr_scores.bin</c> and its <c>.run-info.json</c> - so their identities
        /// (name, size, mtime; <c>absent</c> for a missing file) key that run's output and no
        /// other. A rewritten input redoes one run, never the cohort, and the task key itself
        /// still names no run (P4). Rebuilding a spectra cache writes the run info, so it
        /// invalidates an export made without it. The identities follow mtimes, so a relay
        /// that rewrites them redoes the export: copy with <c>cp -p</c> or robocopy
        /// <c>/COPY:DAT</c>, as the library already requires.
        /// </summary>
        public override string OutputValidityKey(PipelineContext ctx, string taskKey, string output)
        {
            string input = InputFor(ctx.Config, output);
            return input == null ? taskKey : taskKey + RunInputIdentities(input);
        }

        /// <summary>Publishes nothing, so nothing ever rehydrates it.</summary>
        public override bool Rehydrate(PipelineContext ctx) => true;

        public override bool Run(PipelineContext ctx)
        {
            var config = ctx.Config;
            string key = ValidityKey(ctx);

            // The baseline, loaded once: the second pass's experiment scope (one record per
            // distinct entry) and the library's targets.
            string experimentPath = ExperimentSidecarPath(config);
            var experiment = FdrExperimentSidecar.ReadMap(experimentPath, FdrScoresSidecar.Pass.SecondPass);
            if (experiment == null)
            {
                ctx.LogError(string.Format(
                    @"--training-export needs the finished SecondPassFDR task, but its intermediate file '{0}' is " +
                    @"missing or unreadable. Run (or finish) SecondPassFDR first.", experimentPath));
                ctx.ExitCode = 1;
                return false;
            }
            var library = ResolveTargets(ctx);
            if (library == null)
                return false;

            int nFiles = config.InputFiles.Count;
            for (int i = 0; i < nFiles; i++)
            {
                string input = config.InputFiles[i];
                string output = TrainingExportParquet.PathFor(input);
                string stem = Path.GetFileNameWithoutExtension(input);
                string runKey = OutputValidityKey(ctx, key, output);
                if (PerFileResumeDriver.IsCurrent(output, Name, runKey))
                {
                    ctx.LogInfo(string.Format(@"Training export {0}/{1}: {2} (current, skipped)", i + 1, nFiles, stem));
                    continue;
                }
                PerFileResumeDriver.ClearStale(output, Name);
                ctx.LogInfo(string.Format(@"Training export {0}/{1}: {2}", i + 1, nFiles, stem));
                try
                {
                    ExportRun(input, output, library, experiment, ctx);
                }
                catch (Exception ex) when (!(ex is OutOfMemoryException))
                {
                    ctx.LogError(string.Format(@"Training export failed for {0}: {1}", input, ex));
                    ctx.ExitCode = 1;
                    return false;
                }
                // Stamped per run as it lands, so a kill loses only the run in flight; the
                // driver restamps every output with the same key when the task ends.
                PerFileResumeDriver.Stamp(output, Name, OspreyVersion.Current, runKey, RunInputs(input), ctx.LogWarning);
            }
            return ctx.ExitCode == 0;
        }

        /// <summary>
        /// One run: read its reconciled rows, second-pass q-values, calibration and spectra;
        /// compute every exported precursor's evidence one isolation window at a time; write
        /// the parquet; report the median-polish parity.
        /// </summary>
        private static void ExportRun(string input, string output,
            IReadOnlyDictionary<uint, LibraryEntry> library,
            IReadOnlyDictionary<uint, FdrExperimentRecord> experiment, PipelineContext ctx)
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
                    @"The reconciled scores '{0}' are missing; the training export reads the final boundaries from them.",
                    reconciledPath), reconciledPath);
            }
            // The footer check every other post-Stage-4 consumer makes - this build's version,
            // this search and library, and a reconciled (post-Stage-6) file - on every route,
            // since a --task TrainingExport node has nothing else vouching for the file.
            string footerError = ParquetScoreCache.ValidateScoresParquetGroup(new[] { reconciledPath }, config,
                OspreyVersion.Current, RECONCILED_CONSUMER);
            if (footerError != null)
                throw new InvalidDataException(footerError);
            var rows = ParquetScoreCache.LoadTrainingExportRows(reconciledPath);
            var pass2 = ReadPass2(input);
            ScoringTaskShared.LoadMassCalibrations(input, CONSUMER, out MzCalibrationResult ms2Cal, out _, out _);
            MzCalibration.CalibratedTolerance(ms2Cal, config.FragmentTolerance.Tolerance, config.FragmentTolerance.Unit,
                out double tolerance, out ToleranceUnit toleranceUnit);
            var searchConfig = config.ShallowClone();
            searchConfig.FragmentTolerance = new FragmentToleranceConfig { Tolerance = tolerance, Unit = toleranceUnit };
            ScoringPipeline.DoubleCountingTolerance(ms2Cal, config, out double ddcTolerance, out ToleranceUnit ddcUnit);
            var index = ScoringTaskShared.LoadSpectraForRescore(input, stem, CONSUMER, false);
            double rtNeighborhood = ScoringPipeline.DoubleCountingRtNeighborhood(index.AllMs2Rts);
            string runInfoPath = RunInfoFile.PathFor(input);
            var runInfo = RunInfoFile.TryLoad(runInfoPath);
            bool staleRunInfo = runInfo != null && !RunInfoFile.DescribesSpectraCache(runInfo, SpectraCache.GetCachePath(input));
            if (staleRunInfo)
            {
                ctx.LogWarning(string.Format(
                    @"[TRAIN-EXPORT] {0}: run info '{1}' describes a different version of the source than the spectra " +
                    @"cache does (size or mtime), so it is ignored and the instrument and collision-energy footer keys " +
                    @"are empty. Rebuilding the spectra cache rewrites it.", stem, runInfoPath));
                runInfo = null;
            }
            // The scan window is set per isolation window below: the run info records each
            // window's own range, which is narrower than the run's union on a method whose MS2
            // scan range follows the isolation window.
            var evidenceSettings = new TrainingEvidenceSettings
            {
                SearchConfig = searchConfig,
                WriteXics = settings.WriteXics,
            };

            // This run's targets that the library still describes; decoys are never exported.
            var targets = PairTargets(reconciledPath, rows, library, FdrScoresSidecar.Pass2Path(input), pass2,
                out int nNoLibrary);
            int nToExport = targets.Count(t => t.RunQ <= maxQ);

            // Evidence, one isolation window at a time: every target is placed in the window
            // whose spectra hold its apex scan - the window it was scored in.
            var windows = index.IsolationWindows;
            var perWindow = new List<TrainingRecord>[windows.Count];
            var provider = new StreamingWindowSpectraProvider(index, ms2Cal);
            Parallel.For(0, windows.Count, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, config.NThreads) },
                w => perWindow[w] = ExportWindow(windows[w], provider, targets, maxQ, claimantQ,
                    evidenceSettings.WithMs2ScanWindow(runInfo?.Ms2ScanWindowFor(windows[w].Center)),
                    rtNeighborhood, ddcTolerance, ddcUnit));

            var records = perWindow.Where(list => list != null).SelectMany(list => list)
                .OrderBy(r => r.EntryId).ToList();
            foreach (var record in records)
                Complete(record, library[record.EntryId], stem, experiment);
            int nParity = records.Count(r => r.MpCosineParity);
            int nEntrapment = records.Count(r => r.IsEntrapment);
            int nUnplaced = nToExport - records.Count;

            var metadata = BuildMetadata(config, stem, index, runInfo, ms2Cal, tolerance, toleranceUnit,
                ddcTolerance, ddcUnit, rtNeighborhood, maxQ, claimantQ, nParity, records.Count);
            TrainingExportParquet.Write(output, records, metadata, settings.WriteXics);
            sw.Stop();

            ctx.LogInfo(string.Format(
                @"[TRAIN-EXPORT] {0}: {1:N0} target precursors at run q <= {2} ({3:N0} entrapment) of {4:N0} reconciled targets in {5:F1}s",
                stem, records.Count, maxQ.ToString(@"R", CultureInfo.InvariantCulture), nEntrapment, targets.Count,
                sw.Elapsed.TotalSeconds));
            ctx.LogInfo(string.Format(@"[TRAIN-EXPORT] mp_cosine parity: {0}/{1}", nParity, records.Count));
            if (nParity != records.Count)
            {
                ctx.LogWarning(string.Format(
                    @"[TRAIN-EXPORT] {0}: {1:N0} of {2:N0} exported precursors did not reproduce the scored median_polish_cosine.",
                    stem, records.Count - nParity, records.Count));
            }
            if (nUnplaced > 0)
            {
                ctx.LogWarning(string.Format(
                    @"[TRAIN-EXPORT] {0}: {1:N0} precursors had no isolation window holding their apex scan and were not exported.",
                    stem, nUnplaced));
            }
            if (nNoLibrary > 0)
            {
                ctx.LogWarning(string.Format(
                    @"[TRAIN-EXPORT] {0}: {1:N0} reconciled targets have no library spectrum and were skipped.",
                    stem, nNoLibrary));
            }
            if (runInfo == null && !staleRunInfo)
            {
                ctx.LogWarning(string.Format(
                    @"[TRAIN-EXPORT] {0}: no run info at '{1}' (written when the spectra cache is built); " +
                    @"the instrument and collision-energy footer keys are empty.", stem, runInfoPath));
            }
        }

        /// <summary>
        /// One isolation window: load its calibrated spectra once, place the targets whose
        /// apex scan it holds, and compute the evidence of those to export against the ones
        /// that claim peaks here.
        /// </summary>
        private static List<TrainingRecord> ExportWindow(IsolationWindow window,
            StreamingWindowSpectraProvider provider, List<Target> targets, double maxQ, double claimantQ,
            TrainingEvidenceSettings settings, double rtNeighborhood, double ddcTolerance, ToleranceUnit ddcUnit)
        {
            var result = new List<TrainingRecord>();
            int windowKey = (int)Math.Round(window.Center * 10.0);
            var evidenceWindow = new TrainingEvidenceWindow(provider.GetCalibratedWindow(windowKey));
            var members = targets.Where(t => window.Contains(t.Entry.PrecursorMz) &&
                                             evidenceWindow.TryGetScanIndex(t.Row.ScanNumber, out _)).ToList();
            if (members.Count == 0)
                return result;

            var tolerance = settings.SearchConfig.FragmentTolerance;
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
                    evidenceWindow, claimants, settings);
                record.RunPeptideQ = target.RunPeptideQ;
                record.DdcNeighborCount = TrainingEvidence.CountDoubleCountingNeighbors(target.Entry,
                    target.Row.ApexRt, neighbors, rtNeighborhood, ddcTolerance, ddcUnit);
                result.Add(record);
            }
            return result;
        }

        /// <summary>The run-independent fields: file, entrapment class and the experiment scope.</summary>
        private static void Complete(TrainingRecord record, LibraryEntry entry, string stem,
            IReadOnlyDictionary<uint, FdrExperimentRecord> experiment)
        {
            record.FileName = stem;
            var kind = EntrapmentLibraryClassifier.Classify(entry.ProteinIds);
            record.IsEntrapment = kind == PeptideKind.PTarget || kind == PeptideKind.PDecoy;
            record.PeptideKind = PeptideKindName(kind);
            if (experiment.TryGetValue(record.EntryId, out var exp))
            {
                record.ExperimentPrecursorQ = exp.ExperimentPrecursorQvalue;
                record.ExperimentPeptideQ = exp.ExperimentPeptideQvalue;
                record.ExperimentProteinQ = exp.ExperimentProteinQvalue;
                record.Pep = exp.Pep;
            }
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
            SpectraWindowIndex index, RunInfo runInfo, MzCalibrationResult ms2Cal, double tolerance,
            ToleranceUnit toleranceUnit, double ddcTolerance, ToleranceUnit ddcUnit, double rtNeighborhood,
            double maxQ, double claimantQ, int nParity, int nRows)
        {
            var ic = CultureInfo.InvariantCulture;
            string R(double v) => v.ToString(@"R", ic);
            string Unit(ToleranceUnit u) => u == ToleranceUnit.Ppm ? @"ppm" : @"Th";
            var rts = index.AllMs2Rts;
            var metadata = new Dictionary<string, string>
            {
                [@"osprey.version"] = OspreyVersion.Current,
                [@"osprey.search_hash"] = config.Identity.SearchParameterHash(),
                [@"osprey.library_hash"] = config.Identity.LibraryIdentityHash(),
                [@"osprey.file_name"] = stem,
                [@"osprey.training_export.rows"] = nRows.ToString(ic),
                [@"osprey.training_export.max_q"] = R(maxQ),
                [@"osprey.training_export.claimant_q"] = R(claimantQ),
                [@"osprey.training_export.xics"] = config.TrainingExport.WriteXics ? @"true" : @"false",
                [@"osprey.training_export.slot_order"] = @"slot = p*4 + t; t: 0 b z1, 1 b z2, 2 y z1, 3 y z2; position p holds b(p+1) and y(L-1-p)",
                [@"osprey.training_export.mp_cosine_parity"] = string.Format(ic, @"{0}/{1}", nParity, nRows),
                [@"osprey.rt_min"] = rts.Count > 0 ? R(rts.Min()) : string.Empty,
                [@"osprey.rt_max"] = rts.Count > 0 ? R(rts.Max()) : string.Empty,
                [@"osprey.isolation_mz_min"] = index.IsolationWindows.Count > 0 ? R(index.IsolationWindows.Min(w => w.LowerBound)) : string.Empty,
                [@"osprey.isolation_mz_max"] = index.IsolationWindows.Count > 0 ? R(index.IsolationWindows.Max(w => w.UpperBound)) : string.Empty,
                [@"osprey.fragment_tolerance"] = R(tolerance),
                [@"osprey.fragment_tolerance_unit"] = Unit(toleranceUnit),
                [@"osprey.ms2_calibration.calibrated"] = ms2Cal.Calibrated ? @"true" : @"false",
                [@"osprey.ms2_calibration.mean"] = R(ms2Cal.Mean),
                [@"osprey.ms2_calibration.sd"] = R(ms2Cal.SD),
                [@"osprey.ms2_calibration.unit"] = ms2Cal.Unit ?? string.Empty,
                [@"osprey.ddc.tolerance"] = R(ddcTolerance),
                [@"osprey.ddc.tolerance_unit"] = Unit(ddcUnit),
                [@"osprey.ddc.rt_neighborhood"] = R(rtNeighborhood),
                [@"osprey.run_info"] = runInfo != null ? JsonConvert.SerializeObject(runInfo, Formatting.None) : string.Empty,
                [@"osprey.instrument_vendor"] = runInfo?.InstrumentVendor ?? string.Empty,
                [@"osprey.instrument_model"] = runInfo?.InstrumentModel ?? string.Empty,
                [@"osprey.ms2_scan_window"] = runInfo?.Ms2ScanWindow != null
                    ? string.Format(ic, @"{0},{1}", R(runInfo.Ms2ScanWindow[0]), R(runInfo.Ms2ScanWindow[1]))
                    : string.Empty,
                [@"osprey.dissociation_methods"] = runInfo != null ? JsonConvert.SerializeObject(runInfo.DissociationMethods) : string.Empty,
                [@"osprey.collision_energies"] = runInfo != null ? JsonConvert.SerializeObject(runInfo.CollisionEnergies) : string.Empty,
            };
            return metadata;
        }

        /// <summary>
        /// Each reconciled target row with the library entry it names and its own second-pass
        /// record. <paramref name="nNoLibrary"/> counts the target rows whose library spectrum
        /// is gone, which are not returned.
        ///
        /// <para>A record is paired by (entry_id, apex RT), not entry_id alone: Stage 6 gap-fill
        /// can leave two reconciled rows of one entry_id in a run (one target scored in two
        /// overlapping isolation windows), and each must take the record of its own peak.
        /// Every writer of the sidecar records the row's own apex RT - the per-file worker one
        /// record per reconciled row in the parquet's order (checked when written), the
        /// Stage 7 writers one per pool entry in the pool's order, which need not be the
        /// parquet's - so the pair is exact for all of them, where position is exact only for
        /// the first. Two rows of one entry_id come from different windows, so different scans
        /// and apex RTs; a pair the key cannot separate is refused, never overwritten.</para>
        ///
        /// <para>A row whose modified sequence or charge is not its library entry's is refused
        /// too: the ids came from another library or another build, and exporting the pair
        /// would put one precursor's ladder on another's peak.</para>
        /// </summary>
        internal static List<Target> PairTargets(string reconciledPath, IReadOnlyList<FdrEntry> rows,
            IReadOnlyDictionary<uint, LibraryEntry> library, string pass2Path,
            IReadOnlyList<FdrScoreRecord> pass2Records, out int nNoLibrary)
        {
            var pass2 = new Dictionary<(uint EntryId, long ApexRtBits), FdrScoreRecord>(pass2Records.Count);
            foreach (var r in pass2Records)
            {
                if (!pass2.TryAdd(ObservationKey(r.EntryId, r.ApexRt), r))
                {
                    throw new InvalidDataException(string.Format(
                        @"The SecondPassFDR intermediate file '{0}' holds two records for entry_id {1} at apex RT {2}; " +
                        @"the training export cannot tell which one is that observation's.",
                        pass2Path, r.EntryId, r.ApexRt.ToString(@"R", CultureInfo.InvariantCulture)));
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
                        @"The reconciled scores '{0}' name entry_id {1} as {2} {3}+, but the library's entry {1} is {4} {5}+. " +
                        @"The file was scored against another library or by a build that numbered it differently; " +
                        @"re-run the pipeline against this library before exporting.",
                        reconciledPath, row.EntryId, row.ModifiedSequence, row.Charge, entry.ModifiedSequence, entry.Charge));
                }
                if (entry.IsSpectrumReleased)
                {
                    nNoLibrary++;
                    continue;
                }
                var key = ObservationKey(row.EntryId, row.ApexRt);
                FdrScoreRecord? record = null;
                if (pass2.TryGetValue(key, out var found))
                {
                    if (!paired.Add(key))
                    {
                        throw new InvalidDataException(string.Format(
                            @"The reconciled scores '{0}' hold two rows for entry_id {1} at apex RT {2}; the training " +
                            @"export cannot tell which one the second-pass record in '{3}' belongs to.",
                            reconciledPath, row.EntryId, row.ApexRt.ToString(@"R", CultureInfo.InvariantCulture), pass2Path));
                    }
                    record = found;
                }
                targets.Add(new Target(row, entry, record));
            }
            return targets;
        }

        /// <summary>One observation's identity: its entry_id and the exact bits of its apex RT.</summary>
        private static (uint EntryId, long ApexRtBits) ObservationKey(uint entryId, double apexRt)
        {
            return (entryId, BitConverter.DoubleToInt64Bits(apexRt));
        }

        /// <summary>This run's second-pass records in file order; a missing or unreadable sidecar fails the run.</summary>
        private static List<FdrScoreRecord> ReadPass2(string input)
        {
            string path = FdrScoresSidecar.Pass2Path(input);
            var records = new List<FdrScoreRecord>();
            if (!FdrScoresSidecar.ReadRecords(path, FdrScoresSidecar.Pass.SecondPass, records.Add))
            {
                throw new InvalidDataException(string.Format(
                    @"The SecondPassFDR intermediate file '{0}' is missing or unreadable.", path));
            }
            return records;
        }

        /// <summary>
        /// The library by entry id. The in-process map when an earlier stage of this run still
        /// holds it - the same artifact, already loaded - and a load from the library (its
        /// <c>.libcache</c>) otherwise, which is every <c>--task TrainingExport</c> node and
        /// every pay-later run.
        ///
        /// <para>The load retains fragments only for the base_ids the first pass kept (the
        /// retained summary), as <c>--task SecondPassFDR</c> loads it: every row the export
        /// reads is a reconciled survivor, so no row it exports or compares against loses its
        /// spectrum, and the rest of the library's peaks are never allocated. Without a
        /// readable summary it loads everything. A load indexes the targets alone, since decoys
        /// are never exported; the in-process map is used as it stands, not copied.</para>
        /// </summary>
        private static IReadOnlyDictionary<uint, LibraryEntry> ResolveTargets(PipelineContext ctx)
        {
            if (ctx.TryGet(out LibraryById loaded))
                return loaded.Value;
            var options = new LibraryLoadOptions
            {
                RetainFragmentsFor = ScoringTaskShared.ReadRetainedBaseIds(ctx.Config, out _),
            };
            var library = LibraryLoader.Load(ctx.Config, options, ctx.LogInfo, ctx.LogWarning, out string error);
            if (error != null || library == null || library.Count == 0)
            {
                ctx.LogError(error ?? @"Library is empty after loading");
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

        private static string ExperimentSidecarPath(OspreyConfig config)
        {
            return FdrExperimentSidecar.PathFor(config.OutputBlib, ScoringTaskShared.ArtifactSiblingPath(config),
                FdrScoresSidecar.Pass.SecondPass);
        }

        /// <summary>The input whose export <paramref name="output"/> is, or null for no input's.</summary>
        private static string InputFor(OspreyConfig config, string output)
        {
            if (config.InputFiles == null)
                return null;
            foreach (string input in config.InputFiles)
            {
                if (string.Equals(TrainingExportParquet.PathFor(input), output, StringComparison.Ordinal))
                    return input;
            }
            return null;
        }

        /// <summary>
        /// The identities of the per-run artifacts one run's export reads that the task key
        /// does not already follow (see <see cref="OutputValidityKey"/>).
        /// </summary>
        private static string RunInputIdentities(string input)
        {
            return @";recon=" + IdentityOrAbsent(ParquetScoreCache.GetReconciledScoresPath(input))
                + @";pass2run=" + IdentityOrAbsent(FdrScoresSidecar.Pass2Path(input))
                + @";calib=" + IdentityOrAbsent(CalibrationIO.CalibrationPathForInput(input, ArtifactPaths.ResolveOutputDir(input)))
                + @";spectra=" + IdentityOrAbsent(SpectraCache.GetCachePath(input))
                + @";runinfo=" + IdentityOrAbsent(RunInfoFile.PathFor(input));
        }

        private static string IdentityOrAbsent(string path)
        {
            return File.Exists(path) ? SearchIdentity.FileIdentityTerm(path) : @"absent";
        }

        /// <summary>The artifacts one run's export reads.</summary>
        private static IEnumerable<string> RunInputs(string input)
        {
            yield return ParquetScoreCache.GetReconciledScoresPath(input);
            yield return FdrScoresSidecar.Pass2Path(input);
            yield return CalibrationIO.CalibrationPathForInput(input, ArtifactPaths.ResolveOutputDir(input));
            yield return SpectraCache.GetCachePath(input);
            yield return RunInfoFile.PathFor(input);
        }

        /// <summary>
        /// A reconciled target row with its library entry and the three second-pass values the
        /// export reads from its record. The row is the export projection's
        /// (<see cref="ParquetScoreCache.LoadTrainingExportRows"/>): scalars, PIN features and
        /// the reference XIC, with no CWT candidates or fragment arrays, so it holds nothing the
        /// evidence does not read.
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

            /// <summary>Second-pass run precursor q; NaN when the run has no record, which no threshold admits.</summary>
            public double RunQ { get; }
            public double RunPeptideQ { get; }
            public double Score { get; }
        }
    }
}
