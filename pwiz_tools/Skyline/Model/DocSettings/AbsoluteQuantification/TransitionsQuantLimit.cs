using System;
using System.Collections.Generic;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Hibernate;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class QuantLimit : IFormattable, IComparable
    {
        public QuantLimit(double lod, double loq)
        {
            Lod = lod;
            Loq = loq;
        }

        public double Lod { get; }
        public double Loq { get; }

        public double GetQuantLimit(OptimizeType optimizeType)
        {
            if (optimizeType == OptimizeType.LOD)
            {
                return Lod;
            }

            return Loq;
        }

        public override string ToString()
        {
            return ToString(Formats.CalibrationCurve, null);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return string.Format(formatProvider, QuantificationStrings.QuantLimit_ToString_LOD___0__LLOQ___1_, Lod.ToString(format, formatProvider),
                Loq.ToString(format, formatProvider));
        }

        public int CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            var that = (QuantLimit)obj;
            int result = Lod.CompareTo(that.Lod);
            if (result == 0)
            {
                result = Loq.CompareTo(that.Loq);
            }

            return result;
        }
    }

    public class TransitionsQuantLimit
    {
        public TransitionsQuantLimit(QuantLimit quantLimit, IdentityPath transitionIdentityPath)
            : this(quantLimit, ImmutableList.Singleton(transitionIdentityPath))
        {
        }

        public TransitionsQuantLimit(QuantLimit quantLimit, IEnumerable<IdentityPath> transitionIdentityPaths)
        {
            QuantLimit = quantLimit;
            TransitionIdentityPaths = ImmutableList.ValueOf(transitionIdentityPaths);
        }
        public QuantLimit QuantLimit { get; }
        public ImmutableList<IdentityPath> TransitionIdentityPaths { get; }

        public double GetQuantLimit(OptimizeType optimizeType)
        {
            return QuantLimit.GetQuantLimit(optimizeType);
        }
    }
}
