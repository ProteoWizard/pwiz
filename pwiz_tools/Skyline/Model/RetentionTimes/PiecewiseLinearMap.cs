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

        public static PiecewiseLinearMap FromValues(IEnumerable<KeyValuePair<double, double>> points)
        {
            var pointList = points.Select(pt=>Tuple.Create(pt.Key, pt.Value)).OrderBy(tuple=>tuple).ToList();
            return new PiecewiseLinearMap(pointList.Select(pt => pt.Item1).ToArray(),
                pointList.Select(pt => pt.Item2).ToArray());
        }

        public static PiecewiseLinearMap FromValues(IEnumerable<double> x, IEnumerable<double> y)
        {
            return FromValues(x.Zip(y, (x, y) => new KeyValuePair<double, double>(x, y)));
        }


        private PiecewiseLinearMap(double[] x, double[] y)
        {
            _x = x;
            _y = y;
        }

        public PiecewiseLinearMap ReverseMap()
        {
            if (Enumerable.Range(0, _y.Length - 1).All(i => _y[i] <= _y[i + 1]))
            {
                return new PiecewiseLinearMap(_y, _x);
            }

            return FromValues(_y.Zip(_x, (y, x) => new KeyValuePair<double, double>(y, x)));
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
            switch (Count)
            {
                case 0:
                    return x;
                case 1:
                    return _y[0];
            }

            int i = Array.BinarySearch(_x, x);
            if (i >= 0)
            {
                return _y[i];
            }

            i = ~i;
            int prev = Math.Min(Math.Max(0, i - 1), _x.Length - 2);
            int next = prev + 1;
            double xPrev = _x[prev];
            double xNext = _x[next];
            if (xPrev == xNext)
            {
                if (xPrev == 0)
                {
                    return _y[0];
                }
                else
                {
                    return _y[_y.Length - 1];
                }
            }

            return (_y[prev] * (x - xPrev) + _y[next] * (xNext - x)) / (xNext - xPrev);
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

        public ReversibleMap ToReversibleMap()
        {
            return new ReversibleMap(this);
        }

        public AlignmentFunction ToAlignmentFunction()
        {
            return AlignmentFunction.Define(GetY, ReverseMap().GetY);
        }
    }

    public class ReversibleMap
    {
        public ReversibleMap(PiecewiseLinearMap forwardMap)
        {
            ForwardMap = forwardMap;
            ReverseMap = forwardMap.ReverseMap();
        }

        public PiecewiseLinearMap ForwardMap { get; }
        public PiecewiseLinearMap ReverseMap { get; }

        public AlignmentFunction GetAlignmentFunction(bool forward)
        {
            if (forward)
            {
                return AlignmentFunction.Define(ForwardMap.GetY, ReverseMap.GetY);
            }
            else
            {
                return AlignmentFunction.Define(ReverseMap.GetY, ForwardMap.GetY);
            }
        }
    }
}
