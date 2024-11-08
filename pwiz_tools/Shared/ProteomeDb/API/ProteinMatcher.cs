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
using System.Linq;
using System.Threading;
using NHibernate;
using pwiz.ProteomeDatabase.DataModel;
using pwiz.ProteomeDatabase.Util;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.ProteomeDatabase.API
{
    /// <summary>
    /// Settings for doing statement completion on protein names, descriptions, or peptide sequences.
    /// </summary>
    public class ProteinMatchSettings
    {
        public ProteinMatchSettings(ProteomeDbPath proteomeDbPath, ProteinMatchTypes proteinMatchTypes, String searchText)
        {
            ProteomeDbPath = proteomeDbPath;
            MatchTypes = proteinMatchTypes;
            SearchText = searchText;
        }
        public ProteomeDbPath ProteomeDbPath { get; private set; }
        public ProteinMatchTypes MatchTypes { get; private set; }
        public String SearchText { get; private set; }
    }

    /// <summary>
    /// A running query to find statement completion matches.
    /// </summary>
    public class ProteinMatchQuery
    {
        private Action<ProteinMatchQuery> _callback;
        private Dictionary<long, ProteinMatch> _matches;
        public ProteinMatchSettings Settings
        {
            get; private set;
        }
        public static String ProteinMatchTypeDbFieldName(ProteinMatchType type)
        {
            switch (type)
            {
                case ProteinMatchType.name:
                    return @"Name";
                case ProteinMatchType.description:
                    return @"Description";
                case ProteinMatchType.sequence:
                    return @"Sequence";
                case ProteinMatchType.accession:
                    return @"Accession";
                case ProteinMatchType.preferredName:
                    return @"PreferredName";
                case ProteinMatchType.gene:
                    return @"Gene";
                case ProteinMatchType.species:
                    return @"Species";
                default:
                    return String.Empty;
            }
        }

        public ProteinMatchQuery(ProteinMatchSettings settings, CancellationToken cancellationToken, int maxResults = MAX_RESULTS_DEFAULT)
        {
            Settings = settings;
            CancellationToken = cancellationToken;
            MaxResults = maxResults;
        }

        public int MaxResults { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public const int MAX_RESULTS_DEFAULT = 100;
        public const int MIN_FREE_SEARCH_LENGTH = 3; // below this search text length we only match metadata beginnings, and not species or description at all

//        public Action<ProteinMatchQuery> Callback { get; private set; }
        public void BeginExecute(Action<ProteinMatchQuery> callback)
        {
            _callback = callback;
//            var runner = new Action(ExecuteBackground);
//            runner.BeginInvoke(runner.EndInvoke, null);
            new Thread(ExecuteBackground).Start();
        }

        /// <summary>
        /// Executes the query (which must be a query on the DbProtein table), and adds
        /// all of the rows in that query to the ProteinMatches.
        /// </summary>
        private void AddProteinMatches(IStatelessSession session, Func<IEnumerable<long>> proteinIdQuery)
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
            var newProteinIds = new List<long>();
            CancellationToken.ThrowIfCancellationRequested();
            foreach (long id in proteinIdQuery())
            {
                CancellationToken.ThrowIfCancellationRequested();
                if (newMatches.ContainsKey(id))
                {
                    continue;
                }
                newProteinIds.Add(id);
            }
            if (newProteinIds.Count == 0)
            {
                return;
            }
            using (var proteomeDb = ProteomeDb.OpenProteomeDb(Settings.ProteomeDbPath.FilePath, CancellationToken))
            {
                // We fetched the protein ids.  Now fetch all the protein rows themselves.
                var proteins = proteomeDb.GetProteinsWithIds(session, newProteinIds);
                foreach (var protein in proteins)
                {
                    var proteinMatch = new ProteinMatch(Settings, protein);
                    newMatches.Add(protein.Id, proteinMatch);
                }
                SetProteinMatches(newMatches);
            }
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

        private void ExecuteBackground()
        {
            try
            {
                ProteinMatchTypes matchTypesRemaining = Settings.MatchTypes;
                using (var proteomeDb = ProteomeDb.OpenProteomeDb(Settings.ProteomeDbPath.FilePath, CancellationToken))
                using (var session = proteomeDb.OpenStatelessSession(false))
                {

                    if (matchTypesRemaining.Contains(ProteinMatchType.sequence) && IsAminoAcidSequence(Settings.SearchText))
                    {
                        AddProteinMatches(session, () => proteomeDb.GetDigestion()
                            .GetProteinIdsThatMightHaveSequence(session, new[] {Settings.SearchText}));
                    }
                    matchTypesRemaining = matchTypesRemaining.Except(ProteinMatchType.sequence);

                    if (DoMatching)
                    {
                        string pattern =  @"%" + Settings.SearchText + @"%";
                        if (Settings.SearchText.Length < MIN_FREE_SEARCH_LENGTH)
                        {
                            // That could cast a pretty wide net - only match to beginning of keywords, and not to description or species
                            pattern = Settings.SearchText + @"%";
                            matchTypesRemaining = matchTypesRemaining.Except(ProteinMatchType.description, ProteinMatchType.species);
                        }

                        var exprLike = new List<string>();
                        foreach (ProteinMatchType matchType in matchTypesRemaining)
                        {
                            exprLike.Add(String.Format(@"pn.{0} LIKE :expr", ProteinMatchTypeDbFieldName(matchType)));
                        }

                        String hql = @"SELECT distinct pn.Protein FROM " + typeof(DbProteinName) + @" pn "
                                        // ReSharper disable LocalizableElement
                                        + String.Format("\nWHERE {0}", String.Join(" OR ", exprLike))
                                        + "\nORDER BY pn.IsPrimary DESC, pn.Name";
                                        // ReSharper restore LocalizableElement
                        IQuery query = session.CreateQuery(hql).SetParameter(@"expr", pattern);
                        query.SetMaxResults(MaxResults);
                        AddProteinMatches(session, () =>
                        {
                            return query.List<DbProtein>()
                                .Where(dbProtein => null != dbProtein)
                                .Select(dbProtein => dbProtein.Id.Value);
                        });

                    }
                }
            }
            catch (Exception exception)
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    return;
                }
                Messages.WriteAsyncDebugMessage(@"Unhandled exception: {0}", exception);
            }
        }

        public bool DoMatching
        {
            get
            {
                lock (this)
                {
                    return _matches == null || _matches.Count < MaxResults;
                }
            }
        }
    }

    public class ProteinMatch
    {
        public ProteinMatch(ProteinMatchSettings proteinMatchSettings, Protein protein)
        {
            Settings = proteinMatchSettings;
            Protein = protein;
            MatchTypes = ProteinMatchTypes.EMPTY;
            String ltext = proteinMatchSettings.SearchText.ToLower();
            foreach (ProteinMatchType matchType in ProteinMatchTypes.ALL) // name, accession, gene, etc - case insenstive
            {
                if (proteinMatchSettings.MatchTypes.Contains(matchType) &&
                    !proteinMatchSettings.MatchTypes.Contains(ProteinMatchType.sequence) &&
                        !proteinMatchSettings.MatchTypes.Contains(ProteinMatchType.description)) // handle sequence and description below
                {
                    if (Matches(protein.ProteinMetadata, matchType, ltext))
                    {
                        MatchTypes = MatchTypes.Union(matchType);
                    }
                    else
                    {
                        foreach (ProteinMetadata alternative in Protein.AlternativeNames)
                        {
                            if (Matches(alternative, matchType, ltext))
                            {
                                MatchTypes = MatchTypes.Union(matchType);
                                AlternativeName = alternative;
                                break;
                            }
                        }
                    }
                }
            }
            if (MatchTypes.IsEmpty && // Don't bother declaring a description match if we already have a more specific one (name, accession etc)
                proteinMatchSettings.MatchTypes.Contains(ProteinMatchType.description))
            {
                if (ContainsLowerCase(protein.Description, ltext) ||
                    ContainsLowerCase(protein.Name, ltext))
                {
                       MatchTypes = MatchTypes.Union(ProteinMatchType.description);
                }
                else
                {
                    foreach (ProteinMetadata alternative in Protein.AlternativeNames)
                    {
                        if (ContainsLowerCase(alternative.Name, ltext) ||
                            ContainsLowerCase(alternative.Description, ltext))
                        {
                            MatchTypes = MatchTypes.Union(ProteinMatchType.description);
                            AlternativeDescription = alternative;
                        }
                    }
                }
            }
            if (proteinMatchSettings.MatchTypes.Contains(ProteinMatchType.sequence))
            {
                if (protein.Sequence.IndexOf(proteinMatchSettings.SearchText, StringComparison.Ordinal) >= 0)
                {
                    MatchTypes = MatchTypes.Union(ProteinMatchType.sequence);
                }
            }
        }
        public ProteinMatchSettings Settings { get; private set; }
        public Protein Protein { get; private set;}
        public ProteinMetadata AlternativeName { get; private set; }
        public ProteinMetadata AlternativeDescription { get; private set; }
        public ProteinMatchTypes MatchTypes { get; private set; }

        private static bool Matches(ProteinMetadata proteinMetadata, ProteinMatchType matchType, string ltext)
        {
            return ContainsLowerCase(proteinMetadata.TextForMatchTypes(ProteinMatchTypes.Singleton(matchType)), ltext);
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
        public static string TextForMatchTypes(this ProteinMetadata p, ProteinMatchTypes types)
        {
            if (types.IsSingleton(ProteinMatchType.sequence))
                return null;

            var results = new List<string>();
            if (types.Contains(ProteinMatchType.name))
                results.Add(p.Name??String.Empty);
            if (types.Contains(ProteinMatchType.accession))
                results.Add(p.Accession ?? String.Empty);
            if (types.Contains(ProteinMatchType.preferredName))
                results.Add(p.PreferredName ?? String.Empty);
            if (types.Contains(ProteinMatchType.gene))
                results.Add(p.Gene ?? String.Empty);
            if (types.Contains(ProteinMatchType.species))
                results.Add(p.Species ?? String.Empty);
            if (types.Contains(ProteinMatchType.description)) // put this last, as it likely contains the others
                results.Add(p.Description ?? String.Empty);
            if (results.Count > 0)
                return String.Join(@" ",results);

            return null;
        }
    }

    public enum ProteinMatchType
    {
        name,
        description,
        sequence,
        accession,
        preferredName,
        gene,
        species
    }

    public class ProteinMatchTypes : ValueSet<ProteinMatchTypes, ProteinMatchType>
    {
        public static readonly ProteinMatchTypes ALL = OfValues(
            ProteinMatchType.name, ProteinMatchType.description, ProteinMatchType.sequence, 
            ProteinMatchType.accession, ProteinMatchType.preferredName, ProteinMatchType.gene, 
            ProteinMatchType.species);
    }
}
