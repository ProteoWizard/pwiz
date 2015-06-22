/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

using System.IO;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{

    /// <summary>
    /// This class stores chromatogram intensity (and optional time) values for one transition.
    /// Memory is allocated in large-ish chunks and shared between various instances of ChromCollector.
    /// Blocks are paged out to a disk file if the data grows too large to fit in pre-allocated
    /// slots.
    /// </summary>
    public sealed class ChromCollector
    {
        public static ChromCollector EMPTY = new ChromCollector();

        public int StatusId { get; private set; }
        public BlockedList<float> Intensities { get; private set; } 
        public SortedBlockedList<float> Times { get; set; } 
        public BlockedList<float> MassErrors { get; private set; }
        public BlockedList<int> Scans { get; set; }

        public ChromCollector(int statusId, bool hasTimes, bool hasMassErrors)
        {
            StatusId = statusId;
            Intensities = new BlockedList<float>();
            if (hasTimes)
                Times = new SortedBlockedList<float>();
            if (hasMassErrors)
                MassErrors = new BlockedList<float>();
        }

        private ChromCollector()
        {
        }

        /// <summary>
        /// Get a chromatogram with properly sorted time values.
        /// </summary>
        public void ReleaseChromatogram(out float[] times, out float[] intensities, out float[] massErrors, out int[] scanIds)
        {
            if (Times == null)
            {
                times = new float[0];
                intensities = new float[0];
                massErrors = null;
                scanIds = null;
                return;
            }

            times = Times.ToArray();
            intensities = Intensities.ToArray();
            massErrors = MassErrors != null
                ? MassErrors.ToArray()
                : null;
            scanIds = Scans != null
                ? Scans.ToArray()
                : null;

            // Make sure times and intensities match in length
            if (times.Length != intensities.Length)
            {
                throw new InvalidDataException(
                    string.Format(Resources.ChromCollected_ChromCollected_Times__0__and_intensities__1__disagree_in_point_count,
                    times.Length, intensities.Length));
            }
        }
    }
}