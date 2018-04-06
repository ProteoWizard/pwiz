﻿//
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
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.IO;
using DigitalRune.Windows.Docking;
using NHibernate;
using NHibernate.Stat;

namespace IDPicker.Forms
{
    public partial class LogForm : DockableForm
    {
        DateTime logStart;
        List<Tuple<TimeSpan, string, string>> logTable;
        NotifyingStringWriter logWriter;
        List<QueryStatistics> queryStatistics;
        ISessionFactory sessionFactory;
        DataTable queryDebuggerTable;

        public TextWriter LogWriter { get { return logWriter; } }

        public LogForm()
        {
            InitializeComponent();

            Icon = Properties.Resources.BlankIcon;

            logStart = System.Diagnostics.Process.GetCurrentProcess().StartTime;
            logTable = new List<Tuple<TimeSpan, string, string>>();
            logWriter = new NotifyingStringWriter();
            logWriter.Wrote += logWriter_Wrote;

            queryStatistics = new List<QueryStatistics>();
            sessionFactory = null;

            CreateHandle(); // HACK: ensure handle is created on the same thread as the control
        }

        public void SetSessionFactory(ISessionFactory sessionFactory)
        {
            this.sessionFactory = sessionFactory;
            refreshStatistics();
        }

        private void refreshStatistics()
        {
            queryStatistics.Clear();
            if (sessionFactory == null)
            {
                tabPage2.Text = "Query Statistics (no session factory)";
                tabPage2.Enabled = false;
            }
            else if (!sessionFactory.Statistics.IsStatisticsEnabled)
            {
                tabPage2.Text = "Query Statistics (no statistics)";
                tabPage2.Enabled = false;
            }
            else
            {
                tabPage2.Text = "Query Statistics";
                tabPage2.Enabled = true;
                foreach (var query in sessionFactory.Statistics.Queries)
                    queryStatistics.Add(sessionFactory.Statistics.GetQueryStatistics(query));
                queryStatistics.Sort((x, y) => -x.ExecutionMaxTime.CompareTo(y.ExecutionMaxTime));
            }
            queryStatisticsDataGridView.RowCount = queryStatistics.Count;
            queryStatisticsDataGridView.Refresh();
        }

        private void logWriter_Wrote (object sender, NotifyingStringWriter.WroteEventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new System.Windows.Forms.MethodInvoker(() => logWriter_Wrote(sender, e)));
                return;
            }

            string entry = e.Text;
            string source = String.Empty;
            var queryCommentMatch = Regex.Match(e.Text, "/\\*\\s*Source:(.*)\\*/");
            if (queryCommentMatch.Success && queryCommentMatch.Groups[1].Success)
            {
                source = queryCommentMatch.Groups[1].Value;
                entry = entry.Replace(queryCommentMatch.Groups[0].Value, "");
            }

            logTable.Add(new Tuple<TimeSpan, string, string>(DateTime.Now - logStart, source, entry));

            queryLogDataGridView.RowCount = logTable.Count;

            if (queryLogDataGridView.DisplayRectangle.Height > 0)
            {
                queryLogDataGridView.FirstDisplayedScrollingRowIndex = Math.Max(0, logTable.Count - 4);
                queryLogDataGridView.Refresh();
            }

