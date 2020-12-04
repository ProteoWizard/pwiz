using System;
using System.Collections.Generic;
using System.Linq;

namespace pwiz.Common.DataAnalysis.Clustering
{
    public class ClusterRole
    {
        public static readonly ClusterRole IGNORED = new ClusterRole("ignored", ()=>"Ignored");
        public static readonly ClusterRole ROWHEADER = new ClusterRole("rowheader", ()=>"Row Header");
        public static readonly ClusterRole COLUMNHEADER = new ClusterRole("columnheader", ()=>"Column Header");
        public static readonly Transform RAW = new SimpleTransform(@"raw", ()=>"Raw Value", value=>value, 0);
        public static readonly Transform BOOLEAN = new BooleanTransform();
        public static readonly Transform ZSCORE = new ZScore();
        public static readonly Transform LOGARITHM = new SimpleTransform(@"log10", ()=>"Log10", Math.Log, 0);
        private Func<string> _getLabelFunc;

        public static IEnumerable<ClusterRole> All
        {
            get
            {
                yield return IGNORED;
                yield return ROWHEADER;
                yield return COLUMNHEADER;
                yield return RAW;
                yield return BOOLEAN;
                yield return ZSCORE;
                yield return LOGARITHM;
            }
        }
        public ClusterRole(string name, Func<string> getLabelFunc)
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

        public virtual bool CanHandleDataType(Type type)
        {
            return true;
        }

        public override string ToString()
        {
            return Label;
        }

        private static IDictionary<string, ClusterRole> _valuesByName = All.ToDictionary(value => value.Name);
        public static ClusterRole FromName(string name)
        {
            ClusterRole result;
            _valuesByName.TryGetValue(name, out result);
            return result;
        }

        public abstract class Transform : ClusterRole
        {
            public Transform(string name, Func<string> getLabelFunc, double valueForNull) : base(name, getLabelFunc)
            {
                ValueForNull = valueForNull;
            }

            public abstract IEnumerable<double?> TransformRow(IEnumerable<object> values);

            public double ValueForNull { get; }
        }

        public class SimpleTransform : Transform
        {
            private Func<double, double> _transformFunc;

            public SimpleTransform(string name, Func<string> getLabelFunc, Func<double, double> transformFunc,
                double nullValue) : base(name, getLabelFunc, nullValue)
            {
                _transformFunc = transformFunc;
            }

            public override bool CanHandleDataType(Type type)
            {
                return ZScores.IsNumericType(type);
            }

            protected double? TransformValue(double? value)
            {
                if (value == null)
                {
                    return null;
                }

                return _transformFunc(value.Value);
            }

            public override IEnumerable<double?> TransformRow(IEnumerable<object> values)
            {
                return values.Select(value => TransformValue(ZScores.ToDouble(value)));
            }
        }

        private class BooleanTransform : Transform
        {
            public BooleanTransform() : base("boolean", () => "Boolean", 0)
            {

            }

            public override IEnumerable<double?> TransformRow(IEnumerable<object> values)
            {
                return values.Select(value => null == value ? (double?) null : 1);
            }
        }

        private class ZScore : Transform
        {
            public ZScore() : base("zscore", () => "Z-Score", 0)
            {

            }

            public override IEnumerable<double?> TransformRow(IEnumerable<object> values)
            {
                return ZScores.CalculateZScores(values);
            }
        }
    }
}
