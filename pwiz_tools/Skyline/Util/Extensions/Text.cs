﻿/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util.Extensions
{
    /// <summary>
    /// Extension functions for reading and writing text
    /// </summary>
    public static class TextUtil
    {
        public const string EXT_CSV = ".csv";
        public const string EXT_TSV = ".tsv";

        public static string FILTER_CSV
        {
            get { return FileDialogFilter(Resources.TextUtil_DESCRIPTION_CSV_CSV__Comma_delimited_, EXT_CSV); }
        }

        public static string FILTER_TSV
        {
            get { return FileDialogFilter(Resources.TextUtil_DESCRIPTION_TSV_TSV__Tab_delimited_, EXT_TSV); }
        }

        public const char SEPARATOR_CSV = ',';
        public const char SEPARATOR_CSV_INTL = ';'; // International CSV for comma-decimal locales
        public const char SEPARATOR_TSV = '\t';
        public static readonly string SEPARATOR_TSV_STR = SEPARATOR_TSV.ToString(); 
        public const char SEPARATOR_SPACE = ' ';

        public const string EXCEL_NA = "#N/A";

        /// <summary>
        /// The CSV separator character for the current culture.  Like Excel, a comma
        /// is used unless the decimal separator is a comma.  This allows exported CSV
        /// files to be imported directly into Excel on the same system.
        /// </summary>
        public static char CsvSeparator
        {
            get { return GetCsvSeparator(LocalizationHelper.CurrentCulture); }
        }

        /// <summary>
        /// The CSV separator character for a given culture.  Like Excel, a comma
        /// is used unless the decimal separator is a comma.  This allows exported CSV
        /// files to be imported directly into Excel on the same system.
        /// <param name="cultureInfo">The culture for which the separator is requested.</param>
        /// </summary>
        public static char GetCsvSeparator(CultureInfo cultureInfo)
        {
            return (Equals(SEPARATOR_CSV.ToString(CultureInfo.InvariantCulture), cultureInfo.NumberFormat.NumberDecimalSeparator) ? SEPARATOR_CSV_INTL : SEPARATOR_CSV);
        }

        /// <summary>
        /// Writes a text string as a value in a delimiter-separated value file, ensuring
        /// that characters are properly escaped.
        /// </summary>
        /// <param name="writer">The writer to use for output</param>
        /// <param name="text">The text value to output</param>
        /// <param name="separator">The separator being used</param>
        /// <param name="replace">Optional value for replacing unwanted characters instead of quoting string</param>
        public static void WriteDsvField(this TextWriter writer, string text, char separator, string replace = null)
        {
            writer.Write(text.ToDsvField(separator, replace));
        }

        /// <summary>
        /// Converts a string to a field that can be safely written to a delimiter-separated value file.
        /// </summary>
        /// <param name="text">The text value of the field</param>
        /// <param name="separator">The separator being used</param>
        /// <param name="replace">Optional value for replacing unwanted characters instead of quoting string</param>
        public static string ToDsvField(this string text, char separator, string replace = null)
        {
            if (text == null)
                return string.Empty;
            var unwanted = new[] { '"', separator, '\r', '\n' };
            if (text.IndexOfAny(unwanted) == -1) 
                return text;
            if (!string.IsNullOrEmpty(replace))
                return string.Join(replace, text.Split(unwanted));
            // ReSharper disable LocalizableElement
            return '"' + text.Replace("\"", "\"\"") + '"';
            // ReSharper restore LocalizableElement
        }

        /// <summary>
        /// Converts a list of strings to the fields in a comma-separated line that can be safely written to a comma-separated value file.
        /// </summary>
        /// <param name="fields">List of fields to be written in the comma-separated line</param>
        public static string ToCsvLine(this IEnumerable<string> fields)
        {
            return fields.ToDsvLine(CsvSeparator);
        }

        /// <summary>
        /// Converts a list of strings to the fields in a delimiter-separated line that can be safely writted to a delimiter-separated value file.
        /// </summary>
        /// <param name="fields">List of fields to be written in the delimiter-separated line</param>
        /// <param name="separator">The separator being used</param>
        public static string ToDsvLine(this IEnumerable<string> fields, char separator)
        {
            var sb = new StringBuilder();
            foreach (string field in fields)
            {
                if (sb.Length > 0)
                    sb.Append(separator);
                sb.Append(field.ToDsvField(separator));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Splits a line of text in comma-separated value format into an array of fields.
        /// The function correctly handles quotation marks.
        /// </summary>
        /// <param name="line">The line to be split into fields</param>
        /// <returns>An array of field strings</returns>
        public static string[] ParseCsvFields(this string line)
        {
            return line.ParseDsvFields(SEPARATOR_CSV);
        }

        /// <summary>
        /// Splits a line of text in delimiter-separated value format into an array of fields.
        /// The function correctly handles quotation marks.
        /// (N.B. our quotation mark handling now differs from the (March 2018) behavior of Excel and Google Spreadsheets
        /// when dealing with somewhat absurd uses of quotes as found in our tests, but that seems to be OK for  general use.
        /// </summary>
        /// <param name="line">The line to be split into fields</param>
        /// <param name="separator">The separator being used</param>
        /// <returns>An array of field strings</returns>
        public static string[] ParseDsvFields(this string line, char separator)
        {
            var listFields = new List<string>();
            var sbField = new StringBuilder();
            bool inQuotes = false;
            for (var chIndex = 0; chIndex < line.Length; chIndex++)
            {
                var ch = line[chIndex];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        // Is this the closing quote, or is this an escaped quote?
                        if (chIndex + 1 < line.Length && line[chIndex + 1] == '"')
                        {
                            sbField.Append(ch); // Treat "" as an escaped quote
                            chIndex++; // Consume both quotes
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sbField.Append(ch);
                    }
                }
                else if (ch == '"')
                {
                    if (sbField.Length == 0) // Quote at start of field is special case
                    {
                        inQuotes = true;
                    }
                    else
                    {
                        if (chIndex + 1 < line.Length && line[chIndex + 1] == '"') 
                        {
                            sbField.Append(ch); // Treat "" as an escaped quote
                            chIndex++; // Consume both quotes
                        }
                        else
                        {
                            // N.B. we effectively ignore a bare quote in an unquoted string. 
                            // This is technically an undefined behavior, so that's probably OK.
                            // Excel and Google sheets treat it as a literal quote, but that 
                            // would be a change in our established behavior
                            inQuotes = true;
                        }
                    }
                }
                else if (ch == separator)
                {
                    listFields.Add(sbField.ToString());
                    sbField.Remove(0, sbField.Length);
                }
                else
                {
                    sbField.Append(ch);
                }
            }
            listFields.Add(sbField.ToString());
            return listFields.ToArray();
        }

        /// <summary>
        /// Converts an invariant format DSV file to a locale-specific DSV file
        /// </summary>
        /// <param name="filePath">Path to the original file</param>
        /// <param name="outPath">Path to write the locale-specific file if necessary</param>
        /// <param name="headerLine">True if the input file has a header line</param>
        /// <returns>True if conversion was necessary and the output file was written</returns>
        public static bool WriteDsvToCsvLocal(string filePath, string outPath, bool headerLine)
        {
            if (CsvSeparator == SEPARATOR_CSV)
                return false;

            string[] fileLines = File.ReadAllLines(filePath);
            for (int i = 0; i < fileLines.Length; i++)
            {
                string line = fileLines[i];
                bool tsv = line.Contains(SEPARATOR_TSV);
                if (!tsv)
                    line = line.Replace(SEPARATOR_CSV, SEPARATOR_CSV_INTL);
                if (!headerLine || i > 0)
                    line = ReplaceDecimalPoint(line, tsv);
                fileLines[i] = line;
            }
            File.WriteAllLines(outPath, fileLines);
            return true;
        }

        private static string ReplaceDecimalPoint(string line, bool tsv)
        {
            char separator = tsv ? SEPARATOR_TSV : SEPARATOR_CSV_INTL;
            var fields = line.Split(separator);
            for (int i = 0; i < fields.Length; i++)
            {
                string field = fields[i];
                string fieldConverted = field
                    .Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator,
                             CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
                double fieldValue;
                // Convert if the field is numeric or contains modifications
                if (double.TryParse(fieldConverted, out fieldValue) || new Regex(@"\[[+-]\d+\.\d\]").IsMatch(field))
                    fields[i] = fieldConverted;
            }
            return string.Join(separator.ToString(), fields);
        }

        /// <summary>
        /// Parse a list of comma separated integers, as saved to XML.
        /// </summary>
        public static int[] ParseInts(string s)
        {
            return ArrayUtil.Parse(s, Convert.ToInt32, SEPARATOR_CSV);
        }

        /// <summary>
        /// Puts quotation marks before and after the text passed in
        /// </summary>
        public static string Quote(this string text)
        {
            return '"' + text + '"';
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
        /// This function can be used as a replacement for String.Join(" ", ...)
        /// </summary>
        /// <param name="values">A set of strings to be separated by spaces</param>
        /// <returns>A single string containing the original set separated by spaces</returns>
        public static string SpaceSeparate(params string[] values)
        {
            return SpaceSeparate(values.AsEnumerable());
        }

        /// <summary>
        /// Convert a collection of strings to a TSV line for serialization purposes,
        /// watching out for tabs, CRLF, and existing escapes
        /// </summary>
        public static string ToEscapedTSV(IEnumerable<string> strings)
        {
            return string.Join(SEPARATOR_TSV_STR, strings.Select(s => s.EscapeTabAndCrLf()));
        }

        /// <summary>
        /// Create a collection of strings from a TSV line for deserialization purposes,
        /// watching out for tabs, CRLF, and existing escapes
        /// </summary>
        public static string[] FromEscapedTSV(this string str)
        {
            var strings = str.Split(SEPARATOR_TSV).Select(s => s.UnescapeTabAndCrLf());
            return strings.ToArray();
        }

        /// <summary>
        /// Convert tab and/or CRLF characters to printable form for serialization purposes
        /// </summary>
        public static string EscapeTabAndCrLf(this string str)
        {
            var sb = new StringBuilder();
            var len = str.Length;
            for (int pos = 0; pos < len; pos++)
            {
                var c = str[pos];
                switch (c)
                {
                    case '\\': // Take care to preserve "c:\tmp" as "c:\\tmp" so it roundtrips properly
                        sb.Append(c);
                        sb.Append(c);
                        break;
                    case SEPARATOR_TSV:
                        sb.Append('\\');
                        sb.Append('t');
                        break;
                    case '\n': 
                        sb.Append('\\');
                        sb.Append('n');
                        break;
                    case '\r': 
                        sb.Append('\\');
                        sb.Append('r');
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Convert tab and/or CRLF characters from printable form for deserialization purposes
        /// </summary>
        public static string UnescapeTabAndCrLf(this string str)
        {
            var sb = new StringBuilder();
            var len = str.Length;
            for (int pos = 0; pos < len; pos++)
            {
                var c = str[pos];
                if (c == '\\' && pos < (len-1))
                {
                    var cc = str[pos+1];
                    switch (cc)
                    {
                        case '\\':
                            sb.Append(c);
                            pos++;
                            break;
                        case 't':
                            sb.Append(SEPARATOR_TSV);
                            pos++;
                            break;
                        case 'n':
                            sb.Append('\n');
                            pos++;
                            break;
                        case 'r':
                            sb.Append('\r');
                            pos++;
                            break;
                        default:
                            sb.Append(c);
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
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
        /// Returns a filter string suitable for a common file dialog (e.g. "CSV (Comma delimited) (*.csv)|*.csv")
        /// with the All Files filter appended.
        /// </summary>
        /// <param name="description">The description of the filter</param>
        /// <param name="ext">The file extention, beginning with the period (e.g. ".csv")</param>
        public static string FileDialogFilterAll(string description, string ext)
        {
            return FileDialogFiltersAll(FileDialogFilter(description, ext));
        }

        /// <summary>
        /// Converts a set of file dialog filter strings into a single string containing all filters
        /// suitable for the Filter property on a common file dialog.
        /// </summary>
        /// <param name="filters">Filters to be joined</param>
        public static string FileDialogFilters(params string[] filters)
        {
            return string.Join(@"|", filters);
        }

        /// <summary>
        /// Converts a set of file dialog filter strings into a single string containing all filters,
        /// with an "All Files" filter appended, suitable for the Filter property on a common file dialog.
        /// </summary>
        /// <param name="filters">Filters to be joined</param>
        public static string FileDialogFiltersAll(params string[] filters)
        {
            var listFilters = filters.ToList();
            listFilters.Add(FileDialogFilter(Resources.TextUtil_FileDialogFiltersAll_All_Files, @".*"));
            return string.Join(@"|", listFilters);
        }

        /// <summary>
        /// Encrypts a string. This encryption uses the user's (i.e. not machine) key, so it is 
        /// appropriate for strings that are marked with the [UserScopedSetting].
        /// It is not appropriate for any setting marked [ApplicationScopedSetting]
        /// </summary>
        public static string EncryptString(string str)
        {
            return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(str), null, DataProtectionScope.CurrentUser));
        }

        public static string DecryptString(string str)
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(str), null, DataProtectionScope.CurrentUser));
        }

        /// <summary>
        /// Get a common prefix, if any, among a set of strings.
        /// </summary>
        /// <param name="values">The set of strings to test for a common prefix</param>
        /// <param name="minLen">Minimum length of the prefix below which empty string will be returned</param>
        /// <returns>The common prefix or empty string if none is found</returns>
        public static string GetCommonPrefix(this IEnumerable<string> values, int minLen = 1)
        {
            return values.GetCommonFix(minLen, (s, i) => s[i], (s, i) => s.Substring(0, i));
        }

        /// <summary>
        /// Get a common suffix, if any, among a set of strings.
        /// </summary>
        /// <param name="values">The set of strings to test for a common suffix</param>
        /// <param name="minLen">Minimum length of the suffix below which empty string will be returned</param>
        /// <returns>The common suffix or empty string if none is found</returns>
        public static string GetCommonSuffix(this IEnumerable<string> values, int minLen = 1)
        {
            return values.GetCommonFix(minLen, (s, i) => s[s.Length - i - 1], (s, i) => s.Substring(s.Length - i));
        }

        private static string GetCommonFix(this IEnumerable<string> values,
                                            int minLen,
                                            Func<string, int, char> getChar,
                                            Func<string, int, string> getSubString)
        {
            string commonFix = null;
            foreach (string value in values)
            {
                if (commonFix == null)
                {
                    commonFix = value;
                    continue;
                }
                if (commonFix == string.Empty)
                {
                    break;
                }

                for (int i = 0; i < commonFix.Length; i++)
                {
                    if (i >= value.Length || getChar(commonFix, i) != getChar(value, i))
                    {
                        commonFix = getSubString(commonFix, i);
                        break;
                    }
                }
            }
            return commonFix != null && commonFix.Length >= minLen ? commonFix : String.Empty;
        }
    }

    /// <summary>
    /// Reads a comma-separated variable file, normally assuming the first line contains
    /// the names of the columns, and all following lines contain data for each column
    /// When ctor's optional hasHeaders arg == false, then columns are named "0", "1","2","3" etc.
    /// </summary>
    public class CsvFileReader : DsvFileReader
    {
        public CsvFileReader(string fileName, bool hasHeaders = true) :
            base(fileName, TextUtil.CsvSeparator, hasHeaders)
        {
        }

        public CsvFileReader(TextReader reader, bool hasHeaders = true) :
            base(reader, TextUtil.CsvSeparator, hasHeaders)
        {
        }
    }

    /// <summary>
    /// Reads a delimiter-separated variable file, normally assuming the first line contains
    /// the names of the columns, and all following lines contain data for each column.
    /// When ctor's optional hasHeaders arg == false, then columns are named "0", "1","2","3" etc.
    /// </summary>
    public class DsvFileReader
    {
        private char _separator;
        private string[] _currentFields;
        private string _titleLine;
        private bool _rereadTitleLine; // set true for first readline if the file didn't actually have a header line
        private TextReader _reader;
        
        public int NumberOfFields { get; private set; }
        public Dictionary<string, int> FieldDict { get; private set; }
        public List<string> FieldNames { get; private set; } 

        public DsvFileReader(string fileName, char separator, bool hasHeaders=true) : 
            this(new StreamReader(fileName), separator, hasHeaders)
        {
        }

        public DsvFileReader(TextReader reader, char separator, bool hasHeaders = true)
        {
            Initialize(reader, separator, hasHeaders);
        }

        public DsvFileReader(TextReader reader, char separator, IReadOnlyDictionary<string, string> headerSynonyms)
        {
            Initialize(reader, separator, true, headerSynonyms);
        }

        public void Initialize(TextReader reader, char separator, bool hasHeaders = true, IReadOnlyDictionary<string, string> headerSynonyms = null)
        {
            _separator = separator;
            _reader = reader;
            FieldNames = new List<string>();
            FieldDict = new Dictionary<string, int>();
            _titleLine = _reader.ReadLine(); // we will re-use this if it's not actually a header line
            _rereadTitleLine = !hasHeaders; // tells us whether or not to reuse the supposed header line on first read
            var fields = _titleLine.ParseDsvFields(separator);
            NumberOfFields = fields.Length;
            if (!hasHeaders)
            {
                // that wasn't really the header line, we just used it to get column count
                // replace with made up column names
                for (int i = 0; i < fields.Length; ++i)
                {
                    fields[i] = string.Format(@"{0}", i );
                }
            }
            for (int i = 0; i < fields.Length; ++i)
            {
                FieldNames.Add(fields[i]);
                FieldDict[fields[i]] = i;
                // Check to see if the given column name is actually a synonym for the internal canonical (no spaces, serialized) name
                if (headerSynonyms != null)
                {
                    var key = headerSynonyms.Keys.FirstOrDefault(k => string.Compare(k, fields[i], StringComparison.OrdinalIgnoreCase)==0); // Case insensitive
                    if (!string.IsNullOrEmpty(key))
                    {
                        var syn = headerSynonyms[key];
                        if (!FieldDict.ContainsKey(syn))
                        {
                            // Note the internal name for this field
                            FieldDict.Add(syn, i);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Read a line of text, storing the fields by name for retrieval using GetFieldByName.
        /// Outputs a list of fields (not indexed by name)
        /// </summary>
        /// <returns>Array of fields for the next line</returns>
        public string[] ReadLine()
        {
            var line = _rereadTitleLine?_titleLine:_reader.ReadLine(); // re-use title line on first read if it wasn't actually header info
            _rereadTitleLine = false; // we no longer need to re-use that first line
            if (line == null)
                return null;
            _currentFields = line.ParseDsvFields(_separator);
            if (_currentFields.Length != NumberOfFields)
            {
                throw new IOException(string.Format(Resources.DsvFileReader_ReadLine_Line__0__has__1__fields_when__2__expected_, line, _currentFields.Length, NumberOfFields));
            }
            return _currentFields;
        }


        /// <summary>
        /// For the current line, outputs the field corresponding to the column name fieldName, or null if
        /// there is no such field name.
        /// </summary>
        /// <param name="fieldName">Title of the column for which to get current line data</param>
        /// <returns>Field value</returns>
        public string GetFieldByName(string fieldName)
        {
            int fieldIndex = GetFieldIndex(fieldName);
            return GetFieldByIndex(fieldIndex);
        }

        /// <summary>
        /// For the current line, outputs the field numbered fieldIndex
        /// </summary>
        /// <param name="fieldIndex">Index of the field on the current line to be output</param>
        /// <returns>Field value</returns>
        public string GetFieldByIndex(int fieldIndex)
        {
            return -1 < fieldIndex && fieldIndex < _currentFields.Length ?_currentFields[fieldIndex] : null;
        }

        /// <summary>
        /// Get the index of the field corresponding to the column title fieldName
        /// </summary>
        /// <param name="fieldName">Column title.</param>
        /// <returns>Field index</returns>
        public int GetFieldIndex(string fieldName)
        {
            if (!FieldDict.ContainsKey(fieldName))
                return -1;
            return FieldDict[fieldName];
        }

        /// <summary>
        /// If loading from a file, use this to dispose the text reader.
        /// </summary>
        public void Dispose()
        {
            _reader.Dispose();
        }

    }
}
