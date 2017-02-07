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
using NHibernate;
using NHibernate.Criterion;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.DataModel;

namespace pwiz.ProteomeDatabase.API
{
    /// <summary>
    /// Public API for the digestion of the proteome by a particular enzyme.
    /// </summary>
    public class Digestion : EntityModel<DbDigestion>
    {
        public Digestion(ProteomeDb proteomeDb, DbDigestion digestion) : base(proteomeDb, digestion)
        {
            Name = digestion.Name;
            Description = digestion.Description;
            MinSequenceLength = digestion.MinSequenceLength;
            MaxSequenceLength = digestion.MaxSequenceLength;
        }

        public String Name { get; private set; }
        public String Description { get; private set; }
        public int MinSequenceLength { get; private set; }
        public int MaxSequenceLength { get; private set; }

        public List<Protein> GetProteinsWithSequence(String sequence, ProteomeDb proteomeDb)
        {
            return GetProteinsWithSequences(new[] { sequence }, proteomeDb, null)[sequence];
        }

        public Dictionary<string, List<Protein>> GetProteinsWithSequences(IEnumerable<string> sequences,
            ProteomeDb proteomeDb, IProgressMonitor progressMonitor)
        {
            var sequenceList = sequences.ToList();
            var indexSequences = sequenceList.Select(sequence => sequence.Substring(0, Math.Min(sequence.Length, MaxSequenceLength))).Distinct().ToList();
            var results = new Dictionary<string, List<Protein>>();

            foreach (var s in sequenceList.Distinct())
            {
                results.Add(s, new List<Protein>());
            }

            using (ISession session = proteomeDb.OpenSession())
            {
                var digestion = GetEntity(session);
                var completedQueries = 0;
                for (var querySize = Math.Min(500,indexSequences.Count); querySize > 0; )  // A retry loop in case our query overwhelms SQLite
                {
                    try
                    {
                        while (completedQueries < indexSequences.Count)
                        {
                            var hql = "SELECT DISTINCT pp.Protein, pn, pp.Protein.Sequence" // Not L10N
                                      + "\nFROM " + typeof (DbDigestedPeptideProtein) + " pp, " + typeof (DbProteinName) + " pn" // Not L10N
                                      + "\nWHERE pp.Protein.Id = pn.Protein.Id AND pn.IsPrimary <> 0" // Not L10N
                                      + "\nAND pp.Peptide.Digestion = :Digestion" // Not L10N
                                      + "\nAND pp.Peptide.Sequence IN (:Sequences)"; // Not L10N
                            var query = session.CreateQuery(hql);
                            query.SetParameter("Digestion", digestion); // Not L10N
                            query.SetParameterList("Sequences", indexSequences.Skip(completedQueries).Take(querySize)); // Not L10N
                            foreach (object[] values in query.List())
                            {
                                // At this point what we have is a list of proteins which are a likely match, but not certain.
                                // That's because the indexing scheme only uses the first MaxSequenceLength AAs of a peptide.
                                var protein = (DbProtein) values[0];
                                var name = (DbProteinName) values[1];
                                var proteinSequence = (String) values[2];
                                foreach (
                                    var sequence in
                                        sequenceList.Where(
                                            seq => proteinSequence.IndexOf(seq, StringComparison.Ordinal) >= 0))
                                {
                                    // N.B. at this point all we really know is that this peptide sequence is found in this protein's sequence.
                                    // We don't really know that it's a plausible product of the current digestion.  
                                    results[sequence].Add(new Protein(ProteomeDbPath, protein, name));
                                }
                            }
                            if (progressMonitor != null && progressMonitor.IsCanceled)
                            {
                                break;
                            }
                            completedQueries += querySize;
                        }
                        break;
                    }
                    catch (Exception x)
                    {
                        // Failed - probably due to too-large query
                        querySize /= 2;
                        if (querySize == 0)
                        {
                            throw new Exception("error reading protdb file", x); // Not L10N
                        }
                    }
                }  // End dynamic query size loop
            }
            return results;
        }

        public List<KeyValuePair<Protein, String>> GetProteinsWithSequencePrefix(String sequence, int maxResults)
        {
            List<KeyValuePair<Protein,String>> proteinSequences = new List<KeyValuePair<Protein,String>>();
            using (var proteomeDb = OpenProteomeDb())
            using (ISession session = proteomeDb.OpenSession())
            {
                DbDigestion digestion = GetEntity(session);
                ICriteria criteria = session.CreateCriteria(typeof(DbDigestedPeptide))
                    .Add(Restrictions.Eq("Digestion", digestion)) // Not L10N
                    .Add(Restrictions.Like("Sequence", sequence + "%")) // Not L10N
                    .AddOrder(Order.Asc("Sequence")) // Not L10N
                    .SetMaxResults(maxResults);
                foreach (DbDigestedPeptide dbDigestedPeptide in criteria.List())
                {
                    foreach (var peptideProtein in dbDigestedPeptide.PeptideProteins)
                    {
                        proteinSequences.Add(
                            new KeyValuePair<Protein, string>(
                                new Protein(ProteomeDbPath, peptideProtein.Protein),
                                dbDigestedPeptide.Sequence));
                    }
                }
            }
            return proteinSequences;
        }
    }
}
