/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.Controls;
using pwiz.Common.Properties;

namespace pwiz.Common.DataBinding.Controls.Editor
{
    /// <summary>
    /// User interface for choosing which columns should go in a view, and setting the filter and sort.
    /// </summary>
    public partial class ViewEditor : CommonFormEx, IViewEditor, IMultipleViewProvider
    {
        private bool _inChangeView;
        private bool _showHiddenFields;
        // ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
        private readonly ChooseColumnsTab _chooseColumnsTab;
        private readonly FilterTab _filterTab;
        private readonly SourceTab _sourceTab;
        private readonly List<ViewEditorWidget> _editorWidgets = new List<ViewEditorWidget>();
        // ReSharper restore PrivateFieldCanBeConvertedToLocalVariable
        private readonly List<KeyValuePair<ViewInfo, IList<PropertyPath>>> _undoStack;
        private int _undoIndex;
        private IList<PropertyPath> _selectedPaths = ImmutableList.Empty<PropertyPath>();

        public TabPage TabPageSource
        {
            get { return tabPageSource; }
            set { tabPageSource = value; }
        }

        public class ChooseColumnsView : IFormView {}
        public class FilterView : IFormView {}
        // public class SourceView : IFormView {}   inaccessible

        private static readonly IFormView[] TAB_PAGES =
        {
            new ChooseColumnsView(), new FilterView(), // new SourceView() innaccessible
        };

        public ViewEditor(IViewContext viewContext, ViewInfo viewInfo)
        {
            InitializeComponent();
            ViewContext = viewContext;
            ViewInfo = OriginalViewInfo = viewInfo;
            _undoIndex = 0;
            _undoStack = new List<KeyValuePair<ViewInfo, IList<PropertyPath>>>();
            SetViewInfo(ViewInfo, new PropertyPath[0]);
            tbxViewName.Text = ViewSpec.Name;
            Icon = ViewContext.ApplicationIcon;
            _chooseColumnsTab = new ChooseColumnsTab {Dock = DockStyle.Fill};
            tabPageColumns.Controls.Add(_chooseColumnsTab);
            _filterTab = new FilterTab { Dock = DockStyle.Fill };
            tabPageFilter.Controls.Add(_filterTab);
            _sourceTab = new SourceTab{Dock = DockStyle.Fill};
            tabPageSource.Controls.Add(_sourceTab);
            _editorWidgets.AddRange(new ViewEditorWidget[] { _chooseColumnsTab, _filterTab, _sourceTab });
            foreach (var tab in _editorWidgets)
            {
                tab.SetViewEditor(this);
            }
            toolButtonShowAdvanced.Checked = ShowHiddenFields;
            if (!ShowHiddenFields)
            {
                tabControl1.TabPages.Remove(tabPageSource);
            }
        }

        public ColumnDescriptor ParentColumn { get { return ViewInfo.ParentColumn; } }
        public IViewContext ViewContext { get; private set; }
        public ViewInfo OriginalViewInfo { get; private set; }

        public ViewSpec ViewSpec { 
            get
            {
                return ViewInfo.ViewSpec;
            }
        }

        public IEnumerable<PropertyPath> SelectedPaths
        {
            get
            {
                return _selectedPaths;
            }
            set
            {
                if (_selectedPaths.SequenceEqual(value))
                {
                    return;
                }
                _selectedPaths = ImmutableList.ValueOf(value);
                if (null != ViewChange)
                {
                    ViewChange(this, new EventArgs());
                }
            }
        }

        public string ViewName
        {
            get
            {
                return tbxViewName.Text;
            }
            set
            {
                tbxViewName.Text = value;
            }
        }

