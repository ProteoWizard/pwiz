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
using System.Data;
using System.Data.SQLite;
using System.ComponentModel;
using System.Threading;

namespace IDPicker.DataModel
{
    public class Merger
    {
        #region Events
        public event EventHandler<MergingProgressEventArgs> MergingProgress;
        #endregion

        #region Event arguments
        public class MergingProgressEventArgs : CancelEventArgs
        {
            public int MergedFiles { get; set; }
            public int TotalFiles { get; set; }

            public Exception MergingException { get; set; }
        }
        #endregion

        public Merger (string mergeTargetFilepath, IEnumerable<string> mergeSourceFilepaths)
        {
            this.mergeTargetFilepath = mergeTargetFilepath;
            this.mergeSourceFilepaths = mergeSourceFilepaths;
        }

        public void Start ()
        {
            var workerThread = new Thread(mergeFiles);
            workerThread.Start();

            while (workerThread.IsAlive)
            {
                workerThread.Join(100);
                System.Windows.Forms.Application.DoEvents();
            }
        }


        void mergeFiles ()
        {
            if (!SessionFactoryFactory.IsValidFile(mergeTargetFilepath))
            {
                System.IO.File.Delete(mergeTargetFilepath);
                SessionFactoryFactory.CreateFile(mergeTargetFilepath);
            }

            using (var conn = new SQLiteConnection("Data Source=:memory:"))
            {
                conn.Open();
                conn.ExecuteNonQuery("ATTACH DATABASE '" + mergeTargetFilepath + "' AS merged");
                conn.ExecuteNonQuery("PRAGMA journal_mode=OFF; PRAGMA synchronous=OFF; PRAGMA cache_size=" + tempCacheSize);
                conn.ExecuteNonQuery("PRAGMA merged.journal_mode=OFF; PRAGMA merged.synchronous=OFF; PRAGMA merged.cache_size=" + mergedCacheSize);

                conn.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS merged.MergedFiles (Filepath TEXT PRIMARY KEY)");

                string getMaxIdsSql =
                      @"SELECT (SELECT IFNULL(MAX(Id),0) FROM merged.Protein),
                               (SELECT IFNULL(MAX(Id),0) FROM merged.PeptideInstance),
                               (SELECT IFNULL(MAX(Id),0) FROM merged.Peptide),
                               (SELECT IFNULL(MAX(Id),0) FROM merged.PeptideSpectrumMatch),
                               (SELECT IFNULL(MAX(Id),0) FROM merged.PeptideSpectrumMatchScoreNames),
                               (SELECT IFNULL(MAX(Id),0) FROM merged.PeptideModification),
                               (SELECT IFNULL(MAX(Id),0) FROM merged.Modification),
                               (SELECT IFNULL(MAX(Id),0) FROM merged.SpectrumSourceGroup),
                               (SELECT IFNULL(MAX(Id),0) FROM merged.SpectrumSource),
                               (SELECT IFNULL(MAX(Id),0) FROM merged.SpectrumSourceGroupLink),
                               (SELECT IFNULL(MAX(Id),0) FROM merged.Spectrum),
                               (SELECT IFNULL(MAX(Id),0) FROM merged.Analysis)
                       ";
                var maxIds = conn.ExecuteQuery(getMaxIdsSql).First();
                MaxProteinId = (long) maxIds[0];
                MaxPeptideInstanceId = (long) maxIds[1];
                MaxPeptideId = (long) maxIds[2];
                MaxPeptideSpectrumMatchId = (long) maxIds[3];
                MaxPeptideSpectrumMatchScoreNameId = (long) maxIds[4];
                MaxPeptideModificationId = (long) maxIds[5];
                MaxModificationId = (long) maxIds[6];
                MaxSpectrumSourceGroupId = (long) maxIds[7];
                MaxSpectrumSourceId = (long) maxIds[8];
                MaxSpectrumSourceGroupLinkId = (long) maxIds[9];
                MaxSpectrumId = (long) maxIds[10];
                MaxAnalysisId = (long) maxIds[11];

                IDbTransaction transaction;

                int mergedFiles = 0;
                foreach (string mergeSourceFilepath in mergeSourceFilepaths)
                {
                    if (OnMergingProgress(null, ++mergedFiles))
                        break;

                    if (!System.IO.File.Exists(mergeSourceFilepath) || mergeSourceFilepath == mergeTargetFilepath)
                        continue;

                    // skip files that have already been merged
                    if (conn.ExecuteQuery("SELECT * FROM merged.MergedFiles WHERE Filepath = '" + mergeSourceFilepath + "'").Count() > 0)
                        continue;

                    conn.ExecuteNonQuery("ATTACH DATABASE '" + mergeSourceFilepath + "' AS new");
                    conn.ExecuteNonQuery("PRAGMA new.cache_size=" + newCacheSize);

                    transaction = conn.BeginTransaction();

                    try
                    {
                        mergeProteins(conn);
                        mergePeptideInstances(conn);
                        mergeSpectrumSourceGroups(conn);
                        mergeSpectrumSources(conn);
                        mergeSpectrumSourceGroupLinks(conn);
                        mergeSpectra(conn);
                        mergeModifications(conn);
                        mergePeptideSpectrumMatchScoreNames(conn);
                        mergeAnalyses(conn);
                        addNewProteins(conn);
                        addNewPeptideInstances(conn);
                        addNewPeptides(conn);
                        addNewModifications(conn);
                        addNewSpectrumSourceGroups(conn);
                        addNewSpectrumSources(conn);
                        addNewSpectrumSourceGroupLinks(conn);
                        addNewSpectra(conn);
                        addNewPeptideSpectrumMatches(conn);
                        addNewPeptideSpectrumMatchScoreNames(conn);
                        addNewPeptideSpectrumMatchScores(conn);
                        addNewPeptideModifications(conn);
                        addNewAnalyses(conn);
                        addNewAnalysisParameters(conn);
                        getNewMaxIds(conn);

                        conn.ExecuteNonQuery("INSERT INTO merged.MergedFiles VALUES ('" + mergeSourceFilepath + "')");
                    }
                    /*catch(Exception ex)
                    {
                        if(OnMergingProgress(ex, mergedFiles))
                            break;
                    }*/
                    finally
                    {
                        transaction.Commit();
                        conn.ExecuteNonQuery("DETACH DATABASE new");
                    }
                }

                transaction = conn.BeginTransaction();

                try
                {
                    addIntegerSet(conn);
                }
                finally
                {
                    transaction.Commit();
                }

            } // conn.Dispose()
        }

