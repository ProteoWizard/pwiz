/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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

using pwiz.Common.GUI;
using pwiz.Skyline.Util;
using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace pwiz.Skyline.Alerts
{
    public class AlertDlg : CommonAlertDlg
    {
        private Helpers.ModeUIAwareFormHelper _modeUIHelper;
        public Helpers.ModeUIExtender ModeUIExtender; // Allows UI mode management in Designer
        private Container _components; // For IExtender use
        public Helpers.ModeUIAwareFormHelper GetModeUIHelper() // Method instead of property so it doesn't show up in Designer
        {
            return _modeUIHelper;
        }

        public AlertDlg()
        {
            InitializeComponent(); // Required for Windows Form Designer support
        }

        public AlertDlg(string message) : base(message)
        {
            InitializeComponent();
        }
        public AlertDlg(string message, MessageBoxButtons messageBoxButtons) : base(message, messageBoxButtons)
        {
            InitializeComponent();
        }

        public AlertDlg(string message, MessageBoxButtons messageBoxButtons, DialogResult defaultButton) : base(message, messageBoxButtons, defaultButton)
        {
            InitializeComponent();
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            _components = new Container();
            ModeUIExtender = new Helpers.ModeUIExtender(_components);
            _modeUIHelper = new Helpers.ModeUIAwareFormHelper(ModeUIExtender);
        }
        #endregion

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            GetModeUIHelper().OnLoad(this);
        }
        public override void CopyMessage()
        {
            ClipboardHelper.SetSystemClipboardText(this, GetTitleAndMessageDetail());
        }
    }
}
