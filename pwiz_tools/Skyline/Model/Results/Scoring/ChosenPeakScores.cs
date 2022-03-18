using System;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Results.Scoring
{
    public class ChosenPeakScores : Immutable
    {
        public ChosenPeakScores(float? detectionZScore, float? detectionQValue)
        {
            DetectionZScore = detectionZScore;
            DetectionQValue = detectionQValue;
        }
        [Flags]
        private enum Flags
        {
            HasDetectionQValue = 1,
            HasDetectionZScore = 2,
            HasPeakScore = 4,
        }
        private Flags _flags;

        private float _detectionQValue;
        private float _detectionZScore;
        private float _peakZScore;
        public float? DetectionQValue
        {
            get { return GetOptional(_detectionQValue, Flags.HasDetectionQValue); }
            set
            {
                _detectionQValue = SetOptional(value, Flags.HasDetectionQValue);
            }
        }
        public float? DetectionZScore
        {
            get { return GetOptional(_detectionZScore, Flags.HasDetectionZScore); }
            set
            {
                _detectionZScore = SetOptional(value, Flags.HasDetectionZScore);
            }
        }

        public ChosenPeakScores ChangeDetectionScore(float? detectionZScore, float? detectionQValue)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.DetectionZScore = detectionZScore;
                im.DetectionQValue = detectionQValue;
            });
        }

        public ChosenPeakScores ChangePeakScore(float peakZScore, ImmutableList<float> detailScores)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.PeakZScore = peakZScore;
                im.DetailScores = detailScores;
            });
        }

        public float? PeakZScore
        {
            get
            {
                return GetOptional(_peakZScore, Flags.HasPeakScore);
            }
            set
            {
                _peakZScore = SetOptional(value, Flags.HasPeakScore);
            }
        }

        public ImmutableList<float> DetailScores { get; private set; }
        private T? GetOptional<T>(T field, Flags flag) where T : struct
        {
            return GetFlag(flag) ? field : (T?)null;
        }

        private T SetOptional<T>(T? value, Flags flag) where T : struct
        {
            SetFlag(flag, value.HasValue);
            return value ?? default(T);
        }

        private bool GetFlag(Flags flag)
        {
            return 0 != (_flags & flag);
        }

        private void SetFlag(Flags flag, bool b)
        {
            if (b)
            {
                _flags |= flag;
            }
            else
            {
                _flags &= ~flag;
            }
        }

        protected bool Equals(ChosenPeakScores other)
        {
            return _flags == other._flags && _detectionQValue.Equals(other._detectionQValue) &&
                   _detectionZScore.Equals(other._detectionZScore) && _peakZScore.Equals(other._peakZScore) &&
                   Equals(DetailScores, other.DetailScores);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ChosenPeakScores) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int) _flags;
                hashCode = (hashCode * 397) ^ _detectionQValue.GetHashCode();
                hashCode = (hashCode * 397) ^ _detectionZScore.GetHashCode();
                hashCode = (hashCode * 397) ^ _peakZScore.GetHashCode();
                hashCode = (hashCode * 397) ^ (DetailScores != null ? DetailScores.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}