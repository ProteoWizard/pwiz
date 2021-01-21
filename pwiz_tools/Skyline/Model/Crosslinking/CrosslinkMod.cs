using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class CrosslinkModification
    {
        public CrosslinkModification(StaticMod crosslinker, IEnumerable<CrosslinkSite> sites)
        {
            Crosslinker = crosslinker;
            Sites = new CrosslinkSites(sites);
        }

        public StaticMod Crosslinker { get; private set; }

        public CrosslinkSites Sites { get; private set; }

        protected bool Equals(CrosslinkModification other)
        {
            return Equals(Crosslinker, other.Crosslinker) && Sites.Equals(other.Sites);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((CrosslinkModification) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Crosslinker.GetHashCode() * 397) ^ Sites.GetHashCode();
            }
        }

        public override string ToString()
        {
            StringBuilder result = new StringBuilder(@"[");
            result.Append(Crosslinker.Name);
            result.Append(@"@");
            var aaIndexesByPeptide = Sites.ToLookup(site => site.PeptideIndex, site=>site.AaIndex);
            var maxPeptideIndex = aaIndexesByPeptide.Max(grouping => grouping.Key);
            string strComma = string.Empty;
            for (int iPeptide = 0; iPeptide < maxPeptideIndex; iPeptide++)
            {
                result.Append(strComma);
                strComma = @",";
                string indexesForPeptide = string.Join(@"-", aaIndexesByPeptide[iPeptide]);
                if (string.IsNullOrEmpty(indexesForPeptide))
                {
                    result.Append(@"*");
                }
                else
                {
                    result.Append(indexesForPeptide);
                }
            }

            result.Append(@"]");
            return result.ToString();
        }
    }
}
