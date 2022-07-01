/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.Fasta;
using pwiz.ProteomeDatabase.Properties;

namespace pwiz.ProteomeDatabase.API
{

    /// <summary>
    /// things you'd expect to be able to populate from the information found in a FASTA header, 
    /// or use to get to a webservice to learn the rest
    /// Note on the webservice search term:
    /// this is serialized as a WEBSEARCH_HISTORY_SEP ("#") seperated history of search chain: 
    /// WebSearchInfo "XUNP_313205#G15834432" with Accession "P0A7T9"
    /// means "we searched Entrez for GI 15834432 which led us to search UniprotKB with NP_313205
    /// which gave us accession ID P0A7T9
    ///
    /// </summary>
    public sealed class ProteinMetadata : Immutable, IEquatable<ProteinMetadata>, IAuditLogComparable
    {
        public static readonly ProteinMetadata EMPTY = new ProteinMetadata();
        [Track]
        public string Name { get; private set; } // as read from fasta header line '>[name] [description]'
        [Track]
        public string Description { get; private set; }// as read from fasta header line '>[name] [description]'
        // stuff that may be buried in description, or pulled from a webservice
        [Track]
        public string PreferredName { get; private set; }
        [Track]
        public string Accession { get; private set; }
        [Track]
        public string Gene { get; private set; }
        [Track]
        public string Species { get; private set; }
        // if this does not start with the "search complete" tag, we owe a trip to the internet to try to dig out more metadata
        public WebSearchInfo WebSearchInfo { get; private set; }

        public ProteinMetadata ChangeName(string name)
        {
            return new ProteinMetadata(this){Name = name};
        }

        public ProteinMetadata ChangeDescription(string descr)
        {
            return new ProteinMetadata(this) { Description = descr };
        }
        
        public ProteinMetadata ChangePreferredName(string preferredname)
        {
            return new ProteinMetadata(this) { PreferredName = preferredname };
        }

        public ProteinMetadata ChangeAccession(string accession)
        {
            return new ProteinMetadata(this) { Accession = accession };
        }

        public ProteinMetadata ChangeGene(string gene)
        {
            return new ProteinMetadata(this) { Gene = gene };
        }

        public ProteinMetadata ChangeSpecies(string species)
        {
            return new ProteinMetadata(this){Species = species};
        }

        public ProteinMetadata ChangeWebSearchInfo(WebSearchInfo webSearchInfo)
        {
            return new ProteinMetadata(this) { WebSearchInfo = webSearchInfo };
        }

        public ProteinMetadata ClearWebSearchInfo()
        {
            // sometimes all you really want is to initialize
            return new ProteinMetadata(this) { WebSearchInfo = WebSearchInfo.EMPTY };
        }

        public ProteinMetadata SetWebSearchCompleted()
        {
            return new ProteinMetadata(this) { WebSearchInfo = WebSearchInfo.SetSearchCompleted() };
        }

        public ProteinMetadata SetWebSearchTerm(WebSearchTerm search)
        {
            return new ProteinMetadata(this) { WebSearchInfo = WebSearchInfo.SetSearchTerm(search) };
        }

        public string GetPendingSearchTerm()
        {
            return WebSearchInfo.GetPendingSearchTerm();
        }

        public char GetSearchType()
        {
            return WebSearchInfo.GetSearchType();
        }

        public bool NeedsSearch()
        {
            return WebSearchInfo.NeedsSearch();
        }

        private ProteinMetadata() : this(null,null)
        {
        }

        public ProteinMetadata(string name, string description) : this(name, description, null, null, null, null)
        {
        }

        public ProteinMetadata(string name, string description, string preferredName, string accession, string gene, string species, string websearchterm=null)
        {
            Name = name;
            Description = description;
            PreferredName = preferredName;
            Accession = accession;
            Gene = gene;
            Species = species;
            WebSearchInfo = WebSearchInfo.FromString(websearchterm);
        }

