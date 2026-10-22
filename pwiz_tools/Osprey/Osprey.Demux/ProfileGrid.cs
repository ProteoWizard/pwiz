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
    /// A profile spectrum's sampling grid, uniform in some coordinate of m/z, so that the same sample index means the
    /// same m/z in every spectrum of a run: the joint solve fits its peak basis on it (<see cref="JointDemultiplexer"/>).
    /// </summary>
    public interface IProfileGrid
    {
        /// <summary>The nearest sample to an m/z.</summary>
        long Index(double mz);

        /// <summary>The fractional sample position of an m/z.</summary>
        double Position(double mz);

        /// <summary>The m/z of a (fractional) sample position.</summary>
        double Mz(double position);
    }

    /// <summary>
    /// A grid uniform in m/z: sample k is at Origin + k * Step. A linear ion trap's profile (Thermo Stellar: 0.2 Th,
    /// every MS2 spectrum of a run from 200.2 to 1200 m/z on the same points) is sampled this way, and its peak has
    /// about the same width in Th at every m/z.
    /// </summary>
    public sealed class LinearGrid : IProfileGrid
    {
        /// <summary>A point farther than this from its sample (in samples) is not on the grid.</summary>
        public const double MAX_OFF_GRID = 0.05;

        public LinearGrid(double origin, double step)
        {
            if (step <= 0)
                throw new ArgumentException(@"The grid step must be positive.");
            Origin = origin;
            Step = step;
        }

        /// <summary>m/z of sample 0.</summary>
        public double Origin { get; }

        /// <summary>Th per sample.</summary>
        public double Step { get; }

        /// <summary>
        /// The grid of one profile spectrum (m/z ascending, zero points present or not): the step is the median of
        /// the gaps within 20% of the smallest common gap, and sample 0 is the spectrum's first point. Null when too
        /// few points, or when they do not lie on one uniform grid.
        /// </summary>
        public static LinearGrid Detect(IReadOnlyList<double> mz)
        {
            if (mz.Count < 100)
                return null;
            var gaps = new List<double>(mz.Count);
            for (int i = 1; i < mz.Count; i++)
            {
                double gap = mz[i] - mz[i - 1];
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
            var grid = new LinearGrid(mz[0], steps[steps.Count / 2]);
            for (int i = 0; i < mz.Count; i++)
            {
                if (Math.Abs(grid.Position(mz[i]) - grid.Index(mz[i])) > MAX_OFF_GRID)
                    return null;
            }
            return grid;
        }

        public long Index(double mz)
        {
            return (long)Math.Round(Position(mz));
        }

        public double Position(double mz)
        {
            return (mz - Origin) / Step;
        }

        public double Mz(double position)
        {
            return Origin + position * Step;
        }
    }
}
