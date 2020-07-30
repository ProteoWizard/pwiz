using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.DocSettings
{
    public class IonRatio
    {
        [Format(Formats.STANDARD_RATIO, NullValue = TextUtil.EXCEL_NA)]
        public double? Value { get; private set; }
        public string Status { get; private set; }

        public static IonRatio MakeIonRatio(double? value, double? targetIonRatio, double? ionRatioThreshold)
        {
            return new IonRatio()
            {
                Value = value,
                Status = GetStatus(value, targetIonRatio, ionRatioThreshold)
            };
        }

        public override string ToString()
        {
            if (!Value.HasValue)
            {
                return "Missing";
            }

            return Status + ":" + Value.Value.ToString(Formats.STANDARD_RATIO);
        }

        public static string GetStatus(double? observedValue, double? targetValue, double? targetThreshold)
        {
            if (!observedValue.HasValue)
            {
                return null;
            }

            if (double.IsNaN(observedValue.Value) || double.IsNaN(observedValue.Value))
            {
                return @"undefined";
            }

            if (!targetValue.HasValue)
            {
                return @"present";
            }

            if (!targetThreshold.HasValue)
            {
                if (observedValue == targetValue)
                {
                    return @"equal";
                }

                if (observedValue < targetValue)
                {
                    return @"low";
                }

                if (observedValue > targetValue)
                {
                    return @"high";
                }
            }

            if (observedValue >= targetValue - targetValue * targetThreshold &&
                observedValue <= targetValue + targetValue * targetThreshold)
            {
                return @"pass";
            }

            return @"fail";
        }

    }
}