        private ProteinMetadata(ProteinMetadata other)
        {
            if (other == null)
                return;
            Name = other.Name;
            Description = other.Description;
            PreferredName = other.PreferredName;
            Accession = other.Accession;
            Gene = other.Gene;
            Species = other.Species;
            WebSearchInfo = other.WebSearchInfo;
        }

        /// <summary>
        /// returns a copy of this, with any blanks filled in
        /// using the members of source
        /// </summary>
        /// <param name="source">the source to merge from</param>
        /// <returns>a copy of this merged with source, with this winning when fields conflict</returns>
        public ProteinMetadata Merge(ProteinMetadata source)
        {
            if (source==null)
                return this;
            return new ProteinMetadata
            {
                Name = String.IsNullOrEmpty(Name) ? source.Name : Name,
                Description = String.IsNullOrEmpty(Description) ? source.Description : Description,
                Gene = String.IsNullOrEmpty(Gene) ? source.Gene : Gene,
                Accession = String.IsNullOrEmpty(Accession) ? source.Accession : Accession,
                PreferredName = String.IsNullOrEmpty(PreferredName) ? source.PreferredName : PreferredName,
                Species = String.IsNullOrEmpty(Species) ? source.Species : Species,
                // extend the search history of this by adding that of source
                WebSearchInfo = WebSearchInfo.Merge(source.WebSearchInfo)
            };
        }

        public bool HasMissingMetadata()
        {
            return
                string.IsNullOrEmpty(Name) ||
                string.IsNullOrEmpty(PreferredName) ||
                string.IsNullOrEmpty(Description) ||
                string.IsNullOrEmpty(Gene) ||
                string.IsNullOrEmpty(Species) ||
                string.IsNullOrEmpty(Accession);
            // WebSearchInfo is just a note to ourselves, not part of metadata per se
        }

        public string DisplayTextWithoutNameOrDescription() 
        {
            return DisplayText(true, true);
        }
        public string DisplayTextWithoutName() 
        {
            return DisplayText(true, false);
        }
        public string DisplayText(bool excludeName, bool excludeDescription)  
        {
            string result = string.Empty;
            if (!(excludeName)||String.IsNullOrEmpty(Name))
                result = String.Format(Resources.ProteinMetadata_DisplayText_Name___0__,Name);  
            if (!String.IsNullOrEmpty(Accession))
                result += String.Format(Resources.ProteinMetadata_DisplayText_Accession___0__,Accession);
            if (!string.IsNullOrEmpty(PreferredName))
                result += String.Format(Resources.ProteinMetadata_DisplayText_Preferred_Name___0__,PreferredName);
            if (!string.IsNullOrEmpty(Gene))
                result += String.Format(Resources.ProteinMetadata_DisplayText_Gene___0__,Gene);
            if (!String.IsNullOrEmpty(Species))
                result += String.Format(Resources.ProteinMetadata_DisplayText_Species___0__,Species);
            if (!WebSearchInfo.NeedsSearch() && !String.IsNullOrEmpty(DisplaySearchHistory()))
            {
                // some interesting history there
                result += String.Format(Resources.ProteinMetadata_DisplayText_Searched__);
                result += DisplaySearchHistory();
            }
            if (!(excludeDescription || String.IsNullOrEmpty(Description)))
                result += String.Format(Resources.ProteinMetadata_DisplayText_,Description);
            return result;
        }

        /// <summary>
        /// show webservices search chain for human readability
        /// </summary>
        /// <returns>formatted chain of searches in chronological order</returns>
        public String DisplaySearchHistory()
        {
             var history = (from w in WebSearchInfo.GetHistory() where (w.Service != WebEnabledFastaImporter.SEARCHDONE_TAG) select w).Reverse().ToArray();
             if (history.Any())
            {
                 var historyStrs = history.Select(h => h.ToFriendlyString()).ToList();
                 return String.Join(@" -> ", historyStrs);
            }
            return String.Empty;
        }



