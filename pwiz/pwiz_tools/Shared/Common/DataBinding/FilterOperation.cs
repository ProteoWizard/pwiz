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
using System.Linq;

namespace pwiz.Common.DataBinding
{
    public interface IFilterOperation
    {
        string OpName { get; }
        string DisplayName { get; }
        bool IsValidFor(ColumnDescriptor columnDescriptor);
        Type GetOperandType(ColumnDescriptor columnDescriptor);
        Predicate<object> MakePredicate(ColumnDescriptor columnDescriptor, string operand);
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
                {typeof (char),typeof(char)},
                {typeof (sbyte),typeof(double)},
                {typeof (byte),typeof(double)},
                {typeof (short),typeof(double)},
                {typeof (ushort),typeof(double)},
                {typeof (int),typeof(double)},
                {typeof (uint),typeof(double)},
                {typeof (long),typeof(double)},
                {typeof (ulong),typeof(double)},
                {typeof (float),typeof(double)},
                {typeof (double),typeof(double)},
                {typeof (Decimal),typeof(double)},
                {typeof (DateTime),typeof(DateTime)}
            };
        public static readonly IFilterOperation OP_HAS_ANY_VALUE = new UnaryFilterOperation("", "Has Any Value",
                                                                         (cd, operand) => rowNode => true);

        public static readonly IFilterOperation OP_EQUALS 
            = new FilterOperation("equals", "Equals", FnEquals);

        public static readonly IFilterOperation OP_NOT_EQUALS 
            = new FilterOperation("<>", "Does Not Equal", FnNotEquals);

        public static readonly IFilterOperation OP_IS_BLANK
            = new UnaryFilterOperation("isnullorblank", "Is Blank", FnIsBlank);

        public static readonly IFilterOperation OP_IS_NOT_BLANK
            = new UnaryFilterOperation("isnotnullorblank", "Is Not Blank", FnIsNotBlank);

        public static readonly IFilterOperation OP_IS_GREATER_THAN
            = new ComparisonFilterOperation(">", "Is Greater Than", i => i > 0);
        public static readonly IFilterOperation OP_IS_LESS_THAN
            = new ComparisonFilterOperation("<", "Is Less Than", i => i < 0);
        public static readonly IFilterOperation OP_IS_GREATER_THAN_OR_EQUAL
            = new ComparisonFilterOperation(">=", "Is Greater Than Or Equal To", i => i >= 0);
        public static readonly IFilterOperation OP_IS_LESS_THAN_OR_EQUAL
            = new ComparisonFilterOperation("<=", "Is Less Than Or Equal To", i => i <= 0);

        public static readonly IFilterOperation OP_CONTAINS = new StringFilterOperation("contains", "Contains", FnContains);
        public static readonly IFilterOperation OP_NOT_CONTAINS = new StringFilterOperation("notcontains", "Does Not Contain", FnNotContains);
        public static readonly IFilterOperation OP_STARTS_WITH = new StringFilterOperation("startswith", "Starts With", FnStartsWith);
        public static readonly IFilterOperation OP_NOT_STARTS_WITH = new StringFilterOperation("notstartswith", "Does Not Start With", FnNotStartsWith);

