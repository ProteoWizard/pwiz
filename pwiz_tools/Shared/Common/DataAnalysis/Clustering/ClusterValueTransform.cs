using System;
using System.Collections.Generic;

namespace pwiz.Common.DataAnalysis.Clustering
{
    public class ClusterValueTransform
    {
        public static readonly ClusterValueTransform UNCHANGED = new ClusterValueTransform(@"none", ()=>"None");
        public static readonly ClusterValueTransform BOOLEAN = new ClusterValueTransform(@"boolean", ()=>"Boolean");
        public static readonly ClusterValueTransform ZSCORE = new ClusterValueTransform(@"zscore", ()=>"Z-Score");
        public static readonly ClusterValueTransform LOGARITHM = new ClusterValueTransform(@"logarithm", ()=>"Logarithm");
        private Func<string> _getLabelFunc;

        public static IEnumerable<ClusterValueTransform> All
        {
            get
            {
                yield return UNCHANGED;
                yield return BOOLEAN;
                yield return ZSCORE;
                yield return LOGARITHM;
            }
        }
        public ClusterValueTransform(string name, Func<string> getLabelFunc)
        {
            Name = name;
            _getLabelFunc = getLabelFunc;
        }

        public string Name { get; }

        public string Label
        {
            get
            {
                return _getLabelFunc();
            }
        }

        public override string ToString()
        {
            return Label;
        }
    }
}
