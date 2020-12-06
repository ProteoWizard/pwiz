using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding.Clustering
{
    public class ClusteredReportResults : ReportResults
    {
        public new static readonly ClusteredReportResults EMPTY = new ClusteredReportResults(
            ImmutableList.Empty<RowItem>(),
            ClusteredProperties.EMPTY, null, null);

        public ClusteredReportResults(IEnumerable<RowItem> rowItems, ClusteredProperties clusteredProperties,
            CaptionedDendrogramData rowDendrogramData, IEnumerable<CaptionedDendrogramData> columnDendrogramDatas) 
            : base(rowItems, clusteredProperties.PivotedProperties.ItemProperties)
        {
            if (rowDendrogramData != null && rowDendrogramData.DendrogramData.LeafCount != RowItems.Count)
            {
                throw new ArgumentException(@"Row count does not match", nameof(rowDendrogramData));
            }

            RowDendrogramData = rowDendrogramData;

            ClusteredProperties = clusteredProperties;
            var colDendrograms = ImmutableList.ValueOf(columnDendrogramDatas);
            if (colDendrograms != null)
            {
                if (colDendrograms.Count != clusteredProperties.PivotedProperties.SeriesGroups.Count)
                {
                    throw new ArgumentException(@"Number of groups does not match", nameof(colDendrograms));
                }

                for (int i = 0; i < colDendrograms.Count; i++)
                {
                    var dendrogram = colDendrograms[i];
                    if (dendrogram != null && clusteredProperties.PivotedProperties.SeriesGroups[i].PivotKeys.Count != dendrogram.LeafCount)
                    {
                        throw new ArgumentException(@"Wrong number of columns", nameof(colDendrograms));
                    }
                }

                if (colDendrograms.Any(d => null != d))
                {
                    ColumnGroupDendrogramDatas = colDendrograms;
                }
            }
        }

        public PivotedProperties PivotedProperties {get{return ClusteredProperties.PivotedProperties;}}
        public ClusteredProperties ClusteredProperties { get; private set; }

        public CaptionedDendrogramData RowDendrogramData { get; private set; }

        public ImmutableList<CaptionedDendrogramData> ColumnGroupDendrogramDatas { get; private set; }
    }
}
