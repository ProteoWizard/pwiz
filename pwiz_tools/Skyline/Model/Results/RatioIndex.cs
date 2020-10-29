using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using pwiz.Skyline.Model.DocSettings;
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
        public static readonly RatioIndex DEFAULT = FromInternalStandardIndex(0);
        private static readonly Dictionary<RatioIndex, string> _ratioIndexToName = new Dictionary<RatioIndex, string>
        {
            {GLOBAL_STANDARD, @"global_standard"},
            {NORMALIZED, @"normalized"},
            {CALIBRATED, @"calibrated"}
        };

        private static readonly Dictionary<string, RatioIndex> _nameToRatioIndex =
            _ratioIndexToName.ToDictionary(entry => entry.Value, entry => entry.Key);

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

        public string PersistedName
        {
            get
            {
                if (InternalStandardIndex.HasValue)
                {
                    return InternalStandardIndex.Value.ToString(CultureInfo.InvariantCulture);
                }

                string name;
                if (_ratioIndexToName.TryGetValue(this, out name))
                {
                    return name;
                }

                return string.Empty;
            }
        }

        public static RatioIndex FromPersistedName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return NONE;
            }

            RatioIndex ratioIndex;
            if (_nameToRatioIndex.TryGetValue(name, out ratioIndex))
            {
                return ratioIndex;
            }

            return FromInternalStandardIndex(int.Parse(name, CultureInfo.InvariantCulture));
        }

        public static RatioIndex FromInternalStandardIndex(int internalStandardIndex)
        {
            if (internalStandardIndex < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            return new RatioIndex {_value = internalStandardIndex + 1};
        }

        public static IEnumerable<RatioIndex> AvailableRatioIndexes(SrmSettings settings)
        {
            yield return NONE;
            if (settings.HasGlobalStandardArea)
            {
                yield return GLOBAL_STANDARD;
            }

            if (!Equals(settings.PeptideSettings.Quantification.NormalizationMethod, NormalizationMethod.NONE))
            {
                yield return NORMALIZED;
            }
            if (settings.PeptideSettings.Quantification.RegressionFit != RegressionFit.NONE)
            {
                yield return CALIBRATED;
            }

            int internalStandardCount = settings.PeptideSettings.Modifications.RatioInternalStandardTypes.Count;
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

        public RatioIndex Constrain(SrmSettings settings)
        {
            var ratioIndex = this;
            if (ratioIndex == GLOBAL_STANDARD && !settings.HasGlobalStandardArea)
            {
                ratioIndex = DEFAULT;
            }
            if (!ratioIndex.InternalStandardIndex.HasValue)
            {
                return this;
            }

            var mods = settings.PeptideSettings.Modifications.RatioInternalStandardTypes;
            if (mods.Count == 0)
            {
                return NONE;
            }

            if (mods.Count <= InternalStandardIndex.Value)
            {
                return FromInternalStandardIndex(mods.Count - 1);
            }

            return this;
        }
    }
}
