/*
 * Original author: Shannon Joyner <saj9191 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.ToolsUI
{
    public partial class EditServerDlg : FormEx
    {
        private Server _server;
        private readonly IEnumerable<Server> _existing;

        public IPanoramaClient PanoramaClient { get; set; }

        public EditServerDlg(IEnumerable<Server> existing)
        {
            _existing = existing;
            Icon = Resources.Skyline;
            InitializeComponent();
        }

        public void ShowInstructions()
        {
            InstructionPanel.Visible = true;
        }

        public Server Server
        {
            get { return _server; }
            set
            {
                _server = value;
                if (_server == null)
                {
                    textServerURL.Text = string.Empty;
                    textPassword.Text = string.Empty;
                    textUsername.Text = string.Empty;
                }
                else
                {
                    textServerURL.Text = _server.URI.ToString();
                    textPassword.Text = _server.Password;
                    textUsername.Text = _server.Username;
                    string labelText = lblProjectInfo.Text;
                    if (labelText.Contains(textServerURL.Text))
                        lblProjectInfo.Text = labelText.Substring(0, labelText.IndexOf(' ')) + ':';
                }
            }
        }

        public string URL { get { return textServerURL.Text; } set { textServerURL.Text = value; } }
        public string Username { get { return textUsername.Text; } set { textUsername.Text = value; } }
        public string Password { get { return textPassword.Text; } set { textPassword.Text = value; } }

        public void OkDialog()
        {
            MessageBoxHelper helper = new MessageBoxHelper(this);
            string serverName;
            if (!helper.ValidateNameTextBox(textServerURL, out serverName))
                return;

            Uri uriServer = PanoramaUtil.ServerNameToUri(serverName);
            if (uriServer == null)
            {
                helper.ShowTextBoxError(textServerURL, Resources.EditServerDlg_OkDialog_The_text__0__is_not_a_valid_server_name_, serverName);
                return;
            }

            var panoramaClient = PanoramaClient ?? new WebPanoramaClient(uriServer);

            using (var waitDlg = new LongWaitDlg { Text = Resources.EditServerDlg_OkDialog_Verifying_server_information })
            {
                try
                {
                    waitDlg.PerformWork(this, 1000, () => PanoramaUtil.VerifyServerInformation( panoramaClient, Username, Password));
                }
                catch (Exception x)
                {
                    helper.ShowTextBoxError(textServerURL, x.Message);
                    return;
                }
            }

            Uri updatedUri = panoramaClient.ServerUri ?? uriServer;

            if (_existing.Contains(server => !ReferenceEquals(_server, server) && Equals(updatedUri, server.URI)))
            {
                helper.ShowTextBoxError(textServerURL, Resources.EditServerDlg_OkDialog_The_server__0__already_exists_, uriServer.Host);
                return;
            }

            _server = new Server(updatedUri, Username, Password);
            DialogResult = DialogResult.OK;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }
    }
}
