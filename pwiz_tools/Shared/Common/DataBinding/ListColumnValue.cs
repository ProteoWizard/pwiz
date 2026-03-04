using pwiz.Common.Collections;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;

namespace pwiz.Common.DataBinding
{
    public static class ListColumnValue
    {
        public static Type GetElementType(Type type)
        {
            if (!typeof(IListColumnValue).IsAssignableFrom(type))
            {
                return null;
            }
            for (var t = type; t != null; t = t.BaseType)
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ListColumnValue<>))
                {
                    return t.GetGenericArguments()[0];
                }
            }
            return null;
        }

        public static char GetCsvSeparator(IFormatProvider formatProvider)
        {
            var numberDecimalSeparator =
                ((formatProvider ?? CultureInfo.CurrentCulture).GetFormat(typeof(NumberFormatInfo)) as NumberFormatInfo)
                ?.NumberDecimalSeparator;
            return numberDecimalSeparator == @"," ? ';' : ',';
        }

        public static ListColumnValue<T> FromItems<T>(IEnumerable<T> items)
        {
            if (items == null)
            {
                return null;
            }

            return new Impl<T>(items);
        }

        public static string ItemsToString<T>(IFormatProvider formatProvider, IEnumerable<T> items)
        {
            if (items == null)
            {
                return string.Empty;
            }
            var csvSeparator = GetCsvSeparator(formatProvider);
            return string.Join(csvSeparator.ToString(), items.Select(item => DsvWriter.ToDsvField(csvSeparator, Convert.ToString(item))));
        }

        public static ListColumnValue<string> Parse(string line, char separator)
        {
            var list = new List<string>();
            var sbField = new StringBuilder();
            bool inQuotes = false;
            for (var chIndex = 0; chIndex < line.Length; chIndex++)
            {
                var ch = line[chIndex];
                if (ch == '"')
                {
                    if (chIndex + 1 < line.Length && line[chIndex + 1] == '"')
                    {
                        sbField.Append(ch);
                        chIndex++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (ch == separator && !inQuotes)
                {
                    list.Add(Unquote(sbField.ToString()));
                    sbField.Clear();
                }
                else
                {
                    sbField.Append(ch);
                }
            }

            list.Add(Unquote(sbField.ToString()));
            return new Impl<string>(list);
        }

        private static string Unquote(string value)
        {
            if (value.Length > 1 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                return value.Substring(1, value.Length - 2).Replace(@"""""", @"""");
            }

            return value;
        }

        /// <summary>
        /// Subclass of ListColumnValue which implements IFormattable.
        /// This is hidden in a private class so the declared type of the Property will
        /// not implement IFormattable so the user will not be presented with options to
        /// customize the formatting of the column. We only care about the IFormatProvider
        /// in the ToString override.
        /// </summary>
        private class Impl<T> : ListColumnValue<T>, IFormattable
        {
            public Impl(IEnumerable<T> list) : base(list)
            {
            }

            string IFormattable.ToString(string format, IFormatProvider formatProvider)
            {
                return ItemsToString(formatProvider, Items);
            }
        }
    }

    public interface IListColumnValue
    {
        Array ToArray();
        int Count { get; }
        IEnumerable<object> AsEnumerable();
    }

    public abstract class ListColumnValue<T> : IListColumnValue
    {
        protected ListColumnValue(IEnumerable<T> items)
        {
            Items = ImmutableList.ValueOf(items);
        }

        [Browsable(false)] public ImmutableList<T> Items { get; }

        Array IListColumnValue.ToArray()
        {
            return Items?.ToArray();
        }

        int IListColumnValue.Count
        {
            get { return Items.Count; }
        }

        IEnumerable<object> IListColumnValue.AsEnumerable()
        {
            return Items.Cast<object>();
        }


        public override string ToString()
        {
            if (Items == null)
            {
                return string.Empty;
            }

            return ListColumnValue.ItemsToString(CultureInfo.CurrentCulture, Items);
        }

        protected bool Equals(ListColumnValue<T> other)
        {
            return Equals(Items, other.Items);
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((ListColumnValue<T>)obj);
        }

        public override int GetHashCode()
        {
            return Items != null ? Items.GetHashCode() : 0;
        }
    }
}
