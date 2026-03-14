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
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using SkylineTool;
using Timer = System.Windows.Forms.Timer;
using Version = SkylineTool.Version;

namespace SkylineAiConnector
{
    public partial class MainForm : Form
    {
        private bool _setupExpanded;
        // Suppress CheckedChanged events while probing initial state
        private bool _suppressCheckEvents;

        private readonly string _versionFormat;
        private readonly string _documentFormat;
        private readonly string _setupButtonExpandText;
        private readonly int _groupHeight;

        private bool _autoConnectEnabled;

        private int _skylineProcessId;
        private RemoteClient _remoteClient;
        private Timer _skylineMonitorTimer;

        public MainForm(string[] args)
        {
            InitializeComponent();
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            _versionFormat = labelVersion.Text;
            _documentFormat = labelDocument.Text;
            _setupButtonExpandText = buttonSetup.Text;
            _groupHeight = groupBoxSetup.Height;

            ShowHideSetupPane();

            // The $(SkylineConnection) arg is the legacy ToolService pipe name (a GUID).
            // Launching this tool triggers StartToolService() in Skyline, which creates
            // the JsonToolServer pipe. The JSON pipe name is derived from the same GUID.
            // We write the connection file to advertise this Skyline instance to MCP clients.
            try
            {
                ConnectToSkyline(args);
            }
            catch (Exception ex)
            {
                labelStatus.Text = "Error: " + ex.Message;
            }
        }

        private void ConnectToSkyline(string[] args)
        {
            if (args.Length == 0 || string.IsNullOrEmpty(args[0]))
            {
                labelStatus.Text = "No Skyline connection argument provided.";
                return;
            }

            string legacyPipeName = args[0];

            // Query Skyline version and process ID via the legacy ToolService pipe
            Version version;
            int processId;
            try
            {
                _remoteClient = new RemoteClient(legacyPipeName);
                version = (Version)_remoteClient.RemoteCallName("GetVersion", new object[0]);
                processId = (int)_remoteClient.RemoteCallName("GetProcessId", new object[0]);
            }
            catch
            {
                labelStatus.Text = "Could not connect to Skyline.";
                return;
            }

            if (!IsSupportedVersion(version))
            {
                labelStatus.Text = string.Format("Skyline {0}.{1}.{2}.{3} does not support AI connections.",
                    version.Major, version.Minor, version.Build, version.Revision);
                labelVersion.Text = "This tool requires Skyline 26.1.1.061 or later.";
                labelDocument.Visible = false;
                buttonSetup.Enabled = false;

                // Still monitor the Skyline process so the form closes when Skyline exits
                StartSkylineMonitor(processId);
                return;
            }

            string versionString = string.Format("{0}.{1}.{2}.{3}",
                version.Major, version.Minor, version.Build, version.Revision);

            // Ask Skyline to write the connection file and get auto-connect state
            string mcpResult;
            try
            {
                mcpResult = (string)_remoteClient.RemoteCallName("StartMcpConnection", new object[] { null });
            }
            catch
            {
                labelStatus.Text = "Could not start MCP connection in Skyline.";
                return;
            }

            _autoConnectEnabled = ParseAutoConnect(mcpResult);
            _suppressCheckEvents = true;
            checkAutoConnect.Checked = _autoConnectEnabled;
            _suppressCheckEvents = false;

            labelStatus.Text = _autoConnectEnabled
                ? "Skyline will connect to AI at startup."
                : "Connected to Skyline";
            labelVersion.Text = string.Format(_versionFormat, versionString);
            UpdateDocumentPath();

            DeployMcpServer();
            StartSkylineMonitor(processId);

            // Auto-expand setup panel on first use when no AI clients are registered
            if (!ChatAppRegistry.AnyClientRegistered())
            {
                _setupExpanded = true;
                ShowHideSetupPane();
            }
        }

        private static bool IsSupportedVersion(Version version)
        {
            if (version.Major < 26) // 25.x or earlier
                return false;
            if (version.Major == 26 && version.Minor < 1) // 26.0.9
                return false;
            if (version.Major == 26 && version.Minor == 1 && version.Build < 1) // 26.1.0
                return false;
            return version.Major != 26 || version.Minor != 1 || version.Build != 1 || version.Revision >= 61; // 26.1.1.xxx < 61
        }

        private void DeployMcpServer()
        {
            if (!McpServerDeployer.IsDotNet8Installed())
            {
                var result = MessageBox.Show(this,
                    "The AI Connector requires the .NET 8.0 Desktop Runtime, which was not found on this computer.\n\n" +
                    "Would you like to open the download page?",
                    ".NET 8.0 Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (result == DialogResult.Yes)
                    Process.Start(McpServerDeployer.DotNetDownloadUrl);
                labelStatus.Text = ".NET 8.0 Desktop Runtime is required.";
                return;
            }

            try
            {
                if (!McpServerDeployer.Deploy())
                    return; // Already up to date
            }
            catch (IOException)
            {
                // Files are locked by a running MCP server process — stop it and retry
                StopMcpServerProcesses();
                try
                {
                    McpServerDeployer.Deploy();
                }
                catch (Exception ex2)
                {
                    MessageBox.Show(this,
                        "Could not update the MCP server. Close all AI apps using the MCP server, then reconnect.\n\n" +
                        ex2.Message,
                        "MCP Server Update Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                MessageBox.Show(this,
                    "The MCP server was updated and a previous instance was stopped.\n\n" +
                    "Please restart your AI apps to reconnect.",
                    "MCP Server Updated",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            // Status label note — settings take effect next time Claude Desktop is launched
            labelStatus.Text = "Connected. MCP server updated.";
        }

        /// <summary>
        /// Kill any running SkylineMcpServer processes so that the server DLL
        /// can be overwritten during deployment.
        /// </summary>
        private static void StopMcpServerProcesses()
        {
            foreach (var process in Process.GetProcessesByName("SkylineMcpServer"))
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill();
                    process.WaitForExit(3000);
                }
                catch
                {
                    // Best effort
                }
                finally
                {
                    process.Dispose();
                }
            }
            // Brief wait for file handles to release
            Thread.Sleep(500);
        }

        /// <summary>
        /// Start a timer that polls whether the Skyline process is still running.
        /// When Skyline exits, the connector closes itself.
        /// </summary>
        private void StartSkylineMonitor(int processId)
        {
            _skylineProcessId = processId;
            _skylineMonitorTimer = new Timer { Interval = 2000 };
            _skylineMonitorTimer.Tick += SkylineMonitorTimer_Tick;
            _skylineMonitorTimer.Start();
        }

        private void SkylineMonitorTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                Process.GetProcessById(_skylineProcessId);
            }
            catch (ArgumentException)
            {
                // Skyline process no longer exists
                _skylineMonitorTimer.Stop();
                Close();
                return;
            }

            UpdateDocumentPath();
        }

