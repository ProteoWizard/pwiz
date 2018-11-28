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
using System.Globalization;
using System.Linq;
using pwiz.Common.Properties;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    public interface IFilterOperation
    {
        string OpName { get; }
        [Track]
        string DisplayName { get; }
        bool IsValidFor(ColumnDescriptor columnDescriptor);
        bool IsValidFor(DataSchema dataSchema, Type columnType);
        Type GetOperandType(ColumnDescriptor columnDescriptor);
        Type GetOperandType(DataSchema dataSchema, Type columnType);
        bool Matches(DataSchema dataSchema, Type columnType, object columnValue, object operandValue);
    }

    public static class FilterOperations
    {
        /// <summary>
        /// Given a column type, this Dictionary gives what type the operands of a filter operation should
        /// be converted to.  Number columns get converted to "double" so that "less than 3.5" gives the user
        /// the expected result, even if the column type is integer.
        /// </summary>
        private static readonly IDictionary<Type, Type> convertibleTypes = new Dictionary<Type, Type>
        {
            {typeof (char), typeof (char)},
            {typeof (sbyte), typeof (double)},
            {typeof (byte), typeof (double)},
            {typeof (short), typeof (double)},
            {typeof (ushort), typeof (double)},
            {typeof (int), typeof (double)},
            {typeof (uint), typeof (double)},
            {typeof (long), typeof (double)},
            {typeof (ulong), typeof (double)},
            {typeof (float), typeof (double)},
            {typeof (double), typeof (double)},
            {typeof (Decimal), typeof (double)},
            {typeof (DateTime), typeof (DateTime)},
            {typeof (bool), typeof(bool)},
        };

        public static readonly IFilterOperation OP_HAS_ANY_VALUE = new OpHasAnyValue();

        public static readonly IFilterOperation OP_EQUALS = new OpEquals();

        public static readonly IFilterOperation OP_NOT_EQUALS = new OpNotEquals();

        public static readonly IFilterOperation OP_IS_BLANK = new OpIsBlank();

        public static readonly IFilterOperation OP_IS_NOT_BLANK = new OpIsNotBlank();

        public static readonly IFilterOperation OP_IS_GREATER_THAN = new OpIsGreaterThan();

        public static readonly IFilterOperation OP_IS_LESS_THAN = new OpIsLessThan();

        public static readonly IFilterOperation OP_IS_GREATER_THAN_OR_EQUAL = new OpIsGreaterThanOrEqual();

        public static readonly IFilterOperation OP_IS_LESS_THAN_OR_EQUAL = new OpIsLessThanOrEqualTo();

        public static readonly IFilterOperation OP_CONTAINS = new OpContains();
        public static readonly IFilterOperation OP_NOT_CONTAINS = new OpNotContains();
        public static readonly IFilterOperation OP_STARTS_WITH = new OpStartsWith();
        public static readonly IFilterOperation OP_NOT_STARTS_WITH = new OpNotStartsWith();
        // ReSharper restore LocalizableElement

        private static readonly IList<IFilterOperation> LstFilterOperations = Array.AsReadOnly(new[]
        {
            OP_HAS_ANY_VALUE,
            OP_EQUALS,
            OP_NOT_EQUALS,
            OP_IS_BLANK,
            OP_IS_NOT_BLANK,
            OP_IS_GREATER_THAN,
            OP_IS_LESS_THAN,
            OP_IS_GREATER_THAN_OR_EQUAL,
            OP_IS_LESS_THAN_OR_EQUAL,
            OP_CONTAINS,
            OP_NOT_CONTAINS,
            OP_STARTS_WITH,
            OP_NOT_STARTS_WITH
        });

        private static readonly IDictionary<string, IFilterOperation> DictFilterOperations =
            LstFilterOperations.ToDictionary(op => op.OpName, op => op);

        public static IFilterOperation GetOperation(string name)
        {
            IFilterOperation result;
            DictFilterOperations.TryGetValue(name, out result);
            return result;
        }

        public static IList<IFilterOperation> ListOperations()
        {
            return LstFilterOperations;
        }

        public static bool MatchEquals(object columnValue, object operandValue)
        {
            return Equals(columnValue, operandValue);
        }

        public static bool MatchNotEquals(object columnValue, object operandValue)
        {
            return !Equals(columnValue, operandValue);
        }

        public static Type GetTypeToConvertOperandTo(ColumnDescriptor columnDescriptor)
        {
            return GetTypeToConvertOperandTo(columnDescriptor.DataSchema, columnDescriptor.PropertyType);
        }

        public static Type GetTypeToConvertOperandTo(DataSchema dataSchema, Type columnType)
        {
            if (null == columnType)
            {
                return typeof (string);
            }
            columnType = dataSchema.GetWrappedValueType(columnType);
            columnType = Nullable.GetUnderlyingType(columnType) ?? columnType;
            Type typeToConvertTo;
            if (convertibleTypes.TryGetValue(columnType, out typeToConvertTo))
            {
                return typeToConvertTo;
            }
            return typeof (string);
        }

        public static object ConvertOperand(DataSchema dataSchema, Type columnType, string operand, CultureInfo cultureInfo)
        {
            var type = GetTypeToConvertOperandTo(dataSchema, columnType);
            if (null == type)
            {
                return operand;
            }
            if (typeof (char) == type)
            {
                if (operand.Length != 1)
                {
                    return null;
                }
                return operand[0];
            }
            if (type.IsEnum)
            {
                if (string.IsNullOrEmpty(operand))
                {
                    return null;
                }
                try
                {
                    return Enum.Parse(type, operand);
                }
                catch
                {
                    return Enum.Parse(type, operand, true);
                }
            }
            if (string.IsNullOrEmpty(operand))
            {
                return null;
            }
            return Convert.ChangeType(operand, type);
        }

        public static object ConvertValue(ColumnDescriptor columnDescriptor, object value)
        {
            return ConvertValue(columnDescriptor.DataSchema, columnDescriptor.PropertyType, value);
        }

        public static object ConvertValue(DataSchema dataSchema, Type columnType, object value)
        {
            if (value == null)
            {
                return null;
            }
            var type = GetTypeToConvertOperandTo(dataSchema, columnType);
            if (type == typeof (string))
            {
                return value.ToString();
            }
            return Convert.ChangeType(value, type);
        }

        public delegate bool MatchingFunc(object columnValue, object operandValue);

        abstract class FilterOperation : IFilterOperation
        {
            protected FilterOperation(string opName)
            {
                OpName = opName;
            }
            public string OpName { get; private set; }
            public abstract string DisplayName { get; }
            public bool IsValidFor(ColumnDescriptor columnDescriptor)
            {
                return IsValidFor(columnDescriptor.DataSchema, columnDescriptor.PropertyType);
            }

            public virtual bool IsValidFor(DataSchema dataSchema, Type columnType)
            {
                return true;
            }

            public virtual Type GetOperandType(DataSchema dataSchema, Type columnType)
            {
                return GetTypeToConvertOperandTo(dataSchema, columnType);
            }
            public Type GetOperandType(ColumnDescriptor columnDescriptor)
            {
                return GetOperandType(columnDescriptor.DataSchema, columnDescriptor.PropertyType);
            }

            public abstract bool Matches(DataSchema dataSchema, Type columnType, object columnValue, object operandValue);
        }
        abstract class StringFilterOperation : FilterOperation
        {
            protected StringFilterOperation(string opName) 
                : base(opName)
            {
            }
            public override bool IsValidFor(DataSchema dataSchema, Type columnType)
            {
                var type = dataSchema.GetWrappedValueType(columnType);
                if (typeof(IFormattable).IsAssignableFrom(type))
                {
                    return false;
                }
                if (type.IsPrimitive)
                {
                    return false;
                }
                return true;
            }

            public override bool Matches(DataSchema dataSchema, Type columnType, object columnValue, object operandValue)
            {
                DataSchemaLocalizer dataSchemaLocalizer = dataSchema.DataSchemaLocalizer;
                String strColumnValue = ValueToString(dataSchemaLocalizer, columnValue);
                String strOperandValue = ValueToString(dataSchemaLocalizer, operandValue);
                return StringMatches(strColumnValue, strOperandValue);
            }

            public abstract bool StringMatches(string columnValue, string operandValue);

            protected string ValueToString(DataSchemaLocalizer dataSchemaLocalizer, object value)
            {
                if (value == null)
                {
                    return string.Empty;
                }
                var formattable = value as IFormattable;
                if (formattable != null)
                {
                    return formattable.ToString(null, dataSchemaLocalizer.FormatProvider);
                }
                return value.ToString();
            }
        }

        class OpContains : StringFilterOperation
        {
            public OpContains() : base(@"contains")
            {
            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Contains; }
            }

            public override bool StringMatches(string columnValue, string operandValue)
            {
                if (string.IsNullOrEmpty(columnValue))
                {
                    return false;
                }
                return columnValue.Contains(operandValue);
            }
        }

        class OpNotContains : StringFilterOperation
        {
            public OpNotContains() : base(@"notcontains")
            {
            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Does_Not_Contain; }
            }

            public override bool StringMatches(string columnValue, string operandValue)
            {
                if (string.IsNullOrEmpty(columnValue))
                {
                    return true;
                }
                return !columnValue.Contains(operandValue);
            }
        }

        class OpStartsWith : StringFilterOperation
        {
            public OpStartsWith() : base(@"startswith")
            {
            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Starts_With; }
            }

            public override bool StringMatches(string columnValue, string operandValue)
            {
                if (string.IsNullOrEmpty(columnValue))
                {
                    return false;
                }
                return columnValue.StartsWith(operandValue);
            }
        }

        class OpNotStartsWith : StringFilterOperation
        {
            public OpNotStartsWith() : base(@"notstartswith")
            {
            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Does_Not_Start_With; }
            }

            public override bool StringMatches(string columnValue, string operandValue)
            {
                if (string.IsNullOrEmpty(columnValue))
                {
                    return true;
                }
                return !columnValue.StartsWith(operandValue);
            }
        }

        class OpEquals : FilterOperation
        {
            public OpEquals() : base(@"equals")
            {
            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Equals; }
            }

            public override bool Matches(DataSchema dataSchema, Type columnType, object columnValue, object operandValue)
            {
                return Equals(
                    ConvertValue(dataSchema, columnType, columnValue),
                    operandValue);
            }


        }

        class OpNotEquals : FilterOperation
        {
            public OpNotEquals() : base(@"<>")
            {
            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Does_Not_Equal; }
            }

            public override bool Matches(DataSchema dataSchema, Type columnType, object columnValue, object operandValue)
            {
                return !Equals(ConvertValue(dataSchema, columnType, columnValue), operandValue);
            }
        }

        class OpIsBlank : UnaryFilterOperation
        {
            public OpIsBlank() : base(@"isnullorblank")
            {
                
            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Is_Blank; }
            }

            protected override bool Matches(object columnValue)
            {
                return columnValue == null || Equals(string.Empty, columnValue);
            }
        }

        class OpIsNotBlank : FilterOperation
        {
            public OpIsNotBlank() : base(@"isnotnullorblank")
            {
                
            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Is_Not_Blank; }
            }

            public override bool Matches(DataSchema dataSchema, Type columnType, object columnValue, object operandValue)
            {
                return null != columnValue && !Equals(columnValue, string.Empty);
            }
        }

        class OpIsGreaterThan : ComparisonFilterOperation
        {
            public OpIsGreaterThan() : base(@">")
            {
                
            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Is_Greater_Than; }
            }

            protected override bool ComparisonMatches(int comparisonResult)
            {
                return comparisonResult > 0;
            }
        }

        class OpIsGreaterThanOrEqual : ComparisonFilterOperation
        {
            public OpIsGreaterThanOrEqual() : base(@">=")
            {
                
            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Is_Greater_Than_Or_Equal_To; }
            }


            protected override bool ComparisonMatches(int comparisonResult)
            {
                return comparisonResult >= 0;
            }
        }

        class OpIsLessThan : ComparisonFilterOperation
        {
            public OpIsLessThan() : base(@"<")
            {
                
            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Is_Less_Than; }
            }

            protected override bool ComparisonMatches(int comparisonResult)
            {
                return comparisonResult < 0;
            }
        }

        class OpIsLessThanOrEqualTo : ComparisonFilterOperation
        {
            public OpIsLessThanOrEqualTo() : base(@"<=")
            {
            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Is_Less_Than_Or_Equal_To; }
            }

            protected override bool ComparisonMatches(int comparisonResult)
            {
                return comparisonResult <= 0;
            }
        }

        abstract class ComparisonFilterOperation : FilterOperation
        {
            protected ComparisonFilterOperation(string opName)
                : base(opName)
            {
            }

            public override bool IsValidFor(DataSchema dataSchema, Type propertyType)
            {
                if (null == propertyType)
                {
                    return false;
                }
                var columnType = dataSchema.GetWrappedValueType(propertyType);
                columnType = Nullable.GetUnderlyingType(columnType) ?? columnType;
                return convertibleTypes.ContainsKey(columnType) && columnType != typeof (string) && columnType != typeof(bool);
            }

            public override bool Matches(DataSchema dataSchema, Type columnType, object columnValue, object operandValue)
            {
                if (columnValue == null)
                {
                    return false;
                }
                columnValue = Convert.ChangeType(columnValue, GetTypeToConvertOperandTo(dataSchema, columnType));
                return ComparisonMatches(dataSchema.Compare(columnValue, operandValue));
            }

            protected abstract bool ComparisonMatches(int comparisonResult);
        }

        abstract class UnaryFilterOperation : FilterOperation
        {
            protected UnaryFilterOperation(string opName)
                : base(opName)
            {
                
            }

            public override Type GetOperandType(DataSchema dataSchema, Type columnType)
            {
                return null;
            }

            public override bool Matches(DataSchema dataSchema, Type columnType, object columnValue, object operandValue)
            {
                return Matches(columnValue);
            }

            protected abstract bool Matches(object columnValue);
        }

        class OpHasAnyValue : UnaryFilterOperation
        {
            public OpHasAnyValue() : base(string.Empty)
            {
            }

            public override string DisplayName
            {
                get { return Resources.FilterOperations_Has_Any_Value; }
            }

            protected override bool Matches(object columnValue)
            {
                return true;
            }
        }
    }
}
