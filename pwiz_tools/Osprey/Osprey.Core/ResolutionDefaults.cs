/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// The settings a search uses when neither the command line nor a developer <c>OSPREY_*</c>
    /// variable chose them, per resolution. The one table of them:
    /// <see cref="OspreyConfig.ApplyResolutionMode"/> reads it once the resolution is known, for the
    /// classifier, <c>--protein-boost</c> and the <see cref="ScoringSettings"/>.
    ///
    /// <para><see cref="LEGACY"/> holds the values every setting had before the defaults depended
    /// on the resolution. A configuration that never learns its resolution, such as a test scoring
    /// directly, keeps them. The validity keys spell a setting only when it differs from its
    /// legacy value, so a default that moves keys exactly like the variable that once chose it,
    /// and outputs written under that variable are adopted.</para>
    /// </summary>
    public sealed class ResolutionDefaults
    {
        /// <summary>The values before the defaults depended on the resolution; unit resolution keeps their scoring settings.</summary>
        public static readonly ResolutionDefaults LEGACY = new ResolutionDefaults(FdrClassifier.LinearSvm, ProteinBoost.off,
            LEGACY_MIN_RT_TOLERANCE, LEGACY_LOESS_BANDWIDTH, new ScoringSettings(OspreyEnvironment.DEFAULT_XIC_FRAGMENTS, null, false, null, null, true));

        /// <summary>
        /// HRAM data: the recipe that closed most of the DIA-NN gap on SEA-AD Astral (#4950): gbdt with
        /// out-of-fold scoring and the protein-best column (which brings the two-round training), 12
        /// fragment XICs, a 7 ppm correlation re-extraction, the correlation-chosen reference XIC, the
        /// local RT tolerance over 15 calibration neighbors, a 0.40 min tc_* half window, a 1.0 min RT
        /// search-window floor and a 0.10 LOESS bandwidth. On all 82 SEA-AD files, a 0.3 bandwidth lost
        /// 1.8% at 1% true FDP and a 0.15 min half window 0.7%, though neither showed at 3 or 20 files.
        /// </summary>
        public static readonly ResolutionDefaults HRAM = new ResolutionDefaults(FdrClassifier.Gbdt, ProteinBoost.best,
            1.0, 0.10, new ScoringSettings(12, 7.0, true, 15, 0.40, true));

        /// <summary>
        /// Unit-resolution data: the HRAM classifier and protein-best column with the legacy scoring
        /// settings. On the regression Stellar entrapment data, gbdt with protein-best and the
        /// default feature set found 40,340 / 41,441 second-pass precursors (library / generated
        /// decoys) against 27,171 / 28,792 for the legacy defaults, at no added wall time; the HRAM
        /// scoring settings lost on top of it (35,763 / 36,468), so they stay HRAM-only.
        /// </summary>
        public static readonly ResolutionDefaults UNIT_RESOLUTION = new ResolutionDefaults(FdrClassifier.Gbdt, ProteinBoost.best,
            LEGACY_MIN_RT_TOLERANCE, LEGACY_LOESS_BANDWIDTH, LEGACY.Scoring);

        /// <summary>The defaults for <paramref name="mode"/>.</summary>
        public static ResolutionDefaults For(ResolutionMode mode)
        {
            return mode == ResolutionMode.UnitResolution ? UNIT_RESOLUTION : HRAM;
        }

        /// <summary>The RT search-window floor in minutes before it depended on the resolution.</summary>
        public const double LEGACY_MIN_RT_TOLERANCE = 0.5;

        /// <summary>The RT calibration LOESS bandwidth before it depended on the resolution.</summary>
        public const double LEGACY_LOESS_BANDWIDTH = 0.3;

        private ResolutionDefaults(FdrClassifier fdrClassifier, ProteinBoost proteinBoost, double minRtTolerance,
            double loessBandwidth, ScoringSettings scoring)
        {
            FdrClassifier = fdrClassifier;
            ProteinBoost = proteinBoost;
            MinRtTolerance = minRtTolerance;
            LoessBandwidth = loessBandwidth;
            Scoring = scoring;
        }

        /// <summary>The first-pass classifier (OSPREY_FDR_MODEL overrides it).</summary>
        public FdrClassifier FdrClassifier { get; }

        /// <summary>The protein boost (<c>--protein-boost</c> overrides it).</summary>
        public ProteinBoost ProteinBoost { get; }

        /// <summary>
        /// The RT search-window floor in minutes (<see cref="RTCalibrationConfig.MinRtTolerance"/>;
        /// OSPREY_MIN_RT_TOLERANCE overrides it). It enters the search identity, so a search with the
        /// default keys like one that set the variable to the same value.
        /// </summary>
        public double MinRtTolerance { get; }

        /// <summary>
        /// The RT calibration LOESS bandwidth (<see cref="RTCalibrationConfig.LoessBandwidth"/>;
        /// OSPREY_LOESS_BANDWIDTH overrides it). It enters the search identity, like the floor.
        /// </summary>
        public double LoessBandwidth { get; }

        /// <summary>The scoring settings, before the developer variables that override them.</summary>
        public ScoringSettings Scoring { get; }
    }
}
