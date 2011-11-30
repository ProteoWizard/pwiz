//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IDPicker.DataModel;
using NHibernate.Linq;

namespace IDPicker.DataModel
{
    public class DataFilter : EventArgs
    {
        #region Events
        public event EventHandler<FilteringProgressEventArgs> FilteringProgress;
        #endregion

        #region Event arguments
        public class FilteringProgressEventArgs : System.ComponentModel.CancelEventArgs
        {
            public FilteringProgressEventArgs(string stage, int completed, Exception ex)
            {
                CompletedFilters = completed;
                TotalFilters = 16;
                FilteringStage = stage;
                FilteringException = ex;
            }

            public int CompletedFilters { get; protected set; }
            public int TotalFilters { get; protected set; }

            public string FilteringStage { get; protected set; }
            public Exception FilteringException { get; protected set; }
        }
        #endregion

        protected bool OnFilteringProgress (FilteringProgressEventArgs e)
        {
            if (FilteringProgress != null)
                FilteringProgress(this, e);
            return e.Cancel;
        }

        public DataFilter ()
        {
            MaximumQValue = 0.02;
            MinimumDistinctPeptidesPerProtein = 2;
            MinimumSpectraPerProtein = 2;
            MinimumAdditionalPeptidesPerProtein = 1;

            DistinctMatchFormat = new DistinctMatchFormat()
            {
                AreModificationsDistinct = true,
                IsAnalysisDistinct = false,
                IsChargeDistinct = true,
                ModificationMassRoundToNearest = 1m
            };
        }

        public DataFilter (DataFilter other)
        {
            MaximumQValue = other.MaximumQValue;
            MinimumDistinctPeptidesPerProtein = other.MinimumDistinctPeptidesPerProtein;
            MinimumSpectraPerProtein = other.MinimumSpectraPerProtein;
            MinimumAdditionalPeptidesPerProtein = other.MinimumAdditionalPeptidesPerProtein;
            DistinctMatchFormat = other.DistinctMatchFormat;
            Cluster = other.Cluster == null ? null : new List<int>(other.Cluster);
            ProteinGroup = other.ProteinGroup == null ? null : new List<int>(other.ProteinGroup);
            Protein = other.Protein == null ? null : new List<Protein>(other.Protein);
            Peptide = other.Peptide == null ? null : new List<Peptide>(other.Peptide);
            DistinctMatchKey = other.DistinctMatchKey == null ? null : new List<DistinctMatchKey>(other.DistinctMatchKey);
            Modifications = other.Modifications == null ? null : new List<Modification>(other.Modifications);
            ModifiedSite = other.ModifiedSite == null ? null : new List<char>(other.ModifiedSite);
            Charge = other.Charge == null ? null : new List<int>(other.Charge);
            Analysis = other.Analysis == null ? null : new List<Analysis>(other.Analysis);
            Spectrum = other.Spectrum == null ? null : new List<Spectrum>(other.Spectrum);
            SpectrumSource = other.SpectrumSource == null ? null : new List<SpectrumSource>(other.SpectrumSource);
            SpectrumSourceGroup = other.SpectrumSourceGroup == null ? null : new List<SpectrumSourceGroup>(other.SpectrumSourceGroup);
        }

        public double MaximumQValue { get; set; }
        public int MinimumDistinctPeptidesPerProtein { get; set; }
        public int MinimumSpectraPerProtein { get; set; }
        public int MinimumAdditionalPeptidesPerProtein { get; set; }

        public DistinctMatchFormat DistinctMatchFormat { get; set; }

        public IList<int> Cluster { get; set; }
        public IList<int> ProteinGroup { get; set; }
        public IList<int> PeptideGroup { get; set; }
        public IList<Protein> Protein { get; set; }
        public IList<Peptide> Peptide { get; set; }
        public IList<DistinctMatchKey> DistinctMatchKey { get; set; }
        public IList<Modification> Modifications { get; set; }
        public IList<char> ModifiedSite { get; set; }
        public IList<int> Charge { get; set; }
        public IList<SpectrumSourceGroup> SpectrumSourceGroup { get; set; }
        public IList<SpectrumSource> SpectrumSource { get; set; }
        public IList<Spectrum> Spectrum { get; set; }
        public IList<Analysis> Analysis { get; set; }

        /// <summary>
        /// A regular expression for filtering peptide sequences based on amino-acid composition.
        /// </summary>
        /// <example>To match peptides with at least one histidine: "*H*"</example>
        /// <example>To match peptides with at least two histidines: "*H*H*</example>
        /// <example>To match peptides with two adjacent histidines: "*HH*</example>
        /// <example>To match peptides starting with glutamine: "Q*"</example>
        /// <example>To match peptides with a  histidines: "*H*H*</example>
        public string Composition { get; set; }

        public object FilterSource { get; set; }

        public bool IsBasicFilter
        {
            get
            {
                return Cluster == null && ProteinGroup == null && Protein == null &&
                       Peptide == null && DistinctMatchKey == null &&
                       Modifications == null && ModifiedSite == null && Charge == null &&
                       SpectrumSource == null && Spectrum == null && Analysis == null &&
                       String.IsNullOrEmpty(Composition);
            }
        }

        private static bool NullSafeSequenceEqual<T> (IEnumerable<T> lhs, IEnumerable<T> rhs)
        {
            if (lhs == rhs)
                return true;
            else if (lhs == null || rhs == null)
                return false;
            else
                return lhs.SequenceEqual<T>(rhs);
        }

        private static IList<T> NullSafeSequenceUnion<T> (IEnumerable<T> lhs, IEnumerable<T> rhs)
        {
            if (lhs == null && rhs == null)
                return null;
            else if (lhs == null)
                return rhs.ToList();
            else if (rhs == null)
                return lhs.ToList();
            else if (lhs == rhs)
                return lhs.ToList();
            else
                return lhs.Union<T>(rhs).ToList();
        }

        public override int GetHashCode () { return (this as object).GetHashCode(); }

        public override bool Equals (object obj)
        {
            var other = obj as DataFilter;
            if (other == null)
                return false;
            else if (object.ReferenceEquals(this, obj))
                return true;

            return MaximumQValue == other.MaximumQValue &&
                   MinimumDistinctPeptidesPerProtein == other.MinimumDistinctPeptidesPerProtein &&
                   MinimumSpectraPerProtein == other.MinimumSpectraPerProtein &&
                   MinimumAdditionalPeptidesPerProtein == other.MinimumAdditionalPeptidesPerProtein &&
                   NullSafeSequenceEqual(Cluster, other.Cluster) &&
                   NullSafeSequenceEqual(ProteinGroup, other.ProteinGroup) &&
                   NullSafeSequenceEqual(PeptideGroup, other.PeptideGroup) &&
                   NullSafeSequenceEqual(Protein, other.Protein) &&
                   NullSafeSequenceEqual(Peptide, other.Peptide) &&
                   NullSafeSequenceEqual(DistinctMatchKey, other.DistinctMatchKey) &&
                   NullSafeSequenceEqual(Modifications, other.Modifications) &&
                   NullSafeSequenceEqual(ModifiedSite, other.ModifiedSite) &&
                   NullSafeSequenceEqual(Charge, other.Charge) &&
                   NullSafeSequenceEqual(Analysis, other.Analysis) &&
                   NullSafeSequenceEqual(Spectrum, other.Spectrum) &&
                   NullSafeSequenceEqual(SpectrumSource, other.SpectrumSource) &&
                   NullSafeSequenceEqual(SpectrumSourceGroup, other.SpectrumSourceGroup) &&
                   Composition == other.Composition;
        }

