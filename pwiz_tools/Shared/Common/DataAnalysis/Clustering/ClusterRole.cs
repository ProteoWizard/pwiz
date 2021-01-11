/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
using pwiz.Common.Properties;

namespace pwiz.Common.DataAnalysis.Clustering
{
    public class ClusterRole
    {
        public static readonly ClusterRole IGNORED = new ClusterRole(@"ignored", ()=>Resources.ClusterRole_IGNORED_Ignored);
        public static readonly ClusterRole ROWHEADER = new ClusterRole(@"rowheader", ()=>Resources.ClusterRole_ROWHEADER_Row_Header);
        public static readonly ClusterRole COLUMNHEADER = new ClusterRole(@"columnheader", ()=>Resources.ClusterRole_COLUMNHEADER_Column_Header);
        public static readonly Transform RAW = new SimpleTransform(@"raw", ()=>Resources.ClusterRole_RAW_Raw_Value, value=>value, 0);
        public static readonly Transform BOOLEAN = new BooleanTransform();
        public static readonly Transform ZSCORE = new ZScore();
        public static readonly Transform LOGARITHM = new SimpleTransform(@"log10", ()=>Resources.ClusterRole_LOGARITHM_Log10, Math.Log, 0);
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
                return IsNumericType(type);
            }

            protected double? TransformValue(double? value)
            {
                if (value == null)
                {
                    return null;
                }

                var transformedValue = _transformFunc(value.Value);
                if (IsValidDouble(transformedValue))
                {
                    return transformedValue;
                }

                return null;
            }

            public override IEnumerable<double?> TransformRow(IEnumerable<object> values)
            {
                return values.Select(value => TransformValue(ToDouble(value)));
            }
        }

        private class BooleanTransform : Transform
        {
            public BooleanTransform() : base(@"boolean", () => Resources.BooleanTransform_BooleanTransform_Boolean, 0)
            {

            }

            public override IEnumerable<double?> TransformRow(IEnumerable<object> values)
            {
                return values.Select(value => null == value ? (double?) null : 1);
            }
        }

        public static bool IsNumericType(Type type)
        {
            return type == typeof(double) || type == typeof(double?) || type == typeof(float) || type == typeof(float?);
        }

        public static double? ToDouble(object value)
        {
            if (value is double doubleValue)
            {
                return doubleValue;
            }

            if (value is float floatValue)
            {
                return floatValue;
            }

            return null;
        }

        public static bool IsValidDouble(double? value)
        {
            return value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value);
        }

        private class ZScore : Transform
        {
            public ZScore() : base(@"zscore", () => Resources.ZScore_ZScore_Z_Score, 0)
            {

            }

            public override IEnumerable<double?> TransformRow(IEnumerable<object> values)
            {
                var doubleValuesList = values.Select(ToDouble).ToList();
                var validValues = doubleValuesList.Where(IsValidDouble).ToList();
                double stdDev = 0;
                if (validValues.Count > 1)
                {
                    stdDev = validValues.StandardDeviation();
                }
                if (stdDev == 0)
                {
                    return doubleValuesList.Select(val => val.HasValue ? (double?)0 : null);
                }

                var mean = validValues.Mean();
                return doubleValuesList.Select(val => val.HasValue && IsValidDouble(val.Value) ? (double?)(val.Value - mean) / stdDev : null);
            }

            public override bool CanHandleDataType(Type type)
            {
                return IsNumericType(type);
            }
        }
    }
}
