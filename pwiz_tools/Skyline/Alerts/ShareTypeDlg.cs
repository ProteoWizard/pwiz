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
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Alerts
{
    /// <summary>
    /// Use for a <see cref="MessageBox"/> substitute that can be
    /// detected and closed by automated functional tests.
    /// </summary>
    public partial class ShareTypeDlg : FormEx
    {
        private const string BULLET = "\u2022 "; // Not L10N

        public ShareTypeDlg(SrmDocument document)
        {
            InitializeComponent();

            int lineHeight = labelMessage.Height;

            StringBuilder sbLabel = new StringBuilder();
            sbLabel.AppendLine(Resources.ShareTypeDlg_ShareTypeDlg_The_document_can_be_shared_either_in_its_complete_form_or_in_a_minimal_form_intended_for_read_only_use_with);
            var listMinimizations = new List<string>();
            if (document.Settings.HasBackgroundProteome)
                listMinimizations.Add(BULLET + Resources.ShareTypeDlg_ShareTypeDlg_its_background_proteome_disconnected);
            if (document.Settings.HasRTCalcPersisted)
                listMinimizations.Add(BULLET + Resources.ShareTypeDlg_ShareTypeDlg_its_retention_time_calculator_minimized_to_contain_only_standard_peptides_and_library_peptides_used_in_the_document);
            if (document.Settings.HasLibraries)
                listMinimizations.Add(BULLET + Resources.ShareTypeDlg_ShareTypeDlg_all_libraries_minimized_to_contain_only_precursors_used_in_the_document);
            int lastIndex = listMinimizations.Count - 1;
            if (lastIndex < 0)
            {
                throw new InvalidOperationException(
                    string.Format(Resources.ShareTypeDlg_ShareTypeDlg_Invalid_use_of__0__for_document_without_background_proteome_retention_time_calculator_or_libraries,
                        typeof(ShareTypeDlg).Name));
            }
            if (listMinimizations.Count > 0)
                sbLabel.AppendLine().Append(TextUtil.LineSeparate(listMinimizations)).AppendLine();

            sbLabel.AppendLine().Append(Resources.ShareTypeDlg_ShareTypeDlg_Choose_the_appropriate_sharing_option_below);
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
