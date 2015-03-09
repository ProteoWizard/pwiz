/*
 * Original author: Yuval Boss <yuval .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MPP_Export 
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // There should only be one argument from skyline which is the file path to the report file.
            if (args.Length != 1)
            {
                Console.Error.WriteLine(MppExportResources.Program_Main_Argument_was_invalid__MPP_Export_requires_one_argument__the_path_to_the_Skyline_report_file_);
                return;
            }

            using (var saveFileDialog = new SaveFileDialog
            {
                FileName = "MPPReport.txt", // Not L10N
                Filter = MppExportResources.Program_Main_Text_files____txt____txt_All_files__________,
            })
            {
                if (saveFileDialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }
                ParseCsv(args[0], saveFileDialog.FileName);
            }
        }

        public static void ParseCsv(string csvFilePathName, string outputFile)
        {
            string[] lines = File.ReadAllLines(csvFilePathName);
            string[] csvFields = GetFields(lines[0], ','); // Not L10N
            int colCount = csvFields.Length;

            var dt = new DataTable();
            foreach (string csvField in csvFields)
            {
                dt.Columns.Add(csvField, typeof(string));
            }


            var dtOut = new DataTable();
            const int nonPivotCols = 10; // Number of columns before replicate columns begin.
            const int newCols = 5; // Number of columns before accession column in export csv.
            int rowCount = 1; // Row counter used for RT and Mass values.
            int numOfReplicates = dt.Columns.Count - nonPivotCols;
            dtOut.Columns.Add("RT"); // Not L10N
            dtOut.Columns.Add("Mass"); // Not L10N
            dtOut.Columns.Add("Compound Name"); // Not L10N
            dtOut.Columns.Add("Formula"); // Not L10N
            dtOut.Columns.Add("CAS ID"); // Not L10N
            dtOut.Columns.Add("Swiss-Prot ID"); // Not L10N

            // Column headers for replicate area columns generated here.
            for (int i = 0; i < numOfReplicates; i++)
            {
                if (csvFields[i + nonPivotCols].Contains(" Area")) // Not L10N
                {
                    dtOut.Columns.Add(csvFields[i + nonPivotCols].Replace(" Area", ""), typeof(string)); // Not L10N
                } 
                else
                {
                    dtOut.Columns.Add(csvFields[i + nonPivotCols], typeof(string));
                }
            }

            foreach (string line in lines)
            {
                if (line == lines[0]) // Skips column headers.
                {
                    continue;
                }

                csvFields = GetFields(line, ',');
                var row = dt.NewRow();
                for (int f = 0; f < colCount; f++)
                {
                    row[f] = csvFields[f];
                }
                dt.Rows.Add(row);
            }

            var proteinAccessions = dt.AsEnumerable()
                .Select(dr => dr.Field<string>("ProteinAccession")) // Not L10N
                .Distinct().ToArray();

            Console.WriteLine(MppExportResources.Program_ParseCsv_Unique_Accessions_);
            foreach (var proteinAccession in proteinAccessions)
            {
                Console.WriteLine("# " + proteinAccession);   // Not L10N              
                var dataRows = dt.Select(string.Format("ProteinAccession = '{0}'", proteinAccession)); // Not L10N

                var replicateRowValues = new double[numOfReplicates];
                var replicateRowDescription = ""; // Not L10N
                foreach (var row in dataRows)
                {
                    for (int a = 0; a < numOfReplicates; a++)
                    {
                        double cellValue;
                        if (!double.TryParse(row[a + nonPivotCols].ToString(), out cellValue))
                            cellValue = 0;

                        replicateRowValues[a] += cellValue;
                    }
                    replicateRowDescription = row[1].ToString();
                }

                var newRow = dtOut.NewRow();

                newRow[newCols] = proteinAccession;
                newRow[0] = rowCount;
                newRow[1] = rowCount;
                newRow[2] = replicateRowDescription;
                rowCount = rowCount + 1;

                for (int r = 0; r < replicateRowValues.Length; r++)
                {
                    newRow[r + newCols + 1] = replicateRowValues[r];
                }
                dtOut.Rows.Add(newRow);
            }

            var sb = new StringBuilder();

            string[] columnNames = dtOut.Columns.Cast<DataColumn>().
                                              Select(column => column.ColumnName).
                                              ToArray();
            sb.AppendLine(SetFields(columnNames));

            foreach (DataRow row in dtOut.Rows)
            {
                string[] fields = row.ItemArray.Select(field => field.ToString()).
                                                ToArray();
                sb.AppendLine(SetFields(fields));
            }
            try
            {
                File.WriteAllText(outputFile.ToString(CultureInfo.InvariantCulture), sb.ToString());
                Console.WriteLine(MppExportResources.Program_ParseCsv_Location__ + outputFile);
                Console.WriteLine(MppExportResources.Program_ParseCsv_File_saved_successfully_);
                Console.WriteLine(MppExportResources.Program_ParseCsv_Finished_);
            } // If file MPPReport.csv can't be accessed program will throw IOException.
            catch (IOException ex)
            {   
                Console.WriteLine(ex);
                Console.WriteLine(MppExportResources.Program_ParseCsv_System_IO_Exception__cannot_access__0_, outputFile);
                Console.WriteLine(MppExportResources.Program_ParseCsv_Double_check_that__0__is_not_open_in_another_program_, outputFile);
                Console.WriteLine(MppExportResources.Program_ParseCsv_Finished___Run_Failed_);
            }
        }
        // CSV Parsing to deal with commas.
        private static string[] GetFields(string line, char separator)
        {
            var listFields = new List<string>();
            var sbField = new StringBuilder();
            bool inQuotes = false;
            char chLast = '\0'; // Not L10N 
            foreach (char ch in line)
            {
                if (inQuotes)
                {
                    if (ch == '"') // Not L10N
                        inQuotes = false;
                    else
                        sbField.Append(ch);
                }
                else if (ch == '"') // Not L10N
                {
                    inQuotes = true;
                    // Add quote character, for "" inside quotes.
                    if (chLast == '"') // Not L10N
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

        private static string SetFields(string[] row)
        {
            String csvLine = String.Join("\t", row.Select(field => ToDsvField(field, ','))); // Not L10N
            return csvLine;
        }

        public static string ToDsvField(string text, char separator)
        {
            if (text == null)
                return string.Empty;
            if (text.IndexOfAny(new[] { '"', separator, '\r', '\n' }) == -1) // Not L10N
                return text;
            return '"' + text.Replace("\"", "\"\"") + '"'; // Not L10N
        }
    }
}
