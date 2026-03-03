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
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Databinding
{
    /// <summary>
    /// Maps invariant column display names to PropertyPaths by traversing
    /// the ColumnDescriptor tree for each candidate row source type.
    /// Selects the shallowest row source that resolves all requested columns.
    /// </summary>
    public class ColumnResolver
    {
        private const int MAX_DEPTH = 12;

        private static readonly Type[] TARGET_ROW_SOURCES =
        {
            typeof(Protein),
            typeof(Entities.Peptide),
            typeof(Precursor),
            typeof(Entities.Transition)
        };

        private readonly SkylineDataSchema _dataSchema;

        public ColumnResolver(SkylineDataSchema dataSchema)
        {
            _dataSchema = dataSchema;
        }

        public ResolveResult Resolve(IList<string> columnNames)
        {
            // Try each target row source, shallowest first
            ResolveResult firstTargetMatch = null;
            foreach (var rowSourceType in TARGET_ROW_SOURCES)
            {
                var index = BuildColumnIndex(rowSourceType);
                var paths = TryResolveAll(columnNames, index);
                if (paths != null)
                {
                    var result = BuildResult(rowSourceType, paths, index);
                    // If every resolved path goes through a collection, this is a
                    // replicate-centric query. Defer to the Replicate row source
                    // if it can resolve all columns, since target-level resolution
                    // produces a pivoted cross-join instead of one row per replicate.
                    if (!AllPathsThroughCollection(paths))
                        return result;
                    firstTargetMatch = result;
                    break;
                }
            }

            // Try Replicate row source
            {
                var index = BuildColumnIndex(typeof(Replicate));
                var paths = TryResolveAll(columnNames, index);
                if (paths != null)
                    return BuildResult(typeof(Replicate), paths, index);
            }

            // If a target row source matched (all-Results paths) but Replicate
            // didn't work, use the target match we found earlier
            if (firstTargetMatch != null)
                return firstTargetMatch;

            // None worked -- collect structured error info
            var broadIndex = BuildColumnIndex(typeof(Entities.Transition));
            var unresolvedColumns = new List<UnresolvedColumn>();
            foreach (string name in columnNames)
            {
                if (!broadIndex.ContainsKey(name))
                    unresolvedColumns.Add(new UnresolvedColumn(name, FindSuggestions(name, broadIndex)));
            }
            throw new UnresolvedColumnsException(unresolvedColumns);
        }

        private Dictionary<string, PropertyPath> BuildColumnIndex(Type rowSourceType)
        {
            var index = new Dictionary<string, PropertyPath>(StringComparer.OrdinalIgnoreCase);
            var rootColumn = ColumnDescriptor.RootColumn(_dataSchema, rowSourceType);
            var visitedTypes = new HashSet<Type>();
            TraverseColumns(rootColumn, index, visitedTypes, 0);
            return index;
        }

        private void TraverseColumns(ColumnDescriptor column,
            Dictionary<string, PropertyPath> index, HashSet<Type> visitedTypes, int depth)
        {
            if (depth > MAX_DEPTH)
                return;

            var wrappedType = _dataSchema.GetWrappedValueType(column.PropertyType);
            if (!visitedTypes.Add(wrappedType))
                return;

            try
            {
                foreach (var child in DocumentationGenerator.GetChildColumns(column))
                {
                    if (_dataSchema.IsHidden(child))
                        continue;

                    if (DocumentationGenerator.IsNestedColumn(child))
                    {
                        // Nested columns (e.g., PrecursorResultsSummary) have children
                        // with combined names. Recurse into them but don't add the parent.
                        TraverseNestedChildren(child, index, visitedTypes, depth + 1);
                    }
                    else if (IsScalarType(child.PropertyType))
                    {
                        // Leaf column - record invariant name -> PropertyPath mapping
                        string invariantName = GetInvariantName(child);
                        if (!index.ContainsKey(invariantName))
                            index[invariantName] = child.PropertyPath;
                    }
                    else
                    {
                        // Complex navigation property - recurse into children
                        TraverseColumns(child, index, visitedTypes, depth + 1);
                    }
                }
            }
            finally
            {
                visitedTypes.Remove(wrappedType);
            }
        }

        private void TraverseNestedChildren(ColumnDescriptor nestedColumn,
            Dictionary<string, PropertyPath> index, HashSet<Type> visitedTypes, int depth)
        {
            if (depth > MAX_DEPTH)
                return;

            foreach (var child in DocumentationGenerator.GetChildColumns(nestedColumn))
            {
                if (_dataSchema.IsHidden(child))
                    continue;

                if (DocumentationGenerator.IsNestedColumn(child))
                {
                    TraverseNestedChildren(child, index, visitedTypes, depth + 1);
                }
                else if (IsScalarType(child.PropertyType))
                {
                    string invariantName = GetInvariantName(child);
                    if (!index.ContainsKey(invariantName))
                        index[invariantName] = child.PropertyPath;
                }
                else
                {
                    TraverseColumns(child, index, visitedTypes, depth + 1);
                }
            }
        }

        private string GetInvariantName(ColumnDescriptor child)
        {
            return _dataSchema.GetColumnCaption(child)
                .GetCaption(DataSchemaLocalizer.INVARIANT);
        }

        private bool IsScalarType(Type type)
        {
            return !_dataSchema.GetPropertyDescriptors(type).Any();
        }

        private static List<PropertyPath> TryResolveAll(IList<string> columnNames,
            Dictionary<string, PropertyPath> index)
        {
            var paths = new List<PropertyPath>(columnNames.Count);
            foreach (string name in columnNames)
            {
                if (!index.TryGetValue(name, out var path))
                    return null;
                paths.Add(path);
            }
            return paths;
        }

        private static ResolveResult BuildResult(Type rowSourceType, List<PropertyPath> paths,
            Dictionary<string, PropertyPath> columnIndex)
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
            Dictionary<string, PropertyPath> index)
        {
            // Find close matches by case-insensitive substring
            var suggestions = index.Keys
                .Where(k => k.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(5)
                .ToList();

            if (suggestions.Count == 0 && name.Length >= 3)
            {
                // Try prefix match
                suggestions = index.Keys
                    .Where(k => k.StartsWith(name.Substring(0, 3),
                        StringComparison.OrdinalIgnoreCase))
                    .Take(5)
                    .ToList();
            }

            return suggestions;
        }

        /// <summary>
        /// Successful resolution of column names to PropertyPaths.
        /// </summary>
        public class ResolveResult
        {
            public ResolveResult(Type rowSourceType, List<PropertyPath> propertyPaths,
                PropertyPath sublistId, Dictionary<string, PropertyPath> columnIndex)
            {
                RowSourceType = rowSourceType;
                PropertyPaths = propertyPaths;
                SublistId = sublistId;
                ColumnIndex = columnIndex;
            }

            public Type RowSourceType { get; }
            public List<PropertyPath> PropertyPaths { get; }
            public PropertyPath SublistId { get; }
            public Dictionary<string, PropertyPath> ColumnIndex { get; }
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
