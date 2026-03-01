using pwiz.Common.Collections;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace pwiz.Common.DataBinding
{
    public static class ListColumnValue
    {
        public static Type GetElementType(Type type)
        {
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
            var csvSeparator = ListColumnValue.GetCsvSeparator(cultureInfo);
            return string.Join(csvSeparator.ToString(), items.Select(item => DsvWriter.ToDsvField(csvSeparator, item?.ToString() ?? string.Empty)));
        }

        public static IEnumerable<string> ParseDsvFields(string line, char separator)
        {

        }
    }

    public class ListColumnValue<T>
    {
        public ListColumnValue(IEnumerable<T> items)
        {
            Items = ImmutableList.ValueOf(items);
        }

        [Browsable(false)]
        public ImmutableList<T> Items
        {
            get;
        }

        public T[] ToArray()
        {
            return Items?.ToArray();
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
