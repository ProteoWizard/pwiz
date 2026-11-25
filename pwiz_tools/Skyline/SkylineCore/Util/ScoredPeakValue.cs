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

        public static ScoredPeakValue FromScoredPeak(ScoredPeakBounds scoredPeak)
        {
            if (scoredPeak == null)
            {
                return null;
            }

            return new ScoredPeakValue(scoredPeak.ApexTime, scoredPeak.StartTime, scoredPeak.EndTime, scoredPeak.Score);
        }
    }

    public class SourcedPeakValue
    {
        [Format(Formats.RETENTION_TIME)]
        public double? RetentionTime { get; private set; }

        [Format(Formats.RETENTION_TIME)]
        public double StartTime { get; private set; }
        [Format(Formats.RETENTION_TIME)]
        public double EndTime { get; private set; }

        [Format(Formats.PEAK_SCORE)]
        public double? Score { get; private set; }

        public string ReplicateName { get; private set; }
        public string LibraryName { get; private set; }
        public string FilePath { get; private set; }

        public override string ToString()
        {
            return ToString(Formats.RETENTION_TIME, CultureInfo.CurrentCulture);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return string.Format(EntitiesResources.CandidatePeakGroup_ToString___0___1__,
                StartTime.ToString(format, formatProvider), EndTime.ToString(format, formatProvider));
        }

        public static SourcedPeakValue FromSourcedPeak(SourcedPeak sourcedPeak)
        {
            if (sourcedPeak == null)
            {
                return null;
            }
            return new SourcedPeakValue
            {
                RetentionTime = sourcedPeak.Peak.ApexTime,
                StartTime = sourcedPeak.Peak.StartTime,
                EndTime = sourcedPeak.Peak.EndTime,
                FilePath = sourcedPeak.Source.FilePath,
                LibraryName = sourcedPeak.Source.LibraryName,
                ReplicateName = sourcedPeak.Source.ReplicateName
            };
        }
    }
}
