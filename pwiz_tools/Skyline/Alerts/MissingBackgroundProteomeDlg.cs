using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;

namespace pwiz.Skyline.Alerts
{
    public partial class MissingBackgroundProteomeDlg : Form
    {
        private string _backgroundProteomeName;
        public MissingBackgroundProteomeDlg()
        {
            InitializeComponent();
            Text = Program.Name;
        }

        public string BackgroundProteomeName
        {
            get { return _backgroundProteomeName; }
            set 
            { 
                _backgroundProteomeName = value;
                labelMessage.Text = string.Format("This document requires the '{0}' background proteome.\n" +
                    "Click the Browse button specify its location on your system.\n" +
                    "Click the Open button to open the document without the background proteome.", _backgroundProteomeName);
            }
        }
        
        public string BackgroundProteomeHint { get; set; }

        public string BackgroundProteomePath { get; private set; }
        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnOpen_Click(object sender, System.EventArgs e)
        {
            OkDialog();
        }

        private void btnBrowse_Click(object sender, System.EventArgs e)
        {
            string filterProtDb = string.Format("Proteome File|*" + ProteomeDb.EXT_PROTDB + "|All Files|*.*");

            var openFileDialog = new OpenFileDialog
            {
                Filter = filterProtDb,
                InitialDirectory = Settings.Default.ProteomeDbDirectory,
                Title = "Open Background Protoeme",
                CheckFileExists = true,
                FileName = BackgroundProteomeHint
            };
            if (openFileDialog.ShowDialog() == DialogResult.Cancel)
                return;

            BackgroundProteomePath = openFileDialog.FileName;
            Settings.Default.ProteomeDbDirectory = Path.GetDirectoryName(BackgroundProteomePath);
            OkDialog();
        }

    }
}
