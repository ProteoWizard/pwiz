using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using MathNet.Numerics.Statistics;

namespace pwiz.Common.DataAnalysis.Clustering
{
    public static class ZScores
    {
        public static bool IsNumericType(Type type)
        {
            return type == typeof(double) || type == typeof(double?) || type == typeof(float) || type == typeof(float?);
        }

        public static double? ToDouble(object value)
        {
            if (value is double doubleValue)
            {
                return doubleValue;
            }

            if (value is float floatValue)
            {
                return floatValue;
            }

            return null;
        }

        private static bool IsValid(double? value)
        {
            return value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value);
        }

        public static IEnumerable<double?> CalculateZScores(IEnumerable<double?> doubleValues)
        {
            var doubleValuesList = doubleValues.ToList();
            var validValues = doubleValuesList.Where(IsValid).ToList();
            double stdDev = 0;
            if (validValues.Count > 1)
            {
                stdDev = validValues.StandardDeviation();
            }
            if (stdDev == 0)
            {
                return doubleValuesList.Select(val => val.HasValue ? (double?) 0 : null);
            }

            var mean = validValues.Mean();
            return doubleValuesList.Select(val => val.HasValue ? (double?) (val.Value - mean) / stdDev : null);
        }

        public static IEnumerable<double?> CalculateZScores(IEnumerable<object> values)
        {
            return CalculateZScores(values.Select(ToDouble));
        }

        public static Color ZScoreToColor(double zScore)
        {
            Color backColor;
            // zScores closer to 0 have lighter colors than the extremes
            var lightness = (int)(Math.Max(0, 4 - Math.Abs(zScore)) * (255 / 4.0));
            if (zScore >= 0)
            {
                return Color.FromArgb(255, lightness, lightness);
            }
            else
            {
                return Color.FromArgb(lightness, lightness, 255);
            }
        }
    }
}
