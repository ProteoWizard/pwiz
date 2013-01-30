/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Data
{
    public class WorkspaceUpgrader
    {
        public const int CurrentVersion = 15;
        public const int MinUpgradeableVersion = 12;
        private IDbCommand _currentCommand;
        private LongOperationBroker _longOperationBroker;

        public WorkspaceUpgrader(String path)
        {
            WorkspacePath = path;
            if (Path.GetExtension(path) == TpgLinkDef.Extension)
            {
                TpgLinkDef = TpgLinkDef.Load(path);
            }
        }
        public WorkspaceUpgrader(TpgLinkDef tpgLinkDef)
        {
            TpgLinkDef = tpgLinkDef;
        }

        public String WorkspacePath { get; private set; }

        public TpgLinkDef TpgLinkDef { get; private set; }

        public IDbConnection OpenConnection()
        {
            if (TpgLinkDef != null)
            {
                return TpgLinkDef.OpenConnection();
            }
            var connectionString = new SQLiteConnectionStringBuilder
                                       {
                                           DataSource = WorkspacePath
                                       }.ToString();
            var connection = new SQLiteConnection(connectionString);
            connection.Open();
            return connection;
        }

        public int ReadSchemaVersion(IDbConnection connection)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT MAX(SchemaVersion) FROM DbWorkspace";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private IDbCommand CreateCommand(IDbConnection connection, String commandText)
        {
            lock(this)
            {
                _longOperationBroker.CancellationToken.ThrowIfCancellationRequested();
                _currentCommand = connection.CreateCommand();
                _currentCommand.CommandTimeout = 180000;
                _currentCommand.CommandText = commandText;
                return _currentCommand;
            }
        }

        public bool IsSqlite
        {
            get
            {
                return TpgLinkDef == null;
            }
        }

        public void Run(LongOperationBroker broker)
        {
            _longOperationBroker = broker;
            _longOperationBroker.CancellationToken.Register(Cancel);
            broker.UpdateStatusMessage("Opening file");
            using (var connection = OpenConnection())
            {
                int dbVersion = ReadSchemaVersion(connection);
                if (dbVersion == CurrentVersion)
                {
                    return;
                }
                var transaction = connection.BeginTransaction();
                if (dbVersion < 2)
                {
                    broker.UpdateStatusMessage("Upgrading from version 1 to 2");
                    CreateCommand(connection, "ALTER TABLE DbPeptideAnalysis ADD COLUMN ExcludedMasses BLOB").
                        ExecuteNonQuery();
                    CreateCommand(connection, "UPDATE DbPeptideAnalysis SET ExcludedMasses = ExcludedMzs").ExecuteNonQuery();
                    CreateCommand(connection, "ALTER TABLE DbPeptideFileAnalysis ADD COLUMN ExcludedMasses BLOB").
                        ExecuteNonQuery();
                    CreateCommand(connection, "ALTER TABLE DbPeptideFileAnalysis ADD COLUMN OverrideExcludedMasses INTEGER")
                        .ExecuteNonQuery();
                    CreateCommand(connection, 
                        "UPDATE DbPeptideFileAnalysis SET ExcludedMasses = ExcludedMzs, OverrideExcludedMasses = OverrideExcludedMzs")
                        .ExecuteNonQuery();
                }
                if (dbVersion < 3)
                {
                    broker.UpdateStatusMessage("Upgrading from version 2 to 3");
                    CreateCommand(connection,
                                  "CREATE TABLE DbChangeLog (Id  integer, InstanceIdBytes BLOB, PeptideAnalysisId INTEGER, "
                                  +"PeptideId INTEGER, MsDataFileId INTEGER, WorkspaceId INTEGER, primary key (Id))")
                                  .ExecuteNonQuery();
                    CreateCommand(connection,
                                  "CREATE TABLE DbLock (Id  integer, Version INTEGER not null, InstanceIdBytes BLOB,"
                                  + "LockType INTEGER, WorkspaceId INTEGER, PeptideAnalysisId INTEGER, MsDataFileId INTEGER,"
                                  + " primary key (Id))")
                                  .ExecuteNonQuery();
                    CreateCommand(connection, "ALTER TABLE DbWorkspace ADD COLUMN DataFilePath TEXT").ExecuteNonQuery();
                    CreateCommand(connection, "ALTER TABLE DbChromatogram ADD COLUMN UncompressedSize INTEGER")
                        .ExecuteNonQuery();
                }
                if (dbVersion < 4)
                {
                    broker.UpdateStatusMessage("Upgrading from version 3 to 4");
                    CreateCommand(connection, "ALTER TABLE DbPeptideAnalysis ADD COLUMN MassAccuracy DOUBLE")
                        .ExecuteNonQuery();
                }
                if (dbVersion < 5)
                {
                    broker.UpdateStatusMessage("Upgrading from version 4 to 5");
                    CreateCommand(connection, "ALTER TABLE DbPeptideDistribution ADD COLUMN PrecursorEnrichment DOUBLE")
                        .ExecuteNonQuery();
                    CreateCommand(connection, "ALTER TABLE DbPeptideDistribution ADD COLUMN Turnover DOUBLE")
                        .ExecuteNonQuery();
                    CreateCommand(connection, "ALTER TABLE DbPeptideDistribution ADD COLUMN PrecursorEnrichmentFormula TEXT")
                        .ExecuteNonQuery();
                }
                if (dbVersion < 6)
                {
                    broker.UpdateStatusMessage("Upgrading from version 5 to 6");
                    if (IsSqlite)
                    {
                        CreateCommand(connection, "DROP TABLE DbPeak")
                            .ExecuteNonQuery();
                        CreateCommand(connection, "CREATE TABLE DbPeak (Id  integer, Version INTEGER not null, "
                                                  + "\nPeptideFileAnalysis INTEGER not null, Name TEXT not null, StartTime NUMERIC, EndTime NUMERIC, TotalArea NUMERIC,"
                                                  + "\nBackground NUMERIC, RatioToBase NUMERIC, RatioToBaseError NUMERIC,"
                                                  + "\nCorrelation NUMERIC, Intercept NUMERIC, TracerPercent NUMERIC, RelativeAmount NUMERIC,"
                                                  + "\nprimary key (Id),unique (PeptideFileAnalysis, Name))")
                            .ExecuteNonQuery();
                    }
                    else
                    {
                        CreateCommand(connection, "DELETE FROM DbPeak")
                            .ExecuteNonQuery();
                        try
                        {
                            CreateCommand(connection, "DROP INDEX PeptideFileAnalysis ON DbPeak")
                                .ExecuteNonQuery();
                        }
// ReSharper disable EmptyGeneralCatchClause
                        catch 
// ReSharper restore EmptyGeneralCatchClause
                        {
                            // ignore
                        }
                        CreateCommand(connection, "ALTER TABLE DbPeak ADD COLUMN Name VARCHAR(255)")
                            .ExecuteNonQuery();
                        CreateCommand(connection, "ALTER TABLE DbPeak ADD COLUMN StartTime DOUBLE")
                            .ExecuteNonQuery();
                        CreateCommand(connection, "ALTER TABLE DbPeak ADD COLUMN EndTime DOUBLE")
                            .ExecuteNonQuery();
                        CreateCommand(connection, "ALTER TABLE DbPeak ADD COLUMN RatioToBase DOUBLE")
                            .ExecuteNonQuery();
                        CreateCommand(connection, "ALTER TABLE DbPeak ADD COLUMN RatioToBaseError DOUBLE")
                            .ExecuteNonQuery();
                        CreateCommand(connection, "ALTER TABLE DbPeak ADD COLUMN Correlation DOUBLE")
                            .ExecuteNonQuery();
                        CreateCommand(connection, "ALTER TABLE DbPeak ADD COLUMN Intercept DOUBLE")
                            .ExecuteNonQuery();
                        CreateCommand(connection, "ALTER TABLE DbPeak ADD COLUMN TracerPercent DOUBLE")
                            .ExecuteNonQuery();
                        CreateCommand(connection, "ALTER TABLE DbPeak ADD COLUMN RelativeAmount DOUBLE")
                            .ExecuteNonQuery();
                        CreateCommand(connection,
                                      "CREATE UNIQUE INDEX PeptideFileAnalysis ON DbPeak (PeptideFileAnalysis, Name)")
                            .ExecuteNonQuery();
                    }
                    CreateCommand(connection, "ALTER TABLE DbPeptideFileAnalysis ADD COLUMN BasePeakName TEXT")
                        .ExecuteNonQuery();
                    CreateCommand(connection, "ALTER TABLE DbPeptideFileAnalysis ADD COLUMN TracerPercent DOUBLE")
                        .ExecuteNonQuery();
                    CreateCommand(connection, "ALTER TABLE DbPeptideFileAnalysis ADD COLUMN DeconvolutionScore DOUBLE")
                        .ExecuteNonQuery();
                    CreateCommand(connection, "ALTER TABLE DbPeptideFileAnalysis ADD COLUMN PrecursorEnrichment DOUBLE")
                        .ExecuteNonQuery();
                    CreateCommand(connection, "UPDATE DbPeptideFileAnalysis SET PeakCount = 0")
                        .ExecuteNonQuery();

                    CreateCommand(connection,
                                  "ALTER TABLE DbPeptideFileAnalysis ADD COLUMN PrecursorEnrichmentFormula TEXT")
                        .ExecuteNonQuery();
                    CreateCommand(connection, "ALTER TABLE DbPeptideFileAnalysis ADD COLUMN Turnover DOUBLE")
                        .ExecuteNonQuery();
                }
                if (dbVersion < 7)
                {
                    broker.UpdateStatusMessage("Upgrading from version 6 to 7");
                    CreateCommand(connection, "ALTER TABLE DbMsDataFile ADD Column Sample TEXT")
                        .ExecuteNonQuery();
                }
                if (dbVersion < 8)
                {
                    broker.UpdateStatusMessage("Upgrading from version 7 to 8");
                    CreateCommand(connection, "ALTER TABLE DbPeptideFileAnalysis ADD Column TurnoverScore DOUBLE")
                        .ExecuteNonQuery();
                }
                if (dbVersion < 9)
                {
                    broker.UpdateStatusMessage("Upgrading from version 8 to 9");
                    CreateCommand(connection, "ALTER TABLE DbMsDataFile ADD COLUMN TotalIonCurrentBytes MEDIUMBLOB")
                        .ExecuteNonQuery();
                }
                if (dbVersion < 10)
                {
                    broker.UpdateStatusMessage("Upgrading from version 9 to 10");
                    if (IsSqlite)
                    {
                        CreateCommand(connection,
                                      "CREATE TABLE DbChromatogramSet (Id  integer, Version INTEGER not null, TimesBytes BLOB, ScanIndexesBytes BLOB, PeptideFileAnalysis INTEGER not null, ChromatogramCount INTEGER, primary key (Id),unique (PeptideFileAnalysis))")
                                      .ExecuteNonQuery();
                        CreateCommand(connection, "DROP TABLE DbChromatogram").ExecuteNonQuery();
                        CreateCommand(connection,
                                      "CREATE TABLE DbChromatogram (Id  integer, Version INTEGER not null, ChromatogramSet INTEGER not null, Charge INTEGER not null, MassIndex INTEGER not null, MzMin NUMERIC, MzMax NUMERIC, PointsBytes BLOB, UncompressedSize INTEGER, primary key (Id),unique (ChromatogramSet, Charge, MassIndex))")
                            .ExecuteNonQuery();
                    }
                    else
                    {
                        CreateCommand(connection, "CREATE TABLE DbChromatogramSet ("
                                              + "\nId BIGINT NOT NULL AUTO_INCREMENT"
                                              + "\n,Version INT NOT NULL"
                                              + "\n,TimesBytes MEDIUMBLOB"
                                              + "\n,ScanIndexesBytes MEDIUMBLOB"
                                              + "\n,PeptideFileAnalysis BIGINT NOT NULL"
                                              + "\n,ChromatogramCount INT"
                                              + "\n,primary key (Id)"
                                              + "\n,unique KEY PeptideFileAnalysis (PeptideFileAnalysis)"
                                              +")")
                        .ExecuteNonQuery();
                        CreateCommand(connection, "DROP INDEX PeptideFileAnalysis ON DbChromatogram")
                            .ExecuteNonQuery();
                        CreateCommand(connection, "ALTER TABLE DbChromatogram ADD COLUMN ChromatogramSet BIGINT")
                            .ExecuteNonQuery();
                    }
                    CreateCommand(connection, "ALTER TABLE DbPeptideFileAnalysis ADD COLUMN ChromatogramSet BIGINT")
                        .ExecuteNonQuery();
//                    CreateCommand(connection,
//                                  "SELECT Id AS PeptideFileAnalysis, TimesBytes, ScanIndexesBytes, ChromatogramCount INTO DbChromatogramSet FROM DbPeptideFileAnalysis WHERE ChromatogramCount > 0")
//                        .ExecuteNonQuery();
                    if (!IsSqlite)
                    {
                        CreateCommand(connection,
                                      "INSERT INTO DbChromatogramSet (PeptideFileAnalysis, TimesBytes, ScanIndexesBytes, ChromatogramCount)"
                                      + "\nSELECT Id, TimesBytes, ScanIndexesBytes, ChromatogramCount"
                                      + "\nFROM DbPeptideFileAnalysis"
                                      + "\nWHERE ChromatogramCount > 0")
                            .ExecuteNonQuery();
                    }

                    CreateCommand(connection,
                                      "UPDATE DbPeptideFileAnalysis SET TimesBytes = NULL, ScanIndexesBytes = NULL, ChromatogramSet = (SELECT Id FROM DbChromatogramSet WHERE DbChromatogramSet.PeptideFileAnalysis = DbPeptideFileAnalysis.Id)")
                            .ExecuteNonQuery();
                    if (!IsSqlite) 
                    {
                        CreateCommand(connection,
                                      "UPDATE DbChromatogram C INNER JOIN DbChromatogramSet S ON C.PeptideFileAnalysis = S.PeptideFileAnalysis SET C.ChromatogramSet = S.Id")
                            .ExecuteNonQuery();
                        CreateCommand(connection,
                                      "CREATE UNIQUE INDEX ChromatogramSetMz ON DbChromatogram (ChromatogramSet, Charge, MassIndex)")
                            .ExecuteNonQuery();
                    }
                }
                if (dbVersion < 11)
                {
                    broker.UpdateStatusMessage("Upgrading from version 10 to 11");
                    CreateCommand(connection, "ALTER TABLE DbPeptideSearchResult ADD COLUMN PsmCount INT")
                        .ExecuteNonQuery();
                    CreateCommand(connection, "ALTER TABLE DbPeptideFileAnalysis ADD COLUMN PsmCount INT")
                        .ExecuteNonQuery();
                    CreateCommand(connection,
                            "ALTER TABLE DbPeptideFileAnalysis ADD COLUMN IntegrationNote VARCHAR(255)")
                        .ExecuteNonQuery();
                    CreateCommand(connection,
                                  "UPDATE DbPeptideSearchResult SET PsmCount = 1 WHERE FirstDetectedScan = LastDetectedScan")
                                  .ExecuteNonQuery();
                    CreateCommand(connection,
                                  "UPDATE DbPeptideSearchResult SET PsmCount = 2 WHERE FirstDetectedScan <> LastDetectedScan")
                                  .ExecuteNonQuery();
                    if (!IsSqlite)
                    {
                        CreateCommand(connection, "UPDATE DbPeptideFileAnalysis F"
                                                  + "\nINNER JOIN DbPeptideAnalysis A ON F.PeptideAnalysis = A.Id"
                                                  +
                                                  "\nLEFT JOIN DbPeptideSearchResult R ON R.Peptide = A.Peptide AND R.MsDataFile = F.MsDataFile"
                                                  + "\nSET F.PsmCount = Coalesce(R.PsmCount, 0)")
                            .ExecuteNonQuery();

                            
                    }
                }
                if (dbVersion < 12)
                {
                    broker.UpdateStatusMessage("Upgrading from version 11 to 12");
                    CreateCommand(connection, "CREATE TABLE DbPeptideSpectrumMatch ("
                                              + "\nId bigint NOT NULL AUTO_INCREMENT,"
                                              + "\nVersion int NOT NULL,"
                                              + "\nMsDataFile bigint NOT NULL,"
                                              + "\nPeptide bigint NOT NULL,"
                                              + "\nPrecursorMz double ,"
                                              + "\nPrecursorCharge int,"
                                              + "\nModifiedSequence varchar(255),"
                                              + "\nRetentionTime double,"
                                              + "\nSpectrumId varchar(255),"
                                              + "\nPRIMARY KEY (Id),"
                                              + "\nKEY MsDataFile (MsDataFile),"
                                              + "\nKEY Peptide (Peptide)"
                                              + "\n)").ExecuteNonQuery();
                    if (!IsSqlite)
                    {
                        foreach (string table in new[] { "DbChangeLog", "DbChromatogram", "DbChromatogramSet", "DbLock", "DbModification", "DbMsDataFile", "DbPeak", "DbPeptide", "DbPeptideAnalysis", "DbPeptideFileAnalysis", "DbPeptideSearchResult", "DbPeptideSpectrumMatch", "DbSetting", "DbTracerDef", "DbWorkspace" })
                        {
                            try
                            {
                                CreateCommand(connection, string.Format("ALTER TABLE {0} ENGINE=INNODB", table)).ExecuteNonQuery();
                            }
                            catch(Exception exception)
                            {
                                Trace.TraceWarning("Exception changing storage engine on table {0}:{1}", table, exception);
                            }
                        }
                    }

                    CreateCommand(connection, "INSERT INTO DbPeptideSpectrumMatch(MsDataFile, Peptide, SpectrumId, PrecursorCharge, ModifiedSequence)"
                                  +"\nSELECT S.MsDataFile,"
                                  + "\nS.Peptide,"
                                  + "\nS.FirstDetectedScan," 
                                  + "\nS.MinCharge," 
                                  + "\nP.Sequence"
                                  + "\nFROM DbPeptideSearchResult S INNER JOIN DbPeptide P ON S.Peptide = P.Id")
                        .ExecuteNonQuery();
                    CreateCommand(connection, "INSERT INTO DbPeptideSpectrumMatch(MsDataFile, Peptide, SpectrumId, PrecursorCharge, ModifiedSequence)"
                                  + "\nSELECT S.MsDataFile,"
                                  + "\nS.Peptide,"
                                  + "\nS.FirstDetectedScan,"
                                  + "\nS.MinCharge,"
                                  + "\nP.Sequence"
                                  + "\nFROM DbPeptideSearchResult S INNER JOIN DbPeptide P ON S.Peptide = P.Id"
                                  + "\nWHERE S.FirstDetectedScan <> S.LastDetectedScan")
                        .ExecuteNonQuery();
                }
                if (dbVersion < 13)
                {
                    broker.UpdateStatusMessage("Upgrading from version 12 to 13");
                    CreateCommand(connection, "ALTER TABLE DbPeptide MODIFY COLUMN Workspace BIGINT")
                        .ExecuteNonQuery();
                    CreateCommand(connection, "ALTER TABLE DbMsDataFile MODIFY COLUMN Workspace BIGINT")
                        .ExecuteNonQuery();
                    CreateCommand(connection, "ALTER TABLE DbPeptideAnalysis MODIFY COLUMN Workspace BIGINT")
                        .ExecuteNonQuery();
                }
                if (dbVersion < 14)
                {
                    broker.UpdateStatusMessage("Upgrading from version 13 to 14");
                    string strCreateDbPeak2 = @"CREATE TABLE DbPeak2
(Id bigint(20) NOT NULL AUTO_INCREMENT,
PeptideFileAnalysis bigint(20) NOT NULL,
PeakIndex int NOT NULL,
StartTime double DEFAULT NULL,
EndTime double DEFAULT NULL,
Area double DEFAULT NULL,
PRIMARY KEY (Id),
UNIQUE KEY PeptideFileAnalysis (PeptideFileAnalysis,PeakIndex))";
                    if (!IsSqlite)
                    {
                        strCreateDbPeak2 += " ENGINE=InnoDb";
                    }
                    CreateCommand(connection, strCreateDbPeak2)
                        .ExecuteNonQuery();
                    CreateCommand(connection,
                                  @"INSERT INTO DbPeak2 (Id, PeptideFileAnalysis, PeakIndex, StartTime, EndTime, Area)
SELECT Id, PeptideFileAnalysis,
(SELECT COUNT(Id) FROM DbPeak P2 WHERE P2.PeptideFileAnalysis = P1.PeptideFileAnalysis AND P2.Name < P1.Name),
StartTime,EndTime,TotalArea
FROM DbPeak P1")
                        .ExecuteNonQuery();
                    CreateCommand(connection, "DROP TABLE DbPeak")
                        .ExecuteNonQuery();
                    CreateCommand(connection, "ALTER TABLE DbPeak2 RENAME TO DbPeak")
                        .ExecuteNonQuery();
                }
                if (dbVersion < 15)
                {
                    broker.UpdateStatusMessage("Upgrading from version 14 to 15");
                    CreateCommand(connection, "ALTER TABLE DbMsDataFile ADD COLUMN PrecursorPool VARCHAR(255)")
                        .ExecuteNonQuery();
                }
                if (dbVersion < CurrentVersion)
                {
                    broker.UpdateStatusMessage("Upgrading");
                    CreateCommand(connection, "UPDATE DbWorkspace SET SchemaVersion = " + CurrentVersion)
                        .ExecuteNonQuery();
                }
                broker.UpdateStatusMessage("Committing transaction");
                broker.SetIsCancelleable(false);
                transaction.Commit();
            }
        }

        public void Cancel()
        {
            lock(this)
            {
                if (_currentCommand != null)
                {
                    _currentCommand.Cancel();
                }
            }
        }

    }
}
