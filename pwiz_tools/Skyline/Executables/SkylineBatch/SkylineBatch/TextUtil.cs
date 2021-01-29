﻿using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public static class TextUtil
    {
        public const string EXT_XML = ".xml";
        public const string EXT_SKY = ".sky";
        public const string EXT_SKYR = ".skyr";
        public const string EXT_SKYD = ".skyd";
        public const string EXT_R = ".R";
        public const string EXT_CSV = ".csv";


        public static string FILTER_XML
        {
            get { return FileDialogFilter(Resources.TextUtil_FileDialogFilter_XML_Files, EXT_XML); }
        }

        public static string FILTER_SKY
        {
            get { return FileDialogFilter(Resources.TextUtil_FileDialogFilter_Skyline_Files, EXT_SKY); }
        }

        public static string FILTER_SKYR
        {
            get { return FileDialogFilter(Resources.TextUtil_FileDialogFilter_Skyline_Reports, EXT_SKYR); }
        }

        public static string FILTER_R
        {
            get { return FileDialogFilter(Resources.TextUtil_FileDialogFilter_R_Files, EXT_R); }
        }

        public static string FILTER_ALL
        {
            get { return FileDialogFilter(Resources.TextUtil_FileDialogFilter_All_Files, @".*"); ; }
        }





        public static bool TryReplaceStart(string oldText, string newText, string originalString, out string replacedString)
        {
            replacedString = originalString;
            if (!originalString.Substring(0, oldText.Length).Equals(oldText)) return false;
            replacedString = newText + originalString.Substring(oldText.Length);
            return true;
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
    }
}
