/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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
using pwiz.Osprey.Core;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// Owns protein-FDR orchestration shared by the Tasks layer: the first-pass
    /// run (pre-Stage-6, on the full pre-compaction pool) and the second-pass /
    /// run-wide run (merge node, post Stage-6). Consolidates the glue that was
    /// previously duplicated across three tasks (<c>FirstJoinTask</c>,
    /// <c>MergeNodeTask</c>, <c>PerFileRescoreTask</c>) so FDR orchestration
    /// physically lives in the FDR project; the tasks call this through a thin
    /// facade, passing <c>ctx.LogInfo</c> as the log sink.
    ///
    /// Pure: takes data + a log delegate, never the pipeline context. It does NOT
    /// take <c>IOspreyDiagnostics</c> and does NOT call
    /// <c>OspreyDiagnosticsLog.ExitAfterDump</c> -- the Diagnostics project
    /// references this FDR project (a back-edge), so an FDR -> Diagnostics
    /// dependency would be a project-reference cycle. Instead each method RETURNS
    /// the parsimony / FDR artifacts; the Tasks facade owns the diagnostic dump
    /// and the early-exit decision. Mirrors the PercolatorEngine pattern.
    /// </summary>
    public static class ProteinFdrEngine
    {
        /// <summary>
        /// First-pass protein FDR (pre-Stage-6, full pre-compaction pool): builds
        /// parsimony from peptides passing peptide-level run FDR, runs picked-protein
        /// FDR at <see cref="OspreyConfig.RunFdr"/>, and writes
        /// <see cref="FdrEntry.RunProteinQvalue"/> on every stub. The pure computation
        /// lives in <c>ProteinFdr.RunFirstPassProteinFdr</c>; this adds the
        /// summary logging and returns the artifacts so the Tasks facade can emit the
        /// Stage-6 diagnostic dump + <c>ProteinFdrOnly</c> early-exit WITHOUT
        /// recomputing them. <paramref name="logInfo"/> may be null for the
        /// <c>--task SecondPassFDR</c> rehydration path (<c>PerFileRescoreTask</c>), which
        /// runs the recompute silently before compaction.
        /// </summary>
        public static FirstPassProteinFdrResult RunFirstPass(
            IList<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IList<LibraryEntry> fullLibrary,
            OspreyConfig config,
            Action<string> logInfo)
        {
            var result = ProteinFdr.RunFirstPassProteinFdr(
                perFileEntries, fullLibrary, config);
            LogFirstPassSummary(result, config, logInfo);
            return result;
        }

        /// <summary>
        /// Emit the two first-pass protein-FDR summary lines (detected-peptide count +
        /// target groups passing run FDR) shared by the resident <see cref="FdrEntry"/>
        /// facade above and the projection path's streaming reducer
        /// (<c>FirstJoinTask.RunFirstPassProteinFdrStreaming</c>, which assembles the same
        /// <see cref="FirstPassProteinFdrResult"/> off the sidecar + parquet scalars rather
        /// than the resident buffer). <paramref name="logInfo"/> may be null (silent runs).
        /// </summary>
        public static void LogFirstPassSummary(
            FirstPassProteinFdrResult result, OspreyConfig config, Action<string> logInfo)
        {
            if (logInfo == null)
                return;

            logInfo(string.Format(
                "[COUNT] First-pass detected peptides for protein FDR: {0} unique",
                result.DetectedPeptides.Count));

            int nAtRunFdr = 0;
            foreach (var qv in result.ProteinFdr.GroupQvalues.Values)
            {
                if (qv <= config.RunFdr)
                    nAtRunFdr++;
            }
            logInfo(string.Format(
                "First-pass protein FDR: {0} target groups at {1:P1} FDR",
                nAtRunFdr, config.RunFdr));
        }

        /// <summary>
        /// Second-pass / run-wide protein FDR (merge node, post Stage-6): collects
        /// best peptide scores, gates the detected-peptide set on experiment-level
        /// q-value, builds parsimony, runs picked-protein FDR at
        /// <see cref="OspreyConfig.RunFdr"/>, and propagates both
        /// <see cref="FdrEntry.RunProteinQvalue"/> and
        /// <see cref="FdrEntry.ExperimentProteinQvalue"/> onto every stub. Logs
        /// summary counts via <paramref name="logInfo"/> (which may be null for a
        /// silent run, like <c>RunFirstPass</c>) and returns the parsimony /
        /// FDR artifacts so the Tasks facade can emit the Stage-7 detected-peptides
        /// and protein-FDR diagnostic dumps + the <c>Stage7ProteinFdrOnly</c>
        /// early-exit WITHOUT recomputing them. Moved here from
        /// <c>MergeNodeTask.RunProteinFdr</c> (the dump / early-exit blocks stay in
        /// the Tasks facade -- see the type remarks for why).
        /// </summary>
        public static SecondPassProteinFdrResult RunSecondPass(
            IList<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IList<LibraryEntry> fullLibrary,
            OspreyConfig config,
            Action<string> logInfo)
        {
            // Collect best peptide scores
            var bestScores = ProteinFdr.CollectBestPeptideScores(perFileEntries);
            logInfo?.Invoke(string.Format("Collected scores for {0} unique peptides", bestScores.Count));

            // Get detected peptide set: targets passing experiment-level
            // q-value at the configured fdr_level (matches Rust pipeline.rs
            // second-pass parsimony input which filters on
            // `effective_experiment_qvalue(peptide_gate_level) <= experiment_fdr`
            // where peptide_gate_level = config.fdr_level (Peptide if config
            // is Protein, otherwise the config value). The Rust default
            // `FdrLevel::Precursor` means a default run filters on precursor-
            // level experiment q-values, NOT peptide-level. Matching that
            // here prevents losing ~1500 peptides to an unintentionally
            // stricter Peptide-level gate.
            // Rust pipeline.rs:4510 maps `FdrLevel::Protein -> Peptide` and
            // passes other variants through. C#'s FdrLevel enum doesn't
            // include `Protein` (just Precursor/Peptide/Both), so the remap
            // is a no-op here -- pass config.FdrLevel through directly. The
            // important property is that the gate level matches Rust's
            // default `FdrLevel::Precursor`, NOT a hardcoded Peptide.
            var peptideGateLevel = config.FdrLevel;
            var detectedPeptides = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kvp in perFileEntries)
            {
                foreach (var entry in kvp.Value)
                {
                    if (!entry.IsDecoy &&
                        entry.EffectiveExperimentQvalue(peptideGateLevel) <= config.ExperimentFdr)
                    {
                        detectedPeptides.Add(entry.ModifiedSequence);
                    }
                }
            }

            logInfo?.Invoke(string.Format("Detected {0} unique peptides at {1:P1} experiment FDR ({2})",
                detectedPeptides.Count, config.ExperimentFdr, peptideGateLevel));
            logInfo?.Invoke(string.Format(
                "[COUNT] Detected peptides for protein FDR: {0} unique",
                detectedPeptides.Count));

            // Build protein parsimony
            var parsimony = ProteinFdr.BuildProteinParsimony(
                fullLibrary, config.SharedPeptides, detectedPeptides);

            logInfo?.Invoke(string.Format("Protein parsimony: {0} groups", parsimony.Groups.Count));
            logInfo?.Invoke(string.Format(
                "[COUNT] Protein parsimony groups: {0}", parsimony.Groups.Count));

            // Compute protein FDR. Gate is config.RunFdr (1x) per Savitski's
            // convention, matching Rust pipeline.rs:4389
            // (compute_protein_fdr at config.run_fdr). The previous 2x gate
            // was a divergence from Rust that has since been corrected.
            var proteinFdr = ProteinFdr.ComputeProteinFdr(parsimony, bestScores, config.RunFdr);

            // Count passing proteins
            int passingProteins = 0;
            foreach (var kvp in proteinFdr.GroupQvalues)
            {
                if (kvp.Value <= config.EffectiveProteinFdr)
                    passingProteins++;
            }

            logInfo?.Invoke(string.Format("{0} protein groups pass {1:P1} protein FDR",
                passingProteins, config.EffectiveProteinFdr));
            logInfo?.Invoke(string.Format(
                "[COUNT] Protein groups passing FDR: {0} at {1:P0}",
                passingProteins, config.EffectiveProteinFdr));

            // Propagate protein q-values to FdrEntry stubs. The Stage-7
            // diagnostic dumps + Stage7ProteinFdrOnly early-exit are owned by
            // the Tasks facade (they need IOspreyDiagnostics, which this FDR
            // project cannot reference); the facade fires them from the
            // returned artifacts. Propagation only mutates the stubs, which
            // the dumps do not read, so emitting the dump after propagation is
            // output-invariant -- and matches the first-pass ordering, where
            // RunFirstPassProteinFdr likewise propagates before the facade dump.
            ProteinFdr.PropagateProteinQvalues(perFileEntries, proteinFdr, true, true);

            return new SecondPassProteinFdrResult(detectedPeptides, parsimony, proteinFdr);
        }
    }
}
