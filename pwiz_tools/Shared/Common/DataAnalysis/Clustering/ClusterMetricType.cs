using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Properties;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataAnalysis.Clustering
{
    public class ClusterMetricType : LabeledValues<string>
    {
        public static readonly ClusterMetricType CHEBYSHEV =
            new ClusterMetricType(@"chebyshev", () => Resources.ClusterMetricType_CHEBYSHEV_Chebyshev_distance__L_inf_norm_, 0);
        public static readonly ClusterMetricType MANHATTAN =
            new ClusterMetricType(@"manhattan", ()=>Resources.ClusterMetricType_MANHATTAN_City_block_distance__L_1_norm_, 1);
        public static readonly ClusterMetricType EUCLIDEAN =
            new ClusterMetricType(@"euclidean", ()=>Resources.ClusterMetricType_EUCLIDEAN_Euclidean_distance__L2_norm_, 2);
        public static readonly ClusterMetricType PEARSON = 
            new ClusterMetricType(@"pearson", ()=>Resources.ClusterMetricType_PEARSON_Pearson_correlation, 10);
        public static readonly ClusterMetricType DEFAULT = EUCLIDEAN;

        public ClusterMetricType(string name, Func<string> getLabel, int algLibValue) : base(name, getLabel)
        {
            AlgLibValue = algLibValue;
        }

        public int AlgLibValue { get; }

        public override string ToString()
        {
            return Label;
        }

        public static IEnumerable<ClusterMetricType> ALL
        {
            get
            {
                yield return EUCLIDEAN;
                yield return MANHATTAN;
                yield return CHEBYSHEV;
                yield return PEARSON;
            }
        }

        public static ClusterMetricType FromName(string name)
        {
            return ALL.FirstOrDefault(metric => metric.Name == name);
        }
    }
}
