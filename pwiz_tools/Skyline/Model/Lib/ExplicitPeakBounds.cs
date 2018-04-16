using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Lib
{
    public class ExplicitPeakBounds : Immutable
    {
        public const double UNKNOWN_SCORE = double.NaN;

        public ExplicitPeakBounds(double startTime, double endTime, double score)
        {
            StartTime = startTime;
            EndTime = endTime;
            Score = score;
        }
        public double StartTime { get; private set; }
        public double EndTime { get; private set; }
        public double Score { get; private set; }

        protected bool Equals(ExplicitPeakBounds other)
        {
            return StartTime.Equals(other.StartTime) && EndTime.Equals(other.EndTime) && Score.Equals(other.Score);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ExplicitPeakBounds) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = StartTime.GetHashCode();
                hashCode = (hashCode * 397) ^ EndTime.GetHashCode();
                hashCode = (hashCode * 397) ^ Score.GetHashCode();
                return hashCode;
            }
        }
    }
}
