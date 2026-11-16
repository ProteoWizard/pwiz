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
    /// classifier, <c>--protein-best</c> and the <see cref="ScoringSettings"/>.
    ///
    /// <para><see cref="LEGACY"/> holds the values every setting had before the defaults depended
    /// on the resolution. A configuration that never learns its resolution, such as a test scoring
    /// directly, keeps them. The validity keys spell a setting only when it differs from its
    /// legacy value, so a default that moves keys exactly like the variable that once chose it,
    /// and outputs written under that variable are adopted.</para>
    /// </summary>
    public sealed class ResolutionDefaults
    {
        /// <summary>The values before the defaults depended on the resolution, and still the unit-resolution ones.</summary>
        public static readonly ResolutionDefaults LEGACY = new ResolutionDefaults(FdrClassifier.LinearSvm, false,
            LEGACY_MIN_RT_TOLERANCE, new ScoringSettings(OspreyEnvironment.DEFAULT_XIC_FRAGMENTS, null, false, null, null, true));

        /// <summary>
        /// HRAM data: the recipe that closed most of the DIA-NN gap on SEA-AD Astral (#4950): gbdt with
        /// out-of-fold scoring and the protein-best column (which brings the two-round training), 12
        /// fragment XICs, a 7 ppm correlation re-extraction, the correlation-chosen reference XIC, the
        /// local RT tolerance over 15 calibration neighbors and a 1.0 min RT search-window floor. The
        /// recipe's 0.40 min tc_* half window is left out: dropped one at a time on 20 SEA-AD files it
        /// changed detections by -0.4%, inside the seed spread, where the 1.0 min floor gained 2.8%.
        /// </summary>
        public static readonly ResolutionDefaults HRAM = new ResolutionDefaults(FdrClassifier.Gbdt, true,
            1.0, new ScoringSettings(12, 7.0, true, 15, null, true));

        /// <summary>Unit-resolution data.</summary>
        public static readonly ResolutionDefaults UNIT_RESOLUTION = LEGACY;

        /// <summary>The defaults for <paramref name="mode"/>.</summary>
        public static ResolutionDefaults For(ResolutionMode mode)
        {
            return mode == ResolutionMode.UnitResolution ? UNIT_RESOLUTION : HRAM;
        }

        /// <summary>The RT search-window floor in minutes before it depended on the resolution.</summary>
        public const double LEGACY_MIN_RT_TOLERANCE = 0.5;

        private ResolutionDefaults(FdrClassifier fdrClassifier, bool proteinBest, double minRtTolerance,
            ScoringSettings scoring)
        {
            FdrClassifier = fdrClassifier;
            ProteinBest = proteinBest;
            MinRtTolerance = minRtTolerance;
            Scoring = scoring;
        }

        /// <summary>The first-pass classifier (OSPREY_FDR_MODEL overrides it).</summary>
        public FdrClassifier FdrClassifier { get; }

        /// <summary>The protein-best training column (<c>--protein-best</c> overrides it).</summary>
        public bool ProteinBest { get; }

        /// <summary>
        /// The RT search-window floor in minutes (<see cref="RTCalibrationConfig.MinRtTolerance"/>;
        /// OSPREY_MIN_RT_TOLERANCE overrides it). It enters the search identity, so a search with the
        /// default keys like one that set the variable to the same value.
        /// </summary>
        public double MinRtTolerance { get; }

        /// <summary>The scoring settings, before the developer variables that override them.</summary>
        public ScoringSettings Scoring { get; }
    }
}
