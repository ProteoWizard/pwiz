using System;
using System.Collections;
using System.Drawing;
using System.Linq;
using pwiz.Common.DataAnalysis.Clustering;

namespace pwiz.Common.Colors
{
    public class NumericColorScheme : IColorScheme
    {
        public double MaxValue { get; set; }
        public double MinValue { get; set; }

        public void Calibrate(IEnumerable values)
        {
            double maxValue = 0;
            double minValue = 0;
            foreach (var value in values.OfType<object>().Select(ToDouble).OfType<double>())
            {
                maxValue = Math.Max(value, maxValue);
                minValue = Math.Min(value, minValue);
            }

            MaxValue = maxValue;
            MinValue = minValue;
        }

        public Color? GetColor(object value)
        {
            var doubleValue = ToDouble(value);
            if (!doubleValue.HasValue)
            {
                return null;
            }

            int lightness;
            double maxAbsValue = Math.Max(MaxValue, MinValue);
            if (Math.Abs(doubleValue.Value) < maxAbsValue)
            {
                lightness = (int)((maxAbsValue - Math.Abs(doubleValue.Value)) * (255 / maxAbsValue));
            }
            else
            {
                lightness = 0;
            }

            if (doubleValue >= 0)
            {
                return Color.FromArgb(255, lightness, lightness);
            }
            else
            {
                return Color.FromArgb(lightness, lightness, 255);
            }
        }

        public static double? ToDouble(object value)
        {
            return ZScores.ToDouble(value);
        }
    }
}
