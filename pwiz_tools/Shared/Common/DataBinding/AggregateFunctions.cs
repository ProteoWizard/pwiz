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

        public static readonly IAggregateFunction GroupBy = new GroupByImpl();

        public static readonly IAggregateFunction Sum = new AggregateFunctionImpl("sum", "Sum",
            Properties.Resources.agg_sum, AggSum, cd => IsNumberType(cd) ? typeof (double) : null);
        public static readonly IAggregateFunction Mean = new AggregateFunctionImpl("mean", "Mean",
            Properties.Resources.agg_mean, AggMean, cd => IsNumberType(cd) ? typeof(double) : null);
        public static readonly IAggregateFunction Count = new AggregateFunctionImpl("count", "Count",
            Properties.Resources.agg_count, AggCount, cd => typeof (int));

        public static readonly IAggregateFunction Minimum = new AggregateFunctionImpl("min", "Minimum",
            Properties.Resources.agg_min, AggMin, cd => cd.PropertyType);

        public static readonly IAggregateFunction Maximum = new AggregateFunctionImpl("max", "Maximum",
            Properties.Resources.agg_max, AggMax, cd => cd.PropertyType);

        public static readonly IAggregateFunction StdDev = new AggregateFunctionImpl("stddev", "Standard Deviation",
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

        public static IAggregateFunction GetAggregateFunction(string name)
        {
            int index;
            if (!_aggregateFunctionIndexesByName.TryGetValue(name, out index))
            {
                return null;
            }
            return _aggregateFunctionList[index];
        }

        public static ImageList GetSmallIcons()
        {
            var result = new ImageList();
            result.Images.AddRange(_aggregateFunctionList.Select(fn=>fn.SmallIcon).ToArray());
            return result;
        }

        public static IList<IAggregateFunction> ListAggregateFunctions()
        {
            return _aggregateFunctionList.AsReadOnly();
        }

        static object AggCount(ColumnDescriptor columnDescriptor, IEnumerable<RowItem> items)
        {
            return items.Count(item => item != null && columnDescriptor.GetPropertyValue(item, null) != null);
        }
        static object AggMin(ColumnDescriptor columnDescriptor, IEnumerable<RowItem> items)
        {
            object result = null;
            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }
                var value = columnDescriptor.GetPropertyValue(item, null);
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
        static object AggMax(ColumnDescriptor columnDescriptor, IEnumerable<RowItem> items)
        {
            object result = null;
            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }
                var value = columnDescriptor.GetPropertyValue(item, null);
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

        static IList<double> GetDoubles(ColumnDescriptor columnDescriptor, IEnumerable<RowItem> items)
        {
            var doubles = new List<double>();
            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }
                var unwrappedValue = columnDescriptor.DataSchema.UnwrapValue(columnDescriptor.GetPropertyValue(item, null));
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

        static object AggSum(ColumnDescriptor columnDescriptor, IEnumerable<RowItem> values)
        {
            return GetDoubles(columnDescriptor, values).Sum();
        }

        static object AggStdDev(ColumnDescriptor columnDescriptor, IEnumerable<RowItem> values)
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

        static object AggMean(ColumnDescriptor columnDescriptor, IEnumerable<RowItem> values)
        {
            return GetDoubles(columnDescriptor, values).Average();
        }

        class AggregateFunctionImpl : IAggregateFunction
        {
            private readonly Func<ColumnDescriptor, IEnumerable<RowItem>, object> _fnAggregate;
            private readonly Func<ColumnDescriptor,Type> _fnResultType;

            public AggregateFunctionImpl(string name, string displayName, Image smallIcon, Func<ColumnDescriptor, IEnumerable<RowItem>, object> fnAggregate, Func<ColumnDescriptor,Type> fnResultType)
            {
                Name = name;
                DisplayName = displayName;
                _fnAggregate = fnAggregate;
                _fnResultType = fnResultType;
                SmallIcon = smallIcon;
            }
            public Type GetResultType(ColumnDescriptor columnDescriptor)
            {
                return _fnResultType(columnDescriptor);
            }
            public object Aggregate(ColumnDescriptor columnDescriptor, KeyValuePair<RowKey, IEnumerable<RowItem>> valueGroup)
            {
                return _fnAggregate(columnDescriptor, valueGroup.Value);
            }
            public IDictionary<RowKey, IEnumerable<RowItem>> Group(ColumnDescriptor columnDescriptor, IDictionary<RowKey, IEnumerable<RowItem>> valueGroups)
            {
                return valueGroups;
            }
            public string Name { get; private set; }
            public string DisplayName { get; private set; }
            public override string ToString()
            {
                return DisplayName;
            }
            public Image SmallIcon { get; private set; }
        }
        class GroupByImpl : IAggregateFunction
        {
            public string Name
            {
                get { return "groupby"; }
            }

            public string DisplayName
            {
                get { return "Group By"; }
            }

            public Type GetResultType(ColumnDescriptor columnDescriptor)
            {
                return columnDescriptor.PropertyType;
            }

            public IDictionary<RowKey, IEnumerable<RowItem>> Group(ColumnDescriptor columnDescriptor, IDictionary<RowKey, IEnumerable<RowItem>> groups)
            {
//                var dictionary = new Dictionary<KeyValuePair<RowKey, object>, List<object>>();
//                foreach (var group in groups)
//                {
//                    foreach (var item in group.Value)
//                    {
//                        var value = item == null ? null : columnDescriptor.GetPropertyValue(item, null);
//                        var key = new KeyValuePair<RowKey, object>(group.Key, value);
//                        List<object> list;
//                        if (!dictionary.TryGetValue(key, out list))
//                        {
//                            list = new List<object>();
//                            dictionary.Add(key, list);
//                        }
//                        list.Add(item);
//                    }
//                }
//                return dictionary.ToDictionary(kvp => kvp.Key.Key.AddValue(columnDescriptor.IdPath, kvp.Key.Value), kvp =>kvp.Value);
                return null;
            }

            public object Aggregate(ColumnDescriptor columnDescriptor, KeyValuePair<RowKey, IEnumerable<RowItem>> items)
            {
                return items.Key.FindValue(columnDescriptor.IdPath);
            }

            public Image SmallIcon
            {
                get { return Properties.Resources.agg_groupby; }
            }
        }
    }
}
