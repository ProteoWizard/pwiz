//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
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
using System.Reflection;
using System.Text;
using System.IO;
using System.Windows.Forms;

using Microsoft.Office.Interop;

namespace IDPicker
{
    public static class TableExporter
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
        /// <param name="tables"></param>
        /// <param name="freezeFirstColumn"></param>
        public static void ShowInExcel(Dictionary<string,List<List<string>>> tables, bool freezeFirstColumn)
        {
            var firstSheet = true;
            var newExcel = new Microsoft.Office.Interop.Excel.ApplicationClass();
            var newWorkbook = newExcel.Workbooks.Add(Microsoft.Office.Interop.Excel.XlWBATemplate.xlWBATWorksheet);
            var sheetNames = new List<string>();
            foreach (var kvp in tables)
            {
                var table = kvp.Value;
                //must be converted to array of objects for quick population
                //and to avoid "numbers formatted as text" errors
                var maxWidth = table.Max(x => x.Count);
                var maxHeight = table.Count;
                var genericList = new object[table.Count, maxWidth];
                for (int row = 0; row < table.Count; row++)
                    for (int column = 0; column < maxWidth; column++)
                    {
                        if (column >= table[row].Count)
                            genericList[row, column] = string.Empty;
                        else
                            genericList[row, column] = table[row][column];
                    }

                if (!firstSheet)
                    newWorkbook.Sheets.Add(Missing.Value, Missing.Value, Missing.Value, Missing.Value);
                else firstSheet = false;
                var newWorksheet = (Microsoft.Office.Interop.Excel.Worksheet)newWorkbook.ActiveSheet;

                //name worksheet
                var tempName = kvp.Key;
                if (tempName.Length > 25)
                    tempName = tempName.Remove(25);
                if (sheetNames.Contains(tempName))
                {
                    var tempNumber = 2;
                    while (sheetNames.Contains(tempName + tempNumber))
                        tempNumber++;
                    tempName = tempName + tempNumber;
                }
                sheetNames.Add(tempName);
                newWorksheet.Name = tempName;

                var range = newWorksheet.get_Range("A1", string.Format("{0}{1}", IntToColumn(maxWidth), table.Count));
                range.Value2 = genericList;
                range.NumberFormat = "@";
                newWorksheet.Columns.AutoFit();
                for (var x = 1; x <= maxWidth; x++)
                {
                    var column =
                        (Microsoft.Office.Interop.Excel.Range) newWorksheet.Columns.get_Item(x, Missing.Value);
                    var columnWidth = double.Parse(column.ColumnWidth.ToString());
                    if (columnWidth > 75)
                    {
                        column.ColumnWidth = 75;
                        //column.NumberFormat = "General";
                        for (int row = 1;row <= maxHeight; row++)
                        {
                            var cellNumber = IntToColumn(x) + row;
                            var cell = newWorksheet.get_Range(cellNumber, cellNumber);
                            var text = (string)cell.Text;
                            if (text.Length > 200)
                                cell.NumberFormat = "General";
                        }
                    }
                }

                if (kvp.Key != "Summary")
                {
                    newWorksheet.get_Range("A1", Missing.Value).EntireRow.Font.Bold = true;
                    newWorksheet.Activate();
                    if (freezeFirstColumn)
                        newWorksheet.Application.ActiveWindow.SplitColumn = 1;
                    newWorksheet.Application.ActiveWindow.SplitRow = 1;
                    newWorksheet.Application.ActiveWindow.FreezePanes = true;

                }
            }

            newExcel.UserControl = true;
            newExcel.Visible = true;
        }

