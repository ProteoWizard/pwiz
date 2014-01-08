/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
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
using System.Drawing;
using System.Windows.Forms;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    public partial class AlertLinkDlg : FormEx
    {
        public static void Show(IWin32Window parent, string message, string linkMessage, string linkUrl, bool liveLink = true)
        {
            using (var dlg = new AlertLinkDlg(message, linkMessage, linkUrl, liveLink))
            {
                dlg.ShowDialog(parent);
            }
        }

        public AlertLinkDlg(string message, string linkMessage, string linkUrl, bool liveLink = true)
        {
            InitializeComponent();
            pictureBox1.Image = SystemIcons.Exclamation.ToBitmap();
            _liveLink = liveLink;

            // set message and link
            int height = labelMessage.Height + labelLink.Height;
            labelMessage.Text = Message = message;
            labelLink.Text = linkMessage;
            height = labelMessage.Height + labelLink.Height - height;   // Adjust for growth in size of labels.
            LinkUrl = linkUrl;

            // adjust layout of dialog depending on size of message
            labelLink.SetBounds(labelLink.Left, labelMessage.Bottom + 10, labelLink.Width, labelLink.Height);
            Height += Math.Max(0, height) + 10;
        }

        protected override void CreateHandle()
        {
            base.CreateHandle();

            Text = Program.Name;
        }

        public string Message { get; private set; }
        public string LinkUrl { get; private set; }
        private readonly bool _liveLink;

        private void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void labelLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_liveLink)
            {
                WebHelpers.OpenLink(this, LinkUrl);
                OkDialog();
            }
        }

        private void btnCopyLink_Click(object sender, EventArgs e)
        {
            ClipboardEx.SetText(LinkUrl);
        }
    }
}
