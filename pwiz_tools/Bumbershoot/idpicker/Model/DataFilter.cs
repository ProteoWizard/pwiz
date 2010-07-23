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
using System.Windows.Forms;
using IDPicker.DataModel;

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
        public DataFilter ()
        {
            MaximumQValue = 0.1M;
            MinimumDistinctPeptidesPerProtein = 1;
            MinimumSpectraPerProtein = 1;
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
        public SpectrumSourceGroup SpectrumSourceGroup { get; set; }
        public SpectrumSource SpectrumSource { get; set; }
        public Spectrum Spectrum { get; set; }

        public object FilterSource { get; set; }

        public bool IsBasicFilter
        {
            get
            {
                return Cluster == null && Protein == null && Peptide == null && DistinctPeptideKey == null &&
                       Modifications.Count == 0 && ModifiedSite == null &&
                       SpectrumSourceGroup == null && SpectrumSource == null && Spectrum == null;
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
                   //Modifications == other.Modifications &&
                   ModifiedSite == other.ModifiedSite &&
                   Spectrum == other.Spectrum &&
                   SpectrumSource == other.SpectrumSource &&
                   SpectrumSourceGroup == other.SpectrumSourceGroup;
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

        public string GetBasicQueryStringSQL ()
        {
            return String.Format("FROM PeptideSpectrumMatches psm " +
                                 "JOIN PeptideInstances pi ON psm.Peptide = pi.Peptide " +
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
            // ignore errors if main tables haven't been created yet

            #region Drop Filtered* tables
            try { session.CreateSQLQuery("DROP TABLE FilteredProteins").ExecuteUpdate(); } catch { }
            try { session.CreateSQLQuery("DROP TABLE FilteredPeptideInstances").ExecuteUpdate(); } catch { }
            try { session.CreateSQLQuery("DROP TABLE FilteredPeptides").ExecuteUpdate(); } catch { }
            try { session.CreateSQLQuery("DROP TABLE FilteredPeptideSpectrumMatches").ExecuteUpdate(); } catch { }
            #endregion

            #region Restore Unfiltered* tables as the main tables
            try
            {
                // if unfiltered tables have not been created, this will throw and skip the rest of the block
                session.CreateSQLQuery("SELECT Id FROM UnfilteredProteins LIMIT 1").ExecuteUpdate();

                // drop filtered tables
                try { session.CreateSQLQuery("DROP TABLE Proteins").ExecuteUpdate(); } catch { }
                try { session.CreateSQLQuery("DROP TABLE PeptideInstances").ExecuteUpdate(); } catch { }
                try { session.CreateSQLQuery("DROP TABLE Peptides").ExecuteUpdate(); } catch { }
                try { session.CreateSQLQuery("DROP TABLE PeptideSpectrumMatches").ExecuteUpdate(); } catch { }

                // rename unfiltered tables 
                session.CreateSQLQuery("ALTER TABLE UnfilteredProteins RENAME TO Proteins;" +
                                       "ALTER TABLE UnfilteredPeptideInstances RENAME TO PeptideInstances;" +
                                       "ALTER TABLE UnfilteredPeptides RENAME TO Peptides;" +
                                       "ALTER TABLE UnfilteredPeptideSpectrumMatches RENAME TO PeptideSpectrumMatches"
                                      ).ExecuteUpdate();
            }
            catch
            {
            }
            #endregion

            #region Create Filtered* tables by applying the basic filters to the main tables
            session.CreateSQLQuery("CREATE TABLE FilteredProteins (Id INTEGER PRIMARY KEY, Accession TEXT, Description TEXT, Sequence TEXT, Cluster INT, ProteinGroup TEXT);" +
                String.Format("INSERT INTO FilteredProteins SELECT pro.* " +
                              "FROM PeptideSpectrumMatches psm " +
                              "JOIN PeptideInstances pi ON psm.Peptide = pi.Peptide " +
                              "JOIN Proteins pro ON pi.Protein = pro.Id " +
                              "WHERE {0} >= psm.QValue AND psm.Rank = 1 " +
                              "GROUP BY pi.Protein " +
                              "HAVING {1} <= COUNT(DISTINCT psm.Peptide) AND " +
                              "       {2} <= COUNT(DISTINCT psm.Spectrum)",
                              MaximumQValue,
                              MinimumDistinctPeptidesPerProtein,
                              MinimumSpectraPerProtein)).ExecuteUpdate();

            session.CreateSQLQuery("CREATE TABLE FilteredPeptideSpectrumMatches (Id INTEGER PRIMARY KEY, Spectrum INT, Analysis INT, Peptide INT, QValue NUMERIC, MonoisotopicMass NUMERIC, MolecularWeight NUMERIC, MonoisotopicMassError NUMERIC, MolecularWeightError NUMERIC, Rank INT, Charge INT);" +
                                   "INSERT INTO FilteredPeptideSpectrumMatches SELECT psm.* " +
                                   "FROM FilteredProteins pro " +
                                   "JOIN PeptideInstances pi ON pro.Id = pi.Protein " +
                                   "JOIN PeptideSpectrumMatches psm ON pi.Peptide = psm.Peptide " +
                                   "WHERE " + MaximumQValue.ToString() + " >= psm.QValue AND psm.Rank = 1 " +
                                   "GROUP BY psm.Id;" +
                                   "CREATE INDEX FilteredPeptideSpectrumMatch_SpectrumIndex ON FilteredPeptideSpectrumMatches (Spectrum);" +
                                   "CREATE INDEX FilteredPeptideSpectrumMatch_PeptideIndex ON FilteredPeptideSpectrumMatches (Peptide);" +
                                   "CREATE INDEX FilteredPeptideSpectrumMatch_QValueIndex ON FilteredPeptideSpectrumMatches (QValue)"
                                  ).ExecuteUpdate();

            session.CreateSQLQuery("CREATE TABLE FilteredPeptides (Id INTEGER PRIMARY KEY, MonoisotopicMass NUMERIC, MolecularWeight NUMERIC);" +
                                   "INSERT INTO FilteredPeptides SELECT pep.* " +
                                   "FROM FilteredPeptideSpectrumMatches psm " +
                                   "JOIN Peptides pep ON psm.Peptide = pep.Id " +
                                   "GROUP BY pep.Id"
                                  ).ExecuteUpdate();

            session.CreateSQLQuery("CREATE TABLE FilteredPeptideInstances (Id INTEGER PRIMARY KEY, Protein INT, Peptide INT, Offset INT, Length INT, NTerminusIsSpecific INT, CTerminusIsSpecific INT, MissedCleavages INT);" +
                                   "INSERT INTO FilteredPeptideInstances SELECT pi.* " +
                                   "FROM FilteredProteins pro " +
                                   "JOIN PeptideInstances pi ON pro.Id = pi.Protein " +
                                   "JOIN FilteredPeptides pep ON pi.Peptide = pep.Id;" +
                                   "CREATE INDEX FilteredPeptideInstance_ProteinIndex ON FilteredPeptideInstances (Protein);" +
                                   "CREATE INDEX FilteredPeptideInstance_PeptideIndex ON FilteredPeptideInstances (Peptide)"
                                  ).ExecuteUpdate();
            #endregion

            #region Rename main tables to Unfiltered*
            session.CreateSQLQuery("ALTER TABLE Proteins RENAME TO UnfilteredProteins").ExecuteUpdate();
            session.CreateSQLQuery("ALTER TABLE PeptideInstances RENAME TO UnfilteredPeptideInstances").ExecuteUpdate();
            session.CreateSQLQuery("ALTER TABLE Peptides RENAME TO UnfilteredPeptides").ExecuteUpdate();
            session.CreateSQLQuery("ALTER TABLE PeptideSpectrumMatches RENAME TO UnfilteredPeptideSpectrumMatches").ExecuteUpdate();
            #endregion

            #region Rename Filtered* tables to main tables
            session.CreateSQLQuery("ALTER TABLE FilteredProteins RENAME TO Proteins").ExecuteUpdate();
            session.CreateSQLQuery("ALTER TABLE FilteredPeptideInstances RENAME TO PeptideInstances").ExecuteUpdate();
            session.CreateSQLQuery("ALTER TABLE FilteredPeptides RENAME TO Peptides").ExecuteUpdate();
            session.CreateSQLQuery("ALTER TABLE FilteredPeptideSpectrumMatches RENAME TO PeptideSpectrumMatches").ExecuteUpdate();
            #endregion

            AssembleProteinGroups(session);
            ApplyAdditionalPeptidesFilter(session);
            AssembleClusters(session);

            #region Create FilteringCriteria table to store the basic filter parameters
            try { session.CreateSQLQuery("DROP TABLE FilteringCriteria").ExecuteUpdate(); }
            catch { }
            session.CreateSQLQuery(
                String.Format("CREATE TABLE FilteringCriteria (MaximumQValue NUMERIC, MinimumDistinctPeptidesPerProtein INT, MinimumSpectraPerProtein INT, MinimumAdditionalPeptidesPerProtein INT);" +
                              "INSERT INTO FilteringCriteria SELECT {0}, {1}, {2}, {3}",
                              MaximumQValue,
                              MinimumDistinctPeptidesPerProtein,
                              MinimumSpectraPerProtein,
                              MinimumAdditionalPeptidesPerProtein)).ExecuteUpdate();
            #endregion
        }

        /// <summary>
        /// Set ProteinGroups column (the groups change depending on the basic filters applied)
        /// </summary>
        void AssembleProteinGroups(NHibernate.ISession session)
        {
            var proteinGroupQuery = session.CreateSQLQuery("SELECT GROUP_CONCAT(DISTINCT pi.Peptide), pi.Protein " +
                                                           "FROM PeptideInstances pi " +
                                                           "GROUP BY pi.Protein " +
                                                           "ORDER BY pi.Protein");

            var cmd = session.Connection.CreateCommand();
            cmd.CommandText = "UPDATE Proteins SET ProteinGroup = ? WHERE Id = ?";
            var parameters = new List<System.Data.IDbDataParameter>();
            for (int i = 0; i < 2; ++i)
            {
                parameters.Add(cmd.CreateParameter());
                cmd.Parameters.Add(parameters[i]);
            }
            cmd.Prepare();
            foreach (object[] itr in proteinGroupQuery.List<object[]>())
            {
                parameters[0].Value = (string) itr[0];
                parameters[1].Value = (int) itr[1];
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Calculate additional peptides per protein and filter out proteins that don't meet the minimum
        /// </summary>
        void ApplyAdditionalPeptidesFilter(NHibernate.ISession session)
        {
            if (MinimumAdditionalPeptidesPerProtein == 0)
                return;

            Map<long, long> additionalPeptidesByProteinId = CalculateAdditionalPeptides(session);

            try { session.CreateSQLQuery("DROP TABLE AdditionalMatches").ExecuteUpdate(); } catch { }
            session.CreateSQLQuery("CREATE TABLE AdditionalMatches (ProteinId INTEGER PRIMARY KEY, AdditionalMatches INT)").ExecuteUpdate();

            // temporarily disable stdout SQL logging
            var backup = System.Console.Out;
            var nullOut = new System.IO.StringWriter();
            System.Console.SetOut(nullOut);

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

            // restore stdout
            System.Console.SetOut(backup);

            var additionalPeptidesDeleteCommand = new StringBuilder();

            // delete proteins that don't meet the additional matches filter
            additionalPeptidesDeleteCommand.AppendFormat("DELETE FROM Proteins " +
                                                         "WHERE Id IN (SELECT pro.Id " +
                                                         "             FROM Proteins pro " +
                                                         "             JOIN AdditionalMatches am ON pro.Id = am.ProteinId " +
                                                         "             WHERE am.AdditionalMatches < {0});",
                                                         MinimumAdditionalPeptidesPerProtein);

            #region Cascade the deletions to the other tables
            // delete peptide instances whose protein is gone
            // delete peptides that no longer have any peptide instances
            // delete PSMs whose peptide is gone
            // delete spectra that no longer have any PSMs
            additionalPeptidesDeleteCommand.Append("DELETE FROM PeptideInstances WHERE Protein NOT IN (SELECT Id FROM Proteins);");
            additionalPeptidesDeleteCommand.Append("DELETE FROM Peptides WHERE Id NOT IN (SELECT Peptide FROM PeptideInstances);");
            additionalPeptidesDeleteCommand.Append("DELETE FROM PeptideSpectrumMatches WHERE Peptide NOT IN (SELECT Id FROM Peptides);");
            //additionalPeptidesDeleteCommand.Append("DELETE FROM Spectra WHERE Id NOT IN (SELECT Spectrum FROM PeptideSpectrumMatches);");
            #endregion

            session.CreateSQLQuery(additionalPeptidesDeleteCommand.ToString()).ExecuteUpdate();
        }

        /// <summary>
        /// Calculate clusters (connected components) for proteins
        /// </summary>
        void AssembleClusters (NHibernate.ISession session)
        {
            Map<long, long> clusterByProteinId = calculateProteinClusters(session);

            var cmd = session.Connection.CreateCommand();
            cmd.CommandText = "UPDATE Proteins SET Cluster = ? WHERE Id = ?";
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
        }

        #region Definitions for common HQL strings
        public static readonly string FromProtein = "Protein pro";
        public static readonly string FromPeptide = "Peptide pep";
        public static readonly string FromPeptideSpectrumMatch = "PeptideSpectrumMatch psm";
        public static readonly string FromPeptideInstance = "PeptideInstance pi";
        public static readonly string FromPeptideModification = "PeptideModification pm";
        public static readonly string FromModification = "Modification mod";
        public static readonly string FromSpectrum = "Spectrum s";
        public static readonly string FromSpectrumSource = "SpectrumSource ss";
        public static readonly string FromSpectrumSourceGroupLink = "SpectrumSourceGroupLink ssgl";
        public static readonly string FromSpectrumSourceGroup = "SpectrumSourceGroup ssg";

        public static readonly string ProteinToPeptideInstance = "pro.Peptides pi";
        public static readonly string ProteinToPeptide = ProteinToPeptideInstance + ";pi.Peptide pep";
        public static readonly string ProteinToPeptideSpectrumMatch = ProteinToPeptide + ";pep.Matches psm";
        public static readonly string ProteinToPeptideModification = ProteinToPeptideSpectrumMatch + ";psm.Modifications pm";
        public static readonly string ProteinToModification = ProteinToPeptideModification + ";pm.Modification mod";
        public static readonly string ProteinToSpectrum = ProteinToPeptideSpectrumMatch + ";psm.Spectrum s";
        public static readonly string ProteinToSpectrumSource = ProteinToSpectrum + ";s.Source ss";
        public static readonly string ProteinToSpectrumSourceGroupLink = ProteinToSpectrumSource + ";ss.Groups ssgl";
        public static readonly string ProteinToSpectrumSourceGroup = ProteinToSpectrumSourceGroupLink + ";ssgl.Group";

        public static readonly string PeptideToPeptideInstance = "pep.Instances pi";
        public static readonly string PeptideToPeptideSpectrumMatch = "pep.Matches psm";
        public static readonly string PeptideToProtein = PeptideToPeptideInstance + ";pi.Protein pro";
        public static readonly string PeptideToPeptideModification = PeptideToPeptideSpectrumMatch + ";psm.Modifications pm";
        public static readonly string PeptideToModification = PeptideToPeptideModification + ";pm.Modification mod";
        public static readonly string PeptideToSpectrum = PeptideToPeptideSpectrumMatch + ";psm.Spectrum s";
        public static readonly string PeptideToSpectrumSource = PeptideToSpectrum + ";s.Source ss";
        public static readonly string PeptideToSpectrumSourceGroupLink = PeptideToSpectrumSource + ";ss.Groups ssgl";
        public static readonly string PeptideToSpectrumSourceGroup = PeptideToSpectrumSourceGroupLink + ";ssgl.Group";

        public static readonly string PeptideSpectrumMatchToPeptide = "psm.Peptide pep";
        public static readonly string PeptideSpectrumMatchToSpectrum = "psm.Spectrum s";
        public static readonly string PeptideSpectrumMatchToPeptideModification = "psm.Modifications pm";
        public static readonly string PeptideSpectrumMatchToPeptideInstance = PeptideSpectrumMatchToPeptide + ";pep.Instances pi";
        public static readonly string PeptideSpectrumMatchToProtein = PeptideSpectrumMatchToPeptideInstance + ";pi.Protein pro";
        public static readonly string PeptideSpectrumMatchToModification = PeptideSpectrumMatchToPeptideModification + ";pm.Modification mod";
        public static readonly string PeptideSpectrumMatchToSpectrumSource = PeptideSpectrumMatchToSpectrum + ";s.Source ss";
        public static readonly string PeptideSpectrumMatchToSpectrumSourceGroupLink = PeptideSpectrumMatchToSpectrumSource + ";ss.Groups ssgl";
        public static readonly string PeptideSpectrumMatchToSpectrumSourceGroup = PeptideSpectrumMatchToSpectrumSourceGroupLink + ";ssgl.Group";
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

                if (Modifications.Count > 0)
                    foreach (var branch in ProteinToPeptideModification.Split(';'))
                        if (!joins.Contains(branch))
                            joins.Add(branch);

                if (ModifiedSite != null)
                    foreach (var branch in ProteinToPeptideModification.Split(';'))
                        if (!joins.Contains(branch))
                            joins.Add(branch);

                if (DistinctPeptideKey != null)
                    foreach (var branch in ProteinToPeptideSpectrumMatch.Split(';'))
                        if (!joins.Contains(branch))
                            joins.Add(branch);

                if (Spectrum != null)
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

                if (Modifications.Count > 0)
                    foreach (var branch in PeptideSpectrumMatchToPeptideModification.Split(';'))
                        if (!joins.Contains(branch))
                            joins.Add(branch);

                if (ModifiedSite != null)
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

            if (Spectrum != null)
                conditions.Add(String.Format("psm.Spectrum.id = {0}", Spectrum.Id));

            if (SpectrumSource != null)
                conditions.Add(String.Format("psm.Spectrum.Source.id = {0}", SpectrumSource.Id));

            if (SpectrumSourceGroup != null)
                conditions.Add(String.Format("ssgl.Group = {0}", SpectrumSourceGroup.Id));

            var query = new StringBuilder();

            query.AppendFormat("FROM {0} ", fromTable);
            foreach (var join in joins)
                query.AppendFormat("JOIN {0} ", join);
            query.Append(" ");

            if (conditions.Count > 0)
            {
                query.Append("WHERE ");
                query.Append(String.Join(" AND ", conditions.ToArray()));
                query.Append(" ");
            }

            return query.ToString();
        }

        public IList<ToolStripItem> GetBasicFilterControls (IDPickerForm form)
        {
            var result = new List<ToolStripItem>();

            result.Add(new ToolStripLabel() { Text = "Q-value ≤ ", RightToLeft = RightToLeft.No });
            var qvalueTextBox = new ToolStripTextBox() { Width = 40, RightToLeft = RightToLeft.No, BorderStyle = BorderStyle.FixedSingle, Text = MaximumQValue.ToString() };
            qvalueTextBox.KeyDown += new KeyEventHandler(doubleTextBox_KeyDown);
            qvalueTextBox.Leave += new EventHandler(form.qvalueTextBox_Leave);
            result.Add(qvalueTextBox);

            result.Add(new ToolStripLabel() { Text = "  Distinct Peptides ≥ ", RightToLeft = RightToLeft.No });
            var peptidesTextBox = new ToolStripTextBox() { Width = 20, RightToLeft = RightToLeft.No, BorderStyle = BorderStyle.FixedSingle, Text = MinimumDistinctPeptidesPerProtein.ToString() };
            peptidesTextBox.KeyDown += new KeyEventHandler(integerTextBox_KeyDown);
            peptidesTextBox.Leave += new EventHandler(form.peptidesTextBox_Leave);
            result.Add(peptidesTextBox);

            result.Add(new ToolStripLabel() { Text = "  Spectra ≥ ", RightToLeft = RightToLeft.No });
            var spectraTextBox = new ToolStripTextBox() { Width = 20, RightToLeft = RightToLeft.No, BorderStyle = BorderStyle.FixedSingle, Text = MinimumSpectraPerProtein.ToString() };
            spectraTextBox.KeyDown += new KeyEventHandler(integerTextBox_KeyDown);
            spectraTextBox.Leave += new EventHandler(form.spectraTextBox_Leave);
            result.Add(spectraTextBox);

            result.Add(new ToolStripLabel() { Text = "  Additional Peptides ≥ ", RightToLeft = RightToLeft.No });
            var additionalPeptidesTextBox = new ToolStripTextBox() { Width = 20, RightToLeft = RightToLeft.No, BorderStyle = BorderStyle.FixedSingle, Text = MinimumAdditionalPeptidesPerProtein.ToString() };
            additionalPeptidesTextBox.KeyDown += new KeyEventHandler(integerTextBox_KeyDown);
            additionalPeptidesTextBox.Leave += new EventHandler(form.additionalPeptidesTextBox_Leave);
            result.Add(additionalPeptidesTextBox);

            result[0].Tag = this;

            return result;
        }

        static void doubleTextBox_KeyDown (object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Decimal || e.KeyCode == Keys.OemPeriod)
            {
                if ((sender as ToolStripTextBox).Text.Contains('.'))
                    e.SuppressKeyPress = true;
            }
            else if (!(e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9 ||
                    e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9 ||
                    e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back ||
                    e.KeyCode == Keys.Left || e.KeyCode == Keys.Right))
                e.SuppressKeyPress = true;
        }

        static void integerTextBox_KeyDown (object sender, KeyEventArgs e)
        {
            if (!(e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9 ||
                e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9 ||
                e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back ||
                e.KeyCode == Keys.Left || e.KeyCode == Keys.Right))
                e.SuppressKeyPress = true;
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

            try { session.CreateSQLQuery("DROP TABLE SpectrumResults").ExecuteUpdate(); } catch { }
            session.CreateSQLQuery("CREATE TEMP TABLE SpectrumResults AS " +
                                   "SELECT psm.Spectrum AS Spectrum, GROUP_CONCAT(DISTINCT psm.Peptide) AS Peptides, COUNT(DISTINCT pi.Protein) AS SharedResultCount " +
                                   "FROM PeptideSpectrumMatches psm " +
                                   "JOIN PeptideInstances pi ON psm.Peptide = pi.Peptide " +
                                   "GROUP BY psm.Spectrum").ExecuteUpdate();

            var queryByProtein = session.CreateSQLQuery("SELECT pro.Id, pro.ProteinGroup, SUM(sr.SharedResultCount) " +
                                               "FROM Proteins pro " +
                                               "JOIN (SELECT DISTINCT Peptide, Protein FROM PeptideInstances) pi ON pro.Id=pi.Protein " +
                                               "JOIN (SELECT * FROM PeptideSpectrumMatches GROUP BY Peptide) psm ON pi.Peptide=psm.Peptide " +
                                               "JOIN SpectrumResults sr ON psm.Spectrum = sr.Spectrum " +
                                               "GROUP BY pro.Id");

            // For each protein, get the list of peptides evidencing it;
            // an ambiguous spectrum will show up as a nested list of peptides
            var queryByResult = session.CreateSQLQuery("SELECT pro.Id, GROUP_CONCAT(sr.Peptides) " +
                                               "FROM Proteins pro " +
                                               "JOIN (SELECT DISTINCT Peptide, Protein FROM PeptideInstances) pi ON pro.Id=pi.Protein " +
                                               "JOIN (SELECT * FROM PeptideSpectrumMatches GROUP BY Peptide) psm ON pi.Peptide=psm.Peptide " +
                                               "JOIN SpectrumResults sr ON psm.Spectrum = sr.Spectrum " +
                                               "GROUP BY pro.Id, sr.Peptides");

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
