using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class CrosslinkerDef : XmlNamedElement
    {
        public CrosslinkerDef(string name, FormulaMass formulaMass) : base(name)
        {
            FormulaMass = formulaMass;
        }

        public FormulaMass FormulaMass { get; private set; }
    }
}
