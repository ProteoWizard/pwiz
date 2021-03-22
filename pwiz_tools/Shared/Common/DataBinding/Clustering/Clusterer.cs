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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis.Clustering;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Clustering
{
    public class Clusterer
    {
        public const int MAX_CLUSTER_ROW_COUNT = 15000;

        public Clusterer(CancellationToken cancellationToken, IEnumerable<RowItem> rowItems, ClusteredProperties properties, ClusterMetricType distanceMetric)
        {
            CancellationToken = cancellationToken;
            RowItems = ImmutableList.ValueOf(rowItems);
            Properties = properties;
            RowHeaderLevels = ImmutableList.ValueOf(Properties.RowHeaders);
            DistanceMetric = distanceMetric;
        }

        public CancellationToken CancellationToken { get; private set; }
        public ImmutableList<RowItem> RowItems { get; }

        public ItemProperties ItemProperties => Properties.PivotedProperties.ItemProperties;
        public ClusteredProperties Properties { get; }

        public ClusterMetricType DistanceMetric { get; }

        public ImmutableList<DataPropertyDescriptor> RowHeaderLevels { get; private set; }

        public ClusterDataSet<RowItem, int>.DataFrame MakeDataFrame(PivotedProperties.Series series, ClusterRole.Transform clusterValueTransform)
        {
            var rows = new List<List<double>>();
            foreach (var rowItem in RowItems)
            {
                var rawValues = series.PropertyDescriptors.Select(pd => pd.GetValue(rowItem)).ToList();
                var valuesToAdd = clusterValueTransform.TransformRow(rawValues)
                    .Select(value => value ?? clusterValueTransform.ValueForNull).ToList();
#if DEBUG
                foreach (var value in valuesToAdd)
                {
                    if (double.IsNaN(value) || double.IsInfinity(value))
                    {
                        Debug.WriteLine(@"{0} is not a valid double",  value);
                    }
                }
#endif
                rows.Add(valuesToAdd);
            }
            var columnDatas = new List<ImmutableList<double>>();
            for (int i =0; i  < series.PropertyIndexes.Count; i++)
            {
                columnDatas.Add(ImmutableList.ValueOf(rows.Select(row=>row[i]).ToList()));
            }
            return new ClusterDataSet<RowItem, int>.DataFrame(Enumerable.Range(0, columnDatas.Count), columnDatas);
        }

        public IEnumerable<ClusterDataSet<RowItem, int>.DataFrame> MakeDataFrames(IEnumerable<PivotedProperties.Series> seriesList)
        {
            foreach (var series in seriesList)
            {
                ClusterRole.Transform transform = Properties.GetColumnRole(series) as ClusterRole.Transform;
                if (transform == null)
                {
                    continue;
                }
                yield return MakeDataFrame(series, transform);
            }
        }

        public IEnumerable<ClusterDataSet<RowItem, int>.DataFrame> MakeRowDataFrames()
        {
            foreach (var pd in Properties.PivotedProperties.UngroupedProperties)
            {
                CancellationToken.ThrowIfCancellationRequested();
                var transform = Properties.GetRowTransform(pd);
                if (transform != null)
                {
                    var rawValues = RowItems.Select(pd.GetValue);
                    var transformedValues = rawValues.Select(value =>
                        transform.TransformRow(new[] {value}).FirstOrDefault() ?? transform.ValueForNull);
                    var dataColumn = ImmutableList.ValueOf(transformedValues);
                    yield return new ClusterDataSet<RowItem, int>.DataFrame(new []{0}, new []{dataColumn});
                }
            }
        }

        private CaptionedValues GetRowHeaderLevel(IList<RowItem> rowItems,
            DataPropertyDescriptor dataPropertyDescriptor)
        {
            return new CaptionedValues(dataPropertyDescriptor.ColumnCaption, dataPropertyDescriptor.PropertyType, rowItems.Select(dataPropertyDescriptor.GetValue));
        }

        private CaptionedValues GetColumnHeaderLevel(IList<RowItem> rowItems, PivotedProperties.Series series)
        {
            var values = new List<object>();
            foreach (var propertyDescriptor in series.PropertyDescriptors)
            {
                values.Add(rowItems.Select(propertyDescriptor.GetValue).FirstOrDefault(v => null != v));
            }
            return new CaptionedValues(series.SeriesCaption, series.PropertyType, values);
        }

        private CaptionedValues GetDefaultColumnHeader(IList<RowItem> rowItems,
            PivotedProperties.SeriesGroup seriesGroup)
        {
            return new CaptionedValues(CaptionComponentList.EMPTY, typeof(IColumnCaption), seriesGroup.PivotCaptions);
        }

        private ClusterDataSet<RowItem, int> MakeClusterDataSet()
        {
            var rowDataFrames = ImmutableList.ValueOf(MakeRowDataFrames());
            var dataFrameGroups =
                Properties.PivotedProperties.SeriesGroups.Select(group =>
                    ImmutableList.ValueOf(MakeDataFrames(group.SeriesList)));
            dataFrameGroups = dataFrameGroups.Prepend(rowDataFrames);
            var clusterDataSet = new ClusterDataSet<RowItem, int>(RowItems, dataFrameGroups).ChangeDistanceMetric(DistanceMetric);
            return clusterDataSet;
        }

        private ClusterResults<RowItem, int> GetClusterDataSetResults(ProgressHandler progressHandler)
        {
            var clusterDataSet = MakeClusterDataSet();
            bool performRowClustering = RowHeaderLevels.Any() || Properties.PivotedProperties.UngroupedProperties
                .Any(p => null != Properties.GetRowTransform(p));
            if (clusterDataSet.RowCount > MAX_CLUSTER_ROW_COUNT)
            {
                performRowClustering = false;
            }
            return clusterDataSet.PerformClustering(performRowClustering, progressHandler);
        }

        public PcaResults<int> PerformPcaOnColumnGroup(PivotedProperties.SeriesGroup seriesGroup, int maxLevels)
        {
            ClusterDataSet<RowItem, int> dataSet = new ClusterDataSet<RowItem, int>(RowItems,
                ImmutableList.Singleton(ImmutableList.ValueOf(MakeDataFrames(seriesGroup.SeriesList))));
            return dataSet.PerformPcaOnColumnGroups(maxLevels).FirstOrDefault();
        }

        public PcaResults<RowItem> PerformPcaOnRows(int maxLevels)
        {
            var dataSet = MakeClusterDataSet();
            return dataSet.PerformPcaOnRows(maxLevels);
        }

        public ClusteredReportResults GetClusteredResults(ProgressHandler progressHandler)
        {
            var clusterResults = GetClusterDataSetResults(progressHandler);
            CaptionedDendrogramData rowDendrogramData = null;
            if (clusterResults.RowDendrogram != null)
            {
                var captionLevels = new List<CaptionedValues>();
                foreach (var headerLevel in RowHeaderLevels)
                {
                    captionLevels.Add(GetRowHeaderLevel(clusterResults.DataSet.RowLabels, headerLevel));
                }
                rowDendrogramData = new CaptionedDendrogramData(clusterResults.RowDendrogram, captionLevels);
            }

            List<CaptionedDendrogramData> columnDendrograms = new List<CaptionedDendrogramData>();
            int iResultGroup = 1;
            Debug.Assert(iResultGroup + Properties.PivotedProperties.SeriesGroups.Count == clusterResults.DataSet.DataFrameGroups.Count);
            foreach (var group in Properties.PivotedProperties.SeriesGroups)
            {
                var columnDendrogramData = clusterResults.ColumnGroupDendrograms[iResultGroup];
                CaptionedDendrogramData captionedDendrogramData = null;
                if (columnDendrogramData != null)
                {
                    var captionLevels = new List<CaptionedValues>();
                    foreach (var headerLevel in Properties.GetColumnHeaders(group))
                    {
                        captionLevels.Add(GetColumnHeaderLevel(clusterResults.DataSet.RowLabels, headerLevel));
                    }

                    if (captionLevels.Count == 0)
                    {
                        captionLevels.Add(GetDefaultColumnHeader(clusterResults.DataSet.RowLabels, group));
                    }
                    captionedDendrogramData = new CaptionedDendrogramData(columnDendrogramData, captionLevels);
                }
                columnDendrograms.Add(captionedDendrogramData);
                iResultGroup++;
            }

            if (columnDendrograms.All(v => null == v))
            {
                columnDendrograms = null;
            }
            var pivotedProperties = Properties.PivotedProperties.ReorderPivots(clusterResults.DataSet.DataFrameGroups
                .Skip(1)
                .Select(group => group.FirstOrDefault()?.ColumnHeaders).Cast<IList<int>>().ToList());
            pivotedProperties = pivotedProperties.ReorderItemProperties();
            return new ClusteredReportResults(clusterResults.DataSet.RowLabels, Properties.ReplacePivotedProperties(pivotedProperties), rowDendrogramData, columnDendrograms);
        }

        public static Clusterer CreateClusterer(CancellationToken cancellationToken, ClusteringSpec clusteringSpec, ReportResults reportResults)
        {
            var pivotedPropertySet = new PivotedProperties(reportResults.ItemProperties);
            pivotedPropertySet = pivotedPropertySet.ChangeSeriesGroups(pivotedPropertySet.CreateSeriesGroups()).ReorderItemProperties();
            var clusteredProperties = ClusteredProperties.FromClusteringSpec(clusteringSpec, pivotedPropertySet);

            if (!clusteredProperties.RowValues.Any() && !clusteredProperties.ColumnValues.Any())
            {
                clusteringSpec = ClusteringSpec.GetDefaultClusteringSpec(cancellationToken, reportResults, pivotedPropertySet);
                if (clusteringSpec == null)
                {
                    return null;
                }
                clusteredProperties = ClusteredProperties.FromClusteringSpec(clusteringSpec, pivotedPropertySet);
            }

            return new Clusterer(cancellationToken, reportResults.RowItems, clusteredProperties, ClusterMetricType.FromName(clusteringSpec.DistanceMetric) ?? ClusterMetricType.DEFAULT);
        }

        public static ReportResults PerformClustering(ProgressHandler progressHandler, ClusteringSpec clusteringSpec, ReportResults reportResults)
        {
            return CreateClusterer(progressHandler.CancellationToken, clusteringSpec, reportResults)?.GetClusteredResults(progressHandler);
        }
    }
}
