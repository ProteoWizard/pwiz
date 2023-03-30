using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PanoramaClient
{
    public static class TextUtil
    {
        public const char SEPARATOR_SPACE = ' ';
        
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

        /// <summary>
        /// This function can be used as a replacement for String.Join("\n", ...)
        /// </summary>
        /// <param name="lines">A set of strings to be on separate lines</param>
        /// <returns>A single string containing the original set separated by new lines</returns>
        public static string LineSeparate(params string[] lines)
        {
            return LineSeparate(lines.AsEnumerable());
        }

        /// <summary>
        /// This function can be used as a replacement for String.Join(" ", ...)
        /// </summary>
        /// <param name="values">A set of strings to be separated by spaces</param>
        /// <returns>A single string containing the original set separated by spaces</returns>
        public static string SpaceSeparate(IEnumerable<string> values)
        {
            var sb = new StringBuilder();
            foreach (string value in values)
            {
                if (sb.Length > 0)
                    sb.Append(SEPARATOR_SPACE);
                sb.Append(value);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Like SpaceSeparate but allows arbitrary separator, and ignores empty strings
        /// </summary>
        public static string TextSeparate(string sep, params string[] values)
        {
            var sb = new StringBuilder();
            foreach (var value in values)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(sep);
                    }
                    sb.Append(value);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Like SpaceSeparate but allows arbitrary separator, and ignores empty strings, accepts IEnumerable
        /// </summary>
        public static string TextSeparate(string sep, IEnumerable<string> values)
        {
            var sb = new StringBuilder();
            foreach (string value in values)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(sep);
                    }
                    sb.Append(value);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// This function can be used as a replacement for String.Join(" ", ...)
        /// </summary>
        /// <param name="values">A set of strings to be separated by spaces</param>
        /// <returns>A single string containing the original set separated by spaces</returns>
        public static string SpaceSeparate(params string[] values)
        {
            return SpaceSeparate(values.AsEnumerable());
        }
    }
}
