using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.Clustering;

namespace pwiz.Common.DataAnalysis.Clustering
{
    public class ClusterResults
    {
        public ClusterResults(ClusterDataSet dataSet, DendrogramData rowDendrogram,
            ImmutableList<DendrogramData> columnDendrograms)
        {
            DataSet = dataSet;
            RowDendrogram = rowDendrogram;
            ColumnGroupDendrograms = columnDendrograms;
        }
        public ClusterDataSet DataSet { get; private set; }
        public DendrogramData RowDendrogram { get; private set; }
        public ImmutableList<DendrogramData> ColumnGroupDendrograms { get; private set; }
    }
}
