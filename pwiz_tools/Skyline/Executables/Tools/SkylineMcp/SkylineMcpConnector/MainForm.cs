/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.6) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Windows.Forms;

namespace SkylineMcpConnector
{
    public partial class MainForm : Form
    {
        public MainForm(string[] args)
        {
            InitializeComponent();

            // The $(SkylineConnection) arg triggers StartToolService() in Skyline,
            // which creates the JsonToolServer and writes connection.json.
            // We just need to read it and display status.
            try
            {
                ConnectToSkyline();
            }
            catch (Exception ex)
            {
                labelStatus.Text = "Error: " + ex.Message;
                buttonDisconnect.Enabled = false;
            }
        }

        private void ConnectToSkyline()
        {
            var connectionInfo = ConnectionInfo.Load();
            if (connectionInfo == null)
            {
                labelStatus.Text = "Waiting for Skyline connection...";
                buttonDisconnect.Enabled = false;
                return;
            }

            // Update connection status UI
            labelStatus.Text = "Connected to Skyline";
            labelVersion.Text = "Version: " + connectionInfo.SkylineVersion;
            labelDocument.Text = "Document: " + (connectionInfo.DocumentPath ?? "(none)");
            labelPipe.Text = "Pipe: " + connectionInfo.PipeName;

            // Deploy MCP server and show setup help
            DeployMcpServer();
        }

        private void DeployMcpServer()
        {
            try
            {
                bool deployed = McpServerDeployer.Deploy();
                labelMcpStatus.Text = deployed
                    ? "MCP server deployed to " + McpServerDeployer.DeployDir
                    : "MCP server is up to date";
            }
            catch (Exception ex)
            {
                labelMcpStatus.Text = "MCP server not available: " + ex.Message;
            }

            // Show the registration command regardless of deployment status
            textMcpCommand.Text = McpServerDeployer.GetRegistrationCommand();
        }

        private void buttonCopyCommand_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(textMcpCommand.Text);
            buttonCopyCommand.Text = "Copied!";
            var timer = new Timer { Interval = 2000 };
            timer.Tick += (s, a) =>
            {
                buttonCopyCommand.Text = "Copy";
                timer.Stop();
                timer.Dispose();
            };
            timer.Start();
        }

        private void buttonDisconnect_Click(object sender, EventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Cleanup();
        }

        private void Cleanup()
        {
            // Skyline's JsonToolServer owns connection.json lifecycle,
            // but clean up if it's still around when we exit
            ConnectionInfo.Delete();
        }
    }
}
