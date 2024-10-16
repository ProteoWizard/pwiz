/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;

namespace pwiz.Skyline.Model.Databinding
{
    public class ReportSpecConverter
    {
        public ReportSpecConverter(SkylineDataSchema dataSchema)
        {
            DataSchema = dataSchema;
        }

        public SkylineDataSchema DataSchema { get; private set; }
        public static Type GetNewTableType(ReportSpec reportSpec)
        {
            var rowTypes = new List<Type>();
            foreach (var column in reportSpec.Select)
            {
                var rowType = GetRootTable(column);
                if (null != rowType)
                {
                    rowTypes.Add(rowType);
                }
            }
            var unique = rowTypes.Distinct().ToArray();
            if (unique.Length != 1)
            {
                return null;
            }
            return unique[0];
        }

        public static Type GetRootTable(ReportColumn reportColumn)
        {
            var databindingTable = GetDatabindingTableAttribute(reportColumn);
            if (null == databindingTable)
            {
                return null;
            }
            return databindingTable.RootTable;
        }
        public static Type GetRootTable(ReportSpec reportSpec)
        {
            var types = reportSpec.Select.Select(GetRootTable).Distinct().ToArray();
            if (types.Length == 1)
            {
                return types[0];
            }
            return null;
        }
        /// <summary>
        /// In old custom reports, if the report was showing rows from a Results table,
        /// the report would not include any DocNode's which did not have any results.
        /// To preserve this behavior we add a filter that only DocNode's which have at
        /// least one Result get included.
        /// </summary>
        public static ViewSpec AddFilter(ViewSpec viewSpec, ReportSpec reportSpec)
        {
            var propertyPaths = new HashSet<PropertyPath>();
            IEnumerable<ReportColumn> columns = reportSpec.Select;
            if (reportSpec.CrossTabValues != null)
            {
                columns = columns.Concat(reportSpec.CrossTabValues);
            }
            foreach (var reportColumn in columns)
            {
                var databindingTableAttribute = GetDatabindingTableAttribute(reportColumn);
                if (null != databindingTableAttribute.Property && !databindingTableAttribute.Property.EndsWith(@"Summary"))
                {
                    propertyPaths.Add(PropertyPath.Parse(databindingTableAttribute.Property));
                }
            }
            var newFilters = propertyPaths.Select(propertyPath => new FilterSpec(propertyPath, FilterPredicate.IS_NOT_BLANK));
            viewSpec = viewSpec.SetFilters(viewSpec.Filters.Concat(newFilters));
            return viewSpec;
        }

        public static DatabindingTableAttribute GetDatabindingTableAttribute(ReportColumn reportColumn)
        {
            return (DatabindingTableAttribute)TypeDescriptor.GetAttributes(reportColumn.Table)[typeof(DatabindingTableAttribute)];
        }