        public static DataFilter operator + (DataFilter lhs, DataFilter rhs)
        {
            var newFilter = new DataFilter(lhs);
            newFilter.Cluster = NullSafeSequenceUnion(newFilter.Cluster, rhs.Cluster);
            newFilter.ProteinGroup = NullSafeSequenceUnion(newFilter.ProteinGroup, rhs.ProteinGroup);
            newFilter.PeptideGroup = NullSafeSequenceUnion(newFilter.ProteinGroup, rhs.PeptideGroup);
            newFilter.Protein = NullSafeSequenceUnion(newFilter.Protein, rhs.Protein);
            newFilter.Peptide = NullSafeSequenceUnion(newFilter.Peptide, rhs.Peptide);
            newFilter.DistinctMatchKey = NullSafeSequenceUnion(newFilter.DistinctMatchKey, rhs.DistinctMatchKey);
            newFilter.Modifications = NullSafeSequenceUnion(newFilter.Modifications, rhs.Modifications);
            newFilter.ModifiedSite = NullSafeSequenceUnion(newFilter.ModifiedSite, rhs.ModifiedSite);
            newFilter.Charge = NullSafeSequenceUnion(newFilter.Charge, rhs.Charge);
            newFilter.Analysis = NullSafeSequenceUnion(newFilter.Analysis, rhs.Analysis);
            newFilter.Spectrum = NullSafeSequenceUnion(newFilter.Spectrum, rhs.Spectrum);
            newFilter.SpectrumSource = NullSafeSequenceUnion(newFilter.SpectrumSource, rhs.SpectrumSource);
            newFilter.SpectrumSourceGroup = NullSafeSequenceUnion(newFilter.SpectrumSourceGroup, rhs.SpectrumSourceGroup);
            newFilter.Composition = rhs.Composition;
            return newFilter;
        }

        public static bool operator == (DataFilter lhs, DataFilter rhs) { return object.ReferenceEquals(lhs, null) ? object.ReferenceEquals(rhs, null) : lhs.Equals(rhs); }
        public static bool operator != (DataFilter lhs, DataFilter rhs) { return !(lhs == rhs); }

        public override string ToString ()
        {
            if (Cluster != null)
                return "Cluster (" + Cluster.Count + ")";
            if (ProteinGroup != null)
                return "Protein Group (" + ProteinGroup.Count + ")";
            if (PeptideGroup != null)
                return "Peptide Group (" + PeptideGroup.Count + ")";
            if (Protein != null)
                return "Protein (" + Protein.Count + ")";
            if (Peptide != null)
                return "Peptide (" + Peptide.Count + ")";
            if (DistinctMatchKey != null)
                return "Distinct Match (" + DistinctMatchKey.Count + ")";
            if (SpectrumSourceGroup != null)
                return "Group (" + SpectrumSourceGroup.Count + ")";
            if (SpectrumSource != null)
                return "Source (" + SpectrumSource.Count + ")";
            if (Spectrum != null)
                return "Spectrum (" + Spectrum.Count + ")";
            if (Analysis != null)
                return "Analysis (" + Analysis.Count + ")";
            if (Charge != null)
                return "Charge (" + Charge.Count + ")";
            if (!String.IsNullOrEmpty(Composition))
                return "Composition (" + Composition + ")";

            if (ModifiedSite == null && Modifications == null)
                return String.Format("Q-value ≤ {0}; " +
                                     "Min. distinct peptides per protein ≥ {1}; " +
                                     "Min. spectra per protein ≥ {2}; ",
                                     "Min. additional peptides per protein ≥ {3}",
                                     MaximumQValue,
                                     MinimumDistinctPeptidesPerProtein,
                                     MinimumSpectraPerProtein,
                                     MinimumAdditionalPeptidesPerProtein);
            else
            {
                var result = new StringBuilder();

                if (ModifiedSite != null)
                    result.AppendFormat("Modified site ({0})", ModifiedSite.Count);

                if (Modifications == null)
                    return result.ToString();

                if (ModifiedSite != null)
                    result.Append("; ");

                var distinctModMasses = (from mod in Modifications select Math.Round(mod.MonoMassDelta).ToString()).Distinct();
                result.AppendFormat("Mass shift ({0})", distinctModMasses.Count());
                return result.ToString();
            }
        }

