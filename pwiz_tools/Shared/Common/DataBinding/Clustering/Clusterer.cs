using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis.Clustering;

namespace pwiz.Common.DataBinding.Clustering
{
    public class Clusterer
    {
        public Clusterer(IEnumerable<RowItem> rowItems, ClusteredProperties properties, ClusterMetricType distanceMetric)
        {
            RowItems = ImmutableList.ValueOf(rowItems);
            Properties = properties;
            RowHeaderLevels = ImmutableList.ValueOf(Properties.RowHeaders);
            DistanceMetric = distanceMetric;
        }
        public ImmutableList<RowItem> RowItems { get; }

        public ItemProperties ItemProperties => Properties.PivotedProperties.ItemProperties;
        public ClusteredProperties Properties { get; }

        public ClusterMetricType DistanceMetric { get; }

        public ImmutableList<DataPropertyDescriptor> RowHeaderLevels { get; private set; }

        public ClusterDataSet<RowItem, int>.DataFrame MakeDataFrame(PivotedProperties.Series series, ClusterRole.Transform clusterValueTransform)
        {
            var rows = new List<List<double>>();
            var propertyDescriptors = series.PropertyIndexes.Select(i => ItemProperties[i]).ToList();
            foreach (var rowItem in RowItems)
            {
                var rawValues = propertyDescriptors.Select(pd => pd.GetValue(rowItem)).ToList();
                rows.Add(clusterValueTransform.TransformRow(rawValues).Select(value=>value??clusterValueTransform.ValueForNull).ToList());

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
            foreach (var propertyIndex in series.PropertyIndexes)
            {
                var propertyDescriptor = ItemProperties[propertyIndex];
                values.Add(rowItems.Select(propertyDescriptor.GetValue).FirstOrDefault(v => null != v));
            }
            return new CaptionedValues(series.SeriesCaption, series.PropertyType, values);
        }

        private CaptionedValues GetDefaultColumnHeader(IList<RowItem> rowItems,
            PivotedProperties.SeriesGroup seriesGroup)
        {
            return new CaptionedValues(CaptionComponentList.EMPTY, typeof(IColumnCaption), seriesGroup.PivotCaptions);
        }

        private ClusterDataSet<RowItem, int>.Results GetClusterDataSetResults()
        {
            var rowDataFrames = ImmutableList.ValueOf(MakeRowDataFrames());
            var dataFrameGroups =
                Properties.PivotedProperties.SeriesGroups.Select(group =>
                    ImmutableList.ValueOf(MakeDataFrames(group.SeriesList)));
            dataFrameGroups = dataFrameGroups.Prepend(rowDataFrames);
            var clusterDataSet = new ClusterDataSet<RowItem, int>(RowItems, dataFrameGroups);
            return clusterDataSet.PerformClustering(rowDataFrames.Any() || RowHeaderLevels.Any());
        }

        public PcaResults<int> PerformPca(PivotedProperties.SeriesGroup seriesGroup, int maxLevels)
        {
            var dataSet = new ClusterDataSet<RowItem, int>(RowItems, ImmutableList.Singleton(ImmutableList.ValueOf(MakeDataFrames(seriesGroup.SeriesList))));
            return dataSet.PerformPcaOnColumnGroups(maxLevels).FirstOrDefault();
        }

        public ClusteredReportResults GetClusteredResults()
        {
            var clusterResults = GetClusterDataSetResults();
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

        public static Clusterer CreateClusterer(ClusteringSpec clusteringSpec, ReportResults reportResults)
        {
            var pivotedPropertySet = new PivotedProperties(reportResults.ItemProperties);
            pivotedPropertySet = pivotedPropertySet.ChangeSeriesGroups(pivotedPropertySet.CreateSeriesGroups()).ReorderItemProperties();
            var clusteredProperties = ClusteredProperties.FromClusteringSpec(clusteringSpec, pivotedPropertySet);

            if (!clusteredProperties.RowValues.Any() && !clusteredProperties.ColumnValues.Any())
            {
                clusteringSpec = ClusteringSpec.GetDefaultClusteringSpec(reportResults, pivotedPropertySet);
                if (clusteringSpec == null)
                {
                    return null;
                }
                clusteredProperties = ClusteredProperties.FromClusteringSpec(clusteringSpec, pivotedPropertySet);
            }

            return new Clusterer(reportResults.RowItems, clusteredProperties, ClusterMetricType.FromName(clusteringSpec.DistanceMetric) ?? ClusterMetricType.DEFAULT);
        }

        public static ReportResults PerformClustering(ClusteringSpec clusteringSpec, ReportResults reportResults)
        {
            return CreateClusterer(clusteringSpec, reportResults)?.GetClusteredResults();
        }
    }
}
