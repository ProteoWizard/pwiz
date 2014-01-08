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
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    public partial class FindNodeDlg : FormEx
    {
        private bool _advancedVisible;
        private readonly int _fullHeight;
        private readonly IFinder[] _finders;
        public FindNodeDlg()
        {
            InitializeComponent();
            _fullHeight = Height;
            AdvancedVisible = false;
            _finders = Finders.ListAllFinders().ToArray();
            checkedListBoxFinders.Items.AddRange(
                _finders.Select(finder=>finder.DisplayName).Cast<object>().ToArray());
        }

        public FindOptions FindOptions
        {
            get
            {
                var findOptions = new FindOptions()
                    .ChangeText(SearchString)
                    .ChangeCaseSensitive(CaseSensitive)
                    .ChangeForward(!SearchUp)
                    .ChangeCustomFinders(checkedListBoxFinders.CheckedIndices.Cast<int>()
                        .Select(index => _finders[index]));
                return findOptions;
            }
            set
            {
                SearchString = value.Text;
                CaseSensitive = value.CaseSensitive;
                SearchUp = !value.Forward;
                for (int i = 0; i < checkedListBoxFinders.Items.Count; i++ )
                {
                    checkedListBoxFinders.SetItemChecked(i, value.CustomFinders.Contains(_finders[i]));
                }
                if (FindOptions.CustomFinders.Count > 0)
                {
                    AdvancedVisible = true;
                }
                EnableDisableButtons();
            }
        }

        public string SearchString
        {
            get { return textSequence.Text; }
            set { textSequence.Text = value; }
        }

        public bool SearchUp
        {
            get { return radioUp.Checked; }
            set { radioUp.Checked = value; }
        }

        public bool CaseSensitive
        {
            get { return cbCaseSensitive.Checked; }
            set { cbCaseSensitive.Checked = value; }
        }

        private void textSequence_TextChanged(object sender, EventArgs e)
        {
            EnableDisableButtons();
        }

        private void EnableDisableButtons()
        {
            btnFindAll.Enabled = btnFindNext.Enabled = !FindOptions.IsEmpty;
        }

        private void btnFindNext_Click(object sender, EventArgs e)
        {
            FindNext();
        }

        private void WriteSettings()
        {
            FindOptions.WriteToSettings(Settings.Default, false);
        }

        public void FindNext()
        {
            WriteSettings();
            ((SkylineWindow)Owner).FindNext(SearchUp);
            // Put the focus back on the Find Dialog since Skyline might have popped up a window to display the find result.
            // Don't steal the focus if it's on the SequenceTree, since the SequenceTree might be displaying a tooltip.
            if (!((SkylineWindow) Owner).SequenceTree.Focused)
            {
                Focus();
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnFindAll_Click(object sender, EventArgs e)
        {
            FindAll();
        }

        public void FindAll()
        {
            WriteSettings();
            ((SkylineWindow) Owner).FindAll(this);
        }

        public bool AdvancedVisible
        {
            get
            {
                return _advancedVisible;
            }
            set
            {
                _advancedVisible = value;
                if (_advancedVisible)
                {
                    Height = _fullHeight;
                    btnShowHideAdvanced.Text = Resources.FindNodeDlg_AdvancedVisible_Hide_Advanced;
                }
                else
                {
                    Height = _fullHeight - (checkedListBoxFinders.Bottom - btnShowHideAdvanced.Bottom);
                    btnShowHideAdvanced.Text = Resources.FindNodeDlg_AdvancedVisible_Show_Advanced;
                }
            }
        }

        private void btnShowHideAdvanced_Click(object sender, EventArgs e)
        {
            AdvancedVisible = !AdvancedVisible;
        }

        private void checkedListBoxOptions_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (IsHandleCreated && !IsDisposed)
            {
                // Checked state doesn't update until after event has returned.
                // Therefore update enabled state of buttons via BeginInvoke.
                BeginInvoke(new Action(EnableDisableButtons));
            }
        }
    }
}
