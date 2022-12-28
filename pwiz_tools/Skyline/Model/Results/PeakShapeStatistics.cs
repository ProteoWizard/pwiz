using System;
using System.Collections.Generic;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public class PeakShapeStatistics
    {
        public static PeakShapeStatistics Calculate(IList<double> times, IList<double> intensities)
        {
            int count = times.Count;
            Assume.AreEqual(count, intensities.Count);
            if (count == 0)
            {
                return null;
            }

            if (count == 1)
            {
                return new PeakShapeStatistics()
                {
                    MeanTime = times[0],
                    MedianTime = times[0],
                    StdDevTime = double.NaN
                };
            }
            double totalArea = 0;
            double totalTimeTimesArea = 0;
            for (int i = 0; i < count - 1; i++)
            {
                var time1 = times[i];
                var time2 = times[i + 1];
                var width = time2 - time1;
                var intensity1 = intensities[i];
                var intensity2 = intensities[i + 1];
                var area = (intensity1 + intensity2) * width / 2;
                if (area <= 0)
                {
                    continue;
                }
                var slope = (intensity2 - intensity1) / width;
                var meanTimeThisSlice = time1 + (intensity1 / 2 + slope * width / 3) * width * width / area; 
                totalTimeTimesArea += meanTimeThisSlice * area;
                totalArea += area;
            }

            var meanTime = totalTimeTimesArea / totalArea;
            double medianTime = FindTimeWithAccumulatedArea(times, intensities, totalArea / 2);
            double stdDev = Math.Sqrt(GetTotalVariance(times, intensities, meanTime) / totalArea);
            double skewness = CalculateTotalSkewness(times, intensities, meanTime) / Math.Pow(stdDev, 3) / totalArea;
            double kurtosis = CalculateTotalKurtosis(times, intensities, meanTime) / Math.Pow(stdDev, 4) / totalArea - 3;
            return new PeakShapeStatistics
            {
                Area = totalArea,
                MeanTime = meanTime,
                MedianTime = medianTime,
                StdDevTime = stdDev,
                Skewness = skewness,
                Kurtosis = kurtosis
            };
        }

        public static PeakShapeStatistics CalculateFromTimeIntensities(TimeIntensities timeIntensities, int startIndex,
            int endIndex, float backgroundLevel)
        {
            if (endIndex < startIndex)
            {
                return null;
            }

            var times = new List<double>(endIndex - startIndex + 1);
            var intensities = new List<double>(endIndex - startIndex + 1);
            for (int i = startIndex; i <= endIndex; i++)
            {
                times.Add(timeIntensities.Times[i]);
                intensities.Add(Math.Max(0, timeIntensities.Intensities[i] - backgroundLevel));
            }

            return Calculate(times, intensities);
        }

        private PeakShapeStatistics()
        {

        }

        public double Area { get; private set; }
        public double MeanTime { get; private set; }
        public double MedianTime { get; private set; }
        public double StdDevTime { get; private set; }

        public double Skewness
        {
            get; private set;
        }

        public double Kurtosis { get; private set; }

        private static double FindTimeWithAccumulatedArea(IList<double> times, IList<double> intensities, double targetArea)
        {
            int count = times.Count;
            Assume.AreEqual(count, intensities.Count);
            if (targetArea <= 0)
            {
                return times[0];
            }
            double accumulatedArea = 0;
            for (int i = 0; i < count - 1; i++)
            {
                var time1 = times[i];
                var time2 = times[i + 1];
                var intensity1 = intensities[i];
                var intensity2 = intensities[i + 1];
                var width = time2 - time1;
                var area = (intensity1 + intensity2) * width / 2;
                if (accumulatedArea + area >= targetArea)
                {
                    var midArea = targetArea - accumulatedArea;
                    double slope = (intensity2 - intensity1) / width;
                    if (slope == 0)
                    {
                        var fraction = midArea / area;
                        return time1 * (1- fraction) + time2 * fraction;
                    }
                    // midArea = x * intensity1 + slope * x * x / 2
                    var x = (-intensity1 + Math.Sqrt(intensity1 * intensity1 + 2 * slope * midArea)) / slope;
                    return time1 + x;
                }

                accumulatedArea += area;
            }

            return double.NaN;
        }

        private static double GetTotalVariance(IList<double> times, IList<double> intensities, double mean)
        {
            double totalVariance = 0;
            for (int i = 0; i < times.Count - 1; i++)
            {
                var time1 = times[i];
                var time2 = times[i + 1];
                var intensity1 = intensities[i];
                var intensity2 = intensities[i + 1];
                var width = time2 - time1;
                var slope = (intensity2 - intensity1) / width;
                // Calculate the integral from time1 to time2 of:
                // (x - mean) ^ 2 * (intensity1 + slope * (x - time1)) dx
                var lowerIntegral = Math.Pow(time1 - mean, 3) *
                    (slope * (3 * time1 + mean - 4 * time1) + 4 * intensity1) / 12;
                var upperIntegral = Math.Pow(time2 - mean, 3) *
                    (slope * (3 * time2 + mean - 4 * time1) + 4 * intensity1) / 12;
                totalVariance += upperIntegral - lowerIntegral;
            }

            return totalVariance;
        }

        /// <summary>
        /// Calculate an intermediate value that is used to determine the skewness.
        /// The value being calculated is the integral of (x - mean)^3.
        /// In order to turn the value returned from this function into the actual skewness value, you need to
        /// divide by (area * stdDev^3).
        /// </summary>
        private static double CalculateTotalSkewness(IList<double> times, IList<double> intensities, double mean)
        {
            double totalSkewness = 0;
            for (int i = 0; i < times.Count - 1; i++)
            {
                var time1 = times[i];
                var time2 = times[i + 1];
                var intensity1 = intensities[i];
                var intensity2 = intensities[i + 1];
                var width = time2 - time1;
                var slope = (intensity2 - intensity1) / width;
                // Calculate the integral from time1 to time2 of:
                // (x-mean)^3 * (intensity1 + slope * (x - time1)) dx
                var lowerIntegral = Math.Pow(time1 - mean, 4) *
                    (slope * (4 * time1 + mean - 5 * time1) + 5 * intensity1) / 20;
                var upperIntegral = Math.Pow(time2 - mean, 4) *
                    (slope * (4 * time2 + mean - 5 * time1) + 5 * intensity1) / 20;
                totalSkewness += upperIntegral - lowerIntegral;
            }

            return totalSkewness;
        }

        /// <summary>
        /// Calculate an intermediate value that is used to determine the kurtosis.
        /// The value being calculated is the integral of (x - mean)^4.
        /// In order to turn the value returned from this function into the actual kurtosis value, you need to
        /// divide by (area * stdDev^3) and then subtract 3.
        /// </summary>
        private static double CalculateTotalKurtosis(IList<double> times, IList<double> intensities, double mean)
        {
            double totalKurtosis = 0;
            for (int i = 0; i < times.Count - 1; i++)
            {
                var time1 = times[i];
                var time2 = times[i + 1];
                var intensity1 = intensities[i];
                var intensity2 = intensities[i + 1];
                var width = time2 - time1;
                var slope = (intensity2 - intensity1) / width;
                // Calculate the integral from time1 to time2 of:
                // (x-mean)^4 * (intensity1 + slope * (x - time1)) dx
                var lowerIntegral = Math.Pow(time1 - mean, 5) *
                    (slope * (5 * time1 + mean - 6 * time1) + 6 * intensity1) / 30;
                var upperIntegral = Math.Pow(time2 - mean, 5) *
                    (slope * (5 * time2 + mean - 6 * time1) + 6 * intensity1) / 30;
                totalKurtosis += upperIntegral - lowerIntegral;
            }

            return totalKurtosis;
        }
    }
}