        static string mergeProteinsSql =
              @"DROP TABLE IF EXISTS ProteinMergeMap;
                CREATE TABLE ProteinMergeMap (BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);
                INSERT INTO ProteinMergeMap
                SELECT newPro.Id, IFNULL(oldPro.Id, newPro.Id+{0})
                FROM new.Protein newPro
                LEFT JOIN merged.Protein oldPro ON newPro.Accession = oldPro.Accession;
                CREATE UNIQUE INDEX ProteinMergeMap_Index2 ON ProteinMergeMap (AfterMergeId);

                DROP TABLE IF EXISTS NewProteins;
                CREATE TABLE NewProteins AS
                SELECT BeforeMergeId, AfterMergeId
                FROM ProteinMergeMap
                WHERE AfterMergeId > {0}
               ";
        void mergeProteins (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(mergeProteinsSql, MaxProteinId)); }


        static string mergePeptideInstancesSql =
              @"DROP TABLE IF EXISTS PeptideInstanceMergeMap;
                CREATE TABLE PeptideInstanceMergeMap (BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INTEGER, AfterMergeProtein INTEGER, BeforeMergePeptide INTEGER, AfterMergePeptide INTEGER);
                INSERT INTO PeptideInstanceMergeMap
                SELECT newInstance.Id, IFNULL(oldInstance.Id, newInstance.Id+{0}), IFNULL(oldInstance.Protein, proMerge.AfterMergeId), newInstance.Peptide, IFNULL(oldInstance.Peptide, newInstance.Peptide+{1})
                FROM ProteinMergeMap proMerge
                JOIN new.PeptideInstance newInstance ON proMerge.BeforeMergeId = newInstance.Protein
                LEFT JOIN merged.PeptideInstance oldInstance ON proMerge.AfterMergeId = oldInstance.Protein
                                                            AND newInstance.Length = oldInstance.Length
                                                            AND newInstance.Offset = oldInstance.Offset;
                CREATE UNIQUE INDEX PeptideInstanceMergeMap_Index2 ON PeptideInstanceMergeMap (AfterMergeId);
                CREATE INDEX PeptideInstanceMergeMap_Index3 ON PeptideInstanceMergeMap (BeforeMergePeptide);
                CREATE INDEX PeptideInstanceMergeMap_Index4 ON PeptideInstanceMergeMap (AfterMergePeptide);
               ";
        void mergePeptideInstances (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(mergePeptideInstancesSql, MaxPeptideInstanceId, MaxPeptideId)); }


        static string mergeAnalysesSql =
              @"DROP TABLE IF EXISTS AnalysisMergeMap;
                CREATE TABLE AnalysisMergeMap (BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);
                INSERT INTO AnalysisMergeMap
                SELECT newAnalysis.Id, IFNULL(oldAnalysis.Id, newAnalysis.Id+{0})
                FROM (SELECT a.Id, SoftwareName || ' ' || SoftwareVersion || ' ' || GROUP_CONCAT(ap.Name || ' ' || ap.Value) AS DistinctKey
                      FROM new.Analysis a
                      JOIN new.AnalysisParameter ap ON a.Id = Analysis
                      GROUP BY a.Id) newAnalysis
                LEFT JOIN (SELECT a.Id, SoftwareName || ' ' || SoftwareVersion || ' ' || GROUP_CONCAT(ap.Name || ' ' || ap.Value) AS DistinctKey
                           FROM merged.Analysis a
                           JOIN merged.AnalysisParameter ap ON a.Id = Analysis
                           GROUP BY a.Id) oldAnalysis ON newAnalysis.DistinctKey = oldAnalysis.DistinctKey;
               ";
        void mergeAnalyses (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(mergeAnalysesSql, MaxAnalysisId)); }


        static string mergeSpectrumSourceGroupsSql =
              @"DROP TABLE IF EXISTS SpectrumSourceGroupMergeMap;
                CREATE TABLE SpectrumSourceGroupMergeMap (BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);
                INSERT INTO SpectrumSourceGroupMergeMap
                SELECT newGroup.Id, IFNULL(oldGroup.Id, newGroup.Id+{0})
                FROM new.SpectrumSourceGroup newGroup
                LEFT JOIN merged.SpectrumSourceGroup oldGroup ON newGroup.Name = oldGroup.Name
               ";
        void mergeSpectrumSourceGroups (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(mergeSpectrumSourceGroupsSql, MaxSpectrumSourceGroupId)); }


        static string mergeSpectrumSourcesSql =
              @"DROP TABLE IF EXISTS SpectrumSourceMergeMap;
                CREATE TABLE SpectrumSourceMergeMap (BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);
                INSERT INTO SpectrumSourceMergeMap
                SELECT newSource.Id, IFNULL(oldSource.Id, newSource.Id+{0})
                FROM new.SpectrumSource newSource
                LEFT JOIN merged.SpectrumSource oldSource ON newSource.Name = oldSource.Name;
                CREATE UNIQUE INDEX SpectrumSourceMergeMap_Index2 ON SpectrumSourceMergeMap (AfterMergeId);
               ";
        void mergeSpectrumSources (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(mergeSpectrumSourcesSql, MaxSpectrumSourceId)); }


        static string mergeSpectrumSourceGroupLinksSql =
              @"DROP TABLE IF EXISTS SpectrumSourceGroupLinkMergeMap;
                CREATE TABLE SpectrumSourceGroupLinkMergeMap (BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);
                INSERT INTO SpectrumSourceGroupLinkMergeMap
                SELECT newLink.Id, IFNULL(oldLink.Id, newLink.Id+{0})
                FROM new.SpectrumSourceGroupLink newLink
                JOIN SpectrumSourceMergeMap ssMerge ON newLink.Source = ssMerge.BeforeMergeId
                JOIN SpectrumSourceGroupMergeMap ssgMerge ON newLink.Group_ = ssgMerge.BeforeMergeId
                LEFT JOIN merged.SpectrumSourceGroupLink oldLink ON ssMerge.AfterMergeId = oldLink.Source
                                                                AND ssgMerge.AfterMergeId = oldLink.Group_
               ";
        void mergeSpectrumSourceGroupLinks (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(mergeSpectrumSourceGroupLinksSql, MaxSpectrumSourceGroupLinkId)); }


        static string mergeSpectraSql =
              @"DROP TABLE IF EXISTS SpectrumMergeMap;
                CREATE TABLE SpectrumMergeMap (BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);
                INSERT INTO SpectrumMergeMap
                SELECT newSpectrum.Id, IFNULL(oldSpectrum.Id, newSpectrum.Id+{0})
                FROM new.Spectrum newSpectrum
                JOIN SpectrumSourceMergeMap ssMerge ON newSpectrum.Source = ssMerge.BeforeMergeId
                LEFT JOIN merged.Spectrum oldSpectrum ON ssMerge.AfterMergeId = oldSpectrum.Source
                                                     AND newSpectrum.Index_ = oldSpectrum.Index_;
               ";
        void mergeSpectra (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(mergeSpectraSql, MaxSpectrumId )); }


        static string mergeModificationsSql =
              @"DROP TABLE IF EXISTS ModificationMergeMap;
                CREATE TABLE ModificationMergeMap (BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);
                INSERT INTO ModificationMergeMap
                SELECT newMod.Id, IFNULL(oldMod.Id, newMod.Id+{0})
                FROM new.Modification newMod
                LEFT JOIN merged.Modification oldMod ON IFNULL(newMod.Formula, 1) = IFNULL(oldMod.Formula, 1)
                                                    AND newMod.MonoMassDelta = oldMod.MonoMassDelta
               ";
        void mergeModifications (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(mergeModificationsSql, MaxModificationId)); }


        static string mergePeptideSpectrumMatchScoreNamesSql =
              @"DROP TABLE IF EXISTS PeptideSpectrumMatchScoreNameMergeMap;
                CREATE TABLE PeptideSpectrumMatchScoreNameMergeMap (BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);
                INSERT INTO PeptideSpectrumMatchScoreNameMergeMap
                SELECT newName.Id, IFNULL(oldName.Id, newName.Id+{0})
                FROM new.PeptideSpectrumMatchScoreNames newName
                LEFT JOIN merged.PeptideSpectrumMatchScoreNames oldName ON newName.Name = oldName.Name
               ";
        void mergePeptideSpectrumMatchScoreNames (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(mergePeptideSpectrumMatchScoreNamesSql, MaxPeptideSpectrumMatchScoreNameId)); }


        static string addNewProteinsSql =
              @"INSERT INTO merged.Protein (Id, Accession, Length)
                SELECT AfterMergeId, Accession, Length
                FROM NewProteins
                JOIN new.Protein newPro ON BeforeMergeId = newPro.Id;

                INSERT INTO merged.ProteinMetadata
                SELECT AfterMergeId, Description
                FROM NewProteins
                JOIN new.ProteinMetadata newPro ON BeforeMergeId = newPro.Id;

                INSERT INTO merged.ProteinData
                SELECT AfterMergeId, Sequence
                FROM NewProteins
                JOIN new.ProteinData newPro ON BeforeMergeId = newPro.Id
               ";
        void addNewProteins (IDbConnection conn) { conn.ExecuteNonQuery(addNewProteinsSql); }


        static string addNewPeptideInstancesSql =
              @"INSERT INTO merged.PeptideInstance
                SELECT AfterMergeId, AfterMergeProtein, AfterMergePeptide,
                       Offset, Length, NTerminusIsSpecific, CTerminusIsSpecific, MissedCleavages
                FROM PeptideInstanceMergeMap piMerge
                JOIN new.PeptideInstance newInstance ON BeforeMergeId = newInstance.Id
                WHERE AfterMergeId > {0}
               ";
        void addNewPeptideInstances (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewPeptideInstancesSql, MaxPeptideInstanceId)); }


        static string addNewPeptidesSql =
              @"INSERT INTO merged.Peptide
                SELECT AfterMergePeptide, MonoisotopicMass, MolecularWeight
                FROM new.Peptide newPep
                JOIN PeptideInstanceMergeMap ON newPep.Id = BeforeMergePeptide
                WHERE AfterMergePeptide > {0}
                GROUP BY AfterMergePeptide
               ";
        void addNewPeptides (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewPeptidesSql, MaxPeptideId)); }


        static string addNewSpectrumSourceGroupsSql =
              @"INSERT INTO merged.SpectrumSourceGroup
                SELECT AfterMergeId, Name
                FROM new.SpectrumSourceGroup newGroup
                JOIN SpectrumSourceGroupMergeMap ssgMerge ON Id = BeforeMergeId
                WHERE AfterMergeId > {0}
               ";
        void addNewSpectrumSourceGroups (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewSpectrumSourceGroupsSql, MaxSpectrumSourceGroupId)); }


        static string addNewSpectrumSourcesSql =
              @"INSERT INTO merged.SpectrumSource
                SELECT ssMerge.AfterMergeId, Name, URL, ssgMerge.AfterMergeId, MsDataBytes
                FROM new.SpectrumSource newSource
                JOIN SpectrumSourceMergeMap ssMerge ON newSource.Id = ssMerge.BeforeMergeId
                JOIN SpectrumSourceGroupMergeMap ssgMerge ON newSource.Group_ = ssgMerge.BeforeMergeId
                WHERE ssMerge.AfterMergeId > {0}
               ";
        void addNewSpectrumSources (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewSpectrumSourcesSql, MaxSpectrumSourceId)); }


        static string addNewSpectrumSourceGroupLinksSql =
              @"INSERT INTO merged.SpectrumSourceGroupLink
                SELECT ssglMerge.AfterMergeId, ssMerge.AfterMergeId, ssgMerge.AfterMergeId
                FROM new.SpectrumSourceGroupLink newLink
                JOIN SpectrumSourceMergeMap ssMerge ON newLink.Source = ssMerge.BeforeMergeId
                JOIN SpectrumSourceGroupMergeMap ssgMerge ON newLink.Group_ = ssgMerge.BeforeMergeId
                JOIN SpectrumSourceGroupLinkMergeMap ssglMerge ON newLink.Id = ssglMerge.BeforeMergeId
                WHERE ssglMerge.AfterMergeId > {0}
               ";
        void addNewSpectrumSourceGroupLinks (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewSpectrumSourceGroupLinksSql, MaxSpectrumSourceGroupLinkId)); }


        static string addNewSpectraSql =
              @"INSERT INTO merged.Spectrum
                SELECT sMerge.AfterMergeId, ssMerge.AfterMergeId, Index_, NativeID, PrecursorMZ
                FROM new.Spectrum newSpectrum
                JOIN SpectrumSourceMergeMap ssMerge ON newSpectrum.Source = ssMerge.BeforeMergeId
                JOIN SpectrumMergeMap sMerge ON newSpectrum.Id = sMerge.BeforeMergeId
                WHERE sMerge.AfterMergeId > {0}
               ";
        void addNewSpectra (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewSpectraSql, MaxSpectrumId)); }


        static string addNewModificationsSql =
              @"INSERT INTO merged.Modification
                SELECT AfterMergeId, MonoMassDelta, AvgMassDelta, Formula, Name
                FROM new.Modification newMod
                JOIN ModificationMergeMap modMerge ON newMod.Id = BeforeMergeId
                WHERE AfterMergeId > {0}
               ";
        void addNewModifications (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewModificationsSql, MaxModificationId)); }


        static string addNewPeptideSpectrumMatchesSql =
              @"INSERT INTO merged.PeptideSpectrumMatch
                SELECT newPSM.Id+{0}, sMerge.AfterMergeId, aMerge.AfterMergeId, AfterMergePeptide,
                       QValue, MonoisotopicMass, MolecularWeight, MonoisotopicMassError, MolecularWeightError,
                       Rank, Charge
                FROM new.PeptideSpectrumMatch newPSM
                JOIN PeptideInstanceMergeMap piMerge ON Peptide = BeforeMergePeptide
                JOIN AnalysisMergeMap aMerge ON Analysis = aMerge.BeforeMergeId
                JOIN SpectrumMergeMap sMerge ON Spectrum = sMerge.BeforeMergeId
                GROUP BY newPSM.Id
                ORDER BY newPSM.Id
               ";
        void addNewPeptideSpectrumMatches (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewPeptideSpectrumMatchesSql, MaxPeptideSpectrumMatchId)); }


        static string addNewPeptideSpectrumMatchScoreNamesSql =
              @"INSERT INTO merged.PeptideSpectrumMatchScoreNames
                SELECT AfterMergeId, newName.Name
                FROM new.PeptideSpectrumMatchScoreNames newName
                JOIN PeptideSpectrumMatchScoreNameMergeMap nameMerge ON newName.Id = BeforeMergeId
                WHERE AfterMergeId > {0}
               ";
        void addNewPeptideSpectrumMatchScoreNames (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewPeptideSpectrumMatchScoreNamesSql, MaxPeptideSpectrumMatchScoreNameId)); }


        static string addNewPeptideSpectrumMatchScoresSql =
              @"INSERT INTO merged.PeptideSpectrumMatchScores
                SELECT PsmId+{0}, Value, AfterMergeId
                FROM new.PeptideSpectrumMatchScores newScore
                JOIN PeptideSpectrumMatchScoreNameMergeMap nameMerge ON newScore.ScoreNameId = BeforeMergeId
               ";
        void addNewPeptideSpectrumMatchScores (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewPeptideSpectrumMatchScoresSql, MaxPeptideSpectrumMatchId)); }


        static string addNewPeptideModificationsSql =
              @"INSERT INTO merged.PeptideModification
                SELECT Id+{0}, PeptideSpectrumMatch+{1}, AfterMergeId, Offset, Site
                FROM new.PeptideModification newPM
                JOIN ModificationMergeMap modMerge ON Modification = BeforeMergeId
               ";
        void addNewPeptideModifications (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewPeptideModificationsSql, MaxPeptideModificationId, MaxPeptideSpectrumMatchId)); }


        static string addNewAnalysesSql =
              @"INSERT INTO merged.Analysis
                SELECT AfterMergeId, Name, SoftwareName, SoftwareVersion, Type, StartTime
                FROM new.Analysis newAnalysis
                JOIN AnalysisMergeMap aMerge ON Id = BeforeMergeId
                WHERE AfterMergeId > {0}
               ";
        void addNewAnalyses (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewAnalysesSql, MaxAnalysisId)); }


        static string addNewAnalysisParametersSql =
              @"INSERT INTO merged.AnalysisParameter
                SELECT AfterMergeId * 1000 + Id, AfterMergeId, Name, Value
                FROM new.AnalysisParameter newAP
                JOIN AnalysisMergeMap aMerge ON Analysis = BeforeMergeId
                WHERE AfterMergeId > {0}
               ";
        void addNewAnalysisParameters (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewAnalysisParametersSql, MaxAnalysisId)); }


        static string getNewMaxIdsSql =
              @"SELECT (SELECT IFNULL(MAX(Id),0) FROM new.Protein),
                       (SELECT IFNULL(MAX(Id),0) FROM new.PeptideInstance),
                       (SELECT IFNULL(MAX(Id),0) FROM new.Peptide),
                       (SELECT IFNULL(MAX(Id),0) FROM new.PeptideSpectrumMatch),
                       (SELECT IFNULL(MAX(Id),0) FROM new.PeptideSpectrumMatchScoreNames),
                       (SELECT IFNULL(MAX(Id),0) FROM new.PeptideModification),
                       (SELECT IFNULL(MAX(Id),0) FROM new.Modification),
                       (SELECT IFNULL(MAX(Id),0) FROM new.SpectrumSourceGroup),
                       (SELECT IFNULL(MAX(Id),0) FROM new.SpectrumSource),
                       (SELECT IFNULL(MAX(Id),0) FROM new.SpectrumSourceGroupLink),
                       (SELECT IFNULL(MAX(Id),0) FROM new.Spectrum),
                       (SELECT IFNULL(MAX(Id),0) FROM new.Analysis)
               ";
        void getNewMaxIds (IDbConnection conn)
        {
            var maxIds = conn.ExecuteQuery(getNewMaxIdsSql).First();
            MaxProteinId += (long) maxIds[0];
            MaxPeptideInstanceId += (long) maxIds[1];
            MaxPeptideId += (long) maxIds[2];
            MaxPeptideSpectrumMatchId += (long) maxIds[3];
            MaxPeptideSpectrumMatchScoreNameId += (long) maxIds[4];
            MaxPeptideModificationId += (long) maxIds[5];
            MaxModificationId += (long) maxIds[6];
            MaxSpectrumSourceGroupId += (long) maxIds[7];
            MaxSpectrumSourceId += (long) maxIds[8];
            MaxSpectrumSourceGroupLinkId += (long) maxIds[9];
            MaxSpectrumId += (long) maxIds[10];
            MaxAnalysisId += (long) maxIds[11];
        }

        static void addIntegerSet (IDbConnection conn)
        {
            long maxInteger = (long) conn.ExecuteQuery("SELECT IFNULL(MAX(Value),0) FROM IntegerSet").First()[0];
            long maxProteinLength = (long) conn.ExecuteQuery("SELECT MAX(LENGTH(Sequence)) FROM ProteinData").First()[0];

            var cmd = conn.CreateCommand(); cmd.CommandText = "INSERT INTO IntegerSet VALUES (?)";
            var iParam = cmd.CreateParameter(); cmd.Parameters.Add(iParam);

            for (long i = maxInteger + 1; i <= maxProteinLength; ++i)
            {
                iParam.Value = i;
                cmd.ExecuteNonQuery();
            }
        }

        bool OnMergingProgress (Exception ex, int mergedFiles)
        {
            if (MergingProgress != null)
            {
                var eventArgs = new MergingProgressEventArgs()
                {
                    MergedFiles = mergedFiles,
                    TotalFiles = mergeSourceFilepaths.Count(),
                    MergingException = ex
                };
                MergingProgress(this, eventArgs);
                return eventArgs.Cancel;
            }
            else if (ex != null)
                throw ex;

            return false;
        }

        string mergeTargetFilepath;
        IEnumerable<string> mergeSourceFilepaths;

        long MaxProteinId = 0;
        long MaxPeptideInstanceId = 0;
        long MaxPeptideId = 0;
        long MaxPeptideSpectrumMatchId = 0;
        long MaxPeptideSpectrumMatchScoreNameId = 0;
        long MaxPeptideModificationId = 0;
        long MaxModificationId = 0;
        long MaxSpectrumSourceGroupId = 0;
        long MaxSpectrumSourceId = 0;
        long MaxSpectrumSourceGroupLinkId = 0;
        long MaxSpectrumId = 0;
        long MaxAnalysisId = 0;

        static int tempCacheSize = 1000000;
        static int mergedCacheSize = 800000;
        static int newCacheSize = 50000;
    }
}