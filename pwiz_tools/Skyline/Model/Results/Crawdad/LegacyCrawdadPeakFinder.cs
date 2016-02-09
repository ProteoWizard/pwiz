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
using pwiz.Crawdad;
using System.Collections.Generic;
using pwiz.Common.PeakFinding;

namespace pwiz.Skyline.Model.Results.Crawdad
{
    /// <summary>
    /// IPeakFinder implementation which uses the C++ peak finder in crawdad.dll
    /// </summary>
    public class LegacyCrawdadPeakFinder : IPeakFinder
    {
        private readonly CrawdadPeakFinder _managedCrawdadPeakFinder;
        public LegacyCrawdadPeakFinder()
        {
            _managedCrawdadPeakFinder = new CrawdadPeakFinder();
        }

        public void SetChromatogram(IList<float> times, IList<float> intensities)
        {
            _managedCrawdadPeakFinder.SetChromatogram(times, intensities);
        }

        public IFoundPeak GetPeak(int startIndex, int endIndex)
        {
            return new LegacyCrawdadPeak(_managedCrawdadPeakFinder.GetPeak(startIndex, endIndex));
        }

        public IList<IFoundPeak> CalcPeaks(int max, int[] idIndices)
        {
            return _managedCrawdadPeakFinder.CalcPeaks(max, idIndices).ConvertAll(peak => (IFoundPeak) new LegacyCrawdadPeak(peak));
        }

        public void Dispose()
        {
            _managedCrawdadPeakFinder.Dispose();
        }

        public bool IsHeightAsArea { get { return _managedCrawdadPeakFinder.IsHeightAsArea; } }

        public IList<float> Intensities1D { get { return _managedCrawdadPeakFinder.Intensities1d; } }
        public IList<float> Intensities2d { get { return _managedCrawdadPeakFinder.Intensities2d; } }
    }

    public class LegacyCrawdadPeak : IFoundPeak
    {
        private readonly CrawdadPeak _crawdadPeak;
        public LegacyCrawdadPeak(CrawdadPeak crawdadPeak)
        {
            _crawdadPeak = crawdadPeak;
        }

        public int StartIndex
        {
            get { return _crawdadPeak.StartIndex; }
        }

        public int EndIndex
        {
            get { return _crawdadPeak.EndIndex; }
        }

        public int TimeIndex
        {
            get { return _crawdadPeak.TimeIndex; }
        }

        public float Area
        {
            get { return _crawdadPeak.Area; }
        }

        public float BackgroundArea
        {
            get { return _crawdadPeak.BackgroundArea; }
        }

        public float Height
        {
            get { return _crawdadPeak.Height; }
        }

        public float Fwhm
        {
            get { return _crawdadPeak.Fwhm; }
        }

        public bool FwhmDegenerate
        {
            get { return _crawdadPeak.FwhmDegenerate; }
        }

        public bool Identified
        {
            get { return _crawdadPeak.Identified; }
        }

        public int Length
        {
            get { return _crawdadPeak.Length; }
        }

        public void ResetBoundaries(int startIndex, int endIndex)
        {
            _crawdadPeak.ResetBoundaries(startIndex, endIndex);
        }

        public void Dispose()
        {
            _crawdadPeak.Dispose();
        }

        public override string ToString()
        {
            return PeakFinders.PeakToString(this);
        }
    }
}
