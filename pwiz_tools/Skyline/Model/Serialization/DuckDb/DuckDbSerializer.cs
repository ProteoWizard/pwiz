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
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using DuckDB.NET.Data;
using DuckDB.NET.Native;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Serialization.DuckDb
{
    /// <summary>
    /// Extension methods for DuckDB Appender to handle nullable values.
    /// </summary>
    public static class AppenderRowExtensions
    {
        public static IDuckDBAppenderRow AppendNullableValue(this IDuckDBAppenderRow row, string value)
        {
            if (value == null)
                return row.AppendNullValue();
            return row.AppendValue(value);
        }

        public static IDuckDBAppenderRow AppendNullableValue(this IDuckDBAppenderRow row, double? value)
        {
            if (value == null)
                return row.AppendNullValue();
            return row.AppendValue(value.Value);
        }

        public static IDuckDBAppenderRow AppendNullableValue(this IDuckDBAppenderRow row, int? value)
        {
            if (value == null)
                return row.AppendNullValue();
            return row.AppendValue(value.Value);
        }

        public static IDuckDBAppenderRow AppendNullableValue(this IDuckDBAppenderRow row, long? value)
        {
            if (value == null)
                return row.AppendNullValue();
            return row.AppendValue(value.Value);
        }

        public static IDuckDBAppenderRow AppendNullableValue(this IDuckDBAppenderRow row, bool? value)
        {
            if (value == null)
                return row.AppendNullValue();
            return row.AppendValue(value.Value);
        }
    }

    /// <summary>
    /// Represents a column definition with its name, SQL type, and value extractor.
    /// </summary>
    /// <typeparam name="T">The type of object this column extracts data from.</typeparam>
    public class ColumnDef<T>
    {
        public string Name { get; }
        public string SqlType { get; }
        public Func<T, object> GetValue { get; }
        public bool IsRequired { get; }

        public ColumnDef(string name, string sqlType, Func<T, object> getValue, bool isRequired = false)
        {
            Name = name;
            SqlType = sqlType;
            GetValue = getValue;
            IsRequired = isRequired;
        }
    }

    /// <summary>
    /// Tracks which columns have non-null values and can build dynamic schema.
    /// </summary>
    /// <typeparam name="T">The type of object this table stores.</typeparam>
    public class TableSchema<T>
    {
        public string TableName { get; }
        private readonly List<ColumnDef<T>> _allColumns;
        private readonly HashSet<string> _usedColumns;

        public TableSchema(string tableName, List<ColumnDef<T>> columns)
        {
            TableName = tableName;
            _allColumns = columns;
            _usedColumns = new HashSet<string>();

            // Required columns are always used
            foreach (var col in columns.Where(c => c.IsRequired))
                _usedColumns.Add(col.Name);
        }

        /// <summary>
        /// Scans an item to discover which columns have non-null values.
        /// </summary>
        public void DiscoverColumns(T item)
        {
            foreach (var col in _allColumns)
            {
                if (_usedColumns.Contains(col.Name))
                    continue;

                var value = col.GetValue(item);
                if (value != null)
                    _usedColumns.Add(col.Name);
            }
        }

        /// <summary>
        /// Gets only the columns that have data.
        /// </summary>
        public List<ColumnDef<T>> GetUsedColumns()
        {
            return _allColumns.Where(c => _usedColumns.Contains(c.Name)).ToList();
        }

        /// <summary>
        /// Builds the CREATE TABLE SQL statement with only used columns.
        /// </summary>
        public string BuildCreateTableSql()
        {
            var usedColumns = GetUsedColumns();
            var sb = new StringBuilder();
            sb.AppendLine($@"CREATE TABLE {TableName} (");

            for (int i = 0; i < usedColumns.Count; i++)
            {
                var col = usedColumns[i];
                var notNull = col.IsRequired ? " NOT NULL" : "";
                var comma = i < usedColumns.Count - 1 ? "," : "";
                // Quote column name to handle reserved words
                sb.AppendLine($@"    ""{col.Name}"" {col.SqlType}{notNull}{comma}");
            }

            sb.AppendLine(")");
            return sb.ToString();
        }

        /// <summary>
        /// Appends a row using only the used columns.
        /// </summary>
        /// <param name="row">The appender row.</param>
        /// <param name="item">The item to write.</param>
        /// <param name="id">The ID for the row (used for the 'id' column).</param>
        public void AppendRow(IDuckDBAppenderRow row, T item, long id)
        {
            foreach (var col in GetUsedColumns())
            {
                if (col.Name == "id")
                {
                    row.AppendValue(id);
                    continue;
                }
                var value = col.GetValue(item);
                AppendValue(row, value, col.SqlType);
            }
            row.EndRow();
        }

        private static void AppendValue(IDuckDBAppenderRow row, object value, string sqlType)
        {
            if (value == null)
            {
                row.AppendNullValue();
                return;
            }

            switch (value)
            {
                case string s:
                    row.AppendValue(s);
                    break;
                case long l:
                    row.AppendValue(l);
                    break;
                case int i:
                    row.AppendValue(i);
                    break;
                case double d:
                    row.AppendValue(d);
                    break;
                case bool b:
                    row.AppendValue(b);
                    break;
                default:
                    row.AppendValue(value.ToString());
                    break;
            }
        }
    }

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
        private TableSchema<PeptideGroupDocNode> _moleculeGroupSchema;
        private TableSchema<MoleculeInfo> _moleculeSchema;
        private TableSchema<TransitionGroupInfo> _transitionGroupSchema;
        private TableSchema<TransitionInfo> _transitionSchema;

        public DuckDbSerializer(SrmDocument document, IProgressMonitor progressMonitor)
        {
            Document = document;
            ProgressMonitor = progressMonitor;
            InitializeSchemas();
        }

        /// <summary>
        /// Helper class to hold molecule data with its parent group ID.
        /// </summary>
        private class MoleculeInfo
        {
            public long MoleculeGroupId { get; set; }
            public long MoleculeId { get; set; }
            public PeptideDocNode Molecule { get; set; }
        }

        /// <summary>
        /// Helper class to hold transition group data with its parent molecule ID.
        /// </summary>
        private class TransitionGroupInfo
        {
            public long MoleculeId { get; set; }
            public long TransitionGroupId { get; set; }
            public TransitionGroupDocNode TransitionGroup { get; set; }
        }

        /// <summary>
        /// Helper class to hold transition data with its parent group ID.
        /// </summary>
        private class TransitionInfo
        {
            public long TransitionGroupId { get; set; }
            public long TransitionId { get; set; }
            public TransitionDocNode Transition { get; set; }
        }

        private void InitializeSchemas()
        {
            _moleculeGroupSchema = new TableSchema<PeptideGroupDocNode>("molecule_group", new List<ColumnDef<PeptideGroupDocNode>>
            {
                new ColumnDef<PeptideGroupDocNode>("id", "BIGINT PRIMARY KEY", _ => null, true), // Handled separately
                new ColumnDef<PeptideGroupDocNode>("molecule_group_type", "VARCHAR", g => g.IsProtein ? "protein" : "peptide_list", true),
                new ColumnDef<PeptideGroupDocNode>("name", "VARCHAR", g => g.Name, true),
                new ColumnDef<PeptideGroupDocNode>("description", "VARCHAR", g => g.Description),
                new ColumnDef<PeptideGroupDocNode>("label_name", "VARCHAR", g => g.ProteinMetadata.Name),
                new ColumnDef<PeptideGroupDocNode>("label_description", "VARCHAR", g => g.ProteinMetadata.Description),
                new ColumnDef<PeptideGroupDocNode>("sequence", "VARCHAR", g => g.PeptideGroup.Sequence),
                new ColumnDef<PeptideGroupDocNode>("accession", "VARCHAR", g => g.ProteinMetadata.Accession),
                new ColumnDef<PeptideGroupDocNode>("preferred_name", "VARCHAR", g => g.ProteinMetadata.PreferredName),
                new ColumnDef<PeptideGroupDocNode>("gene", "VARCHAR", g => g.ProteinMetadata.Gene),
                new ColumnDef<PeptideGroupDocNode>("species", "VARCHAR", g => g.ProteinMetadata.Species),
                new ColumnDef<PeptideGroupDocNode>("websearch_status", "VARCHAR", g => g.ProteinMetadata.WebSearchInfo?.ToString()),
                new ColumnDef<PeptideGroupDocNode>("is_decoy", "BOOLEAN", g => g.IsDecoy),
                new ColumnDef<PeptideGroupDocNode>("decoy_match_proportion", "DOUBLE", g => g.ProportionDecoysMatch),
                new ColumnDef<PeptideGroupDocNode>("auto_manage_children", "BOOLEAN", g => g.AutoManageChildren),
                new ColumnDef<PeptideGroupDocNode>("note", "VARCHAR", g => g.Note),
            });

            _moleculeSchema = new TableSchema<MoleculeInfo>("molecule", new List<ColumnDef<MoleculeInfo>>
            {
                new ColumnDef<MoleculeInfo>("id", "BIGINT PRIMARY KEY", _ => null, true),
                new ColumnDef<MoleculeInfo>("molecule_group_id", "BIGINT", m => m.MoleculeGroupId, true),
                new ColumnDef<MoleculeInfo>("molecule_type", "VARCHAR", m => m.Molecule.Peptide.IsCustomMolecule ? "molecule" : "peptide", true),
                new ColumnDef<MoleculeInfo>("sequence", "VARCHAR", m => m.Molecule.Peptide.Sequence),
                new ColumnDef<MoleculeInfo>("modified_sequence", "VARCHAR", m => m.Molecule.ModifiedSequenceDisplay),
                new ColumnDef<MoleculeInfo>("lookup_sequence", "VARCHAR", m => null),
                new ColumnDef<MoleculeInfo>("start_index", "INTEGER", m => m.Molecule.Peptide.Begin),
                new ColumnDef<MoleculeInfo>("end_index", "INTEGER", m => m.Molecule.Peptide.End),
                new ColumnDef<MoleculeInfo>("prev_aa", "VARCHAR", m => m.Molecule.Peptide.Begin.HasValue ? m.Molecule.Peptide.PrevAA.ToString() : null),
                new ColumnDef<MoleculeInfo>("next_aa", "VARCHAR", m => m.Molecule.Peptide.End.HasValue ? m.Molecule.Peptide.NextAA.ToString() : null),
                new ColumnDef<MoleculeInfo>("is_decoy", "BOOLEAN", m => m.Molecule.IsDecoy),
                new ColumnDef<MoleculeInfo>("calc_neutral_pep_mass", "DOUBLE", m => m.Molecule.Peptide.IsCustomMolecule ? m.Molecule.Peptide.CustomMolecule.MonoisotopicMass : (double?)null),
                new ColumnDef<MoleculeInfo>("num_missed_cleavages", "INTEGER", m => m.Molecule.Peptide.MissedCleavages),
                new ColumnDef<MoleculeInfo>("rank", "INTEGER", m => m.Molecule.Rank),
                new ColumnDef<MoleculeInfo>("rt_calculator_score", "DOUBLE", m => null),
                new ColumnDef<MoleculeInfo>("predicted_retention_time", "DOUBLE", m => null),
                new ColumnDef<MoleculeInfo>("avg_measured_retention_time", "DOUBLE", m => m.Molecule.AverageMeasuredRetentionTime),
                new ColumnDef<MoleculeInfo>("standard_type", "VARCHAR", m => m.Molecule.GlobalStandardType?.Name),
                new ColumnDef<MoleculeInfo>("explicit_retention_time", "DOUBLE", m => m.Molecule.ExplicitRetentionTime?.RetentionTime),
                new ColumnDef<MoleculeInfo>("explicit_retention_time_window", "DOUBLE", m => m.Molecule.ExplicitRetentionTime?.RetentionTimeWindow),
                new ColumnDef<MoleculeInfo>("concentration_multiplier", "DOUBLE", m => null),
                new ColumnDef<MoleculeInfo>("internal_standard_concentration", "DOUBLE", m => null),
                new ColumnDef<MoleculeInfo>("normalization_method", "VARCHAR", m => null),
                new ColumnDef<MoleculeInfo>("attribute_group_id", "VARCHAR", m => null),
                new ColumnDef<MoleculeInfo>("surrogate_calibration_curve", "VARCHAR", m => null),
                new ColumnDef<MoleculeInfo>("neutral_formula", "VARCHAR", m => m.Molecule.Peptide.IsCustomMolecule ? m.Molecule.Peptide.CustomMolecule.Formula : null),
                new ColumnDef<MoleculeInfo>("neutral_mass_monoisotopic", "DOUBLE", m => m.Molecule.Peptide.IsCustomMolecule ? m.Molecule.Peptide.CustomMolecule.MonoisotopicMass : (double?)null),
                new ColumnDef<MoleculeInfo>("neutral_mass_average", "DOUBLE", m => m.Molecule.Peptide.IsCustomMolecule ? m.Molecule.Peptide.CustomMolecule.AverageMass : (double?)null),
                new ColumnDef<MoleculeInfo>("custom_ion_name", "VARCHAR", m => m.Molecule.Peptide.IsCustomMolecule ? m.Molecule.Peptide.CustomMolecule.Name : null),
                new ColumnDef<MoleculeInfo>("molecule_id_external", "VARCHAR", m => null),
                new ColumnDef<MoleculeInfo>("chromatogram_target", "VARCHAR", m => null),
                new ColumnDef<MoleculeInfo>("auto_manage_children", "BOOLEAN", m => m.Molecule.AutoManageChildren),
                new ColumnDef<MoleculeInfo>("note", "VARCHAR", m => m.Molecule.Note),
            });

            _transitionGroupSchema = new TableSchema<TransitionGroupInfo>("transition_group", new List<ColumnDef<TransitionGroupInfo>>
            {
                new ColumnDef<TransitionGroupInfo>("id", "BIGINT PRIMARY KEY", _ => null, true),
                new ColumnDef<TransitionGroupInfo>("molecule_id", "BIGINT", g => g.MoleculeId, true),
                new ColumnDef<TransitionGroupInfo>("transition_group_type", "VARCHAR", g => g.TransitionGroup.TransitionGroup.IsCustomIon ? "non_proteomic" : "proteomic", true),
                new ColumnDef<TransitionGroupInfo>("charge", "INTEGER", g => g.TransitionGroup.TransitionGroup.PrecursorAdduct.AdductCharge, true),
                new ColumnDef<TransitionGroupInfo>("precursor_mz", "DOUBLE", g => g.TransitionGroup.PrecursorMz.Value, true),
                new ColumnDef<TransitionGroupInfo>("isotope_label", "VARCHAR", g => g.TransitionGroup.TransitionGroup.LabelType?.Name),
                new ColumnDef<TransitionGroupInfo>("collision_energy", "DOUBLE", g => g.TransitionGroup.ExplicitValues.CollisionEnergy),
                new ColumnDef<TransitionGroupInfo>("declustering_potential", "DOUBLE", g => null),
                new ColumnDef<TransitionGroupInfo>("ccs", "DOUBLE", g => g.TransitionGroup.ExplicitValues.CollisionalCrossSectionSqA),
                new ColumnDef<TransitionGroupInfo>("explicit_collision_energy", "DOUBLE", g => g.TransitionGroup.ExplicitValues.CollisionEnergy),
                new ColumnDef<TransitionGroupInfo>("explicit_ion_mobility", "DOUBLE", g => g.TransitionGroup.ExplicitValues.IonMobility),
                new ColumnDef<TransitionGroupInfo>("explicit_ion_mobility_units", "VARCHAR", g => g.TransitionGroup.ExplicitValues.IonMobilityUnits == eIonMobilityUnits.none ? null : g.TransitionGroup.ExplicitValues.IonMobilityUnits.ToString()),
                new ColumnDef<TransitionGroupInfo>("explicit_ccs_sqa", "DOUBLE", g => null),
                new ColumnDef<TransitionGroupInfo>("explicit_compensation_voltage", "DOUBLE", g => g.TransitionGroup.ExplicitValues.CompensationVoltage),
                new ColumnDef<TransitionGroupInfo>("precursor_concentration", "DOUBLE", g => null),
                new ColumnDef<TransitionGroupInfo>("calc_neutral_mass", "DOUBLE", g => g.TransitionGroup.TransitionGroup.IsCustomIon ? (double?)null : g.TransitionGroup.GetPrecursorIonMass()),
                new ColumnDef<TransitionGroupInfo>("decoy_mass_shift", "DOUBLE", g => g.TransitionGroup.TransitionGroup.DecoyMassShift),
                new ColumnDef<TransitionGroupInfo>("modified_sequence", "VARCHAR", g => g.TransitionGroup.TransitionGroup.IsCustomIon ? null : g.TransitionGroup.TransitionGroup.Peptide?.Sequence),
                new ColumnDef<TransitionGroupInfo>("ion_formula", "VARCHAR", g => null),
                new ColumnDef<TransitionGroupInfo>("custom_ion_name", "VARCHAR", g => null),
                new ColumnDef<TransitionGroupInfo>("neutral_mass_monoisotopic", "DOUBLE", g => null),
                new ColumnDef<TransitionGroupInfo>("neutral_mass_average", "DOUBLE", g => null),
                new ColumnDef<TransitionGroupInfo>("precursor_id_external", "VARCHAR", g => null),
                new ColumnDef<TransitionGroupInfo>("auto_manage_children", "BOOLEAN", g => g.TransitionGroup.AutoManageChildren),
                new ColumnDef<TransitionGroupInfo>("note", "VARCHAR", g => g.TransitionGroup.Note),
            });

            _transitionSchema = new TableSchema<TransitionInfo>("transition", new List<ColumnDef<TransitionInfo>>
            {
                new ColumnDef<TransitionInfo>("id", "BIGINT PRIMARY KEY", _ => null, true),
                new ColumnDef<TransitionInfo>("transition_group_id", "BIGINT", t => t.TransitionGroupId, true),
                new ColumnDef<TransitionInfo>("transition_type", "VARCHAR", t => t.Transition.Transition.IsCustom() ? "non_proteomic" : "proteomic", true),
                new ColumnDef<TransitionInfo>("fragment_type", "VARCHAR", t => t.Transition.Transition.IonType.ToString()),
                new ColumnDef<TransitionInfo>("fragment_ordinal", "INTEGER", t => t.Transition.Transition.Ordinal > 0 ? (int?)t.Transition.Transition.Ordinal : null),
                new ColumnDef<TransitionInfo>("mass_index", "INTEGER", t => t.Transition.Transition.MassIndex > 0 ? (int?)t.Transition.Transition.MassIndex : null),
                new ColumnDef<TransitionInfo>("product_charge", "INTEGER", t => t.Transition.Transition.Adduct.AdductCharge),
                new ColumnDef<TransitionInfo>("isotope_dist_rank", "INTEGER", t => t.Transition.IsotopeDistInfo?.Rank),
                new ColumnDef<TransitionInfo>("isotope_dist_proportion", "DOUBLE", t => t.Transition.IsotopeDistInfo?.Proportion),
                new ColumnDef<TransitionInfo>("quantitative", "BOOLEAN", t => t.Transition.ExplicitQuantitative),
                new ColumnDef<TransitionInfo>("explicit_collision_energy", "DOUBLE", t => t.Transition.ExplicitValues.CollisionEnergy),
                new ColumnDef<TransitionInfo>("explicit_declustering_potential", "DOUBLE", t => t.Transition.ExplicitValues.DeclusteringPotential),
                new ColumnDef<TransitionInfo>("explicit_ion_mobility_high_energy_offset", "DOUBLE", t => t.Transition.ExplicitValues.IonMobilityHighEnergyOffset),
                new ColumnDef<TransitionInfo>("explicit_s_lens", "DOUBLE", t => t.Transition.ExplicitValues.SLens),
                new ColumnDef<TransitionInfo>("explicit_cone_voltage", "DOUBLE", t => t.Transition.ExplicitValues.ConeVoltage),
                new ColumnDef<TransitionInfo>("precursor_mz", "DOUBLE", t => null),
                new ColumnDef<TransitionInfo>("product_mz", "DOUBLE", t => t.Transition.Mz.Value, true),
                new ColumnDef<TransitionInfo>("collision_energy", "DOUBLE", t => null),
                new ColumnDef<TransitionInfo>("declustering_potential", "DOUBLE", t => null),
                new ColumnDef<TransitionInfo>("calc_neutral_mass", "DOUBLE", t => t.Transition.Transition.IsCustom() ? (double?)null : t.Transition.GetMoleculeMass()),
                new ColumnDef<TransitionInfo>("loss_neutral_mass", "DOUBLE", t => null),
                new ColumnDef<TransitionInfo>("cleavage_aa", "VARCHAR", t => t.Transition.Transition.IsCustom() ? null : t.Transition.Transition.AA.ToString()),
                new ColumnDef<TransitionInfo>("decoy_mass_shift", "DOUBLE", t => t.Transition.Transition.DecoyMassShift),
                new ColumnDef<TransitionInfo>("measured_ion_name", "VARCHAR", t => t.Transition.Transition.CustomIon?.Name),
                new ColumnDef<TransitionInfo>("orphaned_crosslink_ion", "BOOLEAN", t => null),
                new ColumnDef<TransitionInfo>("ion_formula", "VARCHAR", t => null),
                new ColumnDef<TransitionInfo>("custom_ion_name", "VARCHAR", t => null),
                new ColumnDef<TransitionInfo>("neutral_mass_monoisotopic", "DOUBLE", t => null),
                new ColumnDef<TransitionInfo>("neutral_mass_average", "DOUBLE", t => null),
                new ColumnDef<TransitionInfo>("transition_id_external", "VARCHAR", t => null),
                new ColumnDef<TransitionInfo>("note", "VARCHAR", t => t.Transition.Note),
            });
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
                _moleculeGroupSchema.DiscoverColumns(moleculeGroup);

                foreach (var molecule in moleculeGroup.Molecules)
                {
                    moleculeId++;
                    var moleculeInfo = new MoleculeInfo
                    {
                        MoleculeGroupId = moleculeGroupId,
                        MoleculeId = moleculeId,
                        Molecule = molecule
                    };
                    _moleculeSchema.DiscoverColumns(moleculeInfo);

                    foreach (TransitionGroupDocNode transitionGroup in molecule.Children)
                    {
                        transitionGroupId++;
                        var groupInfo = new TransitionGroupInfo
                        {
                            MoleculeId = moleculeId,
                            TransitionGroupId = transitionGroupId,
                            TransitionGroup = transitionGroup
                        };
                        _transitionGroupSchema.DiscoverColumns(groupInfo);

                        foreach (TransitionDocNode transition in transitionGroup.Children)
                        {
                            transitionId++;
                            var transitionInfo = new TransitionInfo
                            {
                                TransitionGroupId = transitionGroupId,
                                TransitionId = transitionId,
                                Transition = transition
                            };
                            _transitionSchema.DiscoverColumns(transitionInfo);
                        }
                    }
                }
            }
        }

        private void CreateSchema(DuckDBConnection connection)
        {
            using (var cmd = connection.CreateCommand())
            {
                // ================================================================
                // Document root table (srm_settings_type)
                // ================================================================
                cmd.CommandText = @"
                    CREATE TABLE document (
                        id BIGINT PRIMARY KEY,
                        format_version VARCHAR NOT NULL,
                        software_version VARCHAR NOT NULL
                    )";
                cmd.ExecuteNonQuery();

                // ================================================================
                // Settings Summary (settings_summary_type)
                // ================================================================
                cmd.CommandText = @"
                    CREATE TABLE settings_summary (
                        id BIGINT PRIMARY KEY,
                        name VARCHAR NOT NULL
                    )";
                cmd.ExecuteNonQuery();

                // ================================================================
                // Peptide Settings (peptide_settings_type)
                // ================================================================
                cmd.CommandText = @"
                    CREATE TABLE enzyme (
                        id BIGINT PRIMARY KEY,
                        name VARCHAR NOT NULL,
                        cut VARCHAR,
                        no_cut VARCHAR,
                        cut_c VARCHAR,
                        no_cut_c VARCHAR,
                        cut_n VARCHAR,
                        no_cut_n VARCHAR,
                        sense VARCHAR,
                        ""semi"" BOOLEAN
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE digest_settings (
                        id BIGINT PRIMARY KEY,
                        max_missed_cleavages INTEGER NOT NULL,
                        exclude_ragged_ends BOOLEAN
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE background_proteome (
                        id BIGINT PRIMARY KEY,
                        name VARCHAR NOT NULL,
                        database_path VARCHAR NOT NULL
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE peptide_prediction (
                        id BIGINT PRIMARY KEY,
                        use_measured_rts BOOLEAN NOT NULL,
                        measured_rt_window DOUBLE
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE peptide_filter (
                        id BIGINT PRIMARY KEY,
                        start INTEGER NOT NULL,
                        min_length INTEGER NOT NULL,
                        max_length INTEGER NOT NULL,
                        auto_select BOOLEAN,
                        unique_by VARCHAR
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE peptide_exclusion (
                        id BIGINT PRIMARY KEY,
                        name VARCHAR NOT NULL,
                        regex VARCHAR NOT NULL,
                        include BOOLEAN,
                        match_mod_sequence BOOLEAN
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE peptide_libraries (
                        id BIGINT PRIMARY KEY,
                        pick VARCHAR NOT NULL,
                        rank_type VARCHAR,
                        peptide_count INTEGER,
                        document_library BOOLEAN
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE library_spec (
                        id BIGINT PRIMARY KEY,
                        library_type VARCHAR NOT NULL,
                        name VARCHAR NOT NULL,
                        file_path VARCHAR,
                        file_name_hint VARCHAR,
                        use_explicit_peak_bounds BOOLEAN,
                        revision VARCHAR,
                        lsid VARCHAR,
                        panorama_server VARCHAR
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE peptide_modifications (
                        id BIGINT PRIMARY KEY,
                        max_variable_mods INTEGER NOT NULL,
                        max_neutral_losses INTEGER NOT NULL,
                        internal_standard VARCHAR
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE static_modification (
                        id BIGINT PRIMARY KEY,
                        name VARCHAR NOT NULL,
                        aminoacid VARCHAR,
                        terminus VARCHAR,
                        variable BOOLEAN,
                        formula VARCHAR,
                        massdiff_monoisotopic DOUBLE,
                        massdiff_average DOUBLE,
                        explicit_decl BOOLEAN,
                        unimod_id INTEGER,
                        short_name VARCHAR
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE isotope_modification (
                        id BIGINT PRIMARY KEY,
                        isotope_label VARCHAR,
                        name VARCHAR NOT NULL,
                        aminoacid VARCHAR,
                        terminus VARCHAR,
                        formula VARCHAR,
                        label_13C BOOLEAN,
                        label_15N BOOLEAN,
                        label_18O BOOLEAN,
                        label_2H BOOLEAN,
                        label_37Cl BOOLEAN,
                        label_81Br BOOLEAN,
                        relative_rt VARCHAR,
                        massdiff_monoisotopic DOUBLE,
                        massdiff_average DOUBLE,
                        explicit_decl BOOLEAN,
                        unimod_id INTEGER,
                        short_name VARCHAR
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE potential_loss (
                        id BIGINT PRIMARY KEY,
                        modification_id BIGINT NOT NULL,
                        formula VARCHAR,
                        massdiff_monoisotopic DOUBLE,
                        massdiff_average DOUBLE,
                        inclusion VARCHAR,
                        charge INTEGER,
                        FOREIGN KEY (modification_id) REFERENCES static_modification(id)
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE quantification (
                        id BIGINT PRIMARY KEY,
                        weighting VARCHAR,
                        fit VARCHAR,
                        normalization VARCHAR,
                        units VARCHAR,
                        ms_level INTEGER,
                        lod_calculation VARCHAR,
                        max_loq_bias DOUBLE,
                        max_loq_cv DOUBLE,
                        qualitative_ion_ratio_threshold DOUBLE,
                        simple_ratios BOOLEAN
                    )";
                cmd.ExecuteNonQuery();

                // ================================================================
                // Transition Settings (transition_settings_type)
                // ================================================================
                cmd.CommandText = @"
                    CREATE TABLE transition_prediction (
                        id BIGINT PRIMARY KEY,
                        precursor_mass_type VARCHAR,
                        fragment_mass_type VARCHAR,
                        optimize_by VARCHAR
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE collision_energy_regression (
                        id BIGINT PRIMARY KEY,
                        name VARCHAR NOT NULL,
                        step_size DOUBLE NOT NULL,
                        step_count INTEGER NOT NULL
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE collision_energy_regression_ce (
                        id BIGINT PRIMARY KEY,
                        regression_id BIGINT NOT NULL,
                        charge INTEGER NOT NULL,
                        slope DOUBLE NOT NULL,
                        intercept DOUBLE NOT NULL,
                        FOREIGN KEY (regression_id) REFERENCES collision_energy_regression(id)
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE transition_filter (
                        id BIGINT PRIMARY KEY,
                        precursor_charges VARCHAR,
                        product_charges VARCHAR,
                        precursor_adducts VARCHAR,
                        product_adducts VARCHAR,
                        fragment_types VARCHAR,
                        small_molecule_fragment_types VARCHAR,
                        fragment_range_first VARCHAR,
                        fragment_range_last VARCHAR,
                        precursor_mz_window DOUBLE,
                        exclusion_use_dia_window BOOLEAN,
                        auto_select BOOLEAN
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE measured_ion (
                        id BIGINT PRIMARY KEY,
                        name VARCHAR NOT NULL,
                        cut VARCHAR,
                        no_cut VARCHAR,
                        sense VARCHAR,
                        min_length INTEGER,
                        ion_formula VARCHAR,
                        mass_monoisotopic DOUBLE,
                        mass_average DOUBLE,
                        charge INTEGER,
                        optional BOOLEAN
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE transition_libraries (
                        id BIGINT PRIMARY KEY,
                        ion_match_tolerance DOUBLE NOT NULL,
                        ion_match_tolerance_unit VARCHAR,
                        min_ion_count INTEGER,
                        ion_count INTEGER NOT NULL,
                        pick_from VARCHAR NOT NULL
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE transition_integration (
                        id BIGINT PRIMARY KEY,
                        integrate_all BOOLEAN
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE transition_instrument (
                        id BIGINT PRIMARY KEY,
                        dynamic_min BOOLEAN,
                        min_mz INTEGER NOT NULL,
                        max_mz INTEGER NOT NULL,
                        mz_match_tolerance DOUBLE NOT NULL,
                        min_time INTEGER,
                        max_time INTEGER,
                        max_transitions INTEGER,
                        max_inclusions INTEGER,
                        triggered_acquisition BOOLEAN
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE transition_full_scan (
                        id BIGINT PRIMARY KEY,
                        precursor_filter DOUBLE,
                        precursor_left_filter DOUBLE,
                        precursor_right_filter DOUBLE,
                        acquisition_method VARCHAR,
                        product_mass_analyzer VARCHAR,
                        product_res DOUBLE,
                        product_res_mz DOUBLE,
                        precursor_isotopes VARCHAR,
                        precursor_isotope_filter DOUBLE,
                        precursor_mass_analyzer VARCHAR,
                        precursor_res DOUBLE,
                        precursor_res_mz DOUBLE,
                        ignore_sim_scans BOOLEAN,
                        selective_extraction BOOLEAN,
                        scheduled_filter BOOLEAN,
                        retention_time_filter_type VARCHAR,
                        retention_time_filter_length DOUBLE
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE isolation_scheme (
                        id BIGINT PRIMARY KEY,
                        name VARCHAR NOT NULL,
                        precursor_filter DOUBLE,
                        precursor_left_filter DOUBLE,
                        precursor_right_filter DOUBLE,
                        precursor_filter_margin DOUBLE,
                        special_handling VARCHAR,
                        windows_per_scan INTEGER
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE isolation_window (
                        id BIGINT PRIMARY KEY,
                        scheme_id BIGINT NOT NULL,
                        start_value DOUBLE NOT NULL,
                        end_value DOUBLE NOT NULL,
                        target DOUBLE,
                        margin_left DOUBLE,
                        margin_right DOUBLE,
                        margin DOUBLE,
                        ce_range DOUBLE,
                        FOREIGN KEY (scheme_id) REFERENCES isolation_scheme(id)
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE ion_mobility_filtering (
                        id BIGINT PRIMARY KEY,
                        window_width_calc_type VARCHAR,
                        resolving_power DOUBLE,
                        width_at_ion_mobility_zero DOUBLE,
                        width_at_ion_mobility_max DOUBLE,
                        fixed_width DOUBLE,
                        use_spectral_library_ion_mobility_values BOOLEAN
                    )";
                cmd.ExecuteNonQuery();

                // ================================================================
                // Measured Results (measured_results_type)
                // ================================================================
                cmd.CommandText = @"
                    CREATE TABLE measured_results (
                        id BIGINT PRIMARY KEY,
                        time_normal_area BOOLEAN
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE replicate (
                        id BIGINT PRIMARY KEY,
                        name VARCHAR NOT NULL,
                        sample_type VARCHAR,
                        analyte_concentration DOUBLE,
                        has_midas_spectra BOOLEAN,
                        sample_dilution_factor DOUBLE,
                        batch_name VARCHAR
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE sample_file (
                        id BIGINT PRIMARY KEY,
                        replicate_id BIGINT NOT NULL,
                        file_id VARCHAR,
                        file_path VARCHAR NOT NULL,
                        sample_name VARCHAR NOT NULL,
                        acquired_time TIMESTAMP,
                        modified_time TIMESTAMP,
                        import_time TIMESTAMP,
                        explicit_global_standard_area DOUBLE,
                        tic_area DOUBLE,
                        sample_id VARCHAR,
                        instrument_serial_number VARCHAR,
                        ion_mobility_type VARCHAR,
                        FOREIGN KEY (replicate_id) REFERENCES replicate(id)
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE instrument_info (
                        id BIGINT PRIMARY KEY,
                        sample_file_id BIGINT NOT NULL,
                        model VARCHAR,
                        ionsource VARCHAR,
                        analyzer VARCHAR,
                        detector VARCHAR,
                        FOREIGN KEY (sample_file_id) REFERENCES sample_file(id)
                    )";
                cmd.ExecuteNonQuery();

                // ================================================================
                // Data Settings (data_settings_type)
                // ================================================================
                cmd.CommandText = @"
                    CREATE TABLE data_settings (
                        id BIGINT PRIMARY KEY,
                        document_guid VARCHAR,
                        audit_logging BOOLEAN,
                        panorama_publish_uri VARCHAR
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE annotation_def (
                        id BIGINT PRIMARY KEY,
                        name VARCHAR NOT NULL,
                        targets VARCHAR,
                        type VARCHAR,
                        lookup VARCHAR
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE annotation_def_value (
                        id BIGINT PRIMARY KEY,
                        annotation_def_id BIGINT NOT NULL,
                        value VARCHAR,
                        FOREIGN KEY (annotation_def_id) REFERENCES annotation_def(id)
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE group_comparison (
                        id BIGINT PRIMARY KEY,
                        name VARCHAR,
                        control_annotation VARCHAR,
                        control_value VARCHAR,
                        case_value VARCHAR,
                        identity_annotation VARCHAR,
                        avg_tech_replicates BOOLEAN,
                        sum_transitions BOOLEAN,
                        normalization_method VARCHAR,
                        include_interaction_transitions BOOLEAN,
                        summarization_method VARCHAR,
                        confidence_level DOUBLE,
                        per_protein BOOLEAN,
                        use_zero_for_missing_peaks BOOLEAN,
                        q_value_cutoff DOUBLE,
                        ms_level VARCHAR
                    )";
                cmd.ExecuteNonQuery();

                // ================================================================
                // Molecule Groups (protein_type, peptide_list_type, protein_group_type)
                // Dynamic schema based on document content
                // ================================================================
                cmd.CommandText = _moleculeGroupSchema.BuildCreateTableSql();
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE alternative_protein (
                        id BIGINT PRIMARY KEY,
                        molecule_group_id BIGINT NOT NULL,
                        name VARCHAR,
                        description VARCHAR,
                        accession VARCHAR,
                        preferred_name VARCHAR,
                        gene VARCHAR,
                        species VARCHAR,
                        websearch_status VARCHAR,
                        FOREIGN KEY (molecule_group_id) REFERENCES molecule_group(id)
                    )";
                cmd.ExecuteNonQuery();

                // ================================================================
                // Molecules (peptide_type, non_proteomic_molecule_type)
                // Dynamic schema based on document content
                // ================================================================
                cmd.CommandText = _moleculeSchema.BuildCreateTableSql();
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE molecule_modification (
                        id BIGINT PRIMARY KEY,
                        molecule_id BIGINT NOT NULL,
                        modification_type VARCHAR NOT NULL,
                        index_aa INTEGER NOT NULL,
                        modification_name VARCHAR NOT NULL,
                        mass_diff DOUBLE,
                        isotope_label VARCHAR,
                        FOREIGN KEY (molecule_id) REFERENCES molecule(id)
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE crosslink (
                        id BIGINT PRIMARY KEY,
                        molecule_id BIGINT NOT NULL,
                        modification_name VARCHAR,
                        FOREIGN KEY (molecule_id) REFERENCES molecule(id)
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE crosslink_site (
                        id BIGINT PRIMARY KEY,
                        crosslink_id BIGINT NOT NULL,
                        peptide_index INTEGER,
                        index_aa INTEGER,
                        FOREIGN KEY (crosslink_id) REFERENCES crosslink(id)
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE linked_peptide (
                        id BIGINT PRIMARY KEY,
                        molecule_id BIGINT NOT NULL,
                        sequence VARCHAR,
                        FOREIGN KEY (molecule_id) REFERENCES molecule(id)
                    )";
                cmd.ExecuteNonQuery();

                // ================================================================
                // Transition Groups / Precursors (transition_group_type, non_proteomic_transition_group_type)
                // Dynamic schema based on document content
                // ================================================================
                cmd.CommandText = _transitionGroupSchema.BuildCreateTableSql();
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE spectrum_header_info (
                        id BIGINT PRIMARY KEY,
                        transition_group_id BIGINT NOT NULL,
                        info_type VARCHAR NOT NULL,
                        library_name VARCHAR,
                        count_measured INTEGER,
                        score DOUBLE,
                        score_type VARCHAR,
                        protein VARCHAR,
                        peak_area DOUBLE,
                        expect DOUBLE,
                        processed_intensity DOUBLE,
                        total_intensity DOUBLE,
                        tfratio DOUBLE,
                        FOREIGN KEY (transition_group_id) REFERENCES transition_group(id)
                    )";
                cmd.ExecuteNonQuery();

                // ================================================================
                // Transitions (proteomic_transition_type, non_proteomic_transition_type)
                // Dynamic schema based on document content
                // ================================================================
                cmd.CommandText = _transitionSchema.BuildCreateTableSql();
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE transition_neutral_loss (
                        id BIGINT PRIMARY KEY,
                        transition_id BIGINT NOT NULL,
                        modification_name VARCHAR,
                        loss_index INTEGER,
                        formula VARCHAR,
                        massdiff_monoisotopic DOUBLE,
                        massdiff_average DOUBLE,
                        FOREIGN KEY (transition_id) REFERENCES transition(id)
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE transition_lib_info (
                        id BIGINT PRIMARY KEY,
                        transition_id BIGINT NOT NULL,
                        rank INTEGER NOT NULL,
                        intensity DOUBLE NOT NULL,
                        FOREIGN KEY (transition_id) REFERENCES transition(id)
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE linked_fragment_ion (
                        id BIGINT PRIMARY KEY,
                        transition_id BIGINT NOT NULL,
                        fragment_type VARCHAR,
                        fragment_ordinal INTEGER,
                        FOREIGN KEY (transition_id) REFERENCES transition(id)
                    )";
                cmd.ExecuteNonQuery();

                // ================================================================
                // Results (peptide_result_type, precursor_peak_type, transition_peak_type)
                // ================================================================
                cmd.CommandText = @"
                    CREATE TABLE peptide_result (
                        id BIGINT PRIMARY KEY,
                        molecule_id BIGINT NOT NULL,
                        replicate VARCHAR NOT NULL,
                        file VARCHAR,
                        peak_count_ratio DOUBLE NOT NULL,
                        retention_time DOUBLE,
                        predicted_retention_time DOUBLE,
                        exclude_from_calibration BOOLEAN,
                        analyte_concentration DOUBLE,
                        FOREIGN KEY (molecule_id) REFERENCES molecule(id)
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE precursor_peak (
                        id BIGINT PRIMARY KEY,
                        transition_group_id BIGINT NOT NULL,
                        replicate VARCHAR NOT NULL,
                        file VARCHAR,
                        peak_count_ratio DOUBLE NOT NULL,
                        retention_time DOUBLE,
                        predicted_retention_time DOUBLE,
                        exclude_from_calibration BOOLEAN,
                        analyte_concentration DOUBLE,
                        step INTEGER,
                        start_time DOUBLE,
                        end_time DOUBLE,
                        ccs DOUBLE,
                        ion_mobility_ms1 DOUBLE,
                        ion_mobility_fragment DOUBLE,
                        ion_mobility_window DOUBLE,
                        ion_mobility_type VARCHAR,
                        fwhm DOUBLE,
                        area DOUBLE,
                        background DOUBLE,
                        height DOUBLE,
                        mass_error_ppm DOUBLE,
                        truncated INTEGER,
                        identified VARCHAR,
                        library_dotp REAL,
                        isotope_dotp REAL,
                        qvalue REAL,
                        zscore REAL,
                        user_set VARCHAR,
                        original_score REAL,
                        note VARCHAR,
                        FOREIGN KEY (transition_group_id) REFERENCES transition_group(id)
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE precursor_peak_scored (
                        id BIGINT PRIMARY KEY,
                        precursor_peak_id BIGINT NOT NULL,
                        peak_type VARCHAR NOT NULL,
                        score DOUBLE NOT NULL,
                        retention_time DOUBLE,
                        start_time DOUBLE,
                        end_time DOUBLE,
                        FOREIGN KEY (precursor_peak_id) REFERENCES precursor_peak(id)
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE transition_peak (
                        id BIGINT PRIMARY KEY,
                        transition_id BIGINT NOT NULL,
                        replicate VARCHAR NOT NULL,
                        file VARCHAR,
                        step INTEGER,
                        retention_time DOUBLE,
                        start_time DOUBLE,
                        end_time DOUBLE,
                        ccs DOUBLE,
                        ion_mobility DOUBLE,
                        ion_mobility_window DOUBLE,
                        ion_mobility_type VARCHAR,
                        fwhm DOUBLE,
                        fwhm_degenerate BOOLEAN,
                        area DOUBLE,
                        background DOUBLE,
                        height DOUBLE,
                        mass_error_ppm DOUBLE,
                        truncated BOOLEAN,
                        identified VARCHAR,
                        rank INTEGER,
                        rank_by_level INTEGER,
                        user_set VARCHAR,
                        points_across INTEGER,
                        forced_integration BOOLEAN,
                        skewness DOUBLE,
                        kurtosis DOUBLE,
                        std_dev DOUBLE,
                        shape_correlation DOUBLE,
                        note VARCHAR,
                        FOREIGN KEY (transition_id) REFERENCES transition(id)
                    )";
                cmd.ExecuteNonQuery();

                // ================================================================
                // Annotations on document nodes
                // ================================================================
                cmd.CommandText = @"
                    CREATE TABLE node_annotation (
                        id BIGINT PRIMARY KEY,
                        node_type VARCHAR NOT NULL,
                        node_id BIGINT NOT NULL,
                        annotation_name VARCHAR NOT NULL,
                        annotation_value VARCHAR
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE replicate_annotation (
                        id BIGINT PRIMARY KEY,
                        replicate_id BIGINT NOT NULL,
                        annotation_name VARCHAR NOT NULL,
                        annotation_value VARCHAR,
                        FOREIGN KEY (replicate_id) REFERENCES replicate(id)
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE result_annotation (
                        id BIGINT PRIMARY KEY,
                        result_type VARCHAR NOT NULL,
                        result_id BIGINT NOT NULL,
                        annotation_name VARCHAR NOT NULL,
                        annotation_value VARCHAR
                    )";
                cmd.ExecuteNonQuery();

                // ================================================================
                // Spectrum filters
                // ================================================================
                cmd.CommandText = @"
                    CREATE TABLE spectrum_filter (
                        id BIGINT PRIMARY KEY,
                        parent_type VARCHAR NOT NULL,
                        parent_id BIGINT NOT NULL
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE spectrum_filter_condition (
                        id BIGINT PRIMARY KEY,
                        spectrum_filter_id BIGINT NOT NULL,
                        column_name VARCHAR,
                        opname VARCHAR,
                        operand VARCHAR,
                        FOREIGN KEY (spectrum_filter_id) REFERENCES spectrum_filter(id)
                    )";
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

            // Write data_settings
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO data_settings (id, document_guid, audit_logging, panorama_publish_uri)
                    VALUES (1, $document_guid, $audit_logging, $panorama_publish_uri)";

                AddParameter(cmd, "document_guid", Document.Settings.DataSettings.DocumentGuid);
                AddParameter(cmd, "audit_logging", Document.Settings.DataSettings.AuditLogging);
                AddParameter(cmd, "panorama_publish_uri", Document.Settings.DataSettings.PanoramaPublishUri?.ToString());

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
                    _moleculeGroupSchema.AppendRow(row, moleculeGroup, id);
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
                        var moleculeInfo = new MoleculeInfo
                        {
                            MoleculeGroupId = moleculeGroupId,
                            MoleculeId = moleculeId,
                            Molecule = molecule
                        };

                        var row = appender.CreateRow();
                        _moleculeSchema.AppendRow(row, moleculeInfo, moleculeId);
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
                            var groupInfo = new TransitionGroupInfo
                            {
                                MoleculeId = moleculeId,
                                TransitionGroupId = transitionGroupId,
                                TransitionGroup = transitionGroup
                            };

                            var row = appender.CreateRow();
                            _transitionGroupSchema.AppendRow(row, groupInfo, transitionGroupId);
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
                                var transitionInfo = new TransitionInfo
                                {
                                    TransitionGroupId = transitionGroupId,
                                    TransitionId = transitionId,
                                    Transition = transition
                                };

                                var row = appender.CreateRow();
                                _transitionSchema.AppendRow(row, transitionInfo, transitionId);
                            }
                        }
                    }
                }
            }
        }
    }
}
