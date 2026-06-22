using pwiz.Common.SystemUtil;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.CommonResources;
using pwiz.Common.DataBinding.Attributes;

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
            var doubleOperand = operand.ToDouble();
            if (Equals(value, doubleOperand))
            {
                return true;
            }

            // Multiply the tolerance by ten ninths so that the tolerance is effective 0.555555...
            // That way, we match any number which would be within 0.5 when rounded to some precision
            var tolerance = (double) (operand.Tolerance * 10 / 9);
            return value >= doubleOperand - tolerance && value <= doubleOperand + tolerance;
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
            // Operands produced by this infrastructure are IListColumnValue, not IList<object>
            if (listValue == null || !(operand is IListColumnValue listOperand))
            {
                return false;
            }
            var operandItems = listOperand.AsEnumerable().ToList();

            return listValue.Count >= operandItems.Count &&
                   ElementsEqual(listValue.AsEnumerable().Take(operandItems.Count), operandItems);
        }

        public bool Contains(object value, object operand)
        {
            var listValue = ToListValue(value);
            // Operands produced by this infrastructure are IListColumnValue, not IList<object>
            if (listValue == null || !(operand is IListColumnValue listOperand))
            {
                return false;
            }
            var operandItems = listOperand.AsEnumerable().ToList();

            if (operandItems.Count == 0)
            {
                return true;
            }

            var window = new List<object>();
            foreach (var item in listValue.AsEnumerable())
            {
                if (window.Count == operandItems.Count)
                {
                    window.RemoveAt(0);
                }
                window.Add(item);
                if (window.Count == operandItems.Count && ElementsEqual(window, operandItems))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Applies a comparison operator element-wise between a list-valued column and a list operand.
        /// Equal-length lists compare pairwise by index; when one side has a single element it is
        /// broadcast against every element of the other side; the criterion holds only if every element
        /// comparison holds. Lists of different lengths (neither of length one) never match.
        /// <paramref name="elementMatches"/> applies the operator (including its ComparisonMatches
        /// reduction) to one column element and one operand element.
        /// </summary>
        public bool MatchesComparison(object columnValue, object operandValue, Func<object, object, bool> elementMatches)
        {
            var values = ToListValue(columnValue)?.AsEnumerable().ToList();
            var operands = (operandValue as IListColumnValue)?.AsEnumerable().ToList();
            if (values == null || operands == null || values.Count == 0 || operands.Count == 0)
            {
                return false;
            }
            if (values.Count == operands.Count)
            {
                return values.Zip(operands, elementMatches).All(matched => matched);
            }
            // Broadcast a single element on either side against every element of the other.
            if (operands.Count == 1)
            {
                return values.All(value => elementMatches(value, operands[0]));
            }
            if (values.Count == 1)
            {
                return operands.All(operand => elementMatches(values[0], operand));
            }
            // Different lengths, neither broadcastable: no match.
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

    /// <summary>
    /// Thrown when a filter operand is invalid for its column's type (e.g. a negative value for a
    /// <see cref="PositiveNumber"/> column). A distinct type so callers that try parsing under several
    /// locales (e.g. SpectrumClassFilter.ParseFilterString) can surface this message immediately rather
    /// than retrying — the operand is invalid regardless of locale.
    /// </summary>
    public class FilterOperandException : FormatException
    {
        public FilterOperandException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// A numeric value that is constrained to be non-negative. Used as a column type so the data-binding
    /// filter (via <see cref="PositiveNumberFilterHandler"/>) rejects negative filter operands.
    /// </summary>
    [FilterHandler(typeof(PositiveNumberFilterHandler))]
    public readonly struct PositiveNumber : IFormattable, IComparable, IComparable<PositiveNumber>, IEquatable<PositiveNumber>
    {
        public PositiveNumber(double value)
        {
            Value = value;
        }

        public double Value { get; }

        public static implicit operator PositiveNumber(double value)
        {
            return new PositiveNumber(value);
        }

        public static implicit operator double(PositiveNumber positiveNumber)
        {
            return positiveNumber.Value;
        }

        public override string ToString()
        {
            return Value.ToString(CultureInfo.CurrentCulture);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return Value.ToString(format, formatProvider);
        }

        public int CompareTo(PositiveNumber other)
        {
            return Value.CompareTo(other.Value);
        }

        public int CompareTo(object obj)
        {
            return obj is PositiveNumber other ? CompareTo(other) : 1;
        }

        public bool Equals(PositiveNumber other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is PositiveNumber other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    /// <summary>
    /// Numeric filter handler for <see cref="PositiveNumber"/> columns: behaves like
    /// <see cref="NumericFilterHandler"/> but rejects a negative operand at parse time.
    /// </summary>
    public class PositiveNumberFilterHandler : NumericFilterHandler
    {
        public override object DeserializeOperand(IFilterOperation operation, string text)
        {
            return RejectNegative(base.DeserializeOperand(operation, text));
        }

        protected override PrecisionNumber ParseTypedOperand(IFilterOperation operation, CultureInfo cultureInfo, string text)
        {
            var operand = base.ParseTypedOperand(operation, cultureInfo, text);
            RejectNegative(operand);
            return operand;
        }

        protected override bool TryConvertColumnValue(object value, out double columnValue)
        {
            if (value is PositiveNumber positiveNumber)
            {
                columnValue = positiveNumber.Value;
                return true;
            }
            return base.TryConvertColumnValue(value, out columnValue);
        }

        private static object RejectNegative(object operand)
        {
            if (operand is PrecisionNumber precisionNumber)
            {
                RejectNegative(precisionNumber);
            }
            return operand;
        }

        private static void RejectNegative(PrecisionNumber operand)
        {
            if (operand.ToDouble() < 0)
            {
                throw new FilterOperandException(
                    MessageResources.PositiveNumberFilterHandler_RejectNegative_The_filter_value_must_be_a_positive_number_);
            }
        }
    }
}
