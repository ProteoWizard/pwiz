using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.RetentionTimes
{
    public abstract class AlignmentFunction
    {
        public static readonly AlignmentFunction IDENTITY = new Compound(ImmutableList.Empty<AlignmentFunction>());
        public static AlignmentFunction Create(Func<double, double> forward, Func<double, double> reverse)
        {
            return new Impl(forward, reverse);
        }

        public static AlignmentFunction FromParts(IEnumerable<AlignmentFunction> parts)
        {
            var partsList = ImmutableList.ValueOf(parts);
            if (partsList.Count == 0)
            {
                return IDENTITY;
            }
            if (partsList.Count == 1)
            {
                return partsList[0];
            }

            return new Compound(partsList);
        }

        public abstract double GetY(double x);
        public abstract double GetX(double y);

        private class Impl : AlignmentFunction
        {
            private Func<double, double> _forward;
            private Func<double, double> _reverse;
            public Impl(Func<double, double> forward, Func<double, double> reverse)
            {
                _forward = forward;
                _reverse = reverse;
            }

            public override double GetY(double x)
            {
                return _forward(x);
            }

            public override double GetX(double y)
            {
                return _reverse(y);
            }
        }

        public class Compound : AlignmentFunction
        {
            public Compound(IEnumerable<AlignmentFunction> alignmentFunctions)
            {
                Parts = ImmutableList.ValueOf(alignmentFunctions.SelectMany(part=>(part as Compound)?.Parts ?? ImmutableList.Singleton(part)));
            }
            public ImmutableList<AlignmentFunction> Parts { get; }
            public override double GetY(double x)
            {
                return Parts.Aggregate(x, (v, part) => part.GetY(v));
            }

            public override double GetX(double y)
            {
                return Parts.Reverse().Aggregate(y, (v, part) => part.GetX(v));
            }
        }
    }
}
