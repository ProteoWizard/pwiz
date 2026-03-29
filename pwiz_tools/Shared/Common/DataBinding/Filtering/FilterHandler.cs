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
        object DeserializeOperand(IFilterOperation operation, string text);
        string SerializeOperand(IFilterOperation operation, object operand);
        object ParseOperand(IFilterOperation operation, CultureInfo cultureInfo, string text);
        string OperandToString(IFilterOperation operation, CultureInfo cultureInfo, object operand);

        bool ValueEqualsOperand(object value, object operand);
        bool CanBeBlank { get; }

        /// <summary>
        /// Converts a list of string tokens (as parsed from a filter string) into a typed operand.
        /// </summary>
        object ParseOperandTokens(IFilterOperation operation, CultureInfo cultureInfo, IList<string> values);

        /// <summary>
        /// Converts a typed operand into a list of string tokens for serialization into a filter string.
        /// </summary>
        IList<string> OperandToTokens(IFilterOperation operation, CultureInfo cultureInfo, object operand);

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
    /// Provides default implementations of <see cref="ParseOperandTokens"/> and <see cref="OperandToTokens"/>
    /// for handlers that operate on single values. <see cref="ListFilterHandler"/> overrides these
    /// for multi-value operands.
    /// </summary>
    public abstract class FilterHandler : IFilterHandler
    {
        public abstract bool IsBlank(object value);

        public virtual object DeserializeOperand(IFilterOperation operation, string text)
        {
            return ParseOperand(operation, CultureInfo.InvariantCulture, text);
        }

        public virtual string SerializeOperand(IFilterOperation operation, object operand)
        {
            return OperandToString(operation, CultureInfo.InvariantCulture, operand);
        }
        public abstract object ParseOperand(IFilterOperation operation, CultureInfo cultureInfo, string text);
        public abstract string OperandToString(IFilterOperation operation, CultureInfo cultureInfo, object operand);
        public abstract bool ValueEqualsOperand(object value, object operand);
        public abstract bool CanBeBlank { get; }

        public virtual object ParseOperandTokens(IFilterOperation operation, CultureInfo cultureInfo, IList<string> values)
        {
            if (values.Count != 1)
            {
                throw new ArgumentException(
                    string.Format(@"Expected 1 value but got {0}", values.Count), nameof(values));
            }
            return ParseOperand(operation, cultureInfo, values[0]);
        }

        public virtual IList<string> OperandToTokens(IFilterOperation operation, CultureInfo cultureInfo, object operand)
        {
            return new[] { OperandToString(operation, cultureInfo, operand) };
        }
    }


    public abstract class FilterHandler<TColumn, TOperand> : FilterHandler
    {
        public sealed override string OperandToString(IFilterOperation operation, CultureInfo cultureInfo, object operand)
        {
            if (operand is TOperand tOperand)
            {
                return OperandToString(operation, cultureInfo, tOperand);
            }

            return string.Empty;
        }

        protected abstract string OperandToString(IFilterOperation operation, CultureInfo cultureInfo, TOperand operand);

        public override object ParseOperand(IFilterOperation operation, CultureInfo cultureInfo, string text)
        {
            return ParseTypedOperand(operation, cultureInfo, text);
        }

        protected abstract TOperand ParseTypedOperand(IFilterOperation operation, CultureInfo cultureInfo, string text);

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

        protected override string OperandToString(IFilterOperation operation, CultureInfo cultureInfo, string operand)
        {
            return operand;
        }

        protected override string ParseTypedOperand(IFilterOperation operation, CultureInfo cultureInfo, string text)
        {
            return text;
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

    public class IntegerFilterHandler : FilterHandler<double, double>, IFilterHandler.IComparison
    {
        public static readonly IntegerFilterHandler INSTANCE = new IntegerFilterHandler();
        public override bool IsBlank(object value)
        {
            return value == null;
        }
        public override object DeserializeOperand(IFilterOperation operation, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            return double.Parse(text, CultureInfo.InvariantCulture);
        }
        public override string SerializeOperand(IFilterOperation operation, object operand)
        {
            return (operand as double?)?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        }
        protected override string OperandToString(IFilterOperation operation, CultureInfo cultureInfo, double operand)
        {
            return operand.ToString(cultureInfo);
        }
        protected override double ParseTypedOperand(IFilterOperation operation, CultureInfo cultureInfo, string text)
        {
            return double.Parse(text, cultureInfo);
        }
        protected override bool ValueEqualsOperand(double value, double operand)
        {
            return value == operand;
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
        protected int? Compare(double columnValue, double doubleOperand)
        {
            return columnValue.CompareTo(doubleOperand);
        }
        public override bool CanBeBlank
        {
            get { return false; }
        }
    }

    public class NumericFilterHandler : FilterHandler<double, PrecisionNumber>, IFilterHandler.IComparison
    {
        public static readonly NumericFilterHandler INSTANCE = new NumericFilterHandler();
        public override bool IsBlank(object value)
        {
            return value == null;
        }

        public override object DeserializeOperand(IFilterOperation operation, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            return PrecisionNumber.Parse(text, CultureInfo.InvariantCulture, true);
        }

        public override string SerializeOperand(IFilterOperation operation, object operand)
        {
            return (operand as PrecisionNumber?)?.ToString(CultureInfo.InvariantCulture, true) ?? string.Empty;
        }

        protected override string OperandToString(IFilterOperation operation, CultureInfo cultureInfo, PrecisionNumber operand)
        {
            return operand.ToString(cultureInfo, ExplicitPrecision(operation));
        }

        protected override PrecisionNumber ParseTypedOperand(IFilterOperation operation, CultureInfo cultureInfo, string text)
        {
            return PrecisionNumber.Parse(text, cultureInfo, ExplicitPrecision(operation));
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

        private bool ExplicitPrecision(IFilterOperation filterOperation)
        {
            return !filterOperation.UsesEquality();
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

        public override object ParseOperand(IFilterOperation operation, CultureInfo cultureInfo, string text)
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
                ElementHandler.ParseOperand(FilterOperations.OP_EQUALS, cultureInfo, str)));
        }

        public override string OperandToString(IFilterOperation operation, CultureInfo cultureInfo, object operand)
        {
            var operandList = operand as IListColumnValue;
            if (operandList == null)
            {
                return string.Empty;
            }

            var strings = operandList.AsEnumerable()
                .Select(v => ElementHandler.OperandToString(FilterOperations.OP_EQUALS, cultureInfo, v))
                .ToImmutable();
            return ListColumnValue.ItemsToString(cultureInfo, strings);
        }



        public override object DeserializeOperand(IFilterOperation operation, string text)
        {
            if (text == null)
            {
                return null;
            }
            var strings = ListColumnValue.Parse(text, ListColumnValue.GetCsvSeparator(CultureInfo.InvariantCulture));
            if (strings == null)
            {
                return null;
            }

            return ListColumnValue.FromItems(strings.Items.Select(str =>
                ElementHandler.DeserializeOperand(FilterOperations.OP_EQUALS, str)));
        }

        public override string SerializeOperand(IFilterOperation operation, object operand)
        {
            var operandList = operand as IListColumnValue;
            if (operandList == null)
            {
                return string.Empty;
            }

            var strings = operandList.AsEnumerable()
                .Select(v => ElementHandler.SerializeOperand(FilterOperations.OP_EQUALS, v))
                .ToImmutable();
            return ListColumnValue.ItemsToString(CultureInfo.InvariantCulture, strings);
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

        public override object ParseOperandTokens(IFilterOperation operation, CultureInfo cultureInfo, IList<string> values)
        {
            return ListColumnValue.FromItems(
                values.Select(v => ElementHandler.ParseOperandTokens(FilterOperations.OP_EQUALS, cultureInfo, new[] { v })));
        }

        public override IList<string> OperandToTokens(IFilterOperation operation, CultureInfo cultureInfo, object operand)
        {
            var operandList = operand as IListColumnValue;
            if (operandList == null)
            {
                return new[] { string.Empty };
            }
            return operandList.AsEnumerable()
                .SelectMany(v => ElementHandler.OperandToTokens(FilterOperations.OP_EQUALS, cultureInfo, v))
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

        public override object ParseOperand(IFilterOperation operation, CultureInfo cultureInfo, string text)
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

        public override string OperandToString(IFilterOperation operation, CultureInfo cultureInfo, object operand)
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

        protected TypeConverter TypeConverter
        {
            get
            {
                return TypeDescriptor.GetConverter(ValueType);
            }
        }

        public override object DeserializeOperand(IFilterOperation operation, string text)
        {
            return TypeConverter.ConvertFromInvariantString(text);
        }

        public override string SerializeOperand(IFilterOperation operation, object operand)
        {
            return TypeConverter.ConvertToInvariantString(operand);
        }

        public override object ParseOperand(IFilterOperation operation, CultureInfo cultureInfo, string text)
        {
            return TypeConverter.ConvertFrom(null!, cultureInfo, text);
        }

        public override string OperandToString(IFilterOperation operation, CultureInfo cultureInfo, object operand)
        {
            return TypeConverter.ConvertToString(null!, cultureInfo, operand);
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
