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
    }
}
