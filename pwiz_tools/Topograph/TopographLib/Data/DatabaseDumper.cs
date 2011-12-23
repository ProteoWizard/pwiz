using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using NHibernate.Cfg;
using NHibernate.Dialect;
using NHibernate.Mapping;
using NHibernate.Tool.hbm2ddl;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Data
{
    public class DatabaseDumper : ILongOperationJob
    {
        private LongOperationBroker _longOperationBroker;
        private Configuration _targetConfiguration;
        private IDbConnection _sourceConnection;
        private TextWriter _writer;
        public DatabaseDumper(Workspace workspace, DatabaseTypeEnum databaseTypeEnum, String path)
        {
            Workspace = workspace;
            DatabaseTypeEnum = databaseTypeEnum;
            Path = path;
            Dialect = (Dialect) SessionFactoryFactory
                .GetDialectClass(DatabaseTypeEnum).GetConstructor(new Type[0]).Invoke(new object[0]);
        }

        public Workspace Workspace { get; private set; }
        public DatabaseTypeEnum DatabaseTypeEnum { get; private set; }
        public String Path { get; private set; }
        public Dialect Dialect { get; private set; }
        private void WriteValue(TextWriter writer, object value)
        {
            const string hexDigits = "0123456789ABCDEF";
            if (value == null || value is DBNull)
            {
                writer.Write("NULL");
            } 
            else if (value is String)
            {
                var strValue = (String) value;
                writer.Write("'");
                writer.Write(strValue.Replace("'", "''"));
                writer.Write("'");
            }
            else if (value is byte[])
            {
                var bytes = (byte[]) value;
                writer.Write("X'");
                foreach (byte b in bytes)
                {
                    writer.Write(hexDigits[b >> 4]);
                    writer.Write(hexDigits[b & 0xf]);
                }
                writer.Write("'");
            }
            else
            {
                writer.Write(value);
            }
        }
        private void ExportTable<T>()
        {
            _longOperationBroker.UpdateStatusMessage("Exporting " + typeof(T).Name);
            var table = typeof (T);
            var classMapping = _targetConfiguration.GetClassMapping(table);
            var columnNames = new List<String>();
            foreach (Column column in classMapping.IdentifierProperty.ColumnIterator)
            {
                columnNames.Add(column.Name);
            }
            foreach (Property property in classMapping.PropertyIterator)
            {
                foreach (Column column in property.ColumnIterator)
                {
                    columnNames.Add(column.Name);
                }
            }
            var sqlSelect = "SELECT " + Lists.Join(columnNames, ",") + " FROM " + classMapping.Table.Name;
            var cmd = _sourceConnection.CreateCommand();
            cmd.CommandText = sqlSelect;
            var reader = cmd.ExecuteReader();
            bool lockTable = DatabaseTypeEnum == DatabaseTypeEnum.mysql;
            if (lockTable)
            {
                _writer.WriteLine("LOCK TABLES " + classMapping.Table.Name + " WRITE;");
            }
            else
            {
                _writer.WriteLine("BEGIN TRANSACTION;");
            }
            int recordCount = 0;
            while (reader.Read())
            {
                _longOperationBroker.UpdateStatusMessage("Exporting " + typeof(T).Name + " #" + (++recordCount));
                _writer.Write("INSERT INTO " + classMapping.Table.Name + " VALUES(");
                for (int i = 0; i < columnNames.Count; i++)
                {
                    var value = reader.GetValue(i);
                    if (i != 0)
                    {
                        _writer.Write(",");
                    }
                    WriteValue(_writer, value);
                }
                _writer.WriteLine(");");
            }
            if (lockTable)
            {
                _writer.WriteLine("UNLOCK TABLES;");
            }
            else
            {
                _writer.WriteLine("COMMIT TRANSACTION;");
            }
        }
        public void Run(LongOperationBroker longOperationBroker)
        {
            _longOperationBroker = longOperationBroker;
            _targetConfiguration = SessionFactoryFactory.GetConfiguration(DatabaseTypeEnum, 0);
            var schemaExport = new SchemaExport(_targetConfiguration);
            schemaExport.SetOutputFile(Path);
            schemaExport.SetDelimiter(";");
            schemaExport.Create(true, false);
            using (var stream = File.OpenWrite(Path))
            {
                _writer = new StreamWriter(stream);
                foreach (var line in _targetConfiguration.GenerateSchemaCreationScript(Dialect))
                {
                    _writer.Write(line);
                    _writer.WriteLine(";");
                }
                using (var session = Workspace.OpenSession())
                {
                    _sourceConnection = session.Connection;
                    ExportTable<DbWorkspace>();
                    ExportTable<DbMsDataFile>();
                    ExportTable<DbSetting>();
                    ExportTable<DbModification>();
                    ExportTable<DbTracerDef>();
                    ExportTable<DbPeptide>();
                    ExportTable<DbPeptideSearchResult>();
                    ExportTable<DbPeptideAnalysis>();
                    ExportTable<DbPeptideFileAnalysis>();
                    ExportTable<DbChromatogramSet>();
                    ExportTable<DbChromatogram>();
                    ExportTable<DbPeak>();
                }
                _writer.Flush();
            }
        }

        public bool Cancel()
        {
            return true;
        }
    }
}
