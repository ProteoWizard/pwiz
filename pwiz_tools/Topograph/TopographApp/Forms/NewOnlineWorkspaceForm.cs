using System;
using System.IO;
using System.Windows.Forms;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.ui.Properties;

namespace pwiz.Topograph.ui.Forms
{
    public partial class NewOnlineWorkspaceForm : Form
    {
        public NewOnlineWorkspaceForm()
        {
            InitializeComponent();
            Icon = Resources.TopographIcon;
            comboDatabaseType.SelectedIndex = 0;
            var connectionSettings = Settings.Default.ConnectionSettings;
            tbxServer.Text = connectionSettings.Server ?? "";
            tbxPort.Text = connectionSettings.Port ?? "";
            tbxUsername.Text = connectionSettings.Username ?? "";
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
                           DatabaseType = Convert.ToString(comboDatabaseType.SelectedItem),
                           Port = string.IsNullOrEmpty(tbxPort.Text) ? (int?) null 
                                : int.Parse(tbxPort.Text)
                       };
        }

        public String Filename { get; set; }

        private void BtnOkOnClick(object sender, EventArgs e)
        {
            if (!CheckRequiredField(tbxServer) 
                || !CheckRequiredField(tbxDatabase) 
                || !CheckRequiredField(tbxUsername) 
                || !CheckRequiredField(tbxPassword))
            {
                return;
            }
            Settings.Default.Reload();
            var tpgLnkDef = GetTpgLinkDef();
            try
            {
                using (tpgLnkDef.OpenConnectionNoDatabase())
                {
                    
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(this,
                                string.Format(
                                    "The server, port, username, or password is invalid.  There was an error connecting to the database: {0}",
                                    exception), Program.AppName);
                return;
            }
            bool workspaceExists;
            try
            {
                var workspaceUpgrader = new WorkspaceUpgrader(tpgLnkDef);
                using (var connection = tpgLnkDef.OpenConnection())
                {
                    try
                    {
                        workspaceUpgrader.ReadSchemaVersion(connection);
                        if (MessageBox.Show(this,
                                        string.Format(
                                            "The database {0} already exists.  Instead of creating a new Online Workspace, would you like to save a .tpglnk file which allows you to connect to this existing Online Workspace.",
                                            tpgLnkDef.Database), Program.AppName, MessageBoxButtons.YesNo) == DialogResult.No)
                        {
                            return;
                        }
                        workspaceExists = true;
                    }
                    catch (Exception)
                    {
                        MessageBox.Show(this, string.Format("The database {0} already exists but does not appear to be a Topograph workspace.", tpgLnkDef.Database),
                                        Program.AppName);
                        return;
                    }
                }
            }
            catch (Exception)
            {
                workspaceExists = false;
            }
            if (!workspaceExists)
            {
                try
                {
                    using (var sessionFactory = tpgLnkDef.CreateDatabase())
                    {
                        TopographForm.InitWorkspace(sessionFactory);
                    }
                }
                catch (Exception exception)
                {
                    MessageBox.Show(this, string.Format("There was an error creating the database: {0}", exception), Program.AppName);
                    return;
                }
            }
            while (true)
            {
                using (var fileDialog = new SaveFileDialog
                {
                    Filter = TopographForm.OnlineWorkspaceFilter,
                    InitialDirectory = Settings.Default.WorkspaceDirectory,
                    FileName = tbxDatabase.Text + ".tpglnk",
                })
                {
                    if (fileDialog.ShowDialog(this) == DialogResult.Cancel)
                    {
                        DialogResult = DialogResult.Cancel;
                    }
                    try
                    {
                        tpgLnkDef.Save(fileDialog.FileName);
                    }
                    catch (Exception exception)
                    {
                        MessageBox.Show(this, string.Format("Error saving .tpglnk file: {0}", exception),
                                        Program.AppName);
                        continue;
                    }
                    Filename = fileDialog.FileName;
                    Settings.Default.WorkspaceDirectory = Path.GetDirectoryName(Filename);
                    break;
                }
            }
            Settings.Default.ConnectionSettings = new ConnectionSettings
            {
                Server = tbxServer.Text,
                Port = tbxPort.Text,
                Username = tbxUsername.Text,
            };
            Settings.Default.Save();
            DialogResult = DialogResult.OK;
            Close();
        }

        private bool CheckRequiredField(Control textBox)
        {
            if (!string.IsNullOrEmpty(textBox.Text))
            {
                return true;
            }
            MessageBox.Show(this, "Field cannot be blank", Program.AppName);
            textBox.Focus();
            return false;
        }

        private void BtnVerifyCredentialsOnClick(object sender, EventArgs e)
        {
            try
            {
                var tpgLnkDef = GetTpgLinkDef();
                string prefix = tpgLnkDef.GetDatabaseNamePrefixForUser();
                if (!string.IsNullOrEmpty(prefix))
                {
                    MessageBox.Show(this,
                                    string.Format(
                                        "Successfully connected to server.  You appear to have permissions on databases with names starting with '{0}'.",
                                        prefix), Program.AppName);

                    if (string.IsNullOrEmpty(tbxDatabase.Text))
                    {
                        tbxDatabase.Text = prefix;
                    }
                }
                else
                {
                    MessageBox.Show(this, "Successfully connected to server.", Program.AppName);
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(this,
                                string.Format(
                                    "The server, port, username, or password is probably incorrect.  An error occured trying to connect to the server: {0}",
                                    exception));
            }
        }
    }
}
