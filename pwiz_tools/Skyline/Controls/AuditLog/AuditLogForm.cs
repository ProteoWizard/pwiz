using System;
using System.Collections;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Controls.AuditLog
{
    public partial class AuditLogForm : DocumentGridForm
    {
        private readonly SkylineWindow _skylineWindow;
        private AuditLogRowSource _auditLogRowSource;

        public AuditLogForm(SkylineViewContext viewContext) : base(viewContext)
        {
            InitializeComponent();
            _skylineWindow = viewContext.SkylineDataSchema.SkylineWindow;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            if (_skylineWindow != null)
            {
                _skylineWindow.DocumentUIChangedEvent += SkylineWindowOnDocumentUIChangedEvent;
            }
        }

        private void SkylineWindowOnDocumentUIChangedEvent(object sender, DocumentChangedEventArgs documentChangedEventArgs)
        {
            if (_auditLogRowSource != null)
            {
                _auditLogRowSource.SetDocument(_skylineWindow.Document);
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (_skylineWindow != null)
            {
                _skylineWindow.DocumentUIChangedEvent -= SkylineWindowOnDocumentUIChangedEvent;
            }

            base.OnHandleDestroyed(e);
        }

        public static AuditLogForm MakeAuditLogForm(SkylineWindow skylineWindow)
        {
            var dataSchema = new SkylineDataSchema(skylineWindow, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            var viewInfo = SkylineViewContext.GetDefaultViewInfo(ColumnDescriptor.RootColumn(dataSchema, typeof(AuditLogRow)));
            var rowSource = new AuditLogRowSource();
            rowSource.SetDocument(skylineWindow.Document);
            var rowSourceInfo = new RowSourceInfo(typeof(AuditLogRow), rowSource, new[]{viewInfo});
            var viewContext = new SkylineViewContext(dataSchema, new[] { rowSourceInfo });

            return new AuditLogForm(viewContext) { _auditLogRowSource = rowSource };
        }

        private class AuditLogRowSource : IRowSource
        {
            private SrmDocument _document;

            public void SetDocument(SrmDocument document)
            {
                _document = document;
                if (RowSourceChanged != null)
                {
                    RowSourceChanged();
                }
            }

            public IEnumerable GetItems()
            {
                return _document.AuditLog;
            }

            public event Action RowSourceChanged;
        }
    }
}
