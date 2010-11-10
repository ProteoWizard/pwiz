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
    public class DistinctPeptideFormat
    {
        public string Expression { get; private set; } // e.g. (psm.Peptide || ' ' || psm.MonoisotopicMass)
        public string Sequence { get; private set; } // e.g. "PEPT[-18]IDE"
        public string Key { get; private set; } // e.g. "123 4567.8"

        public DistinctPeptideFormat (string format, string sequence, string key)
        {
            Expression = format;
            Sequence = sequence;
            Key = key;
        }

        public override int GetHashCode ()
        {
            return Expression.GetHashCode() ^ Key.GetHashCode();
        }

        public override bool Equals (object obj)
        {
            var other = obj as DistinctPeptideFormat;
            if (other == null)
                return false;
            return Expression == other.Expression && Key == other.Key;
        }
    }

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
                TotalFilters = 14;
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
            MaximumQValue = 0.02M;
            MinimumDistinctPeptidesPerProtein = 2;
            MinimumSpectraPerProtein = 2;
            MinimumAdditionalPeptidesPerProtein = 1;
            Modifications = new List<Modification>();
        }

        public DataFilter (DataFilter other)
        {
            MaximumQValue = other.MaximumQValue;
            MinimumDistinctPeptidesPerProtein = other.MinimumDistinctPeptidesPerProtein;
            MinimumSpectraPerProtein = other.MinimumSpectraPerProtein;
            MinimumAdditionalPeptidesPerProtein = other.MinimumAdditionalPeptidesPerProtein;
            Modifications = new List<Modification>(other.Modifications);
            Cluster = other.Cluster;
            Protein = other.Protein;
            Peptide = other.Peptide;
            DistinctPeptideKey = other.DistinctPeptideKey;
            ModifiedSite = other.ModifiedSite;
            Charge = other.Charge;
            Analysis = other.Analysis;
            Spectrum = other.Spectrum;
            SpectrumSource = other.SpectrumSource;
            SpectrumSourceGroup = other.SpectrumSourceGroup;
        }

        public decimal MaximumQValue { get; set; }
        public int MinimumDistinctPeptidesPerProtein { get; set; }
        public int MinimumSpectraPerProtein { get; set; }
        public int MinimumAdditionalPeptidesPerProtein { get; set; }

        public long? Cluster { get; set; }
        public Protein Protein { get; set; }
        public Peptide Peptide { get; set; }
        public DistinctPeptideFormat DistinctPeptideKey { get; set; }
        public IList<Modification> Modifications { get; set; }
        public char? ModifiedSite { get; set; }
        public int? Charge { get; set; }
        public SpectrumSourceGroup SpectrumSourceGroup { get; set; }
        public SpectrumSource SpectrumSource { get; set; }
        public Spectrum Spectrum { get; set; }
        public Analysis Analysis { get; set; }

        public object FilterSource { get; set; }

        public bool IsBasicFilter
        {
            get
            {
                return Cluster == null && Protein == null && Peptide == null && DistinctPeptideKey == null &&
                       Modifications.Count == 0 && ModifiedSite == null && Charge == null &&
                       SpectrumSource == null && Spectrum == null && Analysis == null;
            }
        }

        public override int GetHashCode ()
        {
            return MaximumQValue.GetHashCode() ^
                   MinimumDistinctPeptidesPerProtein.GetHashCode() ^
                   MinimumSpectraPerProtein.GetHashCode() ^
                   MinimumAdditionalPeptidesPerProtein.GetHashCode();
        }

        public override bool Equals (object obj)
        {
            var other = obj as DataFilter;
            if (other == null)
                return false;

            return MaximumQValue == other.MaximumQValue &&
                   MinimumDistinctPeptidesPerProtein == other.MinimumDistinctPeptidesPerProtein &&
                   MinimumSpectraPerProtein == other.MinimumSpectraPerProtein &&
                   MinimumAdditionalPeptidesPerProtein == other.MinimumAdditionalPeptidesPerProtein &&
                   Cluster == other.Cluster &&
                   Protein == other.Protein &&
                   Peptide == other.Peptide &&
                   DistinctPeptideKey == other.DistinctPeptideKey &&
                   Modifications.SequenceEqual(other.Modifications) &&
                   ModifiedSite == other.ModifiedSite &&
                   Charge == other.Charge &&
                   Analysis == other.Analysis &&
                   Spectrum == other.Spectrum &&
                   SpectrumSource == other.SpectrumSource &&
                   SpectrumSourceGroup == other.SpectrumSourceGroup;
        }

        public static DataFilter operator + (DataFilter lhs, DataFilter rhs)
        {
            var newFilter = new DataFilter(lhs);
            if (rhs.Cluster != null) newFilter.Cluster = rhs.Cluster;
            if (rhs.Protein != null) newFilter.Protein = rhs.Protein;
            if (rhs.Peptide != null) newFilter.Peptide = rhs.Peptide;
            if (rhs.DistinctPeptideKey != null) newFilter.DistinctPeptideKey = rhs.DistinctPeptideKey;
            if (rhs.Modifications != null) newFilter.Modifications = newFilter.Modifications.Union(rhs.Modifications).ToList();
            if (rhs.ModifiedSite != null) newFilter.ModifiedSite = rhs.ModifiedSite;
            if (rhs.Charge != null) newFilter.Charge = rhs.Charge;
            if (rhs.Analysis != null) newFilter.Analysis = rhs.Analysis;
            if (rhs.Spectrum != null) newFilter.Spectrum = rhs.Spectrum;
            if (rhs.SpectrumSource != null) newFilter.SpectrumSource = rhs.SpectrumSource;
            if (rhs.SpectrumSourceGroup != null) newFilter.SpectrumSourceGroup = rhs.SpectrumSourceGroup;
            return newFilter;
        }

        public static bool operator == (DataFilter lhs, DataFilter rhs) { return object.ReferenceEquals(lhs, null) ? object.ReferenceEquals(rhs, null) : lhs.Equals(rhs); }
        public static bool operator != (DataFilter lhs, DataFilter rhs) { return !(lhs == rhs); }

        public override string ToString ()
        {
            if (Cluster != null)
                return "Cluster = " + Cluster.ToString();
            else if (Protein != null)
                return "Protein = " + Protein.Accession;
            else if (Peptide != null)
                return "Peptide = " + Peptide.Sequence;
            else if (DistinctPeptideKey != null)
                return "Interpretation = " + DistinctPeptideKey.Sequence;
            else if (SpectrumSourceGroup != null)
                return "Group = " + SpectrumSourceGroup.Name;
            else if (SpectrumSource != null)
                return "Source = " + SpectrumSource.Name;
            else if (Spectrum != null)
                return "Spectrum = " + Spectrum.NativeID;
            else if (Analysis != null)
                return "Analysis = " + Analysis.Name;
            else if (Charge != null)
                return "Charge = " + Charge;
            else if (ModifiedSite == null && Modifications.Count == 0)
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
                    result.AppendFormat("Modified site: {0}", ModifiedSite);

                if (Modifications.Count == 0)
                    return result.ToString();

                if (ModifiedSite != null)
                    result.Append("; ");

                var distinctModMasses = (from mod in Modifications
                                         select Math.Round(mod.MonoMassDelta).ToString())
                                         .Distinct();
                result.AppendFormat("Mass shift{0}: {1}",
                                    distinctModMasses.Count() > 1 ? "s" : "",
                                    String.Join(",", distinctModMasses.ToArray()));
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
                dataFilter.MaximumQValue = Convert.ToDecimal(filteringCriteria[0]);
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

        public void ApplyBasicFilters (NHibernate.ISession session)
        {
            bool useScopedTransaction = !session.Transaction.IsActive;
            if (useScopedTransaction)
                session.Transaction.Begin();

            // ignore errors if main tables haven't been created yet

            #region Drop Filtered* tables
            if(OnFilteringProgress(new FilteringProgressEventArgs("Dropping current filters...", 1, null)))
                return;

            session.CreateSQLQuery(@"DROP TABLE IF EXISTS FilteredProtein;
                                     DROP TABLE IF EXISTS FilteredPeptideInstance;
                                     DROP TABLE IF EXISTS FilteredPeptide;
                                     DROP TABLE IF EXISTS FilteredPeptideSpectrumMatch
                                    ").ExecuteUpdate();
            #endregion

            #region Restore Unfiltered* tables as the main tables
            try
            {
                if (OnFilteringProgress(new FilteringProgressEventArgs("Restoring unfiltered data...", 2, null)))
                    return;

                // if unfiltered tables have not been created, this will throw and skip the rest of the block
                session.CreateSQLQuery("SELECT Id FROM UnfilteredProtein LIMIT 1").ExecuteUpdate();

                // drop filtered tables
                session.CreateSQLQuery(@"DROP TABLE IF EXISTS Protein;
                                         DROP TABLE IF EXISTS PeptideInstance;
                                         DROP TABLE IF EXISTS Peptide;
                                         DROP TABLE IF EXISTS PeptideSpectrumMatch
                                        ").ExecuteUpdate();

                // rename unfiltered tables 
                session.CreateSQLQuery(@"ALTER TABLE UnfilteredProtein RENAME TO Protein;
                                         ALTER TABLE UnfilteredPeptideInstance RENAME TO PeptideInstance;
                                         ALTER TABLE UnfilteredPeptide RENAME TO Peptide;
                                         ALTER TABLE UnfilteredPeptideSpectrumMatch RENAME TO PeptideSpectrumMatch
                                        ").ExecuteUpdate();
            }
            catch
            {
            }
            #endregion

            #region Create Filtered* tables by applying the basic filters to the main tables
            if (OnFilteringProgress(new FilteringProgressEventArgs("Filtering proteins...", 3, null)))
                return;
            string filterProteinsSql =
                @"CREATE TABLE FilteredProtein (Id INTEGER PRIMARY KEY, Accession TEXT, Cluster INT, ProteinGroup TEXT, Length INT);
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

            if (OnFilteringProgress(new FilteringProgressEventArgs("Filtering peptide spectrum matches...", 4, null)))
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

            if (OnFilteringProgress(new FilteringProgressEventArgs("Filtering peptides...", 5, null)))
                return;
            session.CreateSQLQuery(@"CREATE TABLE FilteredPeptide (Id INTEGER PRIMARY KEY, MonoisotopicMass NUMERIC, MolecularWeight NUMERIC);
                                     INSERT INTO FilteredPeptide SELECT pep.*
                                     FROM FilteredPeptideSpectrumMatch psm
                                     JOIN Peptide pep ON psm.Peptide = pep.Id
                                     GROUP BY pep.Id"
                                  ).ExecuteUpdate();

            if (OnFilteringProgress(new FilteringProgressEventArgs("Filtering peptide instances...", 6, null)))
                return;
            session.CreateSQLQuery(@"CREATE TABLE FilteredPeptideInstance (Id INTEGER PRIMARY KEY, Protein INT, Peptide INT, Offset INT, Length INT, NTerminusIsSpecific INT, CTerminusIsSpecific INT, MissedCleavages INT);
                                     INSERT INTO FilteredPeptideInstance SELECT pi.*
                                     FROM FilteredPeptide pep
                                     JOIN PeptideInstance pi ON pep.Id = pi.Peptide
                                     JOIN FilteredProtein pro ON pi.Protein = pro.Id;
                                     CREATE INDEX FilteredPeptideInstance_Protein ON FilteredPeptideInstance (Protein);
                                     CREATE INDEX FilteredPeptideInstance_Peptide ON FilteredPeptideInstance (Peptide);
                                     CREATE INDEX FilteredPeptideInstance_PeptideProtein ON FilteredPeptideInstance (Peptide, Protein);
                                     CREATE UNIQUE INDEX FilteredPeptideInstance_ProteinOffsetLength ON FilteredPeptideInstance (Protein, Offset, Length);"
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

            if (AssembleProteinGroups(session)) return;
            if (ApplyAdditionalPeptidesFilter(session)) return;
            if (AssembleClusters(session)) return;
            if (AssembleProteinCoverage(session)) return;

            SaveFilter(session);

            if (useScopedTransaction)
                session.Transaction.Commit();
        }

        #region Implementation of basic filters
        /// <summary>
        /// Set ProteinGroups column (the groups change depending on the basic filters applied)
        /// </summary>
        bool AssembleProteinGroups(NHibernate.ISession session)
        {
            if (OnFilteringProgress(new FilteringProgressEventArgs("Assembling protein groups...", 7, null)))
                return true;

            session.CreateSQLQuery(@"CREATE TEMP TABLE ProteinGroups AS
                                     SELECT pro.Id AS ProteinId, GROUP_CONCAT(DISTINCT pi.Peptide) AS ProteinGroup
                                     FROM PeptideInstance pi
                                     JOIN Protein pro ON pi.Protein = pro.Id
                                     GROUP BY pi.Protein;

                                     CREATE TEMP TABLE TempProtein AS
                                     SELECT pg.ProteinId, pro.Accession, pro.Cluster, pg2.ProteinGroupId, pro.Length
                                     FROM ProteinGroups pg
                                     JOIN ( 
                                           SELECT pg.ProteinGroup, MIN(ProteinId) AS ProteinGroupId
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
        /// Calculate additional peptides per protein and filter out proteins that don't meet the minimum
        /// </summary>
        bool ApplyAdditionalPeptidesFilter(NHibernate.ISession session)
        {
            if (MinimumAdditionalPeptidesPerProtein == 0)
                return false;

            if (OnFilteringProgress(new FilteringProgressEventArgs("Calculating additional peptide counts...", 8, null)))
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

            if (OnFilteringProgress(new FilteringProgressEventArgs("Filtering by additional peptide count...", 9, null)))
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
        bool AssembleClusters (NHibernate.ISession session)
        {
            if (OnFilteringProgress(new FilteringProgressEventArgs("Calculating protein clusters...", 10, null)))
                return true;

            Map<long, long> clusterByProteinId = calculateProteinClusters(session);

            if (OnFilteringProgress(new FilteringProgressEventArgs("Assigning proteins to clusters...", 11, null)))
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
        bool AssembleProteinCoverage (NHibernate.ISession session)
        {
            if (OnFilteringProgress(new FilteringProgressEventArgs("Calculating protein coverage...", 12, null)))
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

            if (OnFilteringProgress(new FilteringProgressEventArgs("Calculating protein coverage masks...", 13, null)))
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

            if (OnFilteringProgress(new FilteringProgressEventArgs("Updating protein coverage masks...", 14, null)))
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
            var joins = new List<string>();
            foreach (var join in joinTables)
                foreach (var branch in join.ToString().Split(';'))
                    if (!joins.Contains(branch))
                        joins.Add(branch);

            var conditions = new List<string>();

            if (fromTable == FromProtein)
            {
                if (Cluster != null)
                    conditions.Add(String.Format("pro.Cluster = {0}", Cluster));

                if (Protein != null)
                    conditions.Add(String.Format("pro.id = {0}", Protein.Id));

                if (Peptide != null)
                {
                    conditions.Add(String.Format("pi.Peptide.id = {0}", Peptide.Id));
                    foreach (var branch in ProteinToPeptideInstance.Split(';'))
                        if (!joins.Contains(branch))
                            joins.Add(branch);
                }

                if (Modifications.Count > 0 || ModifiedSite != null)
                    foreach (var branch in ProteinToPeptideModification.Split(';'))
                        if (!joins.Contains(branch))
                            joins.Add(branch);

                if (DistinctPeptideKey != null || Analysis != null || Spectrum != null)
                    foreach (var branch in ProteinToPeptideSpectrumMatch.Split(';'))
                        if (!joins.Contains(branch))
                            joins.Add(branch);

                if (SpectrumSource != null)
                    foreach (var branch in ProteinToSpectrumSource.Split(';'))
                        if (!joins.Contains(branch))
                            joins.Add(branch);

                if (SpectrumSourceGroup != null)
                    foreach (var branch in ProteinToSpectrumSourceGroupLink.Split(';'))
                        if (!joins.Contains(branch))
                            joins.Add(branch);
            }
            else if (fromTable == FromPeptideSpectrumMatch)
            {
                if (Cluster != null)
                {
                    conditions.Add(String.Format("pro.Cluster = {0}", Cluster));
                    foreach (var branch in PeptideSpectrumMatchToProtein.Split(';'))
                        if (!joins.Contains(branch))
                            joins.Add(branch);
                }

                if (Protein != null)
                {
                    conditions.Add(String.Format("pi.Protein.id = {0}", Protein.Id));
                    foreach (var branch in PeptideSpectrumMatchToPeptideInstance.Split(';'))
                        if (!joins.Contains(branch))
                            joins.Add(branch);
                }

                if (Peptide != null)
                    conditions.Add(String.Format("psm.Peptide.id = {0}", Peptide.Id));

                if (Modifications.Count > 0 || ModifiedSite != null)
                    foreach (var branch in PeptideSpectrumMatchToPeptideModification.Split(';'))
                        if (!joins.Contains(branch))
                            joins.Add(branch);

                if (SpectrumSource != null)
                    foreach (var branch in PeptideSpectrumMatchToSpectrumSource.Split(';'))
                        if (!joins.Contains(branch))
                            joins.Add(branch);

                if (SpectrumSourceGroup != null)
                    foreach (var branch in PeptideSpectrumMatchToSpectrumSourceGroupLink.Split(';'))
                        if (!joins.Contains(branch))
                            joins.Add(branch);
            }

            if (DistinctPeptideKey != null)
                conditions.Add(String.Format("{0} = '{1}'", DistinctPeptideKey.Expression, DistinctPeptideKey.Key));

            if (ModifiedSite != null)
                conditions.Add(String.Format("pm.Site = '{0}'", ModifiedSite));

            if (Modifications.Count > 0)
                conditions.Add(String.Format("pm.Modification.id IN ({0})",
                    String.Join(",", (from mod in Modifications
                                      select mod.Id.ToString()).Distinct().ToArray())));

            if (Charge != null)
                conditions.Add(String.Format("psm.Charge = {0}", Charge));

            if (Analysis != null)
                conditions.Add(String.Format("psm.Analysis.id = {0}", Analysis.Id));

            if (Spectrum != null)
                conditions.Add(String.Format("psm.Spectrum.id = {0}", Spectrum.Id));

            if (SpectrumSource != null)
                conditions.Add(String.Format("psm.Spectrum.Source.id = {0}", SpectrumSource.Id));

            if (SpectrumSourceGroup != null)
                conditions.Add(String.Format("ssgl.Group.id = {0}", SpectrumSourceGroup.Id));

            var query = new StringBuilder();

            query.AppendFormat("FROM {0} ", fromTable);
            foreach (var join in joins)
                query.AppendFormat("{0} ", join);
            query.Append(" ");

            if (conditions.Count > 0)
            {
                query.Append("WHERE ");
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
            var proteinGroupByProteinId = new Dictionary<long, string>();
            var proteinSetByProteinGroup = new Map<string, Set<long>>();
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
                string proteinGroup = (string) queryRow[1];
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
}