        public void SetViewInfo(ViewInfo viewInfo, IEnumerable<PropertyPath> selectedPaths)
        {
            bool inChangeView = _inChangeView;
            try
            {
                _inChangeView = true;
                _undoStack.RemoveRange(_undoIndex, _undoStack.Count - _undoIndex);
                _undoStack.Add(new KeyValuePair<ViewInfo, IList<PropertyPath>>(viewInfo, ImmutableList.ValueOf(selectedPaths ?? _selectedPaths)));
                ApplyViewInfo(_undoStack[_undoIndex++]);
            }
            finally
            {
                _inChangeView = inChangeView;
            }
        }

        private void ApplyViewInfo(KeyValuePair<ViewInfo, IList<PropertyPath>> undoEntry)
        {
            ViewInfo = undoEntry.Key;
            _selectedPaths = undoEntry.Value;
            toolButtonUndo.Enabled = _undoIndex > 1;
            toolButtonRedo.Enabled = _undoIndex < _undoStack.Count;
            if (ViewChange != null)
            {
                ViewChange(this, new EventArgs());
            }

        }

        public ViewSpec GetViewSpecToPersist()
        {
            return ViewSpec;
        }

        public IViewTransformer ViewTransformer { get; private set; }

        public void SetViewTransformer(IViewTransformer viewTransformer)
        {
            ViewTransformer = viewTransformer;
            if (null != ViewChange)
            {
                ViewChange(this, new EventArgs());
            }
        }

        public event EventHandler ViewChange;

        private void OnLoad(object sender, EventArgs e)
        {
            // If you set this in the Designer, DataGridView has a defect that causes it to throw an
            // exception if the the cursor is positioned over the record selector column during loading.
        }

        public ViewInfo ViewInfo
        {
            get; private set;
        }

        public bool ShowHiddenFields
        {
            get
            {
                return _showHiddenFields;
            }
            set
            {
                if (ShowHiddenFields == value)
                {
                    return;
                }
                _showHiddenFields = value;
                toolButtonShowAdvanced.Checked = ShowHiddenFields;
                if (ShowHiddenFields)
                {
                    if (tabPageSource.Parent == null)
                    {
                        tabControl1.TabPages.Add(tabPageSource);
                    }
                }
                else
                {
                    if (tabPageSource.Parent == tabControl1)
                    {
                        tabControl1.TabPages.Remove(tabPageSource);
                    }
                }
                if (null != ViewChange)
                {
                    ViewChange(this, new EventArgs());
                }
            }
        }

        public bool ShowSourceTab
        {
            get
            {
                return tabPageSource.Visible;
            }
            set
            {
                tabPageSource.Visible = value;
            }
        }

        public IFormView ShowingFormView
        {
            get
            {
                int selectedIndex = 0;
                Invoke(new Action(() => selectedIndex = tabControl1.SelectedIndex));
                return TAB_PAGES[selectedIndex];
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel || DialogResult == DialogResult.Cancel)
            {
                return;
            }
            ValidateViewName(e);
        }

        protected void ValidateViewName(FormClosingEventArgs formClosingEventArgs)
        {
            if (formClosingEventArgs.Cancel)
            {
                return;
            }
            var name = tbxViewName.Text;
            string errorMessage = null;
            if (string.IsNullOrEmpty(name))
            {
                errorMessage = Resources.CustomizeViewForm_ValidateViewName_View_name_cannot_be_blank_;
            }
            else 
            {
                if (name != OriginalViewInfo.Name)
                {
                    if (ViewContext.GetViewSpecList(OriginalViewInfo.ViewGroup.Id).ViewSpecs.Any(viewSpec=>viewSpec.Name == name))
                    {
                        errorMessage = string.Format(Resources.ViewEditor_ValidateViewName_There_is_already_a_view_named___0___, name);
                    }
                }
            }
            if (errorMessage != null)
            {
                ViewContext.ShowMessageBox(this, errorMessage, MessageBoxButtons.OK);
                formClosingEventArgs.Cancel = true;
            }
            if (formClosingEventArgs.Cancel)
            {
                tbxViewName.Focus();
            }
        }

