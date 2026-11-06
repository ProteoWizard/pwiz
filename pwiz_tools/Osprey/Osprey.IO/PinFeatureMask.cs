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
using System.Linq;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// The PIN features the FDR classifier may not use (OSPREY_FEATURE_MASK). A masked feature
    /// is absent from the vectors the FDR stage loads (<see cref="ParquetScoreCache.LoadPinFeaturesFromParquet"/>):
    /// each holds only the <see cref="FdrFeatureNames"/>, in PIN order, so training costs what the
    /// unmasked features cost and not what all of them would. A masked feature used to be loaded as
    /// a column of zeros, which the classifier could not use but still paid for - 97 columns where
    /// 21 carried information.
    ///
    /// The mask also decides what scoring computes. Stage 4 computes and records the 21 core
    /// features and each feature family (<see cref="FAMILY_FIRST_FEATURES"/>) that has a feature
    /// the mask leaves unmasked (<see cref="IsRecorded"/>); the scores parquet holds a column for
    /// each recorded feature and none for the rest. Under the default mask that is the 21 core
    /// features alone, so a default search costs and writes what it did before the masked families
    /// were added. OSPREY_RECORD_ALL_FEATURES=1, a developer and research switch, records every
    /// feature, so arms that differ only in the mask can share one scoring and run only the FDR
    /// stage. The recorded set keys per-file scoring and rescoring
    /// (<see cref="RecordedFeaturesValidityKeySuffix()"/>), and a reader that needs a column the
    /// file does not hold refuses it rather than reading zeros.
    ///
    /// Some families can only use HRAM data (<see cref="HRAM_ONLY_FAMILIES"/>). At unit resolution
    /// they are masked and unrecorded whatever the switches say, so a run there has the same FDR
    /// vectors and scores columns as if the families did not exist.
    /// </summary>
    public static class PinFeatureMask
    {
        /// <summary>
        /// The features masked when OSPREY_FEATURE_MASK is unset: the runner-up, context,
        /// per-fragment-rank, spectrum-across-peak, median-polish top-k,
        /// reference-correlation and peak-profile families, still under evaluation. Masking
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
            @"mp_fragment_r2_rank2", @"mp_fragment_r2_rank3", @"mp_fragment_r2_rank4",
            @"mp_fragment_r2_top3_mean", @"mp_fragment_r2_median", @"mp_n_fragments_good_fit",
            @"ref_corr_rank2", @"ref_corr_rank3", @"ref_corr_rank5", @"ref_n_corr_good",
            @"ref_minfilt_corr_top3_mean", @"apex_log_intensity_excl_top",
            @"peak_width", @"shape_bin1", @"shape_bin2", @"shape_bin3", @"shape_bin4", @"shape_bin5",
            @"local_corr_top3_mean",
            @"lib_rel_rank2", @"lib_rel_rank3", @"lib_rel_rank4", @"lib_rel_rank5", @"lib_rel_rank6",
            @"window_share_rank1", @"window_share_rank2", @"window_share_rank3",
            @"window_share_rank4", @"window_share_rank5", @"window_share_rank6",
            @"tc_sum6", @"tc_corr_rank1", @"tc_corr_rank2", @"tc_corr_rank3", @"tc_corr_rank4",
            @"tc_corr_rank5", @"tc_corr_rank6", @"tc_res_sum", @"tc_res_mean",
            @"heavy_sum6", @"heavy_n_good", @"tight45_sum6", @"tight20_sum6", @"tc_best_sum6",
            @"dom_best_corr_delta", @"dom_tot_corr_sum", @"dom_apex_count", @"dom_corr_margin",
            @"top3_ref_n_corr_good", @"top3_mp_min_r2", @"top3_mp_r2_median", @"top3_coelution_min",
            @"wide_coelution_sum", @"wide_n_coeluting", @"wide_coelution_margin", @"wide_top3_coelution_min",
            @"shadow_corr_sum6", @"shadow_frac6", @"tc_clean_sum6", @"tc_clip_sum6",
            @"lib_cosine_coel", @"lib_entropy_sim", @"lib_coel_share",
            @"spec_match_prob", @"spec_proj_cosine_xt", @"spec_hyperscore", @"spec_match_prob_max",
            @"spec_proj_cosine_xt_max",
        };

        /// <summary>
        /// The first PIN feature of each feature family, in PIN order. A family runs from its first
        /// feature to the next family's, and the last to the end of the PIN features; the features
        /// before the first family are the 21 core features. Scoring computes or skips a family as
        /// a unit, because its features share one per-candidate computation (a byproduct of
        /// <c>OspreyScoringContext</c>): recording one feature of a family costs nearly what
        /// recording all of them does.
        /// </summary>
        public static readonly string[] FAMILY_FIRST_FEATURES =
        {
            @"peak_coelution_margin", @"context_peptide_length", @"frag_rank1_abs_mass_error",
            @"peak_spectrum_cosine_mean", @"mp_fragment_r2_rank2", @"ref_corr_rank2", @"peak_width",
            @"tc_sum6", @"heavy_sum6", @"dom_best_corr_delta", @"top3_ref_n_corr_good", @"wide_coelution_sum",
            @"shadow_corr_sum6", @"tc_clean_sum6", @"lib_cosine_coel", @"spec_match_prob",
        };

        /// <summary>
        /// The feature families (<see cref="FAMILY_FIRST_FEATURES"/>) only HRAM data can use. At unit
        /// resolution nearly every library fragment finds a peak in its 1 Th window, so counting
        /// matches says nothing: on Stellar the match probability alone finds 52-112 precursors at
        /// q 1%, where xcorr finds 12,812, and the families add nothing measurable to the classifier.
        /// They would still cost their computation, and as columns they would change which features
        /// the gradient-boosted trees sample.
        /// </summary>
        public static readonly string[] HRAM_ONLY_FAMILIES =
        {
            @"lib_cosine_coel", @"spec_match_prob",
        };

        /// <summary>
        /// The core features, PIN 0 to 20: the features Osprey had before the families were added.
        /// Always computed and recorded, whatever the mask, because more than the classifier reads
        /// them - the coelution sum is every row's first-pass score, and the median-polish
        /// diagnostics and the training export read core features directly.
        /// </summary>
        public static readonly int CORE_FEATURE_COUNT =
            Array.IndexOf(ParquetScoreCache.PIN_FEATURE_NAMES, FAMILY_FIRST_FEATURES[0]);

        // Lazy so an unknown name surfaces as its own exception at first use rather than as a
        // TypeInitializationException; Program checks it at startup with UnknownFeatureName.
        private static readonly Lazy<ResolvedFeatures> _hramFeatures =
            new Lazy<ResolvedFeatures>(() => new ResolvedFeatures(ResolutionMode.HRAM));
        private static readonly Lazy<ResolvedFeatures> _unitFeatures =
            new Lazy<ResolvedFeatures>(() => new ResolvedFeatures(ResolutionMode.UnitResolution));
        private static readonly Lazy<bool[]> _hramOnly = new Lazy<bool[]>(CreateHramOnlyFlags);

        private static ResolutionMode _resolutionMode = ResolutionMode.HRAM;

        /// <summary>The masked feature names in effect: OSPREY_FEATURE_MASK, or the default set when it is unset.</summary>
        private static string[] MaskedNames => OspreyEnvironment.FeatureMask ?? DEFAULT_MASKED_FEATURES;

        private static ResolvedFeatures Resolved =>
            _resolutionMode == ResolutionMode.UnitResolution ? _unitFeatures.Value : _hramFeatures.Value;

        /// <summary>
        /// Sets the resolution of this process's search, HRAM until then. The switches this class
        /// reads are per process, and so is the resolution they combine with: the host calls this once
        /// the resolution is known, before anything reads the mask.
        /// </summary>
        public static void ApplyResolutionMode(ResolutionMode mode)
        {
            _resolutionMode = mode;
        }

        /// <summary>
        /// True when data of the given <paramref name="mode"/> can use the PIN feature at
        /// <paramref name="featureIndex"/>: every feature but the <see cref="HRAM_ONLY_FAMILIES"/> at unit resolution.
        /// </summary>
        public static bool IsAvailable(int featureIndex, ResolutionMode mode)
        {
            return mode == ResolutionMode.HRAM || !_hramOnly.Value[featureIndex];
        }

        /// <summary>The mask <paramref name="isMasked"/> plus the features <paramref name="mode"/> cannot use.</summary>
        public static Func<int, bool> MaskAtResolution(Func<int, bool> isMasked, ResolutionMode mode)
        {
            return f => isMasked(f) || !IsAvailable(f, mode);
        }

        /// <summary>
        /// True when OSPREY_FEATURE_MASK leaves unmasked a feature this search's resolution cannot use.
        /// Program logs it, because the feature is then dropped however it was asked for.
        /// </summary>
        public static bool LeavesUnavailableFeaturesUnmasked()
        {
            bool[] named = CreateMask(MaskedNames);
            return Enumerable.Range(0, named.Length).Any(f => !named[f] && !IsAvailable(f, _resolutionMode));
        }

        /// <summary>True when the PIN feature at <paramref name="featureIndex"/> is masked.</summary>
        public static bool IsMasked(int featureIndex)
        {
            return Resolved.Masked[featureIndex];
        }

        /// <summary>
        /// The PIN index of each feature the FDR classifier sees, ascending: position i of an FDR
        /// feature vector holds PIN feature <c>FdrFeatureIndices[i]</c>. Under the default mask this
        /// is 0 to 20, the 21 features Osprey had before the masked ones were added, so the default
        /// classifier sees exactly the matrix it saw then.
        /// </summary>
        public static int[] FdrFeatureIndices => Resolved.FdrIndices;

        /// <summary>The PIN names of the FDR feature vector's positions, in its order.</summary>
        public static string[] FdrFeatureNames => SelectFdrFeatures(ParquetScoreCache.PIN_FEATURE_NAMES);

        /// <summary>
        /// The FDR classifier's view of a per-PIN-feature array (feature names, feature metadata, a
        /// full feature vector): its unmasked elements, in PIN order. Use it wherever something
        /// indexed by PIN feature has to line up with an FDR feature vector, so names and values
        /// cannot drift apart.
        /// </summary>
        public static T[] SelectFdrFeatures<T>(T[] perPinFeature)
        {
            return SelectFeatures(perPinFeature, FdrFeatureIndices);
        }

        /// <summary><see cref="SelectFdrFeatures{T}"/> for explicitly supplied feature indices.</summary>
        public static T[] SelectFeatures<T>(T[] perPinFeature, int[] featureIndices)
        {
            if (perPinFeature == null)
                throw new ArgumentNullException(nameof(perPinFeature));
            if (perPinFeature.Length != ParquetScoreCache.NUM_PIN_FEATURES)
            {
                // Exception text for internal diagnostics, not displayed to user
                throw new ArgumentException(string.Format(@"Expected {0} PIN features, not {1}",
                    ParquetScoreCache.NUM_PIN_FEATURES, perPinFeature.Length), nameof(perPinFeature));
            }
            var selected = new T[featureIndices.Length];
            for (int i = 0; i < featureIndices.Length; i++)
                selected[i] = perPinFeature[featureIndices[i]];
            return selected;
        }

        /// <summary>The ascending PIN indices <paramref name="isMasked"/> leaves unmasked.</summary>
        public static int[] UnmaskedIndices(Func<int, bool> isMasked)
        {
            return Enumerable.Range(0, ParquetScoreCache.NUM_PIN_FEATURES).Where(f => !isMasked(f)).ToArray();
        }

        /// <summary>
        /// True when Stage 4 computes the PIN feature at <paramref name="featureIndex"/> and the
        /// scores parquet holds its column: a core feature, a feature of a family the mask leaves
        /// a feature of, or any feature under OSPREY_RECORD_ALL_FEATURES=1. Every feature the FDR
        /// classifier reads is recorded. A feature the resolution cannot use never is
        /// (<see cref="IsAvailable"/>).
        /// </summary>
        public static bool IsRecorded(int featureIndex)
        {
            return Resolved.Recorded[featureIndex];
        }

        /// <summary>
        /// A copy of <see cref="IsRecorded"/> for every PIN feature, for the scorer, which cannot
        /// see this class.
        /// </summary>
        public static bool[] RecordedFeatureFlags()
        {
            return (bool[])Resolved.Recorded.Clone();
        }

        /// <summary>
        /// Which PIN features scoring records when <paramref name="isMasked"/> is the mask: the core
        /// features, and every feature of each family that has an unmasked feature - or every
        /// feature when <paramref name="recordAll"/>.
        /// </summary>
        public static bool[] RecordedFeatures(Func<int, bool> isMasked, bool recordAll)
        {
            var recorded = new bool[ParquetScoreCache.NUM_PIN_FEATURES];
            for (int f = 0; f < CORE_FEATURE_COUNT; f++)
                recorded[f] = true;
            foreach (var (start, end) in FamilyRanges())
            {
                bool record = recordAll || Enumerable.Range(start, end - start).Any(f => !isMasked(f));
                for (int f = start; f < end; f++)
                    recorded[f] = record;
            }
            return recorded;
        }

        /// <summary>
        /// <see cref="RecordedFeatures(Func{int,bool},bool)"/> at a resolution: nothing records a feature
        /// <paramref name="mode"/> cannot use, not even <paramref name="recordAll"/>.
        /// </summary>
        public static bool[] RecordedFeatures(Func<int, bool> isMasked, bool recordAll, ResolutionMode mode)
        {
            var recorded = RecordedFeatures(MaskAtResolution(isMasked, mode), recordAll);
            for (int f = 0; f < recorded.Length; f++)
                recorded[f] = recorded[f] && IsAvailable(f, mode);
            return recorded;
        }

        /// <summary>
        /// The validity-key term for the tasks whose outputs hold the recorded feature columns:
        /// per-file scoring, which writes the scores parquet, and per-file rescoring, which copies
        /// its rows into the reconciled parquet. It names the recorded families, or <c>all</c>, so
        /// a run that needs a family its scores lack rescores instead of adopting them, and two
        /// arms that record the same families - OSPREY_FEATURE_MASK=none and
        /// OSPREY_RECORD_ALL_FEATURES=1, say - share their scores. Empty when only the core
        /// features are recorded, the default, so no default output directory is invalidated.
        /// </summary>
        public static string RecordedFeaturesValidityKeySuffix()
        {
            return RecordedFeaturesValidityKeySuffix(IsRecorded, _resolutionMode);
        }

        /// <summary><see cref="RecordedFeaturesValidityKeySuffix()"/> for an explicitly supplied recorded set.</summary>
        public static string RecordedFeaturesValidityKeySuffix(Func<int, bool> isRecorded)
        {
            return RecordedFeaturesValidityKeySuffix(isRecorded, ResolutionMode.HRAM);
        }

        /// <summary>
        /// <see cref="RecordedFeaturesValidityKeySuffix()"/> for an explicitly supplied recorded set at a
        /// resolution, where <c>all</c> means every family <paramref name="mode"/> can use.
        /// </summary>
        public static string RecordedFeaturesValidityKeySuffix(Func<int, bool> isRecorded, ResolutionMode mode)
        {
            var available = FAMILY_FIRST_FEATURES
                .Where(name => IsAvailable(Array.IndexOf(ParquetScoreCache.PIN_FEATURE_NAMES, name), mode)).ToArray();
            var families = available
                .Where(name => isRecorded(Array.IndexOf(ParquetScoreCache.PIN_FEATURE_NAMES, name))).ToArray();
            if (families.Length == 0)
                return string.Empty;
            return @";featrec=" + (families.Length == available.Length ? @"all" : string.Join(@"+", families));
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
        ///
        /// <para>The <c>fdrcols</c> term records that a masked feature is absent from the FDR
        /// vectors rather than a column of zeros. The saved first-pass model and each run's
        /// first-pass feature sums are sized to those vectors, so one written under the zero-column
        /// layout must not be adopted: a frozen model 97 features wide would decline every 21-wide
        /// vector it was handed.</para>
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
            return @";featmask=" + mask + @";fdrcols=unmasked";
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

        private static bool[] CreateHramOnlyFlags()
        {
            var hramOnly = new bool[ParquetScoreCache.NUM_PIN_FEATURES];
            foreach (var (start, end) in FamilyRanges())
            {
                if (!HRAM_ONLY_FAMILIES.Contains(ParquetScoreCache.PIN_FEATURE_NAMES[start]))
                    continue;
                for (int f = start; f < end; f++)
                    hramOnly[f] = true;
            }
            return hramOnly;
        }

        /// <summary>Each family's PIN index range, [start, end), in PIN order.</summary>
        private static IEnumerable<(int Start, int End)> FamilyRanges()
        {
            for (int i = 0; i < FAMILY_FIRST_FEATURES.Length; i++)
            {
                int start = Array.IndexOf(ParquetScoreCache.PIN_FEATURE_NAMES, FAMILY_FIRST_FEATURES[i]);
                int end = i + 1 < FAMILY_FIRST_FEATURES.Length
                    ? Array.IndexOf(ParquetScoreCache.PIN_FEATURE_NAMES, FAMILY_FIRST_FEATURES[i + 1])
                    : ParquetScoreCache.NUM_PIN_FEATURES;
                yield return (start, end);
            }
        }

        /// <summary>The process's mask switches resolved for one resolution.</summary>
        private sealed class ResolvedFeatures
        {
            public ResolvedFeatures(ResolutionMode mode)
            {
                bool[] named = CreateMask(MaskedNames);
                var isMasked = MaskAtResolution(f => named[f], mode);
                Masked = Enumerable.Range(0, named.Length).Select(isMasked).ToArray();
                FdrIndices = UnmaskedIndices(isMasked);
                Recorded = RecordedFeatures(isMasked, OspreyEnvironment.RecordAllFeatures, mode);
            }

            public bool[] Masked { get; }
            public int[] FdrIndices { get; }
            public bool[] Recorded { get; }
        }
    }
}
