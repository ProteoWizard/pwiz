using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SharedBatch.Properties;

namespace SharedBatch
{
    public static class TextUtil
    {
        public const string EXT_XML = ".xml";
        public const string EXT_SKY = ".sky";
        public const string EXT_SKY_ZIP = ".sky.zip";
        public const string EXT_SKYR = ".skyr";
        public const string EXT_SKYD = ".skyd";
        public const string EXT_QCFG = ".qcfg";
        public const string EXT_BCFG = ".bcfg";
        public const string EXT_R = ".R";
        public const string EXT_CSV = ".csv";
        public const string EXT_LOG = ".log";
        public const string EXT_APPREF = ".appref-ms";

        public static string FILTER_XML
        {
            get { return FileDialogFilter(Resources.TextUtil_FILTER_XML_XML_Files, EXT_XML); }
        }

        public static string FILTER_SKY
        {
            get { return FileDialogFilter(Resources.TextUtil_FILTER_SKY_Skyline_Files, EXT_SKY); }
        }

        public static string FILTER_SKYR
        {
            get { return FileDialogFilter(Resources.TextUtil_FILTER_SKYR_Skyline_Reports, EXT_SKYR); }
        }

        public static string FILTER_BCFG
        {
            get { return FileDialogFilter(Resources.TextUtil_FILTER_BCFG_Skyline_Batch_Configuration_Files, EXT_BCFG); }
        }

        public static string FILTER_QCFG
        {
            get { return FileDialogFilter(Resources.TextUtil_FILTER_QCFG_AutoQC_Configuration_Files, EXT_QCFG); }
        }

        public static string FILTER_CSV
        {
            get { return FileDialogFilter(Resources.TextUtil_FILTER_CSV_CSV_Files, EXT_CSV); }
        }

        public static string FILTER_R
        {
            get { return FileDialogFilter(Resources.TextUtil_FILTER_R_R_Files, EXT_R); }
        }

        public static string FILTER_ALL
        {
            get { return FileDialogFilter(Resources.TextUtil_FILTER_ALL_All_Files, @".*"); }
        }


        public static bool SuccessfulReplace(Validator validate, string oldText, string newText, string originalString, out string replacedString)
        {
            var oldPath = originalString;
            var newPath = TryReplaceStart(oldText, newText, originalString);
            replacedString = oldPath;

            try
            {
                validate(oldPath);
                return false;
            }
            catch (ArgumentException)
            {
                // Pass - expect oldPath to be invalid
            }

            replacedString = newPath;
            try
            {
                validate(newPath);
            }
            catch (ArgumentException)
            {
                return false;
            }
            return true;
        }

        public static string TryReplaceStart(string oldText, string newText, string originalString)
        {
            if (!originalString.StartsWith(oldText))
                return originalString;
            return newText + originalString.Substring(oldText.Length);
        }

        /// <summary>
        /// Returns a filter string suitable for a common file dialog (e.g. "CSV (Comma delimited) (*.csv)|*.csv")
        /// </summary>
        /// <param name="description">The description of the filter</param>
        /// <param name="exts">The file extention(s), beginning with the period (e.g. ".csv")</param>
        public static string FileDialogFilter(string description, params string[] exts)
        {
            var sb = new StringBuilder();
            foreach (var ext in exts)
            {
                if (sb.Length > 0)
                    sb.Append(';');
                sb.Append('*').Append(ext);
            }
            return string.Format(@"{0} ({1})|{1}", description, sb);
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

        /// <summary>
        /// This function can be used as a replacement for String.Join("\n", ...)
        /// </summary>
        /// <param name="lines">A set of strings to be on separate lines</param>
        /// <returns>A single string containing the original set separated by new lines</returns>
        public static string LineSeparate(params string[] lines)
        {
            return LineSeparate(lines.AsEnumerable());
        }

        public static string ParseInvariantCultureInteger(string integer)
        {
            if (string.IsNullOrWhiteSpace(integer)) return string.Empty;
            return int.Parse(integer, CultureInfo.InvariantCulture).ToString(CultureInfo.CurrentCulture);
        }

        public static string ParseInvariantCultureDouble(string doubleString)
        {
            if (string.IsNullOrWhiteSpace(doubleString)) return string.Empty;
            return Double.Parse(doubleString, CultureInfo.InvariantCulture).ToString(CultureInfo.CurrentCulture);
        }

        public static string ToInvariantCultureInteger(string integer)
        {
            if (string.IsNullOrWhiteSpace(integer)) return string.Empty;
            return int.Parse(integer, CultureInfo.CurrentCulture).ToString(CultureInfo.InvariantCulture);
        }

        public static string ToInvariantCultureDouble(string doubleString)
        {
            if (string.IsNullOrWhiteSpace(doubleString)) return string.Empty;
            return Double.Parse(doubleString, CultureInfo.CurrentCulture).ToString(CultureInfo.InvariantCulture);
        }
    }
}
