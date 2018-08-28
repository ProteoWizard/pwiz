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

        public static AuditLogEntry GetDefaultEntry(SrmDocument doc)
        {
            return AuditLogEntry.CreateSimpleEntry(doc, MessageType.undocumented_change);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            Entry = string.IsNullOrEmpty(logMessageTextBox.Text)
                ? GetDefaultEntry(_doc)
                : AuditLogEntry.CreateSimpleEntry(_doc, MessageType.modified_outside_of_skyline, logMessageTextBox.Text);
            DialogResult = DialogResult.OK;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Entry = GetDefaultEntry(_doc);
            DialogResult = DialogResult.Cancel;
        }
    }
}
