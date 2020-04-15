using System;
using JetBrains.Annotations;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class FormulaMass : Immutable
    {
        public FormulaMass(string formula) : this(formula, 0, 0)
        {
            Formula = formula;
        }

        private FormulaMass(string formula, double monoMassOffset, double averageMassOffset)
        {
        }

        [CanBeNull]
        public string Formula { get; private set; }

        public double MonoMassOffset { get; private set; }
        public double AverageMassOffset { get; private set; }

        protected bool Equals(FormulaMass other)
        {
            return Formula == other.Formula &&
                   MonoMassOffset.Equals(other.MonoMassOffset) &&
                   AverageMassOffset.Equals(other.AverageMassOffset) ;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((FormulaMass) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Formula != null ? Formula.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ MonoMassOffset.GetHashCode();
                hashCode = (hashCode * 397) ^ AverageMassOffset.GetHashCode();
                return hashCode;
            }
        }

        public MoleculeMassOffset GetMoleculeMassOffset(MassType massType)
        {
            Molecule molecule = Molecule.Empty;
            if (!string.IsNullOrEmpty(Formula))
            {
                molecule = Molecule.ParseExpression(Formula);
            }
            return new MoleculeMassOffset(molecule, massType.IsMonoisotopic() ? MonoMassOffset : AverageMassOffset);
        }
    }
}
