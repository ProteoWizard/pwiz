using System.Collections.Generic;
using System.Windows.Forms;
using pwiz.CommonMsData.RemoteApi;
using pwiz.Skyline.Alerts;

namespace pwiz.Skyline.FileUI
{
    sealed class WatersConnectSaveMethodFileDialog : WatersConnectMethodFileDialog
    {
        public string MethodName { get; private set; }

        public WatersConnectSaveMethodFileDialog(IList<RemoteAccount> remoteAccounts,  IList<string> specificDataSourceFilter = null)
            : base(remoteAccounts, specificDataSourceFilter)
        {
            Text = string.Format(FileUIResources.ExportMethodDlg_OkDialog_Export__0__Method, InstrumentType);
        }
        protected override void DoMainAction()
        {
            Open();
        }

        protected override bool ItemSelected(ListViewItem item)
        {
            MessageDlg.Show(this,
                string.Format(FileUIResources.WatersConnectSaveMethodFileDialog_ItemSelected_The_method_file__0__already_exists, item.Text));
            return false;
        }

        protected override bool ItemSelected(string methodName)
        {
            MethodName = methodName.Trim();
            return true;
        }
    }

    
}
