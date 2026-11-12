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
using System.Globalization;
using System.Linq;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// The PIN features the FDR classifier uses, and the ones it may not (masked). A masked feature
    /// is absent from the vectors the FDR stage loads (<see cref="ParquetScoreCache.LoadPinFeaturesFromParquet"/>):
    /// each holds only the <see cref="FdrFeatureNames"/>, in PIN order, so training costs what the
    /// unmasked features cost and not what all of them would.
    ///
    /// <para>What the classifier uses is a named feature set (<c>--feature-set</c>,
    /// <see cref="OspreyFeatureSet"/>): <c>core</c>, the 21 core features; <c>default</c>, the core
    /// and the family features in <see cref="DEFAULT_FEATURES"/>; or <c>all</c>. Without the
    /// option the resolution chooses (<see cref="FeatureSetFor"/>): <c>default</c> for HRAM data,
    /// <c>core</c> at unit resolution. OSPREY_FEATURE_MASK, a research switch, overrides the set:
    /// when it is set, it alone names what is masked.</para>
    ///
    /// <para>The features used also decide what scoring computes and records. Stage 4 computes the
    /// 21 core features and each feature family (<see cref="FAMILY_FIRST_FEATURES"/>) with a feature
    /// the classifier uses (<see cref="IsComputed"/>), because a family's features share one
    /// computation; the scores parquet holds a column for the core features and for each feature the
    /// classifier uses, and none for the rest (<see cref="IsRecorded"/>), even of a computed family.
    /// The core features are recorded whatever the set, because more than the classifier reads them.
    /// OSPREY_RECORD_ALL_FEATURES=1, a developer and research switch, computes and records every
    /// feature, so arms that differ only in the features used can share one scoring and run only the
    /// FDR stage. The recorded columns key per-file scoring and rescoring
    /// (<see cref="RecordedFeaturesValidityKeySuffix()"/>), and a reader that needs a column the
    /// file does not hold refuses it rather than reading zeros.</para>
    ///
    /// <para>Some families can only use HRAM data (<see cref="HRAM_ONLY_FAMILIES"/>). At unit
    /// resolution they are masked and unrecorded whatever the set or the switches say, so a run there
    /// has the same FDR vectors and scores columns as if the families did not exist.</para>
    /// </summary>
    public static class PinFeatureMask
    {
        /// <summary>
        /// The members of the <c>default</c> feature set (<see cref="OspreyFeatureSet.@default"/>), in
        /// PIN order: the 21 core features and the family features chosen for the default search.
        /// The one list to edit to change the default set; it must hold every core feature, and
        /// Program checks every name at startup (<see cref="UnknownDefaultFeatureName"/>).
        /// </summary>
        public static readonly string[] DEFAULT_FEATURES =
        {
            // The 21 core features
            @"fragment_coelution_sum", @"fragment_coelution_max", @"n_coeluting_fragments",
            @"peak_apex", @"peak_area", @"peak_sharpness",
            @"xcorr", @"consecutive_ions", @"explained_intensity",
            @"mass_accuracy_deviation_mean", @"abs_mass_accuracy_deviation_mean",
            @"rt_deviation", @"abs_rt_deviation",
            @"ms1_precursor_coelution", @"ms1_isotope_cosine",
            @"median_polish_cosine", @"median_polish_residual_ratio",
            @"sg_weighted_xcorr", @"sg_weighted_cosine",
            @"median_polish_min_fragment_r2", @"median_polish_residual_correlation",
            // The family features
            @"peak_coelution_margin", @"peak_rank_margin", @"peak_candidate_count",
            @"context_peptide_length", @"context_precursor_charge", @"context_precursor_mz",
            @"peak_spectrum_cosine_mean", @"peak_spectrum_cosine_sd",
            @"ref_corr_rank2", @"apex_log_intensity_excl_top",
            @"window_share_rank1", @"window_share_rank2", @"window_share_rank3",
            @"window_share_rank4", @"window_share_rank5",
            @"tc_sum6", @"tc_corr_rank1", @"tc_corr_rank2", @"tc_corr_rank3", @"tc_corr_rank4",
            @"tc_corr_rank5", @"tc_corr_rank6", @"tc_res_sum", @"tc_res_mean",
            @"heavy_sum6", @"heavy_n_good", @"tight45_sum6", @"tight20_sum6", @"tc_best_sum6",
            @"shadow_corr_sum6", @"shadow_frac6",
            @"lib_cosine_coel", @"lib_entropy_sim", @"lib_coel_share",
            @"spec_match_prob_max", @"spec_proj_cosine_xt_max",
        };

        /// <summary>
        /// The first PIN feature of each feature family, in PIN order. A family runs from its first
        /// feature to the next family's, and the last to the end of the PIN features; the features
        /// before the first family are the 21 core features. Scoring computes or skips a family as
        /// a unit, because its features share one per-candidate computation (a byproduct of
        /// <c>OspreyScoringContext</c>): computing one feature of a family costs nearly what
        /// computing all of them does.
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
        /// Always computed and recorded, whatever the set or the mask, because more than the
        /// classifier reads them - the coelution sum is every row's first-pass score, and the
        /// median-polish diagnostics and the training export read core features directly.
        /// </summary>
        public static readonly int CORE_FEATURE_COUNT =
            Array.IndexOf(ParquetScoreCache.PIN_FEATURE_NAMES, FAMILY_FIRST_FEATURES[0]);

        // Lazy so an unknown name surfaces as its own exception at first use rather than as a
        // TypeInitializationException; Program checks the names at startup. One per resolution and
        // feature set, indexed by ResolvedIndex.
        private static readonly Lazy<ResolvedFeatures>[] _resolved = CreateResolvedCache();
        private static readonly Lazy<bool[]> _hramOnly = new Lazy<bool[]>(CreateHramOnlyFlags);

        private static ResolutionMode _resolutionMode = ResolutionMode.HRAM;
        private static OspreyFeatureSet? _featureSet;

        private static ResolvedFeatures Resolved => _resolved[ResolvedIndex(_resolutionMode, FeatureSet)].Value;

        /// <summary>
        /// Sets the resolution of this process's search, HRAM until then, and the feature set its
        /// command line chose (<c>--feature-set</c>), null to let the resolution choose. The switches
        /// this class reads are per process, and so are the settings they combine with: the host calls
        /// this once the resolution is known, before anything reads the mask.
        /// </summary>
        public static void ApplySearchSettings(ResolutionMode mode, OspreyFeatureSet? featureSet)
        {
            _resolutionMode = mode;
            _featureSet = featureSet;
        }

        /// <summary>The resolution <see cref="ApplySearchSettings"/> last set, HRAM until then.</summary>
        public static ResolutionMode AppliedResolutionMode => _resolutionMode;

        /// <summary>The <c>--feature-set</c> <see cref="ApplySearchSettings"/> last set, null when the resolution chooses.</summary>
        public static OspreyFeatureSet? AppliedFeatureSet => _featureSet;

        /// <summary>The feature set in effect: <see cref="FeatureSetFor"/> of the applied settings.</summary>
        public static OspreyFeatureSet FeatureSet => FeatureSetFor(_featureSet, _resolutionMode);

        /// <summary>
        /// The feature set a search uses: <paramref name="featureSet"/>, or when the command line did not
        /// choose one, <c>default</c> for HRAM data and <c>core</c> at unit resolution, where the
        /// families add little and every computed feature costs.
        /// </summary>
        public static OspreyFeatureSet FeatureSetFor(OspreyFeatureSet? featureSet, ResolutionMode mode)
        {
            return featureSet ?? (mode == ResolutionMode.HRAM ? OspreyFeatureSet.@default : OspreyFeatureSet.core);
        }

        /// <summary>
        /// The PIN features of a named set, ascending, before the resolution drops the features it
        /// cannot use.
        /// </summary>
        public static int[] FeatureSetIndices(OspreyFeatureSet featureSet)
        {
            var names = ParquetScoreCache.PIN_FEATURE_NAMES;
            switch (featureSet)
            {
                case OspreyFeatureSet.core:
                    return Enumerable.Range(0, CORE_FEATURE_COUNT).ToArray();
                case OspreyFeatureSet.@default:
                    return Enumerable.Range(0, names.Length).Where(f => DEFAULT_FEATURES.Contains(names[f])).ToArray();
                default:
                    return Enumerable.Range(0, names.Length).ToArray();
            }
        }

        /// <summary>
        /// The mask a named feature set implies: every PIN feature outside it is masked. Throws for a
        /// <see cref="DEFAULT_FEATURES"/> name that is not a PIN feature.
        /// </summary>
        public static Func<int, bool> FeatureSetMask(OspreyFeatureSet featureSet)
        {
            if (featureSet == OspreyFeatureSet.@default)
            {
                string unknown = UnknownDefaultFeatureName();
                if (unknown != null)
                {
                    // Exception text for internal diagnostics; Program reports it to the user at startup
                    throw new InvalidOperationException(
                        string.Format(@"PinFeatureMask.DEFAULT_FEATURES names an unknown feature: {0}", unknown));
                }
            }
            var inSet = new bool[ParquetScoreCache.NUM_PIN_FEATURES];
            foreach (int f in FeatureSetIndices(featureSet))
                inSet[f] = true;
            return f => !inSet[f];
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
            if (OspreyEnvironment.FeatureMask == null)
                return false;
            bool[] named = CreateMask(OspreyEnvironment.FeatureMask);
            return Enumerable.Range(0, named.Length).Any(f => !named[f] && !IsAvailable(f, _resolutionMode));
        }

        /// <summary>True when the PIN feature at <paramref name="featureIndex"/> is masked.</summary>
        public static bool IsMasked(int featureIndex)
        {
            return Resolved.Masked[featureIndex];
        }

        /// <summary>
        /// The PIN index of each feature the FDR classifier sees, ascending: position i of an FDR
        /// feature vector holds PIN feature <c>FdrFeatureIndices[i]</c>. Under the <c>core</c> set
        /// this is 0 to 20, the 21 features Osprey had before the families were added, so a core
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
        /// True when the scores parquet holds a column for the PIN feature at <paramref name="featureIndex"/>:
        /// a core feature, a feature the FDR classifier uses, or any feature under
        /// OSPREY_RECORD_ALL_FEATURES=1. A feature of a computed family the classifier does not use
        /// is not recorded (<see cref="IsComputed"/>), and a feature the resolution cannot use never
        /// is (<see cref="IsAvailable"/>).
        /// </summary>
        public static bool IsRecorded(int featureIndex)
        {
            return Resolved.Recorded[featureIndex];
        }

        /// <summary>
        /// True when Stage 4 computes the PIN feature at <paramref name="featureIndex"/>: a core
        /// feature, or a feature of a family with a recorded feature. Every recorded feature is computed.
        /// </summary>
        public static bool IsComputed(int featureIndex)
        {
            return Resolved.Computed[featureIndex];
        }

        /// <summary>
        /// A copy of <see cref="IsComputed"/> for every PIN feature, for the scorer, which cannot
        /// see this class.
        /// </summary>
        public static bool[] ComputedFeatureFlags()
        {
            return (bool[])Resolved.Computed.Clone();
        }

        /// <summary>
        /// Which PIN features the scores parquet records when <paramref name="isMasked"/> is the mask:
        /// the core features and every unmasked feature - or every feature when <paramref name="recordAll"/>.
        /// </summary>
        public static bool[] RecordedFeatures(Func<int, bool> isMasked, bool recordAll)
        {
            var recorded = new bool[ParquetScoreCache.NUM_PIN_FEATURES];
            for (int f = 0; f < recorded.Length; f++)
                recorded[f] = f < CORE_FEATURE_COUNT || recordAll || !isMasked(f);
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
        /// Which PIN features scoring computes to record <paramref name="recorded"/>: the core
        /// features, and every feature of each family with a recorded feature, because a family's
        /// features share their computation.
        /// </summary>
        public static bool[] ComputedFeatures(bool[] recorded)
        {
            var computed = new bool[ParquetScoreCache.NUM_PIN_FEATURES];
            for (int f = 0; f < CORE_FEATURE_COUNT; f++)
                computed[f] = true;
            foreach (var (start, end) in FamilyRanges())
            {
                bool compute = Enumerable.Range(start, end - start).Any(f => recorded[f]);
                for (int f = start; f < end; f++)
                    computed[f] = compute;
            }
            return computed;
        }

        /// <summary>
        /// The validity-key term for the tasks whose outputs hold the recorded feature columns:
        /// per-file scoring, which writes the scores parquet, and per-file rescoring, which copies
        /// its rows into the reconciled parquet. It names the recorded columns beyond the core, or
        /// <c>all</c>, so a run that needs a column its scores lack rescores instead of adopting them,
        /// and two arms that record the same columns - OSPREY_FEATURE_MASK=none and
        /// OSPREY_RECORD_ALL_FEATURES=1, say - share their scores. Empty when only the core features
        /// are recorded, the <c>core</c> set, so no core output directory is invalidated.
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
        /// resolution, where <c>all</c> means every feature <paramref name="mode"/> can use. A partial
        /// set lists its columns (<c>;featcols=</c>), not their families: a column-level list is not
        /// the family-level <c>;featrec=</c> list an older scoring recorded, so neither adopts the other.
        /// </summary>
        public static string RecordedFeaturesValidityKeySuffix(Func<int, bool> isRecorded, ResolutionMode mode)
        {
            var available = Enumerable.Range(CORE_FEATURE_COUNT, ParquetScoreCache.NUM_PIN_FEATURES - CORE_FEATURE_COUNT)
                .Where(f => IsAvailable(f, mode)).ToArray();
            var recorded = available.Where(isRecorded).ToArray();
            if (recorded.Length == 0)
                return string.Empty;
            if (recorded.Length == available.Length)
                return @";featrec=all" + AvailableFeaturesTerm(mode);
            return @";featcols=" + string.Join(@"+", recorded.Select(f => ParquetScoreCache.PIN_FEATURE_NAMES[f]));
        }

        /// <summary>
        /// The first name in OSPREY_FEATURE_MASK that is not a PIN feature, or null when every name
        /// is one (or the mask is unset). Checked at startup so a mistyped name costs seconds, not Stages 1-4.
        /// </summary>
        public static string UnknownFeatureName()
        {
            return OspreyEnvironment.FeatureMask?.FirstOrDefault(IsUnknownName);
        }

        /// <summary>
        /// The first name in <see cref="DEFAULT_FEATURES"/> that is not a PIN feature, or a core
        /// feature it leaves out; null when it is a valid set. Checked at startup, and by a unit test,
        /// because the list is edited by hand.
        /// </summary>
        public static string UnknownDefaultFeatureName()
        {
            string unknown = DEFAULT_FEATURES.FirstOrDefault(IsUnknownName);
            if (unknown != null)
                return unknown;
            return ParquetScoreCache.PIN_FEATURE_NAMES.Take(CORE_FEATURE_COUNT)
                .FirstOrDefault(name => !DEFAULT_FEATURES.Contains(name));
        }

        /// <summary>
        /// The run-log line naming the features the classifier uses: the feature set and how it was
        /// chosen, or OSPREY_FEATURE_MASK when that overrides it, with the count used.
        /// </summary>
        public static string DescribeFeatures()
        {
            int used = FdrFeatureIndices.Length;
            int total = ParquetScoreCache.NUM_PIN_FEATURES;
            if (OspreyEnvironment.FeatureMask != null)
            {
                return string.Format(CultureInfo.InvariantCulture,
                    @"FDR features: OSPREY_FEATURE_MASK ({0} of {1}; {2} is overridden)",
                    used, total, OspreyArgNames.Text(OspreyArgNames.FEATURE_SET));
            }
            return string.Format(CultureInfo.InvariantCulture, @"FDR features: {0} ({1} of {2}{3})",
                FeatureSet, used, total,
                _featureSet == null ? @", the default at this resolution" : string.Empty);
        }

        /// <summary>
        /// The validity-key term for every task whose output depends on the features the classifier
        /// uses: first-pass FDR and everything after it. Emitted for every arm, because outputs
        /// written before the mask moved to load time carry no term and were computed under a mask
        /// nothing recorded.
        ///
        /// <para>The <c>fdrcols</c> term records that a masked feature is absent from the FDR
        /// vectors rather than a column of zeros. The saved first-pass model and each run's
        /// first-pass feature sums are sized to those vectors, so one written under the zero-column
        /// layout must not be adopted: a frozen model 97 features wide would decline every 21-wide
        /// vector it was handed.</para>
        /// </summary>
        public static string ValidityKeySuffix()
        {
            return ValidityKeySuffix(OspreyEnvironment.FeatureMask, _featureSet, _resolutionMode);
        }

        /// <summary>
        /// <see cref="ValidityKeySuffix()"/> for an explicitly supplied OSPREY_FEATURE_MASK value and
        /// resolution, with the feature set the resolution chooses.
        /// </summary>
        public static string ValidityKeySuffix(string[] featureMask, ResolutionMode mode)
        {
            return ValidityKeySuffix(featureMask, null, mode);
        }

        /// <summary>
        /// <see cref="ValidityKeySuffix()"/> for an explicitly supplied OSPREY_FEATURE_MASK value,
        /// <c>--feature-set</c> and resolution. A mask names what is hidden, so its names are sorted
        /// and what it leaves depends on how many features exist: an HRAM key with a mask of its own
        /// also carries that count (<c>;featavail=</c>), and an output from before features were added
        /// is not adopted. Without a mask, the feature set: <c>core</c> keeps the term the default mask
        /// had when it left the core alone (<c>featmask=default</c>), so no output of a core search is
        /// invalidated; <c>all</c> carries the count like a mask; any other set lists the features it
        /// uses beyond the core (<c>;featset=</c>), so an edit of <see cref="DEFAULT_FEATURES"/> changes
        /// the key. A set is keyed by what it resolves to, so one that is the same at a resolution
        /// keys the same.
        /// </summary>
        public static string ValidityKeySuffix(string[] featureMask, OspreyFeatureSet? featureSet, ResolutionMode mode)
        {
            const string fdrcols = @";fdrcols=unmasked";
            if (featureMask != null)
            {
                string mask = featureMask.Length == 0
                    ? @"none"
                    : string.Join(@"+", featureMask.Distinct().OrderBy(name => name, StringComparer.Ordinal));
                return @";featmask=" + mask + AvailableFeaturesTerm(mode) + fdrcols;
            }
            var used = UnmaskedIndices(MaskAtResolution(FeatureSetMask(FeatureSetFor(featureSet, mode)), mode))
                .Where(f => f >= CORE_FEATURE_COUNT).ToArray();
            if (used.Length == 0)
                return @";featmask=default" + fdrcols;
            int available = Enumerable.Range(CORE_FEATURE_COUNT, ParquetScoreCache.NUM_PIN_FEATURES - CORE_FEATURE_COUNT)
                .Count(f => IsAvailable(f, mode));
            if (used.Length == available)
                return @";featset=all" + AvailableFeaturesTerm(mode) + fdrcols;
            return @";featset=" + string.Join(@"+", used.Select(f => ParquetScoreCache.PIN_FEATURE_NAMES[f])) + fdrcols;
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
        /// candidates found. Under the <c>core</c> set it is empty, so no core output directory
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

        private static bool IsUnknownName(string name)
        {
            return Array.IndexOf(ParquetScoreCache.PIN_FEATURE_NAMES, name) < 0;
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

        // The count of features an HRAM search can use, for the keys that depend on it; nothing at unit
        // resolution, where the HRAM-only families are not available and the count has not changed.
        private static string AvailableFeaturesTerm(ResolutionMode mode)
        {
            if (mode != ResolutionMode.HRAM)
                return string.Empty;
            return @";featavail=" + ParquetScoreCache.NUM_PIN_FEATURES.ToString(CultureInfo.InvariantCulture);
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

        private static OspreyFeatureSet[] AllFeatureSets =>
            (OspreyFeatureSet[])Enum.GetValues(typeof(OspreyFeatureSet));

        private static int ResolvedIndex(ResolutionMode mode, OspreyFeatureSet featureSet)
        {
            return (int)mode * AllFeatureSets.Length + (int)featureSet;
        }

        private static Lazy<ResolvedFeatures>[] CreateResolvedCache()
        {
            var modes = (ResolutionMode[])Enum.GetValues(typeof(ResolutionMode));
            var cache = new Lazy<ResolvedFeatures>[modes.Length * AllFeatureSets.Length];
            foreach (var mode in modes)
            {
                foreach (var featureSet in AllFeatureSets)
                {
                    var m = mode;
                    var s = featureSet;
                    cache[ResolvedIndex(m, s)] = new Lazy<ResolvedFeatures>(() => new ResolvedFeatures(m, s));
                }
            }
            return cache;
        }

        /// <summary>
        /// The process's switches resolved for one resolution and feature set: OSPREY_FEATURE_MASK
        /// when it is set, else the set's mask.
        /// </summary>
        private sealed class ResolvedFeatures
        {
            public ResolvedFeatures(ResolutionMode mode, OspreyFeatureSet featureSet)
            {
                Func<int, bool> named;
                if (OspreyEnvironment.FeatureMask != null)
                {
                    bool[] mask = CreateMask(OspreyEnvironment.FeatureMask);
                    named = f => mask[f];
                }
                else
                {
                    named = FeatureSetMask(featureSet);
                }
                var isMasked = MaskAtResolution(named, mode);
                Masked = Enumerable.Range(0, ParquetScoreCache.NUM_PIN_FEATURES).Select(isMasked).ToArray();
                FdrIndices = UnmaskedIndices(isMasked);
                Recorded = RecordedFeatures(isMasked, OspreyEnvironment.RecordAllFeatures, mode);
                Computed = ComputedFeatures(Recorded);
            }

            public bool[] Masked { get; }
            public int[] FdrIndices { get; }
            public bool[] Recorded { get; }
            public bool[] Computed { get; }
        }
    }
}
