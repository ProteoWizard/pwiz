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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using msdata = pwiz.CLI.msdata;

namespace IDPicker.DataModel
{
    public class BulkInserter
    {
        public List<Protein> Proteins { get; set; }
        public List<Peptide> Peptides { get; set; }
        public List<PeptideInstance> PeptideInstances { get; set; }
        public List<SpectrumSourceGroup> SpectrumSourceGroups { get; set; }
        public List<SpectrumSourceGroupLink> SpectrumSourceGroupLinks { get; set; }
        public List<SpectrumSource> SpectrumSources { get; set; }
        public List<Spectrum> Spectra { get; set; }
        public List<Analysis> Analyses { get; set; }
        public List<PeptideSpectrumMatch> PeptideSpectrumMatches { get; set; }
        public List<PeptideModification> PeptideModifications { get; set; }
        public List<Modification> Modifications { get; set; }

        public BulkInserter ()
        {
            Reset();
        }

        public void Reset ()
        {
            Proteins = new List<Protein>();
            Peptides = new List<Peptide>();
            PeptideInstances = new List<PeptideInstance>();
            SpectrumSourceGroups = new List<SpectrumSourceGroup>();
            SpectrumSourceGroupLinks = new List<SpectrumSourceGroupLink>();
            SpectrumSources = new List<SpectrumSource>();
            Spectra = new List<Spectrum>();
            Analyses = new List<Analysis>();
            PeptideSpectrumMatches = new List<PeptideSpectrumMatch>();
            PeptideModifications = new List<PeptideModification>();
            Modifications = new List<Modification>();
        }

        public void Execute(IDbConnection conn)
        {
            var transaction = conn.BeginTransaction();
            insertProteins(conn);
            insertPeptides(conn);
            insertPeptideInstances(conn);
            insertAnalyses(conn);
            insertSpectrumSourceGroups(conn);
            insertSpectrumSources(conn);
            insertSpectrumSourceGroupLinks(conn);
            insertSpectra(conn);
            insertPeptideSpectrumMatches(conn);
            insertModifications(conn);
            insertPeptideModifications(conn);
            transaction.Commit();
        }

        string createInsertSql (string table, string columns)
        {
            int numColumns = columns.ToCharArray().Count(o => o == ',') + 1;
            var parameterPlaceholders = new List<string>();
            for (int i = 0; i < numColumns; ++i) parameterPlaceholders.Add("?");
            string parameterPlaceholderList = String.Join(",", parameterPlaceholders.ToArray());

            return string.Format("INSERT INTO {0} ({1}) VALUES ({2})", table, columns, parameterPlaceholderList);
        }

        List<IDbDataParameter> createParameters (IDbCommand cmd)
        {
            int parameterCount = cmd.CommandText.ToCharArray().Count(o => o == '?');
            var parameters = new List<IDbDataParameter>();
            for (int i = 0; i < parameterCount; ++i)
            {
                var parameter = cmd.CreateParameter();
                parameters.Add(parameter);
                cmd.Parameters.Add(parameter);
            }
            return parameters;
        }

        void setParameters(IList<IDbDataParameter> parameters, params object[] values)
        {
            for (int i=0; i < values.Length; ++i)
                parameters[i].Value = values[i];
        }

        void insertProteins (IDbConnection conn)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = createInsertSql("Proteins", "Id, Accession, Description, Sequence");
            var parameters = createParameters(cmd);
            cmd.Prepare();

            foreach (Protein pro in Proteins)
            {
                setParameters(parameters, pro.Id, pro.Accession, pro.Description, pro.Sequence);
                cmd.ExecuteNonQuery();
            }
        }

        void insertPeptides (IDbConnection conn)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = createInsertSql("Peptides", "Id, MonoisotopicMass, MolecularWeight");
            var parameters = createParameters(cmd);
            cmd.Prepare();

