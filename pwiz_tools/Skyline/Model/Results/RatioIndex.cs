using System;
using System.Collections.Generic;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;

namespace pwiz.Skyline.Model.Results
{
    public struct RatioIndex
    {
        public static readonly RatioIndex NONE = new RatioIndex();
        public static readonly RatioIndex GLOBAL_STANDARD = new RatioIndex {_value = -1};
        public static readonly RatioIndex NORMALIZED = new RatioIndex {_value = -2};
        public static readonly RatioIndex CALIBRATED = new RatioIndex {_value = -3};

        private int _value;

        public int? InternalStandardIndex
        {
            get
            {
                if (_value > 0)
                {
                    return _value - 1;
                }

                return null;
            }
        }

        public static RatioIndex FromInternalStandardIndex(int internalStandardIndex)
        {
            if (internalStandardIndex < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            return new RatioIndex {_value = internalStandardIndex + 1};
        }

        public static IEnumerable<RatioIndex> AvailableRatioIndexes(SrmDocument document)
        {
            yield return NONE;
            if (document.Settings.HasGlobalStandardArea)
            {
                yield return GLOBAL_STANDARD;
            }

            if (!Equals(document.Settings.PeptideSettings.Quantification.NormalizationMethod, NormalizationMethod.NONE))
            {
                yield return NORMALIZED;
            }
            if (document.Settings.PeptideSettings.Quantification.RegressionFit != RegressionFit.NONE)
            {
                yield return CALIBRATED;
            }

            int internalStandardCount = document.Settings.PeptideSettings.Modifications.InternalStandardTypes.Count;
            for (int i = 0; i < internalStandardCount; i++)
            {
                yield return FromInternalStandardIndex(i);
            }
        }

        public bool Equals(RatioIndex other)
        {
            return _value == other._value;
        }

        public override bool Equals(object obj)
        {
            return obj is RatioIndex other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _value;
        }

        public static bool operator ==(RatioIndex left, RatioIndex right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RatioIndex left, RatioIndex right)
        {
            return !left.Equals(right);
        }
    }
}
