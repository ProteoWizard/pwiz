using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataAnalysis.Clustering
{
    public class ClusterMetricType : LabeledValues<string>
    {
        public static readonly ClusterMetricType CHEBYSHEV =
            new ClusterMetricType(@"chebyshev", () => "Chebyshev distance (L-inf norm)", 0);
        public static readonly ClusterMetricType MANHATTAN =
            new ClusterMetricType(@"manhattan", ()=>"City block distance (L-1 norm)", 1);
        public static readonly ClusterMetricType EUCLIDEAN =
            new ClusterMetricType(@"euclidean", ()=>"Euclidean distance (L2 norm)", 2);
        public static readonly ClusterMetricType PEARSON = 
            new ClusterMetricType(@"pearson", ()=>"Pearson correlation", 10);

        private Func<string> _getLabelFunc;
        public ClusterMetricType(string name, Func<string> getLabel, int algLibValue) : base(name, getLabel)
        {
            Name = name;
            _getLabelFunc = getLabel;
            AlgLibValue = algLibValue;
        }

        public int AlgLibValue { get; }

        public string Name { get; }
        public string Label
        {
            get { return _getLabelFunc(); }
        }

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
    }
}
