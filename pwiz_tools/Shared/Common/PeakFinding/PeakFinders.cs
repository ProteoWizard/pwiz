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
    public static class PeakFinders
    {
        public static IPeakFinder NewDefaultPeakFinder()
        {
            return new PeakFinder();
        }

        public static string PeakToString(IFoundPeak foundPeak)
        {
            // ReSharper disable LocalizableElement
            return String.Format(
                "StartIndex: {0} EndIndex: {1} TimeIndex: {2} Area: {3} BackgroundArea: {4} Height: {5} Fwhm: {6} FwhmDegenerate: {7} Identified: {8}",
                foundPeak.StartIndex, foundPeak.EndIndex, foundPeak.TimeIndex, foundPeak.Area,
                foundPeak.BackgroundArea, foundPeak.Height, foundPeak.Fwhm,
                foundPeak.FwhmDegenerate, foundPeak.Identified);
            // ReSharper restore LocalizableElement
        }
    }
}
