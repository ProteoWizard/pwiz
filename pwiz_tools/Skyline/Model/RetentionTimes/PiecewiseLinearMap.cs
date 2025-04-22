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

namespace pwiz.Skyline.Model.RetentionTimes
{
    public class PiecewiseLinearMap
    {
        private readonly double[] _x;
        private readonly double[] _y;
        private readonly double[] _xSortedByY;
        private readonly double[] _ySortedByY;

        public static PiecewiseLinearMap FromValues(IEnumerable<KeyValuePair<double, double>> points)
        {
            var pointList = points.Select(pt=>Tuple.Create(pt.Key, pt.Value)).OrderBy(tuple=>tuple).ToList();
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
            if (Enumerable.Range(0, _y.Length - 1).All(i => _y[i] <= _y[i + 1]))
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

        private static double Interpolate(double key, double[] keys, double[] values)
        {
            switch (keys.Length)
            {
                case 0:
                    return key;
                case 1:
                    return values[0];
            }

            int i = Array.BinarySearch(keys, key);
            if (i >= 0)
            {
                return values[i];
            }

            i = ~i;
            int prev = Math.Min(Math.Max(0, i - 1), keys.Length - 2);
            int next = prev + 1;
            double xPrev = keys[prev];
            double xNext = keys[next];
            if (xPrev == xNext)
            {
                if (xPrev == 0)
                {
                    return values[0];
                }
                else
                {
                    return values[values.Length - 1];
                }
            }

            return (values[prev] * (key - xPrev) + values[next] * (xNext - key)) / (xNext - xPrev);
        }
    }
}
