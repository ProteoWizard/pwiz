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
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic.FileIO;

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
                FileName = "MPPReport.txt",
                Filter = MppExportResources.Program_Main_Text_files____txt____txt_All_files__________,
            })
            {
                if (saveFileDialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                using (var reader = File.OpenText(args[0]))
                {
                    ParseCsv(reader, saveFileDialog.FileName);
                }
            }
        }

        public static void ParseCsv(TextReader reader, string outputFile)
        {
            var parser = new TextFieldParser(reader);

            parser.SetDelimiters(",");
            string[] csvFields = parser.ReadFields() ?? Array.Empty<string>();
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
            dtOut.Columns.Add("RT");
            dtOut.Columns.Add("Mass");
            dtOut.Columns.Add("Compound Name");
            dtOut.Columns.Add("Formula");
            dtOut.Columns.Add("CAS ID");
            dtOut.Columns.Add("Swiss-Prot ID");

            // Column headers for replicate area columns generated here.
            for (int i = 0; i < numOfReplicates; i++)
            {
                if (csvFields[i + nonPivotCols].Contains(" Area"))
                {
                    dtOut.Columns.Add(csvFields[i + nonPivotCols].Replace(" Area", ""), typeof(string));
                } 
                else
                {
                    dtOut.Columns.Add(csvFields[i + nonPivotCols], typeof(string));
                }
            }

            while ((csvFields = parser.ReadFields()) != null)
            {
                var row = dt.NewRow();
                for (int f = 0; f < colCount; f++)
                {
                    row[f] = csvFields[f];
                }
                dt.Rows.Add(row);
            }

            var proteinAccessions = dt.AsEnumerable()
                .Select(dr => dr.Field<string>("ProteinAccession"))
                .Distinct().ToArray();

            Console.WriteLine(MppExportResources.Program_ParseCsv_Unique_Accessions_);
            foreach (var proteinAccession in proteinAccessions)
            {
                Console.WriteLine(@"# {0}", proteinAccession);                
                var dataRows = dt.Select(string.Format("ProteinAccession = '{0}'", proteinAccession));

                var replicateRowValues = new double[numOfReplicates];
                var replicateRowDescription = "";
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
        private static string SetFields(string[] row)
        {
            String csvLine = String.Join("\t", row.Select(field => ToDsvField(field, ',')));
            return csvLine;
        }

        public static string ToDsvField(string text, char separator)
        {
            if (text == null)
                return string.Empty;
            if (text.IndexOfAny(new[] { '"', separator, '\r', '\n' }) == -1)
                return text;
            return '"' + text.Replace("\"", "\"\"") + '"';
        }
    }
}
