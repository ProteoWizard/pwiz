/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Linq;
using System.Threading;
using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis.Clustering;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Clustering;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;

namespace pwiz.Skyline.Controls.Clustering
{
    public class ClusterGraphResults
    {
        public ClusterGraphResults(DendrogramData rowDendrogramData, 
            IEnumerable<Header> rowHeaders,
            IEnumerable<ColumnGroup> columnGroups,
            IEnumerable<Point> points)
        {
            RowDendrogramData = rowDendrogramData;
            RowHeaders = ImmutableList.ValueOf(rowHeaders);
            ColumnGroups = ImmutableList.ValueOf(columnGroups);
            Points = ImmutableList.ValueOf(points);
        }

        public DendrogramData RowDendrogramData { get; private set; }
        public ImmutableList<Header> RowHeaders { get; private set; }
        public ImmutableList<ColumnGroup> ColumnGroups { get; private set; }
        public ImmutableList<Point> Points { get; private set; }


        public int RowCount
        {
            get { return RowHeaders.Count; }
        }
        public class Header
        {
            public Header(string caption, IEnumerable<Color> colors)
            {
                Caption = caption;
                Colors = ImmutableList.ValueOf(colors);
            }
            public string Caption { get; private set; }
            public ImmutableList<Color> Colors { get; private set; }
        }

        public class ColumnGroup
        {
            public ColumnGroup(DendrogramData dendrogramData, IEnumerable<Header> headers)
            {
                DendrogramData = dendrogramData;
                Headers = ImmutableList.ValueOf(headers);
                if (DendrogramData.LeafCount != Headers.Count)
                {
                    throw new ArgumentException(@"Wrong number of headers", nameof(headers));
                }
            }
            public DendrogramData DendrogramData { get; private set; }
            public ImmutableList<Header> Headers { get; private set; }
        }

        public class Point : Immutable
        {
            public Point(int rowIndex, int columnIndex, Color? color)
            {
                RowIndex = rowIndex;
                ColumnIndex = columnIndex;
                Color = color;
            }
            public int ColumnIndex { get; set; }
            public int RowIndex { get; set; }
            public Color? Color { get; private set; }
            public IdentityPath IdentityPath { get; private set; }

            public Point ChangeIdentityPath(IdentityPath identityPath)
            {
                return ChangeProp(ImClone(this), im => im.IdentityPath = identityPath);
            }
            public string ReplicateName { get; private set; }

            public Point ChangeReplicateName(string replicateName)
            {
                return ChangeProp(ImClone(this), im => im.ReplicateName = replicateName);
            }
        }

        public static ClusterGraphResults GetClusterGraphResults(CancellationToken cancellationToken,
            Action<int> updateProgressAction, DataSchemaLocalizer dataSchemaLocalizer, ReportResults reportResults, ClusteringSpec clusteringSpec)
        {
            var tuple = GetClusterResultsTuple(cancellationToken, updateProgressAction, reportResults, clusteringSpec);
            if (tuple == null)
            {
                return null;
            }
            var clusteredResults = tuple.Item2;
            var colorScheme = tuple.Item3;
            var points = new List<Point>();
            var rowHeaders = new List<Header>();
            var columnValues = new List<PivotedProperties.Series>();
            var columnGroups = new List<ColumnGroup>();
            var cellLocators = new List<CellLocator>();
            for (int iGroup = 0; iGroup < clusteredResults.PivotedProperties.SeriesGroups.Count; iGroup++)
            {
                var group = clusteredResults.PivotedProperties.SeriesGroups[iGroup];

                var groupColumnHeaders = clusteredResults.ClusteredProperties.GetColumnHeaders(group).ToList();
                var groupHeaders = new List<Header>();
                for (int iPivotKey = 0; iPivotKey < group.PivotKeys.Count; iPivotKey++)
                {
                    var colors = new List<Color>();
                    foreach (var series in groupColumnHeaders)
                    {
                        var pd = series.PropertyDescriptors[iPivotKey];
                        colors.Add(colorScheme.GetColumnColor(pd) ?? Color.Transparent);
                    }
                    groupHeaders.Add(new Header(group.PivotCaptions[iPivotKey].GetCaption(dataSchemaLocalizer), colors));
                }
                foreach (var series in group.SeriesList)
                {
                    var transform = clusteredResults.ClusteredProperties.GetColumnRole(series) as ClusterRole.Transform;
                    if (transform == null)
                    {
                        continue;
                    }
                    columnValues.Add(series);
                    columnGroups.Add(new ColumnGroup(
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
                rowHeaders.Add(new Header(rowCaption, rowColors));
                int iCol = 0;
                foreach (var series in columnValues)
                {
                    foreach (var color in colorScheme.GetSeriesColors(series, rowItem))
                    {
                        var point = new Point(iRow, iCol, color);
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

        private static Tuple<Clusterer, ClusteredReportResults, ReportColorScheme> GetClusterResultsTuple(
            CancellationToken cancellationToken,
            Action<int> updateProgressAction, ReportResults reportResults, ClusteringSpec clusteringSpec)
        {
            var clusterer = Clusterer.CreateClusterer(cancellationToken, clusteringSpec ?? ClusteringSpec.DEFAULT, reportResults);
            if (clusterer == null)
            {
                return null;
            }

            var results = clusterer.GetClusteredResults();
            var colorScheme = ReportColorScheme.FromClusteredResults(cancellationToken, results);
            return Tuple.Create(clusterer, results, colorScheme);


        }
    }
}
