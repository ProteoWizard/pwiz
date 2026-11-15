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
    /// The scoring settings a search uses when neither the command line nor a developer
    /// <c>OSPREY_*</c> variable chose them, per resolution. The one table of them: the command
    /// line (<see cref="OspreyConfig.ApplyResolutionMode"/>, for the classifier and
    /// <c>--protein-best</c>) and <see cref="OspreyEnvironment.ApplyResolutionDefaults"/> (for the
    /// settings that stay environment variables) both read it once the resolution is known.
    ///
    /// <para><see cref="LEGACY"/> holds the values every setting had before the defaults depended
    /// on the resolution. A process that never learns its resolution, such as a test scoring
    /// directly, keeps them. The validity keys spell a setting only when it differs from its
    /// legacy value, so a default that moves keys exactly like the variable that once chose it,
    /// and outputs written under that variable are adopted.</para>
    /// </summary>
    public sealed class ResolutionDefaults
    {
        /// <summary>The values before the defaults depended on the resolution, and still the unit-resolution ones.</summary>
        public static readonly ResolutionDefaults LEGACY = new ResolutionDefaults(
            FdrClassifier.LinearSvm, false, OspreyEnvironment.DEFAULT_XIC_FRAGMENTS, null, false, null, null, false);

        /// <summary>
        /// HRAM data: the recipe that closed most of the DIA-NN gap on SEA-AD Astral (#4950): gbdt with
        /// out-of-fold scoring and the protein-best column (which brings the two-round training), 12
        /// fragment XICs, a 7 ppm correlation re-extraction, the correlation-chosen reference XIC, the
        /// local RT tolerance over 15 calibration neighbors and a 0.40 min tc_* half window.
        /// </summary>
        public static readonly ResolutionDefaults HRAM = new ResolutionDefaults(
            FdrClassifier.Gbdt, true, 12, 7.0, true, 15, 0.40, true);

        /// <summary>Unit-resolution data.</summary>
        public static readonly ResolutionDefaults UNIT_RESOLUTION = LEGACY;

        /// <summary>The defaults for <paramref name="mode"/>.</summary>
        public static ResolutionDefaults For(ResolutionMode mode)
        {
            return mode == ResolutionMode.UnitResolution ? UNIT_RESOLUTION : HRAM;
        }

        private ResolutionDefaults(FdrClassifier fdrClassifier, bool proteinBest, int xicFragments,
            double? corrFragmentTolerancePpm, bool refXicByCorrelation, int? localRtToleranceNeighbors,
            double? tcHalfWindowMinutes, bool gbtOutOfFold)
        {
            FdrClassifier = fdrClassifier;
            ProteinBest = proteinBest;
            XicFragments = xicFragments;
            CorrFragmentTolerancePpm = corrFragmentTolerancePpm;
            RefXicByCorrelation = refXicByCorrelation;
            LocalRtToleranceNeighbors = localRtToleranceNeighbors;
            TcHalfWindowMinutes = tcHalfWindowMinutes;
            GbtOutOfFold = gbtOutOfFold;
        }

        /// <summary>The first-pass classifier (OSPREY_FDR_MODEL overrides it).</summary>
        public FdrClassifier FdrClassifier { get; }

        /// <summary>The protein-best training column (<c>--protein-best</c> overrides it).</summary>
        public bool ProteinBest { get; }

        /// <summary>Fragment XICs per candidate (OSPREY_XIC_FRAGMENTS).</summary>
        public int XicFragments { get; }

        /// <summary>The correlation features' re-extraction tolerance in ppm, or null for none (OSPREY_CORR_FRAGMENT_TOLERANCE).</summary>
        public double? CorrFragmentTolerancePpm { get; }

        /// <summary>The reference fragment XIC chosen by correlation inside the peak (OSPREY_REF_XIC).</summary>
        public bool RefXicByCorrelation { get; }

        /// <summary>The calibration neighbors the local RT tolerance reads, or null for the global one (OSPREY_LOCAL_RT_TOLERANCE).</summary>
        public int? LocalRtToleranceNeighbors { get; }

        /// <summary>The tc_* features' half window in minutes, or null for their own 0.15 (OSPREY_TC_HALF_WINDOW).</summary>
        public double? TcHalfWindowMinutes { get; }

        /// <summary>Out-of-fold scoring of the gbdt first pass's training rows (OSPREY_GBT_OOF).</summary>
        public bool GbtOutOfFold { get; }
    }
}
