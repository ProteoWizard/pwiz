using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Hibernate;
using System;
using System.Globalization;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Util
{
    public class ScoredPeakValue : IFormattable
    {
        public ScoredPeakValue(double? retentionTime, double startTime, double endTime, double? score)
        {
            RetentionTime = retentionTime;
            StartTime = startTime;
            EndTime = endTime;
            Score = score;
        }
        [Format(Formats.RETENTION_TIME)]
        public double? RetentionTime { get; }

        [Format(Formats.RETENTION_TIME)]
        public double StartTime { get; }
        [Format(Formats.RETENTION_TIME)]
        public double EndTime { get; }

        [Format(Formats.PEAK_SCORE)]
        public double? Score { get; }

        public override string ToString()
        {
            return ToString(Formats.RETENTION_TIME, CultureInfo.CurrentCulture);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return string.Format(EntitiesResources.CandidatePeakGroup_ToString___0___1__,
                StartTime.ToString(format, formatProvider), EndTime.ToString(format, formatProvider));
        }

        public static ScoredPeakValue FromScoredPeak(ScoredPeak scoredPeak)
        {
            if (scoredPeak == null)
            {
                return null;
            }

            return new ScoredPeakValue(scoredPeak.ApexTime, scoredPeak.StartTime, scoredPeak.EndTime, scoredPeak.Score);
        }
    }
}
