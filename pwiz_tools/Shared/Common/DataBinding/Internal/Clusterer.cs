using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis.Clustering;

namespace pwiz.Common.DataBinding.Internal
{
    public class Clusterer
    {
        public Clusterer(IEnumerable<RowItem> rowItems, PivotedProperties pivotedProperties)
        {
            RowItems = ImmutableList.ValueOf(rowItems);
            PivotedProperties = pivotedProperties;
        }
        public ImmutableList<RowItem> RowItems { get; }

        public ItemProperties ItemProperties => PivotedProperties.ItemProperties;
        public PivotedProperties PivotedProperties { get; }

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
                PivotedProperties.SeriesGroups.Select(group => ImmutableList.ValueOf(MakeDataFrames(group))));
            var clusterResults = clusterDataSet.PerformClustering();
            var pivotedProperties = PivotedProperties.ReorderPivots(clusterResults.DataSet.DataFrameGroups
                .Select(group => group[0].ColumnHeaders).Cast<IList<int>>().ToList());
            return new ReportResults(clusterResults.DataSet.RowLabels, pivotedProperties, clusterResults.RowDendrogram, clusterResults.ColumnGroupDendrograms);
        }

        public static ReportResults PerformClustering(ReportResults reportResults)
        {
            var pivotedPropertySet = new PivotedProperties(reportResults.ItemProperties);
            var seriesGroups = pivotedPropertySet.CreateSeriesGroups()
                .Where(group => group.Any(series => ZScores.IsNumericType(series.PropertyType))).ToList();
            if (seriesGroups.Count == 0)
            {
                return null;
            }

            pivotedPropertySet = pivotedPropertySet.ChangeSeriesGroups(seriesGroups);
            var clusterer = new Clusterer(reportResults.RowItems, pivotedPropertySet);
            return clusterer.GetClusteredResults();
        }
    }
}
