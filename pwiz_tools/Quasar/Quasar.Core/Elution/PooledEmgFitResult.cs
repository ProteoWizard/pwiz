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
    /// One EMG shape fitted to several traces at once, with each trace's own amplitude and
    /// baseline (see <see cref="PooledEmgFit"/>).
    /// </summary>
    public sealed class PooledEmgFitResult
    {
        public PooledEmgFitResult(EmgShape shape, double[] amplitudes, double[] baselineStarts,
            double[] baselineEnds, double chiSquare, int pointCount)
        {
            Shape = shape;
            Amplitudes = amplitudes;
            BaselineStarts = baselineStarts;
            BaselineEnds = baselineEnds;
            ChiSquare = chiSquare;
            PointCount = pointCount;
        }

        /// <summary>The shared unit-area shape: the template.</summary>
        public EmgShape Shape { get; }

        /// <summary>Per-trace areas under the shared shape, in input order.</summary>
        public double[] Amplitudes { get; }

        /// <summary>Per-trace baseline at the segment's first point.</summary>
        public double[] BaselineStarts { get; }

        /// <summary>Per-trace baseline at the segment's last point.</summary>
        public double[] BaselineEnds { get; }

        /// <summary>Weighted sum of squared residuals over all traces, with the final robust weights.</summary>
        public double ChiSquare { get; }

        /// <summary>The number of points the shape was fitted to, over all traces.</summary>
        public int PointCount { get; }
    }
}
