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
using System.Linq;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// The PIN features the FDR classifier may not use (OSPREY_FEATURE_MASK). A masked feature
    /// reads as 0 when the FDR stage loads features (<see cref="ParquetScoreCache.LoadPinFeaturesFromParquet"/>),
    /// so the standardizer gives it scale 1 and value 0 and the classifier cannot use it.
    ///
    /// The mask is applied at load, not at scoring, so the scores parquet always holds every
    /// feature. A mask arm then needs only the FDR stage: it can adopt another arm's per-file
    /// scores, and its validity key (<see cref="ValidityKeySuffix"/>) is carried only by the tasks
    /// from first-pass FDR onward. The training export reads every feature unmasked.
    /// </summary>
    public static class PinFeatureMask
    {
        /// <summary>
        /// The features masked when OSPREY_FEATURE_MASK is unset: the runner-up, context,
        /// per-fragment-rank and spectrum-across-peak families, still under evaluation. Masking
        /// them keeps the default search's results what they were before these features.
        /// </summary>
        public static readonly string[] DEFAULT_MASKED_FEATURES =
        {
            @"peak_coelution_margin", @"peak_rank_margin", @"peak_candidate_count",
            @"context_peptide_length", @"context_precursor_charge", @"context_precursor_mz",
            @"frag_rank1_abs_mass_error", @"frag_rank2_abs_mass_error", @"frag_rank3_abs_mass_error",
            @"frag_rank4_abs_mass_error", @"frag_rank5_abs_mass_error", @"frag_rank6_abs_mass_error",
            @"frag_rank1_intensity_share", @"frag_rank2_intensity_share", @"frag_rank3_intensity_share",
            @"frag_rank4_intensity_share", @"frag_rank5_intensity_share", @"frag_rank6_intensity_share",
            @"peak_spectrum_cosine_mean", @"peak_spectrum_cosine_sd",
        };

        // Lazy so an unknown name surfaces as its own exception at first use rather than as a
        // TypeInitializationException; Program checks it at startup with UnknownFeatureName.
        private static readonly Lazy<bool[]> _masked = new Lazy<bool[]>(() => CreateMask(MaskedNames));

        /// <summary>The masked feature names in effect: OSPREY_FEATURE_MASK, or the default set when it is unset.</summary>
        private static string[] MaskedNames => OspreyEnvironment.FeatureMask ?? DEFAULT_MASKED_FEATURES;

        /// <summary>True when the PIN feature at <paramref name="featureIndex"/> is masked.</summary>
        public static bool IsMasked(int featureIndex)
        {
            return _masked.Value[featureIndex];
        }

        /// <summary>
        /// The first name in OSPREY_FEATURE_MASK that is not a PIN feature, or null when every name
        /// is one. Checked at startup so a mistyped name costs seconds, not Stages 1-4.
        /// </summary>
        public static string UnknownFeatureName()
        {
            return MaskedNames.FirstOrDefault(name => Array.IndexOf(ParquetScoreCache.PIN_FEATURE_NAMES, name) < 0);
        }

        /// <summary>
        /// The validity-key term for every task whose output depends on the mask: first-pass FDR
        /// and everything after it. Emitted for every arm, the default included, because outputs
        /// written before the mask moved to load time carry no term and were computed under a mask
        /// nothing recorded. The names are sorted, so the order they were listed in does not matter.
        /// </summary>
        public static string ValidityKeySuffix()
        {
            string mask;
            if (OspreyEnvironment.FeatureMask == null)
                mask = @"default";
            else if (OspreyEnvironment.FeatureMask.Length == 0)
                mask = @"none";
            else
                mask = string.Join(@"+", OspreyEnvironment.FeatureMask.Distinct().OrderBy(name => name, StringComparer.Ordinal));
            return @";featmask=" + mask;
        }

        /// <summary>
        /// The runner-up features, which read their candidates differently on the boundary-override
        /// path since that path began comparing an imposed peak with the first pass's candidates
        /// (it used to report a lone candidate).
        /// </summary>
        public static readonly string[] RUNNER_UP_FEATURES =
        {
            @"peak_coelution_margin", @"peak_rank_margin", @"peak_candidate_count",
        };

        /// <summary>
        /// True when the classifier sees a runner-up feature. Only then does a peak rescored at
        /// override bounds need the first pass's candidates, which take a second CWT detection
        /// per peak (<c>ScoringContext.OverrideRunnerUpCandidates</c>); otherwise the detection
        /// is skipped and the masked features report a lone candidate.
        /// </summary>
        public static bool UsesRunnerUpFeatures()
        {
            return UsesRunnerUpFeatures(IsMasked);
        }

        /// <summary><see cref="UsesRunnerUpFeatures()"/> for an explicitly supplied mask.</summary>
        public static bool UsesRunnerUpFeatures(Func<int, bool> isMasked)
        {
            return RUNNER_UP_FEATURES.Any(name => !isMasked(Array.IndexOf(ParquetScoreCache.PIN_FEATURE_NAMES, name)));
        }

        /// <summary>
        /// The validity-key term for the tasks that rescore peaks at override bounds and use those
        /// scores (PerFileRescoring, SecondPassFDR): present exactly when
        /// <see cref="UsesRunnerUpFeatures()"/> is, because only then are the override-path
        /// candidates found. Under the default mask it is empty, so no default output directory
        /// is invalidated.
        /// </summary>
        public static string RescoreValidityKeySuffix()
        {
            return RescoreValidityKeySuffix(IsMasked);
        }

        /// <summary><see cref="RescoreValidityKeySuffix()"/> for an explicitly supplied mask.</summary>
        public static string RescoreValidityKeySuffix(Func<int, bool> isMasked)
        {
            return UsesRunnerUpFeatures(isMasked) ? @";runnerup=override-candidates" : string.Empty;
        }

        private static bool[] CreateMask(string[] maskedNames)
        {
            var masked = new bool[ParquetScoreCache.NUM_PIN_FEATURES];
            foreach (string name in maskedNames)
            {
                int index = Array.IndexOf(ParquetScoreCache.PIN_FEATURE_NAMES, name);
                if (index < 0)
                {
                    // Exception text for internal diagnostics; Program reports it to the user at startup
                    throw new InvalidOperationException(
                        string.Format(@"OSPREY_FEATURE_MASK names an unknown feature: {0}", name));
                }
                masked[index] = true;
            }
            return masked;
        }
    }
}
