//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Jay Holman.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): 
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;

using Microsoft.Office.Interop;

namespace IDPicker
{
    static class TableExporter
    {
        /// <summary>
        /// Converts a 2D list of strings to a single TSV string
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        private static string TableToString(List<List<string>> table)
        {
            StringBuilder tempString = new StringBuilder(string.Empty);

            foreach (List<string> row in table)
            {
                for (int x = 1; x <= row.Count; x++)
                {
                    tempString.Append(row[x - 1]);

                    if (x != row.Count)
                        tempString.Append("\t");
                }

                tempString.Append(System.Environment.NewLine);
            }

            return tempString.ToString().TrimEnd();
        }

        /// <summary>
        /// Places TSV formatted interpretation of 2D list of strings onto clipboard.
        /// Variable should be in format of a list of rows, wehre each row is a list of values.
        /// </summary>
        /// <param name="table"></param>
        public static void CopyToClipboard(List<List<string>> table)
        {
            Clipboard.SetText(TableToString(table));
        }

        /// <summary>
        /// Exports TSV or CSV formatted interpretation of 2D list of strings to file.
        /// Variable should be in format of a list of rows, wehre each row is a list of values.
        /// </summary>
        /// <param name="table"></param>
        public static void ExportToFile(List<List<string>> table)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.AddExtension = true;
            sfd.CheckPathExists = true;
            sfd.Filter = "TSV File|*.tsv|CSV File|*.csv";
            sfd.RestoreDirectory = true;

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                FileInfo info = new FileInfo(sfd.FileName);

                if (info.Extension == ".tsv")
                {
                    StreamWriter fileOut = new StreamWriter(info.FullName);

                    fileOut.Write(TableToString(table));

                    fileOut.Flush();
                    fileOut.Close();
                }

                else if (info.Extension == ".csv")
                {
                    StreamWriter fileOut = new StreamWriter(info.FullName);
                    string output = TableToString(table);

                    if (output.Contains(','))
                        fileOut.Write("\"" + output
                            .Replace("\t", "\",\"")
                            .Replace(System.Environment.NewLine, string.Format("\"{0}\"", System.Environment.NewLine))
                            + "\"");
                    else
                        fileOut.Write(output.Replace("\t", ","));

                    fileOut.Flush();
                    fileOut.Close();
                }
                else
                    MessageBox.Show("Invalid file extension");
            }
        }

        /// <summary>
        /// Places 2D list of strings in Excel.
        /// Variable should be in format of a list of rows, wehre each row is a list of values.
        /// </summary>
        /// <param name="table"></param>
        public static void ShowInExcel(List<List<string>> table)
        {
            //must be converted to array of objects for quick population
            //and to avoid "numbers formatted as text" errors
            object[,] genericList = new object[table.Count,table[0].Count];
            for (int row = 0; row < table.Count; row++)
                for (int column = 0; column < table[0].Count; column++)
                    genericList[row, column] = table[row][column];

            var newExcel = new Microsoft.Office.Interop.Excel.ApplicationClass();
            var newWorkbook = newExcel.Workbooks.Add(Microsoft.Office.Interop.Excel.XlWBATemplate.xlWBATWorksheet);
            var newWorksheet = (Microsoft.Office.Interop.Excel.Worksheet)newWorkbook.ActiveSheet;
            var range = newWorksheet.get_Range("A1", string.Format("{0}{1}", IntToColumn(table[0].Count), table.Count));

            range.Value2 = genericList;

            newWorksheet.Columns.AutoFit();

            newExcel.UserControl = true;
            newExcel.Visible = true;
        }


        private static string IntToColumn(int number)
        {
            char[] chars = new char[] { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z' };
            string finalresult = string.Empty;
            int remainder = number % 26;
            int othernumber = number;
            othernumber -= remainder;
            if (othernumber > 0)
            {
                othernumber /= 26;
                finalresult += chars[othernumber - 1];
            }

            finalresult += chars[remainder - 1];

            return finalresult;
        }
    }
}
