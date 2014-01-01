using System;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model.Data
{
    public class PsmKey : IComparable<PsmKey>
    {
        public PsmKey(string modifiedSequence, double? precursorMz, int? precursorCharge)
        {
            ModifiedSequence = modifiedSequence;
            PrecursorMz = precursorMz;
            PrecursorCharge = precursorCharge;
        }
        public PsmKey(DbPeptideSpectrumMatch dbPeptideSpectrumMatch)
        {
            ModifiedSequence = dbPeptideSpectrumMatch.ModifiedSequence;
            PrecursorMz = dbPeptideSpectrumMatch.PrecursorMz;
            PrecursorCharge = dbPeptideSpectrumMatch.PrecursorCharge;
        }

        public string ModifiedSequence { get; private set; }
        public double? PrecursorMz { get; private set; }
        public int? PrecursorCharge { get; private set; }

        protected bool Equals(PsmKey other)
        {
            return string.Equals(ModifiedSequence, other.ModifiedSequence) 
                && PrecursorMz.Equals(other.PrecursorMz) 
                && PrecursorCharge == other.PrecursorCharge;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((PsmKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (ModifiedSequence != null ? ModifiedSequence.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ PrecursorMz.GetHashCode();
                hashCode = (hashCode*397) ^ PrecursorCharge.GetHashCode();
                return hashCode;
            }
        }
        public int CompareTo(PsmKey psmKey)
        {
            int result = CompareTo(psmKey.ModifiedSequence);
            if (0 != result)
            {
                return result;
            }
            if (PrecursorMz.HasValue)
            {
                result = psmKey.PrecursorMz.HasValue ? PrecursorMz.Value.CompareTo(psmKey.PrecursorMz.Value) : 1;
            }
            else
            {
                result = psmKey.PrecursorMz.HasValue ? -1 : 0;
            }
            if (0 != result)
            {
                return result;
            }
            if (PrecursorCharge.HasValue)
            {
                result = psmKey.PrecursorCharge.HasValue ? PrecursorCharge.Value.CompareTo(psmKey.PrecursorCharge.Value) : 1;
            }
            else
            {
                result = psmKey.PrecursorCharge.HasValue ? -1 : 0;
            }
            return result;
        }
        public int CompareTo(string modifiedSequence)
        {
            return StringComparer.Ordinal.Compare(ModifiedSequence, modifiedSequence);
        }
    }
}
