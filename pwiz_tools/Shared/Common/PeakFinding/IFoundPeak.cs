/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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

namespace pwiz.Common.PeakFinding
{
    public interface IFoundPeak : IDisposable
    {
        int StartIndex { get; }
        int EndIndex { get; }
        int TimeIndex { get; }
        float Area { get; }
        float BackgroundArea { get; }
        float Height { get; }
        float Fwhm { get; }
        bool FwhmDegenerate { get; }
        bool Identified { get; }
        int Length { get; }
        /// <summary>
        /// Change the boundaries of the peak without recalculating the area.
        /// TODO(nicksh): this method should be remove so that IFoundPeak can
        /// be made immutable.  Also, it makes no sense to want to change
        /// the boundaries of a peak without recomputing the area.
        /// </summary>
        void ResetBoundaries(int startIndex, int endIndex);
    }
}