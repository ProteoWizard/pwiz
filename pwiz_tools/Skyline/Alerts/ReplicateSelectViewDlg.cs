using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using NHibernate.Criterion;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Alerts
{
    public partial class ReplicateSelectViewDlg : Form
    {
        private readonly SrmDocument _document;
        public List<string> _checkedRepList; // Allow ShareTypeDlg to be returned and thus be zipped up.
        public ReplicateSelectViewDlg(SrmDocument document)
        {
            InitializeComponent();
            _document = document;
            PopulateListView();
        }

        private void  PopulateListView()
        {
            List<string> repFiles = new List<string>();
            if (_document.Settings.MeasuredResults != null)
            {
                foreach (var a in _document.Settings.MeasuredResults.Chromatograms)
                {
                    IEnumerable<ChromFileInfo> temp = a.MSDataFileInfos;
                    //IEnumerable<ChromFileInfo> temp2 = a.MSDataFileInfos.GetEnumerator(MsDataFilePath);

                    foreach (ChromFileInfo b in temp)
                    {
                        ListViewItem x = new ListViewItem(b.FilePath.GetFileName());
                        x.SubItems.Add(b.FilePath.GetFileLastWriteTime().ToString());
                        x.SubItems.Add(b.FilePath.GetFilePath());
                        listView.Items.Add(x);
                        repFiles.Add(b.FilePath.GetFileName());
                    }
                }
            }

                //Testing
            // Console.WriteLine("File Paths: \n");
            // repFiles.ForEach(Console.WriteLine);
            // Console.WriteLine("End \n");
        }

        private void Btn_Cancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void Btn_Accept_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void OkDialog()
        {
            _checkedRepList = new List<string>();
            foreach (ListViewItem a in listView.CheckedItems)
            {
                _checkedRepList.Add(a.SubItems[2].Text); //Get the file path of each checked item
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
