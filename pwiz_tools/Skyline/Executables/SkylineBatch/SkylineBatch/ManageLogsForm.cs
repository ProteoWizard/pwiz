using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SkylineBatch
{
    public partial class ManageLogsForm : Form
    {
        //private readonly ConfigManager _configManager;
        //public ManageLogsForm(ConfigManager configManager)
        public ManageLogsForm(ConfigManager configManager)
        {
            //_configManager = configManager;
            InitializeComponent();
           /* if (_configManager.logs.Count > 0)
                listLogs.Items.AddRange(_configManager.GetOldLogFiles());*/
        }

        /*private void btnDelete_Click(object sender, EventArgs e)
        {
            /*var deletingItems = new string[listLogs.SelectedItems.Count];
            listLogs.SelectedItems.CopyTo(deletingItems, 0);
            foreach (var log in deletingItems)
            {
                listLogs.Items.Remove(log);
            }* /
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            /*var logsToKeep = new object[listLogs.Items.Count];
            listLogs.Items.CopyTo(logsToKeep, 0);
            _configManager.DeleteExtraLogs(logsToKeep);* /
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }*/
    }
}
