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

using System.Collections.Generic;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// Builds the flat <see cref="PercolatorEntry"/> input list (one per
    /// observation across all files) that <see cref="PercolatorEngine"/>
    /// feeds to the SVM. Each entry prefers the 21-feature vector computed during
    /// coelution scoring and stored on the <see cref="FdrEntry"/>, falling back to
    /// a basic vector only for stubs that lack features (e.g. loaded from a parquet
    /// cache).
    ///
    /// Extracted verbatim from <c>RunPercolatorFdr</c> as pure code motion so the
    /// parity-locked scoring core is not itself decomposed. The PSM id, feature
    /// fallback, and entry ordering are unchanged. Mirrors Rust
    /// osprey-fdr/src/percolator.rs:5978-5980.
    /// </summary>
    internal static class PercolatorEntryBuilder
    {
        /// <summary>
        /// Construct the PercolatorEntry list in iteration order over
        /// <paramref name="perFileEntries"/>. The per-category counts are returned
        /// via out params for the caller's logging. Pure: no I/O, no context.
        /// <paramref name="numFeatures"/> is the expected PIN feature-vector length
        /// (the caller supplies it from the PIN feature-name list, keeping this
        /// builder free of the Scoring/IO feature-count constants).
        /// </summary>
        internal static List<PercolatorEntry> Build(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            int numFeatures, bool streamFeatures,
            out int nWithFeatures, out int nWithoutFeatures,
            out int nInputTargets, out int nInputDecoys)
        {
            // Build PercolatorEntry list from all files
            var percEntries = new List<PercolatorEntry>();

            nWithFeatures = 0;
            nWithoutFeatures = 0;
            nInputTargets = 0;
            nInputDecoys = 0;
            foreach (var kvp in perFileEntries)
            {
                string fileName = kvp.Key;
                foreach (var fdrEntry in kvp.Value)
                {
                    // Feature vector source. On the streaming path (issue #4355
                    // Phase 4) the vector is left null here and reloaded per file
                    // from parquet by ParquetIndex at score time, so the O(N)
                    // vectors are never all resident at once. Otherwise prefer the
                    // 21-feature vector computed during coelution scoring, falling
                    // back to a basic vector only for stub entries (e.g. loaded
                    // from a Parquet cache without features).
                    double[] features;
                    if (streamFeatures)
                    {
                        features = null;
                        // uint.MaxValue marks an appended entry with no original
                        // parquet row (its features will fall back to basic).
                        if (fdrEntry.ParquetIndex != uint.MaxValue)
                            nWithFeatures++;
                        else
                            nWithoutFeatures++;
                    }
                    else if (fdrEntry.Features != null &&
                        fdrEntry.Features.Length == numFeatures)
                    {
                        features = fdrEntry.Features;
                        nWithFeatures++;
                    }
                    else
                    {
                        features = BuildBasicFeatures(fdrEntry.CoelutionSum, numFeatures);
                        nWithoutFeatures++;
                    }

                    if (fdrEntry.IsDecoy)
                        nInputDecoys++;
                    else nInputTargets++;

                    // One PercolatorEntry per FdrEntry, emitted in this nested
                    // (file, entry) order. Both SVM paths return results
                    // index-aligned to this input, so the score / q-value
                    // write-back zips them back onto the stubs by position
                    // (PercolatorEngine.ApplyPercolatorResults) instead of
                    // re-joining through a per-row psm_id string + resultMap.
                    // The former "{file}_{modseq}_{charge}_{scan}" psm_id is
                    // therefore no longer built here (it allocated one string
                    // per observation). Mirrors the Rust direct path, which
                    // likewise zips by index (osprey-fdr/src/percolator.rs:5978-5980).
                    percEntries.Add(new PercolatorEntry
                    {
                        FileName = fileName,
                        Peptide = fdrEntry.ModifiedSequence,
                        Charge = fdrEntry.Charge,
                        IsDecoy = fdrEntry.IsDecoy,
                        EntryId = fdrEntry.EntryId,
                        ParquetIndex = fdrEntry.ParquetIndex,
                        CoelutionSum = fdrEntry.CoelutionSum,
                        Features = features
                    });
                }
            }
            return percEntries;
        }

        /// <summary>
        /// Projection-buffer counterpart of <see cref="Build"/> (issue #4355 step
        /// (b) increment ii): construct the same flat <see cref="PercolatorEntry"/>
        /// list from the thin <see cref="FdrProjection"/> peak buffer instead of
        /// the full <see cref="FdrEntry"/> stub buffer. The SVM never sees the
        /// projection -- it consumes <see cref="PercolatorEntry"/> exactly as
        /// before, so the training subsample, standardizer, and scoring are
        /// byte-identical. <c>Peptide</c> is materialized from
        /// <paramref name="peptideById"/> (the interned modified-sequence table),
        /// so the ordinal peptide-group sort in the subsample sees the identical
        /// strings; <c>FileName</c> is the per-file group key. Emits entries in the
        /// same nested (file, entry) order as <see cref="Build"/> so the index-zip
        /// write-back (<c>PercolatorEngine.ApplyPercolatorResultsToProjection</c>)
        /// pairs each result with its projection row.
        /// </summary>
        internal static List<PercolatorEntry> BuildFromProjection(
            List<KeyValuePair<string, List<FdrProjection>>> perFileProjections,
            string[] peptideById, int numFeatures, bool streamFeatures,
            out int nWithFeatures, out int nWithoutFeatures,
            out int nInputTargets, out int nInputDecoys)
        {
            var percEntries = new List<PercolatorEntry>();

            nWithFeatures = 0;
            nWithoutFeatures = 0;
            nInputTargets = 0;
            nInputDecoys = 0;
            foreach (var kvp in perFileProjections)
            {
                string fileName = kvp.Key;
                foreach (var proj in kvp.Value)
                {
                    // Feature-source bookkeeping identical to Build: on the
                    // streaming path the vector is null here and reloaded per file
                    // by ParquetIndex at score time. The projection carries no
                    // resident Features, so the non-streaming branch (2nd-pass /
                    // resident) never uses this overload -- streamFeatures is always
                    // true in practice, but keep the same counting shape so the
                    // [COUNT] input line matches.
                    double[] features;
                    if (streamFeatures)
                    {
                        features = null;
                        if (proj.ParquetIndex != uint.MaxValue)
                            nWithFeatures++;
                        else
                            nWithoutFeatures++;
                    }
                    else
                    {
                        features = BuildBasicFeatures(proj.CoelutionSum, numFeatures);
                        nWithoutFeatures++;
                    }

                    if (proj.IsDecoy)
                        nInputDecoys++;
                    else nInputTargets++;

                    percEntries.Add(new PercolatorEntry
                    {
                        FileName = fileName,
                        Peptide = peptideById[proj.PeptideId],
                        Charge = proj.Charge,
                        IsDecoy = proj.IsDecoy,
                        EntryId = proj.EntryId,
                        ParquetIndex = proj.ParquetIndex,
                        CoelutionSum = proj.CoelutionSum,
                        Features = features
                    });
                }
            }
            return percEntries;
        }

        /// <summary>
        /// Build a minimal PIN feature vector from an entry's coelution_sum.
        /// Used as a fallback ONLY when the full 21-feature vector is unavailable
        /// (e.g. stubs loaded from a Parquet cache without features, or a streaming
        /// entry whose parquet row cannot be resolved). In normal operation the
        /// 21-feature vector is computed during coelution scoring in
        /// <c>CoelutionScorer</c> and stored on the entry. Internal so the
        /// streaming score pass (<see cref="PercolatorFdr.ResolveFeatureRow"/>) can
        /// reuse the exact same fallback vector for byte-identical results.
        /// </summary>
        internal static double[] BuildBasicFeatures(double coelutionSum, int numFeatures)
        {
            double[] features = new double[numFeatures];

            // 0: coelution_sum
            features[0] = coelutionSum;
            // 1: coelution_max (approximate as coelution_sum for basic version)
            features[1] = coelutionSum * 0.5;
            // 2: n_coeluting_fragments
            features[2] = 3.0;
            // 3: peak_apex
            features[3] = 0.0;
            // 4: peak_area
            features[4] = 0.0;
            // 5: peak_sharpness
            features[5] = 0.0;
            // 6: xcorr
            features[6] = 0.0;
            // 7: consecutive_ions
            features[7] = 0.0;
            // 8: explained_intensity
            features[8] = 0.0;
            // 9: mass_accuracy_mean
            features[9] = 0.0;
            // 10: abs_mass_accuracy_mean
            features[10] = 0.0;
            // 11: rt_deviation
            features[11] = 0.0;
            // 12: abs_rt_deviation
            features[12] = 0.0;
            // 13: ms1_precursor_coelution
            features[13] = 0.0;
            // 14: ms1_isotope_cosine
            features[14] = 0.0;
            // 15: median_polish_cosine
            features[15] = 0.0;
            // 16: median_polish_residual_ratio
            features[16] = 0.0;
            // 17: sg_weighted_xcorr
            features[17] = 0.0;
            // 18: sg_weighted_cosine
            features[18] = 0.0;
            // 19: median_polish_min_fragment_r2
            features[19] = 0.0;
            // 20: median_polish_residual_correlation
            features[20] = 0.0;

            return features;
        }
    }
}
