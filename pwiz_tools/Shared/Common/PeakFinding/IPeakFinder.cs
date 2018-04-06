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
using System.Collections.Generic;

namespace pwiz.Common.PeakFinding
{
    public interface IPeakFinder : IDisposable
    {
        void SetChromatogram(IList<float> times, IList<float> intensities);
        IFoundPeak GetPeak(int startIndex, int endIndex);
        IList<IFoundPeak> CalcPeaks(int max, int[] idIndices);
        IList<float> Intensities1d { get; }
        IList<float> Intensities2d { get; }
        bool IsHeightAsArea { get; }
    }
}