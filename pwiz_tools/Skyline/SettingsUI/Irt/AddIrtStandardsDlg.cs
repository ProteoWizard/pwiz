﻿/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI.Irt
{
    public partial class AddIrtStandardsDlg : FormEx
    {
        private readonly int _peptideCount;

        public AddIrtStandardsDlg(int peptideCount, bool peptidesExcluded)
        {
            _peptideCount = peptideCount;

            InitializeComponent();

            labelMessage.Text = string.Format(!peptidesExcluded ? labelMessage.Text : Resources.AddIrtStandardsDlg_AddIrtStandardsDlg_MessagePeptidesExcluded, peptideCount);
        }

        public AddIrtStandardsDlg(int peptideCount, string message)
        {
            _peptideCount = peptideCount;

            InitializeComponent();

            labelMessage.Text = message;
        }

        public int StandardCount
        {
            get => int.TryParse(textPeptideCount.Text, out var count) ? count : 0;
            set => textPeptideCount.Text = value.ToString(LocalizationHelper.CurrentCulture);
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            if (!helper.ValidateNumberTextBox(textPeptideCount, CalibrateIrtDlg.MIN_STANDARD_PEPTIDES, _peptideCount, out _))
            {
                DialogResult = DialogResult.None;
                return;
            }

            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }
    }
}