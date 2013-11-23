using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;

namespace CommonTest.DataBinding.SampleData
{
    public class TestViewContext : AbstractViewContext
    {
        private ViewSpecList _viewSpecList = ViewSpecList.EMPTY;

        public TestViewContext(DataSchema dataSchema, IEnumerable<RowSourceInfo> rowSourceInfos) : base(dataSchema, rowSourceInfos)
        {
            
        }
        public override string GetExportDirectory()
        {
            return Directory.GetCurrentDirectory();
        }

        public override void SetExportDirectory(string value)
        {
            
        }

        public override DialogResult ShowMessageBox(Control owner, string message, MessageBoxButtons messageBoxButtons)
        {
            // ReSharper disable once LocalizableElement
            return MessageBox.Show(owner, message, "Test View Context", messageBoxButtons);
        }

        public override bool RunLongJob(Control owner, Action<IProgressMonitor> job)
        {
            throw new NotSupportedException();
        }

        protected override ViewSpecList GetViewSpecList()
        {
            return _viewSpecList;
        }

        protected override void SaveViewSpecList(ViewSpecList viewSpecList)
        {
            _viewSpecList = viewSpecList;
        }

        public override void ExportViews(Control owner, IEnumerable<ViewSpec> views)
        {
            throw new NotSupportedException();
        }

        public override void ImportViews(Control owner)
        {
            throw new NotSupportedException();
        }
    }
}
