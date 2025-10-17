﻿using pwiz.Common.DataBinding;
using pwiz.Common.Properties;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Databinding;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace pwiz.Skyline.Model.Databinding
{
    public class RowItemExporter
    {
        public RowItemExporter(DataSchemaLocalizer localizer, DsvWriter dsvWriter)
        {
            Localizer = localizer;
            DsvWriter = dsvWriter;
        }
        
        public DataSchemaLocalizer Localizer { get; }
        public DsvWriter DsvWriter { get; }
        
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
            var propertyCaption = LocalizationHelper.CallWithCulture(Localizer.Language,
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
                int percentComplete = startPercent + (rowIndex * (100 - startPercent) / rowCount);
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
