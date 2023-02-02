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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Crosslinking
{
    /// <summary>
    /// Represents a place where a crosslinker attaches to a crosslinked peptide.
    /// Crosslink sites are identified by a 0-based peptide index and a 0-based amino
    /// acid index.
    /// </summary>
    public struct CrosslinkSite : IComparable<CrosslinkSite>
    {
        public CrosslinkSite(int peptideIndex, int aaIndex) : this()
        {
            if (peptideIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(peptideIndex));
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
        /// <summary>
        /// Returns a string representation of the crosslinked sites as used in BiblioSpec libraries:
        /// A comma separated list of the 1-based amino acid positions that the crosslink links to
        /// on each of the peptides.
        /// If there are no crosslinked sites on a particular peptide, then there will be a "*" in
        /// the comma separated list.
        /// If there are more than one sites on a particular peptide, then the numbers will be
        /// separated by hyphens.
        /// </summary>
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
