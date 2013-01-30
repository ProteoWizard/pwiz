using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using pwiz.Topograph.ui.Properties;

namespace pwiz.Topograph.ui.Util
{
    public static class GridUtil
    {
        public static void ExportResults(DataGridView dataGridView, String name)
        {
            Settings.Default.Reload();
            using (var dialog = new SaveFileDialog
                {
                    Filter = "Tab Separated Values (*.tsv)|*.tsv|All Files|*.*",
                    InitialDirectory = Settings.Default.ExportResultsDirectory,
                })
            {
                if (name != null)
                {
                    dialog.FileName = name + ".tsv";
                }
                if (dialog.ShowDialog(dataGridView) == DialogResult.Cancel)
                {
                    return;
                }
                String filename = dialog.FileName;
                Settings.Default.ExportResultsDirectory = Path.GetDirectoryName(filename);
                Settings.Default.Save();
                var columns = GetColumnsSortedByDisplayIndex(dataGridView).Where(c => c.Visible).ToArray();
                using (var stream = File.OpenWrite(filename))
                {
                    using (var writer = new StreamWriter(stream))
                    {
                        var tab = "";
                        foreach (var column in columns)
                        {
                            writer.Write(tab);
                            tab = "\t";
                            writer.Write(column.HeaderText);
                        }
                        writer.WriteLine();
                        for (int iRow = 0; iRow < dataGridView.Rows.Count; iRow++)
                        {
                            var row = dataGridView.Rows[iRow];
                            tab = "";
                            foreach (var column in columns)
                            {
                                if (!column.Visible)
                                {
                                    continue;
                                }
                                writer.Write(tab);
                                tab = "\t";
                                writer.Write(StripLineBreaks(row.Cells[column.Index].Value));
                            }
                            writer.WriteLine();
                        }
                    }
                }
            }
        }
        public static string StripLineBreaks(object value)
        {
            if (value == null)
            {
                return null;
            }
            return Convert.ToString(value)
                .Replace("\r\n", " ")
                .Replace("\r", " ")
                .Replace("\n", " ");
        }
        public static IList<DataGridViewColumn> GetColumnsSortedByDisplayIndex(DataGridView dataGridView)
        {
            var result = new DataGridViewColumn[dataGridView.Columns.Count];
            dataGridView.Columns.CopyTo(result, 0);
            Array.Sort(result, (a, b) => a.DisplayIndex.CompareTo(b.DisplayIndex));
            return result;
        }
    }
}
