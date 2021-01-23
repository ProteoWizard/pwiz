using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Crosslinking
{
    public struct CrosslinkSite : IComparable<CrosslinkSite>
    {
        public CrosslinkSite(int peptideIndex, int aaIndex) : this()
        {
            if (peptideIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(peptideIndex));
            }

            if (aaIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(aaIndex));
            }
            PeptideIndex = peptideIndex;
            AaIndex = aaIndex;
        }

        public int PeptideIndex { get; private set; }
        public int AaIndex { get; private set; }

        public int Position
        {
            get { return AaIndex + 1; }
        }

        public override string ToString()
        {
            return @"[" + PeptideIndex + @"," + AaIndex + @"]";
        }

        public int CompareTo(CrosslinkSite other)
        {
            int result = PeptideIndex.CompareTo(other.PeptideIndex);
            if (result == 0)
            {
                result = AaIndex.CompareTo(other.AaIndex);
            }

            return result;
        }
    }

    public struct CrosslinkSites : IEnumerable<CrosslinkSite>
    {
        private ImmutableList<CrosslinkSite> _sites;

        public CrosslinkSites(IEnumerable<CrosslinkSite> sites)
        {
            if (sites == null)
            {
                _sites = null;
            }
            else if (sites is CrosslinkSites crosslinkSites)
            {
                _sites = crosslinkSites._sites;
            }
            else
            {
                _sites = ImmutableList.ValueOf(sites.OrderBy(site => site));
            }
        }

        public int Count
        {
            get { return Sites.Count; }
        }

        public ImmutableList<CrosslinkSite> Sites
        {
            get { return _sites ?? ImmutableList<CrosslinkSite>.EMPTY; }
        }

        public IEnumerable<int> PeptideIndexes
        {
            get
            {
                return Sites.Select(site => site.PeptideIndex);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<CrosslinkSite> GetEnumerator()
        {
            return Sites.GetEnumerator();
        }

        public static CrosslinkSites FromSites(IEnumerable<CrosslinkSite> sites)
        {
            return new CrosslinkSites(sites);
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            ILookup<int, int> sitesByPeptideIndex = Sites.ToLookup(site => site.PeptideIndex, site => site.Position);
            int maxPeptide = Sites.Max(site => site.PeptideIndex);
            string strComma = string.Empty;
            for (int i = 0; i <= maxPeptide; i++)
            {
                stringBuilder.Append(strComma);
                strComma = @",";
                string strIndexes = string.Join(@"-", sitesByPeptideIndex[i]);
                if (strIndexes.Length == 0)
                {
                    stringBuilder.Append(@"*");
                }
                else
                {
                    stringBuilder.Append(strIndexes);
                }
            }

            return stringBuilder.ToString();
        }
    }
}
