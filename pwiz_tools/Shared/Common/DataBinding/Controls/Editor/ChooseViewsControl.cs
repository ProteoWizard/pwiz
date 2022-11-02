using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Properties;

namespace pwiz.Common.DataBinding.Controls.Editor
{
    [DefaultEvent("SelectionChanged")]
    public partial class ChooseViewsControl : UserControl
    {
        private IViewContext _viewContext;
        private ViewGroup _selectedGroup;
        private bool _inRepopulate;
        private bool _filterRowSources;
        private bool _grayDisabledRowSources;
        private HashSet<ViewName> _checkedItems = new HashSet<ViewName>();

        public ChooseViewsControl()
        {
            InitializeComponent();
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IViewContext ViewContext
        {
            get { return _viewContext; }
            set
            {
                if (ReferenceEquals(ViewContext, value))
                {
                    return;
                }
                if (null != ViewContext)
                {
                    ViewContext.ViewsChanged -= ViewContextOnViewsChanged;
                }
                _viewContext = value;
                if (null != ViewContext)
                {
                    ViewContext.ViewsChanged += ViewContextOnViewsChanged;
                    _selectedGroup = ViewContext.DefaultViewGroup;
                }
                Repopulate();
            }
        }

        private void Repopulate()
        {
            if (_inRepopulate)
            {
                return;
            }
            try
            {
                _inRepopulate = true;
                if (!Equals(SelectedGroup, toolStripComboGroup.SelectedItem))
                {
                    listView1.Items.Clear();
                }
                toolStripComboGroup.Items.Clear();
                imageList1.Images.Clear();
                var newItems = new List<ListViewItem>();
                var newGroups = new List<ListViewGroup>();
                if (null != ViewContext)
                {
                    imageList1.Images.AddRange(ViewContext.GetImageList());
                    toolStripComboGroup.Items.AddRange(ViewContext.ViewGroups.ToArray());
                    IEnumerable<ViewGroup> groups;
                    if (null != SelectedGroup)
                    {
                        groups = new[] {SelectedGroup};
                        listView1.View = View.List;
                    }
                    else
                    {
                        groups = ViewContext.ViewGroups;
                        listView1.View = View.SmallIcon;
                    }
                    foreach (var group in groups)
                    {
                        var listViewGroup = new ListViewGroup(group.Id.Name, group.Label) {Tag = group};
                        newGroups.Add(listViewGroup);
                        foreach (var viewSpec in ViewContext.GetViewSpecList(group.Id).ViewSpecs)
                        {
                            bool validRowSource = ViewContext.CanDisplayView(viewSpec);
                            if (FilterRowSources && !validRowSource)
                            {
                                continue;
                            }
                            var viewName = new ViewName(group.Id, viewSpec.Name);
                            var item = new ListViewItem(viewSpec.Name, ViewContext.GetImageIndex(viewSpec))
                            {
                                Name = viewName.ToString(),
                                Group= listViewGroup,
                                Tag = viewName,
                                Checked = _checkedItems.Contains(viewName),
                            };
                            if (!validRowSource && GrayDisabledRowSources)
                            {
                                item.ForeColor = SystemColors.GrayText;
                            }
                            newItems.Add(item);
                        }
                    }
                    toolStripComboGroup.SelectedItem = SelectedGroup;
                }
                ReplaceItems(listView1, newGroups, newItems);
            }
            finally
            {
                _inRepopulate = false;
            }
        }

        private void ViewContextOnViewsChanged()
        {
            Repopulate();
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ViewName? SelectedViewName
        {
            get
            {
                if (listView1.SelectedIndices.Count != 1)
                {
                    return null;
                }
                return GetViewName(listView1.Items[listView1.SelectedIndices[0]]);
            }
        }

        public bool SelectView(string name)
        {
            if (name == SelectedViewName.GetValueOrDefault().Name)
            {
                return true;
            }
            listView1.SelectedIndices.Clear();
            for (int i = 0; i < listView1.Items.Count; i++)
            {
                if (GetViewName(listView1.Items[i]).Name == name)
                {
                    listView1.SelectedIndices.Add(i);
                    return true;
                }
            }
            return false;
        }

        public void SelectViews(IEnumerable<string> names)
        {
            var nameSet = new HashSet<string>(names);
            listView1.SelectedIndices.Clear();
            for (int i = 0; i < listView1.Items.Count; i++)
            {
                if (nameSet.Contains(GetViewName(listView1.Items[i]).Name))
                {
                     listView1.SelectedIndices.Add(i);
                }
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IEnumerable<ViewName> SelectedViews
        {
            get
            {
                return listView1.SelectedItems.OfType<ListViewItem>().Select(GetViewName);
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IEnumerable<ViewName> CheckedViews
        {
            get { return _checkedItems.AsEnumerable(); }
            set
            {
                var newCheckedItems = new HashSet<ViewName>(value);
                if (newCheckedItems.SetEquals(CheckedViews))
                {
                    return;
                }
                _checkedItems = newCheckedItems;
                Repopulate();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ViewGroup SelectedGroup
        {
            get { return _selectedGroup; }
            set
            {
                if (Equals(SelectedGroup, value))
                {
                    return;
                }
                _selectedGroup = value;
                Repopulate();
                FireSelectionChanged();
            }
        }

        public bool AllowEditing
        {
            get { return listView1.LabelEdit; }
            set { listView1.LabelEdit = value; }
        }

        public bool MultiSelect
        {
            get { return listView1.MultiSelect; }
            set { listView1.MultiSelect = value; }
        }

        public bool FilterRowSources
        {
            get { return _filterRowSources; }
            set
            {
                if (FilterRowSources == value)
                {
                    return;
                }
                _filterRowSources = value;
                Repopulate();
            }
        }

        public bool GrayDisabledRowSources
        {
            get { return _grayDisabledRowSources; }
            set
            {
                if (GrayDisabledRowSources == value)
                {
                    return;
                }
                _grayDisabledRowSources = value;
                Repopulate();
            }
        }

        public bool ShowGroupChooser
        {
            get { return toolStrip1.Visible; }
            set { toolStrip1.Visible = value; }
        }

        public bool ShowCheckboxes
        {
            get { return listView1.CheckBoxes; }
            set { listView1.CheckBoxes = value; }
        }

        public event EventHandler SelectionChanged;

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            FireSelectionChanged();
        }

        public void FireSelectionChanged()
        {
            if (SelectionChanged != null)
            {
                SelectionChanged(this, new EventArgs());
            }
        }

        private void toolStripComboGroup_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inRepopulate)
            {
                return;
            }
            SelectedGroup = toolStripComboGroup.SelectedItem as ViewGroup;
        }

        private static void ReplaceItems(ListView listView, IList<ListViewGroup> newGroups, IList<ListViewItem> newItems)
        {
            var selectedNames = new HashSet<string>(listView.SelectedItems.OfType<ListViewItem>().Select(item => item.Name));
            string focusedName = null;
            if (null != listView.FocusedItem)
            {
                focusedName = listView.FocusedItem.Name;
            }
            listView.BeginUpdate();
            try
            {
                listView.Groups.Clear();
                listView.Groups.AddRange(newGroups.ToArray());
                listView.Items.Clear();
                listView.SelectedIndices.Clear();
                for (int i = 0; i < newItems.Count; i++)
                {
                    listView.Items.Add(newItems[i]);
                    if (selectedNames.Contains(newItems[i].Name))
                    {
                        listView.SelectedIndices.Add(i);
                    }
                    if (focusedName == newItems[i].Name)
                    {
                        listView.FocusedItem = newItems[i];
                    }
                }
            }
            finally
            {
                listView.EndUpdate();
            }
        }

        private void listView1_BeforeLabelEdit(object sender, LabelEditEventArgs e)
        {
            if (Equals(SelectedGroup, ViewGroup.BUILT_IN))
            {
                e.CancelEdit = true;
            }
        }

        private void listView1_AfterLabelEdit(object sender, LabelEditEventArgs e)
        {
            if (e.Label == null)
            {
                return;
            }
            if (string.IsNullOrEmpty(e.Label))
            {
                ViewContext.ShowMessageBox(this, Resources.ChooseViewsControl_listView1_AfterLabelEdit_Name_cannot_be_blank, MessageBoxButtons.OK);
                e.CancelEdit = true;
                return;
            }
            var listViewItem = listView1.Items[e.Item];
            var oldViewName = GetViewName(listViewItem);
            if (e.Label == listViewItem.Name)
            {
                return;
            }
            if (ViewContext.TryRenameView(oldViewName.GroupId, oldViewName.Name, e.Label))
            {
                return;
            }
            ViewContext.ShowMessageBox(this, string.Format(
                Resources.ChooseViewsControl_listView1_AfterLabelEdit_There_is_already_a_view_named___0___, e.Label), MessageBoxButtons.OK);
            e.CancelEdit = true;
        }

        private ViewName GetViewName(ListViewItem item)
        {
            return (ViewName) item.Tag;
        }

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
                ViewContext = null;
            }
            base.Dispose(disposing);
        }

        private void listView1_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (_inRepopulate)
            {
                return;
            }
            if (e.Item.Checked)
            {
                _checkedItems.Add(GetViewName(e.Item));
            }
            else
            {
                _checkedItems.Remove(GetViewName(e.Item));
            }
        }
    }
}
