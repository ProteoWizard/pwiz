/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using NHibernate;
using NHibernate.Criterion;
using pwiz.ProteomeDatabase.DataModel;
using pwiz.ProteomeDatabase.Util;

namespace pwiz.ProteomeDatabase.API
{
    /// <summary>
    /// Settings for doing statement completion on protein names, descriptions, or peptide sequences.
    /// </summary>
    public class ProteinMatchSettings
    {
        public ProteinMatchSettings(ProteomeDbPath proteomeDbPath, IProtease protease, ProteinMatchType proteinMatchTypes, String searchText)
        {
            ProteomeDbPath = proteomeDbPath;
            Protease = protease;
            if (protease != null)
            {
                using (var proteomeDb = proteomeDbPath.OpenProteomeDb())
                {
                    Digestion = proteomeDb.GetDigestion(protease.Name);
                }
            }
            MatchTypes = proteinMatchTypes;
            SearchText = searchText;
        }
        public ProteomeDbPath ProteomeDbPath { get; private set; }
        public IProtease Protease { get; private set; }
        public Digestion Digestion { get; private set; }
        public ProteinMatchType MatchTypes { get; private set; }
        public String SearchText { get; private set; }
    }

    /// <summary>
    /// A running query to find statement completion matches.
    /// </summary>
    public class ProteinMatchQuery
    {
        private ISession _session;
        private volatile bool _cancelled;
        private Action<ProteinMatchQuery> _callback;
        private Dictionary<long, ProteinMatch> _matches;
        public ProteinMatchSettings Settings
        {
            get; private set;
        }
        static public String ProteinMatchTypeDbFieldName(ProteinMatchType type)
        {
            switch (type)
            {
                case ProteinMatchType.name:
                    return "Name"; // Not L10N
                case ProteinMatchType.description:
                    return "Description"; // Not L10N
                case ProteinMatchType.sequence:
                    return "Sequence"; // Not L10N
                case ProteinMatchType.accession:
                    return "Accession"; // Not L10N
                case ProteinMatchType.preferredName:
                    return "PreferredName"; // Not L10N
                case ProteinMatchType.gene:
                    return "Gene"; // Not L10N
                case ProteinMatchType.species:
                    return "Species"; // Not L10N
                default:
                    return String.Empty;
            }
        }

        public void Cancel()
        {
            lock(this)
            {
                if (_cancelled)
                {
                    return;
                }
                _cancelled = true;
                if (_session != null && _session.IsOpen)
                    {
                        try
                        {
                            _session.CancelQuery();
                        }
// ReSharper disable EmptyGeneralCatchClause
                        catch (Exception)
                        {

                            // Handle race condition where SQLite command is disposed
                            // before the call to CancelQuery.
                        }
// ReSharper restore EmptyGeneralCatchClause
                    }
                }
            }

        public ProteinMatchQuery(ProteinMatchSettings settings, int maxResults = MAX_RESULTS_DEFAULT)
        {
            Settings = settings;
            MaxResults = maxResults;
        }

        public int MaxResults { get; private set; }

        public const int MAX_RESULTS_DEFAULT = 100;
        public const int MIN_FREE_SEARCH_LENGTH = 3; // below this search text length we only match metadata beginnings, and not species or description at all

//        public Action<ProteinMatchQuery> Callback { get; private set; }
        public void BeginExecute(Action<ProteinMatchQuery> callback)
        {
            _callback = callback;
            var runner = new Action(ExecuteBackground);
            runner.BeginInvoke(runner.EndInvoke, null);
        }