        private void UpdateDocumentPath()
        {
            if (_remoteClient == null)
                return;

            try
            {
                string path = (string)_remoteClient.RemoteCallName("GetDocumentPath", new object[0]);
                labelDocument.Text = string.Format(_documentFormat, path ?? "(unsaved)");
                labelDocument.Visible = true;
            }
            catch
            {
                // Skyline may be busy or shutting down — leave label as-is
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

        private void checkGeminiCli_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressCheckEvents)
                return;
            try
            {
                if (checkGeminiCli.Checked)
                {
                    ChatAppRegistry.AddToGeminiCli();
                    labelSetupStatus.Text = "Gemini CLI: Registered.";
                }
                else
                {
                    ChatAppRegistry.RemoveFromGeminiCli();
                    labelSetupStatus.Text = "Gemini CLI: Removed.";
                }
            }
            catch (Exception ex)
            {
                labelSetupStatus.Text = "Gemini CLI: " + ex.Message;
                RevertCheckbox(checkGeminiCli);
            }
        }

        private void checkVSCode_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressCheckEvents)
                return;
            try
            {
                if (checkVSCode.Checked)
                {
                    ChatAppRegistry.AddToVSCode();
                    labelSetupStatus.Text = "VS Code: Registered. Restart VS Code to activate.";
                }
                else
                {
                    ChatAppRegistry.RemoveFromVSCode();
                    labelSetupStatus.Text = "VS Code: Removed. Restart VS Code to apply.";
                }
            }
            catch (Exception ex)
            {
                labelSetupStatus.Text = "VS Code: " + ex.Message;
                RevertCheckbox(checkVSCode);
            }
        }

        private void checkCursor_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressCheckEvents)
                return;
            try
            {
                if (checkCursor.Checked)
                {
                    ChatAppRegistry.AddToCursor();
                    labelSetupStatus.Text = "Cursor: Registered. Restart Cursor to activate.";
                }
                else
                {
                    ChatAppRegistry.RemoveFromCursor();
                    labelSetupStatus.Text = "Cursor: Removed. Restart Cursor to apply.";
                }
            }
            catch (Exception ex)
            {
                labelSetupStatus.Text = "Cursor: " + ex.Message;
                RevertCheckbox(checkCursor);
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

                // Gemini CLI
                bool geminiInstalled = ChatAppRegistry.IsGeminiCliInstalled();
                checkGeminiCli.Enabled = geminiInstalled;
                if (geminiInstalled)
                    checkGeminiCli.Checked = ChatAppRegistry.IsRegisteredInGeminiCli();
                else
                    checkGeminiCli.Text = "Gemini CLI (not installed)";

                // VS Code (Copilot)
                bool vscodeInstalled = ChatAppRegistry.IsVSCodeInstalled();
                checkVSCode.Enabled = vscodeInstalled;
                if (vscodeInstalled)
                    checkVSCode.Checked = ChatAppRegistry.IsRegisteredInVSCode();
                else
                    checkVSCode.Text = "VS Code (Copilot) (not installed)";

                // Cursor
                bool cursorInstalled = ChatAppRegistry.IsCursorInstalled();
                checkCursor.Enabled = cursorInstalled;
                if (cursorInstalled)
                    checkCursor.Checked = ChatAppRegistry.IsRegisteredInCursor();
                else
                    checkCursor.Text = "Cursor (not installed)";
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

        private void checkAutoConnect_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressCheckEvents || _remoteClient == null)
                return;
            try
            {
                string result = (string)_remoteClient.RemoteCallName("StartMcpConnection",
                    new object[] { checkAutoConnect.Checked.ToString() });
                _autoConnectEnabled = ParseAutoConnect(result);
                labelStatus.Text = _autoConnectEnabled
                    ? "Skyline will connect to AI at startup."
                    : "Connected to Skyline";
            }
            catch (Exception ex)
            {
                labelStatus.Text = "Error: " + ex.Message;
                RevertCheckbox(checkAutoConnect);
            }
        }

        private static bool ParseAutoConnect(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty(nameof(JsonToolConstants.JSON.auto_connect)).GetBoolean();
            }
            catch
            {
                return false;
            }
        }

        private void RevertCheckbox(CheckBox checkBox)
        {
            _suppressCheckEvents = true;
            checkBox.Checked = !checkBox.Checked;
            _suppressCheckEvents = false;
        }
    }
}
