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
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace SkylineMcpConnector
{
    public partial class MainForm : Form
    {
        private bool _setupExpanded;
        // Suppress CheckedChanged events while probing initial state
        private bool _suppressCheckEvents;

        private string _versionFormat;
        private string _documentFormat;
        private string _setupButtonExpandText;
        private int _groupHeight;

        public MainForm(string[] args)
        {
            InitializeComponent();

            _versionFormat = labelVersion.Text;
            _documentFormat = labelDocument.Text;
            _setupButtonExpandText = buttonSetup.Text;
            _groupHeight = groupBoxSetup.Height;

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
            }

            ShowHideSetupPane();
        }

        private void ConnectToSkyline()
        {
            var connectionInfo = ConnectionInfo.Load();
            if (connectionInfo == null)
            {
                labelStatus.Text = "Waiting for Skyline connection...";
                return;
            }

            labelStatus.Text = "Connected to Skyline";
            labelVersion.Text = string.Format(_versionFormat, connectionInfo.SkylineVersion);
            labelDocument.Text = string.Format(_documentFormat, connectionInfo.DocumentPath ?? "(none)");

            DeployMcpServer();
        }

        private void DeployMcpServer()
        {
            try
            {
                McpServerDeployer.Deploy();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($@"MCP server deployment failed: {ex.Message}");
            }
        }

        // -- Button handlers --

        private void buttonSetup_Click(object sender, EventArgs e)
        {
            ToggleSetupPanel();
        }

        private void buttonClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        // -- Checkbox handlers --

        private void checkClaudeDesktop_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressCheckEvents)
                return;
            if (!EnsureClaudeDesktopStopped())
            {
                RevertCheckbox(checkClaudeDesktop);
                return;
            }
            try
            {
                if (checkClaudeDesktop.Checked)
                {
                    ChatAppRegistry.AddToClaudeDesktop();
                    labelSetupStatus.Text = "Claude Desktop: Registered. Start Claude Desktop to activate.";
                }
                else
                {
                    ChatAppRegistry.RemoveFromClaudeDesktop();
                    labelSetupStatus.Text = "Claude Desktop: Removed.";
                }
            }
            catch (Exception ex)
            {
                labelSetupStatus.Text = "Claude Desktop: " + ex.Message;
                RevertCheckbox(checkClaudeDesktop);
            }
        }

        private void checkClaudeCode_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressCheckEvents)
                return;
            try
            {
                if (checkClaudeCode.Checked)
                {
                    ChatAppRegistry.AddToClaudeCode();
                    labelSetupStatus.Text = "Claude Code: Registered. Restart Claude Code to activate.";
                }
                else
                {
                    ChatAppRegistry.RemoveFromClaudeCode();
                    labelSetupStatus.Text = "Claude Code: Removed. Restart Claude Code to apply.";
                }
            }
            catch (Exception ex)
            {
                labelSetupStatus.Text = "Claude Code: " + ex.Message;
                RevertCheckbox(checkClaudeCode);
            }
        }

        // -- Expand/collapse --

        private void ToggleSetupPanel()
        {
            _setupExpanded = !_setupExpanded;
            ShowHideSetupPane();
        }

        private void ShowHideSetupPane()
        {
            groupBoxSetup.Visible = _setupExpanded;
            buttonSetup.Text = _setupExpanded ? _setupButtonExpandText.Replace(@">>", @"<<") : _setupButtonExpandText;

            int targetHeight = buttonClose.Bottom + 20;
            if (_setupExpanded)
                targetHeight += (groupBoxSetup.Top - buttonClose.Bottom) + _groupHeight;
            MinimumSize = MinimumSize with { Height = 0 };
            ClientSize = ClientSize with { Height = targetHeight };
            MinimumSize = MinimumSize with { Height = Size.Height };

            if (_setupExpanded)
                ProbeRegistrationState();
        }

        /// <summary>
        /// Detect which apps are installed and whether Skyline MCP is registered.
        /// Set checkbox state without firing CheckedChanged events.
        /// </summary>
        private void ProbeRegistrationState()
        {
            _suppressCheckEvents = true;
            try
            {
                // Claude Desktop
                bool desktopInstalled = ChatAppRegistry.IsClaudeDesktopInstalled();
                checkClaudeDesktop.Enabled = desktopInstalled;
                if (desktopInstalled)
                    checkClaudeDesktop.Checked = ChatAppRegistry.IsRegisteredInClaudeDesktop();
                else
                    checkClaudeDesktop.Text = "Claude Desktop (not installed)";

                // Claude Code
                bool codeInstalled = ChatAppRegistry.IsClaudeCodeInstalled();
                checkClaudeCode.Enabled = codeInstalled;
                if (codeInstalled)
                    checkClaudeCode.Checked = ChatAppRegistry.IsRegisteredInClaudeCode();
                else
                    checkClaudeCode.Text = "Claude Code (not installed)";
            }
            finally
            {
                _suppressCheckEvents = false;
            }
        }

        /// <summary>
        /// Claude Desktop keeps its config in memory and overwrites the file on exit.
        /// Ensure it is fully stopped before we write to the config file.
        /// Returns true if safe to proceed, false if the user cancelled.
        /// </summary>
        private bool EnsureClaudeDesktopStopped()
        {
            if (!ChatAppRegistry.IsClaudeDesktopRunning())
                return true;

            MessageBox.Show(this,
                "Claude Desktop must be closed before changing its configuration.\n\n" +
                "Please close Claude Desktop, then click OK.",
                "Claude Desktop Running",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            if (!ChatAppRegistry.IsClaudeDesktopRunning())
                return true;

            var result = MessageBox.Show(this,
                "Claude Desktop is still running.\n\n" +
                "Would you like to stop it now?",
                "Stop Claude Desktop?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return false;

            ChatAppRegistry.StopClaudeDesktop();
            Thread.Sleep(1000); // Brief wait for processes to exit
            return !ChatAppRegistry.IsClaudeDesktopRunning();
        }

        private void RevertCheckbox(CheckBox checkBox)
        {
            _suppressCheckEvents = true;
            checkBox.Checked = !checkBox.Checked;
            _suppressCheckEvents = false;
        }
    }
}
