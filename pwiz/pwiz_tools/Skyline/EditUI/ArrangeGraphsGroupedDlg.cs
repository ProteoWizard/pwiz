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
using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    public enum GroupGraphsType { separated, distributed }

// ReSharper disable InconsistentNaming
    public enum GroupGraphsOrder { Position, Document }
// ReSharper restore InconsistentNaming

    public partial class ArrangeGraphsGroupedDlg : FormEx
    {
        private readonly int _countGraphs;

        public ArrangeGraphsGroupedDlg(int countGraphs)
        {
            InitializeComponent();

            _countGraphs = countGraphs;

            textGroups.Text = Groups.ToString(CultureInfo.CurrentCulture);
            if (GroupType == GroupGraphsType.distributed)
                radioDistribute.Checked = true;
            comboSortOrder.SelectedItem = GroupOrder.ToString();
            cbReversed.Checked = Settings.Default.ArrangeGraphsReversed;
        }

        public int Groups
        {
            get { return Settings.Default.ArrangeGraphsGroups; }
            set
            {
                textGroups.Text = value.ToString(CultureInfo.CurrentCulture);
                Settings.Default.ArrangeGraphsGroups = value;
            }
        }

        public GroupGraphsType GroupType
        {
            get
            {
                return Helpers.ParseEnum(Settings.Default.ArrangeGraphsType, GroupGraphsType.separated);
            }
            set
            {
                if (value == GroupGraphsType.separated)
                    radioSeparate.Checked = true;
                else
                    radioDistribute.Checked = true;
                Settings.Default.ArrangeGraphsType = value.ToString();
            }
        }

        public GroupGraphsOrder GroupOrder
        {
            get
            {
                return Helpers.ParseEnum(Settings.Default.ArrangeGraphsOrder, GroupGraphsOrder.Position);
            }
            set
            {
                comboSortOrder.SelectedItem = 
                    Settings.Default.ArrangeGraphsOrder = value.ToString();
            }
        }

        public bool Reversed { get; private set; }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);
            int countGroups;
            if (!helper.ValidateNumberTextBox(new CancelEventArgs(), textGroups, 1, _countGraphs, out countGroups))
                return;
            Groups = countGroups;

            if (radioSeparate.Checked)
                GroupType = GroupGraphsType.separated;
            else if (radioDistribute.Checked)
                GroupType = GroupGraphsType.distributed;

            GroupOrder = Helpers.ParseEnum(comboSortOrder.SelectedItem.ToString(), GroupGraphsOrder.Position);
            Reversed = cbReversed.Checked;
            // Only save the reverse flag, if the order is document.  Otherwise,
            // subsequent changes will flip the order back and forth.
            if (GroupOrder == GroupGraphsOrder.Document)
                Settings.Default.ArrangeGraphsReversed = Reversed;

            DialogResult = DialogResult.OK;
        }
    }
}
