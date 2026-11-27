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
    /// The run-level shape prior (spec Stage 2a): the typical sigma and tau of the run's peaks,
    /// with a log-normal spread. A template fit adds it as two pseudo-observations,
    /// (ln sigma - ln sigma0) / spread and (ln tau - ln tau0) / spread, so a weak template whose
    /// own points cannot settle the tail is pulled toward the run's typical shape instead of
    /// collapsing to tau = 0, while a strong template's points outweigh it.
    /// </summary>
    public sealed class EmgShapePrior
    {
        /// <summary>Default spread: about a factor of 1.5 either way at one standard deviation.</summary>
        public const double DEFAULT_LOG_SPREAD = 0.4;

        public EmgShapePrior(double sigma, double tau, double logSpread = DEFAULT_LOG_SPREAD)
        {
            if (!(sigma > 0.0))
                throw new ArgumentOutOfRangeException(nameof(sigma));
            if (!(tau > 0.0))
                throw new ArgumentOutOfRangeException(nameof(tau));
            if (!(logSpread > 0.0))
                throw new ArgumentOutOfRangeException(nameof(logSpread));
            Sigma = sigma;
            Tau = tau;
            LogSpread = logSpread;
        }

        /// <summary>Typical sigma of the run's peaks, in the time unit of the axis.</summary>
        public double Sigma { get; }

        /// <summary>Typical tau of the run's peaks, in the time unit of the axis.</summary>
        public double Tau { get; }

        /// <summary>Standard deviation of ln sigma and ln tau around the typical values.</summary>
        public double LogSpread { get; }

        /// <summary>The prior's two standardized residuals for a candidate sigma and tau.</summary>
        public double SigmaResidual(double sigma)
        {
            return LogResidual(sigma, Sigma);
        }

        public double TauResidual(double tau)
        {
            return LogResidual(tau, Tau);
        }

        private double LogResidual(double value, double typical)
        {
            // A non-positive value is as far from typical as the fit can step; keep it finite.
            double v = Math.Max(Math.Abs(value), typical * 1e-6);
            return (Math.Log(v) - Math.Log(typical)) / LogSpread;
        }
    }
}
