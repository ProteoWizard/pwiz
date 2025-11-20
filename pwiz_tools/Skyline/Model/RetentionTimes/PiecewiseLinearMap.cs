/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.RetentionTimes
{
    public class PiecewiseLinearMap
    {
        public static readonly PiecewiseLinearMap Empty =
            new PiecewiseLinearMap(Array.Empty<double>(), Array.Empty<double>());
        private readonly double[] _x;
        private readonly double[] _y;
        private readonly double[] _xSortedByY;
        private readonly double[] _ySortedByY;

        public static PiecewiseLinearMap FromValues(IEnumerable<KeyValuePair<double, double>> points)
        {
            var pointList = points.Select(pt=>Tuple.Create(pt.Key, pt.Value)).OrderBy(tuple=>tuple).ToList();
            for (int i = 0; i < pointList.Count; i++)
            {
                var point = pointList[i];
                if (double.IsNaN(point.Item1) || double.IsInfinity(point.Item1) || double.IsNaN(point.Item2) ||
                    double.IsInfinity(point.Item2))
                {
                    throw new ArgumentException(string.Format(@"Invalid point {0} at position {1}", point, i), nameof(points));
                }
            }
            return new PiecewiseLinearMap(pointList.Select(pt => pt.Item1).ToArray(),
                pointList.Select(pt => pt.Item2).ToArray());
        }

        public static PiecewiseLinearMap FromValues(IEnumerable<double> xValues, IEnumerable<double> yValues)
        {
            return FromValues(xValues.Zip(yValues, (x, y) => new KeyValuePair<double, double>(x, y)));
        }


        private PiecewiseLinearMap(double[] x, double[] y)
        {
            _x = x;
            _y = y;
            if (x.Length == 0 || Enumerable.Range(0, _y.Length - 1).All(i => _y[i] <= _y[i + 1]))
            {
                _xSortedByY = _x;
                _ySortedByY = _y;
            }
            else
            {
                var reversePointList = _x.Zip(_y, Tuple.Create).OrderBy(pt => pt.Item2).ToList();
                _xSortedByY = reversePointList.Select(pt => pt.Item1).ToArray();
                _ySortedByY = reversePointList.Select(pt => pt.Item2).ToArray();
            }
        }

        public int Count
        {
            get { return _x.Length; }
        }

        public IEnumerable<double> XValues
        {
            get { return _x.AsEnumerable(); }
        }

        public IEnumerable<double> YValues
        {
            get
            {
                return _y.AsEnumerable();
            }
        }

        public double GetY(double x)
        {
            return Interpolate(x, _x, _y);
        }

        public double GetX(double y)
        {
            return Interpolate(y, _ySortedByY, _xSortedByY);
        }

        protected bool Equals(PiecewiseLinearMap other)
        {
            if (!ReferenceEquals(_x, other._x) && !_x.SequenceEqual(other._x))
            {
                return false;
            }

            if (!ReferenceEquals(_y, other._y) && !_y.SequenceEqual(other._y))
            {
                return false;
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((PiecewiseLinearMap)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return CollectionUtil.GetHashCodeDeep(_x) * 397 ^ CollectionUtil.GetHashCodeDeep(_y);
            }
        }

        public AlignmentFunction ToAlignmentFunction(bool forward)
        {
            if (forward)
            {
                return AlignmentFunction.Define(GetY, GetX);
            }
            return AlignmentFunction.Define(GetX, GetY);
        }

        /// <summary>
        /// Reduce the size of the map by removing the points which are most collinear with their
        /// neighbors.
        /// </summary>
        public PiecewiseLinearMap ReducePointCount(int newCount)
        {
            if (newCount >= Count)
            {
                return this;
            }

            if (newCount <= 0)
            {
                return Empty;
            }

            if (newCount == 1)
            {
                return new PiecewiseLinearMap(new[] { (_x[0] + _x[_x.Length - 1]) / 2 },
                    new[] { (_y[0] + _y[_y.Length - 1]) / 2 });
            }

            if (newCount == 2)
            {
                return new PiecewiseLinearMap(new[] { _x[0], _x[_x.Length - 1] }, new[] { _y[0], _y[_y.Length - 1] });
            }

            var points = _x.Zip(_y, Tuple.Create).ToList();
            while (points.Count > newCount)
            {
                bool[] removed = new bool[points.Count];
                int remainingCount = points.Count;
                // Calculate the area of the triangle formed by each point with its two neighbors. Remove the points
                // with the smallest areas because they are most collinear with their neighbors.
                foreach (var areaTuple in Enumerable.Range(1, points.Count - 2).Select(index =>
                                 Tuple.Create(TriangleArea(points[index - 1], points[index], points[index + 1]), index))
                             .OrderBy(tuple => tuple.Item1))
                {
                    var index = areaTuple.Item2;
                    if (removed[index - 1] || removed[index + 1])
                    {
                        // If we find a point where a neighbor has already been removed, we need to recalculate areas
                        break;
                    }
                    removed[index] = true;
                    remainingCount--;
                    if (remainingCount <= newCount)
                    {
                        break;
                    }
                }

                points = Enumerable.Range(0, points.Count).Where(index => !removed[index]).Select(i => points[i]).ToList();
                Assume.AreEqual(remainingCount, points.Count);
            }

            return new PiecewiseLinearMap(points.Select(pt => pt.Item1).ToArray(),
                points.Select(pt => pt.Item2).ToArray());
        }

        private static double TriangleArea(Tuple<double, double> a, Tuple<double, double> b, Tuple<double, double> c)
        {
            return Math.Abs(a.Item1 * (b.Item2 - c.Item2) + b.Item1 * (c.Item2 - a.Item2) +
                            c.Item1 * (a.Item2 - b.Item2))/2;
        }

        private static double Interpolate(double key, double[] keys, double[] values)
        {
            switch (keys.Length)
            {
                case 0:
                    return key;
                case 1:
                    return values[0];
            }

            if (key < keys[0])
            {
                return GetValueForExtremeLeft(key, keys, values);
            }

            if (key > keys[values.Length - 1])
            {
                return GetValueForExtremeRight(key, keys, values);
            }

            int i = Array.BinarySearch(keys, key);
            if (i >= 0)
            {
                return values[i];
            }

            i = ~i;
            return (values[i] * (key - keys[i - 1]) + values[i - 1] * (keys[i] - key)) / (keys[i] - keys[i - 1]);
        }

        private static double GetValueForExtremeLeft(double key, double[] keys, double[] values)
        {
            double min = keys[0];
            var slope = -1.0;
            for (var i = 1; i < keys.Length && slope < 0; i++)
            {
                if (keys[i] == min)
                {
                    continue;
                }
                slope = (values[i] - values[0]) / (keys[i] - min);
            }
            return values[0] - slope * (min - key);
        }

        private static double GetValueForExtremeRight(double key, double[] keys, double[] values)
        {
            double max = keys[keys.Length - 1];
            var slope = -1.0;
            for (int i = keys.Length - 2; i >= 0 && slope < 0; i--)
            {
                if (keys[i] == max)
                {
                    continue;
                }
                slope = (values[values.Length - 1] - values[i]) / (max - keys[i]);
            }
            return values[values.Length - 1] + slope * (key - max);
        }

        public PiecewiseLinearMap RemoveOutOfOrder()
        {
            if (ReferenceEquals(_x, _xSortedByY))
            {
                return this;
            }

            int n = _y.Length;
            int[] dp = new int[n];
            int[] prev = new int[n];

            for (int i = 0; i < n; i++)
            {
                dp[i] = 1;
                prev[i] = -1;
            }

            int maxLength = 0;
            int bestEnd = -1;

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    if (_y[j].CompareTo(_y[i]) <= 0 && dp[j] + 1 > dp[i])
                    {
                        dp[i] = dp[j] + 1;
                        prev[i] = j;
                    }
                }

                if (dp[i] > maxLength)
                {
                    maxLength = dp[i];
                    bestEnd = i;
                }
            }

            List<int> indexesToKeep = new List<int>();
            int index = bestEnd;
            while (index != -1)
            {
                indexesToKeep.Add(index);
                index = prev[index];
            }

            indexesToKeep.Reverse();
            return FromValues(indexesToKeep.Select(i => new KeyValuePair<double, double>(_x[i], _y[i])));
        }
    }
}
