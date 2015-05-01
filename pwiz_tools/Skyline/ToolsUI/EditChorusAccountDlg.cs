/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Linq;
using System.Net;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Results.RemoteApi;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.ToolsUI
{
    public partial class EditChorusAccountDlg : Form
    {
        private readonly ChorusAccount _originalAccount;
        private readonly IList<ChorusAccount> _existing;
        public EditChorusAccountDlg(ChorusAccount chorusAccount, IEnumerable<ChorusAccount> existing)
        {
            InitializeComponent();
            _existing = ImmutableList.ValueOf(existing);
            _originalAccount = chorusAccount;
            SetChorusAccount(chorusAccount);
        }

        public void SetChorusAccount(ChorusAccount chorusAccount)
        {
            textUsername.Text = chorusAccount.Username;
            textPassword.Text = chorusAccount.Password;
            textServerURL.Text = chorusAccount.ServerUrl;
        }

        public ChorusAccount GetChorusAccount()
        {
            return new ChorusAccount(textServerURL.Text.Trim(), textUsername.Text.Trim(), textPassword.Text);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            if (!ValidateValues())
            {
                return;
            }
            DialogResult = DialogResult.OK;
        }

        private void btnTest_Click(object sender, EventArgs e)
        {
            TestSettings();
        }

        public bool TestSettings()
        {
            if (!ValidateValues())
            {
                return false;
            }
            ChorusAccount chorusAccount = GetChorusAccount();
            ChorusSession chorusSession = new ChorusSession();
            try
            {
                CookieContainer cookieContainer = new CookieContainer();
                try
                {
                    chorusSession.Login(chorusAccount, cookieContainer);
                    MessageDlg.Show(this, Resources.EditChorusAccountDlg_TestSettings_Settings_are_correct);
                    return true;
                }
                catch (ChorusServerException chorusException)
                {
                    MessageDlg.ShowException(this, chorusException);
                    textPassword.Focus();
                    return false;
                }
            }
            catch (Exception x)
            {
                MessageDlg.ShowWithException(this, Resources.EditChorusAccountDlg_TestSettings_Error_connecting_to_server__ + x.Message, x);
                textServerURL.Focus();
                return false;
            }
        }

        private bool ValidateValues()
        {
            var chorusAccount = GetChorusAccount();
            if (string.IsNullOrEmpty(chorusAccount.Username))
            {
                MessageDlg.Show(this, Resources.EditChorusAccountDlg_ValidateValues_Username_cannot_be_blank);
                textUsername.Focus();
                return false;
            }
            if (string.IsNullOrEmpty(chorusAccount.ServerUrl))
            {
                MessageDlg.Show(this, Resources.EditChorusAccountDlg_ValidateValues_Server_cannot_be_blank);
                textServerURL.Focus();
                return false;
            }
            if (chorusAccount.GetKey() != _originalAccount.GetKey())
            {
                if (_existing.Select(existing => existing.GetKey()).Contains(chorusAccount.GetKey()))
                {
                    MessageDlg.Show(this, string.Format(Resources.EditChorusAccountDlg_ValidateValues_There_is_already_an_account_defined_for_the_user__0__on_the_server__1_, chorusAccount.Username, chorusAccount.ServerUrl));
                }
            }
            try
            {
                var uri = new Uri(chorusAccount.ServerUrl, UriKind.Absolute);
                if (uri.Scheme != "https" && uri.Scheme != "http") // Not L10N
                {
                    MessageDlg.Show(this, Resources.EditChorusAccountDlg_ValidateValues_Server_URL_must_start_with_https____or_http___);
                    textServerURL.Focus();
                    return false;
                }
            }
            catch
            {
                MessageDlg.Show(this, Resources.EditChorusAccountDlg_ValidateValues_Invalid_server_URL_);
                textServerURL.Focus();
                return false;
            }
            return true;
        }
    }
}
