/*
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
using System.Text;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util.Extensions
{
    /// <summary>
    /// Extension functions for reading and writing text
    /// </summary>
    public static class TextUtil
    {
        public const string EXT_CSV = ".csv"; // Not L10N
        public const string EXT_TSV = ".tsv"; // Not L10N

        public static string FILTER_CSV
        {
            get { return FileDialogFilter(Resources.TextUtil_DESCRIPTION_CSV_CSV__Comma_delimited_, EXT_CSV); }
        }

        public static string FILTER_TSV
        {
            get { return FileDialogFilter(Resources.TextUtil_DESCRIPTION_TSV_TSV__Tab_delimited_, EXT_TSV); }
        }

        public const char SEPARATOR_CSV = ','; // Not L10N
        public const char SEPARATOR_CSV_INTL = ';'; // International CSV for comma-decimal locales // Not L10N
        public const char SEPARATOR_TSV = '\t'; // Not L10N
        public const char SEPARATOR_SPACE = ' '; // Not L10N

        public const string EXCEL_NA = "#N/A"; // Not L10N

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
            var unwanted = new[] { '"', separator, '\r', '\n' }; // Not L10N
            if (text.IndexOfAny(unwanted) == -1) 
                return text;
            if (!string.IsNullOrEmpty(replace))
                return string.Join(replace, text.Split(unwanted));
            return '"' + text.Replace("\"", "\"\"") + '"'; // Not L10N
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
        /// </summary>
        /// <param name="line">The line to be split into fields</param>
        /// <param name="separator">The separator being used</param>
        /// <returns>An array of field strings</returns>
        public static string[] ParseDsvFields(this string line, char separator)
        {
            var listFields = new List<string>();
            var sbField = new StringBuilder();
            bool inQuotes = false;
            char chLast = '\0';  // Not L10N
            foreach (char ch in line)
            {
                if (inQuotes)
                {
                    if (ch == '"')
                        inQuotes = false;
                    else
                        sbField.Append(ch);
                }
                else if (ch == '"')  // Not L10N
                {
                    inQuotes = true;
                    // Add quote character, for "" inside quotes
                    if (chLast == '"')  // Not L10N
                        sbField.Append(ch);
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
                chLast = ch;
            }
            listFields.Add(sbField.ToString());
            return listFields.ToArray();
        }

        /// <summary>
        /// Parse a list of comma separated integers, as saved to XML.
        /// </summary>
        public static int[] ParseInts(string s)
        {
            return ArrayUtil.Parse(s, Convert.ToInt32, SEPARATOR_CSV, new int[0]);
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
            return string.Format("{0} ({1})|{1}", description, sb); // Not L10N
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
            return string.Join("|", filters); // Not L10N
        }

        /// <summary>
        /// Converts a set of file dialog filter strings into a single string containing all filters,
        /// with an "All Files" filter appended, suitable for the Filter property on a common file dialog.
        /// </summary>
        /// <param name="filters">Filters to be joined</param>
        public static string FileDialogFiltersAll(params string[] filters)
        {
            var listFilters = filters.ToList();
            listFilters.Add(FileDialogFilter(Resources.TextUtil_FileDialogFiltersAll_All_Files, ".*")); // Not L10N
            return string.Join("|", listFilters); // Not L10N
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

        public void Initialize(TextReader reader, char separator, bool hasHeaders = true)
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
                    fields[i] = string.Format("{0}", i ); // Not L10N
                }
            }
            for (int i = 0; i < fields.Length; ++i)
            {
                FieldNames.Add(fields[i]);
                FieldDict[fields[i]] = i;
            }
        }

        /// <summary>
        /// Read a line of text, storing the fields by name for retrieval using GetFieldByName.
        /// Outputs a list of fields (not indexed by name)
        /// </summary>
        /// <returns></returns>
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
        /// <returns></returns>
        public string GetFieldByName(string fieldName)
        {
            int fieldIndex = GetFieldIndex(fieldName);
            return GetFieldByIndex(fieldIndex);
        }

        /// <summary>
        /// For the current line, outputs the field numbered fieldIndex
        /// </summary>
        /// <param name="fieldIndex">Index of the field on the current line to be output</param>
        /// <returns></returns>
        public string GetFieldByIndex(int fieldIndex)
        {
            return -1 < fieldIndex && fieldIndex < _currentFields.Length ?_currentFields[fieldIndex] : null;
        }

        /// <summary>
        /// Get the index of the field corresponding to the column title fieldName
        /// </summary>
        /// <param name="fieldName">Column title.</param>
        /// <returns></returns>
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
