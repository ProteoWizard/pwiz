﻿//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
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
using pwiz.CLI.msdata;
using msdata = pwiz.CLI.msdata;

namespace IDPicker.DataModel
{
    public class BulkInserter
    {
        enum Table
        {
            Protein = 0, ProteinMetadata, ProteinData,
            Peptide, PeptideSequences, PeptideInstance,
            SpectrumSourceGroup, SpectrumSource, SpectrumSourceMetadata, SpectrumSourceGroupLink, Spectrum,
            Analysis, AnalysisParameter,
            PeptideSpectrumMatch, PeptideSpectrumMatchScoreName, PeptideSpectrumMatchScore,
            Modification, PeptideModification,
            IntegerSet,
            QonverterSettings
        }

        List<KeyValuePair<IDbCommand, List<object[]>>> insertCommandByTable;
        Dictionary<string, long> scoreIdByName;
        long currentMaxSequenceLength = -1;

        string diskFilepath;
        IDbConnection memConn;

        IDbTransaction transaction;
        static object ioMutex = new object();

        public BulkInserter (IDbConnection conn)
        {
            Reset(conn);
        }

        public BulkInserter (string diskFilepath)
        {
            Reset(diskFilepath);
        }

        public void Reset (string diskFilepath)
        {
            Reset(SessionFactoryFactory.CreateFile(":memory:"));
            this.diskFilepath = diskFilepath;
        }

