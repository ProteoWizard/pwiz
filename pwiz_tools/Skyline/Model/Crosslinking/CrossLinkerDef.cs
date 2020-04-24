using System;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class CrosslinkerSettings : Immutable
    {
        public static readonly CrosslinkerSettings EMPTY 
            = new CrosslinkerSettings();
        public string Formula { get; private set; }
        public double? MonoisotopicMass { get; private set; }
        public double? AverageMass { get; private set; }

        public CrosslinkerSettings ChangeFormula(string formula, double? monoMass, double? averageMass)
        {
            return ChangeProp(ImClone(this), im =>
            {
                if (string.IsNullOrEmpty(formula))
                {
                    im.Formula = null;
                    im.MonoisotopicMass = monoMass;
                    im.AverageMass = averageMass;
                }
                else
                {
                    im.Formula = formula;
                    im.MonoisotopicMass = null;
                    im.AverageMass = averageMass;
                }
            });
        }

        protected bool Equals(CrosslinkerSettings other)
        {
            return Formula == other.Formula
                   && Nullable.Equals(MonoisotopicMass, other.MonoisotopicMass)
                   && Nullable.Equals(AverageMass, other.AverageMass);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((CrosslinkerSettings) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Formula != null ? Formula.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ MonoisotopicMass.GetHashCode();
                hashCode = (hashCode * 397) ^ AverageMass.GetHashCode();
                return hashCode;
            }
        }
    }
}
