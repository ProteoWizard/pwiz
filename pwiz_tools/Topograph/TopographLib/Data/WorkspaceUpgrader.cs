using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using MySql.Data.MySqlClient;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Data
{
    public class WorkspaceUpgrader : ILongOperationJob
    {
        public const int CurrentVersion = 11;
        public const int MinUpgradeableVersion = 1;
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
            var connectionString = new SQLiteConnectionStringBuilder()
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
                if (_longOperationBroker.WasCancelled)
                {
                    throw new JobCancelledException();
                }
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
                        catch
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
                    broker.UpdateStatusMessage("Upgrading from version 10 to 11");
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
                    broker.UpdateStatusMessage("Upgrading from version 9 to 10");
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
                if (dbVersion < CurrentVersion)
                {
                    broker.UpdateStatusMessage("Upgrading");
                    CreateCommand(connection, "UPDATE DbWorkspace SET SchemaVersion = " + CurrentVersion).ExecuteNonQuery();
                }
                broker.UpdateStatusMessage("Committing transaction");
                broker.SetIsCancelleable(false);
                transaction.Commit();
            }
        }

        public bool Cancel()
        {
            lock(this)
            {
                if (_currentCommand != null)
                {
                    _currentCommand.Cancel();
                }
                return true;
            }
        }
    }
}
