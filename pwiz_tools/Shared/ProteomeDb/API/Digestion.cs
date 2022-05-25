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

namespace pwiz.ProteomeDatabase.API
{
    /// <summary>
    /// Public API for the digestion of the proteome by a particular enzyme.
    /// </summary>
    public class Digestion
    {
        public Digestion(ProteomeDb proteomeDb)
        {
            ProteomeDb = proteomeDb;
        }

        public ProteomeDb ProteomeDb {get; private set;}

        public bool? UseSubsequenceTable { get; set; }

        public List<Protein> GetProteinsWithSequence(String sequence)
        {
            return GetProteinsWithSequences(new[] { sequence }, CancellationToken.None)[sequence];
        }

        public List<Protein> GetProteinsWithSequence(IStatelessSession session, String sequence)
        {
            return GetProteinsWithSequences(session, new[] {sequence}, CancellationToken.None)[sequence];
        }

        public IDictionary<String, List<Protein>> GetProteinsWithSequences(IEnumerable<string> sequences,  CancellationToken cancellationToken)
        {
            using (var session = ProteomeDb.OpenStatelessSession(false))
            {
                return GetProteinsWithSequences(session, sequences, cancellationToken);
            }
        }

        public IDictionary<String, List<Protein>> GetProteinsWithSequences(IStatelessSession session,
            IEnumerable<string> sequences, CancellationToken cancellationToken)
        {
            var sequenceList = sequences.Where(s => !string.IsNullOrEmpty(s)).ToArray();
            var proteinIds = GetProteinIdsThatMightHaveSequence(session, sequenceList);
            var results = new Dictionary<string, List<Protein>>();
            var proteins = GetProteinsWithIds(session, proteinIds);
            var count = 0;
            foreach (var s in sequenceList.Distinct())
            {
                results.Add(s, proteins.Where(p => p.Sequence.IndexOf(s, StringComparison.Ordinal) >= 0).ToList());
                if ((count++ % 100) == 0 && cancellationToken.IsCancellationRequested)
                {
                    return null;
                }
            }
            return results;
        }

        internal IList<long> GetProteinIdsThatMightHaveSequence(IStatelessSession session, ICollection<string> sequencesToLookFor)
        {
            Func<ICollection<String>, IEnumerable<long>> functionToCall;
            IList<String> uniqueStrings;
            bool useSubsequenceTable;
            if (UseSubsequenceTable.HasValue)
            {
                useSubsequenceTable = UseSubsequenceTable.Value;
            }
            else
            {
                useSubsequenceTable = ProteomeDb.HasSubsequencesTable(()=>session.Connection) &&
                                      !sequencesToLookFor.Any(s => s.Length < ProteomeDb.MIN_SEQUENCE_LENGTH);
            }
            if (useSubsequenceTable)
            {
                functionToCall = batchedArgs => QuerySubsequencesTableForSubstrings(session, batchedArgs);
                uniqueStrings = RemoveSuperstrings(sequencesToLookFor
                    .Select(s => s.Substring(0, Math.Min(s.Length, ProteomeDb.MAX_SEQUENCE_LENGTH))));
            }
            else
            {
                functionToCall = batchedArgs => QueryProteinTableForSubstrings(session, batchedArgs);
                uniqueStrings = RemoveSuperstrings(sequencesToLookFor);
            }
            return ProteomeDb.BatchUpArgumentsForFunction(functionToCall, uniqueStrings, 500).Distinct().ToArray();
        }

        /// <summary>
        /// Removes all strings from the list which contain another string in the list.
        /// </summary>
        private static List<String> RemoveSuperstrings(IEnumerable<String> strings)
        {
            IGrouping<int, String>[] stringsByLength = strings.Distinct().ToLookup(s => s.Length).ToArray();
            Array.Sort(stringsByLength, (group1, group2)=>group1.Key.CompareTo(group2.Key));
            
            List<String> returnedStrings = new List<string>();
            // Add strings in order from shortest to longest. 
            // Skip any string which contains a string that is already going to be return.
            foreach (IGrouping<int, String> grouping in stringsByLength)
            {
                int lastGroupEnd = returnedStrings.Count;
                if (lastGroupEnd == 0)
                {
                    returnedStrings.AddRange(grouping);
                    continue;
                }
                foreach (String nextString in grouping)
                {
                    if (returnedStrings.Take(lastGroupEnd)
                        .Any(s => nextString.IndexOf(s, StringComparison.Ordinal) >= 0))
                    {
                        continue;
                    }
                    returnedStrings.Add(nextString);
                }
            }
            return returnedStrings;
        }

        /// <summary>
        /// Use the ProteomeDbSubsequences table to find the protein ids which might contain
        /// one of the specified strings.
        /// </summary>
        private IEnumerable<long> QuerySubsequencesTableForSubstrings(IStatelessSession session, ICollection<string> sequenceList)
        {
            IQuery query = MakeQueryForSubsequencesTable(session, sequenceList);
            return query.List<byte[]>().SelectMany(DbSubsequence.BytesToProteinIds);
        }

        // ReSharper disable LocalizableElement
        private IList<Protein> GetProteinsWithIds(IStatelessSession session, IList<long> ids)
        {
            return ProteomeDb.GetProteinsWithIds(session, ids);
        }

        private IEnumerable<long> QueryProteinTableForSubstrings(IStatelessSession session, ICollection<string> sequenceList)
        {
            List<String> likeClauses = new List<string>();
            foreach (String seq in sequenceList)
            {
                likeClauses.Add("p.Sequence LIKE '%" + seq + "%'");
            }
            String hql = "SELECT p.Id FROM " + typeof(DbProtein) + " p WHERE\n"
                + String.Join("\nOR ", likeClauses);
            IQuery query = session.CreateQuery(hql);
            return query.List<long>();
        }

        private IQuery MakeQueryForSubsequencesTable(IStatelessSession session, ICollection<string> sequenceList)
        {
            String hql;
            IQuery query;
            if (sequenceList.All(s=>s.Length >= ProteomeDb.MAX_SEQUENCE_LENGTH))
            {
                hql = "SELECT ProteinIdBytes"
                    + "\nFROM " + typeof(DbSubsequence) + " ss"
                    + "\nWHERE ss.Sequence IN (:Sequences)";
                query = session.CreateQuery(hql);
                query.SetParameterList("Sequences", sequenceList);
                return query;
            }
            List<String> likeClauses = new List<string>();
            List<String> exactMatches = new List<string>();
            foreach (String seq in sequenceList)
            {
                if (seq.Length >= ProteomeDb.MAX_SEQUENCE_LENGTH)
                {
                    exactMatches.Add(seq);
                }
                else
                {
                    if (seq.Length >= ProteomeDb.MIN_SEQUENCE_LENGTH)
                    {
                        likeClauses.Add("ss.Sequence LIKE '" + seq + "%'");
                    }
                    else
                    {
                        likeClauses.Add("ss.Sequence LIKE '%" + seq + "%'");
                    }
                }
            }
            hql = "SELECT ProteinIdBytes FROM " + typeof(DbSubsequence) + " ss WHERE ";
            hql = hql + String.Join("\nOR ", likeClauses);
            if (exactMatches.Any())
            {
                hql += "\nOR ss.Sequence IN (:exactMatches)";
            }
            query = session.CreateQuery(hql);
            if (exactMatches.Any())
            {
                query.SetParameterList("exactMatches", exactMatches);
            }
            return query;
        }
        // ReSharper restore LocalizableElement
    }
}
