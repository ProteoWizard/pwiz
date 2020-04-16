using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    [XmlRoot("crosslinker")]
    public class CrosslinkerDef : XmlNamedElement
    {
        public CrosslinkerDef(string name, FormulaMass intactFormula) : base(name)
        {
            IntactFormula = intactFormula;
        }

        public FormulaMass IntactFormula { get; private set; }

        private CrosslinkerDef()
        {
        }

        private enum EL
        {
            intact_molecule,
        }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            if (reader.IsEmptyElement)
            {
                reader.Read();
            }
            else
            {
                reader.ReadStartElement();
                IntactFormula = reader.DeserializeElement<FormulaMass>(EL.intact_molecule);
                reader.ReadEndElement();
            }
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            if (IntactFormula != null)
            {
                writer.WriteElement(EL.intact_molecule, IntactFormula);
            }
        }

        public static CrosslinkerDef Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new CrosslinkerDef());
        }

        protected bool Equals(CrosslinkerDef other)
        {
            return base.Equals(other) && Equals(IntactFormula, other.IntactFormula);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((CrosslinkerDef) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode() * 397) ^ (IntactFormula != null ? IntactFormula.GetHashCode() : 0);
            }
        }
    }
}
