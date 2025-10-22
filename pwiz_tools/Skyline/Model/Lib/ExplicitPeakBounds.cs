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
using pwiz.Common.Collections;
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

    public class ExplicitPeakBoundsDict<TKey> : Immutable, IReadOnlyDictionary<TKey, ExplicitPeakBounds> where TKey : IComparable
    {
        public static readonly ExplicitPeakBoundsDict<TKey> EMPTY =
            new ExplicitPeakBoundsDict<TKey>(Array.Empty<KeyValuePair<TKey, ExplicitPeakBounds>>());
        private ImmutableList<TKey> _keys;
        private float[] _startTimes;
        private float[] _endTimes;
        private float[] _scores;

        public ExplicitPeakBoundsDict(IEnumerable<KeyValuePair<TKey, ExplicitPeakBounds>> entries)
        {
            var list = entries.OrderBy(entry => entry.Key).ToList();
            _keys = ImmutableList.ValueOf(list.Select(e => e.Key));
            _startTimes = list.Select(e => (float) e.Value.StartTime).ToArray();
            _endTimes = list.Select(e => (float)e.Value.EndTime).ToArray();
            if (list.Any(entry=>!ExplicitPeakBounds.UNKNOWN_SCORE.Equals(entry.Value.Score)))
            {
                _scores = list.Select(e => (float) e.Value.Score).ToArray();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<TKey, ExplicitPeakBounds>> GetEnumerator()
        {
            return Keys.Zip(Values, (k, v) => new KeyValuePair<TKey, ExplicitPeakBounds>(k, v)).GetEnumerator();
        }

        public int Count
        {
            get
            {
                return _keys.Count;
            }
        }

        public bool ContainsKey(TKey key)
        {
            return IndexOfKey(key) >= 0;
        }

        public bool TryGetValue(TKey key, out ExplicitPeakBounds value)
        {
            int index = IndexOfKey(key);
            if (index < 0)
            {
                value = null;
                return false;
            }

            value = GetExplicitPeakBoundsAt(index);
            return true;
        }

        public ExplicitPeakBounds this[TKey key]
        {
            get
            {
                if (TryGetValue(key, out var value))
                {
                    return value;
                }

                throw new KeyNotFoundException();
            }
        }

        public KeyValuePair<TKey, ExplicitPeakBounds> this[int index]
        {
            get
            {
                return new KeyValuePair<TKey, ExplicitPeakBounds>(_keys[index], GetExplicitPeakBoundsAt(index));
            }
        }

        public IEnumerable<TKey> Keys
        {
            get { return _keys; }
        }

        public IEnumerable<ExplicitPeakBounds> Values
        {
            get
            {
                return Enumerable.Range(0, Count).Select(GetExplicitPeakBoundsAt);
            }
        }

        private int IndexOfKey(TKey key)
        {
            int i = CollectionUtil.BinarySearch(_keys, key);
            return i < 0 ? -1 : i;
        }

        private ExplicitPeakBounds GetExplicitPeakBoundsAt(int index)
        {
            return new ExplicitPeakBounds(_startTimes[index], _endTimes[index],
                _scores == null ? ExplicitPeakBounds.UNKNOWN_SCORE : _scores[index]);
        }

        public ExplicitPeakBoundsDict<TKey> ValueFromCache(ValueCache valueCache)
        {
            var newKeys = valueCache.CacheValue(_keys);
            if (ReferenceEquals(newKeys, _keys))
            {
                return this;
            }

            return ChangeProp(ImClone(this), im => im._keys = newKeys);
        }
    }
}
