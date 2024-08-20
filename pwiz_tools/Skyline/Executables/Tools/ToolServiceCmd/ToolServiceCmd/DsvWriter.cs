using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ToolServiceCmd
{
    public static class DsvWriter
    {
        public static string ToDsvField(char separator, string text)
        {
            if (text == null)
                return string.Empty;
            if (text.IndexOfAny(new[] { '"', separator, '\r', '\n' }) == -1)
                return text;
            return '"' + text.Replace("\"", "\"\"") + '"'; // Not L10N
        }

        public static string ToDsvRow(char separator, IEnumerable<string> values)
        {
            return string.Join(new string(separator, 1), values.Select(value => ToDsvField(separator, value)));
        }
    }
}
