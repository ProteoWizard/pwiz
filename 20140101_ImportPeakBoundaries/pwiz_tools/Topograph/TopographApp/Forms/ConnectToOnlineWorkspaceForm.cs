/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.ui.Properties;

namespace pwiz.Topograph.ui.Forms
{
    public partial class ConnectToOnlineWorkspaceForm : Form
    {
        public ConnectToOnlineWorkspaceForm()
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
                return comboDatabaseName.Text;
            }
            set
            {
                comboDatabaseName.Text = value;
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
                || !CheckRequiredField(comboDatabaseName) 
                || !CheckRequiredField(tbxUsername) 
                || !CheckRequiredField(tbxPassword))
            {
                return;
            }
            Settings.Default.Reload();
            var tpgLinkDef = GetTpgLinkDef();
            var workspaceUpgrader = new WorkspaceUpgrader(tpgLinkDef);
            try
            {
                using (var connection = workspaceUpgrader.OpenConnection())
                {
                    try
                    {
                        workspaceUpgrader.ReadSchemaVersion(connection);
                    }
                    catch
                    {
                        MessageBox.Show(this,
                                        string.Format("The database {0} is not a Topograph workspace",
                                                      tpgLinkDef.Database));
                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(this,
                                string.Format("There was an error trying to connect to the database: {0}",
                                              exception), Program.AppName);
                return;
            }
            using (var fileDialog = new SaveFileDialog
                                 {
                                     Filter = TopographForm.OnlineWorkspaceFilter,
                                     InitialDirectory = Settings.Default.WorkspaceDirectory,
                                     FileName = comboDatabaseName.Text + ".tpglnk",
                                 })
            {

                while (true)
                {
                    if (fileDialog.ShowDialog(this) == DialogResult.Cancel)
                    {
                        return;
                    }
                    try
                    {
                        tpgLinkDef.Save(fileDialog.FileName);
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
        

        private void BtnShowDatabasesOnClick(object sender, EventArgs e)
        {
            var tpgLnkDef = GetTpgLinkDef();
            try
            {
                var databaseNames = tpgLnkDef.ListDatabaseNames();
                comboDatabaseName.Items.Clear();
                comboDatabaseName.Items.AddRange(databaseNames.Cast<object>().ToArray());
                comboDatabaseName.Focus();
                comboDatabaseName.DroppedDown = true;
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    this,
                    string.Format("An exception occured trying to fetch the list of database names: {0}", exception),
                    Program.AppName);
            }
        }
    }
}