        public IEnumerable<ViewSpec> ConvertAll(IEnumerable<ReportSpec> reportSpecs)
        {
            foreach (var reportSpec in reportSpecs)
            {
                yield return Convert(reportSpec).GetViewSpec().SetUiMode(string.Empty);
            }
        }
        public ViewInfo Convert(ReportSpec reportSpec)
        {
            var rootTable = GetRootTable(reportSpec);
            if (null == rootTable)
            {
                return null;
            }
            var columns = new List<ColumnSpec>();
            var columnNames = new HashSet<PropertyPath>();
            var sublistId = PropertyPath.Root;
            foreach (var reportColumn in reportSpec.Select)
            {
                var columnSpec = ConvertReportColumn(reportColumn);
                var collectionProperty = columnSpec.PropertyPath;
                while (!collectionProperty.IsRoot && !collectionProperty.IsUnboundLookup)
                {
                    collectionProperty = collectionProperty.Parent;
                }
                if (collectionProperty.StartsWith(sublistId))
                {
                    sublistId = collectionProperty;
                }
                if (columnNames.Add(columnSpec.PropertyPath))
                {
                    columns.Add(columnSpec);
                }
            }
            if (null != reportSpec.GroupBy)
            {
                foreach (var reportColumn in reportSpec.GroupBy)
                {
                    var columnSpec = ConvertReportColumn(reportColumn);
                    if (!columns.Any(col => Equals(col.PropertyPath, columnSpec.PropertyPath)))
                    {
                        columns.Add(columnSpec.SetHidden(true));
                    }
                }
            }
            bool pivotIsotopeLabel = false;
            if (null != reportSpec.CrossTabHeaders)
            {
                pivotIsotopeLabel =
                    reportSpec.CrossTabHeaders.Any(reportColumn => !reportColumn.Column.ToString().EndsWith(@"Replicate"));
                if (pivotIsotopeLabel)
                {
                    sublistId = PropertyPath.Root.Property(@"Results").LookupAllItems();
                }
                foreach (var reportColumn in reportSpec.CrossTabHeaders)
                {
                    if (pivotIsotopeLabel || !reportColumn.Column.ToString().EndsWith(@"Replicate"))
                    {
                        var columnSpec = ConvertReportColumn(reportColumn).SetTotal(TotalOperation.PivotKey).SetHidden(true);
                        columns.Add(columnSpec);
                    }
                }
            }
            if (null != reportSpec.CrossTabValues)
            {
                foreach (var reportColumn in reportSpec.CrossTabValues)
                {
                    var convertedColumn = ConvertReportColumn(reportColumn);
                    if (pivotIsotopeLabel)
                    {
                        convertedColumn = convertedColumn.SetTotal(TotalOperation.PivotValue);
                    }
                    if (columnNames.Add(convertedColumn.PropertyPath))
                    {
                        columns.Add(convertedColumn);
                    }
                }
            }
            var viewSpec = new ViewSpec()
                .SetName(reportSpec.Name)
                .SetSublistId(sublistId)
                .SetColumns(columns)
                .SetRowType(rootTable);
            viewSpec = AddFilter(viewSpec, reportSpec);
            return new ViewInfo(DataSchema, rootTable, viewSpec);
        }