        private static readonly IList<IFilterOperation> LstFilterOperations = Array.AsReadOnly(new[]{
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

        public static Predicate<object> FnEquals(ColumnDescriptor columnDescriptor, string strOperand)
        {
            object operand = ConvertOperand(columnDescriptor, strOperand);
            return value => Equals(ConvertValue(columnDescriptor, value), operand);
        }
        public static Predicate<object> FnNotEquals(ColumnDescriptor columnDescriptor, string strOperand)
        {
            object operand = ConvertOperand(columnDescriptor, strOperand);
            return value => !Equals(ConvertValue(columnDescriptor, value), operand);
        }
        public static Predicate<object> FnIsBlank(ColumnDescriptor columnDescriptor, string operand)
        {
            return value => null == value || Equals(value, "");
        }
        public static Predicate<object> FnIsNotBlank(ColumnDescriptor columnDescriptor, string operand)
        {
            return value => null != value && !Equals(value, "");
        }
        public static Func<ColumnDescriptor, string, Predicate<object>> MakeFnCompare(Predicate<int> comparisonPredicate)
        {
            return (columnDescriptor, strOperand)
                   =>
                       {
                           object operand = ConvertOperand(columnDescriptor, strOperand);
                           return value => null != value && comparisonPredicate(columnDescriptor.DataSchema.Compare(ConvertValue(columnDescriptor, value), operand));
                       };
            
        }
        public static Predicate<object> FnContains(ColumnDescriptor columnDescriptor, string strOperand)
        {
            return value => null != value && value.ToString().IndexOf(strOperand, StringComparison.Ordinal) >= 0;
        }
        public static Predicate<object> FnNotContains(ColumnDescriptor columnDescriptor, string strOperand)
        {
            return value => null != value && value.ToString().IndexOf(strOperand, StringComparison.Ordinal) < 0;
        }
        public static Predicate<object> FnStartsWith(ColumnDescriptor columnDescriptor, string strOperand)
        {
            return value => null != value && value.ToString().StartsWith(strOperand);
        }
        public static Predicate<object> FnNotStartsWith(ColumnDescriptor columnDescriptor, string strOperand)
        {
            return value => null != value && !value.ToString().StartsWith(strOperand);
        }

        public static Type GetTypeToConvertOperandTo(ColumnDescriptor columnDescriptor)
        {
            if (null == columnDescriptor)
            {
                return typeof (string);
            }
            var columnType = columnDescriptor.WrappedPropertyType;
            columnType = Nullable.GetUnderlyingType(columnType) ?? columnType;
            Type typeToConvertTo;
            if (convertibleTypes.TryGetValue(columnType, out typeToConvertTo))
            {
                return typeToConvertTo;
            }
            return typeof (string);
        }

        public static object ConvertOperand(ColumnDescriptor columnDescriptor, string operand)
        {
            var type = GetTypeToConvertOperandTo(columnDescriptor);
            if (null == type)
            {
                return operand;
            }
            if (typeof(char) == type)
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
            if (value == null)
            {
                return null;
            }
            var type = GetTypeToConvertOperandTo(columnDescriptor);
            if (type == typeof (string))
            {
                return value.ToString();
            }
            return Convert.ChangeType(value, type);
        }

        class FilterOperation : IFilterOperation
        {
            private readonly Func<ColumnDescriptor, string, Predicate<object>> _fnMakePredicate;
            public FilterOperation(string opName, string displayName, Func<ColumnDescriptor, string, Predicate<object>> fnMakePredicate)
            {
                OpName = opName;
                DisplayName = displayName;
                _fnMakePredicate = fnMakePredicate;
            }
            public string OpName { get; private set; }
            public string DisplayName { get; private set; }
            public virtual bool IsValidFor(ColumnDescriptor columnDescriptor)
            {
                return true;
            }
            public virtual Type GetOperandType(ColumnDescriptor columnDescriptor)
            {
                return GetTypeToConvertOperandTo(columnDescriptor);
            }
            public Predicate<object> MakePredicate(ColumnDescriptor columnDescriptor, string operand)
            {
                return _fnMakePredicate(columnDescriptor, operand);
            }
        }
        class StringFilterOperation : FilterOperation
        {
            public StringFilterOperation(string opName, string displayName, Func<ColumnDescriptor, string, Predicate<object>> fnMakePredicate) : base(opName, displayName, fnMakePredicate)
            {
            }
            public override bool IsValidFor(ColumnDescriptor columnDescriptor)
            {
                return typeof (string) == columnDescriptor.WrappedPropertyType;
            }
        }

        class ComparisonFilterOperation : FilterOperation
        {
            public ComparisonFilterOperation(string opName, string displayName,
                Predicate<int> filterFunc)
                : base(opName, displayName, MakeFnCompare(filterFunc))
            {
            }

            public override bool IsValidFor(ColumnDescriptor columnDescriptor)
            {
                if (null == columnDescriptor)
                {
                    return false;
                }
                var columnType = columnDescriptor.WrappedPropertyType;
                columnType = Nullable.GetUnderlyingType(columnType) ?? columnType;
                return convertibleTypes.ContainsKey(columnType) && columnType != typeof (string);
            }
        }

        class UnaryFilterOperation : FilterOperation
        {
            public UnaryFilterOperation(string opName, string displayName,
                Func<ColumnDescriptor, string, Predicate<object>> fnMakePredicate)
                : base(opName, displayName, fnMakePredicate)
            {
                
            }

            public override Type GetOperandType(ColumnDescriptor columnDescriptor)
            {
                return null;
            }
        }
    }
}
