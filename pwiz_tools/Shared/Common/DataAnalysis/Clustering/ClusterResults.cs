using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pwiz.Common.Collections;

namespace pwiz.Common.DataAnalysis.Clustering
{
    public class ClusterResults<TRow, TColumn>
    {
        public ClusterResults(ClusterDataSet<TRow, TColumn> dataSet, DendrogramData rowDendrogram,
            ImmutableList<DendrogramData> columnDendrograms)
        {
            DataSet = dataSet;
            RowDendrogram = rowDendrogram;
            ColumnGroupDendrograms = columnDendrograms;
        }
        public ClusterDataSet<TRow, TColumn> DataSet { get; private set; }
        public DendrogramData RowDendrogram { get; private set; }
        public ImmutableList<DendrogramData> ColumnGroupDendrograms { get; private set; }
    }
}
