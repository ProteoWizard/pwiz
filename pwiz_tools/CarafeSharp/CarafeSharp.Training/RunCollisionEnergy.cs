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

namespace pwiz.CarafeSharp.Training
{
    /// <summary>
    /// A training run's collision energy as its file reports it, and the NCE the run trains (and
    /// a library from it predicts) with. pwiz reports every vendor's energy as PSI-MS's collision
    /// energy in eV, but a Thermo file's value is the NCE of its scan filter; any other vendor's
    /// is in eV, which the MS2 model's NCE input is not, so its NCE is calibrated
    /// (<see cref="NceCalibration"/>).
    /// </summary>
    public sealed class RunCollisionEnergy
    {
        /// <summary>The run's own NCE, from a Thermo file (or a file that names no vendor, as Carafe reads it).</summary>
        public const string FROM_FILE = @"file";
        /// <summary>Calibrated on the run's spectra, its energy being in eV.</summary>
        public const string CALIBRATED = @"calibrated";
        /// <summary><c>-nce</c>.</summary>
        public const string COMMAND_LINE = @"-nce";
        /// <summary>Carafe's default, for a run that reports no energy and no <c>-nce</c>.</summary>
        public const string DEFAULT = @"default";

        public const string NCE_UNIT = @"NCE";
        public const string EV_UNIT = @"eV";

        public RunCollisionEnergy(double nce, string source, double? energy, string unit)
        {
            Nce = nce;
            Source = source;
            Energy = energy;
            Unit = unit;
        }

        /// <summary>The NCE the run trains with.</summary>
        public double Nce { get; internal set; }

        /// <summary>Where <see cref="Nce"/> came from: <see cref="FROM_FILE"/>, <see cref="CALIBRATED"/>, <see cref="COMMAND_LINE"/> or <see cref="DEFAULT"/>.</summary>
        public string Source { get; internal set; }

        /// <summary>The collision energy most of the run's MS2 spectra were acquired at, as its file reports it, or null.</summary>
        public double? Energy { get; }

        /// <summary>The unit of <see cref="Energy"/>: <see cref="NCE_UNIT"/> or <see cref="EV_UNIT"/>, or null when the run reports none.</summary>
        public string Unit { get; }

        /// <summary>The calibration that chose <see cref="Nce"/>, or null.</summary>
        public NceCalibration Calibration { get; internal set; }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, @"NCE {0} ({1}); the file records {2}{3}", Nce, Source, DescribeEnergy(),
                Calibration == null ? string.Empty : @"; calibration: " + Calibration);
        }

        /// <summary>The recorded energy with its unit: "NCE 30", "35 eV", or "no collision energy".</summary>
        public string DescribeEnergy()
        {
            if (!Energy.HasValue)
                return @"no collision energy";
            return Unit == NCE_UNIT
                ? string.Format(CultureInfo.InvariantCulture, @"NCE {0}", Energy.Value)
                : string.Format(CultureInfo.InvariantCulture, @"{0} eV", Energy.Value);
        }
    }
}