        public static string CreateHTMLTablePage(List<List<List<string>>> tablesToCreate, string fileName, string title, bool firstRowEmphasis, bool firstColumnEmphasis, bool clusterPage)
        {
            if (File.Exists(fileName))
            {
                if (fileName.EndsWith(".html"))
                fileName = Path.GetFileNameWithoutExtension(fileName);
                var number = 2;
                while (File.Exists(fileName + number))
                    number++;
                fileName += number + ".html";
            }
            var sb =
                new StringBuilder(
                    string.Format(
                        "<html>{0}\t<head>{0}\t\t<title>{1}</title>" +
                        "{0}\t\t<link rel=\"stylesheet\" type=\"text/css\"" +
                        " href=\"idpicker-style.css\" />{0}\t</head>\t<body>{0}",
                        Environment.NewLine, title));
            for (var tableNumber = 0; tableNumber < tablesToCreate.Count;tableNumber++)
            {
                var lastCluster = clusterPage && tableNumber == tablesToCreate.Count - 1;
                var evenRow = true;
                var firstR = firstRowEmphasis;
                sb.AppendLine("\t\t<p><table" + (lastCluster ? " class=t4 border=\"1\"" : string.Empty) + ">");
                //(lastCluster ? " border=\"1\" cellspacing=\"0\"" : string.Empty)

                foreach (var row in tablesToCreate[tableNumber])
                {
                    if (clusterPage && tableNumber == 1)
                    {
                        if (row[0] != string.Empty)
                            evenRow = !evenRow;
                    }
                    var firstC = firstColumnEmphasis || lastCluster;
                    var modifier = firstR ? " id=es1>" : (evenRow ? " id=even>" : ">");
                    if (!clusterPage || tableNumber != 1)
                        evenRow = !evenRow;
                    var line = new StringBuilder("\t\t\t<tr" + modifier);
                    firstR = false;

                    foreach (var cell in row)
                    {
                        var value = cell ?? String.Empty;
                        line.Append("<td" + (firstC ? " id=es1>" : ">") +
                                    value.Replace("     ", "&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;") + "</td>");
                        firstC = false;
                    }

                    line.Append("</tr>");
                    sb.AppendLine(line.ToString());
                }

                sb.AppendLine("\t\t</table></p>");
            }
            sb.Append(string.Format("\t</body>{0}</html>", Environment.NewLine));
            var fileOut = new StreamWriter(fileName);
            fileOut.Write(sb.ToString());
            fileOut.Flush();
            fileOut.Close();
            return fileName;
        }

        public static string CreateHTMLTreePage(List<TreeNode> groups, string fileName, string title, List<string> firstHeaders, List<string> secondHeaders)
        {
            //'Name','Filtered Spectra','Distinct Peptides','Distinct Matches','Distinct Analyses','Distinct Charges'
            var indexList = new List<string>();
            for (int x = 1; x <= firstHeaders.Count;x++ )
                indexList.Add("[" + x + "]");

            if (File.Exists(fileName))
            {
                if (fileName.EndsWith(".html"))
                    fileName = Path.GetFileNameWithoutExtension(fileName);
                var number = 2;
                while (File.Exists(fileName + number))
                    number++;
                fileName += number + ".html";
            }
            var sb = new StringBuilder("<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML//EN\">" + Environment.NewLine +
                                       "<script src=\"idpicker-scripts.js\" language=javascript></script>" +
                                       Environment.NewLine +
                                       "<script language=javascript>" + Environment.NewLine +
                                       "\tvar treeTables = new Array;" + Environment.NewLine +
                                       "\ttreeTables['GroupTable'] = {caption:'Group Table'," +
                                       " show:true, sortable:true, headerSortIndexes: [" +
                                       string.Join(",", indexList.ToArray()) + "]," +
                                       " header:[" + string.Join(",", firstHeaders.ToArray()) + "]," +
                                       " titles:[" + string.Join(",", firstHeaders.ToArray()) + "]," +
                                       " data:[");
            var nodes = new List<string>();
            foreach (var group in groups)
                nodes.Add("[" + string.Join(",", (string[]) group.Tag) + ",{child:'" + group.Text.Replace(".","-") + "'}]");
            sb.AppendLine(string.Join(",", nodes.ToArray()) + "] };");

            foreach (var group in groups)
            {
                sb.Append("\ttreeTables['" + group.Text.Replace(".", "-") + "'] = { sortable:true," +
                          " header:[" + string.Join(",", secondHeaders.ToArray()) + "]," +
                          " titles:[" + string.Join(",", secondHeaders.ToArray()) + "], data:[");
                var groupNodes = new List<string>();
                foreach (TreeNode source in group.Nodes)
                {
                    groupNodes.Add("[" + string.Join(",", (string[]) source.Tag) + "]");
                }
                sb.AppendLine(string.Join(",", groupNodes.ToArray()) + "] };");
            }

            sb.Append(string.Format("</script>{0}" +
                                    "<html>{0}" + "\t<head>{0}" +
                                    "\t\t<title>{1}</title>{0}" +
                                    "\t\t<link rel=\"stylesheet\" type=\"text/css\" href=\"idpicker-style.css\" />{0}" +
                                    "\t</head>{0}" + "\t<body>{0}" +
                                    "\t\t<script language=javascript>document.body.appendChild(makeTreeTable('GroupTable'))</script><br />{0}" +
                                    "\t</body>{0}" + "</html>", Environment.NewLine, title));
            var fileOut = new StreamWriter(fileName);
            fileOut.Write(sb.ToString());
            fileOut.Flush();
            fileOut.Close();
            return fileName;

        }

