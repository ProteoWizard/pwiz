using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.Query;
using pwiz.Topograph.ui.Forms;
using pwiz.Topograph.ui.Properties;
using pwiz.Topograph.ui.Util;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui.Controls
{
    public class QueryGrid : DataGridView
    {
        public QueryGrid()
        {
            CellContentClick += QueryGrid_CellContentClick;
            CellMouseEnter += QueryGrid_CellMouseEnter;
            AllowUserToAddRows = false;
            AllowUserToDeleteRows = false;
            AllowUserToOrderColumns = true;
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            ReadOnly = true;

        }

        void QueryGrid_CellMouseEnter(object sender, DataGridViewCellEventArgs e)
        {
            bool isHyperlink = false;
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                var cell = Rows[e.RowIndex].Cells[e.ColumnIndex];
                isHyperlink = cell.Value is IDbEntity;
            }
            Cursor = isHyperlink ? Cursors.Hand : Cursors.Default;
        }

        void QueryGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }
            var cell = Rows[e.RowIndex].Cells[e.ColumnIndex];
            var value = cell.Value;
            DbPeptideAnalysis dbPeptideAnalysis;
            DbPeptideFileAnalysis dbPeptideFileAnalysis;
            if (!(value is IDbEntity))
            {
                return;
            }
            using (Workspace.GetReadLock())
            {
                using (var session = Workspace.OpenSession())
                {
                    dbPeptideFileAnalysis = null;
                    if (value is DbPeak)
                    {
                        dbPeptideFileAnalysis = session.Get<DbPeak>(((DbPeak) value).Id).PeptideFileAnalysis;
                    }
                    else if (value is DbPeptideFileAnalysis)
                    {
                        dbPeptideFileAnalysis = session.Get<DbPeptideFileAnalysis>(((DbPeptideFileAnalysis) value).Id);
                    }
                    dbPeptideAnalysis = null;
                    if (dbPeptideFileAnalysis != null)
                    {
                        dbPeptideAnalysis = dbPeptideFileAnalysis.PeptideAnalysis;
                    }
                    else
                    {
                        if (value is DbPeptideAnalysis)
                        {
                            dbPeptideAnalysis = session.Get<DbPeptideAnalysis>(((DbPeptideAnalysis) value).Id);
                        }
                    }
                    if (dbPeptideAnalysis == null)
                    {
                        return;
                    }
                }
            }
            var peptideAnalysis = TurnoverForm.Instance.LoadPeptideAnalysis(dbPeptideAnalysis.Id.Value);
            if (peptideAnalysis == null)
            {
                return;
            }
            var form = Program.FindOpenEntityForm<PeptideAnalysisFrame>(peptideAnalysis);
            if (form != null)
            {
                form.Activate();
            }
            else
            {
                var dockableForm = FindForm() as DockableForm;
                form = new PeptideAnalysisFrame(peptideAnalysis);
                if (dockableForm != null)
                {
                    form.Show(dockableForm.DockPanel, dockableForm.DockState);
                }
                else
                {
                    form.Show(TurnoverForm.Instance);
                }
            }
            if (dbPeptideFileAnalysis != null)
            {
                PeptideFileAnalysisFrame.ActivatePeptideDataForm<AbstractChromatogramForm>(form.PeptideAnalysisSummary,
                                                                                   peptideAnalysis.
                                                                                       GetFileAnalysis(
                                                                                       dbPeptideFileAnalysis.Id.
                                                                                           Value));
            }
        }

        public Workspace Workspace { get; set; }

        public bool ExecuteQuery(ParsedQuery parsedQuery, out IList<object[]> rows, out IList<String> columnNames)
        {
            columnNames = new List<string>();
            rows = new List<object[]>();
            using (var session = Workspace.OpenQuerySession())
            {
                var entities = new List<IDbEntity>();
                var entityTypesToQuery = new HashSet<String>();
                var query = session.CreateQuery(parsedQuery.GetExecuteHql());
                var queryExecuter = new QueryExecuter(session, query, new List<object>());
                using (var longWaitDialog = new LongWaitDialog(TopLevelControl, "Executing Query"))
                {
                    var broker = new LongOperationBroker(queryExecuter, longWaitDialog);
                    if (!broker.LaunchJob())
                    {
                        return false;
                    }
                }
                var results = queryExecuter.Results;
                foreach (var selectColumn in parsedQuery.Columns)
                {
                    columnNames.Add(selectColumn.GetColumnName());
                }
                for (int iRow = 0; iRow < results.Count; iRow++)
                {
                    var o = results[iRow];
                    var row = o is object[] ? (object[])o : new[] { o };
                    for (int i = 0; i < row.Length; i++)
                    {
                        var entity = row[i] as IDbEntity;
                        if (entity != null)
                        {
                            entities.Add(entity);
                            entityTypesToQuery.Add(session.GetEntityName(entity));
                        }
                    }
                    rows.Add(row);
                }
                if (entityTypesToQuery.Contains(typeof(DbPeptideFileAnalysis).ToString()))
                {
                    entityTypesToQuery.Add(typeof(DbPeptideAnalysis).ToString());
                    entityTypesToQuery.Add(typeof(DbMsDataFile).ToString());
                    entityTypesToQuery.Add(typeof (DbPeptideFileAnalysis).ToString());
                }
                if (entityTypesToQuery.Contains(typeof(DbPeptideAnalysis).ToString()))
                {
                    entityTypesToQuery.Add(typeof(DbPeptide).ToString());
                    session.CreateCriteria(typeof(DbPeptideAnalysis)).List();
                }
                if (entityTypesToQuery.Contains(typeof(DbPeptide).ToString()))
                {
                    session.CreateCriteria(typeof(DbPeptide)).List();
                }
                if (entityTypesToQuery.Contains(typeof(DbMsDataFile).ToString()))
                {
                    session.CreateCriteria(typeof(DbMsDataFile)).List();
                }
                foreach (var entity in entities)
                {
                    entity.ToString();
                }
                return true;
            }
        }

        public bool ExecuteQuery(ParsedQuery query)
        {
            try
            {
                IList<object[]> rows;
                IList<String> columnNames;
                if (!ExecuteQuery(query, out rows, out columnNames))
                {
                    return false;
                }
                Columns.Clear();
                Rows.Clear();
                foreach (var columnName in columnNames)
                {
                    Columns.Add(new DataGridViewTextBoxColumn { HeaderText = columnName });
                }
                var underlineFont = new Font(Font, FontStyle.Underline);

                if (rows.Count > 0)
                {
                    Rows.Add(rows.Count);
                    for (int iRow = 0; iRow < rows.Count; iRow++)
                    {
                        var values = rows[iRow];
                        var row = Rows[iRow];
                        for (int i = 0; i < Columns.Count && i < values.Length; i++)
                        {
                            var value = values[i];
                            var cell = row.Cells[i];
                            cell.Value = values[i];
                            if (value is IDbEntity)
                            {
                                cell.Style.ForeColor = Color.Blue;
                                cell.Style.Font = underlineFont;
                            }
                        }
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, "An exception occurred:" + exception, Program.AppName);
                return false;
            }
        }

        public void ExportResults(ParsedQuery parsedQuery, String name)
        {
            Settings.Default.Reload();
            using (var dialog = new SaveFileDialog()
            {
                Filter = "Tab Separated Values (*.tsv)|*.tsv|All Files|*.*",
                InitialDirectory = Settings.Default.ExportResultsDirectory,
            })
            {
                if (name != null)
                {
                    dialog.FileName = name + ".tsv";
                }
                if (dialog.ShowDialog(this) == DialogResult.Cancel)
                {
                    return;
                }
                String filename = dialog.FileName;
                Settings.Default.ExportResultsDirectory = Path.GetDirectoryName(filename);
                Settings.Default.Save();
                IList<object[]> rows;
                IList<String> columnNames;
                if (!ExecuteQuery(parsedQuery, out rows, out columnNames))
                {
                    return;
                }
                using (var stream = File.OpenWrite(filename))
                {
                    using (var writer = new StreamWriter(stream))
                    {
                        var tab = "";
                        foreach (var columnName in columnNames)
                        {
                            writer.Write(tab);
                            tab = "\t";
                            writer.Write(columnName);
                        }
                        writer.WriteLine();
                        foreach (var row in rows)
                        {
                            tab = "";
                            foreach (var cell in row)
                            {
                                writer.Write(tab);
                                tab = "\t";
                                writer.Write(StripLineBreaks(cell));
                            }
                            writer.WriteLine();
                        }
                    }
                }
            }
        }

        private static string StripLineBreaks(object value)
        {
            return GridUtil.StripLineBreaks(value);
        }
    }
}
