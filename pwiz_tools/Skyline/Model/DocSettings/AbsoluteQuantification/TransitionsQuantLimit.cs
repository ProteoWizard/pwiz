using System.Collections.Generic;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class QuantLimit
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
    }

    public class TransitionsQuantLimit
    {
        public TransitionsQuantLimit(QuantLimit quantLimit, IdentityPath  transitionIdentityPath)
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