        private ColumnSpec ConvertReportColumn(ReportColumn reportColumn)
        {
            var identifierPath = PropertyPath.Root;
            var databindingTableAttribute = GetDatabindingTableAttribute(reportColumn);
            var component = reportColumn.Table;
            string oldCaption = null;
            foreach (string part in reportColumn.Column.Parts)
            {
                PropertyPath propertyPath;
                if (part.StartsWith(AnnotationDef.ANNOTATION_PREFIX))
                {
                    string annotationName = AnnotationDef.GetColumnDisplayName(part);
                    if (component == typeof (DbProteinResult))
                    {
                        propertyPath = PropertyPath.Root.Property(@"Replicate").Property(AnnotationDef.ANNOTATION_PREFIX + annotationName);
                    }
                    else
                    {
                        propertyPath = PropertyPath.Root.Property(AnnotationDef.ANNOTATION_PREFIX + annotationName);
                    }
                    oldCaption = annotationName;
                    component = typeof (string);
                }
                else if (RatioPropertyAccessor.IsRatioOrRdotpProperty(part))
                {
                    propertyPath = null;
                    if (component == typeof (DbPeptideResult))
                    {
                        const string prefixPeptideRatio = "ratio_Ratio";
                        string labelName, standardName;
                        if (part.StartsWith(prefixPeptideRatio))
                        {
                            if (TryParseLabelNames(part.Substring(prefixPeptideRatio.Length), out labelName, out standardName))
                            {
                                propertyPath = PropertyPath.Root.Property(RatioPropertyDescriptor.MakePropertyName(
                                    RatioPropertyDescriptor.RATIO_PREFIX, labelName, standardName));
                            }
                        }
                        const string prefixPeptideRdotp = "rdotp_DotProduct";
                        if (part.StartsWith(prefixPeptideRdotp))
                        {
                            if (TryParseLabelNames(part.Substring(prefixPeptideRdotp.Length), out labelName, out standardName))
                            {
                                propertyPath = PropertyPath.Root.Property(RatioPropertyDescriptor.MakePropertyName(
                                    RatioPropertyDescriptor.RDOTP_PREFIX, labelName, standardName));
                            }
                        }
                    }
                    else if (component == typeof (DbPrecursorResult))
                    {
                        const string prefixPrecursorRatio = "ratio_TotalAreaRatioTo";
                        const string prefixPrecursorRdotp = "rdotp_DotProductTo";
                        if (part.StartsWith(prefixPrecursorRatio))
                        {
                            propertyPath = PropertyPath.Root.Property(
                                RatioPropertyDescriptor.MakePropertyName(RatioPropertyDescriptor.RATIO_PREFIX, part.Substring(prefixPrecursorRatio.Length)));
                        }
                        else if (part.StartsWith(prefixPrecursorRdotp))
                        {
                            propertyPath = PropertyPath.Root.Property(
                                RatioPropertyDescriptor.MakePropertyName(RatioPropertyDescriptor.RDOTP_PREFIX,
                                    part.Substring(prefixPrecursorRdotp.Length)));
                        }
                    }
                    else if (component == typeof (DbTransitionResult))
                    {
                        const string prefixTransitionRatio = "ratio_AreaRatioTo";
                        if (part.StartsWith(prefixTransitionRatio))
                        {
                            propertyPath = PropertyPath.Root.Property(
                                RatioPropertyDescriptor.MakePropertyName(RatioPropertyDescriptor.RATIO_PREFIX, 
                                part.Substring(prefixTransitionRatio.Length)));
                        }
                    }
                    component = typeof (double);
                    oldCaption = null;
                    if (null == propertyPath)
                    {
                        Messages.WriteAsyncDebugMessage(@"Unable to parse ratio property {0}", part);
                        propertyPath = PropertyPath.Root.Property(part);
                    }
                }
//                else if (component == typeof (DbProteinResult) && part == "ResultFile")
//                {
//                    propertyPath = PropertyPath.Parse("Results!*.Value");
//                }
                else
                {
                    PropertyInfo property = component.GetProperty(part);
                    if (null == property)
                    {
                        Messages.WriteAsyncDebugMessage(@"Could not find property {0}", part);
                        continue;
                    }
                    propertyPath = PropertyPath.Root.Property(part);
                    foreach (DatabindingColumnAttribute databindingColumn in
                        property.GetCustomAttributes(typeof (DatabindingColumnAttribute), true))
                    {
                        if (null != databindingColumn.Name)
                        {
                            propertyPath = PropertyPath.Parse(databindingColumn.Name);
                            break;
                        }
                    }
                    oldCaption = property.Name;
                    foreach (QueryColumn attr in property.GetCustomAttributes(typeof (QueryColumn), true))
                    {
                        oldCaption = attr.FullName ?? oldCaption;
                    }
                    component = property.PropertyType;
                }
                identifierPath = identifierPath.Concat(propertyPath);
            }
            var columnDescriptor = GetColumnDescriptor(databindingTableAttribute, identifierPath);
            if (null == columnDescriptor)
            {
                return new ColumnSpec(identifierPath);
            }
            var columnSpec = new ColumnSpec(columnDescriptor.PropertyPath);
            var newCaption = DataSchema.GetColumnCaption(columnDescriptor).GetCaption(DataSchemaLocalizer.INVARIANT);
            if (oldCaption != newCaption)
            {
                columnSpec = columnSpec.SetCaption(oldCaption);
            }
            return columnSpec;
        }

        private ColumnDescriptor GetColumnDescriptor(DatabindingTableAttribute databindingTable, PropertyPath identifierPath)
        {
            identifierPath = PropertyPath.Parse(databindingTable.Property).Concat(identifierPath);
            var viewSpec = new ViewSpec().SetColumns(new[] { new ColumnSpec(identifierPath) });
            var viewInfo = new ViewInfo(DataSchema, databindingTable.RootTable, viewSpec);
            return viewInfo.DisplayColumns.First().ColumnDescriptor;
        }

        private bool TryParseLabelNames(string ratioParts, out string labelName, out string standardName)
        {
            const string splitter = "To";
            int ichSplitter = ratioParts.IndexOf(splitter, 1, StringComparison.InvariantCulture);
            if (ichSplitter < 0 || ichSplitter + splitter.Length >= ratioParts.Length)
            {
                labelName = standardName = null;
                return false;
            }
            labelName = ratioParts.Substring(0, ichSplitter);
            standardName = ratioParts.Substring(ichSplitter + splitter.Length);
            return true;
        }
    }
}
