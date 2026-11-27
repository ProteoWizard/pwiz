/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on skyline-glimpse (https://github.com/maccoss/skyline-glimpse)
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

namespace pwiz.Quasar.Features.Hardklor
{
    /// <summary>
    /// The mass analyzer whose peak width Hardklor assumes (its <c>instrument</c> key). Only the peak-width
    /// formula depends on it; see <see cref="HardklorSettings.PeakWidth"/>.
    /// </summary>
    public enum HardklorInstrument
    {
        /// <summary>FWHM = m/z^1.5 / (20 * resolution): resolution falls with the square root of m/z.</summary>
        orbitrap,

        /// <summary>FWHM = m/z / resolution: constant resolution.</summary>
        tof,

        /// <summary>FWHM = resolution / 5000: constant peak width.</summary>
        qit,

        /// <summary>FWHM = m/z^2 / (400 * resolution): resolution falls with m/z.</summary>
        fticr,
    }

    /// <summary>
    /// Settings of <see cref="HardklorFinder"/>. The defaults are the configuration Skyline's feature detection
    /// writes for Hardklor (pwiz_tools/Skyline/Model/DdaSearch/HardklorSearchEngine.cs): algorithm Version2,
    /// charge_algorithm Quick, charge 1 to 7, correlation from Skyline's default minimum idotp of 0.9, depth 2,
    /// averagine_mod 0, molecule_max_mz 5000, distribution_area 1, centroided input.
    /// </summary>
    /// <remarks>
    /// Skyline's other keys have no effect on the Version2 algorithm and have no setting here:
    /// signal_to_noise, sn_window, static_sn, sensitivity, mz_window and max_features are read only by the
    /// Version1 algorithm; smooth, boxcar averaging and mz_min/mz_max are 0 (off); centroided input means no
    /// centroiding step.
    /// </remarks>
    public sealed record HardklorSettings
    {
        /// <summary>Skyline's default minimum idotp (normalized contrast angle) for feature detection.</summary>
        public const double SKYLINE_DEFAULT_IDOTP = 0.9;

        /// <summary>The settings Skyline uses by default, for an Orbitrap at 60,000 resolution.</summary>
        public static HardklorSettings Default { get; } = new HardklorSettings();

        /// <summary>
        /// Skyline's conversion from a normalized contrast angle (its idotp) to Hardklor's cosine correlation,
        /// cos((1 - idotp) * pi / 2) (ImportPeptideSearch.HardklorSettings.CosineAngleFromNormalizedContrastAngle).
        /// </summary>
        public static double CorrelationFromIdotP(double normalizedContrastAngle) =>
            Math.Cos((1 - Math.Min(1.0, normalizedContrastAngle)) * Math.PI / 2);

        /// <summary>The analyzer, which sets the peak width (Hardklor's <c>instrument</c>).</summary>
        public HardklorInstrument Instrument { get; init; } = HardklorInstrument.orbitrap;

        /// <summary>
        /// Resolution at m/z 400 (Hardklor's <c>resolution</c>). For <see cref="HardklorInstrument.qit"/> this is
        /// the value Hardklor divides by 5000 to get the peak width.
        /// </summary>
        public double Resolution { get; init; } = 60_000;

        /// <summary>Lowest charge considered (<c>charge_min</c>).</summary>
        public int MinCharge { get; init; } = 1;

        /// <summary>Highest charge considered (<c>charge_max</c>; Skyline's HardklorSearchEngine.MaxCharge).</summary>
        public int MaxCharge { get; init; } = 7;

        /// <summary>
        /// Cosine correlation an envelope must exceed (<c>correlation</c>). Skyline converts its minimum idotp
        /// with <see cref="CorrelationFromIdotP"/>: 0.9 becomes 0.98769.
        /// </summary>
        public double Correlation { get; init; } = CorrelationFromIdotP(SKYLINE_DEFAULT_IDOTP);

        /// <summary>Most envelopes kept with base peaks within 0.5 m/z of each other (<c>depth</c>).</summary>
        public int Depth { get; init; } = 2;

        /// <summary>Highest m/z of a model envelope (<c>molecule_max_mz</c>); peaks above it are not modelled.</summary>
        public double MaxMoleculeMz { get; init; } = 5000;

        /// <summary>
        /// When true (<c>distribution_area = 1</c>, Skyline's choice) an envelope's intensity is its base peak
        /// times the model's summed relative abundance; when false, the base peak's intensity alone.
        /// </summary>
        public bool DistributionArea { get; init; } = true;

        /// <summary>
        /// Expected full width at half maximum of a peak at <paramref name="mz"/> (CHardklor2::CalcFWHM).
        /// </summary>
        public double PeakWidth(double mz) => Instrument switch
        {
            HardklorInstrument.orbitrap => mz * Math.Sqrt(mz) / (20 * Resolution),
            HardklorInstrument.tof => mz / Resolution,
            HardklorInstrument.qit => Resolution / 5000.0,
            _ => mz * mz / (400 * Resolution),
        };

        internal void Validate()
        {
            if (MinCharge < 1 || MaxCharge < MinCharge || MaxCharge > 100)
            {
                throw new ArgumentException(string.Format(
                    QuasarFeaturesResources.HardklorSettings_Validate_Charge_range__0____1__must_lie_within_1__100_, MinCharge, MaxCharge));
            }
            if (!(Resolution > 0))
                throw new ArgumentException(string.Format(QuasarFeaturesResources.HardklorSettings_Validate_Resolution__0__must_be_positive_, Resolution));
            if (Depth < 1)
                throw new ArgumentException(string.Format(QuasarFeaturesResources.HardklorSettings_Validate_Depth__0__must_be_at_least_1_, Depth));
            if (!(MaxMoleculeMz >= 5))
                throw new ArgumentException(string.Format(QuasarFeaturesResources.HardklorSettings_Validate_MaxMoleculeMz__0__must_be_at_least_5_, MaxMoleculeMz));
        }
    }
}
