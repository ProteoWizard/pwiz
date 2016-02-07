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
using pwiz.Common.PeakFinding;

namespace pwiz.Skyline.Model.Results.Crawdad
{
    public static class Crawdads
    {
        private enum PeakFinderOption
        {
            NewPeakFinder,
            LegacyPeakFinder,
            ConsensusFavoringNew,
            ConsensusFavoringLegacy,
        }
        private static PeakFinderOption _peakFinderOption =
#if DEBUG
            PeakFinderOption.ConsensusFavoringNew
#else
            PeakFinderOption.NewPeakFinder
#endif
            ;
        public static IPeakFinder NewCrawdadPeakFinder()
        {
            ConsensusPeakFinder consensusPeakFinder;
            switch (_peakFinderOption)
            {
                case PeakFinderOption.NewPeakFinder:
                    return PeakFinders.NewDefaultPeakFinder();
                case PeakFinderOption.LegacyPeakFinder:
                    return new LegacyCrawdadPeakFinder();
                case PeakFinderOption.ConsensusFavoringNew:
                    consensusPeakFinder = new ConsensusPeakFinder(new []
                    {
                        PeakFinders.NewDefaultPeakFinder(),
                        new LegacyCrawdadPeakFinder(), 
                    });
                    break;
                case PeakFinderOption.ConsensusFavoringLegacy:
                    consensusPeakFinder = new ConsensusPeakFinder(new []
                    {
                        new LegacyCrawdadPeakFinder(), 
                        PeakFinders.NewDefaultPeakFinder(),
                    });
                    break;
                default:
                    throw new ArgumentException();
            }
#if DEBUG
            consensusPeakFinder.ThrowOnMismatch = true;
#endif
            if (Program.FunctionalTest)
            {
                consensusPeakFinder.ThrowOnMismatch = true;
            }
            return consensusPeakFinder;
        }
    }
}
