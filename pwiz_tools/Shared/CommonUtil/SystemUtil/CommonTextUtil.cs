using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Common.SystemUtil
{
    public static class CommonTextUtil
    {
        /// <summary>
        /// This function can be used as a replacement for String.Join(" ", ...)
        /// </summary>
        /// <param name="values">A set of strings to be separated by spaces</param>
        /// <returns>A single string containing the original set separated by spaces</returns>
        public static string SpaceSeparate(params string[] values)
        {
            return SpaceSeparate(values.AsEnumerable());
        }

        public static string SpaceSeparate(IEnumerable<string> values)
        {
            var sb = new StringBuilder();
            foreach (string value in values)
            {
                if (sb.Length > 0)
                    sb.Append(@" ");
                sb.Append(value);
            }
            return sb.ToString();
        }
        /// <summary>
        /// This function can be used as a replacement for String.Join("\n", ...)
        /// </summary>
        /// <param name="lines">A set of strings to be on separate lines</param>
        /// <returns>A single string containing the original set separated by new lines</returns>
        public static string LineSeparate(IEnumerable<string> lines)
        {
            var sb = new StringBuilder();
            foreach (string line in lines)
            {
                if (sb.Length > 0)
                    sb.AppendLine();
                sb.Append(line);
            }
            return sb.ToString();
        }

        public static string LineSeparate(params string[] lines)
        {
            return LineSeparate((IEnumerable<string>) lines);
        }
    }
}
