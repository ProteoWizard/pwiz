using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Spectra
{
    public class SpectrumMetadata : Immutable
    {
        private ImmutableList<ImmutableList<SpectrumPrecursor>> _precursorsByMsLevel =
            ImmutableList<ImmutableList<SpectrumPrecursor>>.EMPTY;

        public SpectrumMetadata(string id, double retentionTime)
        {
            Id = id;
            RetentionTime = retentionTime;
        }

        public string Id { get; private set; }
        public double RetentionTime { get; private set; }
        public int MsLevel
        {
            get { return _precursorsByMsLevel.Count + 1; }
        }

        public ImmutableList<SpectrumPrecursor> GetPrecursors(int msLevel)
        {
            if (msLevel < 1 || msLevel > _precursorsByMsLevel.Count)
            {
                return ImmutableList<SpectrumPrecursor>.EMPTY;
            }

            return _precursorsByMsLevel[msLevel - 1];
        }

        public SpectrumMetadata ChangePrecursors(IEnumerable<IEnumerable<SpectrumPrecursor>> precursorsByMsLevel)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im._precursorsByMsLevel = ImmutableList.ValueOf(precursorsByMsLevel.Select(ImmutableList.ValueOf));
            });
        }

        public bool NegativeCharge { get; private set; }

        public SpectrumMetadata ChangeNegativeCharge(bool negativeCharge)
        {
            return ChangeProp(ImClone(this), im => im.NegativeCharge = negativeCharge);
        }

        public string ScanDescription { get; private set; }

        public SpectrumMetadata ChangeScanDescription(string scanDescription)
        {
            if (scanDescription == ScanDescription)
            {
                return this;
            }

            return ChangeProp(ImClone(this), im => im.ScanDescription = scanDescription);
        }

        public double? CollisionEnergy { get; private set; }

        public double? ScanWindowLowerLimit { get; private set; }
        public double? ScanWindowUpperLimit { get; private set; }

        public SpectrumMetadata ChangeScanWindow(double lowerLimit, double upperLimit)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.ScanWindowLowerLimit = ScanWindowLowerLimit;
                im.ScanWindowUpperLimit = ScanWindowUpperLimit;
            });
        }

        public SpectrumMetadata ChangeCollisionEnergy(double? collisionEnergy)
        {
            if (CollisionEnergy == collisionEnergy)
            {
                return this;
            }

            return ChangeProp(ImClone(this), im => im.CollisionEnergy = collisionEnergy);
        }

        protected bool Equals(SpectrumMetadata other)
        {
            return _precursorsByMsLevel.Equals(other._precursorsByMsLevel) && Id == other.Id &&
                   RetentionTime.Equals(other.RetentionTime) && NegativeCharge == other.NegativeCharge &&
                   ScanDescription == other.ScanDescription &&
                   Nullable.Equals(CollisionEnergy, other.CollisionEnergy) &&
                   Nullable.Equals(ScanWindowLowerLimit, other.ScanWindowLowerLimit) &&
                   Nullable.Equals(ScanWindowUpperLimit, other.ScanWindowUpperLimit);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SpectrumMetadata) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = _precursorsByMsLevel.GetHashCode();
                hashCode = (hashCode * 397) ^ Id.GetHashCode();
                hashCode = (hashCode * 397) ^ RetentionTime.GetHashCode();
                hashCode = (hashCode * 397) ^ NegativeCharge.GetHashCode();
                hashCode = (hashCode * 397) ^ ScanDescription.GetHashCode();
                hashCode = (hashCode * 397) ^ CollisionEnergy.GetHashCode();
                hashCode = (hashCode * 397) ^ ScanWindowLowerLimit.GetHashCode();
                hashCode = (hashCode * 397) ^ ScanWindowUpperLimit.GetHashCode();
                return hashCode;
            }
        }
    }
}
