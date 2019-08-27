/*
 * Original author: Kaipo Tamura <kaipot .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI.Irt
{
    public partial class AddIrtStandardsToDocumentDlg : FormEx, IAuditLogModifier<AddIrtStandardsToDocumentDlg.AddIrtStandardsToDocumentSettings>
    {
        public int NumTransitions
        {
            get { return Convert.ToInt32(numTransitions.Value); }
            set { numTransitions.Value = value; }
        }

        public AddIrtStandardsToDocumentDlg()
        {
            InitializeComponent();
        }

        public class AddIrtStandardsToDocumentSettings : AuditLogOperationSettings<AddIrtStandardsToDocumentSettings>
        {

            private SrmDocument.DOCUMENT_TYPE _docType;
            public override MessageInfo MessageInfo
            {
                get { return new MessageInfo(MessageType.added_irt_standard_peptides, _docType); }
            }

            public AddIrtStandardsToDocumentSettings(int numTransitions, SrmDocument.DOCUMENT_TYPE docType)
            {
                NumTransitions = numTransitions;
                _docType = docType;
            }

            [Track]
            public int NumTransitions { get; private set; }
        }

        public AddIrtStandardsToDocumentSettings FormSettings
        {
            get { return new AddIrtStandardsToDocumentSettings(NumTransitions, ModeUI); }
        }

        private void btnYes_Click(object sender, EventArgs e)
        {
            BtnYesClick();
        }

        public void BtnYesClick()
        {
            DialogResult = DialogResult.Yes;
        }

        private void btnNo_Click(object sender, EventArgs e)
        {
            BtnNoClick();
        }

        public void BtnNoClick()
        {
            DialogResult = DialogResult.No;
        }
    }
}
