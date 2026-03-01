using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding
{
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

        public override string ToString()
        {
            var csvSeparator = CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalSeparator == @"," ? ';' : ',';
            return string.Join(csvSeparator.ToString(), Items.Select(item=>DsvWriter.ToDsvField(csvSeparator, item?.ToString() ?? string.Empty)));
        }
    }
}
