using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class FormulaMass : Immutable, IXmlSerializable
    {
        public static readonly FormulaMass EMPTY = new FormulaMass(string.Empty);
        public FormulaMass(string formula) : this(formula, 0, 0)
        {
        }

        public FormulaMass(string formula, double monoMassOffset, double averageMassOffset)
        {
            Formula = formula ?? string.Empty;
            MonoMassOffset = monoMassOffset;
            AverageMassOffset = averageMassOffset;
        }

        [Track(defaultValues:typeof(DefaultValuesNull))]
        public string Formula { get; private set; }

        [Track(defaultValues: typeof(DefaultValuesZero))]
        public double MonoMassOffset { get; private set; }
        [Track(defaultValues: typeof(DefaultValuesZero))]
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
                var hashCode = Formula.GetHashCode();
                hashCode = (hashCode * 397) ^ MonoMassOffset.GetHashCode();
                hashCode = (hashCode * 397) ^ AverageMassOffset.GetHashCode();
                return hashCode;
            }
        }

        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
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

        private enum ATTR
        {
            formula,
            mono_mass_offset,
            average_mass_offset
        }

        private FormulaMass()
        {

        }
        void IXmlSerializable.ReadXml(XmlReader reader)
        {
            if (null != Formula)
            {
                throw new InvalidOperationException();
            }

            Formula = reader.GetAttribute(ATTR.formula) ?? string.Empty;
            MonoMassOffset = reader.GetDoubleAttribute(ATTR.mono_mass_offset);
            AverageMassOffset = reader.GetDoubleAttribute(ATTR.average_mass_offset);
            reader.Read();
        }

        void IXmlSerializable.WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeIfString(ATTR.formula, Formula);
            if (MonoMassOffset != 0)
            {
                writer.WriteAttribute(ATTR.mono_mass_offset, MonoMassOffset);
            }
            if (AverageMassOffset != 0)
            {
                writer.WriteAttribute(ATTR.average_mass_offset, AverageMassOffset);
            }
        }

        public static FormulaMass Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new FormulaMass());
        }
    }
}
