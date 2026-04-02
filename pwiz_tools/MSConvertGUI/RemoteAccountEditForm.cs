/*
 * Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
 *
 * Copyright 2024 Matt Chambers
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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.Unifi;
using pwiz.CommonMsData.RemoteApi.WatersConnect;

namespace MSConvertGUI
{
    public class RemoteAccountEditForm : Form
    {
        private ListBox _accountListBox;
        private Button _addButton;
        private Button _editButton;
        private Button _removeButton;
        private Button _okButton;
        private Button _cancelButton;

        private readonly List<RemoteAccount> _accounts;

        public IList<RemoteAccount> Accounts => _accounts;

        public RemoteAccountEditForm(IEnumerable<RemoteAccount> existingAccounts)
        {
            _accounts = new List<RemoteAccount>(existingAccounts);
            InitializeComponents();
            PopulateList();
        }

        private void InitializeComponents()
        {
            Text = "Remote Accounts";
            Size = new Size(450, 350);
            MinimumSize = new Size(400, 300);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            _accountListBox = new ListBox
            {
                Location = new Point(12, 12),
                Size = new Size(310, 250),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            _accountListBox.SelectedIndexChanged += (s, e) => UpdateButtonState();

            _addButton = new Button { Text = "Add...", Location = new Point(330, 12), Size = new Size(95, 28) };
            _addButton.Click += AddButton_Click;

            _editButton = new Button { Text = "Edit...", Location = new Point(330, 46), Size = new Size(95, 28), Enabled = false };
            _editButton.Click += EditButton_Click;

            _removeButton = new Button { Text = "Remove", Location = new Point(330, 80), Size = new Size(95, 28), Enabled = false };
            _removeButton.Click += RemoveButton_Click;

            _okButton = new Button { Text = "OK", Location = new Point(242, 275), Size = new Size(80, 28), DialogResult = DialogResult.OK };
            _cancelButton = new Button { Text = "Cancel", Location = new Point(330, 275), Size = new Size(95, 28), DialogResult = DialogResult.Cancel };

            AcceptButton = _okButton;
            CancelButton = _cancelButton;

            Controls.AddRange(new Control[] { _accountListBox, _addButton, _editButton, _removeButton, _okButton, _cancelButton });
        }

        private void PopulateList()
        {
            _accountListBox.Items.Clear();
            foreach (var account in _accounts)
                _accountListBox.Items.Add(FormatAccountDisplay(account));
            UpdateButtonState();
        }

        private static string FormatAccountDisplay(RemoteAccount account)
        {
            string type = account is UnifiAccount ? "UNIFI" : account is WatersConnectAccount ? "Waters Connect" : "Unknown";
            return $"[{type}] {account.AccountAlias} ({account.Username}@{account.ServerUrl})";
        }

        private void UpdateButtonState()
        {
            bool hasSelection = _accountListBox.SelectedIndex >= 0;
            _editButton.Enabled = hasSelection;
            _removeButton.Enabled = hasSelection;
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            using (var dlg = new RemoteAccountDetailForm(null))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Account != null)
                {
                    _accounts.Add(dlg.Account);
                    PopulateList();
                    _accountListBox.SelectedIndex = _accounts.Count - 1;
                }
            }
        }

        private void EditButton_Click(object sender, EventArgs e)
        {
            int idx = _accountListBox.SelectedIndex;
            if (idx < 0)
                return;

            using (var dlg = new RemoteAccountDetailForm(_accounts[idx]))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Account != null)
                {
                    _accounts[idx] = dlg.Account;
                    PopulateList();
                    _accountListBox.SelectedIndex = idx;
                }
            }
        }

        private void RemoveButton_Click(object sender, EventArgs e)
        {
            int idx = _accountListBox.SelectedIndex;
            if (idx < 0)
                return;

            if (MessageBox.Show(this, "Remove this account?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _accounts.RemoveAt(idx);
                PopulateList();
            }
        }
    }

    /// <summary>
    /// Detail form for adding/editing a single remote account.
    /// </summary>
    public class RemoteAccountDetailForm : Form
    {
        private ComboBox _typeCombo;
        private TextBox _serverUrlBox;
        private TextBox _usernameBox;
        private TextBox _passwordBox;
        private TextBox _aliasBox;
        private Button _okButton;
        private Button _cancelButton;

        public RemoteAccount Account { get; private set; }

        public RemoteAccountDetailForm(RemoteAccount existing)
        {
            InitializeComponents();
            if (existing != null)
            {
                Text = "Edit Account";
                if (existing is UnifiAccount)
                    _typeCombo.SelectedIndex = 0;
                else if (existing is WatersConnectAccount)
                    _typeCombo.SelectedIndex = 1;
                _typeCombo.Enabled = false; // can't change type when editing
                _serverUrlBox.Text = existing.ServerUrl ?? string.Empty;
                _usernameBox.Text = existing.Username ?? string.Empty;
                _passwordBox.Text = existing.Password ?? string.Empty;
                _aliasBox.Text = existing.AccountAlias ?? string.Empty;
            }
            else if (IsDevEnvironment())
            {
                PrepopulateDevDefaults();
            }
        }

        /// <summary>
        /// Returns true if running from a development build directory (parent is "bin" or "msvc-release-*" or "msvc-debug-*").
        /// </summary>
        private static bool IsDevEnvironment()
        {
            string exeDir = Path.GetFileName(Path.GetDirectoryName(Application.ExecutablePath) ?? string.Empty);
            return exeDir.StartsWith("msvc-", StringComparison.OrdinalIgnoreCase) ||
                   exeDir.Equals("bin", StringComparison.OrdinalIgnoreCase);
        }

        private void PrepopulateDevDefaults()
        {
            _typeCombo.SelectedIndexChanged += TypeCombo_DevDefaultChanged;
            TypeCombo_DevDefaultChanged(null, EventArgs.Empty);
        }

        private void TypeCombo_DevDefaultChanged(object sender, EventArgs e)
        {
            if (_typeCombo.SelectedIndex == 0) // UNIFI
            {
                var defaults = UnifiAccount.DEFAULT;
                _serverUrlBox.Text = defaults.ServerUrl;
                _usernameBox.Text = Environment.GetEnvironmentVariable("UNIFI_USERNAME") ?? "msconvert";
                _passwordBox.Text = Environment.GetEnvironmentVariable("UNIFI_PASSWORD") ?? string.Empty;
                _aliasBox.Text = "UNIFI Demo";
            }
            else if (_typeCombo.SelectedIndex == 1) // Waters Connect
            {
                var defaults = WatersConnectAccount.DEV_DEFAULT;
                _serverUrlBox.Text = defaults.ServerUrl;
                _usernameBox.Text = Environment.GetEnvironmentVariable("WC_USERNAME") ?? "skyline";
                _passwordBox.Text = Environment.GetEnvironmentVariable("WC_PASSWORD") ?? string.Empty;
                _aliasBox.Text = "Waters Connect Dev";
            }
        }

        private void InitializeComponents()
        {
            Text = "Add Account";
            Size = new Size(400, 260);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;

            int labelX = 12, fieldX = 110, y = 15, rowHeight = 30;

            Controls.Add(new Label { Text = "Type:", Location = new Point(labelX, y + 3), AutoSize = true });
            _typeCombo = new ComboBox
            {
                Location = new Point(fieldX, y),
                Size = new Size(260, 22),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Items = { "UNIFI", "Waters Connect" }
            };
            _typeCombo.SelectedIndex = 0;
            Controls.Add(_typeCombo);
            y += rowHeight;

            Controls.Add(new Label { Text = "Server URL:", Location = new Point(labelX, y + 3), AutoSize = true });
            _serverUrlBox = new TextBox { Location = new Point(fieldX, y), Size = new Size(260, 22) };
            Controls.Add(_serverUrlBox);
            y += rowHeight;

            Controls.Add(new Label { Text = "Username:", Location = new Point(labelX, y + 3), AutoSize = true });
            _usernameBox = new TextBox { Location = new Point(fieldX, y), Size = new Size(260, 22) };
            Controls.Add(_usernameBox);
            y += rowHeight;

            Controls.Add(new Label { Text = "Password:", Location = new Point(labelX, y + 3), AutoSize = true });
            _passwordBox = new TextBox { Location = new Point(fieldX, y), Size = new Size(260, 22), UseSystemPasswordChar = true };
            Controls.Add(_passwordBox);
            y += rowHeight;

            Controls.Add(new Label { Text = "Alias:", Location = new Point(labelX, y + 3), AutoSize = true });
            _aliasBox = new TextBox { Location = new Point(fieldX, y), Size = new Size(260, 22) };
            Controls.Add(_aliasBox);
            y += rowHeight + 10;

            _okButton = new Button { Text = "OK", Location = new Point(210, y), Size = new Size(75, 28) };
            _okButton.Click += OkButton_Click;
            _cancelButton = new Button { Text = "Cancel", Location = new Point(295, y), Size = new Size(75, 28), DialogResult = DialogResult.Cancel };

            AcceptButton = _okButton;
            CancelButton = _cancelButton;
            Controls.AddRange(new Control[] { _okButton, _cancelButton });
        }

        /// <summary>
        /// Creates a WatersConnectAccount from user-provided values. The constructor derives
        /// IdentityServer from serverUrl; client settings come from DEV_DEFAULT or DEFAULT
        /// depending on the environment.
        /// </summary>
        public static WatersConnectAccount CreateWatersConnectAccount(
            string serverUrl, string username, string password, bool isDevEnvironment)
        {
            var defaults = isDevEnvironment ? WatersConnectAccount.DEV_DEFAULT : WatersConnectAccount.DEFAULT;
            return new WatersConnectAccount(serverUrl, username, password)
                .ChangeClientScope(defaults.ClientScope)
                .ChangeClientSecret(defaults.ClientSecret)
                .ChangeClientId(defaults.ClientId);
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_serverUrlBox.Text))
            {
                MessageBox.Show(this, "Server URL is required.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string serverUrl = _serverUrlBox.Text.Trim();
            string username = _usernameBox.Text.Trim();
            string password = _passwordBox.Text;
            string alias = _aliasBox.Text.Trim();

            RemoteAccount account;
            if (_typeCombo.SelectedIndex == 0) // UNIFI
                account = new UnifiAccount(serverUrl, username, password);
            else // Waters Connect
                account = CreateWatersConnectAccount(serverUrl, username, password, IsDevEnvironment());

            if (!string.IsNullOrEmpty(alias))
                account.AccountAlias = alias;

            Account = account;
            DialogResult = DialogResult.OK;
        }
    }
}