        public static DataFilter LoadFilter (NHibernate.ISession session)
        {
            try
            {
                var filteringCriteria = session.CreateSQLQuery(@"SELECT MaximumQValue,
                                                                        MinimumDistinctPeptidesPerProtein,
                                                                        MinimumSpectraPerProtein,
                                                                        MinimumAdditionalPeptidesPerProtein
                                                                 FROM FilteringCriteria
                                                                ").List<object[]>()[0];
                var dataFilter = new DataFilter();
                dataFilter.MaximumQValue = Convert.ToDouble(filteringCriteria[0]);
                dataFilter.MinimumDistinctPeptidesPerProtein = Convert.ToInt32(filteringCriteria[1]);
                dataFilter.MinimumSpectraPerProtein = Convert.ToInt32(filteringCriteria[2]);
                dataFilter.MinimumAdditionalPeptidesPerProtein = Convert.ToInt32(filteringCriteria[3]);
                return dataFilter;
            }
            catch
            {
                return null;
            }
        }

        static string saveFilterSql = @"DROP TABLE IF EXISTS FilteringCriteria;
                                        CREATE TABLE IF NOT EXISTS FilteringCriteria
                                        (
                                         MaximumQValue NUMERIC,
                                         MinimumDistinctPeptidesPerProtein INT,
                                         MinimumSpectraPerProtein INT,
                                         MinimumAdditionalPeptidesPerProtein INT
                                        );
                                        INSERT INTO FilteringCriteria SELECT {0}, {1}, {2}, {3}
                                       ";
        private void SaveFilter(NHibernate.ISession session)
        {
            session.CreateSQLQuery(String.Format(saveFilterSql,
                                                 MaximumQValue,
                                                 MinimumDistinctPeptidesPerProtein,
                                                 MinimumSpectraPerProtein,
                                                 MinimumAdditionalPeptidesPerProtein)).ExecuteUpdate();
        }

        public string GetBasicQueryStringSQL ()
        {
            return String.Format("FROM PeptideSpectrumMatch psm " +
                                 "JOIN PeptideInstance pi ON psm.Peptide = pi.Peptide " +
                                 "WHERE psm.QValue <= {0} " +
                                 "GROUP BY pi.Protein " +
                                 "HAVING {1} <= COUNT(DISTINCT (psm.Peptide || ' ' || psm.MonoisotopicMass || ' ' || psm.Charge)) AND " +
                                 "       {2} <= COUNT(DISTINCT psm.Spectrum)",
                                 MaximumQValue,
                                 MinimumDistinctPeptidesPerProtein,
                                 MinimumSpectraPerProtein);
        }

        public static void DropFilters (System.Data.IDbConnection conn)
        {
            // ignore errors if main tables haven't been created yet

            #region Drop Filtered* tables
            conn.ExecuteNonQuery(@"DROP TABLE IF EXISTS FilteredProtein;
                                   DROP TABLE IF EXISTS FilteredPeptideInstance;
                                   DROP TABLE IF EXISTS FilteredPeptide;
                                   DROP TABLE IF EXISTS FilteredPeptideSpectrumMatch
                                  ");
            #endregion

            #region Restore Unfiltered* tables as the main tables
            try
            {
                // if unfiltered tables have not been created, this will throw and skip the rest of the block
                conn.ExecuteNonQuery("SELECT Id FROM UnfilteredProtein LIMIT 1");

                // drop filtered tables
                conn.ExecuteNonQuery(@"DROP TABLE IF EXISTS Protein;
                                       DROP TABLE IF EXISTS PeptideInstance;
                                       DROP TABLE IF EXISTS Peptide;
                                       DROP TABLE IF EXISTS PeptideSpectrumMatch
                                      ");

                // rename unfiltered tables 
                conn.ExecuteNonQuery(@"ALTER TABLE UnfilteredProtein RENAME TO Protein;
                                       ALTER TABLE UnfilteredPeptideInstance RENAME TO PeptideInstance;
                                       ALTER TABLE UnfilteredPeptide RENAME TO Peptide;
                                       ALTER TABLE UnfilteredPeptideSpectrumMatch RENAME TO PeptideSpectrumMatch
                                      ");

                // reset QValues
                //conn.ExecuteNonQuery("UPDATE PeptideSpectrumMatch SET QValue = 2");
            }
            catch
            {
            }
            #endregion
        }

        public void DropFilters (NHibernate.ISession session)
        {
            DropFilters(session.Connection);
        }

        public void ApplyBasicFilters (NHibernate.ISession session)
        {
            // free up memory
            session.Clear();

            bool useScopedTransaction = !session.Transaction.IsActive;
            if (useScopedTransaction)
                session.Transaction.Begin();

            int stepsCompleted = 0;

            if (OnFilteringProgress(new FilteringProgressEventArgs("Dropping current filters...", ++stepsCompleted, null)))
                return;

            DropFilters(session);

            #region Create Filtered* tables by applying the basic filters to the main tables
            if (OnFilteringProgress(new FilteringProgressEventArgs("Filtering proteins...", ++stepsCompleted, null)))
                return;
            string filterProteinsSql =
                @"CREATE TABLE FilteredProtein (Id INTEGER PRIMARY KEY, Accession TEXT, IsDecoy INT, Cluster INT, ProteinGroup INT, Length INT);
                  INSERT INTO FilteredProtein SELECT pro.*
                  FROM PeptideSpectrumMatch psm
                  JOIN PeptideInstance pi ON psm.Peptide = pi.Peptide
                  JOIN Protein pro ON pi.Protein = pro.Id
                  JOIN Spectrum s ON psm.Spectrum = s.Id
                  JOIN SpectrumSource ss ON s.Source = ss.Id
                  -- filter out ungrouped spectrum sources
                  WHERE ss.Group_ AND {0} >= psm.QValue AND psm.Rank = 1
                  GROUP BY pi.Protein
                  HAVING {1} <= COUNT(DISTINCT psm.Peptide) AND
                         {2} <= COUNT(DISTINCT psm.Spectrum);
                  CREATE UNIQUE INDEX FilteredProtein_Accession ON FilteredProtein (Accession);";
            session.CreateSQLQuery(String.Format(filterProteinsSql,
                                                 MaximumQValue,
                                                 MinimumDistinctPeptidesPerProtein,
                                                 MinimumSpectraPerProtein)).ExecuteUpdate();

            if (OnFilteringProgress(new FilteringProgressEventArgs("Filtering peptide spectrum matches...", ++stepsCompleted, null)))
                return;
            session.CreateSQLQuery(@"CREATE TABLE FilteredPeptideSpectrumMatch (Id INTEGER PRIMARY KEY, Spectrum INT, Analysis INT, Peptide INT, QValue NUMERIC, MonoisotopicMass NUMERIC, MolecularWeight NUMERIC, MonoisotopicMassError NUMERIC, MolecularWeightError NUMERIC, Rank INT, Charge INT);
                                     INSERT INTO FilteredPeptideSpectrumMatch SELECT psm.*
                                     FROM FilteredProtein pro
                                     JOIN PeptideInstance pi ON pro.Id = pi.Protein
                                     JOIN PeptideSpectrumMatch psm ON pi.Peptide = psm.Peptide
                                     JOIN Spectrum s ON psm.Spectrum = s.Id
                                     JOIN SpectrumSource ss ON s.Source = ss.Id
                                     -- filter out ungrouped spectrum sources
                                     WHERE ss.Group_ AND " + MaximumQValue.ToString() + @" >= psm.QValue AND psm.Rank = 1
                                     GROUP BY psm.Id;
                                     CREATE INDEX FilteredPeptideSpectrumMatch_Spectrum ON FilteredPeptideSpectrumMatch (Spectrum);
                                     CREATE INDEX FilteredPeptideSpectrumMatch_Peptide ON FilteredPeptideSpectrumMatch (Peptide);
                                     CREATE INDEX FilteredPeptideSpectrumMatch_QValue ON FilteredPeptideSpectrumMatch (QValue);
                                     CREATE INDEX FilteredPeptideSpectrumMatch_Analysis ON FilteredPeptideSpectrumMatch (Analysis);
                                     CREATE INDEX FilteredPeptideSpectrumMatch_Rank ON FilteredPeptideSpectrumMatch (Rank);
                                    "
                                  ).ExecuteUpdate();

            if (OnFilteringProgress(new FilteringProgressEventArgs("Filtering peptides...", ++stepsCompleted, null)))
                return;
            session.CreateSQLQuery(@"CREATE TABLE FilteredPeptide (Id INTEGER PRIMARY KEY, MonoisotopicMass NUMERIC, MolecularWeight NUMERIC, PeptideGroup INT, DecoySequence TEXT);
                                     INSERT INTO FilteredPeptide SELECT pep.*
                                     FROM FilteredPeptideSpectrumMatch psm
                                     JOIN Peptide pep ON psm.Peptide = pep.Id
                                     GROUP BY pep.Id"
                                  ).ExecuteUpdate();

            if (OnFilteringProgress(new FilteringProgressEventArgs("Filtering peptide instances...", ++stepsCompleted, null)))
                return;
            session.CreateSQLQuery(@"CREATE TABLE FilteredPeptideInstance (Id INTEGER PRIMARY KEY, Protein INT, Peptide INT, Offset INT, Length INT, NTerminusIsSpecific INT, CTerminusIsSpecific INT, MissedCleavages INT);
                                     INSERT INTO FilteredPeptideInstance SELECT pi.*
                                     FROM FilteredPeptide pep
                                     JOIN PeptideInstance pi ON pep.Id = pi.Peptide
                                     JOIN FilteredProtein pro ON pi.Protein = pro.Id;
                                     CREATE INDEX FilteredPeptideInstance_Protein ON FilteredPeptideInstance (Protein);
                                     CREATE INDEX FilteredPeptideInstance_Peptide ON FilteredPeptideInstance (Peptide);
                                     CREATE INDEX FilteredPeptideInstance_PeptideProtein ON FilteredPeptideInstance (Peptide, Protein);
                                     CREATE INDEX FilteredPeptideInstance_ProteinOffsetLength ON FilteredPeptideInstance (Protein, Offset, Length);"
                                  ).ExecuteUpdate();
            #endregion

            #region Rename main tables to Unfiltered*
            session.CreateSQLQuery(@"ALTER TABLE Protein RENAME TO UnfilteredProtein;
                                     ALTER TABLE PeptideInstance RENAME TO UnfilteredPeptideInstance;
                                     ALTER TABLE Peptide RENAME TO UnfilteredPeptide;
                                     ALTER TABLE PeptideSpectrumMatch RENAME TO UnfilteredPeptideSpectrumMatch
                                    ").ExecuteUpdate();
            #endregion

            #region Rename Filtered* tables to main tables
            session.CreateSQLQuery(@"ALTER TABLE FilteredProtein RENAME TO Protein;
                                     ALTER TABLE FilteredPeptideInstance RENAME TO PeptideInstance;
                                     ALTER TABLE FilteredPeptide RENAME TO Peptide;
                                     ALTER TABLE FilteredPeptideSpectrumMatch RENAME TO PeptideSpectrumMatch
                                    ").ExecuteUpdate();
            #endregion

            if (AssembleProteinGroups(session, ref stepsCompleted)) return;
            if (ApplyAdditionalPeptidesFilter(session, ref stepsCompleted)) return;
            if (AssembleClusters(session, ref stepsCompleted)) return;
            if (AssembleProteinCoverage(session, ref stepsCompleted)) return;
            if (AssembleDistinctMatches(session, ref stepsCompleted)) return;

            // assemble new protein groups after the additional peptides filter
            session.CreateSQLQuery("DROP INDEX Protein_ProteinGroup").ExecuteUpdate();
            if (AssembleProteinGroups(session, ref stepsCompleted)) return;
            if (AssemblePeptideGroups(session, ref stepsCompleted)) return;

            SaveFilter(session);

            if (useScopedTransaction)
                session.Transaction.Commit();
        }

        #region Implementation of basic filters
        /// <summary>
        /// Set ProteinGroup column (the groups change depending on the basic filters applied)
        /// </summary>
        bool AssembleProteinGroups(NHibernate.ISession session, ref int stepsCompleted)
        {
            if (OnFilteringProgress(new FilteringProgressEventArgs("Assembling protein groups...", ++stepsCompleted, null)))
                return true;

            session.CreateSQLQuery(@"CREATE TEMP TABLE ProteinGroups AS
                                     SELECT pro.Id AS ProteinId, GROUP_CONCAT(DISTINCT pi.Peptide) AS ProteinGroup
                                     FROM PeptideInstance pi
                                     JOIN Protein pro ON pi.Protein = pro.Id
                                     GROUP BY pi.Protein;

                                     -- ProteinGroup will be a continuous sequence starting at 1
                                     CREATE TEMP TABLE TempProtein AS
                                     SELECT ProteinId, Accession, IsDecoy, Cluster, pg2.rowid, Length
                                     FROM ProteinGroups pg
                                     JOIN ( 
                                           SELECT pg.ProteinGroup
                                           FROM ProteinGroups pg
                                           GROUP BY pg.ProteinGroup
                                          ) pg2 ON pg.ProteinGroup = pg2.ProteinGroup
                                     JOIN Protein pro ON pg.ProteinId = pro.Id;

                                     DELETE FROM Protein;
                                     INSERT INTO Protein SELECT * FROM TempProtein;
                                     CREATE INDEX Protein_ProteinGroup ON Protein (ProteinGroup);
                                     DROP TABLE ProteinGroups;
                                     DROP TABLE TempProtein;
                                    ").ExecuteUpdate();
            session.Clear();

            return false;
        }

        /// <summary>
        /// Set PeptideGroup column (the groups change depending on the basic filters applied)
        /// </summary>
        bool AssemblePeptideGroups (NHibernate.ISession session, ref int stepsCompleted)
        {
            if (OnFilteringProgress(new FilteringProgressEventArgs("Assembling peptide groups...", ++stepsCompleted, null)))
                return true;

            session.CreateSQLQuery(@"CREATE TEMP TABLE PeptideGroups AS
                                     SELECT pep.Id AS PeptideId, GROUP_CONCAT(DISTINCT pi.Protein) AS PeptideGroup
                                     FROM PeptideInstance pi
                                     JOIN Peptide pep ON pi.Peptide=pep.Id
                                     GROUP BY pi.Peptide;

                                     -- PeptideGroup will be a continuous sequence starting at 1
                                     CREATE TEMP TABLE TempPeptide AS
                                     SELECT PeptideId, MonoisotopicMass, MolecularWeight, pg2.rowid, DecoySequence
                                     FROM PeptideGroups pg
                                     JOIN ( 
                                           SELECT pg.PeptideGroup
                                           FROM PeptideGroups pg
                                           GROUP BY pg.PeptideGroup
                                          ) pg2 ON pg.PeptideGroup = pg2.PeptideGroup
                                     JOIN Peptide pro ON pg.PeptideId = pro.Id;

                                     DELETE FROM Peptide;
                                     INSERT INTO Peptide SELECT * FROM TempPeptide;
                                     CREATE INDEX Peptide_PeptideGroup ON Peptide (PeptideGroup);
                                     DROP TABLE PeptideGroups;
                                     DROP TABLE TempPeptide;
                                    ").ExecuteUpdate();
            session.Clear();

            return false;
        }

        /// <summary>
        /// Calculate additional peptides per protein and filter out proteins that don't meet the minimum
        /// </summary>
        bool ApplyAdditionalPeptidesFilter (NHibernate.ISession session, ref int stepsCompleted)
        {
            if (MinimumAdditionalPeptidesPerProtein == 0)
                return false;

            if (OnFilteringProgress(new FilteringProgressEventArgs("Calculating additional peptide counts...", ++stepsCompleted, null)))
                return true;

            Map<long, long> additionalPeptidesByProteinId = CalculateAdditionalPeptides(session);

            session.CreateSQLQuery(@"DROP TABLE IF EXISTS AdditionalMatches;
                                     CREATE TABLE AdditionalMatches (ProteinId INTEGER PRIMARY KEY, AdditionalMatches INT)
                                    ").ExecuteUpdate();

            var cmd = session.Connection.CreateCommand();
            cmd.CommandText = "INSERT INTO AdditionalMatches VALUES (?, ?)";
            var parameters = new List<System.Data.IDbDataParameter>();
            for (int i = 0; i < 2; ++i)
            {
                parameters.Add(cmd.CreateParameter());
                cmd.Parameters.Add(parameters[i]);
            }
            cmd.Prepare();
            foreach (Map<long, long>.MapPair itr in additionalPeptidesByProteinId)
            {
                parameters[0].Value = itr.Key;
                parameters[1].Value = itr.Value;
                cmd.ExecuteNonQuery();
            }

            if (OnFilteringProgress(new FilteringProgressEventArgs("Filtering by additional peptide count...", ++stepsCompleted, null)))
                return true;

            // delete proteins that don't meet the additional matches filter
            // delete peptide instances whose protein is gone
            // delete peptides that no longer have any peptide instances
            // delete PSMs whose peptide is gone
            string additionalPeptidesDeleteSql = @"DELETE FROM Protein
                                                         WHERE Id IN (SELECT pro.Id
                                                                      FROM Protein pro
                                                                      JOIN AdditionalMatches am ON pro.Id = am.ProteinId
                                                                      WHERE am.AdditionalMatches < {0});
                                                   DELETE FROM PeptideInstance WHERE Protein NOT IN (SELECT Id FROM Protein);
                                                   DELETE FROM Peptide WHERE Id NOT IN (SELECT Peptide FROM PeptideInstance);
                                                   DELETE FROM PeptideSpectrumMatch WHERE Peptide NOT IN (SELECT Id FROM Peptide);
                                                  ";

            session.CreateSQLQuery(String.Format(additionalPeptidesDeleteSql, MinimumAdditionalPeptidesPerProtein)).ExecuteUpdate();

            return false;
        }

        /// <summary>
        /// Calculate clusters (connected components) for proteins
        /// </summary>
        bool AssembleClusters (NHibernate.ISession session, ref int stepsCompleted)
        {
            if (OnFilteringProgress(new FilteringProgressEventArgs("Calculating protein clusters...", ++stepsCompleted, null)))
                return true;

            Map<long, long> clusterByProteinId = calculateProteinClusters(session);

            if (OnFilteringProgress(new FilteringProgressEventArgs("Assigning proteins to clusters...", ++stepsCompleted, null)))
                return true;

            var cmd = session.Connection.CreateCommand();
            cmd.CommandText = "UPDATE Protein SET Cluster = ? WHERE Id = ?";
            var parameters = new List<System.Data.IDbDataParameter>();
            for (int i = 0; i < 2; ++i)
            {
                parameters.Add(cmd.CreateParameter());
                cmd.Parameters.Add(parameters[i]);
            }
            cmd.Prepare();
            foreach (Map<long, long>.MapPair itr in clusterByProteinId)
            {
                parameters[0].Value = itr.Value;
                parameters[1].Value = itr.Key;
                cmd.ExecuteNonQuery();
            }
            cmd.ExecuteNonQuery("CREATE INDEX Protein_Cluster ON Protein (Cluster)");

            return false;
        }

        /// <summary>
        /// Calculate coverage and coverage masks for proteins
        /// </summary>
        bool AssembleProteinCoverage (NHibernate.ISession session, ref int stepsCompleted)
        {
            if (OnFilteringProgress(new FilteringProgressEventArgs("Calculating protein coverage...", ++stepsCompleted, null)))
                return true;
            
            session.CreateSQLQuery(@"DELETE FROM ProteinCoverage;
                                     INSERT INTO ProteinCoverage (Id, Coverage)
                                     SELECT pi.Protein, CAST(COUNT(DISTINCT i.Value) AS REAL) * 100 / pro.Length
                                     FROM PeptideInstance pi
                                     JOIN Protein pro ON pi.Protein=pro.Id
                                     JOIN ProteinData pd ON pi.Protein=pd.Id
                                     JOIN IntegerSet i
                                     WHERE i.Value BETWEEN pi.Offset AND pi.Offset+pi.Length-1
                                     GROUP BY pi.Protein;
                                    ").ExecuteUpdate();

            if (OnFilteringProgress(new FilteringProgressEventArgs("Calculating protein coverage masks...", ++stepsCompleted, null)))
                return true;

            // get non-zero coverage depths at each protein offset
            var coverageMaskRows = session.CreateSQLQuery(
                                   @"SELECT pi.Protein, pro.Length, i.Value, COUNT(i.Value)
                                     FROM PeptideInstance pi
                                     JOIN Protein pro ON pi.Protein=pro.Id
                                     JOIN ProteinData pd ON pi.Protein=pd.Id
                                     JOIN IntegerSet i 
                                     WHERE i.Value BETWEEN pi.Offset AND pi.Offset+pi.Length-1
                                     GROUP BY pi.Protein, i.Value
                                     ORDER BY pi.Protein, i.Value;
                                    ").List().OfType<object[]>();

            if (OnFilteringProgress(new FilteringProgressEventArgs("Updating protein coverage masks...", ++stepsCompleted, null)))
                return true;

            var cmd = session.Connection.CreateCommand();
            cmd.CommandText = "UPDATE ProteinCoverage SET CoverageMask = ? WHERE Id = ?";
            var parameters = new List<System.Data.IDbDataParameter>();
            for (int i = 0; i < 2; ++i)
            {
                parameters.Add(cmd.CreateParameter());
                cmd.Parameters.Add(parameters[i]);
            }
            cmd.Prepare();

            var proteinCoverageMaskUserType = new ProteinCoverageMaskUserType();
            long currentProteinId = 0;
            ushort[] currentProteinMask = null;

            foreach (object[] row in coverageMaskRows)
            {
                long proteinId = Convert.ToInt64(row[0]);
                int proteinLength = Convert.ToInt32(row[1]);

                // before moving on to the next protein, update the current one
                if (proteinId > currentProteinId)
                {
                    if (currentProteinMask != null)
                    {
                        parameters[0].Value = proteinCoverageMaskUserType.Disassemble(currentProteinMask);
                        parameters[1].Value = currentProteinId;
                        cmd.ExecuteNonQuery();
                    }

                    currentProteinId = proteinId;
                    currentProteinMask = new ushort[proteinLength];

                    // initialize all offsets to 0 (no coverage)
                    currentProteinMask.Initialize();
                }

                // set a covered offset to its coverage depth
                currentProteinMask[Convert.ToInt32(row[2])] = Convert.ToUInt16(row[3]);
            }

            // set the last protein's mask
            if (currentProteinMask != null)
            {
                parameters[0].Value = proteinCoverageMaskUserType.Disassemble(currentProteinMask);
                parameters[1].Value = currentProteinId;
                cmd.ExecuteNonQuery();
            }

            return false;
        }

        bool AssembleDistinctMatches (NHibernate.ISession session, ref int stepsCompleted)
        {
            if (OnFilteringProgress(new FilteringProgressEventArgs("Assembling distinct matches...", ++stepsCompleted, null)))
                return true;
            string sql = String.Format(@"DROP TABLE IF EXISTS DistinctMatch;
                                         CREATE TABLE DistinctMatch (PsmId INTEGER PRIMARY KEY, DistinctMatchKey TEXT);
                                         INSERT INTO DistinctMatch (PsmId, DistinctMatchKey)
                                         SELECT DISTINCT psm.Id, {0}
                                         FROM PeptideSpectrumMatch psm;
                                        ", DistinctMatchFormat.SqlExpression);
            session.CreateSQLQuery(sql).ExecuteUpdate();
            return false;
        }
        #endregion

        #region Definitions for common HQL strings
        public static readonly string FromProtein = "Protein pro";
        public static readonly string FromPeptide = "Peptide pep";
        public static readonly string FromPeptideSpectrumMatch = "PeptideSpectrumMatch psm";
        public static readonly string FromPeptideInstance = "PeptideInstance pi";
        public static readonly string FromPeptideModification = "PeptideModification pm";
        public static readonly string FromModification = "Modification mod";
        public static readonly string FromAnalysis = "Analysis a";
        public static readonly string FromSpectrum = "Spectrum s";
        public static readonly string FromSpectrumSource = "SpectrumSource ss";
        public static readonly string FromSpectrumSourceGroupLink = "SpectrumSourceGroupLink ssgl";
        public static readonly string FromSpectrumSourceGroup = "SpectrumSourceGroup ssg";

        public static readonly string ProteinToPeptideInstance = "JOIN pro.Peptides pi";
        public static readonly string ProteinToPeptide = ProteinToPeptideInstance + ";JOIN pi.Peptide pep";
        public static readonly string ProteinToPeptideSpectrumMatch = ProteinToPeptide + ";JOIN pep.Matches psm";
        public static readonly string ProteinToPeptideModification = ProteinToPeptideSpectrumMatch + ";LEFT JOIN psm.Modifications pm";
        public static readonly string ProteinToModification = ProteinToPeptideModification + ";LEFT JOIN pm.Modification mod";
        public static readonly string ProteinToAnalysis = ProteinToPeptideSpectrumMatch + ";JOIN psm.Analysis a";
        public static readonly string ProteinToSpectrum = ProteinToPeptideSpectrumMatch + ";JOIN psm.Spectrum s";
        public static readonly string ProteinToSpectrumSource = ProteinToSpectrum + ";JOIN s.Source ss";
        public static readonly string ProteinToSpectrumSourceGroupLink = ProteinToSpectrumSource + ";JOIN ss.Groups ssgl";
        public static readonly string ProteinToSpectrumSourceGroup = ProteinToSpectrumSourceGroupLink + ";JOIN ssgl.Group ssg";

        public static readonly string PeptideToPeptideInstance = "JOIN pep.Instances pi";
        public static readonly string PeptideToPeptideSpectrumMatch = "JOIN pep.Matches psm";
        public static readonly string PeptideToProtein = PeptideToPeptideInstance + ";JOIN pi.Protein pro";
        public static readonly string PeptideToPeptideModification = PeptideToPeptideSpectrumMatch + ";LEFT JOIN psm.Modifications pm";
        public static readonly string PeptideToModification = PeptideToPeptideModification + ";LEFT JOIN pm.Modification mod";
        public static readonly string PeptideToAnalysis = PeptideToPeptideSpectrumMatch + ";JOIN psm.Analysis a";
        public static readonly string PeptideToSpectrum = PeptideToPeptideSpectrumMatch + ";JOIN psm.Spectrum s";
        public static readonly string PeptideToSpectrumSource = PeptideToSpectrum + ";JOIN s.Source ss";
        public static readonly string PeptideToSpectrumSourceGroupLink = PeptideToSpectrumSource + ";JOIN ss.Groups ssgl";
        public static readonly string PeptideToSpectrumSourceGroup = PeptideToSpectrumSourceGroupLink + ";JOIN ssgl.Group ssg";

        public static readonly string PeptideSpectrumMatchToPeptide = "JOIN psm.Peptide pep";
        public static readonly string PeptideSpectrumMatchToAnalysis = "JOIN psm.Analysis a";
        public static readonly string PeptideSpectrumMatchToSpectrum = "JOIN psm.Spectrum s";
        public static readonly string PeptideSpectrumMatchToPeptideModification = "LEFT JOIN psm.Modifications pm";
        public static readonly string PeptideSpectrumMatchToPeptideInstance = PeptideSpectrumMatchToPeptide + ";JOIN pep.Instances pi";
        public static readonly string PeptideSpectrumMatchToProtein = PeptideSpectrumMatchToPeptideInstance + ";JOIN pi.Protein pro";
        public static readonly string PeptideSpectrumMatchToModification = PeptideSpectrumMatchToPeptideModification + ";LEFT JOIN pm.Modification mod";
        public static readonly string PeptideSpectrumMatchToSpectrumSource = PeptideSpectrumMatchToSpectrum + ";JOIN s.Source ss";
        public static readonly string PeptideSpectrumMatchToSpectrumSourceGroupLink = PeptideSpectrumMatchToSpectrumSource + ";JOIN ss.Groups ssgl";
        public static readonly string PeptideSpectrumMatchToSpectrumSourceGroup = PeptideSpectrumMatchToSpectrumSourceGroupLink + ";JOIN ssgl.Group ssg";
        #endregion

        public string GetFilteredQueryString (string fromTable, params string[] joinTables)
        {
            var joins = new Map<int, object>();
            foreach (var join in joinTables)
                foreach (var branch in join.ToString().Split(';'))
                    joins.Add(joins.Count, branch);

            // these different condition sets are AND'd together, but within each set they are OR'd
            var proteinConditions = new List<string>();
            var peptideConditions = new List<string>();
            var spectrumConditions = new List<string>();
            var modConditions = new List<string>();
            var otherConditions = new List<string>();

            if (fromTable == FromProtein)
            {
                if (Peptide != null)
                    foreach (var branch in ProteinToPeptideInstance.Split(';'))
                        joins.Add(joins.Count, branch);

                if (PeptideGroup != null)
                    foreach (var branch in ProteinToPeptide.Split(';'))
                        joins.Add(joins.Count, branch);

                if (Modifications != null || ModifiedSite != null)
                    foreach (var branch in ProteinToPeptideModification.Split(';'))
                        joins.Add(joins.Count, branch);

                if (DistinctMatchKey != null || Analysis != null || Spectrum != null || Charge != null)
                    foreach (var branch in ProteinToPeptideSpectrumMatch.Split(';'))
                        joins.Add(joins.Count, branch);

                if (SpectrumSource != null)
                    foreach (var branch in ProteinToSpectrumSource.Split(';'))
                        joins.Add(joins.Count, branch);

                if (SpectrumSourceGroup != null)
                    foreach (var branch in ProteinToSpectrumSourceGroupLink.Split(';'))
                        joins.Add(joins.Count, branch);
            }
            else if (fromTable == FromPeptideSpectrumMatch)
            {
                if (Cluster != null || ProteinGroup != null)
                    foreach (var branch in PeptideSpectrumMatchToProtein.Split(';'))
                        joins.Add(joins.Count, branch);

                if (Protein != null)
                    foreach (var branch in PeptideSpectrumMatchToPeptideInstance.Split(';'))
                        joins.Add(joins.Count, branch);

                if (PeptideGroup != null)
                    foreach (var branch in PeptideSpectrumMatchToPeptide.Split(';'))
                        joins.Add(joins.Count, branch);

                if (Modifications != null || ModifiedSite != null)
                    foreach (var branch in PeptideSpectrumMatchToPeptideModification.Split(';'))
                        joins.Add(joins.Count, branch);

                if (SpectrumSource != null)
                    foreach (var branch in PeptideSpectrumMatchToSpectrumSource.Split(';'))
                        joins.Add(joins.Count, branch);

                if (SpectrumSourceGroup != null)
                    foreach (var branch in PeptideSpectrumMatchToSpectrumSourceGroupLink.Split(';'))
                        joins.Add(joins.Count, branch);
            }

            if (Cluster != null)
                proteinConditions.Add(String.Format("pro.Cluster IN ({0})", String.Join(",", Cluster.Select(o => o.ToString()).ToArray())));

            if (ProteinGroup != null)
                proteinConditions.Add(String.Format("pro.ProteinGroup IN ({0})", String.Join(",", ProteinGroup.Select(o => o.ToString()).ToArray())));

            if (Protein != null)
            {
                string column = joins.Count(o => ((string) o.Value).EndsWith(" pro")) > 0 ? "pro.id" : "pi.Protein.id";
                proteinConditions.Add(String.Format("{0} IN ({1})", column, String.Join(",", Protein.Select(o => o.Id.ToString()).ToArray())));
            }

            if (PeptideGroup != null)
                peptideConditions.Add(String.Format("pep.PeptideGroup IN ({0})", String.Join(",", PeptideGroup.Select(o => o.ToString()).ToArray())));

            if (Peptide != null)
            {
                string column = joins.Count(o => ((string) o.Value).EndsWith(" pi")) > 0 ? "pi.Peptide.id" : "psm.Peptide.id";
                peptideConditions.Add(String.Format("{0} IN ({1})", column, String.Join(",", Peptide.Select(o => o.Id.ToString()).ToArray())));
            }

            if (DistinctMatchKey != null)
                peptideConditions.Add(String.Format("psm.DistinctMatchKey IN ('{0}')", String.Join("','", DistinctMatchKey.Select(o=> o.Key).ToArray())));

            if (ModifiedSite != null)
                modConditions.Add(String.Format("pm.Site IN ('{0}')", String.Join("','", ModifiedSite.Select(o => o.ToString()).ToArray())));

            if (Modifications != null)
                modConditions.Add(String.Format("pm.Modification.id IN ({0})", String.Join(",", Modifications.Select(o => o.Id.ToString()).ToArray())));

            if (Charge != null)
                spectrumConditions.Add(String.Format("psm.Charge IN ({0})", String.Join(",", Charge.Select(o => o.ToString()).ToArray())));

            if (Analysis != null)
                otherConditions.Add(String.Format("psm.Analysis.id IN ({0})", String.Join(",", Analysis.Select(o => o.Id.ToString()).ToArray())));

            if (Spectrum != null)
                spectrumConditions.Add(String.Format("psm.Spectrum.id IN ({0})", String.Join(",", Spectrum.Select(o => o.Id.ToString()).ToArray())));

            if (SpectrumSource != null)
                spectrumConditions.Add(String.Format("psm.Spectrum.Source.id IN ({0})", String.Join(",", SpectrumSource.Select(o => o.Id.ToString()).ToArray())));

            if (SpectrumSourceGroup != null)
                spectrumConditions.Add(String.Format("ssgl.Group.id IN ({0})", String.Join(",", SpectrumSourceGroup.Select(o => o.Id.ToString()).ToArray())));

            var query = new StringBuilder();

            query.AppendFormat(" FROM {0} ", fromTable);
            foreach (var join in joins.Values.Distinct())
                query.AppendFormat("{0} ", join);
            query.Append(" ");

            var conditions = new List<string>();
            if (proteinConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", proteinConditions.ToArray()) + ")");
            if (peptideConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", peptideConditions.ToArray()) + ")");
            if (spectrumConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", spectrumConditions.ToArray()) + ")");
            if (modConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", modConditions.ToArray()) + ")");
            if (otherConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", otherConditions.ToArray()) + ")");

            if (conditions.Count > 0)
            {
                query.Append(" WHERE ");
                query.Append(String.Join(" AND ", conditions.ToArray()));
                query.Append(" ");
            }

            return query.ToString();
        }

        public string GetFilteredWhereClauseSQL ()
        {
            var conditions = new List<string>();

            if (Cluster != null)
            {
                var temp = Cluster.Select(item => String.Format("pro.Cluster = {0}", item.ToString())).ToArray();
                conditions.Add("(" + string.Join(" OR ", temp) + ")");
            }

            if (Protein != null)
            {
                var temp = Protein.Select(item => String.Format("pi.Protein = {0}", item.Id)).ToArray();
                conditions.Add("(" + string.Join(" OR ", temp) + ")");
            }

            if (Peptide != null)
            {
                var temp = Peptide.Select(item => String.Format("psm.Peptide = {0}", item.Id)).ToArray();
                conditions.Add("(" + string.Join(" OR ", temp) + ")");
            }

            /*if (DistinctMatchKey != null)
            {
                var temp = DistinctMatchKey.Select(item => String.Format("{0} = '{1}'", item.Expression, item.Key)).ToArray();
                conditions.Add("(" + string.Join(" OR ", temp) + ")");
            }*/

            if (ModifiedSite != null)
            {
                var temp = ModifiedSite.Select(item => String.Format("pm.Site = '{0}'", item.ToString())).ToArray();
                conditions.Add("(" + string.Join(" OR ", temp) + ")");
            }

            if (Modifications != null)
                conditions.Add(String.Format("pm.Modification IN ({0})",
                                             String.Join(",", Modifications.Select(o=> o.Id.ToString()).ToArray())));

            if (Charge != null)
            {
                var temp = Charge.Select(item => String.Format("psm.Charge = {0}", item)).ToArray();
                conditions.Add("(" + string.Join(" OR ", temp) + ")");
            }

            if (Analysis != null)
            {
                var temp = Analysis.Select(item => String.Format("psm.Analysis = {0}", item.Id)).ToArray();
                conditions.Add("(" + string.Join(" OR ", temp) + ")");
            }

            if (Spectrum != null)
            {
                var temp = Spectrum.Select(item => String.Format("psm.Spectrum = {0}", item.Id)).ToArray();
                conditions.Add("(" + string.Join(" OR ", temp) + ")");
            }

            if (SpectrumSource != null)
            {
                var temp = SpectrumSource.Select(item => String.Format("s.Source = {0}", item.Id)).ToArray();
                conditions.Add("(" + string.Join(" OR ", temp) + ")");
            }

            if (SpectrumSourceGroup != null)
            {
                var temp = SpectrumSourceGroup.Select(item => String.Format("ssgl.Group_ = {0}", item.Id)).ToArray();
                conditions.Add("(" + string.Join(" OR ", temp) + ")");
            }

            var query = new StringBuilder();

            if (conditions.Count > 0)
            {
                query.Append(" WHERE ");
                query.Append(String.Join(" AND ", conditions.ToArray()));
                query.Append(" ");
            }

            return query.ToString();
        }

        /// <summary>
        /// Calculates (by a greedy algorithm) how many additional results each protein group explains.
        /// </summary>
        static Map<long, long> CalculateAdditionalPeptides (NHibernate.ISession session)
        {
            var resultSetByProteinId = new Map<long, Set<Set<long>>>();
            var proteinGroupByProteinId = new Dictionary<long, int>();
            var proteinSetByProteinGroup = new Map<int, Set<long>>();
            var sharedResultsByProteinId = new Map<long, long>();

            session.CreateSQLQuery(@"DROP TABLE IF EXISTS SpectrumResults;
                                     CREATE TABLE SpectrumResults AS
                                     SELECT psm.Spectrum AS Spectrum, GROUP_CONCAT(DISTINCT psm.Peptide) AS Peptides, COUNT(DISTINCT pi.Protein) AS SharedResultCount
                                     FROM PeptideSpectrumMatch psm
                                     JOIN PeptideInstance pi ON psm.Peptide = pi.Peptide
                                     GROUP BY psm.Spectrum
                                    ").ExecuteUpdate();

            var queryByProtein = session.CreateSQLQuery(@"SELECT pro.Id, pro.ProteinGroup, SUM(sr.SharedResultCount)
                                                          FROM Protein pro
                                                          JOIN PeptideInstance pi ON pro.Id = pi.Protein
                                                          JOIN PeptideSpectrumMatch psm ON pi.Peptide = psm.Peptide
                                                          JOIN SpectrumResults sr ON psm.Spectrum = sr.Spectrum
                                                          WHERE pi.Id = (SELECT Id FROM PeptideInstance WHERE Peptide = pi.Peptide AND Protein = pi.Protein LIMIT 1)
                                                            AND psm.Id = (SELECT Id FROM PeptideSpectrumMatch WHERE Peptide = pi.Peptide LIMIT 1)
                                                          GROUP BY pro.Id");

            // For each protein, get the list of peptides evidencing it;
            // an ambiguous spectrum will show up as a nested list of peptides
            var queryByResult = session.CreateSQLQuery(@"SELECT pro.Id, GROUP_CONCAT(sr.Peptides)
                                                         FROM Protein pro
                                                         JOIN PeptideInstance pi ON pro.Id = pi.Protein
                                                         JOIN PeptideSpectrumMatch psm ON pi.Peptide = psm.Peptide
                                                         JOIN SpectrumResults sr ON psm.Spectrum = sr.Spectrum
                                                         WHERE pi.Id = (SELECT Id FROM PeptideInstance WHERE Peptide = pi.Peptide AND Protein = pi.Protein LIMIT 1)
                                                           AND psm.Id = (SELECT Id FROM PeptideSpectrumMatch WHERE Peptide = pi.Peptide LIMIT 1)
                                                         GROUP BY pro.Id, sr.Peptides");

            // keep track of the proteins that explain the most results
            Set<long> maxProteinIds = new Set<long>();
            int maxExplainedCount = 0;
            long minSharedResults = 0;

            foreach(var queryRow in queryByProtein.List<object[]>())
            {
                long proteinId = (long) queryRow[0];
                int proteinGroup = (int) queryRow[1];
                sharedResultsByProteinId[proteinId] = (long) queryRow[2];

                proteinGroupByProteinId[proteinId] = proteinGroup;
                proteinSetByProteinGroup[proteinGroup].Add(proteinId);
            }

            // construct the result set for each protein
            foreach (var queryRow in queryByResult.List<object[]>())
            {
                long proteinId = (long) queryRow[0];
                string resultIds = (string) queryRow[1];
                string[] resultIdTokens = resultIds.Split(',');
                Set<long> resultIdSet = new Set<long>(resultIdTokens.Select(o => Convert.ToInt64(o)));
                Set<Set<long>> explainedResults = resultSetByProteinId[proteinId];
                explainedResults.Add(resultIdSet);

                long sharedResults = sharedResultsByProteinId[proteinId];

                if (explainedResults.Count > maxExplainedCount)
                {
                    maxProteinIds.Clear();
                    maxProteinIds.Add(proteinId);
                    maxExplainedCount = explainedResults.Count;
                    minSharedResults = sharedResults;
                }
                else if (explainedResults.Count == maxExplainedCount)
                {
                    if (sharedResults < minSharedResults)
                    {
                        maxProteinIds.Clear();
                        maxProteinIds.Add(proteinId);
                        minSharedResults = sharedResults;
                    }
                    else if (sharedResults == minSharedResults)
                        maxProteinIds.Add(proteinId);
                }
            }

            var additionalPeptidesByProteinId = new Map<long, long>();

            // loop until the maxProteinIdsSetByProteinId map is empty
            while (resultSetByProteinId.Count > 0)
            {
                // the set of results explained by the max. proteins
                Set<Set<long>> maxExplainedResults = null;

                // remove max. proteins from the resultSetByProteinId map
                foreach (long maxProteinId in maxProteinIds)
                {
                    if (maxExplainedResults == null)
                        maxExplainedResults = resultSetByProteinId[maxProteinId];
                    else
                        maxExplainedResults.Union(resultSetByProteinId[maxProteinId]);

                    resultSetByProteinId.Remove(maxProteinId);
                    additionalPeptidesByProteinId[maxProteinId] = maxExplainedCount;
                }

                // subtract the max. proteins' results from the remaining proteins
                maxProteinIds.Clear();
                maxExplainedCount = 0;
                minSharedResults = 0;

                foreach (Map<long, Set<Set<long>>>.MapPair itr in resultSetByProteinId)
                {
                    Set<Set<long>> explainedResults = itr.Value;
                    explainedResults.Subtract(maxExplainedResults);

                    long sharedResults = sharedResultsByProteinId[itr.Key];

                    if (explainedResults.Count > maxExplainedCount)
                    {
                        maxProteinIds.Clear();
                        maxProteinIds.Add(itr.Key);
                        maxExplainedCount = explainedResults.Count;
                        minSharedResults = sharedResults;
                    }
                    else if (explainedResults.Count == maxExplainedCount)
                    {
                        if (sharedResults < minSharedResults)
                        {
                            maxProteinIds.Clear();
                            maxProteinIds.Add(itr.Key);
                            minSharedResults = sharedResults;
                        }
                        else if (sharedResults == minSharedResults)
                            maxProteinIds.Add(itr.Key);
                    }
                }

                // all remaining proteins present no additional evidence, so break the loop
                if (maxExplainedCount == 0)
                {
                    foreach (Map<long, Set<Set<long>>>.MapPair itr in resultSetByProteinId)
                        additionalPeptidesByProteinId[itr.Key] = 0;
                    break;
                }
            }

            return additionalPeptidesByProteinId;
        }

        void recursivelyAssignProteinToCluster (long proteinId,
                                                long clusterId,
                                                Set<long> spectrumSet,
                                                Map<long, Set<long>> spectrumSetByProteinId,
                                                Map<long, Set<long>> proteinSetBySpectrumId,
                                                Map<long, long> clusterByProteinId)
        {
            // try to assign the protein to the current cluster
            var insertResult = clusterByProteinId.Insert(proteinId, clusterId);
            if (!insertResult.WasInserted)
            {
                // error if the protein was already assigned to a DIFFERENT cluster
                if (insertResult.Element.Value != clusterId)
                    throw new InvalidOperationException("error calculating protein clusters");

                // early exit if the protein was already assigned to the CURRENT cluster
                return;
            }

            // recursively add all "cousin" proteins to the current cluster
            foreach (long spectrumId in spectrumSet)
                foreach (var cousinProteinId in proteinSetBySpectrumId[spectrumId])
                {
                    if (proteinId != cousinProteinId)
                    {
                        Set<long> cousinSpectrumSet = spectrumSetByProteinId[cousinProteinId];
                        recursivelyAssignProteinToCluster(cousinProteinId,
                                                          clusterId,
                                                          cousinSpectrumSet,
                                                          spectrumSetByProteinId,
                                                          proteinSetBySpectrumId,
                                                          clusterByProteinId);
                    }
                    //else if (cousinProGroup.cluster != c.id)
                    //    throw new InvalidDataException("protein groups that are connected are assigned to different clusters");
                }
        }

        Map<long, long> calculateProteinClusters (NHibernate.ISession session)
        {
            var spectrumSetByProteinId = new Map<long, Set<long>>();
            var proteinSetBySpectrumId = new Map<long, Set<long>>();

            var query = session.CreateQuery("SELECT pi.Protein.id, psm.Spectrum.id " +
                                            GetFilteredQueryString(FromProtein, ProteinToPeptideSpectrumMatch));

            foreach (var queryRow in query.List<object[]>())
            {
                long proteinId = (long) queryRow[0];
                long spectrumId = (long) queryRow[1];

                spectrumSetByProteinId[proteinId].Add(spectrumId);
                proteinSetBySpectrumId[spectrumId].Add(proteinId);
            }

            var clusterByProteinId = new Map<long, long>();
            int clusterId = 0;

            foreach (var pair in spectrumSetByProteinId)
            {
                long proteinId = pair.Key;

                // for each protein without a cluster assignment, make a new cluster
                if (!clusterByProteinId.Contains(proteinId))
                {
                    ++clusterId;

                    recursivelyAssignProteinToCluster(proteinId,
                                                      clusterId,
                                                      pair.Value,
                                                      spectrumSetByProteinId,
                                                      proteinSetBySpectrumId,
                                                      clusterByProteinId);
                }
            }

            return clusterByProteinId;
        }
    }

    /// <summary>
    /// Read-only wrapper of DataFilter that is safe to use as a Dictionary or Hashtable key.
    /// </summary>
    public class DataFilterKey
    {
        private DataFilter DataFilter { get; set; }

        public DataFilterKey (DataFilter dataFilter) { DataFilter = new DataFilter(dataFilter); }

        public override bool Equals (object obj)
        {
            DataFilterKey other = obj as DataFilterKey;
            if (other == null)
                return false;
            return DataFilter.Equals(other.DataFilter);
        }

        public override int GetHashCode ()
        {
            return DataFilter.MaximumQValue.GetHashCode() ^
                   DataFilter.MinimumDistinctPeptidesPerProtein.GetHashCode() ^
                   DataFilter.MinimumSpectraPerProtein.GetHashCode() ^
                   DataFilter.MinimumAdditionalPeptidesPerProtein.GetHashCode() ^
                   NullSafeHashCode(DataFilter.Cluster) ^
                   NullSafeHashCode(DataFilter.ProteinGroup) ^
                   NullSafeHashCode(DataFilter.PeptideGroup) ^
                   NullSafeHashCode(DataFilter.Protein) ^
                   NullSafeHashCode(DataFilter.Peptide) ^
                   NullSafeHashCode(DataFilter.DistinctMatchKey) ^
                   NullSafeHashCode(DataFilter.Modifications) ^
                   NullSafeHashCode(DataFilter.ModifiedSite) ^
                   NullSafeHashCode(DataFilter.Charge) ^
                   NullSafeHashCode(DataFilter.Analysis) ^
                   NullSafeHashCode(DataFilter.Spectrum) ^
                   NullSafeHashCode(DataFilter.SpectrumSource) ^
                   NullSafeHashCode(DataFilter.SpectrumSourceGroup) ^
                   NullSafeHashCode(DataFilter.Composition);
        }

        private static int NullSafeHashCode<T> (IEnumerable<T> obj)
        {
            if (obj == null)
                return 0;

            int hash = 0;
            foreach(T item in obj)
                hash ^= item.GetHashCode();
            return hash;
        }
    }
}
