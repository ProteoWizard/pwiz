using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using pwiz.Common.DataAnalysis.Clustering;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Clustering;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Databinding;

namespace pwiz.Skyline.Controls.Clustering
{
    public class ClusterInput
    {
        public ClusterInput(DataSchemaLocalizer dataSchemaLocalizer, ReportResults reportResults,
            ClusteringSpec clusteringSpec)
        {
            DataSchemaLocalizer = dataSchemaLocalizer;
            ReportResults = reportResults;
            ClusteringSpec = clusteringSpec;
        }

        public DataSchemaLocalizer DataSchemaLocalizer { get; private set; }
        public ReportResults ReportResults { get; private set; }
        public ClusteringSpec ClusteringSpec { get; private set; }

        public ClusterGraphResults GetClusterGraphResults(ProgressHandler progressHandler)
        {
            var tuple = GetClusterResultsTuple(progressHandler);
            if (tuple == null)
            {
                return null;
            }
            var clusteredResults = tuple.Item2;
            var colorScheme = tuple.Item3;
            var points = new List<ClusterGraphResults.Point>();
            var rowHeaders = new List<ClusterGraphResults.Header>();
            var columnValues = new List<PivotedProperties.Series>();
            var columnGroups = new List<ClusterGraphResults.ColumnGroup>();
            var cellLocators = new List<CellLocator>();
            for (int iGroup = 0; iGroup < clusteredResults.PivotedProperties.SeriesGroups.Count; iGroup++)
            {
                var group = clusteredResults.PivotedProperties.SeriesGroups[iGroup];

                var groupColumnHeaders = clusteredResults.ClusteredProperties.GetColumnHeaders(group).ToList();
                var groupHeaders = new List<ClusterGraphResults.Header>();
                for (int iPivotKey = 0; iPivotKey < group.PivotKeys.Count; iPivotKey++)
                {
                    var colors = new List<Color>();
                    foreach (var series in groupColumnHeaders)
                    {
                        var pd = series.PropertyDescriptors[iPivotKey];
                        colors.Add(colorScheme.GetColumnColor(pd) ?? Color.Transparent);
                    }
                    groupHeaders.Add(new ClusterGraphResults.Header(group.PivotCaptions[iPivotKey].GetCaption(DataSchemaLocalizer), colors));
                }
                foreach (var series in group.SeriesList)
                {
                    var transform = clusteredResults.ClusteredProperties.GetColumnRole(series) as ClusterRole.Transform;
                    if (transform == null)
                    {
                        continue;
                    }
                    columnValues.Add(series);
                    columnGroups.Add(new ClusterGraphResults.ColumnGroup(
                        clusteredResults.ColumnGroupDendrogramDatas[iGroup].DendrogramData, groupHeaders));
                    for (int iProperty = 0; iProperty < series.PropertyIndexes.Count; iProperty++)
                    {
                        var columnHeaders = groupColumnHeaders.Prepend(series)
                            .Select(s => s.PropertyDescriptors[iProperty]).ToList();
                        var cellLocator = CellLocator.ForColumn(columnHeaders, clusteredResults.ItemProperties);
                        cellLocators.Add(cellLocator);
                    }
                }
            }
            for (int iRow = 0; iRow < clusteredResults.RowCount; iRow++)
            {
                var rowItem = clusteredResults.RowItems[iRow];
                var rowColors = new List<Color>();
                var rowHeaderParts = new List<object>();
                foreach (var rowHeader in clusteredResults.ClusteredProperties.RowHeaders)
                {
                    rowHeaderParts.Add(rowHeader.GetValue(rowItem));
                    rowColors.Add(colorScheme.GetColor(rowHeader, rowItem) ?? Color.Transparent);
                }

                string rowCaption = CaptionComponentList.SpaceSeparate(rowHeaderParts)
                    .GetCaption(DataSchemaLocalizer.INVARIANT);
                rowHeaders.Add(new ClusterGraphResults.Header(rowCaption, rowColors));
                int iCol = 0;
                foreach (var series in columnValues)
                {
                    foreach (var color in colorScheme.GetSeriesColors(series, rowItem))
                    {
                        var point = new ClusterGraphResults.Point(iRow, iCol, color);
                        var skylineDocNode = cellLocators[iCol].GetSkylineDocNode(rowItem);
                        if (skylineDocNode != null)
                        {
                            point = point.ChangeIdentityPath(skylineDocNode.IdentityPath);
                        }

                        var replicate = cellLocators[iCol].GetReplicate(rowItem);
                        if (replicate != null)
                        {
                            point = point.ChangeReplicateName(replicate.Name);
                        }
                        points.Add(point);
                        iCol++;
                    }
                }
            }
            return new ClusterGraphResults(clusteredResults.RowDendrogramData?.DendrogramData, rowHeaders, columnGroups, points);
        }

        public Tuple<Clusterer, ClusteredReportResults, ReportColorScheme> GetClusterResultsTuple(ProgressHandler progressHandler)
        {
            var clusterer = Clusterer.CreateClusterer(progressHandler.CancellationToken, ClusteringSpec ?? ClusteringSpec.DEFAULT, ReportResults);
            if (clusterer == null)
            {
                return null;
            }

            var results = clusterer.GetClusteredResults(progressHandler);
            var colorScheme = ReportColorScheme.FromClusteredResults(progressHandler.CancellationToken, results);
            return Tuple.Create(clusterer, results, colorScheme);
        }
    }
}
