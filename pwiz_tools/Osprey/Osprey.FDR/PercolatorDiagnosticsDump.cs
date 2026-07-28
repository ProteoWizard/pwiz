/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using System.Globalization;
using System.IO;
using pwiz.Osprey.Core;
using pwiz.Osprey.ML;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// Stage-5 (Percolator) diagnostic output, extracted from
    /// the original <c>PercolatorFdr</c> god class (issue #4468). Everything here is write-only reporting:
    /// each member either writes a cross-impl bisection dump to disk or emits
    /// console text. Nothing returns a value that feeds the pipeline, so none
    /// of it can move a score or a q-value.
    ///
    /// The dumps mirror their Rust counterparts in osprey-fdr/src/percolator.rs
    /// so the Compare-* bisection scripts can hash-join the two tools' output.
    /// Each is one-shot and gated behind a <see cref="PercolatorDiagnosticsConfig"/>
    /// flag, and several have an "-Only" companion flag that aborts the run right
    /// after the dump.
    /// </summary>
    internal static class PercolatorDiagnosticsDump
    {
        /// <summary>
        /// Cross-impl bisection dump of the feature standardizer state,
        /// taken right after FitTransform returns and before subsampling
        /// / fold assignment. Mirrors dump_stage5_standardizer in Rust.
        /// Writes cs_stage5_standardizer.tsv with one row per feature.
        /// Columns: feature_idx, feature_name, mean, std.
        /// </summary>
        internal static void WriteStandardizerDump(
            FeatureStandardizer standardizer,
            OspreyFeatureInfo[] featureInfos)
        {
            const string path = @"cs_stage5_standardizer.tsv";
            var inv = CultureInfo.InvariantCulture;
            var means = standardizer.Means;
            var stds = standardizer.Stds;

            using (var sw = new StreamWriter(path))
            {
                sw.NewLine = "\n";
                sw.WriteLine(@"feature_idx	feature_name	mean	std");
                for (int i = 0; i < means.Length; i++)
                {
                    string name = (featureInfos != null && i < featureInfos.Length)
                        ? featureInfos[i].Name
                        : @"unknown";
                    sw.Write(i.ToString(inv));
                    sw.Write('\t'); sw.Write(name);
                    sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(means[i]));
                    sw.Write('\t'); sw.WriteLine(Diagnostics.FormatF64Roundtrip(stds[i]));
                }
            }
            OspreyOutput.Out.WriteLine(@"Wrote Stage 5 standardizer dump: {0} ({1} features)", path, means.Length);
        }

        /// <summary>
        /// One-shot diagnostic dump of the raw per-entry feature vectors
        /// fed into FeatureStandardizer.FitTransform. Mirrors Rust
        /// dump_stage5_perc_input. Writes cs_stage5_perc_input.tsv with
        /// columns native_position, entry_id, is_decoy, &lt;features...&gt;
        /// sorted by (entry_id, native_position).
        /// </summary>
        internal static void WritePercInputDump(
            IList<PercolatorEntry> entries,
            OspreyFeatureInfo[] featureInfos)
        {
            const string path = @"cs_stage5_perc_input.tsv";
            var inv = CultureInfo.InvariantCulture;
            int nFeatures = entries.Count > 0 ? entries[0].Features.Length : 0;
            using (var sw = new StreamWriter(path))
            {
                sw.NewLine = "\n";
                sw.Write(@"native_position	entry_id	is_decoy");
                for (int i = 0; i < nFeatures; i++)
                {
                    string name = (featureInfos != null && i < featureInfos.Length)
                        ? featureInfos[i].Name
                        : @"unknown";
                    sw.Write('\t'); sw.Write(name);
                }
                sw.WriteLine();

                int n = entries.Count;
                int[] order = new int[n];
                for (int i = 0; i < n; i++) order[i] = i;
                Array.Sort(order, (a, b) => // Array.Sort OK: tie-break on native_position (the input index a/b) makes the comparator total
                {
                    int c = entries[a].EntryId.CompareTo(entries[b].EntryId);
                    return c != 0 ? c : a.CompareTo(b);
                });

                foreach (int idx in order)
                {
                    var e = entries[idx];
                    sw.Write(idx.ToString(inv));
                    sw.Write('\t'); sw.Write(e.EntryId.ToString(inv));
                    sw.Write('\t'); sw.Write(e.IsDecoy ? @"true" : @"false");
                    for (int i = 0; i < e.Features.Length; i++)
                    {
                        sw.Write('\t');
                        sw.Write(Diagnostics.FormatF64Roundtrip(e.Features[i]));
                    }
                    sw.WriteLine();
                }
            }
            OspreyOutput.Out.WriteLine(@"Wrote Stage 5 Percolator input dump: {0} ({1} rows)", path, entries.Count);
        }

        /// <summary>
        /// Cross-impl bisection dump of the training subsample + fold assignment
        /// state, written to cs_stage5_subsample.tsv. Mirrors the Rust dump
        /// in osprey-fdr/src/percolator.rs so Compare-Subsample.ps1 can
        /// hash-join on entry_id.
        ///
        /// Columns: entry_id, native_position, charge, modified_sequence,
        /// is_decoy, base_id, in_subsample, fold_id. native_position is
        /// the entry's index in the input list -- divergence here means
        /// the two tools populate their arrays in different order. Rows
        /// sorted by entry_id for stable human inspection; compare is
        /// sort-order-agnostic.
        /// </summary>
        internal static void WriteSubsampleDump(
            IList<PercolatorEntry> entries,
            int[] trainSubset,
            int[] foldAssignments)
        {
            const string path = @"cs_stage5_subsample.tsv";
            var inv = CultureInfo.InvariantCulture;
            int n = entries.Count;

            var inSub = new bool[n];
            var foldFor = new int[n];
            for (int i = 0; i < n; i++) foldFor[i] = -1;

            for (int subPos = 0; subPos < trainSubset.Length; subPos++)
            {
                int nativePos = trainSubset[subPos];
                inSub[nativePos] = true;
                foldFor[nativePos] = foldAssignments[subPos];
            }

            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            // EntryId is NOT unique in the 2nd-pass entries[] vector --
            // a single (base_id, charge) precursor observed across N
            // files contributes N entries with the same EntryId, and
            // post-reconciliation gap-fill can add yet more duplicates
            // at the same (EntryId, Charge, ScanNumber). Tie-break on
            // the input index a (native_position) so the dump order
            // is deterministic AND matches Rust's stable
            // sort_by_key(|&i| entries[i].entry_id), which preserves
            // native_position order at duplicate EntryIds.
            Array.Sort(order, (a, b) => // Array.Sort OK: tie-break on native_position (the input index a/b) makes the comparator total
            {
                int c = entries[a].EntryId.CompareTo(entries[b].EntryId);
                return c != 0 ? c : a.CompareTo(b);
            });

            using (var sw = new StreamWriter(path))
            {
                sw.NewLine = "\n";
                sw.WriteLine(@"entry_id	native_position	charge	modified_sequence	is_decoy	base_id	in_subsample	fold_id");
                foreach (int i in order)
                {
                    var e = entries[i];
                    uint baseId = e.EntryId & PercolatorEntry.BASE_ID_MASK;
                    sw.Write(e.EntryId.ToString(inv));
                    sw.Write('\t'); sw.Write(i.ToString(inv));
                    sw.Write('\t'); sw.Write(e.Charge.ToString(inv));
                    sw.Write('\t'); sw.Write(e.Peptide ?? string.Empty);
                    sw.Write('\t'); sw.Write(e.IsDecoy ? @"true" : @"false");
                    sw.Write('\t'); sw.Write(baseId.ToString(inv));
                    sw.Write('\t'); sw.Write(inSub[i] ? @"true" : @"false");
                    sw.Write('\t'); sw.WriteLine(foldFor[i].ToString(inv));
                }
            }
            OspreyOutput.Out.WriteLine(@"Wrote Stage 5 subsample dump: {0} ({1} rows)", path, n);
        }

        /// <summary>
        /// Cross-impl bisection dump of per-fold SVM weights, taken right
        /// after training converges and before Granholm cross-fold
        /// calibration. Mirrors dump_stage5_svm_weights in Rust. Writes
        /// cs_stage5_svm_weights.tsv with one row per (fold, weight) pair:
        /// 21 feature weights + 1 bias per fold.
        ///
        /// Columns: fold, weight_idx, feature_name, value, fold_iterations.
        /// Sorted by (fold, weight_idx) for stable inspection; compare is
        /// hash-joined.
        /// </summary>
        internal static void WriteSvmWeightsDump(
            LinearSvmClassifier[] foldModels,
            int[] foldIterations,
            OspreyFeatureInfo[] featureInfos)
        {
            const string path = @"cs_stage5_svm_weights.tsv";
            var inv = CultureInfo.InvariantCulture;

            using (var sw = new StreamWriter(path))
            {
                sw.NewLine = "\n";
                sw.WriteLine(@"fold	weight_idx	feature_name	value	fold_iterations");
                for (int fold = 0; fold < foldModels.Length; fold++)
                {
                    var model = foldModels[fold];
                    var weights = model.Weights;
                    int iters = fold < foldIterations.Length ? foldIterations[fold] : 0;
                    for (int wi = 0; wi < weights.Length; wi++)
                    {
                        string name = (featureInfos != null && wi < featureInfos.Length)
                            ? featureInfos[wi].Name
                            : @"unknown";
                        sw.Write(fold.ToString(inv));
                        sw.Write('\t'); sw.Write(wi.ToString(inv));
                        sw.Write('\t'); sw.Write(name);
                        sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(weights[wi]));
                        sw.Write('\t'); sw.WriteLine(iters.ToString(inv));
                    }
                    sw.Write(fold.ToString(inv));
                    sw.Write('\t'); sw.Write(weights.Length.ToString(inv));
                    sw.Write('\t'); sw.Write(@"bias");
                    sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(model.Bias));
                    sw.Write('\t'); sw.WriteLine(iters.ToString(inv));
                }
            }
            OspreyOutput.Out.WriteLine(@"Wrote Stage 5 SVM weights dump: {0} ({1} folds)", path, foldModels.Length);
        }

        /// <summary>
        /// Writes the feature-contribution table (<see cref="FeatureContributions.ToReportLines"/>)
        /// to <c>OspreyOutput.Out</c> after Stage 5 training -- one row per line so each
        /// keeps its own log timestamp prefix. Pure reporting; never moves q-values.
        /// Gated behind <c>--verbose</c> (<see cref="OspreyOutput.Verbose"/>): the table is
        /// a model sanity check for implementers, not default-console output (per issue
        /// #4364 -- the raw coefficients aren't comparable on magnitude alone and the L2
        /// SVM splits signal across correlated scores, so it should not be emphasized).
        /// </summary>
        internal static void EmitFeatureContributions(FeatureContributions contributions)
        {
            if (!OspreyOutput.Verbose)
                return;
            foreach (string line in contributions.ToReportLines())
                OspreyOutput.Out.WriteLine(line);
        }
    }
}
