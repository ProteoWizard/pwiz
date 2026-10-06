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
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Models;

namespace pwiz.CarafeSharp
{
    /// <summary>
    /// An RT model whose predictions an aligned training's maps take to minutes on one of its gradients: a Chronologer's
    /// hydrophobic index to the runs' median minutes (<see cref="RtAlignment.ToMinutes"/>) or one run's
    /// (<see cref="RtAlignment.ToRunMinutes"/>), or a model's median minutes to one run's. They are clipped at 0 and
    /// divided by the library's rt_max, which the library multiplies back. Owns the model it wraps.
    /// </summary>
    internal sealed class AlignedRtPredictor : IRtPredictor
    {
        private readonly Func<double, double> _toMinutes;
        private readonly double _rtMax;

        /// <param name="inner">The model.</param>
        /// <param name="toMinutes">From what the model predicts to minutes.</param>
        /// <param name="rtMax">The library's rt_max.</param>
        public AlignedRtPredictor(IRtPredictor inner, Func<double, double> toMinutes, double rtMax)
        {
            Inner = inner;
            _toMinutes = toMinutes;
            _rtMax = rtMax;
        }

        /// <summary>The model whose predictions are taken to minutes.</summary>
        public IRtPredictor Inner { get; }

        /// <summary>True: the predictions are the map's minutes as a fraction of the library's rt_max.</summary>
        public bool PredictsNormalizedRt
        {
            get { return true; }
        }

        public double[] Predict(IReadOnlyList<PeptideForm> peptides)
        {
            return Inner.Predict(peptides).Select(hi => Math.Max(0, _toMinutes(hi)) / _rtMax).ToArray();
        }

        public (double Slope, double Intercept) FitIrtCalibration()
        {
            return IrtKit.FitCalibration(Predict);
        }

        public void Dispose()
        {
            Inner.Dispose();
        }
    }
}