            foreach (Peptide pep in Peptides)
            {
                setParameters(parameters, pep.Id, pep.MonoisotopicMass, pep.MolecularWeight);
                cmd.ExecuteNonQuery();
            }
        }

        void insertPeptideInstances (IDbConnection conn)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = createInsertSql("PeptideInstances", "Id, Protein, Peptide, Offset, Length, NTerminusIsSpecific, CTerminusIsSpecific, MissedCleavages");
            var parameters = createParameters(cmd);
            cmd.Prepare();

            foreach (PeptideInstance pi in PeptideInstances)
            {
                setParameters(parameters, pi.Id, pi.Protein.Id, pi.Peptide.Id, pi.Offset, pi.Length,
                              pi.NTerminusIsSpecific ? 1 : 0,
                              pi.CTerminusIsSpecific ? 1 : 0,
                              pi.MissedCleavages);
                cmd.ExecuteNonQuery();
            }
        }

        void insertAnalyses (IDbConnection conn)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = createInsertSql("Analyses", "Id, Name, SoftwareName, SoftwareVersion, StartTime, Type");
            var parameters = createParameters(cmd);
            cmd.Prepare();

            var cmd2 = conn.CreateCommand();
            cmd2.CommandText = createInsertSql("AnalysisParameters", "Id, Analysis, Name, Value");
            var parameters2 = createParameters(cmd2);
            cmd2.Prepare();

            foreach (Analysis a in Analyses)
            {
                setParameters(parameters, a.Id, a.Name, a.Software.Name, a.Software.Version, a.StartTime, (int) a.Type);
                cmd.ExecuteNonQuery();

                foreach (AnalysisParameter ap in a.Parameters)
                {
                    setParameters(parameters2, ap.Id, a.Id, ap.Name, ap.Value);
                    cmd2.ExecuteNonQuery();
                }
            }
        }

        void insertSpectrumSourceGroups (IDbConnection conn)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = createInsertSql("SpectrumSourceGroups", "Id, Name");
            var parameters = createParameters(cmd);
            cmd.Prepare();

            foreach (SpectrumSourceGroup ssg in SpectrumSourceGroups)
            {
                setParameters(parameters, ssg.Id, ssg.Name);
                cmd.ExecuteNonQuery();
            }
        }

        void insertSpectrumSources (IDbConnection conn)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = createInsertSql("SpectrumSources", "Id, Name, URL, Group_, MsDataBytes");
            var parameters = createParameters(cmd);
            cmd.Prepare();

            foreach (SpectrumSource ss in SpectrumSources)
            {
                byte[] msdataBytes = null;
                if (ss.Metadata != null)
                {
                    string tmpFilepath = Path.GetTempFileName() + ".mzML.gz";
                    msdata.MSDataFile.write(ss.Metadata, tmpFilepath,
                                            new msdata.MSDataFile.WriteConfig() { gzipped = true });
                    msdataBytes = File.ReadAllBytes(tmpFilepath);
                    File.Delete(tmpFilepath);
                }

                setParameters(parameters, ss.Id, ss.Name, ss.URL, ss.Group.Id, msdataBytes);
                cmd.ExecuteNonQuery();
            }
        }

        void insertSpectrumSourceGroupLinks (IDbConnection conn)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = createInsertSql("SpectrumSourceGroupLinks", "Id, Source, Group_");
            var parameters = createParameters(cmd);
            cmd.Prepare();

            foreach (SpectrumSourceGroupLink ssgl in SpectrumSourceGroupLinks)
            {
                setParameters(parameters, ssgl.Id, ssgl.Source.Id, ssgl.Group.Id);
                cmd.ExecuteNonQuery();
            }
        }

        void insertSpectra (IDbConnection conn)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = createInsertSql("Spectra", "Id, Index_, NativeID, Source, PrecursorMZ");
            var parameters = createParameters(cmd);
            cmd.Prepare();

            foreach (Spectrum s in Spectra)
            {
                setParameters(parameters, s.Id, s.Index, s.NativeID, s.Source.Id, s.PrecursorMZ);
                cmd.ExecuteNonQuery();
            }
        }

        void insertPeptideSpectrumMatches (IDbConnection conn)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = createInsertSql("PeptideSpectrumMatches",
                                              "Id, Peptide, Spectrum, Analysis, " +
                                              "MonoisotopicMass, MolecularWeight, " +
                                              "MonoisotopicMassError, MolecularWeightError, " +
                                              "Rank, QValue, Charge");
            var parameters = createParameters(cmd);
            cmd.Prepare();

            var cmd2 = conn.CreateCommand();
            cmd2.CommandText = createInsertSql("PeptideSpectrumMatchScores", "Id, Name, Value");
            var parameters2 = createParameters(cmd2);
            cmd2.Prepare();

            foreach (PeptideSpectrumMatch psm in PeptideSpectrumMatches)
            {
                setParameters(parameters,
                              psm.Id, psm.Peptide.Id, psm.Spectrum.Id, psm.Analysis.Id,
                              psm.MonoisotopicMass, psm.MolecularWeight,
                              psm.MonoisotopicMassError, psm.MolecularWeightError,
                              psm.Rank, psm.QValue, psm.Charge);
                cmd.ExecuteNonQuery();

                if (psm.Scores != null)
                {
                    foreach (var score in psm.Scores)
                    {
                        setParameters(parameters2, psm.Id, score.Key, score.Value);
                        cmd2.ExecuteNonQuery();
                    }
                }
            }
        }

        void insertModifications (IDbConnection conn)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = createInsertSql("Modifications", "Id, MonoMassDelta, AvgMassDelta, Name, Formula");
            var parameters = createParameters(cmd);
            cmd.Prepare();

            foreach (Modification mod in Modifications)
            {
                setParameters(parameters, mod.Id, mod.MonoMassDelta, mod.AvgMassDelta, mod.Name, mod.Formula);
                cmd.ExecuteNonQuery();
            }
        }

        void insertPeptideModifications (IDbConnection conn)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = createInsertSql("PeptideModifications", "Id, Modification, PeptideSpectrumMatch, Offset, Site");
            var parameters = createParameters(cmd);
            cmd.Prepare();

            foreach (PeptideModification pm in PeptideModifications)
            {
                setParameters(parameters, pm.Id, pm.Modification.Id, pm.PeptideSpectrumMatch.Id, pm.Offset, pm.Site.ToString());
                cmd.ExecuteNonQuery();
            }
        }
    }
}
