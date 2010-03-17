using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace pwiz.Common.Controls
{
    public partial class FindBox : UserControl
    {
        public FindBox()
        {
            InitializeComponent();
        }

        public DataGridView DataGridView { get; set; }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            var dataGridView = DataGridView;
            if (dataGridView == null)
            {
                return;
            }
            var text = textBox1.Text;
            var rows = new DataGridViewRow[dataGridView.Rows.Count];
            var rowsRemoved = false;
            dataGridView.Rows.CopyTo(rows, 0);
            foreach (var row in rows)
            {
                var visible = false;
                if (string.IsNullOrEmpty(text))
                {
                    visible = true;
                }
                else
                {
                    for (int iCol = 0; iCol < row.Cells.Count; iCol++)
                    {
                        var cell = row.Cells[iCol];
                        if (cell.Value == null)
                        {
                            continue;
                        }
                        var strValue = cell.Value.ToString();
                        if (strValue.IndexOf(text) >= 0)
                        {
                            visible = true;
                            break;
                        }
                    }
                }
                if (visible == row.Visible)
                {
                    continue;
                }
                if (!rowsRemoved)
                {
                    dataGridView.Rows.Clear();
                    rowsRemoved = true;
                }
                row.Visible = visible;
            }
            if (rowsRemoved)
            {
                dataGridView.Rows.AddRange(rows);
            }
        }
    }
}
