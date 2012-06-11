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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace pwiz.Skyline.Util.Extensions
{
    /// <summary>
    /// Extension functions for reading and writing text
    /// </summary>
    public static class TextUtil
    {
        public const char SEPARATOR_CSV = ',';
        public const char SEPARATOR_CSV_INTL = ';'; // International CSV for comma-decimal locales
        public const char SEPARATOR_TSV = '\t';

        /// <summary>
        /// The CSV separator character for the current culture.  Like Excel, a comma
        /// is used unless the decimal separator is a comma.  This allows exported CSV
        /// files to be imported directly into Excel on the same system.
        /// </summary>
        public static char CsvSeparator
        {
            get { return GetCsvSeparator(CultureInfo.CurrentCulture); }
        }

        /// <summary>
        /// The CSV separator character for a given culture.  Like Excel, a comma
        /// is used unless the decimal separator is a comma.  This allows exported CSV
        /// files to be imported directly into Excel on the same system.
        /// <param name="cultureInfo">The culture for which the separator is requested.</param>
        /// </summary>
        public static char GetCsvSeparator(CultureInfo cultureInfo)
        {
            return (Equals(",", cultureInfo.NumberFormat.NumberDecimalSeparator) ? ';' : ',');
        }

        /// <summary>
        /// Writes a text string as a value in a delimiter-separated value file, ensuring
        /// that characters are properly escaped.
        /// </summary>
        /// <param name="writer">The writer to use for output</param>
        /// <param name="text">The text value to output</param>
        /// <param name="separator">The separator being used</param>
        public static void WriteDsvField(this TextWriter writer, string text, char separator)
        {
            writer.Write(text.ToDsvField(separator));
        }

        /// <summary>
        /// Converts a string to a field that can be safely written to a delimiter-separated value file.
        /// </summary>
        /// <param name="text">The text value of the field</param>
        /// <param name="separator">The separator being used</param>
        public static string ToDsvField(this string text, char separator)
        {
            if (text.IndexOfAny(new[] {'"', separator, '\r', '\n'}) == -1)
                return text;
            return '"' + text.Replace("\"", "\"\"") + '"';
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
            char chLast = '\0';
            foreach (char ch in line)
            {
                if (inQuotes)
                {
                    if (ch == '"')
                        inQuotes = false;
                    else
                        sbField.Append(ch);
                }
                else if (ch == '"')
                {
                    inQuotes = true;
                    // Add quote character, for "" inside quotes
                    if (chLast == '"')
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
    }
}
