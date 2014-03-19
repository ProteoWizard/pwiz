using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;


namespace MPP_Export
{
    public class Program
    {
        private static void Main(string[] args)
        {
            foreach (var arg in args)
            {
                // There should only be one argument from skyline which is the file path to the report file.
                Console.WriteLine("CSV file path: {0}", arg);
                if (arg != null & args.Length == 1)
                {
                    ParseCsv(arg);
                }
                else
                {
                    Console.Out.WriteLine("Argument was invalid.");
                }
            }
        }

        public static void ParseCsv(String filepath)
        {
            string csvFilePathName = filepath;
            string[] lines = File.ReadAllLines(csvFilePathName);
            string[] Fields = GetFields(lines[0], ','); // Not L10N
            int colCount = Fields.Length;

            var dt = new DataTable();
            for (int i = 0; i < colCount; i++)
            {         
                    dt.Columns.Add(Fields[i], typeof(string));               
            }
                

            var dtOut = new DataTable();
            const int nonPivotCols = 10; // Number of columns before replicate columns begin.
            const int newCols = 5; // Number of columns before accession column in export csv.
            int rowCount = 1; // Row counter used for RT and Mass values.
            int numOfReplicates = dt.Columns .Count - nonPivotCols;
            dtOut.Columns.Add("RT"); // Not L10N
            dtOut.Columns.Add("Mass"); // Not L10N
            dtOut.Columns.Add("Compound Name"); // Not L10N
            dtOut.Columns.Add("Formula"); // Not L10N
            dtOut.Columns.Add("CAS ID"); // Not L10N
            dtOut.Columns.Add("Swiss Prot ID"); // Not L10N

            // Column headers for replicate area columns generated here.
            for (int i = 0; i < numOfReplicates; i++)
            {
                if (Fields[i + nonPivotCols].Contains(" Area")) // Not L10N
                {
                    dtOut.Columns.Add(Fields[i + nonPivotCols].Replace(" Area", ""), typeof(string)); // Not L10N
                }
                else
                {
                    dtOut.Columns.Add(Fields[i + nonPivotCols], typeof(string));
                }
            }
                
            for (int i = 1; i < lines.Length; i++)
            {
                Fields = GetFields(lines[i], ',');
                var row = dt.NewRow();
                for (int f = 0; f < colCount; f++)
                {                  
                        row[f] = Fields[f];
                }
                dt.Rows.Add(row);
            }

            var proteinAccessions = dt.AsEnumerable()
                .Select(dr => dr.Field<string>("ProteinAccession")) // Not L10N
                .Distinct().ToArray();

            Console.WriteLine("Unique  Accessions:");
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
            
                for (int r = 0; r < replicateRowValues.Length; r++ )
                {
                    newRow[r+newCols+1] = replicateRowValues[r];  
                }
                dtOut.Rows.Add(newRow);
            }

            // Prints output data table in skyline console window.
            Console.WriteLine("----------Output File----------");

            DataRow[] outputRows = dtOut.Select(null, null, DataViewRowState.CurrentRows);

            foreach (DataColumn column in dtOut.Columns)
                Console.Write("\t{0}", column.ColumnName); // Not L10N

            Console.WriteLine("\tRowState"); // Not L10N

            foreach (DataRow row in outputRows)
            {
                foreach (DataColumn column in dtOut.Columns)
                    Console.Write("\t{0}", row[column]); // Not L10N

                Console.WriteLine("\t" + row.RowState); // Not L10N
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
                var currentDirectory = Directory.GetCurrentDirectory();
                File.WriteAllText("MPPReport.csv", sb.ToString());
                Console.WriteLine("Location: {0}\\MPPReport.csv",currentDirectory);
                Console.WriteLine("File saved successfully.");
                Console.WriteLine("Finished.");
            } // If file MPPReport.csv can't be accessed program will throw IOException.
            catch (System.IO.IOException ex)
            {
                Console.WriteLine("System IO Exception, cannot access final.csv.");
                Console.WriteLine("Double check that final.csv is not open in another program.");
                Console.WriteLine("Finished. (Run Failed)"); 
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
            String csvLine = String.Join(",", row.Select(field=>ToDsvField(field, ','))); // Not L10N
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