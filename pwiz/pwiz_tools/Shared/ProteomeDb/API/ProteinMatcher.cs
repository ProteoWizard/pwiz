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

namespace pwiz.ProteomeDatabase.API
{
    /// <summary>
    /// Settings for doing statement completion on protein names, descriptions, or peptide sequences.
    /// </summary>
    public class ProteinMatchSettings
    {
        public ProteinMatchSettings(ProteomeDb proteomeDb, IProtease protease, ProteinMatchType proteinMatchTypes, String searchText)
        {
            ProteomeDb = proteomeDb;
            Protease = protease;
            if (protease != null)
            {
                Digestion = ProteomeDb.GetDigestion(protease.Name);
            }
            MatchTypes = proteinMatchTypes;
            SearchText = searchText;
        }
        public ProteomeDb ProteomeDb { get; private set; }
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
        public ProteinMatchQuery(ProteinMatchSettings settings, int maxResults)
        {
            Settings = settings;
            MaxResults = maxResults;
        }

        public int MaxResults { get; private set; }
//        public Action<ProteinMatchQuery> Callback { get; private set; }
        public void BeginExecute(Action<ProteinMatchQuery> callback)
        {
            _callback = callback;
            new Action(ExecuteBackground).BeginInvoke(null, null);
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
                .Add(Restrictions.In("Id", newProteinIds))
                .List(newProteins);
            // Now fetch all of the protein name records at once
            var criteria = _session.CreateCriteria(typeof (DbProteinName))
                .Add(Restrictions.In("Protein", newProteins));
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
                var proteinMatch = new ProteinMatch(Settings, new Protein(Settings.ProteomeDb, entry.Key, entry.Value));
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
                return "A";
            }
            return str.Substring(0, str.Length - 1) + (char) (str[str.Length - 1] + 1);
        }

        private void ExecuteBackground()
        {
            try
            {
                using (_session = Settings.ProteomeDb.OpenSession())
                {
                    if (0 != (Settings.MatchTypes & ProteinMatchType.sequence) && Settings.Digestion != null &&
                        IsAminoAcidSequence(Settings.SearchText))
                    {
                        String truncatedSearchText = Settings.SearchText.Substring(
                            0, Math.Min(Settings.SearchText.Length, Settings.Digestion.MaxSequenceLength));
                        String hql = "SELECT distinct dpp.Protein FROM " + typeof (DbDigestedPeptideProtein) + " dpp"
                                     +
                                     "\nWHERE dpp.Peptide.Digestion = :digestion AND dpp.Peptide.Sequence >= :start AND dpp.Peptide.Sequence < :end"
                                     + "\nORDER BY dpp.Peptide.Sequence";
                        IQuery query = _session.CreateQuery(hql)
                            .SetParameter("digestion", Settings.Digestion.GetEntity(_session))
                            .SetParameter("start", truncatedSearchText)
                            .SetParameter("end", NextString(truncatedSearchText));
                        AddProteinMatches(query);
                    }
                    if (_matches != null)
                    {
                        return;
                    }
                    if (0 != (Settings.MatchTypes & ProteinMatchType.name))
                    {
                        String hql = "SELECT distinct pn.Protein FROM " + typeof (DbProteinName) + " pn "
                                     + "\nWHERE pn.Name >= :start AND pn.Name < :end"
                                     + "\nORDER BY pn.IsPrimary DESC, pn.Name";
                        IQuery query = _session.CreateQuery(hql)
                            .SetParameter("start", Settings.SearchText)
                            .SetParameter("end", NextString(Settings.SearchText));
                        AddProteinMatches(query);
                    }
                    if (_matches != null)
                    {
                        return;
                    }
                    if (0 != (Settings.MatchTypes & ProteinMatchType.description) && Settings.SearchText.Length >= 3)
                    {
                        String hql = "SELECT distinct pn.Protein FROM " + typeof (DbProteinName) + " pn "
                                     + "\nWHERE pn.Name LIKE :expr OR pn.Description LIKE :expr"
                                     + "\nORDER by pn.IsPrimary DESC, pn.Name";
                        IQuery query = _session.CreateQuery(hql)
                            .SetParameter("expr", "%" + Settings.SearchText + "%");
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
            if (0 != (proteinMatchSettings.MatchTypes & ProteinMatchType.name))
            {
                if (protein.Name.StartsWith(proteinMatchSettings.SearchText))
                {
                    MatchType |= ProteinMatchType.name;
                }
                else
                {
                    foreach (AlternativeName alternative in Protein.AlternativeNames)
                    {
                        if (alternative.Name.StartsWith(proteinMatchSettings.SearchText))
                        {
                            MatchType |= ProteinMatchType.name;
                            AlternativeName = alternative;
                            break;
                        }
                    }
                }
            }
            if (0 != (proteinMatchSettings.MatchTypes & ProteinMatchType.description))
            {
                String ltext = proteinMatchSettings.SearchText.ToLower();
                if (protein.Description.ToLower().IndexOf(ltext, StringComparison.Ordinal) >= 0 ||
                    protein.Name.ToLower().IndexOf(ltext, StringComparison.Ordinal) >= 0)
                {
                    MatchType |= ProteinMatchType.description;
                }
                else
                {
                    foreach (AlternativeName alternative in Protein.AlternativeNames)
                    {
                        if (alternative.Description.ToLower().IndexOf(ltext, StringComparison.Ordinal) >= 0 ||
                            alternative.Description.ToLower().IndexOf(ltext, StringComparison.Ordinal) >= 0)
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
        public AlternativeName AlternativeName { get; private set; }
        public AlternativeName AlternativeDescription { get; private set; }
        public IList<DigestedPeptide> DigestedPeptides { get; private set; }
        public ProteinMatchType MatchType {get;private set;} 
    }

    [Flags]
    public enum ProteinMatchType
    {
        name = 1,
        description = 2,
        sequence = 4,
        all = name | description | sequence
    }
}
