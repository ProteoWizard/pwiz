/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.IO;
using System.Text;
using DuckDB.NET.Data;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Serialization.DuckDb
{
    /// <summary>
    /// Serializes a Skyline document to DuckDB format (.skydb).
    /// Schema based on Skyline_Current.xsd
    /// </summary>
    public class DuckDbSerializer
    {
        public const string EXT = ".skydb";

        public SrmDocument Document { get; }
        public IProgressMonitor ProgressMonitor { get; }

        // Dynamic table schemas
        private TableSchema _moleculeGroupSchema;
        private TableSchema _moleculeSchema;
        private TableSchema _transitionGroupSchema;
        private TableSchema _transitionSchema;

        public DuckDbSerializer(SrmDocument document, IProgressMonitor progressMonitor)
        {
            Document = document;
            ProgressMonitor = progressMonitor;
            InitializeSchemas();
        }

        private void InitializeSchemas()
        {
            _moleculeGroupSchema = TableSchema.Create<MoleculeGroupRecord>("molecule_group");
            _moleculeSchema = TableSchema.Create<MoleculeRecord>("molecule");
            _transitionGroupSchema = TableSchema.Create<TransitionGroupRecord>("transition_group");
            _transitionSchema = TableSchema.Create<TransitionRecord>("transition");
        }

        public void SerializeDocument(string filePath)
        {
            // Delete existing file if present
            if (File.Exists(filePath))
                File.Delete(filePath);

            // Discovery phase: scan document to determine which columns have data
            DiscoverUsedColumns();

            using (var connection = new DuckDBConnection($"Data Source={filePath}"))
            {
                connection.Open();

                CreateSchema(connection);
                WriteDocumentMetadata(connection);
                WriteDocumentContent(connection);
            }
        }

        /// <summary>
        /// Scans the document to discover which columns have non-null values.
        /// This allows us to create a schema with only the columns that are actually used.
        /// </summary>
        private void DiscoverUsedColumns()
        {
            long moleculeGroupId = 0;
            long moleculeId = 0;
            long transitionGroupId = 0;
            long transitionId = 0;

            foreach (var moleculeGroup in Document.MoleculeGroups)
            {
                moleculeGroupId++;
                _moleculeGroupSchema.DiscoverColumns(new MoleculeGroupRecord(moleculeGroup, moleculeGroupId));

                foreach (var molecule in moleculeGroup.Molecules)
                {
                    moleculeId++;
                    _moleculeSchema.DiscoverColumns(new MoleculeRecord(molecule, moleculeId, moleculeGroupId));

                    foreach (TransitionGroupDocNode transitionGroup in molecule.Children)
                    {
                        transitionGroupId++;
                        _transitionGroupSchema.DiscoverColumns(new TransitionGroupRecord(transitionGroup, transitionGroupId, moleculeId));

                        foreach (TransitionDocNode transition in transitionGroup.Children)
                        {
                            transitionId++;
                            _transitionSchema.DiscoverColumns(new TransitionRecord(transition, transitionId, transitionGroupId));
                        }
                    }
                }
            }
        }

        private void CreateSchema(DuckDBConnection connection)
        {
            using (var cmd = connection.CreateCommand())
            {
                // Document metadata table
                cmd.CommandText = @"
                    CREATE TABLE document (
                        id BIGINT PRIMARY KEY,
                        format_version VARCHAR NOT NULL,
                        software_version VARCHAR NOT NULL
                    )";
                cmd.ExecuteNonQuery();

                // Molecule groups (proteins/peptide lists)
                cmd.CommandText = _moleculeGroupSchema.BuildCreateTableSql();
                cmd.ExecuteNonQuery();

                // Molecules (peptides/small molecules)
                cmd.CommandText = _moleculeSchema.BuildCreateTableSql();
                cmd.ExecuteNonQuery();

                // Transition groups (precursors)
                cmd.CommandText = _transitionGroupSchema.BuildCreateTableSql();
                cmd.ExecuteNonQuery();

                // Transitions
                cmd.CommandText = _transitionSchema.BuildCreateTableSql();
                cmd.ExecuteNonQuery();
            }
        }

        private void WriteDocumentMetadata(DuckDBConnection connection)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO document (id, format_version, software_version)
                    VALUES (1, $format_version, $software_version)";

                AddParameter(cmd, "format_version", DocumentFormat.CURRENT.ToString());
                AddParameter(cmd, "software_version", Install.Version);

                cmd.ExecuteNonQuery();
            }
        }

        private static void AddParameter(IDbCommand cmd, string name, object value)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = name;
            param.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(param);
        }

        private void WriteDocumentContent(DuckDBConnection connection)
        {
            var status = new ProgressStatus(string.Empty);

            // Pass 1: Write all molecule groups
            UpdateProgress(status.ChangeMessage("Writing molecule groups..."), 0);
            WriteMoleculeGroups(connection);

            // Pass 2: Write all molecules
            UpdateProgress(status.ChangeMessage("Writing molecules..."), 25);
            WriteMolecules(connection);

            // Pass 3: Write all transition groups
            UpdateProgress(status.ChangeMessage("Writing transition groups..."), 50);
            WriteTransitionGroups(connection);

            // Pass 4: Write all transitions
            UpdateProgress(status.ChangeMessage("Writing transitions..."), 75);
            WriteTransitions(connection);

            UpdateProgress(status.ChangeMessage("Complete"), 100);
        }

        private void UpdateProgress(IProgressStatus status, int percentComplete)
        {
            if (ProgressMonitor != null)
            {
                if (ProgressMonitor.IsCanceled)
                    throw new OperationCanceledException();
                ProgressMonitor.UpdateProgress(status.ChangePercentComplete(percentComplete));
            }
        }

        private void WriteMoleculeGroups(DuckDBConnection connection)
        {
            using (var appender = connection.CreateAppender("molecule_group"))
            {
                long id = 0;
                foreach (var moleculeGroup in Document.MoleculeGroups)
                {
                    id++;
                    var row = appender.CreateRow();
                    _moleculeGroupSchema.AppendRow(row, new MoleculeGroupRecord(moleculeGroup, id));
                }
            }
        }

        private void WriteMolecules(DuckDBConnection connection)
        {
            using (var appender = connection.CreateAppender("molecule"))
            {
                long moleculeGroupId = 0;
                long moleculeId = 0;

                foreach (var moleculeGroup in Document.MoleculeGroups)
                {
                    moleculeGroupId++;
                    foreach (var molecule in moleculeGroup.Molecules)
                    {
                        moleculeId++;
                        var row = appender.CreateRow();
                        _moleculeSchema.AppendRow(row, new MoleculeRecord(molecule, moleculeId, moleculeGroupId));
                    }
                }
            }
        }

        private void WriteTransitionGroups(DuckDBConnection connection)
        {
            using (var appender = connection.CreateAppender("transition_group"))
            {
                long moleculeId = 0;
                long transitionGroupId = 0;

                foreach (var moleculeGroup in Document.MoleculeGroups)
                {
                    foreach (var molecule in moleculeGroup.Molecules)
                    {
                        moleculeId++;
                        foreach (TransitionGroupDocNode transitionGroup in molecule.Children)
                        {
                            transitionGroupId++;
                            var row = appender.CreateRow();
                            _transitionGroupSchema.AppendRow(row, new TransitionGroupRecord(transitionGroup, transitionGroupId, moleculeId));
                        }
                    }
                }
            }
        }

        private void WriteTransitions(DuckDBConnection connection)
        {
            using (var appender = connection.CreateAppender("transition"))
            {
                long transitionGroupId = 0;
                long transitionId = 0;

                foreach (var moleculeGroup in Document.MoleculeGroups)
                {
                    foreach (var molecule in moleculeGroup.Molecules)
                    {
                        foreach (TransitionGroupDocNode transitionGroup in molecule.Children)
                        {
                            transitionGroupId++;
                            foreach (TransitionDocNode transition in transitionGroup.Children)
                            {
                                transitionId++;
                                var row = appender.CreateRow();
                                _transitionSchema.AppendRow(row, new TransitionRecord(transition, transitionId, transitionGroupId));
                            }
                        }
                    }
                }
            }
        }
    }
}
