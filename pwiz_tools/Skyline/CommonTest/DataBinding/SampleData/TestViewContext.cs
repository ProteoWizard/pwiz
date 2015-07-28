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
        private Dictionary<ViewGroupId, ViewSpecList> _viewSpecLists = new Dictionary<ViewGroupId, ViewSpecList>();

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

        public override ViewGroup DefaultViewGroup
        {
            get { return new ViewGroup("default", () => "Default"); }
        }

        protected override void SaveViewSpecList(ViewGroupId viewGroup, ViewSpecList viewSpecList)
        {
            _viewSpecLists[viewGroup] = viewSpecList;
        }

        public override ViewSpecList GetViewSpecList(ViewGroupId viewGroup)
        {
            ViewSpecList viewSpecList;
            if (_viewSpecLists.TryGetValue(viewGroup, out viewSpecList))
            {
                return viewSpecList;
            }
            return base.GetViewSpecList(viewGroup);
        }

        public override void ExportViews(Control owner, ViewSpecList views)
        {
            throw new NotSupportedException();
        }

        public override IEnumerable<ViewGroup> ViewGroups
        {
            get { return new[] {DefaultViewGroup}; }
        }

        public override void ImportViews(Control owner, ViewGroup viewGroup)
        {
            throw new NotSupportedException();
        }

        public override void ExportViewsToFile(Control owner, ViewSpecList views, string fileName)
        {
            throw new NotSupportedException();
        }

        public override void ImportViewsFromFile(Control control, ViewGroup viewGroup, string fileName)
        {
            throw new NotSupportedException();
        }

        public override void CopyViewsToGroup(Control control, ViewGroup viewGroup, ViewSpecList viewSpecList)
        {
            throw new NotSupportedException();
        }
    }
}
