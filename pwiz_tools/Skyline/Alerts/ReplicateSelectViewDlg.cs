using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using NHibernate.Criterion;
using pwiz.Common.Collections;
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
            List<string> repFiles = new List<string>(); //List of file paths
            if (_document.Settings.MeasuredResults != null)
            {
                foreach (var a in _document.Settings.MeasuredResults.Chromatograms)
                {
                    IEnumerable<ChromFileInfo> temp = a.MSDataFileInfos;

                    foreach (ChromFileInfo b in temp)
                    {
                        //Check for path validity
                        if (ScanProvider.FileExists(Program.MainWindow.DocumentFilePath, b.FilePath, out string path))
                        {
                            repFiles.Add(path);
                        }
                    }
                }
                repFiles.Distinct(); // Only keep unique paths
                repFiles.Sort((x, y) => NaturalComparer.Compare(x, y)); // Natural Sort
                
                // Add to list view for selection
                foreach (var a in repFiles)
                {
                    ListViewItem item = new ListViewItem(a);
                    listView.Items.Add(item);
                }
            }
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

        /// <summary>
        /// Add all checked boxes to list
        /// </summary>
        private void OkDialog()
        {
            _checkedRepList = new List<string>();
            foreach (ListViewItem a in listView.CheckedItems)
            {
                _checkedRepList.Add(a.Text); //Get the file path of each checked item
            }


            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
