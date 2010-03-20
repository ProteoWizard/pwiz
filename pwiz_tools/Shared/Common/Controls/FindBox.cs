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
        private Timer _timer;
        public FindBox()
        {
            InitializeComponent();
        }

        public DataGridView DataGridView { get; set; }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (_timer == null)
            {
                _timer = new Timer
                             {
                                 Interval = 2000,
                             };
                _timer.Tick += _timer_Tick;
            }
            _timer.Start();
        }

        void _timer_Tick(object sender, EventArgs e)
        {
            if (_timer == null)
            {
                return;
            }
            _timer.Stop();
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
