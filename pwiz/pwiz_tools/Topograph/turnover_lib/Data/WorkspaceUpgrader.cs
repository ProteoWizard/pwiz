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
        public const int CurrentVersion = 5;
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
                _currentCommand.CommandText = commandText;
                return _currentCommand;
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
