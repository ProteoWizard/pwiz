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

using System.Globalization;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// The PIN features one search uses, computes and records, resolved from its resolution and
    /// <c>--feature-set</c> (<see cref="PinFeatureMask.For(OspreyConfig)"/>) and the process's
    /// research switches. Immutable: two searches in one process at different resolutions each read
    /// their own. See <see cref="PinFeatureMask"/> for what the sets hold and why.
    /// </summary>
    public sealed class PinFeatureSelection
    {
        private readonly bool[] _masked;
        private readonly int[] _fdrIndices;
        private readonly bool[] _recorded;
        private readonly bool[] _computed;

        internal PinFeatureSelection(ResolutionMode mode, OspreyFeatureSet? featureSetGiven, bool[] masked,
            int[] fdrIndices, bool[] recorded, bool[] computed)
        {
            Mode = mode;
            FeatureSetGiven = featureSetGiven;
            _masked = masked;
            _fdrIndices = fdrIndices;
            _recorded = recorded;
            _computed = computed;
        }

        /// <summary>The resolution of the search.</summary>
        public ResolutionMode Mode { get; }

        /// <summary>The <c>--feature-set</c> the search was given, null when the resolution chooses.</summary>
        public OspreyFeatureSet? FeatureSetGiven { get; }

        /// <summary>The feature set in effect: <see cref="PinFeatureMask.FeatureSetFor"/> of the search's settings.</summary>
        public OspreyFeatureSet FeatureSet => PinFeatureMask.FeatureSetFor(FeatureSetGiven, Mode);

        /// <summary>True when the PIN feature at <paramref name="featureIndex"/> is masked.</summary>
        public bool IsMasked(int featureIndex)
        {
            return _masked[featureIndex];
        }

        /// <summary>
        /// The PIN index of each feature the FDR classifier sees, ascending: position i of an FDR
        /// feature vector holds PIN feature <c>FdrFeatureIndices[i]</c>. Under the <c>core</c> set
        /// this is 0 to 20, the 21 features Osprey had before the families were added, so a core
        /// classifier sees exactly the matrix it saw then.
        /// </summary>
        public int[] FdrFeatureIndices => _fdrIndices;

        /// <summary>The PIN names of the FDR feature vector's positions, in its order.</summary>
        public string[] FdrFeatureNames => SelectFdrFeatures(ParquetScoreCache.PIN_FEATURE_NAMES);

        /// <summary>
        /// The FDR classifier's view of a per-PIN-feature array (feature names, feature metadata, a
        /// full feature vector): its unmasked elements, in PIN order. Use it wherever something
        /// indexed by PIN feature has to line up with an FDR feature vector, so names and values
        /// cannot drift apart.
        /// </summary>
        public T[] SelectFdrFeatures<T>(T[] perPinFeature)
        {
            return PinFeatureMask.SelectFeatures(perPinFeature, _fdrIndices);
        }

        /// <summary>
        /// True when the scores parquet holds a column for the PIN feature at <paramref name="featureIndex"/>:
        /// a core feature, a feature the FDR classifier uses, or any feature under
        /// OSPREY_RECORD_ALL_FEATURES=1. A feature of a computed family the classifier does not use
        /// is not recorded (<see cref="IsComputed"/>), and a feature the resolution cannot use never
        /// is (<see cref="PinFeatureMask.IsAvailable"/>).
        /// </summary>
        public bool IsRecorded(int featureIndex)
        {
            return _recorded[featureIndex];
        }

        /// <summary>
        /// This selection, recording only the first PIN column (<c>fragment_coelution_sum</c>), for
        /// a stub scores parquet: the stub readers take each row's coelution sum from that column,
        /// and nothing that reads a stub reads another feature. What the classifier sees and what
        /// Stage 4 computes are unchanged. Only the <c>FdrEntry</c> writer takes it: the
        /// <c>CoelutionScoredEntry</c> one expects every core feature column.
        /// </summary>
        public PinFeatureSelection StubColumnsOnly()
        {
            var recorded = new bool[_recorded.Length];
            recorded[0] = true;
            return new PinFeatureSelection(Mode, FeatureSetGiven, _masked, _fdrIndices, recorded, _computed);
        }

        /// <summary>
        /// True when Stage 4 computes the PIN feature at <paramref name="featureIndex"/>: a core
        /// feature, or a feature of a family with a recorded feature. Every recorded feature is computed.
        /// </summary>
        public bool IsComputed(int featureIndex)
        {
            return _computed[featureIndex];
        }

        /// <summary>
        /// A copy of <see cref="IsComputed"/> for every PIN feature, for the scorer, which cannot
        /// see this class.
        /// </summary>
        public bool[] ComputedFeatureFlags()
        {
            return (bool[])_computed.Clone();
        }

        /// <summary>
        /// True when OSPREY_FEATURE_MASK leaves unmasked a feature this search's resolution cannot use.
        /// </summary>
        public bool LeavesUnavailableFeaturesUnmasked()
        {
            return PinFeatureMask.LeavesUnavailableFeaturesUnmasked(Mode);
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
        public string RecordedFeaturesValidityKeySuffix()
        {
            return PinFeatureMask.RecordedFeaturesValidityKeySuffix(IsRecorded, Mode);
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
        public string ValidityKeySuffix()
        {
            return PinFeatureMask.ValidityKeySuffix(OspreyEnvironment.FeatureMask, FeatureSetGiven, Mode);
        }

        /// <summary>
        /// True when the classifier sees a runner-up feature. Only then does a peak rescored at
        /// override bounds need the first pass's candidates, which take a second CWT detection
        /// per peak (<c>ScoringContext.OverrideRunnerUpCandidates</c>); otherwise the detection
        /// is skipped and the masked features report a lone candidate.
        /// </summary>
        public bool UsesRunnerUpFeatures()
        {
            return PinFeatureMask.UsesRunnerUpFeatures(IsMasked);
        }

        /// <summary>
        /// The validity-key term for the tasks that rescore peaks at override bounds and use those
        /// scores (PerFileRescoring, SecondPassFDR): present exactly when
        /// <see cref="UsesRunnerUpFeatures"/> is, because only then are the override-path
        /// candidates found. Under the <c>core</c> set it is empty, so no core output directory
        /// is invalidated.
        /// </summary>
        public string RescoreValidityKeySuffix()
        {
            return PinFeatureMask.RescoreValidityKeySuffix(IsMasked);
        }

        /// <summary>
        /// The run-log line naming the features the classifier uses: the feature set and how it was
        /// chosen, or OSPREY_FEATURE_MASK when that overrides it, with the count used.
        /// </summary>
        public string DescribeFeatures()
        {
            int used = _fdrIndices.Length;
            int total = ParquetScoreCache.NUM_PIN_FEATURES;
            if (OspreyEnvironment.FeatureMask != null)
            {
                if (FeatureSetGiven == null)
                {
                    return string.Format(CultureInfo.InvariantCulture,
                        @"FDR features: OSPREY_FEATURE_MASK ({0} of {1})", used, total);
                }
                return string.Format(CultureInfo.InvariantCulture,
                    @"FDR features: OSPREY_FEATURE_MASK ({0} of {1}; {2} is overridden)",
                    used, total, OspreyArgNames.Text(OspreyArgNames.FEATURE_SET));
            }
            return string.Format(CultureInfo.InvariantCulture, @"FDR features: {0} ({1} of {2}{3})",
                FeatureSet, used, total,
                FeatureSetGiven == null ? @", the default at this resolution" : string.Empty);
        }
    }
}
