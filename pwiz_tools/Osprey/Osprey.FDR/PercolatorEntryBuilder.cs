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
            int numFeatures,
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
                    // Use the 21-feature vector computed during coelution scoring.
                    // Skip entries without one (e.g. stubs loaded from a Parquet cache
                    // without features, or gap-fill entries never scored), matching Rust
                    // run_percolator_fdr_direct (pipeline.rs:6153-6162 `continue`).
                    // Fabricating a placeholder vector would perturb the standardizer
                    // statistics, the training pool, and the output row set. In normal
                    // operation every entry has features, so nWithoutFeatures stays 0.
                    if (fdrEntry.Features == null || fdrEntry.Features.Length != numFeatures)
                    {
                        nWithoutFeatures++;
                        continue;
                    }
                    double[] features = fdrEntry.Features;
                    nWithFeatures++;

                    if (fdrEntry.IsDecoy)
                        nInputDecoys++;
                    else nInputTargets++;

                    // PSM Id must uniquely identify each observation so the
                    // result -> FdrEntry write-back can score every row
                    // independently. EntryId alone is NOT unique within a
                    // file: a single base_id with multiple scan-time
                    // observations (different scan numbers, same charge,
                    // same modified_sequence) shares one EntryId. Using
                    // "{fileName}_{EntryId}" collided those rows in
                    // resultMap, leaving the last-inserted score
                    // overwriting every same-EntryId observation's
                    // FdrEntry.Score and producing 176-185 score
                    // divergences per file vs. Rust's 4-component psm_id.
                    // Mirrors osprey-fdr/src/percolator.rs:5978-5980.
                    percEntries.Add(new PercolatorEntry
                    {
                        Id = string.Format("{0}_{1}_{2}_{3}",
                            fileName, fdrEntry.ModifiedSequence,
                            fdrEntry.Charge, fdrEntry.ScanNumber),
                        FileName = fileName,
                        Peptide = fdrEntry.ModifiedSequence,
                        Charge = fdrEntry.Charge,
                        IsDecoy = fdrEntry.IsDecoy,
                        EntryId = fdrEntry.EntryId,
                        Features = features
                    });
                }
            }
            return percEntries;
        }
    }
}
