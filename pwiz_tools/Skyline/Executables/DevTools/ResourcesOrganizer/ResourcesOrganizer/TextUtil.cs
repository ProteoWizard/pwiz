using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourcesOrganizer
{
    public static class TextUtil
    {
        public static string Quote(string? s)
        {
            if (s == null)
            {
                return "null";
            }

            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        public static string ToDsvField(char separator, string? text)
        {
            if (text == null)
                return string.Empty;
            if (text.IndexOfAny(['"', separator, '\r', '\n']) == -1)
                return text;
            return '"' + text.Replace("\"", "\"\"") + '"';
        }


        public static string ToCsvRow(IEnumerable<string?> fields)
        {
            return string.Join(",", fields.Select(field => ToDsvField(',', field?.ToString())));
        }

        public static string ToCsvRow(params string?[] fields)
        {
            return ToCsvRow(fields.AsEnumerable());
        }
    }
}
