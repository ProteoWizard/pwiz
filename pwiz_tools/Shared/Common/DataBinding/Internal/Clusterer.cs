using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis.Clustering;
using pwiz.Common.DataBinding.Clustering;

namespace pwiz.Common.DataBinding.Internal
{
    public class Clusterer
    {
        public Clusterer(IEnumerable<RowItem> rowItems, ClusteredProperties properties)
        {
            RowItems = ImmutableList.ValueOf(rowItems);
            Properties = properties;
            RowHeaderLevels = ImmutableList.ValueOf(Properties.RowHeaders);
        }
        public ImmutableList<RowItem> RowItems { get; }

        public ItemProperties ItemProperties => Properties.PivotedProperties.ItemProperties;
        public ClusteredProperties Properties { get; }

        public ImmutableList<DataPropertyDescriptor> RowHeaderLevels { get; private set; }

        public ClusterDataSet<RowItem, int>.DataFrame MakeDataFrame(PivotedProperties.Series series, ClusterRole.Transform clusterValueTransform)
        {
            var columnDatas = new List<ImmutableList<double?>>();
            foreach (var propertyDescriptor in series.PropertyIndexes.Select(i => ItemProperties[i]))
            {
                var values = RowItems.Select(rowItem => ZScores.ToDouble(propertyDescriptor.GetValue(rowItem)));
                columnDatas.Add(ImmutableList.ValueOf(values));
            }
            return new ClusterDataSet<RowItem, int>.DataFrame(Enumerable.Range(0, columnDatas.Count), columnDatas, clusterValueTransform);
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
            var clusterDataSet = new ClusterDataSet<RowItem, int>(RowItems,
                Properties.PivotedProperties.SeriesGroups.Select(group => ImmutableList.ValueOf(MakeDataFrames(group.SeriesList))));
            return clusterDataSet.PerformClustering(RowHeaderLevels.Any());
        }

        public ClusterDataSet<IColumnCaption, IColumnCaption>.Results GetCaptionedClusterDataSetResults()
        {
            var rawResults = GetClusterDataSetResults();
            if (rawResults == null)
            {
                return null;
            }

            var rowItems = rawResults.DataSet.RowLabels;

            var newRowLabels = rowItems.Select(rowItem =>
                (IColumnCaption) CaptionComponentList.SpaceSeparate(RowHeaderLevels.Select(pd => pd.GetValue(rowItem))));
            var newColumnLabels = new List<ImmutableList<IColumnCaption>>();
            foreach (var seriesGroup in Properties.PivotedProperties.SeriesGroups)
            {
                var columnHeaders = seriesGroup.SeriesList
                    .Where(series => Properties.GetColumnRole(series) == ClusterRole.COLUMNHEADER);
                ImmutableList<IColumnCaption> captions;
                var parts = columnHeaders.Select(part => GetColumnHeaderLevel(rowItems, part)).ToList();
                if (parts.Any())
                {
                    captions = ImmutableList.ValueOf(Enumerable.Range(0, parts[0].ValueCount)
                        .Select(i => CaptionComponentList.SpaceSeparate(parts.Select(part => part.Values[i])))
                        .Cast<IColumnCaption>());
                }
                else
                {
                    captions = ImmutableList.ValueOf(GetDefaultColumnHeader(rowItems, seriesGroup).Values
                        .Cast<IColumnCaption>());
                }
                newColumnLabels.Add(captions);
            }

            return rawResults.ChangeLabels(newRowLabels, newColumnLabels);
        }

        public ReportResults GetClusteredResults()
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
            for (int iGroup = 0; iGroup < Properties.PivotedProperties.SeriesGroups.Count; iGroup++)
            {
                var columnDendrogramData = clusterResults.ColumnGroupDendrograms[iGroup];
                CaptionedDendrogramData captionedDendrogramData = null;
                if (columnDendrogramData != null)
                {
                    var group = Properties.PivotedProperties.SeriesGroups[iGroup];
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
            }

            if (columnDendrograms.All(v => null == v))
            {
                columnDendrograms = null;
            }
            var pivotedProperties = Properties.PivotedProperties.ReorderPivots(clusterResults.DataSet.DataFrameGroups
                .Select(group => group[0].ColumnHeaders).Cast<IList<int>>().ToList());
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

            return new Clusterer(reportResults.RowItems, clusteredProperties);
        }

        public static ReportResults PerformClustering(ClusteringSpec clusteringSpec, ReportResults reportResults)
        {
            return CreateClusterer(clusteringSpec, reportResults)?.GetClusteredResults();
        }
    }
}
