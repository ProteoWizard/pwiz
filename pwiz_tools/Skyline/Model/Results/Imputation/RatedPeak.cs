using System;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.RetentionTimes;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class RatedPeak : Immutable
    {
        public RatedPeak(ReplicateFileInfo resultFileInfo, AlignmentFunction alignmentFunction, PeakBounds rawPeakBounds, double? score, bool manuallyIntegrated)
        {
            ReplicateFileInfo = resultFileInfo;
            AlignmentFunction = alignmentFunction;
            RawPeakBounds = rawPeakBounds;
            AlignedPeakBounds = rawPeakBounds?.Align(alignmentFunction);
            ManuallyIntegrated = manuallyIntegrated;
            Score = score;
        }

        public ReplicateFileInfo ReplicateFileInfo { get; }
        public PeakBounds RawPeakBounds { get; }

        public PeakBounds AlignedPeakBounds { get; private set; }

        public double? Score { get; private set; }

        public RatedPeak ChangeScore(double? value)
        {
            return ChangeProp(ImClone(this), im => im.Score = value);
        }

        public bool ManuallyIntegrated { get; }
        public double? Percentile { get; private set; }

        public RatedPeak ChangePercentile(double? value)
        {
            return ChangeProp(ImClone(this), im => im.Percentile = value);
        }

        public double? PValue { get; private set; }

        public RatedPeak ChangePValue(double? value)
        {
            return ChangeProp(ImClone(this), im => im.PValue = value);
        }

        public double? QValue { get; private set; }

        public RatedPeak ChangeQValue(double? value)
        {
            return ChangeProp(ImClone(this), im => im.QValue = value);
        }

        public Verdict PeakVerdict { get; private set; }

        public string Opinion { get; private set; }

        public RatedPeak ChangeVerdict(Verdict verdict, string opinion)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.PeakVerdict = verdict;
                im.Opinion = opinion;
            });
        }

        public double? RtShift { get; private set; }

        public RatedPeak ChangeRtShift(double? value)
        {
            return ChangeProp(ImClone(this), im => im.RtShift = value);
        }

        public AlignmentFunction AlignmentFunction { get; }

        public class PeakBounds : IFormattable
        {
            public PeakBounds(double startTime, double endTime)
            {
                StartTime = startTime;
                EndTime = endTime;
            }

            public double StartTime { get; }
            public double EndTime { get; }
            public double MidTime
            {
                get { return (StartTime + EndTime) / 2; }
            }
            public double Width
            {
                get { return EndTime - StartTime; }
            }

            protected bool Equals(PeakBounds other)
            {
                return StartTime.Equals(other.StartTime) && EndTime.Equals(other.EndTime);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((PeakBounds)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (StartTime.GetHashCode() * 397) ^ EndTime.GetHashCode();
                }
            }

            public PeakBounds Align(AlignmentFunction alignmentFunction)
            {
                if (alignmentFunction == null)
                {
                    return this;
                }

                return new PeakBounds(alignmentFunction.GetY(StartTime), alignmentFunction.GetY(EndTime));
            }

            public PeakBounds AlignPreservingWidth(AlignmentFunction alignmentFunction)
            {
                if (alignmentFunction == null)
                {
                    return this;
                }

                var newMidPoint = alignmentFunction.GetY(MidTime);
                return new PeakBounds(newMidPoint - Width / 2, newMidPoint + Width / 2);
            }

            public PeakBounds ReverseAlign(AlignmentFunction alignmentFunction)
            {
                if (alignmentFunction == null)
                {
                    return this;
                }

                return new PeakBounds(alignmentFunction.GetX(StartTime), alignmentFunction.GetX(EndTime));
            }

            public PeakBounds ReverseAlignPreservingWidth(AlignmentFunction alignmentFunction)
            {
                if (alignmentFunction == null)
                {
                    return this;
                }

                var newMidPoint = alignmentFunction.GetX(MidTime);
                return new PeakBounds(newMidPoint - Width / 2, newMidPoint + Width / 2);
            }

            public override string ToString()
            {
                return string.Format(@"[{0},{1}]", StartTime.ToString(Formats.RETENTION_TIME),
                    EndTime.ToString(Formats.RETENTION_TIME));
            }

            public string ToString(string format, IFormatProvider formatProvider)
            {
                return string.Format(@"[{0},{1}]", StartTime.ToString(format, formatProvider),
                    EndTime.ToString(format, formatProvider));
            }
        }

        public enum Verdict
        {
            Unknown,
            NeedsRemoval,
            NeedsAdjustment,
            Accepted,
            Exemplary
        }
    }
}