            refreshStatistics();
        }

        private void queryLogDataGridView_CellValueNeeded (object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.ColumnIndex == timestampColumn.Index)
                e.Value = logTable[e.RowIndex].Item1.TotalSeconds;
            else if (e.ColumnIndex == sourceColumn.Index)
                e.Value = logTable[e.RowIndex].Item2;
            else
                e.Value = logTable[e.RowIndex].Item3;
        }

        private void queryStatisticsDataGridView_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.ColumnIndex == maxTimeColumn.Index)
                e.Value = queryStatistics[e.RowIndex].ExecutionMaxTime.TotalSeconds;
            else if (e.ColumnIndex == rowCountColumn.Index)
                e.Value = queryStatistics[e.RowIndex].ExecutionRowCount;
            else
                e.Value = queryStatistics[e.RowIndex].CategoryName;
        }

        private void queryDebuggerQueryButton_Click(object sender, EventArgs e)
        {
            using(var session = sessionFactory.OpenStatelessSession())
            {
                try
                {
                    string queryString = queryDebuggerQueryTextBox.Text;
                    var query = session.CreateQuery(queryString).SetReadOnly(true);

                    queryDebuggerDataGridView.DataSource = null;
                    queryDebuggerTable = new DataTable();
                    queryDebuggerTable.BeginLoadData();
                    foreach(var returnType in query.ReturnTypes)
                    {
                        queryDebuggerTable.Columns.Add(returnType.Name, typeof(string));
                    }

                    const string realNumberFormat = "{0:0}";

                    if (queryDebuggerTable.Columns.Count > 1)
                        foreach(object[] row in query.List<object[]>())
                        {
                            for(int i=0; i < row.Length; ++i)
                            {
                                object output = row[i];
                                if (!tryObjectAsArray<double>(row[i], ref output, realNumberFormat))
                                    if (!tryObjectAsArray<float>(row[i], ref output, realNumberFormat))
                                        if (!tryObjectAsArray<int>(row[i], ref output))
                                            if (!tryObjectAsArray<long>(row[i], ref output))
                                                if (!tryObjectAsArray<short>(row[i], ref output))
                                                    if (!tryObjectAsArray<char>(row[i], ref output))
                                                        if (row[i] is byte[])
                                                        {
                                                            byte[] arrayBytes = row[i] as byte[];
                                                            if (arrayBytes == null || arrayBytes.Length % 8 > 0)
                                                                throw new ArgumentException("PivotData assumes that BLOBs are arrays of double precision floats");

                                                            int arrayLength = arrayBytes.Length / 8;

                                                            double[] arrayValues = new double[arrayLength];
                                                            var arrayStream = new BinaryReader(new MemoryStream(arrayBytes));
                                                            for (int j = 0; j < arrayLength; ++j)
                                                                arrayValues[j] += arrayStream.ReadDouble();
                                                            tryObjectAsArray<double>(arrayValues, ref output, realNumberFormat);
                                                        }
                                row[i] = output;
                            }
                            queryDebuggerTable.Rows.Add(row);
                        }
                    else
                        foreach (object row in query.List())
                        {
                            object output = row;
                            if (!tryObjectAsArray<double>(row, ref output, realNumberFormat))
                                if (!tryObjectAsArray<float>(row, ref output, realNumberFormat))
                                    if (!tryObjectAsArray<int>(row, ref output))
                                        if (!tryObjectAsArray<long>(row, ref output))
                                            if (!tryObjectAsArray<short>(row, ref output))
                                                if (!tryObjectAsArray<char>(row, ref output))
                                                    if (row is byte[])
                                                    {
                                                        byte[] arrayBytes = row as byte[];
                                                        if (arrayBytes == null || arrayBytes.Length % 8 > 0)
                                                            throw new ArgumentException("PivotData assumes that BLOBs are arrays of double precision floats");

                                                        int arrayLength = arrayBytes.Length / 8;

                                                        double[] arrayValues = new double[arrayLength];
                                                        var arrayStream = new BinaryReader(new MemoryStream(arrayBytes));
                                                        for (int j = 0; j < arrayLength; ++j)
                                                            arrayValues[j] += arrayStream.ReadDouble();
                                                        tryObjectAsArray<double>(arrayValues, ref output, realNumberFormat);
                                                    }
                            queryDebuggerTable.Rows.Add(output);
                        }
                    queryDebuggerDataGridView.DataSource = queryDebuggerTable;
                    queryDebuggerDataGridView.Refresh();
                }
                catch(Exception ex)
                {
                    Program.HandleUserError(ex);
                }
            }
        }

        private bool tryObjectAsArray<T>(object value, ref object output, string format = "{0}")
        {
            T[] array = value as T[];
            if (array != null)
            {
                output = String.Format("{{ {0} }}", String.Join(", ", array.Select(o => String.Format(format, o))));
                return true;
            }
            return false;
        }

        private void queryDebuggerDataGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            //e.Value = output;
            //e.FormattingApplied = true;
        }

        private void queryDebuggerQueryTextBox_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.F9)
                queryDebuggerQueryButton_Click(sender, e);
        }

        private void queryDebuggerSaveButton_Click(object sender, EventArgs e)
        {
            var sfg = new SaveFileDialog() { FileName = "queryDebugger.tsv", OverwritePrompt = true };
            if (sfg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            using (var fileStream = new StreamWriter(File.OpenWrite(sfg.FileName)))
            {
                for (int i = 0; i < queryDebuggerDataGridView.Rows.Count; ++i)
                {
                    fileStream.Write(queryDebuggerDataGridView[0, i].Value);
                    for (int j = 1; j < queryDebuggerDataGridView.Columns.Count; ++j)
                        fileStream.Write("\t{0}", queryDebuggerDataGridView[j, i].Value);
                    fileStream.WriteLine();
                }
            }
        }
    }

    /// <summary>
    /// A wrapper for StringWriter that sends an event when it is written to.
    /// </summary>
    public class NotifyingStringWriter : StringWriter
    {
        public class WroteEventArgs : EventArgs { public string Text { get; set; } }

        public event EventHandler<WroteEventArgs> Wrote;

        public override void Write (char value)
        {
            base.Write(value);
            OnWrote(value.ToString());
        }

        public override void Write (char[] buffer, int index, int count)
        {
            base.Write(buffer, index, count);
            OnWrote(new string(buffer, index, count));
        }

        public override void Write (string value)
        {
            base.Write(value);
            OnWrote(value);
        }

        protected void OnWrote (string text)
        {
            if (Wrote != null)
                Wrote(this, new WroteEventArgs() {Text = text});
        }
    }
}