        /// <summary>
        /// Executes the query (which must be a query on the DbProtein table), and adds
        /// all of the rows in that query to the ProteinMatches.
        /// </summary>
        private void AddProteinMatches(IQuery query)
        {
            Dictionary<long, ProteinMatch> newMatches = new Dictionary<long, ProteinMatch>();
            lock (this)
            {
                if (_matches != null)
                {
                    foreach (var entry in _matches)
                    {
                        newMatches.Add(entry.Key, entry.Value);
                    }
                }
            }
            query.SetMaxResults(MaxResults);
            var newProteinIds = new List<long>();
            ThrowIfCancelled();
            foreach (DbProtein dbProtein in query.Enumerable())
            {
                ThrowIfCancelled();
                if (!dbProtein.Id.HasValue || newMatches.ContainsKey(dbProtein.Id.Value))
                {
                    continue;
                }
                newProteinIds.Add(dbProtein.Id.Value);
            }
            if (newProteinIds.Count == 0)
            {
                return;
            }
            // We fetched the protein ids.  Now fetch all the protein rows themselves.
            var newProteins = new List<DbProtein>();
            _session.CreateCriteria(typeof (DbProtein))
                .Add(Restrictions.In("Id", newProteinIds)) // Not L10N
                .List(newProteins);
            // Now fetch all of the protein name records at once
            var criteria = _session.CreateCriteria(typeof (DbProteinName))
                .Add(Restrictions.In("Protein", newProteins)); // Not L10N
            var proteinNames = new Dictionary<DbProtein, List<DbProteinName>>();
            foreach (DbProteinName proteinName in criteria.List())
            {
                List<DbProteinName> names;
                if (!proteinNames.TryGetValue(proteinName.Protein, out names))
                {
                    names = new List<DbProteinName>();
                    proteinNames.Add(proteinName.Protein, names);
                }
                names.Add(proteinName);
            }

            // Create a ProteinMatch for each Protein
            foreach (var entry in proteinNames)
            {
                var proteinMatch = new ProteinMatch(Settings, new Protein(Settings.ProteomeDbPath, entry.Key, entry.Value));
                if (entry.Key.Id.HasValue)
                    newMatches.Add(entry.Key.Id.Value, proteinMatch);
            }
            SetProteinMatches(newMatches);
        }
        /// <summary>
        /// Updates the set of proteins that match the search text, and notify the callback with the new results.
        /// </summary>
        private void SetProteinMatches(Dictionary<long, ProteinMatch> newMatches)
        {
            if (newMatches.Count == 0)
            {
                return;
            }
            lock (this)
            {
                if (_matches != null && _matches.Count == newMatches.Count)
                {
                    return;
                }
                _matches = newMatches;
            }
            if (_callback != null)
            {
                _callback.Invoke(this);
            }
        }

        public List<ProteinMatch> GetMatches()
        {
            lock (this)
            {
                if (_matches == null)
                {
                    return new List<ProteinMatch>();
                }
                return new List<ProteinMatch>(_matches.Values);
            }
        }

        class QueryCancelledException : Exception
        {
        }


