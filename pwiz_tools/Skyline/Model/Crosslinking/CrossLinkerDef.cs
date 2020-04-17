using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class CrosslinkerSettings : Immutable
    {
        public static readonly CrosslinkerSettings EMPTY 
            = new CrosslinkerSettings();
        public string Aas { get; private set; }

        public CrosslinkerSettings ChangeAas(string aas)
        {
            return ChangeProp(ImClone(this), im => im.Aas = aas);
        }
        public ModTerminus? Terminus { get; private set; }

        public CrosslinkerSettings ChangeTerminus(ModTerminus? modTerminus)
        {
            return ChangeProp(ImClone(this), im => im.Terminus = modTerminus);
        }
    }
}
