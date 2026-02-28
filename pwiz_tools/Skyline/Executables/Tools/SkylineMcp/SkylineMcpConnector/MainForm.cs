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
using SkylineTool;

namespace SkylineMcpConnector
{
    public partial class MainForm : Form
    {
        private SkylineToolClient _toolClient;
        private JsonPipeServer _pipeServer;

        public MainForm(string[] args)
        {
            InitializeComponent();

            if (args.Length > 0)
            {
                try
                {
                    _toolClient = new SkylineToolClient(args[0], "Skyline MCP Connector");
                    ConnectToSkyline();
                }
                catch (Exception ex)
                {
                    labelStatus.Text = "Error: " + ex.Message;
                }
            }
            else
            {
                labelStatus.Text = "No Skyline connection provided";
                buttonDisconnect.Enabled = false;
            }
        }

        private void ConnectToSkyline()
        {
            string documentPath = _toolClient.GetDocumentPath();
            var skylineVersion = _toolClient.GetSkylineVersion();
            int processId = _toolClient.GetProcessId();

            // Start the JSON pipe bridge server
            _pipeServer = new JsonPipeServer(_toolClient);
            _pipeServer.Start();

            // Write connection info for the MCP server to find
            var connectionInfo = new ConnectionInfo
            {
                PipeName = _pipeServer.PipeName,
                ProcessId = processId,
                ConnectedAt = DateTime.UtcNow.ToString("o"),
                SkylineVersion = skylineVersion != null ? skylineVersion.ToString() : "unknown",
                DocumentPath = documentPath != null ? documentPath.Replace('\\', '/') : null
            };
            connectionInfo.Save();

            // Update connection status UI
            labelStatus.Text = "Connected to Skyline";
            labelVersion.Text = "Version: " + connectionInfo.SkylineVersion;
            labelDocument.Text = "Document: " + (documentPath ?? "(none)");
            labelPipe.Text = "Pipe: " + _pipeServer.PipeName;

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
            if (_pipeServer != null)
            {
                _pipeServer.Dispose();
                _pipeServer = null;
            }
            ConnectionInfo.Delete();
            if (_toolClient != null)
            {
                _toolClient.Dispose();
                _toolClient = null;
            }
        }
    }
}