        public static bool IsCanonical(DisplayColumn displayColumn)
        {
            if (displayColumn.ColumnSpec.Hidden && null != displayColumn.ColumnSpec.SortDirection)
            {
                return false;
            }
            return true;
        }

        private void toolButtonUndo_Click(object sender, EventArgs e)
        {
            if (_inChangeView || _undoIndex <= 1)
            {
                return;
            }
            try
            {
                _inChangeView = true;
                _undoIndex--;
                ApplyViewInfo(_undoStack[_undoIndex - 1]);
            }
            finally
            {
                _inChangeView = false;
            }
        }

        private void toolButtonRedo_Click(object sender, EventArgs e)
        {
            if (_inChangeView || _undoIndex >= _undoStack.Count)
            {
                return;
            }
            try
            {
                _inChangeView = true;
                ApplyViewInfo(_undoStack[_undoIndex++]);
            }
            finally
            {
                _inChangeView = false;
            }
        }

        private void toolButtonShowAdvanced_Click(object sender, EventArgs e)
        {
            ShowHiddenFields = !ShowHiddenFields;
        }

        public void AddViewEditorWidget(ViewEditorWidget viewEditorWidget)
        {
            _editorWidgets.Add(viewEditorWidget);
            viewEditorWidget.SetViewEditor(this);
            panelButtons.Controls.Add(viewEditorWidget);
        }

        public IEnumerable<ViewEditorWidget> ViewEditorWidgets
        {
            get
            {
                return _editorWidgets.AsEnumerable();
            }
        }

        public ChooseColumnsTab ChooseColumnsTab
        {
            get
            {
                return _chooseColumnsTab;
            }
        }

        public FilterTab FilterTab
        {
            get { return _filterTab; }
        }

        public bool PreviewButtonVisible
        {
            get
            {
                return btnPreview.Visible;
            }
            set
            {
                btnPreview.Visible = value;
            }
        }

        private void btnPreview_Click(object sender, EventArgs e)
        {
            ShowPreview();
        }

        public void ShowPreview()
        {
            Debug.Assert(PreviewButtonVisible);
            var viewInfo = new ViewInfo(ViewInfo.ParentColumn, ViewSpec.SetName(ViewName));
            ViewContext.Preview(this, viewInfo);
        }

        public TabControl TabControl { get { return tabControl1; }}

        public AvailableFieldsTree ActiveAvailableFieldsTree
        {
            get
            {
                switch (TabControl.SelectedIndex)
                {
                    case 0:
                        return ChooseColumnsTab.AvailableFieldsTree;
                    case 1:
                        return FilterTab.AvailableFieldsTree;
                }
                return null;
            }
        }

        public void OkDialog()
        {
            DialogResult = btnOK.DialogResult;
        }

        public void ActivatePropertyPath(PropertyPath propertyPath)
        {
            if (null != PropertyPathActivated)
            {
                PropertyPathActivated(this, new PropertyPathEventArgs(propertyPath));
            }
        }

        public event EventHandler<PropertyPathEventArgs> PropertyPathActivated;

        private void toolButtonFind_Click(object sender, EventArgs e)
        {
            ShowFindDialog();
        }

        public FindColumnDlg GetFindColumnDlg()
        {
            return OwnedForms.OfType<FindColumnDlg>().FirstOrDefault();
        }

        public void ShowFindDialog()
        {
            FindColumnDlg findColumnDlg = GetFindColumnDlg();
            if (findColumnDlg != null)
            {
                findColumnDlg.Activate();
            }
            else
            {
                findColumnDlg = new FindColumnDlg {ViewEditor = this};
                findColumnDlg.Show(this);
            }
        }

        private void ViewEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Handled)
            {
                if (e.Modifiers == Keys.Control && e.KeyCode == Keys.F)
                {
                    e.Handled = true;
                    ShowFindDialog();
                }
            }
        }
    }
}