        private void ThrowIfCancelled()
        {
            if (_cancelled)
            {
                throw new QueryCancelledException();
            }
        }
        /// <summary>
        /// Returns true if the string might be the start of an amino acid sequence.
        /// </summary>
        private static bool IsAminoAcidSequence(String str)
        {
            if (str.Length < 2)
            {
                return false;
            }
            foreach (char c in str)
            {
                if (c < 'A' || c > 'Z')
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Returns the string that sorts lexicographically after str.
        /// </summary>
        private static String NextString(String str)
        {
            if (str.Length == 0)
            {
                return "A"; // Not L10N
            }
            return str.Substring(0, str.Length - 1) + (char) (str[str.Length - 1] + 1);
        }

        private void ExecuteBackground()
        {
            try
            {
                using (var proteomeDb = Settings.ProteomeDbPath.OpenProteomeDb())
                using (_session = proteomeDb.OpenSession())
                {
                    if (0 != (Settings.MatchTypes & ProteinMatchType.sequence) && Settings.Digestion != null &&
                        IsAminoAcidSequence(Settings.SearchText))
                    {
                        String truncatedSearchText = Settings.SearchText.Substring(
                            0, Math.Min(Settings.SearchText.Length, Settings.Digestion.MaxSequenceLength));
                        String hql = "SELECT distinct dpp.Protein FROM " + typeof(DbDigestedPeptideProtein) + " dpp" // Not L10N
                                     +
                                     "\nWHERE dpp.Peptide.Digestion = :digestion AND dpp.Peptide.Sequence >= :start AND dpp.Peptide.Sequence < :end" // Not L10N
                                     + "\nORDER BY dpp.Peptide.Sequence"; // Not L10N
                        IQuery query = _session.CreateQuery(hql)
                            .SetParameter("digestion", Settings.Digestion.GetEntity(_session)) // Not L10N
                            .SetParameter("start", truncatedSearchText) // Not L10N
                            .SetParameter("end", NextString(truncatedSearchText)); // Not L10N
                        AddProteinMatches(query);
                    }

                    if ((_matches == null) || (_matches.Count < MaxResults))
                    {
                        string pattern =  "%" + Settings.SearchText + "%"; // Not L10N
                        ProteinMatchType unwantedMatchTypes = ProteinMatchType.sequence; // Don't search on sequence again
                        if (Settings.SearchText.Length < MIN_FREE_SEARCH_LENGTH)
                        {
                            // That could cast a pretty wide net - only match to beginning of keywords, and not to description or species
                            pattern = Settings.SearchText + "%"; // Not L10N
                            unwantedMatchTypes |= (ProteinMatchType.description | ProteinMatchType.species);
                        }

                        var exprLike = new List<string>();
                        for (int bit = 1; bit < (int)ProteinMatchType.all; bit = bit << 1)
                        {
                            ProteinMatchType matchType = (ProteinMatchType)bit;
                            if ((0 == (matchType & unwantedMatchTypes)) &&
                                (0 != (Settings.MatchTypes & matchType)))
                            {
                                exprLike.Add(String.Format("pn.{0} LIKE :expr", ProteinMatchTypeDbFieldName(matchType)));  // Not L10N
                            }
                        }

                        String hql = "SELECT distinct pn.Protein FROM " + typeof(DbProteinName) + " pn " // Not L10N
                                        + String.Format("\nWHERE {0}", String.Join(" OR ", exprLike)) // Not L10N
                                        + "\nORDER BY pn.IsPrimary DESC, pn.Name"; // Not L10N
                        IQuery query = _session.CreateQuery(hql).SetParameter("expr", pattern); // Not L10N
                        AddProteinMatches(query);
                    }
                }
            }
            catch (Exception exception)
            {
                if (_cancelled)
                {
                    return;
                }
                Console.Out.WriteLine(exception);
            }
        }
    }

    public class ProteinMatch
    {
        public ProteinMatch(ProteinMatchSettings proteinMatchSettings, Protein protein)
        {
            Settings = proteinMatchSettings;
            Protein = protein;
            String ltext = proteinMatchSettings.SearchText.ToLower();
            for (int bit = 1; bit < (int)ProteinMatchType.all; bit <<= 1) // name, accession, gene, etc - case insenstive
            {
                ProteinMatchType matchType = (ProteinMatchType) bit;
                if ((0 != (proteinMatchSettings.MatchTypes & matchType)) && 
                    (0==(matchType &(ProteinMatchType.sequence|ProteinMatchType.description)))) // handle sequence and description below
                {
                    if (Matches(protein.ProteinMetadata, matchType, ltext))
                    {
                        MatchType |= matchType;
                    }
                    else
                    {
                        foreach (ProteinMetadata alternative in Protein.AlternativeNames)
                        {
                            if (Matches(alternative, matchType, ltext))
                            {
                                MatchType |= matchType;
                                AlternativeName = alternative;
                                break;
                            }
                        }
                    }
                }
            }
            if ((MatchType == 0) && // Don't bother declaring a description match if we already have a more specific one (name, accession etc)
                (0 != (proteinMatchSettings.MatchTypes & ProteinMatchType.description))) 
            {
                if (ContainsLowerCase(protein.Description, ltext) ||
                    ContainsLowerCase(protein.Name, ltext))
                {
                       MatchType |= ProteinMatchType.description;
                }
                else
                {
                    foreach (ProteinMetadata alternative in Protein.AlternativeNames)
                    {
                        if (ContainsLowerCase(alternative.Name, ltext) ||
                            ContainsLowerCase(alternative.Description, ltext))
                        {
                            MatchType |= ProteinMatchType.description;
                            AlternativeDescription = alternative;
                        }
                    }
                }
            }
            List<DigestedPeptide> digestedPeptides = new List<DigestedPeptide>();
            if (0 != (proteinMatchSettings.MatchTypes & ProteinMatchType.sequence))
            {
                if (proteinMatchSettings.Protease != null)
                {
                    String lastPeptide = null;
                    foreach (var peptide in proteinMatchSettings.Protease.Digest(protein))
                    {
                        if (!peptide.Sequence.StartsWith(proteinMatchSettings.SearchText))
                        {
                            continue;
                        }
                        // If this peptide is just an extension of the previous peptide (i.e. with one
                        // more missed cleavage), then only include it if the user has typed the entire
                        // sequence of the previous peptide.
                        if (lastPeptide != null && peptide.Sequence.StartsWith(lastPeptide) 
                            && lastPeptide.Length > proteinMatchSettings.SearchText.Length)
                        {
                            continue;
                        }
                        lastPeptide = peptide.Sequence;
                        MatchType |= ProteinMatchType.sequence;
                        digestedPeptides.Add(peptide);
                    }
                }
            }
            DigestedPeptides = digestedPeptides;
        }
        public ProteinMatchSettings Settings { get; private set; }
        public Protein Protein { get; private set;}
        public ProteinMetadata AlternativeName { get; private set; }
        public ProteinMetadata AlternativeDescription { get; private set; }
        public IList<DigestedPeptide> DigestedPeptides { get; private set; }
        public ProteinMatchType MatchType { get; private set; }

        private static bool Matches(ProteinMetadata proteinMetadata, ProteinMatchType matchType, string ltext)
        {
            return ContainsLowerCase(proteinMetadata.TextForMatchTypes(matchType), ltext);
        }
        private static bool ContainsLowerCase(string bigString, string ltext)
        {
            if (string.IsNullOrEmpty(bigString) || string.IsNullOrEmpty(ltext))
            {
                return false;
            }
            return bigString.ToLower().IndexOf(ltext, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    public static class ProteinMatchTypeExtension
    {
        public static string TextForMatchTypes(this ProteinMetadata p, ProteinMatchType type)
        {
            if (type == ProteinMatchType.sequence)
                return null;

            var results = new List<string>();
            if (0 != (type & ProteinMatchType.name))
                results.Add(p.Name??String.Empty);
            if (0 != (type & ProteinMatchType.accession))
                results.Add(p.Accession ?? String.Empty);
            if (0 != (type & ProteinMatchType.preferredName))
                results.Add(p.PreferredName ?? String.Empty);
            if (0 != (type & ProteinMatchType.gene))
                results.Add(p.Gene ?? String.Empty);
            if (0 != (type & ProteinMatchType.species))
                results.Add(p.Species ?? String.Empty);
            if (0 != (type & ProteinMatchType.description)) // put this last, as it likely contains the others
                results.Add(p.Description ?? String.Empty);
            if (results.Count > 0)
                return String.Join(" ",results); // Not L10N

            return null;
        }
    }

    [Flags]
    public enum ProteinMatchType
    {
        name = 1,
        description = 2,
        sequence = 4,
        accession = 8,
        preferredName = 16,
        gene = 32,
        species = 64,
        all = name | description | sequence | accession | preferredName | gene | species
    }
}
