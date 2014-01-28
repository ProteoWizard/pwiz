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
            Console.WriteLine(args.Length);
            foreach (var arg in args)
            {
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
            string[] Fields = GetFields(lines[0], ',');
            int colCount = Fields.Length;

            var dt = new DataTable();
            //first row colnames
            for (int i = 0; i < colCount; i++)
                dt.Columns.Add(Fields[i].ToLower(), typeof(string));

            var dtOut = new DataTable();
            const int nonPivotCols = 5; // number of rows before replicate rows begin
            int numOfReplicates = dt.Columns.Count - nonPivotCols;

            dtOut.Columns.Add("Protein Name");
            for (int i = 0; i < numOfReplicates; i++)
                dtOut.Columns.Add(Fields[i + nonPivotCols].ToLower(), typeof(string));


            for (int i = 1; i < lines.Length; i++)
            {
                Fields = GetFields(lines[i], ',');
                var row = dt.NewRow();
                for (int f = 0; f < colCount; f++)
                    row[f] = Fields[f];
                dt.Rows.Add(row);
            }

            var proteinNames = dt.AsEnumerable()
                .Select(dr => dr.Field<string>("ProteinName"))
                .Distinct().ToArray();

            Console.WriteLine("Unique  Replicates:");
            foreach (var proteinName in proteinNames)
            {
                Console.WriteLine("# " + proteinName);                
                var dataRows = dt.Select(string.Format("ProteinName = '{0}'", proteinName));

                var replicateRowValues = new double[numOfReplicates];
                foreach (var row in dataRows)
                {
                    for (int a = 0; a < numOfReplicates; a++)
                    {
                        double cellValue;
                        if (!double.TryParse(row[a + nonPivotCols].ToString(), out cellValue))
                            cellValue = 0;
                        replicateRowValues[a] += cellValue;
                    }
                }

                var newRow = dtOut.NewRow();
               
                newRow[0] = proteinName;  
                for (int r = 0; r < replicateRowValues.Length; r++ )
                {
                    newRow[r+1] = replicateRowValues[r];  
                }
                dtOut.Rows.Add(newRow);
            }

            //prints output data table
            Console.WriteLine("----------Output File----------");

            DataRow[] outputRows = dtOut.Select(null, null, DataViewRowState.CurrentRows);

            foreach (DataColumn column in dtOut.Columns)
                Console.Write("\t{0}", column.ColumnName);

            Console.WriteLine("\tRowState");

            foreach (DataRow row in outputRows)
            {
                foreach (DataColumn column in dtOut.Columns)
                    Console.Write("\t{0}", row[column]);

                Console.WriteLine("\t" + row.RowState);
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

            File.WriteAllText("final.csv", sb.ToString());
            Console.WriteLine("File saved successfully.");
            Console.WriteLine("Finished.");
        }

        private static string[] GetFields(string line, char separator)
        {
            // TODO: Use CSV parsing code to deal with possible quoted values that contain commas
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

        private static string SetFields(string[] row)
        {
//            // TODO: Use CSV parsing code to deal with possible quoted values that contain commas
//            for (int cell = 0; cell < row.Length; cell++)
//            {
//                var requiresComma = row[cell].ToLowerInvariant().Contains(',');
//                var requiresQuote = row[cell].Contains('"');
//                if(requiresComma)
//                    row[cell] = "\"" + row[cell].Replace("\"","\"\"") + "\"";
//            }
            String csvLine = String.Join(",", row.Select(field=>ToDsvField(field, ',')));
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