using System;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls
{
    public partial class DocumentChangeLogEntryDlg : FormEx
    {
        public DocumentChangeLogEntryDlg()
        {
            InitializeComponent();
        }

        public AuditLogEntry Entry { get; private set; }

        public void OkDialog()
        {
            Entry = string.IsNullOrEmpty(logMessageTextBox.Text)
                ? AuditLogEntry.CreateUndocumentedChangeEntry()
                : AuditLogEntry.CreateSimpleEntry(MessageType.modified_outside_of_skyline, SrmDocument.DOCUMENT_TYPE.none, logMessageTextBox.Text);
            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Entry = AuditLogEntry.CreateUndocumentedChangeEntry();
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
