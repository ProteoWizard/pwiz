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
using System.ComponentModel;
using System.Linq;
using pwiz.Common.DataBinding.Internal;
using pwiz.Common.Properties;

namespace pwiz.Common.DataBinding
{
    public interface IFilterOperation
    {
        string OpName { get; }
        string DisplayName { get; }
        bool IsValidFor(ColumnDescriptor columnDescriptor);
        bool IsValidFor(DataSchema dataSchema, Type columnType);
        Type GetOperandType(ColumnDescriptor columnDescriptor);
        Type GetOperandType(DataSchema dataSchema, Type columnType);
        Predicate<object> MakePredicate(DataSchema dataSchema, Type columnType, string operand);
        Predicate<object> MakePredicate(ColumnDescriptor columnDescriptor, string operand);
        Predicate<object> MakePredicate(PropertyDescriptor propertyDescriptor, string operand);
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

        // ReSharper disable NonLocalizedString
        public static readonly IFilterOperation OP_HAS_ANY_VALUE = new UnaryFilterOperation("", () => Resources.FilterOperations_Has_Any_Value,
            (dataSchema, columnType, operand) => rowNode => true);

        public static readonly IFilterOperation OP_EQUALS
            = new FilterOperation("equals", ()=>Resources.FilterOperations_Equals, FnEquals);

        public static readonly IFilterOperation OP_NOT_EQUALS
            = new FilterOperation("<>", () => Resources.FilterOperations_Does_Not_Equal, FnNotEquals);

        public static readonly IFilterOperation OP_IS_BLANK
            = new UnaryFilterOperation("isnullorblank", () => Resources.FilterOperations_Is_Blank, FnIsBlank);

        public static readonly IFilterOperation OP_IS_NOT_BLANK
            = new UnaryFilterOperation("isnotnullorblank", () => Resources.FilterOperations_Is_Not_Blank, FnIsNotBlank);

        public static readonly IFilterOperation OP_IS_GREATER_THAN
            = new ComparisonFilterOperation(">", () => Resources.FilterOperations_Is_Greater_Than, i => i > 0);

        public static readonly IFilterOperation OP_IS_LESS_THAN
            = new ComparisonFilterOperation("<", () => Resources.FilterOperations_Is_Less_Than, i => i < 0);

        public static readonly IFilterOperation OP_IS_GREATER_THAN_OR_EQUAL
            = new ComparisonFilterOperation(">=", () => Resources.FilterOperations_Is_Greater_Than_Or_Equal_To, i => i >= 0);

        public static readonly IFilterOperation OP_IS_LESS_THAN_OR_EQUAL
            = new ComparisonFilterOperation("<=", () => Resources.FilterOperations_Is_Less_Than_Or_Equal_To, i => i <= 0);

        public static readonly IFilterOperation OP_CONTAINS = new StringFilterOperation("contains", () => Resources.FilterOperations_Contains, FnContains);
        public static readonly IFilterOperation OP_NOT_CONTAINS = new StringFilterOperation("notcontains", () => Resources.FilterOperations_Does_Not_Contain, FnNotContains);
        public static readonly IFilterOperation OP_STARTS_WITH = new StringFilterOperation("startswith", () => Resources.FilterOperations_Starts_With, FnStartsWith);
        public static readonly IFilterOperation OP_NOT_STARTS_WITH = new StringFilterOperation("notstartswith", () => Resources.FilterOperations_Does_Not_Start_With, FnNotStartsWith);
        // ReSharper enable NonLocalizedString

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

        public static Predicate<object> FnEquals(DataSchema dataSchema, Type columnType, string strOperand)
        {
            object operand = ConvertOperand(dataSchema, columnType, strOperand);
            return value => Equals(ConvertValue(dataSchema, columnType, value), operand);
        }

        public static Predicate<object> FnNotEquals(DataSchema dataSchema, Type columnType, string strOperand)
        {
            object operand = ConvertOperand(dataSchema, columnType, strOperand);
            return value => !Equals(ConvertValue(dataSchema, columnType, value), operand);
        }

        public static Predicate<object> FnIsBlank(DataSchema dataSchema, Type columnType, string operand)
        {
            return value => null == value || Equals(value, "");
        }

        public static Predicate<object> FnIsNotBlank(DataSchema dataSchema, Type columnType, string operand)
        {
            return value => null != value && !Equals(value, "");
        }

