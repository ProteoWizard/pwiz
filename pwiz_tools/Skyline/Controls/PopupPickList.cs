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
using System.Windows.Forms.VisualStyles;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls
{
    public partial class PopupPickList : FormEx, ITipDisplayer
    {
        /// <summary>
        /// Current size used for all popup pick-lists.
        /// </summary>
        // CONSIDER: Make the pick lists sizable, and store in the settings?
        public static Size SizeAll { get { return new Size(410, 251); } }

        private readonly IChildPicker _picker;
        private readonly List<DocNode> _chosenAtStart;
        private readonly bool _okOnDeactivate;
        private List<PickListChoice> _choices;
        private bool _closing;
        private bool _autoManageChildren;
        private bool _selectalInternalChange;

        private int _leftText;

        private readonly ModFontHolder _modFonts;

        private readonly NodeTip _nodeTip;
        private readonly MoveThreshold _moveThreshold = new MoveThreshold(5, 5);
        private DocNode _lastTipNode;
        private ITipProvider _lastTipProvider;

        public PopupPickList(IChildPicker picker, string childHeading, bool okOnDeactivate)
        {
            InitializeComponent();

            Size = SizeAll;

            cbItems.Text = childHeading;

            _modFonts = new ModFontHolder(pickListMulti);
            _nodeTip = new NodeTip(this) {Parent = this};

            _picker = picker;
            _chosenAtStart = new List<DocNode>(picker.Chosen);
            _okOnDeactivate = okOnDeactivate;

            bool filter = tbbFilter.Checked = _picker.Filtered;
            var choices = picker.GetChoices(filter).ToArray();            
            if (choices.Length != choices.Select(c => c.Id.GlobalIndex).Distinct().Count())
                throw new ArgumentException(@"Choices must be unique");

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

            // Hide the synchronize checkbox, or set its label correctly
            string synchLabelText = _picker.SynchSiblingsLabel;
            if (string.IsNullOrEmpty(synchLabelText))
            {
                cbSynchronize.Visible = false;

                // Resize to hide space for the checkbox
                var anchorList = pickListMulti.Anchor;
                pickListMulti.Anchor = anchorList & ~AnchorStyles.Bottom;
                Height = pickListMulti.Bottom + 8;
                pickListMulti.Anchor = anchorList;
            }
            else
            {
                cbSynchronize.Text = synchLabelText;
                cbSynchronize.Checked = _picker.IsSynchSiblings;
            }
        }

        public IEnumerable<string> ItemNames
        {
            get
            {
                for (int i = 0; i < pickListMulti.Items.Count; i++)
                    yield return GetVisibleChoice(i).Label;
            }
        }

        public Rectangle GetItemTextRectangle(int i)
        {
            var rect = pickListMulti.GetItemRectangle(i);
            rect.Width += rect.X - _leftText;
            rect.X = _leftText;
            return rect;
        }

        private int GetIndexAtPoint(Point pt)
        {
            for (int i = pickListMulti.TopIndex; 0 <= i && i < pickListMulti.Items.Count; i++)
            {
                var rectItem = pickListMulti.GetItemRectangle(i);
                if (rectItem.Top > ClientRectangle.Bottom)
                    break;

                if (rectItem.Contains(pt))
                    return i;
            }
            return -1;
        }

        public bool IsSynchSiblings
        {
            get { return cbSynchronize.Checked; }
            set { cbSynchronize.Checked = value; }
        }

        public bool CanSynchSiblings
        {
            get { return cbSynchronize.Visible; }
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
                    var filteredChoices = new HashSet<DocNode>(_picker.GetChoices(true));
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
            tbbAutoManageChildren.Image = (_autoManageChildren ? Resources.Wand : Resources.WandProhibit);
            string[] words = cbItems.Text.Split(TextUtil.SEPARATOR_SPACE);
            tbbAutoManageChildren.ToolTipText =
                string.Format(Resources.PopupPickList_UpdateAutoManageUI_Auto_select_filtered__0_,
                              words[words.Length - 1].ToLower());
            if (!_autoManageChildren)
                tbbAutoManageChildren.ToolTipText += String.Format(@" ({0})",
                                                                   Resources.PopupPickList_UpdateAutoManageUI_off);
        }

        public void SetChoices(IEnumerable<DocNode> choices, IList<DocNode> chosen)
        {
            var choicesNew = new List<PickListChoice>();
            foreach (DocNode choice in choices)
            {
                bool check = false;
                if (_choices == null)
                    check = ContainsChoice(chosen, choice);
                else
                {
                    foreach (PickListChoice choiceExisting in _choices)
                    {
                        if (ReferenceEquals(choice, choiceExisting.Choice))
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
            pickListMulti.Items.Clear();
            
            string searchString = textSearch.Text;
            string[] searches = (string.IsNullOrEmpty(searchString) ?
                null : searchString.Split(new[] {' '}));

            for (int i = 0; i < _choices.Count; i++)
            {
                var choice = _choices[i];
                if (!textSearch.Visible || AcceptChoice(choice, searches))
                {
                    pickListMulti.Items.Add(choice);
                }
            }
            pickListMulti.EndUpdate();
            UpdateSelectAll();
        }

        private static bool ContainsChoice(IList<DocNode> choices, DocNode choice)
        {
            return choices.IndexOf(c => ReferenceEquals(c, choice)) != -1;
        }

        private static bool AcceptChoice(PickListChoice choice, IEnumerable<string> searches)
        {
            if (searches != null)
            {
                // Make sure all search strings are in the label
                foreach (string search in searches)
                {
                    if (string.IsNullOrEmpty(search))
                        continue;
                    if (!choice.SearchMatch(search))
                        return false;
                }
            }
            return true;
        }

        public void OnOk()
        {
            var picks = new List<DocNode>();
            foreach (PickListChoice choice in _choices)
            {
                if (choice.Chosen)
                    picks.Add(choice.Choice);
            }
            _picker.IsSynchSiblings = IsSynchSiblings;
            _picker.Pick(picks, AutoManageChildren, IsSynchSiblings);

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
            _nodeTip.HideTip();

            base.OnDeactivate(e);
            if (!_closing && _okOnDeactivate)
                OnOk();
        }

        private void pickListMulti_KeyDown(object sender, KeyEventArgs e)
        {
            // Ignore keys if already closing or in test mode
            if (_closing || !_okOnDeactivate)
                return;

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
                case Keys.Space:
                {
                    int i = pickListMulti.SelectedIndex;
                    if (i >= 0)
                    {
                        ToggleItem(pickListMulti.SelectedIndex);
                    }
                    break;
                }
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
            ApplyFilter(tbbFilter.Checked);
        }

        public void ApplyFilter(bool filter)
        {
            _picker.Filtered = tbbFilter.Checked = filter;
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

        private PickListChoice GetVisibleChoice(int i)
        {
            return (PickListChoice) pickListMulti.Items[i];
        }

        public bool GetItemChecked(int i)
        {
            return GetVisibleChoice(i).Chosen;
        }

        public string GetItemLabel(int i)
        {
            return GetVisibleChoice(i).Label;
        }

        public bool SelectAll
        {
            get { return cbItems.Checked; }
            set { cbItems.Checked = value; }
        }

        private bool IsAllChecked
        {
            get
            {
                for (int i = 0; i < pickListMulti.Items.Count; i++)
                {
                    if (!GetItemChecked(i))
                        return false;
                }
                return true;
            }
        }

        private void UpdateSelectAll()
        {
            _selectalInternalChange = true;
            SelectAll = IsAllChecked;
            _selectalInternalChange = false;
        }

        private void cbItems_CheckedChanged(object sender, EventArgs e)
        {
            if (_selectalInternalChange)
                return;

            bool checkAll = cbItems.Checked;
            for (int i = 0; i < pickListMulti.Items.Count; i++)
                SetItemCheckedInternal(i, checkAll);
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
                textSearch.Text = string.Empty;
            tbbFind_Click(this, null);            
        }

        private void pickListMulti_MouseDown(object sender, MouseEventArgs e)
        {
            int i = GetIndexAtPoint(e.Location);
            if (i != -1)
                ToggleItem(i);
        }

        public void ToggleItem(int iChange)
        {
            SetItemChecked(iChange, !GetItemChecked(iChange));
        }

        public void SetItemChecked(int i, bool checkItem)
        {
            SetItemCheckedInternal(i, checkItem);
            pickListMulti.Invalidate(pickListMulti.GetItemRectangle(i));

            UpdateSelectAll();

            AutoManageChildren = false;
        }

        public void SetItemCheckedInternal(int i, bool checkItem)
        {
            GetVisibleChoice(i).Chosen = checkItem;
        }

        private const int MARGIN_LEFT_CHECKBOX = 1;
        private const int MARGIN_RIGHT_CHECKBOX = 1;
        private const int MARGIN_RIGHT_IMAGE = 1;
        private const TextFormatFlags FORMAT_PLAIN = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;

        private void pickListMulti_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index == -1)
            {
                e.DrawBackground();
                return;
            }

            var g = e.Graphics;
            var bounds = e.Bounds;
            var choice = GetVisibleChoice(e.Index);

            // Draw checkbox
            var checkState = (choice.Chosen ? CheckBoxState.CheckedNormal : CheckBoxState.UncheckedNormal);
            var checkSize = CheckBoxRenderer.GetGlyphSize(g, checkState);
            var checkLocation = new Point(bounds.X + MARGIN_LEFT_CHECKBOX,
                bounds.Y + (bounds.Height - checkSize.Height) / 2);

            CheckBoxRenderer.DrawCheckBox(g, checkLocation, checkState);

            checkSize.Width += MARGIN_LEFT_CHECKBOX + MARGIN_RIGHT_CHECKBOX;

            bounds.X += checkSize.Width;
            bounds.Width -= checkSize.Width;

            // Draw images
            var imgPeak = _picker.GetPickPeakImage(choice.Choice);
            if (imgPeak != null)
            {
                g.DrawImageUnscaled(imgPeak, bounds.Left, bounds.Top, imgPeak.Width, bounds.Height);
                bounds.X += imgPeak.Width + MARGIN_RIGHT_IMAGE;
                bounds.Width -= imgPeak.Width + MARGIN_RIGHT_IMAGE;                
            }
            
            var imgType = _picker.GetPickTypeImage(choice.Choice);
            g.DrawImageUnscaled(imgType, bounds.Left, bounds.Top, imgType.Width, bounds.Height);
            bounds.X += imgType.Width + MARGIN_RIGHT_IMAGE;
            bounds.Width -= imgType.Width + MARGIN_RIGHT_IMAGE;

            // Draw background and text clipped to remaining space
            var clipRect = g.ClipBounds;
            g.SetClip(bounds);
            e.DrawBackground();

            if (!_picker.DrawPickLabel(choice.Choice, g, bounds, _modFonts, e.ForeColor, e.BackColor))
            {
                TextRenderer.DrawText(e.Graphics, choice.Label,
                                      _modFonts.Plain, bounds, e.ForeColor, e.BackColor,
                                      FORMAT_PLAIN);
            }

            // Store the left edge of the text for use with tool tips
            _leftText = bounds.X;

            // Reset the clipping rectangle
            g.SetClip(clipRect);
        }

        private void pickListMulti_MouseMove(object sender, MouseEventArgs e)
        {
            Point pt = e.Location;
            if (!_moveThreshold.Moved(pt))
                return;
            _moveThreshold.Location = null;

            ITipProvider tipProvider = null;
            int i = GetIndexAtPoint(pt);
            // Make sure it is in the text portion of the item
            if (i != -1 && !GetItemTextRectangle(i).Contains(pt))
                i = -1;

            if (i == -1)
            {
                _lastTipNode = null;
                _lastTipProvider = null;
            }
            else
            {
                var nodeDoc = GetVisibleChoice(i).Choice;
                if (!ReferenceEquals(nodeDoc, _lastTipNode))
                {
                    _lastTipNode = nodeDoc;
                    _lastTipProvider = _picker.GetPickTip(nodeDoc);
                }
                tipProvider = _lastTipProvider;
            }

            if (tipProvider == null || !tipProvider.HasTip)
                _nodeTip.HideTip();
            else
                _nodeTip.SetTipProvider(tipProvider, GetItemTextRectangle(i), pt);
        }

        private void pickListMulti_MouseLeave(object sender, EventArgs e)
        {
            _nodeTip.HideTip();
        }

        private sealed class PickListChoice
        {
            public PickListChoice(DocNode choice, string label, bool chosen)
            {
                Choice = choice;
                Label = label;
                Chosen = chosen;
            }

            public DocNode Choice { get; private set; }
            public string Label { get; private set; }
            public bool Chosen { get; set; }

            public bool SearchMatch(string searchString)
            {
                if (string.IsNullOrEmpty(searchString) || Label.Contains(searchString))
                    return true;

                if (Choice.Id is Transition transition)
                {
                    foreach (var inputString in transition.IonType.GetInputAliases())
                    {
                        if (Label.Contains(searchString.Replace(inputString, transition.IonType.GetLocalizedString())))
                            return true;
                    }
                }

                return false;
            }
        }

        public Rectangle ScreenRect
        {
            get { return Screen.GetBounds(pickListMulti); }
        }

        public bool AllowDisplayTip
        {
            get { return pickListMulti.Focused; }
        }

        public Rectangle RectToScreen(Rectangle r)
        {
            return pickListMulti.RectangleToScreen(r);
        }
    }
}
