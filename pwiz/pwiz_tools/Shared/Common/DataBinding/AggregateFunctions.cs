/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace pwiz.Common.DataBinding
{
    public static class AggregateFunctions
    {
        private static List<IAggregateFunction> _aggregateFunctionList = new List<IAggregateFunction>();
        private static IDictionary<string, int> _aggregateFunctionIndexesByName = new Dictionary<string, int>();

        public static readonly IAggregateFunction GroupBy = new AggregateFunctionImpl("groupby", "Group By", "",
                                                                                      Properties.Resources.agg_groupby,
                                                                                      null, cd => cd.PropertyType);
        public static readonly IAggregateFunction Sum = new AggregateFunctionImpl("sum", "Sum", "Sum of ",
            Properties.Resources.agg_sum, AggSum, cd => IsNumberType(cd) ? typeof (double) : null);
        public static readonly IAggregateFunction Mean = new AggregateFunctionImpl("mean", "Mean", "Average of ",
            Properties.Resources.agg_mean, AggMean, cd => IsNumberType(cd) ? typeof(double) : null);
        public static readonly IAggregateFunction Count = new AggregateFunctionImpl("count", "Count", "Count of ",
            Properties.Resources.agg_count, AggCount, cd => typeof (int));

        public static readonly IAggregateFunction Minimum = new AggregateFunctionImpl("min", "Minimum", "Min of ",
            Properties.Resources.agg_min, AggMin, cd => cd.PropertyType);

        public static readonly IAggregateFunction Maximum = new AggregateFunctionImpl("max", "Maximum", "Max of ",
            Properties.Resources.agg_max, AggMax, cd => cd.PropertyType);

        public static readonly IAggregateFunction StdDev = new AggregateFunctionImpl("stddev", "Standard Deviation", "StdDev of ",
            Properties.Resources.agg_stddev, AggStdDev, cd => IsNumberType(cd) ? typeof (double) : null);

        static AggregateFunctions()
        {
            AddAggregateFunction(GroupBy);
            AddAggregateFunction(Sum);
            AddAggregateFunction(Mean);
            AddAggregateFunction(Minimum);
            AddAggregateFunction(Maximum);
            AddAggregateFunction(Count);
            AddAggregateFunction(StdDev);
            // TODO(nicksh): StdErr, Variance, Median
        }
        static void AddAggregateFunction(IAggregateFunction aggregateFunction)
        {
            _aggregateFunctionIndexesByName.Add(aggregateFunction.Name, _aggregateFunctionList.Count);
            _aggregateFunctionList.Add(aggregateFunction);
        }

        public static int GetImageIndex(IAggregateFunction aggregateFunction)
        {
            if (aggregateFunction == null)
            {
                return -1;
            }
            int result;
            if (!_aggregateFunctionIndexesByName.TryGetValue(aggregateFunction.Name, out result))
            {
                return -1;
            }
            return result;
        }

        public static IAggregateFunction GetAggregateFunction(string name)
        {
            if (name == null)
            {
                return null;
            }
            int index;
            if (!_aggregateFunctionIndexesByName.TryGetValue(name, out index))
            {
                return null;
            }
            return _aggregateFunctionList[index];
        }

        public static ImageList GetSmallIcons()
        {
            var result = new ImageList(){TransparentColor = Color.Magenta};
            result.Images.AddRange(_aggregateFunctionList.Select(fn=>fn.SmallIcon).ToArray());
            return result;
        }

        public static IList<IAggregateFunction> ListAggregateFunctions()
        {
            return _aggregateFunctionList.AsReadOnly();
        }

        static object AggCount(ColumnDescriptor columnDescriptor, IEnumerable<RowNode> items)
        {
            return items.Count(item => item != null && columnDescriptor.GetPropertyValue(item) != null);
        }
        static object AggMin(ColumnDescriptor columnDescriptor, IEnumerable<RowNode> items)
        {
            object result = null;
            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }
                var value = columnDescriptor.GetPropertyValue(item);
                if (value == null)
                {
                    continue;
                }
                if (result == null || columnDescriptor.DataSchema.Compare(value, result) < 0)
                {
                    result = value;
                }
            }
            return result;
        }
        static object AggMax(ColumnDescriptor columnDescriptor, IEnumerable<RowNode> items)
        {
            object result = null;
            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }
                var value = columnDescriptor.GetPropertyValue(item);
                if (value == null)
                {
                    continue;
                }
                if (result == null || columnDescriptor.DataSchema.Compare(value, result) > 0)
                {
                    result = value;
                }
            }
            return result;
        }

        static bool IsNumberType(ColumnDescriptor columnDescriptor)
        {
            var wrappedType = columnDescriptor.DataSchema.GetWrappedValueType(columnDescriptor.PropertyType);
            return wrappedType.IsPrimitive;
        }

        static IList<double> GetDoubles(ColumnDescriptor columnDescriptor, IEnumerable<RowNode> items)
        {
            var doubles = new List<double>();
            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }
                var unwrappedValue = columnDescriptor.DataSchema.UnwrapValue(columnDescriptor.GetPropertyValue(item));
                if (ReferenceEquals(null, unwrappedValue))
                {
                    continue;
                }
                try
                {
                    doubles.Add((double) Convert.ChangeType(unwrappedValue, typeof(double)));
                }
                catch
                {
                    // ignore
                }
            }
            return doubles;
        }

        static object AggSum(ColumnDescriptor columnDescriptor, IEnumerable<RowNode> values)
        {
            return GetDoubles(columnDescriptor, values).Sum();
        }

        static object AggStdDev(ColumnDescriptor columnDescriptor, IEnumerable<RowNode> values)
        {
            var doubles = GetDoubles(columnDescriptor, values);
            if (doubles.Count <= 1)
            {
                return double.NaN;
            }
            double sumOfSquares = doubles.Aggregate(0.0, (seed, value) => seed + value*value);
            double mean = doubles.Average();
            double totalVariance = Math.Max(0, sumOfSquares - doubles.Count * mean * mean);
            return Math.Sqrt(totalVariance/(doubles.Count - 1));
        }

        static object AggMean(ColumnDescriptor columnDescriptor, IEnumerable<RowNode> values)
        {
            return GetDoubles(columnDescriptor, values).Average();
        }

        class AggregateFunctionImpl : IAggregateFunction
        {
            private readonly Func<ColumnDescriptor, IEnumerable<RowNode>, object> _fnAggregate;
            private readonly Func<ColumnDescriptor,Type> _fnResultType;

            public AggregateFunctionImpl(string name, string displayName, string captionPrefix, Image smallIcon, Func<ColumnDescriptor, IEnumerable<RowNode>, object> fnAggregate, Func<ColumnDescriptor,Type> fnResultType)
            {
                Name = name;
                DisplayName = displayName;
                CaptionPrefix = captionPrefix;
                _fnAggregate = fnAggregate;
                _fnResultType = fnResultType;
                SmallIcon = smallIcon;

            }
            public Type GetResultType(ColumnDescriptor columnDescriptor)
            {
                return _fnResultType(columnDescriptor);
            }
            public object Aggregate(ColumnDescriptor columnDescriptor, IEnumerable<RowNode> valueGroup)
            {
                return _fnAggregate(columnDescriptor, valueGroup);
            }
            public string CaptionPrefix { get; private set; }
            public string Name { get; private set; }
            public string DisplayName { get; private set; }
            public override string ToString()
            {
                return DisplayName;
            }
            public Image SmallIcon { get; private set; }
        }
    }
}
