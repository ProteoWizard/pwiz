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
            TransformsByColumnRef = ClusteringSpec.ToValueTransformDictionary();
            RowHeaderLevels = ImmutableList.ValueOf(pivotedProperties.UngroupedProperties.Where(pd =>
                ClusterRole.ROWHEADER == GetRole(ClusteringSpec.ColumnRef.FromPropertyDescriptor(pd))));
            var columnHeaderLevels = new List<ImmutableList<PivotedProperties.Series>>();
            foreach (var group in pivotedProperties.SeriesGroups)
            {
                var headers = group.SeriesList.Where(series =>
                    ClusterRole.COLUMNHEADER == GetRole(ClusteringSpec.ColumnRef.FromPivotedPropertySeries(series)));
                columnHeaderLevels.Add(ImmutableList.ValueOf(headers));
            }
            ColumnHeaderLevels = ImmutableList.ValueOf(columnHeaderLevels);
        }
        public ImmutableList<RowItem> RowItems { get; }

        public ItemProperties ItemProperties => PivotedProperties.ItemProperties;
        public PivotedProperties PivotedProperties { get; }

        public ImmutableList<DataPropertyDescriptor> RowHeaderLevels { get; private set; }

        public ImmutableList<ImmutableList<PivotedProperties.Series>> ColumnHeaderLevels { get; private set; }

        public IDictionary<ClusteringSpec.ColumnRef, ClusterRole> TransformsByColumnRef { get; }

        private ClusterRole GetRole(ClusteringSpec.ColumnRef columnRef)
        {
            if (columnRef == null)
            {
                return null;
            }
            TransformsByColumnRef.TryGetValue(columnRef, out ClusterRole role);
            return role;
        }

        public ClusteringSpec ClusteringSpec { get; }

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
                var columnRef = ClusteringSpec.ColumnRef.FromPivotedPropertySeries(series);
                if (columnRef == null)
                {
                    continue;
                }

                ClusterRole.Transform transform;
                if (TransformsByColumnRef.TryGetValue(columnRef, out ClusterRole role))
                {
                    transform = role as ClusterRole.Transform;
                    if (transform == null)
                    {
                        continue;
                    }
                }
                else
                {
                    transform = ZScores.IsNumericType(series.PropertyType)
                        ? ClusterRole.ZSCORE
                        : ClusterRole.BOOLEAN;
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
                PivotedProperties.SeriesGroups.Select(group => ImmutableList.ValueOf(MakeDataFrames(group.SeriesList))));
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
            for (int iGroup = 0; iGroup < ColumnHeaderLevels.Count; iGroup++)
            {
                ImmutableList<IColumnCaption> captions;
                var parts = ColumnHeaderLevels[iGroup].Select(part => GetColumnHeaderLevel(rowItems, part)).ToList();
                if (parts.Any())
                {
                    captions = ImmutableList.ValueOf(Enumerable.Range(0, parts[0].ValueCount)
                        .Select(i => CaptionComponentList.SpaceSeparate(parts.Select(part => part.Values[i])))
                        .Cast<IColumnCaption>());
                }
                else
                {
                    captions = ImmutableList.ValueOf(GetDefaultColumnHeader(rowItems, PivotedProperties.SeriesGroups[iGroup]).Values
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
            for (int iGroup = 0; iGroup < PivotedProperties.SeriesGroups.Count; iGroup++)
            {
                var columnDendrogramData = clusterResults.ColumnGroupDendrograms[iGroup];
                CaptionedDendrogramData captionedDendrogramData = null;
                if (columnDendrogramData != null)
                {
                    var captionLevels = new List<CaptionedValues>();
                    foreach (var headerLevel in ColumnHeaderLevels[iGroup])
                    {
                        captionLevels.Add(GetColumnHeaderLevel(clusterResults.DataSet.RowLabels, headerLevel));
                    }

                    if (captionLevels.Count == 0)
                    {
                        captionLevels.Add(GetDefaultColumnHeader(clusterResults.DataSet.RowLabels, PivotedProperties.SeriesGroups[iGroup]));
                    }
                    captionedDendrogramData = new CaptionedDendrogramData(columnDendrogramData, captionLevels);
                }
                columnDendrograms.Add(captionedDendrogramData);
            }

            if (columnDendrograms.All(v => null == v))
            {
                columnDendrograms = null;
            }
            var pivotedProperties = PivotedProperties.ReorderPivots(clusterResults.DataSet.DataFrameGroups
                .Select(group => group[0].ColumnHeaders).Cast<IList<int>>().ToList());
            pivotedProperties = pivotedProperties.ReorderItemProperties();
            return new ReportResults(clusterResults.DataSet.RowLabels, pivotedProperties, rowDendrogramData, columnDendrograms);
        }

        public static Clusterer CreateClusterer(ClusteringSpec clusteringSpec, ReportResults reportResults)
        {
            var pivotedPropertySet = new PivotedProperties(reportResults.ItemProperties);
            pivotedPropertySet = pivotedPropertySet.ChangeSeriesGroups(pivotedPropertySet.CreateSeriesGroups()).ReorderItemProperties();
            if (!clusteringSpec.Values.Any())
            {
                clusteringSpec = ClusteringSpec.GetDefaultClusteringSpec(reportResults, pivotedPropertySet);
                if (clusteringSpec == null)
                {
                    return null;
                }
            }

            return new Clusterer(clusteringSpec, reportResults.RowItems, pivotedPropertySet);
        }

        public static ReportResults PerformClustering(ClusteringSpec clusteringSpec, ReportResults reportResults)
        {
            return CreateClusterer(clusteringSpec, reportResults)?.GetClusteredResults();
        }
    }
}
