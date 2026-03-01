using System;
using System.Globalization;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Filtering
{
    public interface IFilterOperand
    {
        Type ValueType { get; }
        int? CompareTo(object value);
        string ToString(CultureInfo cultureInfo);
    }

    public interface IListFilterOperand
    {
        Type ListElementType { get; }
    }

    public abstract class FilterOperand : Immutable, IFilterOperand
    {
        protected FilterOperand(Type valueType)
        {
            ValueType = valueType;
        }

        public Type ValueType { get; }
        public abstract int? CompareTo(object value);
        public abstract string ToString(CultureInfo cultureInfo);
    }


    public abstract class FilterOperand<T> : FilterOperand, IFilterOperand
    {
        public FilterOperand() : base(typeof(T))
        {

        }
        protected abstract bool TryConvert(object value, out T tValue);
        public override int? CompareTo(object value)
        {
            if (value == null)
            {
                return null;
            }
            if (!TryConvert(value, out T tValue))
            {
                return null;
            }

            return CompareTo(tValue);
        }

        protected abstract int? CompareTo(T value);
    }



    public class NumericFilterOperand : FilterOperand<double>
    {
        private PrecisionNumber _precisionNumber;

        public NumericFilterOperand(PrecisionNumber precisionNumber)
        {
            _precisionNumber = precisionNumber;
        }

        protected override bool TryConvert(object value, out double tValue)
        {
            try
            {
                tValue = Convert.ToDouble(value);
                return true;
            }
            catch
            {
                tValue = 0;
                return false;
            }
        }

        protected override int? CompareTo(double value)
        {
            return _precisionNumber.CompareTo(value);
        }

        public override string ToString(CultureInfo cultureInfo)
        {
            return _precisionNumber.ToString(cultureInfo);
        }
    }

    // public class ListFilterOperand : FilterOperand
    // {
    //     public ListFilterOperand(IEnumerable<IFilterOperand> elements) : base(typeof(object[]))
    //     {
    //         Elements = elements.ToImmutable();
    //     }
    //
    //     public ImmutableList<IFilterOperand> Elements { get; private set; }
    //
    //     public override int? CompareTo(object value)
    //     {
    //         int count = Math.Min(Elements.Count, value.Length);
    //         for (int i = 0; i < count; i++)
    //         {
    //             var comparison = Elements[i].CompareTo(value[i]);
    //             if (comparison != 0)
    //             {
    //                 return comparison;
    //             }
    //         }
    //         return Elements.Count.CompareTo(value.Length);
    //     }
    //
    //     protected override bool IsEqualTo(object[] value)
    //     {
    //         if (Elements.Count != value.Length)
    //         {
    //             return false;
    //         }
    //
    //         for (int i = 0; i < Elements.Count; i++)
    //         {
    //             if (!Elements[i].IsEqualTo(value[i]))
    //             {
    //                 return false;
    //             }
    //         }
    //
    //         return true;
    //     }
    //
    //     protected override bool IsGreaterThan(object[] value)
    //     {
    //         for (int i = 0; i < Math.Min(Elements.Count, value.Length); i++)
    //         {
    //             if (Elements[i].IsGreaterThan(value[i]))
    //             {
    //                 continue;
    //             }
    //
    //             if (!Elements[i].IsEqualTo(value[i]))
    //             {
    //
    //             }
    //         }
    //     }
    // }

    public class TextFilterOperand : FilterOperand<string>
    {
        public TextFilterOperand(string text)
        {
            Text = text;
        }

        public string Text { get; }
        protected override bool TryConvert(object value, out string tValue)
        {
            tValue = value?.ToString();
            return true;
        }

        protected override int? CompareTo(string value)
        {
            return StringComparer.Ordinal.Compare(Text, value);
        }

        public override string ToString(CultureInfo cultureInfo)
        {
            return Text;
        }
    }

    public class LiteralFilterOperand<T> : FilterOperand<T>
    {
        public LiteralFilterOperand(T value)
        {
            Value = value;
        }

        public T Value { get; }
        public override string ToString(CultureInfo cultureInfo)
        {
            if (Value is IFormattable formattable)
            {
                return formattable.ToString(null, cultureInfo);
            }
            return Value?.ToString() ?? string.Empty;
        }

        protected override bool TryConvert(object value, out T tValue)
        {
            tValue = default;
            if (value == null)
            {
                return false;
            }

            try
            {
                tValue = (T)Convert.ChangeType(value, typeof(T));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        protected override int? CompareTo(T value)
        {
            if (Equals(Value, value))
            {
                return 0;
            }

            if (Value is IComparable<T> genericComparable)
            {
                return genericComparable.CompareTo(value);
            }

            if (Value is IComparable comparable)
            {
                try
                {
                    return comparable.CompareTo(value);
                }
                catch
                {
                    // ignore
                }
            }

            return null;
        }
    }
}
