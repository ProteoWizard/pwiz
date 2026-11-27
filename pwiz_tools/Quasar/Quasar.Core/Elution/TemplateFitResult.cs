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

namespace pwiz.Quasar.Core.Elution
{
    /// <summary>
    /// One trace segment fitted as a non-negative combination of fixed templates plus a linear
    /// baseline (see <see cref="TemplateFit"/>).
    /// </summary>
    public sealed class TemplateFitResult
    {
        public TemplateFitResult(int start, double[] amplitudes, double baselineStart, double baselineEnd,
            double[] fitted, double chiSquare, double[] deltaChiSquare, int[] supportPoints)
        {
            Start = start;
            Amplitudes = amplitudes;
            BaselineStart = baselineStart;
            BaselineEnd = baselineEnd;
            Fitted = fitted;
            ChiSquare = chiSquare;
            DeltaChiSquare = deltaChiSquare;
            SupportPoints = supportPoints;
        }

        /// <summary>Index of the segment's first point on the trace's scan axis.</summary>
        public int Start { get; }

        /// <summary>
        /// Per-template amplitudes, in input order. Each template has unit area, so an amplitude is
        /// the trace's area in that template, in intensity times the time unit of the axis.
        /// </summary>
        public double[] Amplitudes { get; }

        /// <summary>The baseline at the segment's first point.</summary>
        public double BaselineStart { get; }

        /// <summary>The baseline at the segment's last point.</summary>
        public double BaselineEnd { get; }

        /// <summary>The fitted values over the segment (templates plus baseline).</summary>
        public double[] Fitted { get; }

        /// <summary>Sum of squared residuals, each divided by the noise model's variance at the fit.</summary>
        public double ChiSquare { get; }

        /// <summary>
        /// Per template, how much <see cref="ChiSquare"/> rises when that template is dropped and the
        /// rest refitted. The likelihood-ratio statistic for membership.
        /// </summary>
        public double[] DeltaChiSquare { get; }

        /// <summary>Per template, the segment points where the template is above 5% of its peak.</summary>
        public int[] SupportPoints { get; }

        /// <summary>
        /// Whether the trace is a member of template <paramref name="k"/>: the template explains it
        /// (its likelihood-ratio statistic exceeds <paramref name="threshold"/>) on at least
        /// <see cref="TemplateFit.MIN_SUPPORT_POINTS"/> points.
        /// </summary>
        public bool IsMember(int k, double threshold = TemplateFit.DEFAULT_MEMBERSHIP_THRESHOLD)
        {
            return Amplitudes[k] > 0.0 && DeltaChiSquare[k] > threshold &&
                   SupportPoints[k] >= TemplateFit.MIN_SUPPORT_POINTS;
        }
    }
}
