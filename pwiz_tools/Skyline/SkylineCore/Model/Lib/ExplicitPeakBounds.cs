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
using System.Collections;
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
    /// indexed by the position of the file in the library's <see cref="LibraryFiles"/> list. A
    /// file which has none reads as null, as does an index which is out of range.
    /// The <see cref="Results.ReplicatePositions"/> says which file indexes have boundaries, and
    /// is shared between the spectra which have them in the same files.
    /// </summary>
    public class ExplicitPeakBoundsList : Immutable, IReadOnlyList<ExplicitPeakBounds>
    {
        public static readonly ExplicitPeakBoundsList EMPTY =
            new ExplicitPeakBoundsList(Array.Empty<ExplicitPeakBounds>());
        private ReplicatePositions _positions;
        private float[] _startTimes;
        private float[] _endTimes;
        private float[] _scores;

        /// <summary>
        /// Takes the boundaries of each file in file index order, with a null for each file which
        /// has none.
        /// </summary>
        public ExplicitPeakBoundsList(IEnumerable<ExplicitPeakBounds> peakBoundsByFileIndex)
        {
            var list = peakBoundsByFileIndex.ToList();
            while (list.Count > 0 && list[list.Count - 1] == null)
            {
                list.RemoveAt(list.Count - 1);
            }
            _positions = ReplicatePositions.FromCounts(list.Select(peakBounds => peakBounds == null ? 0 : 1));
            var peakBoundsList = list.Where(peakBounds => peakBounds != null).ToList();
            _startTimes = peakBoundsList.Select(peakBounds => (float) peakBounds.StartTime).ToArray();
            _endTimes = peakBoundsList.Select(peakBounds => (float) peakBounds.EndTime).ToArray();
            if (peakBoundsList.Any(peakBounds => !ExplicitPeakBounds.UNKNOWN_SCORE.Equals(peakBounds.Score)))
            {
                _scores = peakBoundsList.Select(peakBounds => (float) peakBounds.Score).ToArray();
            }
        }

        /// <summary>
        /// One more than the highest file index which has boundaries. Indexing past this returns
        /// null, the same as a file in range which has none.
        /// </summary>
        public int Count
        {
            get { return _positions.ReplicateCount; }
        }

        public bool IsEmpty
        {
            get { return _startTimes.Length == 0; }
        }

        /// <summary>
        /// The boundaries in a file, or null if it has none. Indexes which are out of range
        /// return null rather than throwing.
        /// </summary>
        public ExplicitPeakBounds this[int fileIndex]
        {
            get
            {
                if (_positions.GetCount(fileIndex) == 0)
                {
                    return null;
                }

                int index = _positions.GetStart(fileIndex);
                return new ExplicitPeakBounds(_startTimes[index], _endTimes[index],
                    _scores == null ? ExplicitPeakBounds.UNKNOWN_SCORE : _scores[index]);
            }
        }

        public IEnumerator<ExplicitPeakBounds> GetEnumerator()
        {
            return Enumerable.Range(0, Count).Select(fileIndex => this[fileIndex]).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public ExplicitPeakBoundsList Merge(IReadOnlyList<ExplicitPeakBounds> other)
        {
            var count = Math.Max(Count, other.Count);
            return new ExplicitPeakBoundsList(Enumerable.Range(0, count).Select(i => this[i] ?? other[i]));
        }

        public ExplicitPeakBoundsList ValueFromCache(ValueCache valueCache)
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