        public static MakePredicateFunc MakeFnCompare(Predicate<int> comparisonPredicate)
        {
            return (dataSchema, columnType, strOperand)
                =>
            {
                object operand = ConvertOperand(dataSchema, columnType, strOperand);
                return
                    value =>
                        null != value &&
                        comparisonPredicate(dataSchema.Compare(ConvertValue(dataSchema, columnType, value),
                            operand));
            };

        }

        public static Predicate<object> FnContains(DataSchema dataSchema, Type columnType, string strOperand)
        {
            return value => null != value && value.ToString().IndexOf(strOperand, StringComparison.Ordinal) >= 0;
        }

        public static Predicate<object> FnNotContains(DataSchema dataSchema, Type columnType, string strOperand)
        {
            return value => null != value && value.ToString().IndexOf(strOperand, StringComparison.Ordinal) < 0;
        }

        public static Predicate<object> FnStartsWith(DataSchema dataSchema, Type columnType, string strOperand)
        {
            return value => null != value && value.ToString().StartsWith(strOperand);
        }

        public static Predicate<object> FnNotStartsWith(DataSchema dataSchema, Type columnType, string strOperand)
        {
            return value => null != value && !value.ToString().StartsWith(strOperand);
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

        public static object ConvertOperand(DataSchema dataSchema, Type columnType, string operand)
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

        public delegate Predicate<object> MakePredicateFunc(DataSchema dataSchema, Type columnType, String operand);

        class FilterOperation : IFilterOperation
        {
            private readonly MakePredicateFunc _fnMakePredicate;
            private readonly Func<string> _getDisplayNameFunc;
            public FilterOperation(string opName, Func<string> getDisplayNameFunc, MakePredicateFunc fnMakePredicate)
            {
                OpName = opName;
                _getDisplayNameFunc = getDisplayNameFunc;
                _fnMakePredicate = fnMakePredicate;
            }
            public string OpName { get; private set; }
            public string DisplayName { get { return _getDisplayNameFunc(); } }
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
            public Predicate<object> MakePredicate(ColumnDescriptor columnDescriptor, string operand)
            {
                return MakePredicate(columnDescriptor.DataSchema, columnDescriptor.PropertyType, operand);
            }

            public Predicate<object> MakePredicate(DataSchema dataSchema, Type columnType, string operand)
            {
                return _fnMakePredicate(dataSchema, columnType, operand);
            }

            public Predicate<object> MakePredicate(PropertyDescriptor propertyDescriptor, string operand)
            {
                DataSchema dataSchema = GetDataSchema(propertyDescriptor)
                                        ?? new DataSchema();
                return MakePredicate(dataSchema, propertyDescriptor.PropertyType, operand);
            }

            private DataSchema GetDataSchema(PropertyDescriptor properyDescriptor)
            {
                ColumnPropertyDescriptor columnPropertyDescriptor = properyDescriptor as ColumnPropertyDescriptor;
                if (null != columnPropertyDescriptor)
                {
                    return columnPropertyDescriptor.DisplayColumn.DataSchema;
                }
                GroupedPropertyDescriptor groupedPropertyDescriptor = properyDescriptor as GroupedPropertyDescriptor;
                if (null != groupedPropertyDescriptor)
                {
                    return groupedPropertyDescriptor.DisplayColumn.DataSchema;
                }
                return null;
            }
        }
        class StringFilterOperation : FilterOperation
        {
            public StringFilterOperation(string opName, Func<string> fnDisplayName, MakePredicateFunc fnMakePredicate) 
                : base(opName, fnDisplayName, fnMakePredicate)
            {
            }
            public override bool IsValidFor(DataSchema dataSchema, Type columnType)
            {
                return typeof (string) == dataSchema.GetWrappedValueType(columnType);
            }
        }

        class ComparisonFilterOperation : FilterOperation
        {
            public ComparisonFilterOperation(string opName, Func<string> fnDisplayName,
                Predicate<int> filterFunc)
                : base(opName, fnDisplayName, MakeFnCompare(filterFunc))
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
        }

        class UnaryFilterOperation : FilterOperation
        {
            public UnaryFilterOperation(string opName, Func<string> fnGetDisplayName,
                MakePredicateFunc fnMakePredicate)
                : base(opName, fnGetDisplayName, fnMakePredicate)
            {
                
            }

            public override Type GetOperandType(DataSchema dataSchema, Type columnType)
            {
                return null;
            }
        }
    }
}
