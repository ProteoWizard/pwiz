/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Threading;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.Layout;

namespace pwiz.Common.DataBinding.Internal
{
    internal class GroupAndTotaler
    {
        public GroupAndTotaler(CancellationToken cancellationToken, DataSchema dataSchema, PivotSpec pivotSpec, IEnumerable<DataPropertyDescriptor> itemProperties)
        {
            CancellationToken = cancellationToken;
            DataSchema = dataSchema;
            var columnsByDisplayName = itemProperties.ToLookup(ColumnId.GetColumnId);
            RowHeaders = ImmutableList.ValueOf(pivotSpec.RowHeaders.SelectMany(
                col => columnsByDisplayName[col.SourceColumn]
                    .Select(pd=>Tuple.Create(pd, ColumnCaption.ExplicitCaption(col.Caption) ?? ColumnCaption.GetColumnCaption(pd)))));
            ColumnHeaders = ImmutableList.ValueOf(pivotSpec.ColumnHeaders.SelectMany(
                col => columnsByDisplayName[col.SourceColumn]));
            AggregateColumns = ImmutableList.ValueOf(pivotSpec.Values.SelectMany(
                col => columnsByDisplayName[col.SourceColumn]
                    .Select(pd => Tuple.Create(pd,
                        ColumnCaption.ExplicitCaption(col.Caption) ??
                        col.AggregateOperation.QualifyColumnCaption(ColumnCaption.GetColumnCaption(pd)),
                        col.AggregateOperation))));
        }

        public CancellationToken CancellationToken { get; private set; }
        public ImmutableList<Tuple<DataPropertyDescriptor, IColumnCaption>> RowHeaders { get; private set; }
        public ImmutableList<DataPropertyDescriptor> ColumnHeaders { get; private set; }
        public ImmutableList<Tuple<DataPropertyDescriptor, IColumnCaption, AggregateOperation>> AggregateColumns { get; private set; }
        public DataSchema DataSchema { get; private set; }
        public IEnumerable<RowItem> GroupAndTotal(IEnumerable<RowItem> rows, IList<DataPropertyDescriptor> propertyDescriptors)
        {
            var rowsByRowHeader = rows.ToLookup(row =>
            {
                CancellationToken.ThrowIfCancellationRequested();
                return ImmutableList.ValueOf(RowHeaders.Select(tuple=> tuple.Item1.GetValue(row)));
            });
            propertyDescriptors.AddRange(RowHeaders.Select((tuple, index)=>new IndexedPropertyDescriptor(DataSchema, index, tuple.Item1, tuple.Item2)));
            var columnHeaders = new Dictionary<ImmutableList<object>, int>();
            var resultRows = new List<RowItem>();
            List<List<object>> aggregateValues = new List<List<object>>();
            foreach (IGrouping<ImmutableList<object>, RowItem> rowGroup in rowsByRowHeader)
            {
                foreach (var list in aggregateValues)
                {
                    list.Clear();
                }
                var rowValues = rowGroup.Key.ToList();
                foreach (RowItem row in rowGroup)
                {
                    var columnHeader = ImmutableList.ValueOf(ColumnHeaders.Select(pd => pd.GetValue(row)));
                    int columnHeaderIndex;
                    if (!columnHeaders.TryGetValue(columnHeader, out columnHeaderIndex))
                    {
                        columnHeaderIndex = columnHeaders.Count;
                        columnHeaders.Add(columnHeader, columnHeaderIndex);
                        foreach (var aggregateColumn in AggregateColumns)
                        {
                            propertyDescriptors.Add(MakePropertyDescriptor(propertyDescriptors.Count, columnHeader,
                                aggregateColumn.Item1, aggregateColumn.Item2, aggregateColumn.Item3));
                            aggregateValues.Add(new List<object>());
                        }
                    }
                    for (int iAggColumn = 0; iAggColumn < AggregateColumns.Count; iAggColumn++)
                    {
                        CancellationToken.ThrowIfCancellationRequested();
                        var value = AggregateColumns[iAggColumn].Item1.GetValue(row);
                        if (value != null)
                        {
                            var aggList = aggregateValues[iAggColumn + columnHeaderIndex * AggregateColumns.Count];
                            aggList.Add(value);
                        }
                    }
                }
                for (int iGroup = 0; iGroup < columnHeaders.Count; iGroup++)
                {
                    for (int iAggColumn = 0; iAggColumn < AggregateColumns.Count; iAggColumn++)
                    {
                        int aggregateValueIndex = iGroup * AggregateColumns.Count + iAggColumn;
                        var individualValues = aggregateValues[aggregateValueIndex];
                        if (individualValues.Count == 0)
                        {
                            rowValues.Add(null);
                        }
                        else
                        {
                            rowValues.Add(AggregateColumns[iAggColumn].Item3.CalculateValue(DataSchema, individualValues));
                        }
                    }
                }
                resultRows.Add(new RowItem(rowValues));
            }
            return resultRows;
        }

        public IndexedPropertyDescriptor MakePropertyDescriptor(int index, ImmutableList<object> columnHeaderKey,
            PropertyDescriptor originalPropertyDescriptor, IColumnCaption caption, AggregateOperation aggregateOperation)
        {
            IColumnCaption qualifiedCaption;
            PivotedColumnId pivotedColumnId = null;
            if (columnHeaderKey.Count == 0)
            {
                qualifiedCaption = caption;
            }
            else
            {
                var pivotCaptionComponents = columnHeaderKey.Select(CaptionComponentList.MakeCaptionComponent).ToList();
                qualifiedCaption = new CaptionComponentList(pivotCaptionComponents.Append(caption));
                pivotedColumnId = new PivotedColumnId(columnHeaderKey,
                    new CaptionComponentList(pivotCaptionComponents),
                    caption,
                    caption);
            }
            var attributes = DataSchema.GetAggregateAttributes(originalPropertyDescriptor, aggregateOperation).ToArray();
            return new IndexedPropertyDescriptor(DataSchema, index, aggregateOperation.GetPropertyType(originalPropertyDescriptor.PropertyType), 
                qualifiedCaption, pivotedColumnId, attributes);
        }

        public static ReportResults GroupAndTotal(CancellationToken cancellationToken, DataSchema dataSchema,
            PivotSpec pivotSpec, ReportResults input)
        {
            if (pivotSpec.IsEmpty)
            {
                return input;
            }
            var groupAndTotaller = new GroupAndTotaler(cancellationToken, dataSchema, pivotSpec, input.ItemProperties);
            var newProperties = new List<DataPropertyDescriptor>();
            var newRows = ImmutableList.ValueOf(groupAndTotaller.GroupAndTotal(input.RowItems, newProperties));
            return new ReportResults(newRows, newProperties);
        }
    }
}
