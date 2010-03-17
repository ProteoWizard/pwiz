using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Topograph.Util
{
    public static class ConvertHelper
    {
        public static double? ToDbValue(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return null;
            }
            return value;
        }
        public static double FromDbValue(double? value)
        {
            if (value.HasValue)
            {
                return value.Value;
            }
            return double.NaN;
        }
    }
}
