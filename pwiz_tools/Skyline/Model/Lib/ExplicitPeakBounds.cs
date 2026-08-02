/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Common.PeakFinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.Lib
{
    public class ExplicitPeakBounds : Immutable
    {
        public const double UNKNOWN_SCORE = double.NaN;
        public static readonly ExplicitPeakBounds EMPTY = new ExplicitPeakBounds(0, 0, UNKNOWN_SCORE);

        public ExplicitPeakBounds(double startTime, double endTime, double score)
        {
            StartTime = startTime;
            EndTime = endTime;
            Score = score;
        }
        public double StartTime { get; private set; }
        public double EndTime { get; private set; }
        public double Score { get; private set; }

        public PeakBounds PeakBounds
        {
            get { return new PeakBounds(StartTime, EndTime); }
        }

        protected bool Equals(ExplicitPeakBounds other)
        {
            return StartTime.Equals(other.StartTime) && EndTime.Equals(other.EndTime) && Score.Equals(other.Score);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ExplicitPeakBounds) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = StartTime.GetHashCode();
                hashCode = (hashCode * 397) ^ EndTime.GetHashCode();
                hashCode = (hashCode * 397) ^ Score.GetHashCode();
                return hashCode;
            }
        }

        public bool IsEmpty
        {
            get { return StartTime == 0 && EndTime == 0; }
        }

        public override string ToString()
        {
            return string.Format(@"[{0:F04},{1:F04}]:{2:F04}", StartTime, EndTime, Score);
        }

        public ScoredPeakBounds ToScoredPeak()
        {
            return new ScoredPeakBounds((float)(StartTime + EndTime) / 2, (float)StartTime, (float)EndTime, (float)Score);
        }
    }

    /// <summary>
    /// The explicit peak boundaries that a library holds for one spectrum, at most one per file,
    /// keyed by the index of the file in the library's <see cref="LibraryFiles"/> list.
    /// The <see cref="Results.ReplicatePositions"/> says which file indexes have boundaries, and
    /// is shared between spectra which have them in the same files.
    /// </summary>
    public class ExplicitPeakBoundsDict : Immutable
    {
        public static readonly ExplicitPeakBoundsDict EMPTY =
            new ExplicitPeakBoundsDict(Array.Empty<KeyValuePair<int, ExplicitPeakBounds>>());
        private ReplicatePositions _positions;
        private float[] _startTimes;
        private float[] _endTimes;
        private float[] _scores;

        public ExplicitPeakBoundsDict(IEnumerable<KeyValuePair<int, ExplicitPeakBounds>> entriesByFileIndex)
        {
            var list = entriesByFileIndex.OrderBy(entry => entry.Key).ToList();
            var counts = new int[list.Count == 0 ? 0 : list[list.Count - 1].Key + 1];
            foreach (var entry in list)
            {
                counts[entry.Key]++;
            }

            _positions = ReplicatePositions.FromCounts(counts);
            _startTimes = list.Select(e => (float) e.Value.StartTime).ToArray();
            _endTimes = list.Select(e => (float)e.Value.EndTime).ToArray();
            if (list.Any(entry=>!ExplicitPeakBounds.UNKNOWN_SCORE.Equals(entry.Value.Score)))
            {
                _scores = list.Select(e => (float) e.Value.Score).ToArray();
            }
        }

        public int Count
        {
            get { return _startTimes.Length; }
        }

        public bool TryGetValue(int fileIndex, out ExplicitPeakBounds value)
        {
            if (_positions.GetCount(fileIndex) == 0)
            {
                value = null;
                return false;
            }

            value = GetExplicitPeakBoundsAt(_positions.GetStart(fileIndex));
            return true;
        }

        private ExplicitPeakBounds GetExplicitPeakBoundsAt(int index)
        {
            return new ExplicitPeakBounds(_startTimes[index], _endTimes[index],
                _scores == null ? ExplicitPeakBounds.UNKNOWN_SCORE : _scores[index]);
        }

        public IEnumerable<KeyValuePair<int, ExplicitPeakBounds>> GetEntries()
        {
            for (int fileIndex = 0; fileIndex < _positions.ReplicateCount; fileIndex++)
            {
                if (_positions.GetCount(fileIndex) > 0)
                {
                    yield return new KeyValuePair<int, ExplicitPeakBounds>(fileIndex,
                        GetExplicitPeakBoundsAt(_positions.GetStart(fileIndex)));
                }
            }
        }

        public ExplicitPeakBoundsDict ValueFromCache(ValueCache valueCache)
        {
            var positions = valueCache.CacheValue(_positions);
            if (ReferenceEquals(positions, _positions))
            {
                return this;
            }

            return ChangeProp(ImClone(this), im => im._positions = positions);
        }
    }
}