        public static string IntToColumn(int number)
        {
            //Alas, had an awesome calculation for this, but StackOverflow
            //had a much simpler (and more powerful) method
            int dividend = number;
            string columnName = String.Empty;
            int modulo;

            while (dividend > 0)
            {
                modulo = (dividend - 1) % 26;
                columnName = Convert.ToChar(65 + modulo) + columnName;
                dividend = (dividend - modulo) / 26;
            }

            return columnName;

        }

        public static void CreateNavigationPage(List<string[]> clusterList, string outFolder, string reportName)
        {
            var outPathBase = Path.Combine(outFolder, reportName);
            var outstring = new StringBuilder();
            if (clusterList.Any())
            {
                outstring.Append("<script src=\"idpicker-scripts.js\" language=javascript></script>" + Environment.NewLine +
                                       "<script language=javascript>" + Environment.NewLine +
                                       "var treeTables = new Array;" + Environment.NewLine +
                                       "treeTables['nav'] = { caption:'cluster table of contents'," +
                                       " show:true, sortable:true, header:['Id','Protein Groups','Unique Results','Spectra']," +
                                       " titles:['cluster id','number of protein groups in cluster','number of unique results in" +
                                       " cluster','total number of spectra in cluster'], metadata:[1,0,0,0,0]," +
                                       " headerSortIndexes:[[1],[-3,-4,-5,1],[-4,-3,-5,1],[-5,-4,-3,1]], data:[");
                var formattedClusterList = clusterList.Select(cluster => "[" + string.Join(",", cluster) + "]").ToList();
                outstring.Append(string.Join(",", formattedClusterList.ToArray()));
                outstring.Append("] };" + Environment.NewLine + "</script>");
            }
            outstring.AppendLine("<html>" + Environment.NewLine +
                                 "\t<head>" + Environment.NewLine +
                                 "\t\t<link rel=\"stylesheet\" type=\"text/css\"" +
                                 " href=\"idpicker-style.css\" />" + Environment.NewLine +
                                 "\t</head>" + Environment.NewLine + "<body>");
            if (File.Exists(outPathBase + "-summary.html"))
                outstring.AppendLine("\t\t<a href=\"" + reportName + "-summary.html" +
                                     "\" target=\"mainFrame\">Summary</a><br />");
            if (File.Exists(outPathBase + "-analyses.html"))
                outstring.AppendLine("\t\t<a href=\"" + reportName + "-analyses.html" +
                                     "\" target=\"mainFrame\">Analysis Parameters</a><br />");
            if (File.Exists(outPathBase + "-groups.html"))
                outstring.AppendLine("\t\t<a href=\"" + reportName + "-groups.html" +
                                     "\" target=\"mainFrame\">Index by source groups</a><br />");
            if (File.Exists(outPathBase + "-protein.html"))
                outstring.AppendLine("\t\t<a href=\"" + reportName + "-protein.html" +
                                     "\" target=\"mainFrame\">Index by Protein</a><br />");
            if (File.Exists(outPathBase + "-peptide.html"))
                outstring.AppendLine("\t\t<a href=\"" + reportName + "-peptide.html" +
                                     "\" target=\"mainFrame\">Index by Peptide</a><br />");
            if (File.Exists(outPathBase + "-modificationTable.html"))
                outstring.AppendLine("\t\t<a href=\"" + reportName + "-modificationTable.html" +
                                     "\" target=\"mainFrame\">Modification Summary Table</a><br />");
            if (File.Exists(outPathBase + "-modificationList.html"))
                outstring.AppendLine("\t\t<a href=\"" + reportName + "-modificationList.html" +
                                     "\" target=\"mainFrame\">Modification List</a><br />");
            outstring.Append("\t\t<br /><script language=javascript>document.body." +
                             "appendChild(makeTreeTable('nav'))</script>" + Environment.NewLine +
                             "\t</body>" + Environment.NewLine + "</html>");

            var outfile = new StreamWriter(outPathBase + "-nav.html");
            outfile.Write(outstring.ToString());
            outfile.Flush();
            outfile.Close();
        }

        public static void CreateIndexPage(string outFolder, string reportName)
        {
            var outFile = new StreamWriter(Path.Combine(outFolder, "index.html"));
            outFile.Write(string.Format("<html>{0}" +
                                        "\t<title>{1} IDPicker Analysis</title>{0}" +
                                        "\t<frameset cols=\"240,*\">{0}" +
                                        "\t\t<frame src=\"{1}-nav.html\" />{0}" +
                                        "\t\t<frame src=\"{1}-summary.html\" name=\"mainFrame\" />{0}" +
                                        "\t</frameset>{0}</html>", Environment.NewLine, reportName));
            outFile.Flush();
            outFile.Close();
        }
    }
}
