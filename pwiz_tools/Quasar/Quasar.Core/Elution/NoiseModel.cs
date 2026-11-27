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

namespace pwiz.Quasar.Core.Elution
{
    /// <summary>
    /// The variance of one trace point, from counting statistics plus a baseline term. If a point's
    /// intensity is g times an ion count N with N Poisson, its variance is g^2 E[N] = g * E[intensity],
    /// where g is the intensity per ion (from Glimpse's ion conversion on HRAM data). The baseline
    /// variance covers what counting does not: electronic noise and unresolved chemical background.
    /// </summary>
    public sealed class NoiseModel
    {
        public NoiseModel(double intensityPerIon, double baselineVariance)
        {
            if (!(intensityPerIon >= 0.0) || double.IsInfinity(intensityPerIon))
                throw new ArgumentOutOfRangeException(nameof(intensityPerIon));
            if (!(baselineVariance > 0.0) || double.IsInfinity(baselineVariance))
                throw new ArgumentOutOfRangeException(nameof(baselineVariance));
            IntensityPerIon = intensityPerIon;
            BaselineVariance = baselineVariance;
        }

        /// <summary>Intensity units per detected ion.</summary>
        public double IntensityPerIon { get; }

        /// <summary>Variance of a point with no signal, in intensity units squared. Always positive.</summary>
        public double BaselineVariance { get; }

        /// <summary>The variance of a point whose expected intensity is <paramref name="expected"/>.</summary>
        public double Variance(double expected)
        {
            return IntensityPerIon * Math.Max(0.0, expected) + BaselineVariance;
        }

        /// <summary>
        /// This model with every variance multiplied by <paramref name="factor"/>, for the per-run
        /// correction when weighted residuals of good fits do not have unit variance.
        /// </summary>
        public NoiseModel Scaled(double factor)
        {
            return new NoiseModel(IntensityPerIon * factor, BaselineVariance * factor);
        }

        public override string ToString()
        {
            return $@"Noise[perIon={IntensityPerIon}, baselineVar={BaselineVariance}]";
        }
    }
}
