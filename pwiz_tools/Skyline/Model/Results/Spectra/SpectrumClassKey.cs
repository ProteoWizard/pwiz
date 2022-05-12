using System;
using System.Linq;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public class SpectrumClassKey : Immutable
    {
        public SpectrumClassKey()
        {
            Ms1Precursors = SpectrumPrecursors.EMPTY;
            Ms2Precursors = SpectrumPrecursors.EMPTY;
        }
        public SpectrumPrecursors Ms1Precursors { get; private set; }

        public SpectrumClassKey ChangeMs1Precursors(SpectrumPrecursors precursors)
        {
            return ChangeProp(ImClone(this), im => im.Ms1Precursors = precursors);
        }
        public SpectrumPrecursors Ms2Precursors { get; private set; }

        public SpectrumClassKey ChangeMs2Precursors(SpectrumPrecursors precursors)
        {
            return ChangeProp(ImClone(this), im => im.Ms2Precursors = precursors);
        }

        public string ScanDescription { get; private set; }
        
        public static SpectrumClassKey FromSpectrumMetadata(SpectrumMetadata metadata)
        {
            var classKey = new SpectrumClassKey
            {
                Ms1Precursors = new SpectrumPrecursors(metadata.GetPrecursors(1)),
                Ms2Precursors = new SpectrumPrecursors(metadata.GetPrecursors(2)),
                ScanDescription = metadata.ScanDescription,
            };
            var collisionEnergies = Enumerable.Range(1, metadata.MsLevel - 1).SelectMany(metadata.GetPrecursors)
                .Select(precursor => precursor.CollisionEnergy).Distinct().ToList();
            if (collisionEnergies.Count == 1)
            {
                classKey = classKey.ChangeCollisionEnergy(collisionEnergies[0]);
            }

            return classKey;
        }

        public double? CollisionEnergy { get; private set; }

        public SpectrumClassKey ChangeCollisionEnergy(double? collisionEnergy)
        {
            return ChangeProp(ImClone(this), im => im.CollisionEnergy = collisionEnergy);
        }

        protected bool Equals(SpectrumClassKey other)
        {
            return Equals(Ms1Precursors, other.Ms1Precursors) && Equals(Ms2Precursors, other.Ms2Precursors) &&
                   ScanDescription == other.ScanDescription && Nullable.Equals(CollisionEnergy, other.CollisionEnergy);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SpectrumClassKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Ms1Precursors.GetHashCode();
                hashCode = (hashCode * 397) ^ Ms2Precursors.GetHashCode();
                hashCode = (hashCode * 397) ^ (ScanDescription != null ? ScanDescription.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ CollisionEnergy.GetHashCode();
                return hashCode;
            }
        }
    }
}
