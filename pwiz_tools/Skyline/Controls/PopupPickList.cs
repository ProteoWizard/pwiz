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
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls
{
    public partial class PopupPickList : Form
    {
        /// <summary>
        /// Current size used for all popup pick-lists.
        /// </summary>
        // CONSIDER: Make the pick lists sizable, and store in the settings?
        public static Size SizeAll { get { return new Size(375, 243); } }

        private readonly IChildPicker _picker;
        private readonly List<object> _chosenAtStart;
        private List<PickListChoice> _choices;
        private List<int> _indexListChoices;
        private bool _closing;
        private bool _autoManageChildren;

        public PopupPickList(IChildPicker picker, string childHeading)
        {
            InitializeComponent();

            Size = SizeAll;

            cbItems.Text = childHeading;

            _picker = picker;
            _chosenAtStart = new List<object>(picker.Chosen);

            bool filter = tbbFilter.Checked = _picker.Filtered;
            var choices = picker.GetChoices(filter).ToArray();
            if (filter)
            {
                // If filtered choices do not contain a choice that
                // has already been chose, then use the unfiltered list.
                foreach (var choice in _chosenAtStart)
                {
                    if (!ContainsChoice(choices, choice))
                    {
                        choices = picker.GetChoices(false).ToArray();
                        tbbFilter.Checked = false;
                        break;
                    }
                }
            }
            SetChoices(choices, _chosenAtStart);

            if (pickListMulti.Items.Count > 0)
                pickListMulti.SelectedIndex = 0;
            // Avoid setting the property, because it will actually
            // change what is picked.
            _autoManageChildren = _picker.AutoManageChildren;
            UpdateAutoManageUI();
        }

        public IEnumerable<string> ItemNames
        {
            get
            {
                foreach (var item in pickListMulti.Items)
                    yield return item.ToString();
            }
        }

        public bool AutoManageChildren
        {
            get
            {
                return _autoManageChildren;
            }
            set
            {
                if (_autoManageChildren == value)
                {
                    return;
                }
                _autoManageChildren = value;
                if (_autoManageChildren)
                {
                    FindComplete(false);
                    if (!tbbFilter.Checked)
                    {
                        tbbFilter.Checked = true;
                        tbbFilter_Click(this, null);
                    }
                    var filteredChoices = new HashSet<object>(_picker.GetChoices(true));
                    foreach (PickListChoice choice in _choices)
                    {
                        choice.Chosen = filteredChoices.Contains(choice.Choice);
                    }
                    // Keep ShowChoices() from changing the UI
                    _autoManageChildren = false;
                    ShowChoices();
                    _autoManageChildren = true;
                }
                UpdateAutoManageUI();
            }
        }

        private void UpdateAutoManageUI()
        {
            tbbAutoManageChildren.Checked = _autoManageChildren;
            tbbAutoManageChildren.Image = (_autoManageChildren ?
                Properties.Resources.Wand : Properties.Resources.WandProhibit);
            string[] words = cbItems.Text.Split(' ');
            tbbAutoManageChildren.ToolTipText = string.Format("Auto-select filtered {0}",
                words[words.Length - 1].ToLower());
            if (!_autoManageChildren)
                tbbAutoManageChildren.ToolTipText += " (off)";
        }

        public void SetChoices(IEnumerable<object> choices, IList<object> chosen)
        {
            var choicesNew = new List<PickListChoice>();
            foreach (object choice in choices)
            {
                bool check = false;
                if (_choices == null)
                    check = ContainsChoice(chosen, choice);
                else
                {
                    foreach (PickListChoice choiceExisting in _choices)
                    {
                        if (_picker.Equivalent(choice, choiceExisting.Choice))
                        {
                            check = choiceExisting.Chosen;
                            break;
                        }
                    }
                }
                choicesNew.Add(new PickListChoice(choice,
                    _picker.GetPickLabel(choice), check));
            }
            _choices = choicesNew;
            ShowChoices();
        }

        private void ShowChoices()
        {
            pickListMulti.BeginUpdate();
            _indexListChoices = new List<int>();
            pickListMulti.Items.Clear();
            
            string searchString = textSearch.Text;
            string[] searches = (string.IsNullOrEmpty(searchString) ?
                null : searchString.Split(new[] {' '}));

            for (int i = 0; i < _choices.Count; i++)
            {
                var choice = _choices[i];
                if (!textSearch.Visible || AcceptChoice(choice, searches))
                {
                    _indexListChoices.Add(i);
                    pickListMulti.Items.Add(choice.Label, choice.Chosen);
                }
            }
            pickListMulti.EndUpdate();
        }

        private bool ContainsChoice(IList<object> choices, object choice)
        {
            return choices.IndexOf(c => _picker.Equivalent(c, choice)) != -1;
        }

        private static bool AcceptChoice(PickListChoice choice, IEnumerable<string> searches)
        {
            if (searches != null)
            {
                // Make sure all search strings are in the label
                string label = choice.Label;
                foreach (string search in searches)
                {
                    if (string.IsNullOrEmpty(search))
                        continue;
                    if (!label.Contains(search))
                        return false;
                }
            }
            return true;
        }

        public void OnOk()
        {
            var picks = new List<object>();
            foreach (PickListChoice choice in _choices)
            {
                if (choice.Chosen)
                    picks.Add(choice.Choice);
            }
            _picker.Pick(picks, AutoManageChildren);

            _closing = true;
            Dispose();
        }

        public void OnCancel()
        {
            _closing = true;
            Dispose();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (!_closing)
                OnOk();
        }

        private void pickListMulti_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.F:
                    if (e.Control)
                        tbbFind_Click(this, null);
                    break;
                case Keys.Escape:
                    OnCancel();
                    break;
                case Keys.Enter:
                    OnOk();
                    break;
            }
        }

        private void tbbOk_Click(object sender, EventArgs e)
        {
            OnOk();
        }

        private void tbbCancel_Click(object sender, EventArgs e)
        {
            OnCancel();
        }

        private void tbbFilter_Click(object sender, EventArgs e)
        {
            bool filter = _picker.Filtered = tbbFilter.Checked;
            ApplyFilter(filter);
        }

        public void ApplyFilter(bool filter)
        {
            SetChoices(_picker.GetChoices(filter), _chosenAtStart);
        }

        private void tbbAutoManageChildren_Click(object sender, EventArgs e)
        {
            ToggleAutoManageChildren();
        }

        public void ToggleAutoManageChildren()
        {
            AutoManageChildren = tbbAutoManageChildren.Checked;
        }

        public string SearchString
        {
            get { return textSearch.Text; }
            set { textSearch.Text = value; }
        }

        private void tbbFind_Click(object sender, EventArgs e)
        {
            ToggleFind();
        }

        public void ToggleFind()
        {
            if (textSearch.Focused)
            {
                textSearch.Visible = false;
                ShowChoices();
                pickListMulti.Focus();
            }
            else
            {
                textSearch.Visible = true;
                textSearch.Focus();
                ShowChoices();
            }
        }

        public bool GetItemChecked(int i)
        {
            return pickListMulti.GetItemChecked(i);
        }

        public void SetItemChecked(int i, bool checkItem)
        {
            pickListMulti.SetItemChecked(i, checkItem);
        }

        private void cbItems_CheckedChanged(object sender, EventArgs e)
        {
            bool checkAll = cbItems.Checked;
            for (int i = 0; i < pickListMulti.Items.Count; i++)
                pickListMulti.SetItemChecked(i, checkAll);
            AutoManageChildren = false;
            pickListMulti.Focus();
        }

        private void textSearch_TextChanged(object sender, EventArgs e)
        {
            ShowChoices();
        }

        private void textSearch_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    FindComplete(true);
                    break;
                case Keys.Escape:
                    FindComplete(false);
                    break;
                case Keys.Down:
                    pickListMulti.Focus();
                    pickListMulti.SelectedIndex = 0;
                    break;
            }
        }

        private void FindComplete(bool find)
        {
            if (!textSearch.Visible)
                return;
            if (!find)
                textSearch.Text = "";
            tbbFind_Click(this, null);            
        }

        private void pickListMulti_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            // If all other checkboxes in the list match the new state,
            // update the header checkbox to match also.
            int iChange = e.Index;
            CheckState state = e.NewValue;
            
            _choices[_indexListChoices[iChange]].Chosen = (state == CheckState.Checked);

            for (int i = 0; i < pickListMulti.Items.Count; i++)
            {
                if (i == iChange)
                    continue;
                if (pickListMulti.GetItemCheckState(i) != state)
                    return;
            }
            cbItems.CheckState = state;
        }

        private void pickListMulti_Click(object sender, EventArgs e)
        {
            AutoManageChildren = false;
        }

        private sealed class PickListChoice
        {
            public PickListChoice(object choice, string label, bool chosen)
            {
                Choice = choice;
                Label = label;
                Chosen = chosen;
            }

            public object Choice { get; private set; }
            public string Label { get; private set; }
            public bool Chosen { get; set; }
        }
    }
}
