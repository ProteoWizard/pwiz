using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace pwiz.Topograph.MsData
{
    [XmlRoot("half_life_settings")]
    public struct HalfLifeSettings
    {
        public static readonly HalfLifeSettings Default = new HalfLifeSettings
                                                     {
                                                         HalfLifeCalculationType =
                                                             HalfLifeCalculationType.GroupPrecursorPool,
                                                     };
        public bool HoldInitialTracerPercentConstant { get; set; }
        public HalfLifeCalculationType HalfLifeCalculationType { get; set; }
        public EvviesFilterEnum EvviesFilter { get; set; }
        public double MinimumAuc { get; set; }
        public double MinimumDeconvolutionScore { get; set; }
        public double MinimumTurnoverScore { get; set; }
        public bool ByProtein { get; set; }
        public bool BySample { get; set; }

        public bool Equals(HalfLifeSettings other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return other.HoldInitialTracerPercentConstant.Equals(HoldInitialTracerPercentConstant) && Equals(other.HalfLifeCalculationType, HalfLifeCalculationType) && Equals(other.EvviesFilter, EvviesFilter) && other.MinimumAuc.Equals(MinimumAuc) && other.MinimumDeconvolutionScore.Equals(MinimumDeconvolutionScore) && other.MinimumTurnoverScore.Equals(MinimumTurnoverScore) && other.ByProtein.Equals(ByProtein) && other.BySample.Equals(BySample);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (HalfLifeSettings)) return false;
            return Equals((HalfLifeSettings) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = HoldInitialTracerPercentConstant.GetHashCode();
                result = (result*397) ^ HalfLifeCalculationType.GetHashCode();
                result = (result*397) ^ EvviesFilter.GetHashCode();
                result = (result*397) ^ MinimumAuc.GetHashCode();
                result = (result*397) ^ MinimumDeconvolutionScore.GetHashCode();
                result = (result*397) ^ MinimumTurnoverScore.GetHashCode();
                result = (result*397) ^ ByProtein.GetHashCode();
                result = (result*397) ^ BySample.GetHashCode();
                return result;
            }
        }

        public static double TryParseDouble(string strValue, double defaultValue)
        {
            double value;
            if (string.IsNullOrEmpty(strValue) || !double.TryParse(strValue, out value))
            {
                return defaultValue;
            }
            return value;
        }
    }
}
