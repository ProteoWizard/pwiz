using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Topograph.Model;
using pwiz.Topograph.ui.Properties;

namespace pwiz.Topograph.ui.Forms
{
    public partial class TpgLinkForm : Form
    {
        public TpgLinkForm()
        {
            InitializeComponent();
            comboDatabaseType.SelectedIndex = 0;
        }
        public bool ShowReadOnlyCheckbox 
        {
            get
            {
                return cbxReadonly.Visible;
            }
            set
            {
                cbxReadonly.Visible = value;
            }
        }
        public bool BrowseOnOk
        {
            get; set;
        }
        public String Server
        {
            get
            {
                return tbxServer.Text;
            }
            set
            {
                tbxServer.Text = value;
            }
        }
        public String Database
        {
            get
            {
                return tbxDatabase.Text;
            }
            set
            {
                tbxDatabase.Text = value;
            }
        }
        public String Username
        {
            get
            {
                return tbxUsername.Text;
            }
            set
            {
                tbxUsername.Text = value;
            }
        }
        public String Password
        {
            get
            {
                return tbxPassword.Text;
            }
            set
            {
                tbxPassword.Text = value;
            }
        }
        public bool Readonly
        {
            get
            {
                return cbxReadonly.Checked;
            }
            set
            {
                cbxReadonly.Checked = value;
            }
        }
        public String OkButtonText
        {
            get
            {
                return btnOK.Text;
            }
            set
            {
                btnOK.Text = value;
            }
        }
        public TpgLinkDef GetTpgLinkDef()
        {
            return new TpgLinkDef
                       {
                           Server = Server,
                           Database = Database,
                           Username = Username,
                           Password = Password,
                           Readonly = Readonly,
                           DatabaseType = Convert.ToString(comboDatabaseType.SelectedItem),
                           Port = string.IsNullOrEmpty(tbxPort.Text) ? (int?) null 
                                : int.Parse(tbxPort.Text)
                       };
        }

        public String Filename { get; set; }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (!CheckRequiredField(tbxServer) 
                || !CheckRequiredField(tbxDatabase) 
                || !CheckRequiredField(tbxUsername) 
                || !CheckRequiredField(tbxPassword))
            {
                return;
            }
            if (BrowseOnOk && string.IsNullOrEmpty(Filename))
            {
                Settings.Default.Reload();
                using (var fileDialog = new SaveFileDialog()
                                     {
                                         Filter = TurnoverForm.OnlineWorkspaceFilter,
                                         InitialDirectory = Settings.Default.WorkspaceDirectory,
                                         FileName = tbxDatabase.Text + ".tpglnk",
                                     })
                {

                    if (fileDialog.ShowDialog(this) == DialogResult.Cancel)
                    {
                        return;
                    }
                    Filename = fileDialog.FileName;
                }
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        private bool CheckRequiredField(TextBox textBox)
        {
            if (!string.IsNullOrEmpty(textBox.Text))
            {
                return true;
            }
            MessageBox.Show(this, "Field cannot be blank", Program.AppName);
            textBox.Focus();
            return false;
        }
    }
}
