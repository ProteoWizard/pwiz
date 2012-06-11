/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Text;
using System.Collections.Generic;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    /// <summary>
    /// Use for a <see cref="MessageBox"/> substitute that can be
    /// detected and closed by automated functional tests.
    /// </summary>
    public partial class ShareTypeDlg : FormEx
    {
        public ShareTypeDlg(SrmDocument document)
        {
            InitializeComponent();

            int lineHeight = labelMessage.Height;

            var sbLabel = new StringBuilder("The document can be shared either in its complete form, or in a minimal form intended for read-only use with ");
            var listMinimizations = new List<string>();
            if (document.Settings.HasBackgroundProteome)
                listMinimizations.Add("its background proteome disconnected");
            if (document.Settings.HasRTCalcPersisted)
                listMinimizations.Add("its retention time calculator minimized to contain only standard peptides and library peptides used in the document");
            if (document.Settings.HasLibraries)
                listMinimizations.Add("all libraries minimized to contain only precursors used in the document");
            int lastIndex = listMinimizations.Count - 1;
            if (lastIndex < 0)
                throw new InvalidOperationException(string.Format("Invalide use of {0} for document without background proteome, retention time calculator or libraries.", typeof(ShareTypeDlg).Name));
            string lastMin = listMinimizations[lastIndex];
            listMinimizations.RemoveAt(lastIndex);
            if (listMinimizations.Count > 0)
            {
                sbLabel.Append(string.Join(", ", listMinimizations.ToArray()));
                sbLabel.Append(", and ");
            }
            sbLabel.Append(lastMin).Append(".");
            sbLabel.Append("\nChoose the appropriate sharing option below.");
            labelMessage.Text = sbLabel.ToString();
            Height += Math.Max(0, labelMessage.Height - lineHeight * 3);
        }

        public bool IsCompleteSharing { get; set; }

        protected override void CreateHandle()
        {
            base.CreateHandle();

            Text = Program.Name;
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnComplete_Click(object sender, EventArgs e)
        {
            IsCompleteSharing = true;
            OkDialog();
        }
    }
}
