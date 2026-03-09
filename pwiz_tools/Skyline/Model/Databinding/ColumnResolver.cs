/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using pwiz.Common.DataBinding.Documentation;
using pwiz.Skyline.Model.AuditLog.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Databinding
{
    /// <summary>
    /// Maps invariant column display names to PropertyPaths by traversing
    /// the ColumnDescriptor tree for each candidate row source type.
    /// Selects the deepest row source that resolves all requested columns.
    /// </summary>
    public class ColumnResolver
    {
        private const int MAX_DEPTH = 12;

        private static readonly Type[] TARGET_ROW_SOURCES =
        {
            typeof(Protein),
            typeof(Entities.Peptide),
            typeof(Precursor),
            typeof(Entities.Transition),
            typeof(AuditLogRow),
        };

        /// <summary>
        /// Ordered list of entity types that get their own documentation topic.
        /// The order defines the final topic ordering in GetTopics().
        /// Types not in this list have their columns folded into the nearest parent topic.
        /// </summary>
        private static readonly Type[] TOPIC_ENTITY_TYPES =
        {
            typeof(Protein),
            typeof(ProteinResult),
            typeof(Entities.Peptide),
            typeof(PeptideResult),
            typeof(Precursor),
            typeof(PrecursorResult),
            typeof(PrecursorResultSummary),
            typeof(Entities.Transition),
            typeof(TransitionResult),
            typeof(TransitionResultSummary),
            typeof(Replicate),
            typeof(AuditLogRow),
        };

        private static readonly HashSet<Type> TOPIC_ENTITY_SET =
            new HashSet<Type>(TOPIC_ENTITY_TYPES);

        /// <summary>
        /// Entity types that are exposed as direct properties (not collections) on their
        /// parent entity. These get their own topic when encountered as non-collection children.
        /// Result types are NOT included here because they are reached via dictionary collections.
        /// </summary>
        private static readonly HashSet<Type> DIRECT_PROPERTY_TOPIC_TYPES = new HashSet<Type>
        {
            typeof(PrecursorResultSummary),
            typeof(TransitionResultSummary),
        };

        private readonly SkylineDataSchema _dataSchema;

        public ColumnResolver(SkylineDataSchema dataSchema)
        {
            _dataSchema = dataSchema;
        }

        public ResolveResult Resolve(IList<string> columnNames)
        {
            // Try each target row source and pick the one that minimizes the total
            // number of collection steps across all resolved paths. This matches
            // DocumentViewTransformer.ConvertFromDocumentView, which determines
            // row source by the deepest entity level any column touches.
            // From the correct row source, entity columns resolve via navigation
            // up (no collections) and only result columns traverse the Results
            // collection. From a too-shallow row source, columns traverse entity
            // hierarchy collections (Precursors!*, Transitions!*), inflating the
            // total collection step count.
            ResolveResult bestMatch = null;
            int bestCollectionSteps = int.MaxValue;
            foreach (var rowSourceType in TARGET_ROW_SOURCES)
            {
                var index = BuildColumnIndex(rowSourceType);
                var paths = TryResolveAll(columnNames, index);
                if (paths != null)
                {
                    var result = BuildResult(rowSourceType, paths, index);
                    int totalSteps = TotalCollectionSteps(paths);
                    if (totalSteps < bestCollectionSteps)
                    {
                        bestMatch = result;
                        bestCollectionSteps = totalSteps;
                    }
                }
            }

            // Try Replicate row source - preferred when all paths from the best
            // target row source go through collections (replicate-centric query)
            {
                var index = BuildColumnIndex(typeof(Replicate));
                var paths = TryResolveAll(columnNames, index);
                if (paths != null)
                {
                    if (bestMatch == null || AllPathsThroughCollection(bestMatch.PropertyPaths))
                        return BuildResult(typeof(Replicate), paths, index);
                }
            }

            if (bestMatch != null)
                return bestMatch;

            // None worked -- collect structured error info
            var broadIndex = BuildColumnIndex(typeof(Entities.Transition));
            var unresolvedColumns = new List<UnresolvedColumn>();
            foreach (string name in columnNames)
            {
                if (!broadIndex.ContainsKey(name))
                    unresolvedColumns.Add(new UnresolvedColumn(name, FindSuggestions(name, broadIndex.Keys)));
            }
            throw new UnresolvedColumnsException(unresolvedColumns);
        }

        /// <summary>
        /// Returns the available columns for a given row source type, with descriptions
        /// and type names for documentation purposes. Column names match what
        /// <see cref="Resolve"/> accepts.
        /// </summary>
        public IList<ColumnInfo> GetAvailableColumns(Type rowSourceType)
        {
            return BuildColumnIndex(rowSourceType).Values.ToList();
        }

        /// <summary>
        /// Returns documentation topics for the curated set of higher-level entities,
        /// in a fixed hierarchy order. Sub-group columns (QuantificationResult,
        /// CalibrationCurve, etc.) are folded into their parent topic.
        /// Topic display names respect the current UI mode (proteomic vs small molecule).
        /// </summary>
        public IList<TopicInfo> GetTopics()
        {
            var topics = new List<TopicInfo>();

            // Walk from Protein root - covers the full target hierarchy
            var proteinRoot = ColumnDescriptor.RootColumn(_dataSchema, typeof(Protein));
            AddEntityTopic(proteinRoot, topics);

            // Walk from Replicate root - separate hierarchy
            var replicateRoot = ColumnDescriptor.RootColumn(_dataSchema, typeof(Replicate));
            AddEntityTopic(replicateRoot, topics);

            // Walk from AuditLogRow root - audit log hierarchy
            var auditLogRoot = ColumnDescriptor.RootColumn(_dataSchema, typeof(AuditLogRow));
            AddEntityTopic(auditLogRoot, topics, @"AuditLog");

            // Sort all topics by the defined TOPIC_ENTITY_TYPES order
            var typeOrderList = TOPIC_ENTITY_TYPES.ToList();
            topics.Sort((a, b) => typeOrderList.IndexOf(a.EntityType)
                .CompareTo(typeOrderList.IndexOf(b.EntityType)));

            return topics;
        }

        /// <summary>
        /// Sum collection lookup steps across all paths. Used by Resolve to
        /// pick the row source that minimizes total collection traversal.
        /// </summary>
        private static int TotalCollectionSteps(List<PropertyPath> paths)
        {
            return paths.Sum(CountCollectionSteps);
        }

        /// <summary>
        /// Count collection lookup steps (!*) in a PropertyPath.
        /// Used by IndexColumn to prefer paths with fewer collection steps.
        /// </summary>
        private static int CountCollectionSteps(PropertyPath path)
        {
            int count = 0;
            var walk = path;
            while (!walk.IsRoot)
            {
                if (walk.IsUnboundLookup)
                    count++;
                walk = walk.Parent;
            }
            return count;
        }

        private Dictionary<string, ColumnInfo> BuildColumnIndex(Type rowSourceType)
        {
            var index = new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);
            var rootColumn = ColumnDescriptor.RootColumn(_dataSchema, rowSourceType);
            // Index the root entity itself (e.g., "Precursor" from Precursor row source)
            var rootGroup = GetGroupName(_dataSchema.GetWrappedValueType(rootColumn.PropertyType));
            IndexColumn(rootColumn, index, rootGroup);
            var visitedTypes = new HashSet<Type>();
            TraverseColumns(rootColumn, index, visitedTypes, 0);
            return index;
        }

        private void TraverseColumns(ColumnDescriptor column,
            Dictionary<string, ColumnInfo> index, HashSet<Type> visitedTypes, int depth)
        {
            if (depth > MAX_DEPTH)
                return;

            var wrappedType = _dataSchema.GetWrappedValueType(column.PropertyType);
            if (!visitedTypes.Add(wrappedType))
                return;

            // Each non-nested complex type defines a group (entity type section),
            // matching the sections shown in the ViewEditor and HTML documentation.
            string group = GetGroupName(wrappedType);

            try
            {
                foreach (var child in DocumentationGenerator.GetChildColumns(column))
                {
                    if (_dataSchema.IsHidden(child))
                        continue;

                    if (DocumentationGenerator.IsNestedColumn(child))
                    {
                        // Nested columns (e.g., NormalizedArea, ModifiedSequence) have a
                        // ChildDisplayName attribute. Index the parent if it's checkable
                        // in the ViewEditor, then recurse. Nested children stay in the group.
                        if (IsCheckableParent(child.PropertyType))
                            IndexColumn(child, index, group);
                        TraverseNestedChildren(child, index, visitedTypes, depth + 1, group);
                    }
                    else if (IsScalarType(child.PropertyType))
                    {
                        IndexColumn(child, index, group);
                    }
                    else
                    {
                        // Complex navigation property (e.g., Peptide from Precursor context).
                        // Index if checkable (entity references, annotated values).
                        if (IsCheckableParent(child.PropertyType))
                            IndexColumn(child, index, group);
                        TraverseColumns(child, index, visitedTypes, depth + 1);
                    }
                }
            }
            finally
            {
                visitedTypes.Remove(wrappedType);
            }
        }

        /// <summary>
        /// Add a column to the index, preferring paths with fewer collection steps.
        /// The traversal may find the same invariant name through multiple paths
        /// (e.g., ReplicateName via Precursors!*.Transitions!*.Results!*.Key vs
        /// Results!*.Value.ResultFile.Replicate). The path with fewer collection
        /// lookups produces cleaner reports that match what the ViewEditor generates.
        /// </summary>
        private void IndexColumn(ColumnDescriptor child, Dictionary<string, ColumnInfo> index,
            string group)
        {
            string invariantName = GetInvariantName(child);
            if (index.TryGetValue(invariantName, out var existing))
            {
                if (CountCollectionSteps(child.PropertyPath) < CountCollectionSteps(existing.PropertyPath))
                    index[invariantName] = CreateColumnInfo(invariantName, child, group);
            }
            else
            {
                index[invariantName] = CreateColumnInfo(invariantName, child, group);
            }
        }

        private ColumnInfo CreateColumnInfo(string invariantName, ColumnDescriptor column,
            string group)
        {
            string description = _dataSchema.GetColumnDescription(column) ?? string.Empty;
            string typeName = GetSimpleTypeName(column.PropertyType);
            return new ColumnInfo(invariantName, column.PropertyPath, description, typeName, group);
        }

        private string GetSimpleTypeName(Type type)
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

        private void TraverseNestedChildren(ColumnDescriptor nestedColumn,
            Dictionary<string, ColumnInfo> index, HashSet<Type> visitedTypes, int depth,
            string group)
        {
            if (depth > MAX_DEPTH)
                return;

            foreach (var child in DocumentationGenerator.GetChildColumns(nestedColumn))
            {
                if (_dataSchema.IsHidden(child))
                    continue;

                if (DocumentationGenerator.IsNestedColumn(child))
                {
                    if (IsCheckableParent(child.PropertyType))
                        IndexColumn(child, index, group);
                    TraverseNestedChildren(child, index, visitedTypes, depth + 1, group);
                }
                else if (IsScalarType(child.PropertyType))
                {
                    IndexColumn(child, index, group);
                }
                else
                {
                    if (IsCheckableParent(child.PropertyType))
                        IndexColumn(child, index, group);
                    TraverseColumns(child, index, visitedTypes, depth + 1);
                }
            }
        }

        private string GetInvariantName(ColumnDescriptor child)
        {
            return _dataSchema.GetColumnCaption(child)
                .GetCaption(DataSchemaLocalizer.INVARIANT);
        }

        private string GetGroupName(Type wrappedType)
        {
            return _dataSchema.GetInvariantDisplayName(_dataSchema.DefaultUiMode, wrappedType)
                .GetCaption(DataSchemaLocalizer.INVARIANT);
        }

        private void AddEntityTopic(ColumnDescriptor entityRoot, List<TopicInfo> topics,
            string nameOverride = null)
        {
            var columns = new List<ColumnInfo>();
            string topicName = nameOverride ?? GetTopicDisplayName(entityRoot);
            var entityType = GetEntityType(entityRoot);
            var childEntityNodes = new List<ColumnDescriptor>();

            // Index the entity root itself (e.g., "Precursor" is checkable in the ViewEditor)
            AddColumnToTopic(entityRoot, topicName, columns);
            CollectTopicColumns(entityRoot, topicName, columns, childEntityNodes);

            if (columns.Count > 0)
                topics.Add(new TopicInfo(topicName, columns, entityType));

            // Recurse into child entities in tree order
            foreach (var childEntity in childEntityNodes)
                AddEntityTopic(childEntity, topics);
        }

        /// <summary>
        /// Gets the entity type for a ColumnDescriptor, handling dictionary collections
        /// (where PropertyType is KeyValuePair) by using ElementValueType.
        /// </summary>
        private Type GetEntityType(ColumnDescriptor column)
        {
            if (column.CollectionInfo != null && column.CollectionInfo.IsDictionary)
                return _dataSchema.GetWrappedValueType(column.CollectionInfo.ElementValueType);
            return _dataSchema.GetWrappedValueType(column.PropertyType);
        }

        /// <summary>
        /// Collects scalar columns for the current topic, folding sub-group columns in.
        /// Collection children whose element type is a topic-worthy entity are accumulated
        /// in childEntityNodes for separate topic creation.
        /// </summary>
        private void CollectTopicColumns(ColumnDescriptor column, string topicName,
            List<ColumnInfo> columns, List<ColumnDescriptor> childEntityNodes)
        {
            foreach (var child in ListAllChildren(column))
            {
                if (_dataSchema.IsHidden(child))
                    continue;

                if (child.CollectionInfo != null)
                {
                    // Collection child - is it a topic-worthy entity or a sub-group?
                    // For dictionaries, ElementType is KeyValuePair<K,V>; use ElementValueType instead.
                    var rawElementType = child.CollectionInfo.IsDictionary
                        ? child.CollectionInfo.ElementValueType
                        : child.CollectionInfo.ElementType;
                    var elementType = _dataSchema.GetWrappedValueType(rawElementType);
                    if (TOPIC_ENTITY_SET.Contains(elementType))
                        childEntityNodes.Add(child); // Will become its own topic
                    else
                        CollectTopicColumns(child, topicName, columns, childEntityNodes); // Fold in
                }
                else if (DocumentationGenerator.IsNestedColumn(child))
                {
                    if (IsCheckableParent(child.PropertyType))
                        AddColumnToTopic(child, topicName, columns);
                    CollectNestedScalars(child, topicName, columns, childEntityNodes);
                }
                else if (IsScalarType(child.PropertyType))
                {
                    AddColumnToTopic(child, topicName, columns);
                }
                else
                {
                    // Complex non-collection, non-nested type
                    var wrappedType = _dataSchema.GetWrappedValueType(child.PropertyType);
                    if (DIRECT_PROPERTY_TOPIC_TYPES.Contains(wrappedType))
                    {
                        // Summary entity exposed as a direct property (e.g., ResultSummary)
                        childEntityNodes.Add(child);
                    }
                    else
                    {
                        if (IsCheckableParent(child.PropertyType))
                            AddColumnToTopic(child, topicName, columns);
                        CollectTopicColumns(child, topicName, columns, childEntityNodes);
                    }
                }
            }
        }

        /// <summary>
        /// Replicates AvailableFieldsTree.ListAllChildren - enumerates direct children,
        /// unwrapping dictionaries and detecting collections.
        /// </summary>
        private IList<ColumnDescriptor> ListAllChildren(ColumnDescriptor parent)
        {
            var result = new List<ColumnDescriptor>();
            if (parent.CollectionInfo != null && parent.CollectionInfo.IsDictionary)
            {
                result.Add(parent.ResolveChild(@"Key"));
                result.AddRange(ListAllChildren(parent.ResolveChild(@"Value")));
                return result;
            }
            foreach (var child in parent.GetChildColumns())
            {
                var collectionColumn = child.GetCollectionColumn();
                result.Add(collectionColumn ?? child);
            }
            return result;
        }

        /// <summary>
        /// Collects scalar columns from nested children (those with ChildDisplayName).
        /// These stay in the parent topic.
        /// </summary>
        private void CollectNestedScalars(ColumnDescriptor nestedColumn, string topicName,
            List<ColumnInfo> columns, List<ColumnDescriptor> childEntityNodes)
        {
            foreach (var child in DocumentationGenerator.GetChildColumns(nestedColumn))
            {
                if (_dataSchema.IsHidden(child))
                    continue;

                if (DocumentationGenerator.IsNestedColumn(child))
                {
                    if (IsCheckableParent(child.PropertyType))
                        AddColumnToTopic(child, topicName, columns);
                    CollectNestedScalars(child, topicName, columns, childEntityNodes);
                }
                else if (IsScalarType(child.PropertyType))
                {
                    AddColumnToTopic(child, topicName, columns);
                }
                else
                {
                    if (IsCheckableParent(child.PropertyType))
                        AddColumnToTopic(child, topicName, columns);
                    CollectTopicColumns(child, topicName, columns, childEntityNodes);
                }
            }
        }

        /// <summary>
        /// Gets topic display name using ViewEditor caption resolution.
        /// </summary>
        private string GetTopicDisplayName(ColumnDescriptor column)
        {
            return _dataSchema.GetColumnCaption(column)
                .GetCaption(DataSchemaLocalizer.INVARIANT);
        }

        /// <summary>
        /// Adds a column to the topic list, avoiding duplicates by invariant name.
        /// </summary>
        private void AddColumnToTopic(ColumnDescriptor child, string topicName,
            List<ColumnInfo> columns)
        {
            string invariantName = GetInvariantName(child);
            if (columns.Any(c => string.Equals(c.InvariantName, invariantName,
                    StringComparison.OrdinalIgnoreCase)))
                return;
            columns.Add(CreateColumnInfo(invariantName, child, topicName));
        }

        private bool IsScalarType(Type type)
        {
            return !_dataSchema.GetPropertyDescriptors(type).Any();
        }

        /// <summary>
        /// Returns true for complex types that the ViewEditor displays as checkable
        /// parent nodes. These have child properties but are also valid as standalone
        /// column selections: IAnnotatedValue (e.g., NormalizedArea), SkylineObject
        /// subclasses (entity references like Peptide from Precursor context), and
        /// ProteomicSequence (ModifiedSequence).
        /// </summary>
        private static bool IsCheckableParent(Type type)
        {
            return typeof(IAnnotatedValue).IsAssignableFrom(type) ||
                   typeof(SkylineObject).IsAssignableFrom(type) ||
                   typeof(ProteomicSequence).IsAssignableFrom(type);
        }

        private static List<PropertyPath> TryResolveAll(IList<string> columnNames,
            Dictionary<string, ColumnInfo> index)
        {
            var paths = new List<PropertyPath>(columnNames.Count);
            foreach (string name in columnNames)
            {
                if (!index.TryGetValue(name, out var info))
                    return null;
                paths.Add(info.PropertyPath);
            }
            return paths;
        }

        private static ResolveResult BuildResult(Type rowSourceType, List<PropertyPath> paths,
            Dictionary<string, ColumnInfo> columnIndex)
        {
            var sublistId = FindDeepestSublist(paths);
            return new ResolveResult(rowSourceType, paths, sublistId, columnIndex);
        }

        /// <summary>
        /// Walks each PropertyPath to find the deepest unbound collection lookup (!*),
        /// then returns the deepest one across all paths as the SublistId.
        /// Same algorithm as ReportSpecConverter.
        /// </summary>
        private static PropertyPath FindDeepestSublist(List<PropertyPath> paths)
        {
            var sublistId = PropertyPath.Root;
            foreach (var path in paths)
            {
                var collectionPath = path;
                while (!collectionPath.IsRoot && !collectionPath.IsUnboundLookup)
                    collectionPath = collectionPath.Parent;
                if (collectionPath.StartsWith(sublistId))
                    sublistId = collectionPath;
            }
            return sublistId;
        }

        /// <summary>
        /// Returns true if every path passes through at least one collection (!*).
        /// Used to detect replicate-centric queries resolved from target row sources.
        /// </summary>
        private static bool AllPathsThroughCollection(List<PropertyPath> paths)
        {
            return paths.All(PathContainsCollection);
        }

        private static bool PathContainsCollection(PropertyPath path)
        {
            var walk = path;
            while (!walk.IsRoot)
            {
                if (walk.IsUnboundLookup)
                    return true;
                walk = walk.Parent;
            }
            return false;
        }

        internal static List<string> FindSuggestions(string name,
            ICollection<string> columnNames)
        {
            // Find close matches by case-insensitive substring
            var suggestions = columnNames
                .Where(k => k.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(5)
                .ToList();

            if (suggestions.Count == 0 && name.Length >= 3)
            {
                // Try prefix match
                suggestions = columnNames
                    .Where(k => k.StartsWith(name.Substring(0, 3),
                        StringComparison.OrdinalIgnoreCase))
                    .Take(5)
                    .ToList();
            }

            return suggestions;
        }

        /// <summary>
        /// A documentation topic with display name, column list, and column count.
        /// </summary>
        public class TopicInfo
        {
            public TopicInfo(string displayName, IList<ColumnInfo> columns, Type entityType)
            {
                DisplayName = displayName;
                Columns = columns;
                EntityType = entityType;
            }

            public string DisplayName { get; }
            public IList<ColumnInfo> Columns { get; }
            internal Type EntityType { get; }
        }

        /// <summary>
        /// Successful resolution of column names to PropertyPaths.
        /// </summary>
        public class ResolveResult
        {
            public ResolveResult(Type rowSourceType, List<PropertyPath> propertyPaths,
                PropertyPath sublistId, Dictionary<string, ColumnInfo> columnIndex)
            {
                RowSourceType = rowSourceType;
                PropertyPaths = propertyPaths;
                SublistId = sublistId;
                ColumnIndex = columnIndex;
            }

            public Type RowSourceType { get; }
            public List<PropertyPath> PropertyPaths { get; }
            public PropertyPath SublistId { get; }
            public Dictionary<string, ColumnInfo> ColumnIndex { get; }
        }

        /// <summary>
        /// Column metadata for documentation and resolution. Group identifies the
        /// entity type that owns the column (e.g., "Peptide", "PeptideResult",
        /// "QuantificationResult"), matching the sections in the ViewEditor tree.
        /// </summary>
        public class ColumnInfo
        {
            public ColumnInfo(string invariantName, PropertyPath propertyPath,
                string description, string typeName, string group)
            {
                InvariantName = invariantName;
                PropertyPath = propertyPath;
                Description = description;
                TypeName = typeName;
                Group = group;
            }

            public string InvariantName { get; }
            public PropertyPath PropertyPath { get; }
            public string Description { get; }
            public string TypeName { get; }
            public string Group { get; }
        }

        /// <summary>
        /// A column name that could not be resolved, with suggested alternatives.
        /// </summary>
        public class UnresolvedColumn
        {
            public UnresolvedColumn(string name, List<string> suggestions)
            {
                Name = name;
                Suggestions = suggestions;
            }

            public string Name { get; }
            public List<string> Suggestions { get; }
        }

        /// <summary>
        /// Thrown when one or more column names cannot be resolved to PropertyPaths.
        /// Contains structured data about each unresolved column and suggested alternatives.
        /// </summary>
        public class UnresolvedColumnsException : Exception
        {
            public UnresolvedColumnsException(List<UnresolvedColumn> unresolvedColumns)
                : base(FormatMessage(unresolvedColumns))
            {
                UnresolvedColumns = unresolvedColumns;
            }

            public List<UnresolvedColumn> UnresolvedColumns { get; }

            // Basic message for non-LLM consumers (e.g., logging)
            private static string FormatMessage(List<UnresolvedColumn> columns)
            {
                return string.Format(@"Unresolved columns: {0}",
                    string.Join(@", ", columns.Select(c => c.Name)));
            }
        }
    }
}
