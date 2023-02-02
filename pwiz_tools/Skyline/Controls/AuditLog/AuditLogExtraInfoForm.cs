/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.AuditLog
{
    public partial class AuditLogExtraInfoForm : FormEx
    {
        public AuditLogExtraInfoForm(string text, string extraInfo)
        {
            InitializeComponent();

            // Resize form to fit text and extra info (horizontally)
            var textWidth = TextRenderer.MeasureText(text, messageTextBox.Font).Width;
            var widthChange = textWidth - messageTextBox.Width;

            var extraInfoWidth = TextRenderer.MeasureText(extraInfo, extraInfoTextBox.Font).Width;
            widthChange = Math.Min(720, Math.Max(extraInfoWidth - extraInfoTextBox.Width, widthChange));

            if (widthChange > 0)
                Width += widthChange;

            messageTextBox.Text = text;
            extraInfoTextBox.Text = extraInfo;
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void copyButton_Click(object sender, EventArgs e)
        {
            ClipboardHelper.SetClipboardText(this, extraInfoTextBox.Text);
        }

        // Test Support
        public string Message
        {
            get { return messageTextBox.Text; }
        }

        public string ExtraInfo
        {
            get { return extraInfoTextBox.Text; }
        }
    }
}
