/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * Additional author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.6) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.AuditLog.Databinding;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Util.Extensions;
using SkylineTool;
using ColumnInfo = pwiz.Skyline.Model.Databinding.ColumnResolver.ColumnInfo;
using TopicInfo = pwiz.Skyline.Model.Databinding.ColumnResolver.TopicInfo;
using UnresolvedColumn = pwiz.Skyline.Model.Databinding.ColumnResolver.UnresolvedColumn;
using UnresolvedColumnsException = pwiz.Skyline.Model.Databinding.ColumnResolver.UnresolvedColumnsException;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Resolves <see cref="ReportDefinition"/> objects (from the MCP/JSON tool API) into
    /// <see cref="ViewSpec"/> objects that the Skyline databinding layer can execute.
    /// Supports multiple reporting scopes (Document Grid, Audit Log, Group Comparisons,
    /// Candidate Peaks), each with its own column namespace and root type.
    /// </summary>
    public class ReportDefinitionReader
    {
        public const string SCOPE_DOCUMENT_GRID = @"document_grid";
        public const string SCOPE_AUDIT_LOG = @"audit_log";
        public const string SCOPE_GROUP_COMPARISONS = @"group_comparisons";
        public const string SCOPE_CANDIDATE_PEAKS = @"candidate_peaks";

        public ReportDefinitionReader(SkylineDataSchema dataSchema)
        {
            DataSchema = dataSchema;
        }

        public SkylineDataSchema DataSchema { get; }

        /// <summary>
        /// Resolves a <see cref="ReportDefinition"/> to a <see cref="ViewSpec"/>.
        /// When scope is null, tries all resolvers and uses the first that succeeds.
        /// When scope is specified, uses only that scope's resolver.
        /// </summary>
        public ViewSpec CreateViewSpec(ReportDefinition definition, string scope = null)
        {
            if (definition.Select == null || definition.Select.Length == 0)
            {
                throw new ArgumentException(new LlmInstruction(
                    @"The 'select' array is required and must not be empty."));
            }
            if (definition.Select.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException(new LlmInstruction(
                    @"Column names in 'select' must not be empty."));
            }

            if (!string.IsNullOrEmpty(scope))
                return GetResolverForScope(scope).ResolveReportDefinition(definition);

            // Auto-detect: try all resolvers, use first that succeeds.
            // Only catch UnresolvedColumnsException (column not found in this scope).
            // Other exceptions (bad filter op, missing value, etc.) should propagate
            // since the columns DID resolve -- the error is in the definition itself.
            foreach (var resolver in GetResolvers())
            {
                try
                {
                    return resolver.ResolveReportDefinition(definition);
                }
                catch (UnresolvedColumnsException)
                {
                }
            }

            // All failed - provide helpful error with suggestions
            throw FormatAutoDetectError(definition);
        }

        /// <summary>
        /// Returns documentation topics for a given scope. Document Grid scope
        /// produces entity-level topics (Protein, Peptide, etc.). Other scopes
        /// produce a single flat topic with all available columns.
        /// </summary>
        public IList<TopicInfo> GetTopics(string scope = null)
        {
            scope = scope ?? SCOPE_DOCUMENT_GRID;
            return GetResolverForScope(scope).GetTopics();
        }

        /// <summary>
        /// Returns column resolvers for each reporting scope, in auto-detect priority order.
        /// </summary>
        public IEnumerable<IScopeResolver> GetResolvers()
        {
            yield return new DocumentGridResolver(DataSchema);
            yield return NewScopeResolver(SCOPE_GROUP_COMPARISONS, typeof(FoldChangeRow));
            yield return NewScopeResolver(SCOPE_AUDIT_LOG, typeof(AuditLogRow),
                PropertyPath.Root.Property(nameof(AuditLogRow.Details)).LookupAllItems());
            yield return NewScopeResolver(SCOPE_CANDIDATE_PEAKS, typeof(CandidatePeakGroup));
        }

        private IScopeResolver GetResolverForScope(string scope)
        {
            switch (scope.ToLowerInvariant())
            {
                case SCOPE_DOCUMENT_GRID:
                    return new DocumentGridResolver(DataSchema);
                case SCOPE_AUDIT_LOG:
                    return NewScopeResolver(SCOPE_AUDIT_LOG, typeof(AuditLogRow),
                        PropertyPath.Root.Property(nameof(AuditLogRow.Details)).LookupAllItems());
                case SCOPE_GROUP_COMPARISONS:
                    return NewScopeResolver(SCOPE_GROUP_COMPARISONS, typeof(FoldChangeRow));
                case SCOPE_CANDIDATE_PEAKS:
                    return NewScopeResolver(SCOPE_CANDIDATE_PEAKS, typeof(CandidatePeakGroup));
                default:
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Unknown scope '{0}'. Valid scopes: {1}.",
                        scope, string.Join(@", ", SCOPE_DOCUMENT_GRID, SCOPE_AUDIT_LOG,
                            SCOPE_GROUP_COMPARISONS, SCOPE_CANDIDATE_PEAKS)));
            }
        }

        private ScopeColumnResolver NewScopeResolver(string scopeName, Type rowType,
            PropertyPath sublistId = null)
        {
            return new ScopeColumnResolver(scopeName,
                ColumnDescriptor.RootColumn(DataSchema, rowType),
                sublistId ?? PropertyPath.Root);
        }

        private ArgumentException FormatAutoDetectError(ReportDefinition definition)
        {
            // Collect all column names across all resolvers to provide suggestions
            var allColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var resolver in GetResolvers())
            {
                foreach (string name in resolver.ColumnNames)
                    allColumns.Add(name);
            }

            var unresolvedColumns = new List<UnresolvedColumn>();
            foreach (string name in definition.Select)
            {
                if (!allColumns.Contains(name))
                    unresolvedColumns.Add(new UnresolvedColumn(name,
                        ColumnResolver.FindSuggestions(name, allColumns)));
            }

            if (unresolvedColumns.Count > 0)
                return new UnresolvedColumnsException(unresolvedColumns);

            // All columns resolve in some scope but not all in the same scope
            return new ArgumentException(new LlmInstruction(
                @"The selected columns span multiple scopes and cannot be resolved together. " +
                @"Specify a 'scope' parameter: " +
                string.Join(@", ", SCOPE_DOCUMENT_GRID, SCOPE_AUDIT_LOG,
                    SCOPE_GROUP_COMPARISONS, SCOPE_CANDIDATE_PEAKS)));
        }

        /// <summary>
        /// Interface for scope-specific column resolvers.
        /// </summary>
        public interface IScopeResolver
        {
            ViewSpec ResolveReportDefinition(ReportDefinition definition);
            IList<TopicInfo> GetTopics();
            ICollection<string> ColumnNames { get; }
        }

        /// <summary>
        /// Column resolver for non-Document-Grid scopes (Audit Log, Group Comparisons,
        /// Candidate Peaks). Uses single-root DFS column indexing via
        /// <see cref="ColumnGroupKey"/> for cycle prevention.
        /// </summary>
        public class ScopeColumnResolver : IScopeResolver
        {
            private Dictionary<string, ColumnDescriptor> _columnsByCaption;
            // ReSharper disable once NotAccessedField.Local
            private readonly string _scopeName; // For debugging

            public ScopeColumnResolver(string scopeName, ColumnDescriptor rootColumn,
                PropertyPath sublistId)
            {
                _scopeName = scopeName;
                RootColumn = rootColumn;
                SublistId = sublistId;
                BuildColumnIndex();
            }

            public ColumnDescriptor RootColumn { get; }
            public PropertyPath SublistId { get; }
            public ICollection<string> ColumnNames => _columnsByCaption.Keys;

            public ViewSpec ResolveReportDefinition(ReportDefinition definition)
            {
                var columnSpecs = new List<ColumnSpec>();
                var unresolvedColumns = new List<UnresolvedColumn>();

                foreach (string name in definition.Select)
                {
                    if (_columnsByCaption.TryGetValue(name, out var column))
                    {
                        columnSpecs.Add(new ColumnSpec().SetPropertyPath(column.PropertyPath));
                    }
                    else
                    {
                        unresolvedColumns.Add(new UnresolvedColumn(name,
                            ColumnResolver.FindSuggestions(name, _columnsByCaption.Keys)));
                    }
                }

                if (unresolvedColumns.Count > 0)
                    throw new UnresolvedColumnsException(unresolvedColumns);

                var filterSpecs = ParseFilterSpecs(definition.Filter);

                string reportName = definition.Name ?? JsonToolConstants.DEFAULT_REPORT_NAME;
                var viewSpec = new ViewSpec()
                    .SetName(reportName)
                    .SetRowType(RootColumn.PropertyType)
                    .SetSublistId(SublistId)
                    .SetColumns(columnSpecs)
                    .SetFilters(filterSpecs);

                // Apply UI mode
                string uiMode = definition.Uimode;
                if (!string.IsNullOrEmpty(uiMode))
                    viewSpec = viewSpec.SetUiMode(uiMode);

                return viewSpec;
            }

            public IList<TopicInfo> GetTopics()
            {
                string topicName = GetTopicDisplayName(RootColumn);
                var dataSchema = RootColumn.DataSchema;
                var columns = new List<ColumnInfo>();

                foreach (var kvp in _columnsByCaption.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                {
                    string description = dataSchema.GetColumnDescription(kvp.Value) ?? string.Empty;
                    string typeName = GetSimpleTypeName(kvp.Value.PropertyType);
                    columns.Add(new ColumnInfo(kvp.Key, kvp.Value.PropertyPath,
                        description, typeName, topicName));
                }

                var rowType = dataSchema.GetWrappedValueType(RootColumn.PropertyType);
                return new List<TopicInfo> { new TopicInfo(topicName, columns, rowType) };
            }

            private void BuildColumnIndex()
            {
                _columnsByCaption = new Dictionary<string, ColumnDescriptor>(
                    StringComparer.OrdinalIgnoreCase);
                if (RootColumn.DataSchema.IsRootTypeSelectable(RootColumn.PropertyType))
                {
                    _columnsByCaption[RootColumn.GetColumnCaption(ColumnCaptionType.invariant)]
                        = RootColumn;
                }
                PopulateColumnIndex(new HashSet<ColumnGroupKey>(), RootColumn);
            }

            /// <summary>
            /// Recursively indexes child columns. Uses <see cref="ColumnGroupKey"/> to
            /// prevent infinite recursion: a (type, displayNameTemplate) pair is only
            /// traversed once.
            /// </summary>
            private void PopulateColumnIndex(HashSet<ColumnGroupKey> visitedColumnGroups,
                ColumnDescriptor column)
            {
                var childDisplayNameAttribute = column.GetAttributes()
                    .OfType<ChildDisplayNameAttribute>().FirstOrDefault();
                var displayNameTemplate = childDisplayNameAttribute?.InvariantFormat;

                foreach (var immediateChild in column.GetChildColumns())
                {
                    var child = immediateChild.GetCollectionColumn() ?? immediateChild;
                    var caption = child.GetColumnCaption(ColumnCaptionType.invariant);
                    if (!_columnsByCaption.ContainsKey(caption))
                        _columnsByCaption[caption] = child;
                    var key = new ColumnGroupKey(child.PropertyType, displayNameTemplate);
                    if (visitedColumnGroups.Add(key))
                        PopulateColumnIndex(visitedColumnGroups, child);
                }
            }

            private List<FilterSpec> ParseFilterSpecs(ReportFilter[] reportFilters)
            {
                var filters = new List<FilterSpec>();
                if (reportFilters == null)
                    return filters;

                foreach (var item in reportFilters)
                {
                    string columnName = item.Column;
                    if (string.IsNullOrWhiteSpace(columnName))
                    {
                        throw new ArgumentException(new LlmInstruction(
                            @"Each filter must have a 'column' field."));
                    }

                    string opName = item.Op;
                    if (string.IsNullOrWhiteSpace(opName))
                    {
                        throw new ArgumentException(LlmInstruction.Format(
                            @"Filter on column {0} must have an 'op' field.",
                            columnName.SingleQuote()));
                    }

                    if (!_columnsByCaption.TryGetValue(columnName, out var columnDescriptor))
                    {
                        var suggestions = ColumnResolver.FindSuggestions(columnName, _columnsByCaption.Keys);
                        if (suggestions.Count > 0)
                        {
                            throw new ArgumentException(LlmInstruction.Format(
                                @"Unknown filter column {0}. Did you mean: {1}?",
                                columnName.SingleQuote(),
                                string.Join(@", ", suggestions.Select(s => s.SingleQuote()))));
                        }
                        throw new ArgumentException(LlmInstruction.Format(
                            @"Unknown filter column {0}.", columnName.SingleQuote()));
                    }

                    var operation = FilterOperations.GetOperation(opName);
                    if (operation == null)
                    {
                        var validOps = FilterOperations.ListOperations()
                            .Where(o => !string.IsNullOrEmpty(o.OpName))
                            .Select(o => o.OpName.SingleQuote());
                        throw new ArgumentException(LlmInstruction.Format(
                            @"Unknown filter operation {0}. Valid operations: {1}.",
                            opName.SingleQuote(), string.Join(@", ", validOps)));
                    }

                    string operand = item.Value;
                    bool isUnaryOp = operation == FilterOperations.OP_IS_BLANK ||
                                     operation == FilterOperations.OP_IS_NOT_BLANK;
                    if (!isUnaryOp && string.IsNullOrEmpty(operand))
                    {
                        throw new ArgumentException(LlmInstruction.Format(
                            @"Filter operation {0} on column {1} requires a 'value' field.",
                            opName.SingleQuote(), columnName.SingleQuote()));
                    }

                    var predicate = FilterPredicate.FromInvariantOperandText(
                        operation, operand ?? string.Empty);
                    filters.Add(new FilterSpec(columnDescriptor.PropertyPath, predicate));
                }
                return filters;
            }

            private static string GetTopicDisplayName(ColumnDescriptor column)
            {
                return column.GetColumnCaption(ColumnCaptionType.invariant);
            }

            private static string GetSimpleTypeName(Type type)
            {
                if (type == typeof(string))
                    return @"Text";
                if (type == typeof(bool) || type == typeof(bool?))
                    return @"True/False";
                if (type == typeof(int) || type == typeof(int?) ||
                    type == typeof(long) || type == typeof(long?) ||
                    type == typeof(double) || type == typeof(double?) ||
                    type == typeof(float) || type == typeof(float?))
                    return @"Number";
                if (typeof(IAnnotatedValue).IsAssignableFrom(type))
                    return @"Number";
                return type.Name;
            }

            /// <summary>
            /// Prevents recursing into the same data type looking for columns more than
            /// once. If the property has a <see cref="ChildDisplayNameAttribute"/> then
            /// the property names get qualified, yielding a new set of property names.
            /// </summary>
            protected sealed class ColumnGroupKey
            {
                public ColumnGroupKey(Type type, string displayNameTemplate)
                {
                    ComponentType = type;
                    DisplayNameTemplate = displayNameTemplate;
                }

                public Type ComponentType { get; }
                public string DisplayNameTemplate { get; }

                private bool Equals(ColumnGroupKey other)
                {
                    return ReferenceEquals(ComponentType, other.ComponentType) &&
                           DisplayNameTemplate == other.DisplayNameTemplate;
                }

                public override bool Equals(object obj)
                {
                    return ReferenceEquals(this, obj) ||
                           obj is ColumnGroupKey other && Equals(other);
                }

                public override int GetHashCode()
                {
                    unchecked
                    {
                        return (ComponentType.GetHashCode() * 397) ^
                               (DisplayNameTemplate?.GetHashCode() ?? 0);
                    }
                }
            }
        }

        /// <summary>
        /// Column resolver for the Document Grid scope. Delegates to the standalone
        /// <see cref="ColumnResolver"/> for per-entity-root column resolution and
        /// topic generation, then applies scope-specific logic (UI mode, pivots, filters).
        /// </summary>
        public class DocumentGridResolver : IScopeResolver
        {
            private readonly ColumnResolver _resolver;

            public DocumentGridResolver(SkylineDataSchema dataSchema)
            {
                _resolver = new ColumnResolver(dataSchema);
            }

            public ICollection<string> ColumnNames
            {
                get
                {
                    // Build broadest index (Transition) for column name enumeration
                    return _resolver.GetAvailableColumns(typeof(Transition))
                        .Select(c => c.InvariantName).ToArray();
                }
            }

            public ViewSpec ResolveReportDefinition(ReportDefinition definition)
            {
                var columnNames = definition.Select.ToList();

                var result = _resolver.Resolve(columnNames);

                var columnSpecs = result.PropertyPaths.Select(p => new ColumnSpec(p)).ToList();
                string reportName = definition.Name ?? JsonToolConstants.DEFAULT_REPORT_NAME;
                var viewSpec = new ViewSpec()
                    .SetName(reportName)
                    .SetRowType(result.RowSourceType)
                    .SetColumns(columnSpecs);
                if (!result.SublistId.IsRoot)
                    viewSpec = viewSpec.SetSublistId(result.SublistId);

                // Apply UI mode: use explicit value or default to current schema mode
                string uiMode = definition.Uimode;
                if (string.IsNullOrEmpty(uiMode))
                    uiMode = _resolver.DataSchema.DefaultUiMode;
                viewSpec = viewSpec.SetUiMode(uiMode);

                // Apply filter specs using the resolved column index
                if (definition.Filter != null && definition.Filter.Length > 0)
                {
                    var filterSpecs = ParseFilterSpecs(definition.Filter, result);
                    viewSpec = viewSpec.SetFilters(filterSpecs);
                }

                // Apply pivot settings
                if (definition.PivotReplicate == true)
                    viewSpec = viewSpec.SetSublistId(PropertyPath.Root);
                else if (definition.PivotReplicate == false)
                    viewSpec = viewSpec.SetSublistId(
                        SublistPaths.GetReplicateSublist(result.RowSourceType));

                if (definition.PivotIsotopeLabel == true)
                    viewSpec = PivotReplicateAndIsotopeLabelWidget.PivotIsotopeLabel(viewSpec, true);

                return viewSpec;
            }

            public IList<TopicInfo> GetTopics()
            {
                return _resolver.GetTopics();
            }

            private static List<FilterSpec> ParseFilterSpecs(ReportFilter[] reportFilters,
                ColumnResolver.ResolveResult result)
            {
                var filters = new List<FilterSpec>();
                foreach (var item in reportFilters)
                {
                    string columnName = item.Column;
                    if (string.IsNullOrWhiteSpace(columnName))
                    {
                        throw new ArgumentException(new LlmInstruction(
                            @"Each filter must have a 'column' field."));
                    }

                    string opName = item.Op;
                    if (string.IsNullOrWhiteSpace(opName))
                    {
                        throw new ArgumentException(LlmInstruction.Format(
                            @"Filter on column {0} must have an 'op' field.",
                            columnName.SingleQuote()));
                    }

                    if (!result.ColumnIndex.TryGetValue(columnName, out var columnInfo))
                    {
                        var suggestions = ColumnResolver.FindSuggestions(columnName, result.ColumnIndex.Keys);
                        if (suggestions.Count > 0)
                        {
                            throw new ArgumentException(LlmInstruction.Format(
                                @"Unknown filter column {0}. Did you mean: {1}?",
                                columnName.SingleQuote(),
                                string.Join(@", ", suggestions.Select(s => s.SingleQuote()))));
                        }
                        throw new ArgumentException(LlmInstruction.Format(
                            @"Unknown filter column {0}.", columnName.SingleQuote()));
                    }

                    var operation = FilterOperations.GetOperation(opName);
                    if (operation == null)
                    {
                        var validOps = FilterOperations.ListOperations()
                            .Where(o => !string.IsNullOrEmpty(o.OpName))
                            .Select(o => o.OpName.SingleQuote());
                        throw new ArgumentException(LlmInstruction.Format(
                            @"Unknown filter operation {0}. Valid operations: {1}.",
                            opName.SingleQuote(), string.Join(@", ", validOps)));
                    }

                    string operand = item.Value;
                    bool isUnaryOp = operation == FilterOperations.OP_IS_BLANK ||
                                     operation == FilterOperations.OP_IS_NOT_BLANK;
                    if (!isUnaryOp && string.IsNullOrEmpty(operand))
                    {
                        throw new ArgumentException(LlmInstruction.Format(
                            @"Filter operation {0} on column {1} requires a 'value' field.",
                            opName.SingleQuote(), columnName.SingleQuote()));
                    }

                    var predicate = FilterPredicate.FromInvariantOperandText(
                        operation, operand ?? string.Empty);
                    filters.Add(new FilterSpec(columnInfo.PropertyPath, predicate));
                }
                return filters;
            }
        }
    }
}
