using pwiz.Common.SystemUtil;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace pwiz.Common.DataBinding.Filtering
{
    public interface IFilterHandler
    {
        bool IsBlank(object value);
        object ParseOperand(IFilterOperation operation, string text, CultureInfo cultureInfo);
        string OperandToString(IFilterOperation operation, object operand, CultureInfo cultureInfo);
        bool ValueEqualsOperand(object value, object operand);
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


    public abstract class FilterHandler<TColumn, TOperand> : IFilterHandler
    {
        object IFilterHandler.ParseOperand(IFilterOperation operation, string text, CultureInfo cultureInfo)
        {
            return ParseOperand(operation, text, cultureInfo);
        }

        protected abstract TOperand ParseOperand(IFilterOperation operation, string text, CultureInfo cultureInfo);

        public string OperandToString(IFilterOperation operation, object operand, CultureInfo cultureInfo)
        {
            if (operand is TOperand tOperand)
            {
                return OperandToString(operation, tOperand, cultureInfo);
            }
            return string.Empty;
        }

        protected abstract string OperandToString(IFilterOperation operation, TOperand operand, CultureInfo cultureInfo);

        public abstract bool IsBlank(object value);

        public bool ValueEqualsOperand(object value, object operand)
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


        protected override string ParseOperand(IFilterOperation operation, string text, CultureInfo cultureInfo)
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

        protected override PrecisionNumber ParseOperand(IFilterOperation filterOperation, string text, CultureInfo cultureInfo)
        {
            return PrecisionNumber.Parse(text, cultureInfo, ScientificPrecisionOnly(filterOperation, cultureInfo));
        }

        protected override string OperandToString(IFilterOperation operation, PrecisionNumber operand, CultureInfo cultureInfo)
        {
            return operand.ToString(cultureInfo, ScientificPrecisionOnly(operation, cultureInfo));
        }

        protected override bool ValueEqualsOperand(double value, PrecisionNumber operand)
        {
            return operand.EqualsWithinPrecision(value);
        }

        protected override bool TryConvertColumnValue(object value, out double columnValue)
        {
            try
            {
                columnValue = Convert.ToDouble(value);
                return true;
            }
            catch
            {
                columnValue = 0;
                return false;
            }
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

        private bool ScientificPrecisionOnly(IFilterOperation filterOperation, CultureInfo cultureInfo)
        {
            return string.IsNullOrEmpty(cultureInfo.Name) || !filterOperation.UsesEquality();
        }
    }

    public class ListFilterHandler : IFilterHandler, IFilterHandler.IContains
    {
        public ListFilterHandler(IFilterHandler elementHandler)
        {
            ElementHandler = elementHandler;
        }

        public IFilterHandler ElementHandler { get; }

        public bool IsBlank(object value)
        {
            return 0 == ((value as IListColumnValue)?.Count ?? 0);
        }

        public object ParseOperand(IFilterOperation operation, string text, CultureInfo cultureInfo)
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

            return new ListColumnValue<object>(
                strings.Items.Select(str => ElementHandler.ParseOperand(FilterOperations.OP_EQUALS, str, cultureInfo)));
        }

        public bool ValueEqualsOperand(object value, object operand)
        {
            if (!(value is IListColumnValue listValue) || !(operand is IListColumnValue listOperand))
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

        public string OperandToString(IFilterOperation operation, object operand, CultureInfo cultureInfo)
        {
            var operandList = operand as IListColumnValue;
            if (operandList == null)
            {
                return string.Empty;
            }

            return new ListColumnValue<string>(operandList.AsEnumerable()
                .Select(v => ElementHandler.OperandToString(FilterOperations.OP_EQUALS, operand, cultureInfo))).ToString();
        }

        public bool StartsWith(object value, object operand)
        {
            if (!(value is IListColumnValue listValue) || !(operand is IList<object> listOperand))
            {
                return false;
            }

            return listValue.Count >= listOperand.Count &&
                   ElementsEqual(listValue.AsEnumerable().Take(listOperand.Count), listOperand);
        }

        public bool Contains(object value, object operand)
        {
            if (!(value is IListColumnValue listValue) || !(operand is IList<object> listOperand))
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

        private bool ElementsEqual(IEnumerable<object> items, IList<object> operands)
        {
            return items.Zip(operands, (item, operand) =>
                ElementHandler.ValueEqualsOperand(item, operand)).All(result => result);
        }

    }
    public class EnumFilterHandler : IFilterHandler
    {
        public EnumFilterHandler(Type enumType)
        {
            EnumType = enumType;
        }

        public Type EnumType { get; }
        public bool IsBlank(object value)
        {
            return value == null;
        }

        public object ParseOperand(IFilterOperation operation, string text, CultureInfo cultureInfo)
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

        public string OperandToString(IFilterOperation operation, object operand, CultureInfo cultureInfo)
        {
            return operand?.ToString() ?? string.Empty;
        }

        public bool ValueEqualsOperand(object value, object operand)
        {
            return Equals(value, operand);
        }

        public string OperandToString(object operand, CultureInfo cultureInfo)
        {
            return operand?.ToString() ?? string.Empty;
        }
    }

    public class SimpleFilterHandler : IFilterHandler
    {
        public SimpleFilterHandler(Type type)
        {
            ValueType = type;
        }

        public Type ValueType { get; }

        public bool IsBlank(object value)
        {
            return value == null;
        }

        public object ParseOperand(IFilterOperation operation, string text, CultureInfo cultureInfo)
        {
            var typeConverter = TypeDescriptor.GetConverter(ValueType);
            // ReSharper disable AssignNullToNotNullAttribute
            return typeConverter.ConvertFrom(null, cultureInfo, text);
        }

        public string OperandToString(IFilterOperation operation, object operand, CultureInfo cultureInfo)
        {
            return Convert.ToString(operand, cultureInfo);
        }

        public bool ValueEqualsOperand(object value, object operand)
        {
            return Equals(value, operand);
        }

        public string OperandToString(object operand, CultureInfo cultureInfo)
        {
            return Convert.ToString(operand, cultureInfo);
        }
    }
}
