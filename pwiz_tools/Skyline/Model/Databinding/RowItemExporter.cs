/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding;
using pwiz.Common.Properties;
using pwiz.Common.SystemUtil;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace pwiz.Skyline.Model.Databinding
{
    public class RowItemExporter : IRowItemExporter
    {
        public RowItemExporter(DsvWriter dsvWriter)
        {
            DsvWriter = dsvWriter;
        }
        
        public DsvWriter DsvWriter { get; }

        public void Export(IProgressMonitor progressMonitor, ref IProgressStatus status, Stream stream,
            RowItemEnumerator rowItemEnumerator)
        {
            using var writer = new StreamWriter(stream);
            Export(progressMonitor, ref status, writer, rowItemEnumerator);
        }

        public void Export(IProgressMonitor progressMonitor, ref IProgressStatus status, TextWriter writer,
            RowItemEnumerator rowItemEnumerator) 
        {
            var replicatePivotColumns = ReplicatePivotColumns.FromItemProperties(rowItemEnumerator.ItemProperties);
            if (true != replicatePivotColumns?.HasConstantAndVariableColumns())
            {
                WriteFlatTable(progressMonitor, ref status, writer, rowItemEnumerator);
                return;
            }

            // Filter columns for main grid to be written out.
            var filteredColumnDescriptors = rowItemEnumerator.ItemProperties.OfType<ColumnPropertyDescriptor>()
                .Where(columnDescriptor => !replicatePivotColumns.IsConstantColumn(columnDescriptor)).ToList();

            // Count main grid columns to add spaces.
            var replicateVariablePropertyCounts = filteredColumnDescriptors.GroupBy(column => column.PivotKey ?? PivotKey.EMPTY)
                .ToDictionary(grouping => grouping.Key, grouping => grouping.Count());
            // Create header line with property column and replicate headers.
            var propertyCaption = LocalizationHelper.CallWithCulture(rowItemEnumerator.ItemProperties.First().DataSchemaLocalizer.Language,
                () => DatabindingResources.SkylineViewContext_WriteDataWithStatus_Property);
            var headerLine = Enumerable.Repeat(string.Empty, replicateVariablePropertyCounts[PivotKey.EMPTY] - 1)
                .Prepend(propertyCaption).ToList();
            var propertyLineDictionary = new Dictionary<PropertyPath, List<string>>();
            var propertyLines = new List<List<string>> { headerLine };
            var allRowItems = rowItemEnumerator.GetRowItems();
            foreach (var group in replicatePivotColumns.GetReplicateColumnGroups())
            {
                // Add replicate to header line.
                headerLine.Add(group.Key.ReplicateName);
                headerLine.AddRange(Enumerable.Repeat(string.Empty, replicateVariablePropertyCounts[group.ToList()[0].PivotKey] - 1));

                foreach (var column in group)
                {
                    if (!replicatePivotColumns.IsConstantColumn(column) || column.DisplayColumn.ColumnDescriptor == null)
                    {
                        continue;
                    }

                    var propertyPath = column.DisplayColumn.PropertyPath;
                    if (!propertyLineDictionary.TryGetValue(propertyPath, out var propertyLine))
                    {
                        // Create a new row if property line does not exist yet.
                        var propertyDisplayName = column.DisplayColumn.ColumnDescriptor.GetColumnCaption(ColumnCaptionType.localized);
                        propertyLine = Enumerable.Repeat(string.Empty, replicateVariablePropertyCounts[PivotKey.EMPTY] - 1).Prepend(propertyDisplayName).ToList();
                        propertyLineDictionary[propertyPath] = propertyLine;
                        propertyLines.Add(propertyLine);
                    }

                    // Add value to property line.
                    var rowItem = allRowItems.FirstOrDefault(item => column.GetValue(item) != null);
                    var formattedValue = DsvWriter.GetFormattedValue(rowItem, column);
                    propertyLine.AddRange(Enumerable.Repeat(string.Empty, replicateVariablePropertyCounts[column.PivotKey] - 1).Prepend(formattedValue));
                }
            }

            // Write pivot replicate data.
            foreach (var line in propertyLines)
            {
                DsvWriter.WriteRowValues(writer, line);
            }
            writer.WriteLine();
            var newRowItemEnumerator = new RowItemEnumerator(allRowItems, new ItemProperties(filteredColumnDescriptors), rowItemEnumerator.ColumnFormats);
            WriteFlatTable(progressMonitor, ref status, writer, newRowItemEnumerator);
        }

        public void WriteFlatTable(IProgressMonitor progressMonitor, ref IProgressStatus status, TextWriter writer,
            RowItemEnumerator rowItemEnumerator)
        {
            DsvWriter.WriteHeaderRow(writer, rowItemEnumerator.ItemProperties);
            var rowCount = rowItemEnumerator.Count;
            int startPercent = status.PercentComplete;
            int rowIndex = 0;
            while (rowItemEnumerator.MoveNext())
            {
                if (progressMonitor.IsCanceled)
                {
                    return;
                }
                int percentComplete = (int) (startPercent + rowIndex * (100 - startPercent) / rowCount);
                if (percentComplete > status.PercentComplete)
                {
                    status = status.ChangeMessage(string.Format(Resources.AbstractViewContext_WriteData_Writing_row__0___1_, (rowIndex + 1), rowCount))
                        .ChangePercentComplete(percentComplete);
                    progressMonitor.UpdateProgress(status);
                }
                DsvWriter.WriteDataRow(writer, rowItemEnumerator.Current, rowItemEnumerator.ItemProperties);
                rowIndex++;
            }
        }
        
    }
}