        public void Reset (IDbConnection conn)
        {
            diskFilepath = null;

            memConn = conn;
            memConn.ExecuteNonQuery(@"PRAGMA journal_mode=OFF;
                                      PRAGMA synchronous=OFF;");
            SessionFactoryFactory.DropIndexes(memConn);

            insertCommandByTable = new List<KeyValuePair<IDbCommand, List<object[]>>>();
            for (int i = 0; i <= (int) Table.QonverterSettings; ++i)
                insertCommandByTable.Add(new KeyValuePair<IDbCommand, List<object[]>>());

            insertCommandByTable[(int) Table.Protein] = new KeyValuePair<IDbCommand, List<object[]>>(conn.CreateCommand(), new List<object[]>());
            insertCommandByTable[(int) Table.Protein].Key.CommandText = createInsertSql("Protein", "Id, Accession, Length");

            insertCommandByTable[(int) Table.ProteinMetadata] = new KeyValuePair<IDbCommand, List<object[]>>(conn.CreateCommand(), new List<object[]>());
            insertCommandByTable[(int) Table.ProteinMetadata].Key.CommandText = createInsertSql("ProteinMetadata", "Id, Description");

            insertCommandByTable[(int) Table.ProteinData] = new KeyValuePair<IDbCommand, List<object[]>>(conn.CreateCommand(), new List<object[]>());
            insertCommandByTable[(int) Table.ProteinData].Key.CommandText = createInsertSql("ProteinData", "Id, Sequence");

            insertCommandByTable[(int) Table.Peptide] = new KeyValuePair<IDbCommand, List<object[]>>(conn.CreateCommand(), new List<object[]>());
            insertCommandByTable[(int) Table.Peptide].Key.CommandText = createInsertSql("Peptide", "Id, MonoisotopicMass, MolecularWeight");

            insertCommandByTable[(int) Table.PeptideSequences] = new KeyValuePair<IDbCommand, List<object[]>>(conn.CreateCommand(), new List<object[]>());
            insertCommandByTable[(int) Table.PeptideSequences].Key.CommandText = createInsertSql("PeptideSequences", "Id, Sequence");

            insertCommandByTable[(int) Table.PeptideInstance] = new KeyValuePair<IDbCommand, List<object[]>>(conn.CreateCommand(), new List<object[]>());
            insertCommandByTable[(int) Table.PeptideInstance].Key.CommandText = createInsertSql("PeptideInstance", "Id, Protein, Peptide, Offset, Length, NTerminusIsSpecific, CTerminusIsSpecific, MissedCleavages");

            insertCommandByTable[(int) Table.Analysis] = new KeyValuePair<IDbCommand, List<object[]>>(conn.CreateCommand(), new List<object[]>());
            insertCommandByTable[(int) Table.Analysis].Key.CommandText = createInsertSql("Analysis", "Id, Name, SoftwareName, SoftwareVersion, StartTime, Type");

            insertCommandByTable[(int) Table.AnalysisParameter] = new KeyValuePair<IDbCommand, List<object[]>>(conn.CreateCommand(), new List<object[]>());
            insertCommandByTable[(int) Table.AnalysisParameter].Key.CommandText = createInsertSql("AnalysisParameter", "Id, Analysis, Name, Value");

            insertCommandByTable[(int) Table.SpectrumSourceGroup] = new KeyValuePair<IDbCommand, List<object[]>>(conn.CreateCommand(), new List<object[]>());
            insertCommandByTable[(int) Table.SpectrumSourceGroup].Key.CommandText = createInsertSql("SpectrumSourceGroup", "Id, Name");

            insertCommandByTable[(int) Table.SpectrumSource] = new KeyValuePair<IDbCommand, List<object[]>>(conn.CreateCommand(), new List<object[]>());
            insertCommandByTable[(int) Table.SpectrumSource].Key.CommandText = createInsertSql("SpectrumSource", "Id, Name, URL, Group_");

            insertCommandByTable[(int) Table.SpectrumSourceMetadata] = new KeyValuePair<IDbCommand, List<object[]>>(conn.CreateCommand(), new List<object[]>());
            insertCommandByTable[(int) Table.SpectrumSourceMetadata].Key.CommandText = createInsertSql("SpectrumSourceMetadata", "Id, MsDataBytes");

            insertCommandByTable[(int) Table.SpectrumSourceGroupLink] = new KeyValuePair<IDbCommand, List<object[]>>(conn.CreateCommand(), new List<object[]>());
            insertCommandByTable[(int) Table.SpectrumSourceGroupLink].Key.CommandText = createInsertSql("SpectrumSourceGroupLink", "Id, Source, Group_");

            insertCommandByTable[(int) Table.Spectrum] = new KeyValuePair<IDbCommand, List<object[]>>(conn.CreateCommand(), new List<object[]>());
            insertCommandByTable[(int) Table.Spectrum].Key.CommandText = createInsertSql("Spectrum", "Id, Index_, NativeID, Source, PrecursorMZ");

            insertCommandByTable[(int) Table.PeptideSpectrumMatch] = new KeyValuePair<IDbCommand, List<object[]>>(conn.CreateCommand(), new List<object[]>());
            insertCommandByTable[(int) Table.PeptideSpectrumMatch].Key.CommandText =
                createInsertSql("PeptideSpectrumMatch",
                                "Id, Peptide, Spectrum, Analysis, " +
                                "ObservedNeutralMass, " +
                                "MonoisotopicMassError, MolecularWeightError, " +
                                "Rank, QValue, Charge");

            insertCommandByTable[(int) Table.PeptideSpectrumMatchScoreName] = new KeyValuePair<IDbCommand, List<object[]>>(conn.CreateCommand(), new List<object[]>());
            insertCommandByTable[(int) Table.PeptideSpectrumMatchScoreName].Key.CommandText = createInsertSql("PeptideSpectrumMatchScoreName", "Id, Name");

            insertCommandByTable[(int) Table.PeptideSpectrumMatchScore] = new KeyValuePair<IDbCommand, List<object[]>>(conn.CreateCommand(), new List<object[]>());
            insertCommandByTable[(int) Table.PeptideSpectrumMatchScore].Key.CommandText = createInsertSql("PeptideSpectrumMatchScore", "PsmId, ScoreNameId, Value");

            insertCommandByTable[(int) Table.Modification] = new KeyValuePair<IDbCommand, List<object[]>>(conn.CreateCommand(), new List<object[]>());
            insertCommandByTable[(int) Table.Modification].Key.CommandText = createInsertSql("Modification", "Id, MonoMassDelta, AvgMassDelta, Name, Formula");

            insertCommandByTable[(int) Table.PeptideModification] = new KeyValuePair<IDbCommand, List<object[]>>(conn.CreateCommand(), new List<object[]>());
            insertCommandByTable[(int) Table.PeptideModification].Key.CommandText = createInsertSql("PeptideModification", "Id, Modification, PeptideSpectrumMatch, Offset, Site");

            insertCommandByTable[(int) Table.IntegerSet] = new KeyValuePair<IDbCommand, List<object[]>>(conn.CreateCommand(), new List<object[]>());
            insertCommandByTable[(int) Table.IntegerSet].Key.CommandText = "INSERT INTO IntegerSet VALUES (?)";

            insertCommandByTable[(int) Table.QonverterSettings] = new KeyValuePair<IDbCommand, List<object[]>>(conn.CreateCommand(), new List<object[]>());
            insertCommandByTable[(int) Table.QonverterSettings].Key.CommandText = createInsertSql("QonverterSettings", "Id, QonverterMethod, DecoyPrefix, RerankMatches, ScoreInfoByName");

            foreach (var itr in insertCommandByTable)
            {
                createParameters(itr.Key);
                itr.Key.Prepare();
            }

            scoreIdByName = new Dictionary<string, long>();

            foreach (var queryRow in conn.ExecuteQuery("SELECT Name, Id FROM PeptideSpectrumMatchScoreName"))
                scoreIdByName[queryRow.GetString(0)] = queryRow.GetInt64(1);

            /*diskConn.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS PeptideSequences (Id INTEGER PRIMARY KEY, Sequence TEXT)");
            diskConn.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS IntegerSet (Value INTEGER PRIMARY KEY)");
            currentMaxSequenceLength = diskConn.ExecuteQuery("SELECT IFNULL(MAX(Value),0) FROM IntegerSet").First().GetInt64(0);
            */
            memConn.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS PeptideSequences (Id INTEGER PRIMARY KEY, Sequence TEXT)");
            transaction = memConn.BeginTransaction();
        }

        public void Execute ()
        {
            SessionFactoryFactory.CreateIndexes(memConn);
            transaction.Commit();

            if(String.IsNullOrEmpty(diskFilepath))
                return;

            lock (ioMutex)
            {
                if (!File.Exists(diskFilepath))
                {
                    (memConn as System.Data.SQLite.SQLiteConnection).SaveToDisk(diskFilepath);
                }
                else
                {
                    memConn.ExecuteNonQuery("ATTACH DATABASE '" + diskFilepath.Replace("'", "''") + "' AS disk");
                    memConn.ExecuteNonQuery("PRAGMA disk.journal_mode=OFF; PRAGMA disk.synchronous=OFF;");

                    transaction = memConn.BeginTransaction();
                    memConn.ExecuteNonQuery("INSERT INTO disk.Protein SELECT * FROM Protein");
                    memConn.ExecuteNonQuery("INSERT INTO disk.ProteinMetadata SELECT * FROM ProteinMetadata");
                    memConn.ExecuteNonQuery("INSERT INTO disk.ProteinData SELECT * FROM ProteinData");
                    memConn.ExecuteNonQuery("INSERT INTO disk.Peptide SELECT * FROM Peptide");
                    memConn.ExecuteNonQuery("INSERT INTO disk.PeptideSequences SELECT * FROM PeptideSequences");
                    memConn.ExecuteNonQuery("INSERT INTO disk.PeptideInstance SELECT * FROM PeptideInstance");
                    memConn.ExecuteNonQuery("INSERT INTO disk.SpectrumSourceGroup SELECT * FROM SpectrumSourceGroup");
                    memConn.ExecuteNonQuery("INSERT INTO disk.SpectrumSource SELECT * FROM SpectrumSource");
                    memConn.ExecuteNonQuery("INSERT INTO disk.SpectrumSourceMetadata SELECT * FROM SpectrumSourceMetadata");
                    memConn.ExecuteNonQuery("INSERT INTO disk.SpectrumSourceGroupLink SELECT * FROM SpectrumSourceGroupLink");
                    memConn.ExecuteNonQuery("INSERT INTO disk.Spectrum SELECT * FROM Spectrum");
                    memConn.ExecuteNonQuery("INSERT INTO disk.Analysis SELECT * FROM Analysis");
                    memConn.ExecuteNonQuery("INSERT INTO disk.AnalysisParameter SELECT * FROM AnalysisParameter");
                    memConn.ExecuteNonQuery("INSERT INTO disk.PeptideSpectrumMatch SELECT * FROM PeptideSpectrumMatch");
                    memConn.ExecuteNonQuery("INSERT INTO disk.PeptideSpectrumMatchScoreName SELECT * FROM PeptideSpectrumMatchScoreName");
                    memConn.ExecuteNonQuery("INSERT INTO disk.PeptideSpectrumMatchScore SELECT * FROM PeptideSpectrumMatchScore");
                    memConn.ExecuteNonQuery("INSERT INTO disk.Modification SELECT * FROM Modification");
                    memConn.ExecuteNonQuery("INSERT INTO disk.PeptideModification SELECT * FROM PeptideModification");
                    memConn.ExecuteNonQuery("INSERT OR IGNORE INTO disk.IntegerSet SELECT * FROM IntegerSet");
                    memConn.ExecuteNonQuery("INSERT INTO disk.QonverterSettings SELECT * FROM QonverterSettings");

                    /*foreach (var itr in insertCommandByTable)
                    {
                        IDbCommand cmd = itr.Key;
                        foreach (var row in itr.Value)
                        {
                            setParameters(cmd, row);
                            cmd.ExecuteNonQuery();
                        }
                    }*/

                    transaction.Commit();

                    memConn.ExecuteNonQuery("DETACH DATABASE disk");
                }
            }
        }

        void insertRow (Table table, params object[] values)
        {
            IDbCommand cmd = insertCommandByTable[(int) table].Key;
            setParameters(cmd, values);
            cmd.ExecuteNonQuery();
            //insertCommandByTable[(int) table].Value.Add(values);
        }

        static string createInsertSql (string table, string columns)
        {
            int numColumns = columns.ToCharArray().Count(o => o == ',') + 1;
            var parameterPlaceholders = new List<string>();
            for (int i = 0; i < numColumns; ++i) parameterPlaceholders.Add("?");
            string parameterPlaceholderList = String.Join(",", parameterPlaceholders.ToArray());

            return String.Format("INSERT INTO {0} ({1}) VALUES ({2})", table, columns, parameterPlaceholderList);
        }

        static void createParameters (IDbCommand cmd)
        {
            int parameterCount = cmd.CommandText.ToCharArray().Count(o => o == '?');
            cmd.Parameters.Clear();
            for (int i = 0; i < parameterCount; ++i)
            {
                var parameter = cmd.CreateParameter();
                cmd.Parameters.Add(parameter);
            }
        }

        static void setParameters(IDbCommand cmd, params object[] values)
        {
            for (int i=0; i < values.Length; ++i)
                (cmd.Parameters[i] as IDbDataParameter).Value = values[i];
        }

        public void Add (Protein pro)
        {
            insertRow(Table.Protein, new object[] {pro.Id, pro.Accession, pro.Length});
            insertRow(Table.ProteinMetadata, new object[] {pro.Id, pro.Description});
            insertRow(Table.ProteinData, new object[] {pro.Id, pro.Sequence});

            long length = pro.Sequence != null ? pro.Sequence.Length : 0;

            for (long i = currentMaxSequenceLength + 1; i <= length; ++i)
                insertRow(Table.IntegerSet, new object[] {i});
            currentMaxSequenceLength = Math.Max(length, currentMaxSequenceLength);
        }

        public void Add (Peptide pep)
        {
            insertRow(Table.Peptide, new object[] {pep.Id, pep.MonoisotopicMass, pep.MolecularWeight});
            insertRow(Table.PeptideSequences, new object[] {pep.Id, pep.Sequence});
        }

        public void Add (PeptideInstance pi)
        {
            insertRow(Table.PeptideInstance, new object[]
                                                  {
                                                      pi.Id, pi.Protein.Id, pi.Peptide.Id, pi.Offset, pi.Length,
                                                      pi.NTerminusIsSpecific ? 1 : 0,
                                                      pi.CTerminusIsSpecific ? 1 : 0,
                                                      pi.MissedCleavages
                                                  });
        }

        public void Add (Analysis a)
        {
            insertRow(Table.Analysis, new object[] {a.Id, a.Name, a.Software.Name, a.Software.Version, a.StartTime, (int) a.Type});

            foreach (AnalysisParameter ap in a.Parameters)
                insertRow(Table.AnalysisParameter, new object[] {ap.Id, a.Id, ap.Name, ap.Value});
        }

        public void Add (SpectrumSourceGroup ssg)
        {
            insertRow(Table.SpectrumSourceGroup, new object[] {ssg.Id, ssg.Name});
        }

        public void Add (SpectrumSource ss)
        {
            byte[] msdataBytes = null;
            if (ss.Metadata != null)
            {
                string tmpFilepath = Path.GetTempFileName() + ".mz5";
                msdata.MSDataFile.write(ss.Metadata, tmpFilepath,
                                        new msdata.MSDataFile.WriteConfig
                                        {
                                            format = MSDataFile.Format.Format_MZ5,
                                            compression = MSDataFile.Compression.Compression_Zlib
                                        });
                msdataBytes = File.ReadAllBytes(tmpFilepath);
                File.Delete(tmpFilepath);
            }

            insertRow(Table.SpectrumSource, new object[] {ss.Id, ss.Name, ss.URL, ss.Group.Id});
            insertRow(Table.SpectrumSourceMetadata, new object[] { ss.Id, msdataBytes });
        }

        public void Add (SpectrumSourceGroupLink ssgl)
        {
            insertRow(Table.SpectrumSourceGroupLink, new object[] {ssgl.Id, ssgl.Source.Id, ssgl.Group.Id});
        }

        public void Add (Spectrum s)
        {
            insertRow(Table.Spectrum, new object[] {s.Id, s.Index, s.NativeID, s.Source.Id, s.PrecursorMZ});
        }

        public void Add (PeptideSpectrumMatch psm)
        {
            insertRow(Table.PeptideSpectrumMatch, new object[]
                                                        {
                                                            psm.Id, psm.Peptide.Id, psm.Spectrum.Id, psm.Analysis.Id,
                                                            psm.ObservedNeutralMass,
                                                            psm.MonoisotopicMassError, psm.MolecularWeightError,
                                                            psm.Rank, psm.QValue, psm.Charge
                                                        });

            if (psm.Scores != null)
                foreach (var score in psm.Scores)
                {
                    long scoreId;
                    if (!scoreIdByName.TryGetValue(score.Key, out scoreId))
                    {
                        if (scoreIdByName.Count > 0)
                            scoreIdByName[score.Key] = scoreId = scoreIdByName.Max(o => o.Value) + 1;
                        else
                            scoreIdByName[score.Key] = scoreId = 1;

                        insertRow(Table.PeptideSpectrumMatchScoreName, new object[] { scoreId, score.Key });
                    }
                    insertRow(Table.PeptideSpectrumMatchScore, new object[] {psm.Id, scoreId, score.Value});
                }
        }

        public void Add (Modification mod)
        {
            insertRow(Table.Modification, new object[] {mod.Id, mod.MonoMassDelta, mod.AvgMassDelta, mod.Name, mod.Formula});
        }

        public void Add (PeptideModification pm)
        {
            insertRow(Table.PeptideModification, new object[] {pm.Id, pm.Modification.Id, pm.PeptideSpectrumMatch.Id, pm.Offset, pm.Site.ToString()});
        }

        public void Add (QonverterSettings qs)
        {
            insertRow(Table.QonverterSettings, new object[] {qs.Analysis.Id, qs.QonverterMethod, qs.DecoyPrefix, qs.RerankMatches, QonverterSettings.assembleScoreInfo(qs.ScoreInfoByName)});
        }
    }
}
