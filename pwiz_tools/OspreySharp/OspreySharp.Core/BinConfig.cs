/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
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

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Configuration for m/z binning used in spectral matching.
    /// Maps to osprey-core/src/types.rs BinConfig.
    /// </summary>
    public struct BinConfig
    {
        public double BinWidth { get; set; }
        public double BinOffset { get; set; }
        public double InverseBinWidth { get; set; }
        public double OneMinusOffset { get; set; }
        public double MaxMz { get; set; }
        public int NBins { get; set; }

        /// <summary>
        /// Creates a BinConfig for unit-resolution instruments.
        /// </summary>
        public static BinConfig UnitResolution()
        {
            double binWidth = 1.0005079;
            double offset = 0.4;
            double maxMz = 2000.0;
            double inverseBinWidth = 1.0 / binWidth;
            double oneMinusOffset = 1.0 - offset;
            int nBins = (int)(maxMz * inverseBinWidth + oneMinusOffset) + 1;

            return new BinConfig
            {
                BinWidth = binWidth,
                BinOffset = offset,
                InverseBinWidth = inverseBinWidth,
                OneMinusOffset = oneMinusOffset,
                MaxMz = maxMz,
                NBins = nBins
            };
        }

        /// <summary>
        /// Creates the appropriate BinConfig for the given resolution mode.
        /// </summary>
        public static BinConfig ForResolution(ResolutionMode mode)
        {
            return mode == ResolutionMode.HRAM ? HRAM() : UnitResolution();
        }

        /// <summary>
        /// Creates a BinConfig for high-resolution accurate mass instruments.
        /// </summary>
        public static BinConfig HRAM()
        {
            double binWidth = 0.02;
            double offset = 0.0;
            double maxMz = 2000.0;
            double inverseBinWidth = 1.0 / binWidth;
            double oneMinusOffset = 1.0 - offset;
            int nBins = (int)(maxMz * inverseBinWidth + oneMinusOffset) + 1;

            return new BinConfig
            {
                BinWidth = binWidth,
                BinOffset = offset,
                InverseBinWidth = inverseBinWidth,
                OneMinusOffset = oneMinusOffset,
                MaxMz = maxMz,
                NBins = nBins
            };
        }

        /// <summary>
        /// Converts an m/z value to a bin index.
        /// </summary>
        public readonly int MzToBin(double mz)
        {
            return (int)(mz * InverseBinWidth + OneMinusOffset);
        }

        /// <summary>
        /// Converts a bin index back to an m/z value.
        /// </summary>
        public double BinToMz(int bin)
        {
            return (bin - OneMinusOffset) / InverseBinWidth;
        }
    }
}
