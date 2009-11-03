using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Data
{
    public class WorkspaceUpgrader : ILongOperationJob
    {
        public const int CurrentVersion = 2;
        public const int MinUpgradeableVersion = 1;
        private IDbCommand _currentCommand;
        private LongOperationBroker _longOperationBroker;

        public WorkspaceUpgrader(String path)
        {
            WorkspacePath = path;
        }

        public String WorkspacePath { get; private set; }

        public IDbConnection OpenConnection()
        {
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
