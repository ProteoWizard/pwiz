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

        public static char GetCsvSeparator(CultureInfo cultureInfo)
        {
            return cultureInfo.NumberFormat.CurrencyDecimalSeparator == @"," ? ';' : ',';
        }

        public static string ItemsToString<T>(CultureInfo cultureInfo, IEnumerable<T> items)
        {
            var csvSeparator = GetCsvSeparator(cultureInfo);
            return string.Join(csvSeparator.ToString(), items.Select(item => DsvWriter.ToDsvField(csvSeparator, item?.ToString() ?? string.Empty)));
        }

        public static IEnumerable<string> ParseDsvFields(CultureInfo cultureInfo, string line)
        {
            return ParseDsvFields(line, GetCsvSeparator(cultureInfo));
        }

        public static IEnumerable<string> ParseDsvFields(string line, char separator)
        {
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
                    yield return Unquote(sbField.ToString());
                    sbField.Clear();
                }
                else
                {
                    sbField.Append(ch);
                }
            }
            yield return Unquote(sbField.ToString());
        }

        private static string Unquote(string value)
        {
            if (value.Length > 1 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                return value.Substring(1, value.Length - 2).Replace(@"""""", @"""");
            }

            return value;
        }
    }

    public interface IListColumnValue
    {
        Array ToArray();
        int Count { get; }
        IEnumerable<object> AsEnumerable();
    }

    public class ListColumnValue<T> : IListColumnValue
    {
        public ListColumnValue(IEnumerable<T> items)
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
    }
}
