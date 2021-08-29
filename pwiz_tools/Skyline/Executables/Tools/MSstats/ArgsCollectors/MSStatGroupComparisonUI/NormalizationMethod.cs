using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MSStatArgsCollector
{
    public class NormalizationMethod
    {
        public static readonly NormalizationMethod NONE = new NormalizationMethod(() => MSstatsResources.ArgsCollectorForm_GetNormalizationOptionLabels_None, null);

        public static readonly NormalizationMethod EQUALIZE_MEDIANS =
            new NormalizationMethod(() => MSstatsResources.ArgsCollectorForm_GetNormalizationOptionLabels_Equalize_Medians, "equalizeMedians");

        public static readonly NormalizationMethod QUANTILE = new NormalizationMethod(() => MSstatsResources.ArgsCollectorForm_GetNormalizationOptionLabels_Quantile, "quantile");

        public static readonly NormalizationMethod GLOBAL_STANDARDS =
            new NormalizationMethod(() => MSstatsResources.ArgsCollectorForm_GetNormalizationOptionLabels_Global_Standards, "globalStandards");

        private Func<string> _getLabelFunc;
        public NormalizationMethod(Func<string> getLabelFunc, string parameterValue)
        {
            _getLabelFunc = getLabelFunc;
            ParameterValue = parameterValue;
        }

        public string ParameterValue { get; }
        public override string ToString()
        {
            return _getLabelFunc();
        }

        protected bool Equals(NormalizationMethod other)
        {
            return ParameterValue == other.ParameterValue;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((NormalizationMethod) obj);
        }

        public override int GetHashCode()
        {
            return (ParameterValue != null ? ParameterValue.GetHashCode() : 0);
        }
    }
}
