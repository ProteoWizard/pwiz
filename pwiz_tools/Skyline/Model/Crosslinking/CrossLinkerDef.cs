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

        protected bool Equals(CrosslinkerSettings other)
        {
            return Aas == other.Aas && Terminus == other.Terminus;
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
                return ((Aas != null ? Aas.GetHashCode() : 0) * 397) ^ Terminus.GetHashCode();
            }
        }
    }
}
