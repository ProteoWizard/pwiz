using System;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls
{
    public partial class DocumentChangeLogEntryDlg : FormEx
    {
        private readonly SrmDocument _doc;

        public DocumentChangeLogEntryDlg(SrmDocument doc)
        {
            InitializeComponent();

            _doc = doc;
        }

        public AuditLogEntry Entry { get; private set; }

        public void OkDialog()
        {
            Entry = string.IsNullOrEmpty(logMessageTextBox.Text)
                ? AuditLogEntry.GetUndocumentedChangeEntry(_doc)
                : AuditLogEntry.CreateSimpleEntry(_doc, MessageType.modified_outside_of_skyline, logMessageTextBox.Text);
            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Entry = AuditLogEntry.GetUndocumentedChangeEntry(_doc);
            DialogResult = DialogResult.Cancel;
        }

        // Test support
        public string LogMessage
        {
            get { return logMessageTextBox.Text; }
            set { logMessageTextBox.Text = value; }
        }
    }
}
