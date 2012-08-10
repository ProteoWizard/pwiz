using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SettingsCleaner
{
    public partial class SettingsCleaner : Form
    {
        public SettingsCleaner()
        {
            InitializeComponent();
        }

        private void DisclaimerBox_Enter(object sender, EventArgs e)
        {
            ActionButton.Focus();
        }

        private void AcceptBox_CheckedChanged(object sender, EventArgs e)
        {
            ActionButton.Text = AcceptBox.Checked ? "Proceed" : "Exit";
        }

        private void ActionButton_Click(object sender, EventArgs e)
        {
            if (AcceptBox.Checked)
            {
                try
                {
                    var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Bumberdash");
                    var dataFile = Path.Combine(root, "Bumbershoot.db");
                    if (File.Exists(dataFile))
                    {
                        File.Delete(dataFile);
                        MessageBox.Show("Database was successfully reset");
                    }
                    else
                        MessageBox.Show("The database appears to have already been reset");
                }
                catch (Exception)
                {
                    MessageBox.Show("The database appears to be in use");
                    return;
                }
            }

            Application.Exit();
        }
    }
}
