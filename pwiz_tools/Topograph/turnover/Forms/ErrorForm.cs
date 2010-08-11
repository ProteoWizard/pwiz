using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui.Forms
{
    public partial class ErrorForm : Form
    {
        public ErrorForm()
        {
            InitializeComponent();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            var errors = ErrorHandler.GetErrors();
            ErrorHandler.ErrorAdded += ErrorHandler_ErrorAdded;
            if (errors.Count == 0)
            {
                return;
            }
            dataGridView1.Rows.Add(errors.Count);
            for (int i = 0; i < errors.Count; i++)
            {
                UpdateRow(dataGridView1.Rows[i], errors[i]);
            }
        }
        protected override void OnHandleDestroyed(EventArgs e)
        {
            ErrorHandler.ErrorAdded -= ErrorHandler_ErrorAdded;
            base.OnHandleDestroyed(e);
        }


        void ErrorHandler_ErrorAdded(Error error)
        {
            try
            {
                BeginInvoke(new Action(() =>
                                           {
                                               while (dataGridView1.Rows.Count >= ErrorHandler.MaxErrorCount)
                                               {
                                                   dataGridView1.Rows.RemoveAt(0);
                                               }
                                               UpdateRow(dataGridView1.Rows[dataGridView1.Rows.Add()], error);
                                           }));
            }
            catch
            {
                
            }
        }

        void UpdateRow(DataGridViewRow row, Error error)
        {
            row.Tag = error;
            row.Cells[colTime.Index].Value = error.DateTime;
            row.Cells[colMessage.Index].Value = error.Message;
            row.Cells[colDetail.Index].Value = error.Detail;
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            dataGridView1.Rows.Clear();
            ErrorHandler.ClearErrors();
        }
    }
}
