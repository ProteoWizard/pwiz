using System;
using System.Globalization;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Filtering
{
    public interface IFilterHandler
    {
        bool IsBlank(object value);
        object ParseOperand(IFilterOperation filterOperation, string text, CultureInfo cultureInfo);
        bool Equals(object value, IFilterOperand operand);
        string OperandToString(object operand, CultureInfo cultureInfo);
    }

    public interface ICompareFilter
    {
        int? Compare(object value, IFilterOperand operand);
    }

    public interface IContainsFilter
    {
        bool StartsWith(object value, object operand);
        bool Contains(object value, object operand);
    }

    public abstract class FilterHandler<TColumn, TOperand> : IFilterHandler
    {
        object IFilterHandler.ParseOperand(IFilterOperation filterOperation, string text,
            CultureInfo cultureInfo)
        {
            return ParseOperand(filterOperation, text, cultureInfo);
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

        protected abstract TOperand ParseOperand(IFilterOperation filterOperation, string text,
            CultureInfo cultureInfo);

        public abstract bool IsBlank(object value);

        public bool Equals(object value, IFilterOperand operand)
        {
            return CallWithOperand(value, operand, Equals);
        }

        protected abstract bool Equals(TColumn value, TOperand operand);

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

    public class StringFilterHandler : FilterHandler<string, string>, IContainsFilter
    {
        public override bool IsBlank(object value)
        {
            return value == null || Equals(string.Empty, value);
        }

        protected override string ParseOperand(IFilterOperation filterOperation, string text, CultureInfo cultureInfo)
        {
            return text;
        }


        protected override bool Equals(string value, string operand)
        {
            return StringComparer.Ordinal.Equals(value, operand);
        }

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
    }

    public class NumericFilterHandler : FilterHandler<double, PrecisionNumber>, ICompareFilter
    {
        public override bool IsBlank(object value)
        {
            return value == null;
        }

        protected override PrecisionNumber ParseOperand(IFilterOperation filterOperation, string text, CultureInfo cultureInfo)
        {
            return PrecisionNumber.Parse(text, cultureInfo);
        }

        protected override string OperandToString(PrecisionNumber operand, CultureInfo cultureInfo)
        {
            return operand.ToString(cultureInfo);
        }

        protected override bool Equals(double value, PrecisionNumber operand)
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

        public int? Compare(object value, IFilterOperand operand)
        {
            return CallWithOperand(value, operand, (doubleValue, precisionNumber) => -precisionNumber.CompareTo(doubleValue));
        }
    }

    
}
