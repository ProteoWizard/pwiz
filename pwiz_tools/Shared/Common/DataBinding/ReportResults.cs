using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis.Clustering;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    public class ReportResults : Immutable
    {
        public static readonly ReportResults EMPTY = new ReportResults(ImmutableList.Empty<RowItem>(), ItemProperties.EMPTY);
        public ReportResults(IEnumerable<RowItem> rowItems, IEnumerable<DataPropertyDescriptor> itemProperties) 
            : this(rowItems, new PivotedProperties(ItemProperties.FromList(itemProperties)))
        {
        }

        public ReportResults(IEnumerable<RowItem> rowItems, PivotedProperties pivotedProperties) 
            : this(rowItems, pivotedProperties, null, null)
        {
        }

        public ReportResults(IEnumerable<RowItem> rowItems, PivotedProperties pivotedProperties,
            DendrogramData rowDendrogramData, IEnumerable<DendrogramData> columnDendrogramDatas)
        {
            RowItems = ImmutableList.ValueOf(rowItems);
            if (rowDendrogramData != null && rowDendrogramData.LeafCount != RowItems.Count)
            {
                throw new ArgumentException(@"Row count does not match", nameof(rowDendrogramData));
            }

            RowDendrogramData = rowDendrogramData;

            PivotedProperties = pivotedProperties;
            var colDendrograms = ImmutableList.ValueOf(columnDendrogramDatas);
            if (colDendrograms != null)
            {
                if (colDendrograms.Count != PivotedProperties.SeriesGroups.Count)
                {
                    throw new ArgumentException(@"Number of groups does not match", nameof(colDendrograms));
                }

                for (int i = 0; i < colDendrograms.Count; i++)
                {
                    var dendrogram = colDendrograms[i];
                    if (dendrogram != null && PivotedProperties.SeriesGroups[i].First().PivotKeys.Count !=
                        dendrogram.LeafCount)
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

        public ImmutableList<RowItem> RowItems { get; private set; }

        public ReportResults ChangeRowItems(IEnumerable<RowItem> rowItems)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.RowItems = ImmutableList.ValueOf(rowItems);
                RowDendrogramData = null;
            });
        }

        public int RowCount
        {
            get { return RowItems.Count; }
        }
        public ItemProperties ItemProperties
        {
            get { return PivotedProperties.ItemProperties; }
        }
        public PivotedProperties PivotedProperties { get; private set; }

        public DendrogramData RowDendrogramData { get; private set; }

        public ImmutableList<DendrogramData> ColumnGroupDendrogramDatas { get; private set; }
    }
}
