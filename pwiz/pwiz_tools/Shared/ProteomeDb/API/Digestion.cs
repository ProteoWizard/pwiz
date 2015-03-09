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

        public List<Protein> GetProteinsWithSequence(String sequence)
        {
            List<Protein> proteins = new List<Protein>();
            using (var proteomeDb = OpenProteomeDb())
            using (ISession session = proteomeDb.OpenSession())
            {
                DbDigestion digestion = GetEntity(session);
                String sequencePrefix = sequence.Substring(0, Math.Min(sequence.Length, MaxSequenceLength));
                String hql = "SELECT DISTINCT pp.Protein, pn" // Not L10N
                    + "\nFROM " + typeof(DbDigestedPeptideProtein) + " pp, " + typeof(DbProteinName) + " pn" // Not L10N
                    + "\nWHERE pp.Protein.Id = pn.Protein.Id AND pn.IsPrimary <> 0" // Not L10N
                    + "\nAND pp.Peptide.Digestion = :Digestion" // Not L10N
                    + "\nAND pp.Peptide.Sequence = :Sequence"; // Not L10N
                IQuery query = session.CreateQuery(hql);
                query.SetParameter("Digestion", digestion); // Not L10N
                query.SetParameter("Sequence", sequencePrefix); // Not L10N
                foreach (object[] values in query.List())
                {
                    var protein = (DbProtein)values[0];
                    var name = (DbProteinName)values[1];

                    if (protein.Sequence.IndexOf(sequence, StringComparison.Ordinal) < 0)
                        continue;

                    proteins.Add(new Protein(ProteomeDbPath, protein, name));
                }
            }
            return proteins;
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
