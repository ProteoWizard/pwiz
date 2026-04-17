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
    /// Defines an m/z isolation window. Maps to osprey-core/src/types.rs IsolationWindow.
    /// </summary>
    public struct IsolationWindow
    {
        public double Center { get; set; }
        public double LowerOffset { get; set; }
        public double UpperOffset { get; set; }

        public IsolationWindow(double center, double lowerOffset, double upperOffset)
        {
            Center = center;
            LowerOffset = lowerOffset;
            UpperOffset = upperOffset;
        }

        /// <summary>
        /// Creates a symmetric isolation window centered at the given m/z.
        /// </summary>
        public static IsolationWindow Symmetric(double center, double halfWidth)
        {
            return new IsolationWindow(center, halfWidth, halfWidth);
        }

        public double LowerBound { get { return Center - LowerOffset; } }
        public double UpperBound { get { return Center + UpperOffset; } }
        public double Width { get { return LowerOffset + UpperOffset; } }

        /// <summary>
        /// Returns true if the given m/z falls within this window (half-open interval).
        /// </summary>
        public bool Contains(double mz)
        {
            return mz >= LowerBound && mz < UpperBound;
        }
    }
}
