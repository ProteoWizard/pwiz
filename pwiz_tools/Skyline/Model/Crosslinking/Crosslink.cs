/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Crosslinking
{
    /// <summary>
    /// Represents a crosslinker attached to two or more sites on a crosslinked peptide.
    /// </summary>
    public class Crosslink
    {
        public Crosslink(StaticMod crosslinker, IEnumerable<CrosslinkSite> sites)
        {
            if (crosslinker.IsExplicit)
            {
                crosslinker = crosslinker.ChangeExplicit(false);
            }
            Crosslinker = crosslinker;
            Sites = new CrosslinkSites(sites);
        }

        public StaticMod Crosslinker { get; private set; }

        public CrosslinkSites Sites { get; private set; }

        protected bool Equals(Crosslink other)
        {
            return Equals(Crosslinker, other.Crosslinker) && Sites.Equals(other.Sites);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Crosslink) obj);
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
            for (int iPeptide = 0; iPeptide <= maxPeptideIndex; iPeptide++)
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

        public static Crosslink Looplink(StaticMod crosslinker, int peptideIndex, int aaIndex1,
            int aaIndex2)
        {
            return new Crosslink(crosslinker, new []{new CrosslinkSite(peptideIndex, aaIndex1), new CrosslinkSite(peptideIndex, aaIndex2)});
        }
    }
}
