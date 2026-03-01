using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Filtering
{
    public interface IFilterHandler
    {
        bool IsBlank(object value);
        object ParseOperand(string text, CultureInfo cultureInfo);
        bool ValueEqualsOperand(object value, object operand);
        string OperandToString(object operand, CultureInfo cultureInfo);
        public interface ICompare
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
        object IFilterHandler.ParseOperand(string text,
            CultureInfo cultureInfo)
        {
            return ParseOperand(text, cultureInfo);
        }

        string IFilterHandler.OperandToString(object operand, CultureInfo cultureInfo)
        {
            if (operand is TOperand tOperand)
            {
                return OperandToString(tOperand, cultureInfo);
            }
            return string.Empty;
        }

        protected abstract string OperandToString(TOperand operand, CultureInfo cultureInfo);

        protected abstract TOperand ParseOperand(string text, CultureInfo cultureInfo);

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
        public static readonly WithContains WITH_CONTAINS = new TextFilterHandler.WithContains();
        public override bool IsBlank(object value)
        {
            return value == null || ValueEqualsOperand(string.Empty, value);
        }

        protected override string ParseOperand(string text, CultureInfo cultureInfo)
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

        protected override string OperandToString(string operand, CultureInfo cultureInfo)
        {
            return operand;
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

    public class NumericFilterHandler : FilterHandler<double, PrecisionNumber>, IFilterHandler.ICompare
    {
        public static readonly NumericFilterHandler INSTANCE = new NumericFilterHandler();
        public override bool IsBlank(object value)
        {
            return value == null;
        }

        protected override PrecisionNumber ParseOperand(string text, CultureInfo cultureInfo)
        {
            return PrecisionNumber.Parse(text, cultureInfo);
        }

        protected override string OperandToString(PrecisionNumber operand, CultureInfo cultureInfo)
        {
            return operand.ToString(cultureInfo);
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
            return precisionNumber.CompareTo(doubleValue);
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

        public object ParseOperand(string text, CultureInfo cultureInfo)
        {
            return ListColumnValue.ParseDsvFields(cultureInfo, text)
                .Select(field => ElementHandler.ParseOperand(field, cultureInfo)).ToList();
        }

        public bool ValueEqualsOperand(object value, object operand)
        {
            if (!(value is IListColumnValue listValue) || !(operand is IList<object> listOperand))
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
                    .All(item => ElementHandler.ValueEqualsOperand(item, listOperand[0]));
            }

            if (listOperand.Count != listValue.Count)
            {
                return false;
            }
            return ElementsEqual(listValue.AsEnumerable(), listOperand);
        }

        public string OperandToString(object operand, CultureInfo cultureInfo)
        {
            if (operand is IList<object> list)
            {
                return ListColumnValue.ItemsToString(cultureInfo,
                    list.Select(item => ElementHandler.OperandToString(item, cultureInfo)));
            }

            return string.Empty;
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

        public class EnumFilterHandler : IFilterHandler
        {
            public EnumFilterHandler(Type enumType)
            {
                ValueType = enumType;
            }

            public Type ValueType { get; }
            public int? CompareTo(object value)
            {
                return null;
            }

            public string ToString(CultureInfo cultureInfo)
            {
                throw new NotImplementedException();
            }
        }
    }
}
