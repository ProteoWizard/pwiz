using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis.Clustering;
using pwiz.Common.DataBinding.Layout;

namespace pwiz.Common.DataBinding.Internal
{
    public class Clusterer
    {
        public Clusterer(ClusteringSpec clusteringSpec, IEnumerable<RowItem> rowItems, PivotedProperties pivotedProperties)
        {
            ClusteringSpec = clusteringSpec;
            RowItems = ImmutableList.ValueOf(rowItems);
            PivotedProperties = pivotedProperties;
        }
        public ImmutableList<RowItem> RowItems { get; }

        public ItemProperties ItemProperties => PivotedProperties.ItemProperties;
        public PivotedProperties PivotedProperties { get; }

        public ClusteringSpec ClusteringSpec { get; }

        public ClusterDataSet<RowItem, int>.DataFrame MakeDataFrame(PivotedProperties.Series series)
        {
            var columnDatas = new List<ImmutableList<double?>>();
            foreach (var propertyDescriptor in series.PropertyIndexes.Select(i => ItemProperties[i]))
            {
                var values = RowItems.Select(rowItem => ZScores.ToDouble(propertyDescriptor.GetValue(rowItem)));
                columnDatas.Add(ImmutableList.ValueOf(values));
            }
            return new ClusterDataSet<RowItem, int>.DataFrame(Enumerable.Range(0, columnDatas.Count), columnDatas);
        }

        public IEnumerable<ClusterDataSet<RowItem, int>.DataFrame> MakeDataFrames(IEnumerable<PivotedProperties.Series> seriesList)
        {
            return seriesList.Where(series => ZScores.IsNumericType(series.PropertyType))
                .Select(MakeDataFrame);
        }

        public ReportResults GetClusteredResults()
        {
            var clusterDataSet = new ClusterDataSet<RowItem, int>(RowItems,
                PivotedProperties.SeriesGroups.Select(group => ImmutableList.ValueOf(MakeDataFrames(group.SeriesList))));
            var clusterResults = clusterDataSet.PerformClustering(ClusteringSpec.ClusterRows, ClusteringSpec.ClusterColumns);
            var pivotedProperties = PivotedProperties.ReorderPivots(clusterResults.DataSet.DataFrameGroups
                .Select(group => group[0].ColumnHeaders).Cast<IList<int>>().ToList());
            pivotedProperties = pivotedProperties.ReorderItemProperties();
            return new ReportResults(clusterResults.DataSet.RowLabels, pivotedProperties, clusterResults.RowDendrogram, clusterResults.ColumnGroupDendrograms);
        }

        public static ReportResults PerformClustering(ClusteringSpec clusteringSpec, ReportResults reportResults)
        {
            var pivotedPropertySet = new PivotedProperties(reportResults.ItemProperties);
            var seriesGroups = pivotedPropertySet.CreateSeriesGroups()
                .Where(group => group.SeriesList.Any(series => ZScores.IsNumericType(series.PropertyType))).ToList();
            if (seriesGroups.Count == 0)
            {
                return null;
            }

            if (!clusteringSpec.ClusterColumns && !clusteringSpec.ClusterRows)
            {
                return new ReportResults(reportResults.RowItems, pivotedPropertySet);
            }

            pivotedPropertySet = pivotedPropertySet.ChangeSeriesGroups(seriesGroups).ReorderItemProperties();
            var clusterer = new Clusterer(clusteringSpec, reportResults.RowItems, pivotedPropertySet);
            return clusterer.GetClusteredResults();
        }
    }
}
