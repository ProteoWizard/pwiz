/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Linq;
using MathNet.Numerics.Statistics;
using pwiz.Common.Collections;
using pwiz.Common.Properties;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    public abstract class AggregateOperation : LabeledValues<string>
    {
        public static readonly AggregateOperation Count = new CountImpl();

        public static readonly AggregateOperation Sum = new NumericAggregate(@"Sum",
            () => Resources.AggregateOperation_Sum_Sum, () => Resources.AggregateOperation_Sum_Sum__0_, 
            values => values.Sum());

        public static readonly AggregateOperation Mean = new NumericAggregate(@"Mean",
            () => Resources.AggregateOperation_Mean_Mean, () => Resources.AggregateOperation_Mean_Mean__0_, 
            values => values.Mean());

        public static readonly AggregateOperation Min = new SelectOne(@"Min",
            () => Resources.AggregateOperation_Min_Min, () => Resources.AggregateOperation_Min_Min__0_, 
            (dataSchema, values) => FindFirst(values, (v1, v2) => +dataSchema.Compare(v1, v2))
        );

        public static readonly AggregateOperation Max = new SelectOne(@"Max",
            () => Resources.AggregateOperation_Max_Max, () => Resources.AggregateOperation_Max_Max__0_,
            (dataSchema, values) => FindFirst(values, (v1, v2) => -dataSchema.Compare(v1, v2)));

        public static readonly AggregateOperation StdDev = new NumericAggregate(@"StdDev",
            () => Resources.AggregateOperation_StdDev_Standard_Deviation,
            () => Resources.AggregateOperation_StdDev_StdDev__0_, 
            values => values.StandardDeviation());

        public static readonly AggregateOperation Cv = new NumericAggregate(@"Cv",
            () => Resources.AggregateOperation_Cv_Coefficient_Of_Variation,
            () => Resources.AggregateOperation_Cv_CV_Of__0_, 
            values => values.StandardDeviation() / values.Mean());

        public static readonly ImmutableList<AggregateOperation> ALL = ImmutableList.ValueOf(new[]
        {
            Sum,
            Count, 
            Mean,
            Min,
            Max,
            StdDev,
            Cv
        });

        public static AggregateOperation FromName(string name)
        {
            return ALL.FirstOrDefault(op => op.Name == name);
        }

        private readonly Func<string> _getCaptionFormatStringFunc;

        protected AggregateOperation(string name, Func<string> getLabelFunc, Func<string> getCaptionFormatString) :
            base(name, getLabelFunc)
        {
            _getCaptionFormatStringFunc = getCaptionFormatString;
        }

        public abstract bool IsValidForType(DataSchema dataSchema, Type type);

        public override string ToString()
        {
            return Label;
        }

        public abstract Type GetPropertyType(Type originalPropertyType);

        public abstract object CalculateValue(DataSchema dataSchema, IEnumerable<object> values);

        public IColumnCaption QualifyColumnCaption(IColumnCaption baseCaption)
        {
            return new AggregateCaption(this, baseCaption);
        }
        private class SelectOne : AggregateOperation
        {
            private Func<DataSchema, IEnumerable<object>, object> _calculator;
            public SelectOne(string name, Func<string> getLabelFunc, Func<string> columnCaption, Func<DataSchema, IEnumerable<object>, object> calculator) : base(name, getLabelFunc, columnCaption)
            {
                _calculator = calculator;
            }

            public override object CalculateValue(DataSchema dataSchema, IEnumerable<object> values)
            {
                return _calculator(dataSchema, values);
            }

            public override Type GetPropertyType(Type originalPropertyType)
            {
                return originalPropertyType;
            }

            public override bool IsValidForType(DataSchema dataSchema, Type type)
            {
                return true;
            }
        }

        private class CountImpl : AggregateOperation
        {
            public CountImpl() : base(@"Count",
                () => Resources.CountImpl_CountImpl_Count, ()=>Resources.CountImpl_CountImpl_Count__0_)
            {
                
            }

            public override object CalculateValue(DataSchema dataSchema, IEnumerable<object> values)
            {
                return values.Count(o => !ReferenceEquals(o, null));
            }

            public override Type GetPropertyType(Type originalPropertyType)
            {
                return typeof(int);
            }

            public override bool IsValidForType(DataSchema dataSchema, Type type)
            {
                return true;
            }
        }

        private class NumericAggregate : AggregateOperation
        {
            private Func<IList<double>, double?> _calculator;
            public NumericAggregate(string name, Func<string> getLabelFunc, Func<string> columnCaption, Func<IList<double>, double?> calculator)
                : base(name, getLabelFunc, columnCaption)
            {
                _calculator = calculator;
            }

            public override object CalculateValue(DataSchema dataSchema, IEnumerable<object> values)
            {
                var doubleValues = new List<double>();
                foreach (var value in values)
                {
                    try
                    {
                        var unwrappedValue = dataSchema.UnwrapValue(value);
                        if (unwrappedValue != null)
                        {
                            doubleValues.Add(Convert.ToDouble(unwrappedValue));
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore
                    }
                }
                return _calculator(doubleValues);
            }

            public override Type GetPropertyType(Type originalPropertyType)
            {
                return typeof(double);
            }

            public override bool IsValidForType(DataSchema dataSchema, Type type)
            {
                type = dataSchema.GetWrappedValueType(type);
                return TypeDescriptor.GetConverter(type).CanConvertTo(typeof(double));
            }
        }

        private static object FindFirst(IEnumerable<object> values, Func<object, object, int> comparator)
        {
            object result = null;
            foreach (var value in values)
            {
                if (value == null)
                {
                    continue;
                }
                if (result == null || comparator(value, result) < 0)
                {
                    result = value;
                }
            }
            return result;
        }

        private class AggregateCaption : IColumnCaption
        {
            private AggregateOperation _aggregateOperation;
            private IColumnCaption _baseCaption;
            public AggregateCaption(AggregateOperation aggregateOperation, IColumnCaption baseCaption)
            {
                _aggregateOperation = aggregateOperation;
                _baseCaption = baseCaption;
            }

            public string GetCaption(DataSchemaLocalizer localizer)
            {
                return LocalizationHelper.CallWithCulture(localizer.FormatProvider,
                    () => string.Format(_aggregateOperation._getCaptionFormatStringFunc(),
                        _baseCaption.GetCaption(localizer)));
            }

            protected bool Equals(AggregateCaption other)
            {
                return Equals(_aggregateOperation, other._aggregateOperation) && Equals(_baseCaption, other._baseCaption);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((AggregateCaption) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((_aggregateOperation != null ? _aggregateOperation.GetHashCode() : 0) * 397) ^
                           (_baseCaption != null ? _baseCaption.GetHashCode() : 0);
                }
            }
        }
    }
}

    
