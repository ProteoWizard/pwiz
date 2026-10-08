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

namespace pwiz.Osprey.Demux
{
    /// <summary>
    /// A time-of-flight digitizer's sampling grid: sample k is at m/z = (Origin + k * Step)^2, uniform in
    /// sqrt(m/z), that is in flight time. On a ZenoTOF 8600 every MS2 profile spectrum of a run, and of
    /// every run measured, lies on one such grid (step 9.786595e-5), so the same sample index means the
    /// same m/z in every spectrum.
    /// </summary>
    public sealed class TofGrid
    {
        /// <summary>A point farther than this from its sample (in samples) is not on the grid.</summary>
        public const double MAX_OFF_GRID = 0.05;

        public TofGrid(double origin, double step)
        {
            if (step <= 0)
                throw new ArgumentException(@"The grid step must be positive.");
            Origin = origin;
            Step = step;
        }

        /// <summary>sqrt(m/z) of sample 0.</summary>
        public double Origin { get; }

        /// <summary>sqrt(m/z) per sample.</summary>
        public double Step { get; }

        /// <summary>
        /// The grid of one profile spectrum (m/z ascending, zero points present or not): the step is the
        /// median of the gaps within 20% of the smallest common gap, which within peaks is one sample,
        /// and sample 0 is the spectrum's first point. Null when too few points, or when they do not lie
        /// on one uniform grid.
        /// </summary>
        public static TofGrid Detect(IReadOnlyList<double> mz)
        {
            if (mz.Count < 100)
                return null;
            var gaps = new List<double>(mz.Count);
            for (int i = 1; i < mz.Count; i++)
            {
                double gap = Math.Sqrt(mz[i]) - Math.Sqrt(mz[i - 1]);
                if (gap > 0)
                    gaps.Add(gap);
            }
            if (gaps.Count < 50)
                return null;
            gaps.Sort(); // Array.Sort OK: primitive values sorted only for a percentile and a median
            double smallest = gaps[gaps.Count / 20];
            var steps = new List<double>(gaps.Count);
            foreach (double gap in gaps)
            {
                if (Math.Abs(gap / smallest - 1) < 0.2)
                    steps.Add(gap);
            }
            double step = steps[steps.Count / 2];
            var grid = new TofGrid(Math.Sqrt(mz[0]), step);
            for (int i = 0; i < mz.Count; i++)
            {
                if (Math.Abs(grid.Position(mz[i]) - grid.Index(mz[i])) > MAX_OFF_GRID)
                    return null;
            }
            return grid;
        }

        /// <summary>The nearest sample to an m/z.</summary>
        public long Index(double mz)
        {
            return (long)Math.Round(Position(mz));
        }

        /// <summary>The fractional sample position of an m/z.</summary>
        public double Position(double mz)
        {
            return (Math.Sqrt(mz) - Origin) / Step;
        }

        /// <summary>The m/z of a (fractional) sample position.</summary>
        public double Mz(double position)
        {
            double root = Origin + position * Step;
            return root * root;
        }
    }
}
