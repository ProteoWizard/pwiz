using pwiz.Common.SystemUtil;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding.Filtering
{
    public interface IFilterHandler
    {
        bool IsBlank(object value);
        object ParseOperand(IFilterOperation operation, string text, CultureInfo cultureInfo);
        string OperandToString(IFilterOperation operation, object operand, CultureInfo cultureInfo);
        bool ValueEqualsOperand(object value, object operand);
        bool CanBeBlank { get; }

        /// <summary>
        /// Converts a list of string tokens (as parsed from a filter string) into a typed operand.
        /// </summary>
        object GetOperand(IFilterOperation operation, IList<string> values);

        /// <summary>
        /// Converts a typed operand into a list of string tokens for serialization into a filter string.
        /// </summary>
        IList<string> OperandToList(IFilterOperation operation, object operand);

        public interface IComparison
        {
            int? Compare(object value, object operand);
        }

        public interface IContains
        {
            bool StartsWith(object value, object operand);
            bool Contains(object value, object operand);
        }
    }

    /// <summary>
    /// Base class for all <see cref="IFilterHandler"/> implementations.
    /// Provides default implementations of <see cref="GetOperand"/> and <see cref="OperandToList"/>
    /// for handlers that operate on single values. <see cref="ListFilterHandler"/> overrides these
    /// for multi-value operands.
    /// </summary>
    public abstract class FilterHandler : IFilterHandler
    {
        public abstract bool IsBlank(object value);
        public abstract object ParseOperand(IFilterOperation operation, string text, CultureInfo cultureInfo);
        public abstract string OperandToString(IFilterOperation operation, object operand, CultureInfo cultureInfo);
        public abstract bool ValueEqualsOperand(object value, object operand);
        public abstract bool CanBeBlank { get; }

        public virtual object GetOperand(IFilterOperation operation, IList<string> values)
        {
            if (values.Count != 1)
            {
                throw new ArgumentException(
                    string.Format(@"Expected 1 value but got {0}", values.Count), nameof(values));
            }
            return ParseOperand(operation, values[0], CultureInfo.InvariantCulture);
        }

        public virtual IList<string> OperandToList(IFilterOperation operation, object operand)
        {
            return new[] { OperandToString(operation, operand, CultureInfo.InvariantCulture) };
        }
    }


    public abstract class FilterHandler<TColumn, TOperand> : FilterHandler
    {
        public sealed override object ParseOperand(IFilterOperation operation, string text, CultureInfo cultureInfo)
        {
            return ParseTypedOperand(operation, text, cultureInfo);
        }

        protected abstract TOperand ParseTypedOperand(IFilterOperation operation, string text, CultureInfo cultureInfo);

        public sealed override string OperandToString(IFilterOperation operation, object operand, CultureInfo cultureInfo)
        {
            if (operand is TOperand tOperand)
            {
                return OperandToString(operation, tOperand, cultureInfo);
            }
            return string.Empty;
        }

        protected abstract string OperandToString(IFilterOperation operation, TOperand operand, CultureInfo cultureInfo);

        public sealed override bool ValueEqualsOperand(object value, object operand)
        {
            return CallWithOperand(value, operand, ValueEqualsOperand);
        }

        protected abstract bool ValueEqualsOperand(TColumn value, TOperand operand);

        protected T CallWithOperand<T>(object value, object operand, Func<TColumn, TOperand, T> func)
        {
            if (TryConvertColumnValue(value, out var columnValue) && operand is TOperand typedOperand)
            {
                return func(columnValue, typedOperand);
            }
            return default;
        }

        protected abstract bool TryConvertColumnValue(object value, out TColumn columnValue);
    }

    public class TextFilterHandler : FilterHandler<string, string>
    {
        public static readonly TextFilterHandler WITHOUT_CONTAINS = new TextFilterHandler();
        public static readonly WithContains WITH_CONTAINS = new WithContains();
        public override bool IsBlank(object value)
        {
            return value == null || ValueEqualsOperand(string.Empty, value);
        }

        public override bool CanBeBlank
        {
            get { return true; }
        }

        protected override string ParseTypedOperand(IFilterOperation operation, string text, CultureInfo cultureInfo)
        {
            return text;
        }

        protected override string OperandToString(IFilterOperation operation, string operand, CultureInfo cultureInfo)
        {
            return operand;
        }

        protected override bool ValueEqualsOperand(string value, string operand)
        {
            return StringComparer.Ordinal.Equals(value, operand);
        }

        protected override bool TryConvertColumnValue(object value, out string columnValue)
        {
            if (value == null)
            {
                columnValue = string.Empty;
                return false;
            }
            columnValue = value as string ?? value.ToString();
            return true;
        }

        public class WithContains : TextFilterHandler, IFilterHandler.IContains
        {
            public bool StartsWith(object value, object operand)
            {
                return CallWithOperand(value, operand,
                    (stringValue, stringOperand) => stringValue?.StartsWith(stringOperand ?? string.Empty) ?? false);
            }

            public bool Contains(object value, object operand)
            {
                return CallWithOperand(value, operand,
                    (stringValue, stringOperand) => stringValue?.Contains(stringOperand ?? string.Empty) ?? false);
            }
        }
    }

    public class NumericFilterHandler : FilterHandler<double, PrecisionNumber>, IFilterHandler.IComparison
    {
        public static readonly NumericFilterHandler INSTANCE = new NumericFilterHandler();
        public override bool IsBlank(object value)
        {
            return value == null;
        }

        protected override PrecisionNumber ParseTypedOperand(IFilterOperation filterOperation, string text, CultureInfo cultureInfo)
        {
            return PrecisionNumber.Parse(text, cultureInfo, ExplicitPrecision(filterOperation, cultureInfo));
        }

        protected override string OperandToString(IFilterOperation operation, PrecisionNumber operand, CultureInfo cultureInfo)
        {
            return operand.ToString(cultureInfo, ExplicitPrecision(operation, cultureInfo));
        }

        protected override bool ValueEqualsOperand(double value, PrecisionNumber operand)
        {
            return operand.EqualsWithinPrecision(value);
        }

        protected override bool TryConvertColumnValue(object value, out double columnValue)
        {
            if (value != null)
            {
                try
                {
                    columnValue = Convert.ToDouble(value);
                    return true;
                }
                catch
                {
                    // ignore
                }
            }
            columnValue = 0;
            return false;
        }

        public int? Compare(object value, object operand)
        {
            return CallWithOperand(value, operand, Compare);
        }

        protected int? Compare(double doubleValue, PrecisionNumber precisionNumber)
        {
            // Return the negative of the result because we want the doubleValue compared to the precisionNumber
            return -precisionNumber.CompareTo(doubleValue);
        }

        private bool ExplicitPrecision(IFilterOperation filterOperation, CultureInfo cultureInfo)
        {
            return string.IsNullOrEmpty(cultureInfo.Name) || !filterOperation.UsesEquality();
        }

        public override bool CanBeBlank
        {
            get { return false; }
        }
    }

    public class ListFilterHandler : FilterHandler, IFilterHandler.IContains
    {
        public ListFilterHandler(IFilterHandler elementHandler)
        {
            ElementHandler = elementHandler;
        }

        public IFilterHandler ElementHandler { get; }

        public override bool IsBlank(object value)
        {
            return 0 == ((value as IListColumnValue)?.Count ?? 0);
        }

        public override object ParseOperand(IFilterOperation operation, string text, CultureInfo cultureInfo)
        {
            if (text == null)
            {
                return null;
            }

            var strings = ListColumnValue.Parse(text, ListColumnValue.GetCsvSeparator(cultureInfo));
            if (strings == null)
            {
                return null;
            }

            return ListColumnValue.FromItems(strings.Items.Select(str =>
                ElementHandler.ParseOperand(FilterOperations.OP_EQUALS, str, cultureInfo)));
        }

        public override bool ValueEqualsOperand(object value, object operand)
        {
            var listValue = ToListValue(value);
            if (listValue == null)
            {
                return false;
            }
            if (!(operand is IListColumnValue listOperand))
            {
                return false;
            }

            if (listOperand.Count == 0)
            {
                return listValue.Count == 0;
            }

            if (listOperand.Count == 1)
            {
                return listValue.Count > 0 && listValue.AsEnumerable()
                    .All(item => ElementHandler.ValueEqualsOperand(item, listOperand.AsEnumerable().First()));
            }

            if (listOperand.Count != listValue.Count)
            {
                return false;
            }
            return ElementsEqual(listValue.AsEnumerable(), listOperand.AsEnumerable().ToList());
        }

        public override string OperandToString(IFilterOperation operation, object operand, CultureInfo cultureInfo)
        {
            var operandList = operand as IListColumnValue;
            if (operandList == null)
            {
                return string.Empty;
            }

            var strings = operandList.AsEnumerable()
                .Select(v => ElementHandler.OperandToString(FilterOperations.OP_EQUALS, v, cultureInfo))
                .ToImmutable();
            return ListColumnValue.ItemsToString(cultureInfo, strings);
        }

        public bool StartsWith(object value, object operand)
        {
            var listValue = ToListValue(value);
            if (listValue == null || !(operand is IList<object> listOperand))
            {
                return false;
            }

            return listValue.Count >= listOperand.Count &&
                   ElementsEqual(listValue.AsEnumerable().Take(listOperand.Count), listOperand);
        }

        public bool Contains(object value, object operand)
        {
            var listValue = ToListValue(value);
            if (listValue == null || !(operand is IList<object> listOperand))
            {
                return false;
            }

            if (listOperand.Count == 0)
            {
                return true;
            }

            var window = new List<object>();
            foreach (var item in listValue.AsEnumerable())
            {
                if (window.Count == listOperand.Count)
                {
                    window.RemoveAt(0);
                }
                window.Add(item);
                if (window.Count == listOperand.Count && ElementsEqual(window, listOperand))
                {
                    return true;
                }
            }

            return false;
        }

        public override object GetOperand(IFilterOperation operation, IList<string> values)
        {
            return ListColumnValue.FromItems(
                values.Select(v => ElementHandler.GetOperand(FilterOperations.OP_EQUALS, new[] { v })));
        }

        public override IList<string> OperandToList(IFilterOperation operation, object operand)
        {
            var operandList = operand as IListColumnValue;
            if (operandList == null)
            {
                return new[] { string.Empty };
            }
            return operandList.AsEnumerable()
                .SelectMany(v => ElementHandler.OperandToList(FilterOperations.OP_EQUALS, v))
                .ToArray();
        }

        public override bool CanBeBlank
        {
            get { return true; }
        }

        protected virtual IListColumnValue ToListValue(object columnValue)
        {
            return columnValue as IListColumnValue;
        }

        private bool ElementsEqual(IEnumerable<object> items, IList<object> operands)
        {
            return items.Zip(operands, (item, operand) =>
                ElementHandler.ValueEqualsOperand(item, operand)).All(result => result);
        }
    }

    public class EnumFilterHandler : FilterHandler
    {
        public EnumFilterHandler(Type enumType)
        {
            EnumType = enumType;
        }

        public Type EnumType { get; }
        public override bool IsBlank(object value)
        {
            return value == null;
        }

        public override object ParseOperand(IFilterOperation operation, string text, CultureInfo cultureInfo)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            try
            {
                return Enum.Parse(EnumType, text);
            }
            catch
            {
                return Enum.Parse(EnumType, text, true);
            }
        }

        public override string OperandToString(IFilterOperation operation, object operand, CultureInfo cultureInfo)
        {
            return operand?.ToString() ?? string.Empty;
        }

        public override bool ValueEqualsOperand(object value, object operand)
        {
            return Equals(value, operand);
        }

        public override bool CanBeBlank
        {
            get { return false; }
        }
    }

    public class SimpleFilterHandler : FilterHandler
    {
        public SimpleFilterHandler(Type type)
        {
            ValueType = type;
        }

        public Type ValueType { get; }

        public override bool IsBlank(object value)
        {
            return value == null;
        }

        public override object ParseOperand(IFilterOperation operation, string text, CultureInfo cultureInfo)
        {
            var typeConverter = TypeDescriptor.GetConverter(ValueType);
            // ReSharper disable AssignNullToNotNullAttribute
            return typeConverter.ConvertFrom(null, cultureInfo, text);
        }

        public override string OperandToString(IFilterOperation operation, object operand, CultureInfo cultureInfo)
        {
            return Convert.ToString(operand, cultureInfo);
        }

        public override bool ValueEqualsOperand(object value, object operand)
        {
            return Equals(value, operand);
        }

        public override bool CanBeBlank
        {
            get
            {
                return !ValueType.IsValueType;
            }
        }

        public class Comparable : SimpleFilterHandler, IFilterHandler.IComparison
        {
            public Comparable(Type type) : base(type)
            {
            }

            public int? Compare(object value, object operand)
            {
                return (value as IComparable)?.CompareTo(operand);
            }
        }
    }
}
