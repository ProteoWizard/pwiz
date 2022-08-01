using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Alerts
{
    public partial class ReplicateSelectViewDlg : Form
    {
        private readonly SrmDocument _document;
        public List<string> ReplicateFilesToInclude { get; private set; } // Data files that user wants to include in the .sky.zip file
        public ReplicateSelectViewDlg(SrmDocument document)
        {
            InitializeComponent();
            _document = document;
            PopulateListView();
        }


        private void  PopulateListView()
        {
            var paths = new HashSet<string>(); // List of file paths
            if (_document.Settings.MeasuredResults != null)
            {
                foreach (var chromatogramSet in _document.Settings.MeasuredResults.Chromatograms)
                {
                    foreach (var chromFileInfo in chromatogramSet.MSDataFileInfos)
                    {
                        // Check for path validity, using our standard rules for locating data files when they aren't in current working directory
                        if (ScanProvider.FileExists(Program.MainWindow.DocumentFilePath, chromFileInfo.FilePath, out var path))
                        {
                            paths.Add(path);
                        }
                    }
                }
                var repFiles = paths.ToList();
                repFiles.Sort(NaturalComparer.Compare); // Natural Sort
                
                // Add to list view for selection
                foreach (var fileName in repFiles)
                {
                    ListViewItem item = new ListViewItem(fileName);
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
            ReplicateFilesToInclude = new List<string>();
            foreach (ListViewItem a in listView.CheckedItems)
            {
                ReplicateFilesToInclude.Add(a.Text); //Get the file path of each checked item
            }


            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