        public override int GetHashCode()
        {
            unchecked
            {
                int result = (Name != null ? Name.GetHashCode() : 0);
                result = (result * 397) ^ (Description != null ? Description.GetHashCode() : 0);
                result = (result * 397) ^ (Accession != null ? Accession.GetHashCode() : 0);
                result = (result * 397) ^ (Gene != null ? Gene.GetHashCode() : 0);
                result = (result * 397) ^ (Species != null ? Species.GetHashCode() : 0);
                result = (result * 397) ^ (PreferredName != null ? PreferredName.GetHashCode() : 0);
                result = (result * 397) ^ (WebSearchInfo != null ? WebSearchInfo.GetHashCode() : 0); // not proper metadata but worth noting when it changes
                return result;
            }
        }

        public object GetDefaultObject(ObjectInfo<object> info)
        {
            return EMPTY;
        }

        public bool Equals(ProteinMetadata other)
        {
            if (other == null)
                return false;
            if (!string.Equals(Name, other.Name))
                return false;
            if (!string.Equals(PreferredName, other.PreferredName))
                return false;
            if (!string.Equals(Description, other.Description))
                return false;
            if (!string.Equals(Gene, other.Gene))
                return false;
            if (!string.Equals(Species, other.Species))
                return false;
            if (!string.Equals(Accession, other.Accession))
                return false;
            if (!Equals(WebSearchInfo, other.WebSearchInfo)) // not proper metadata but worth noting when it changes)
                return false;
            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ProteinMetadata)obj);
        }

        public override string ToString()
        {
            return String.Format(@"name='{0}' accession='{1}' preferredname='{2}' description='{3}' gene='{4}' species='{5}' websearch='{6}'", Name, Accession, PreferredName, Description, Gene, Species, WebSearchInfo);
        }

    }

    /// <summary>
    /// Classes for tracking state and history of web searches for resolving
    /// missing protein metadata
    /// </summary>

    public class WebSearchTerm: IEquatable<WebSearchTerm>
    {
        public WebSearchTerm(char service, string query)
        {
            Service = service;
            Query = query;
            if (service == WebEnabledFastaImporter.UNIPROTKB_TAG && query != null)
            {
                // UniprotKB has gotten tweaky about SGD yeast entries, as of 6/28/2022 only
                // wants to see "S000000001" from "SGD:S000000001" or "SGDID:S000000001"
                if (query.StartsWith(@"SGD:"))
                {
                    Query = query.Substring(4);
                }
                else if (query.StartsWith(@"SGDID:"))
                {
                    Query = query.Substring(6);
                }
            }
        }
        public char Service { get; private set; }
        public string Query { get; private set; } // an accession id, gi number etc

        public bool Equals(WebSearchTerm other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Service,other.Service) && Equals(Query,other.Query);
        }

        public override string ToString()
        {
            return Service.ToString(CultureInfo.InvariantCulture) + Query;
        }

        public string ToFriendlyString()
        {
            if (Query == null)
                return null;
            if (Query.ToUpperInvariant().StartsWith(@"IPI"))
                return Query; // we handled that ourselves
            switch (Service)
            {
                case WebEnabledFastaImporter.GENINFO_TAG:
                    return String.Format(@"gi:{0}", Query);
                case WebEnabledFastaImporter.ENTREZ_TAG:
                    return String.Format(@"Entrez:{0}", Query);
                case WebEnabledFastaImporter.UNIPROTKB_TAG:
                    return String.Format(@"Uniprot:{0}", Query);
            }
            return null;
        }
    }

    public class WebSearchInfo : Immutable, IEquatable<WebSearchInfo>
    {
        public static readonly WebSearchInfo EMPTY = new WebSearchInfo(new List<WebSearchTerm>());
        public const char WEBSEARCH_HISTORY_SEP = '#';

        public bool NeedsSearch() // If true, we owe a trip to the internet to try to resolve the search
        {
            return (IsEmpty() || _history[0].Service != WebEnabledFastaImporter.SEARCHDONE_TAG); 
        }

        public bool IsEmpty()
        {
            return !_history.Any();
        }

        public WebSearchInfo SetSearchCompleted()
        {
            return SetSearchTerm(new WebSearchTerm(WebEnabledFastaImporter.SEARCHDONE_TAG,null)); 
        }

        public WebSearchInfo SetSearchTerm(WebSearchTerm searchterm)
        {
            var newHistory = new List<WebSearchTerm>(_history); 
            newHistory.Insert(0,searchterm); // newest first
            return new WebSearchInfo(newHistory);
        }


        public WebSearchInfo Merge(WebSearchInfo source)
        {
            if (!_history.Any())
                return source;
            var newHistory = new List<WebSearchTerm>(_history);
            newHistory.AddRange(source._history);
            return new WebSearchInfo(newHistory);
        }

        private readonly ImmutableList<WebSearchTerm> _history;

        public WebSearchInfo(IList<WebSearchTerm> history)
        {
            _history = ImmutableList.ValueOfOrEmpty(history);
        }

        public List<WebSearchTerm> GetHistory()
        {
            return new List<WebSearchTerm>(_history);
        }

        /// <summary>
        /// returns the protein's webservice search term without the
        /// service code, unless the search has been tagged as done,
        /// or is simply empty
        /// </summary>
        /// <returns>search term if any</returns>
        public string GetPendingSearchTerm()
        {
            if (NeedsSearch() && _history.Any())
            {
                return _history[0].Query;
            }
            return String.Empty;
        }

        public char GetSearchType()
        {
            if (_history.Any())
            {
                return _history[0].Service;
            }
            return '\0';
        }


        public bool MatchesPendingSearchTerm(string val)
        {
            var searchterm = GetPendingSearchTerm().ToUpperInvariant();
            if (String.IsNullOrEmpty(searchterm) || String.IsNullOrEmpty(val))
                return false;
            var valUpper = val.ToUpperInvariant();
            if (String.IsNullOrEmpty(valUpper))
                return false;
            if (searchterm.StartsWith(valUpper) || valUpper.StartsWith(searchterm))
                return true;
            if ((_history[0].Service==WebEnabledFastaImporter.GENINFO_TAG) &&
                Equals(valUpper,@"GI|"+searchterm))
                return true; // of form gi|nnnnnnnnn
            return false;
        }

        public bool Equals(WebSearchInfo other)
        {
            if (other == null)
                return false;
            if (!_history.SequenceEqual(other._history))
                return false;
            return true;
        }

        public override int GetHashCode()
        {
            return CollectionUtil.GetHashCodeDeep(_history);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((WebSearchInfo)obj);
        }

        /// <summary>
        /// Encode to a string suitable for storage in a protdb file
        /// </summary>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            for (int n = 0; n < _history.Count; n++ )
            {
                if (n > 0)
                    sb.Append(WEBSEARCH_HISTORY_SEP);
                sb.Append(_history[n].Service);
                sb.Append(_history[n].Query);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Pick apart a string as stored in a protDB file
        /// Looks something line X#UQ0345#G12345678
        /// which reading right to left means
        /// "we searched gi12345678 on entrez
        /// to get the accession Q0345 which we searched
        /// on uniprot, and we're done."
        /// </summary>
        /// <param name="str">the string-encoded data</param>
        public static WebSearchInfo FromString(string str)
        {
            if (str == null)
                return EMPTY;

            var history = str.Split(WEBSEARCH_HISTORY_SEP);
            if (!history.Any())
                return EMPTY;

            var terms = new List<WebSearchTerm>();
            foreach (var hist in history)
            {
                string h = hist;
                if (h.Length>0)
                {
                    terms.Add((h.Length==1)
                        ? new WebSearchTerm(h[0], null)
                        : new WebSearchTerm(h[0], h.Substring(1)));
                }
            }
            return new WebSearchInfo(terms);
        }

    }
}
