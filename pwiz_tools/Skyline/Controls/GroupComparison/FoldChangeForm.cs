/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using DigitalRune.Windows.Docking;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.GroupComparison
{
    public partial class FoldChangeForm : DockableFormEx
    {
        private IDocumentUIContainer _documentContainer;
        private string _groupComparisonName;
        private Form _owner;
        public FoldChangeForm()
        {
            InitializeComponent();
            Icon = Resources.Skyline;
        }

        public void SetBindingSource(FoldChangeBindingSource bindingSource)
        {
            FoldChangeBindingSource = bindingSource;
            string groupComparisonName = bindingSource.GroupComparisonModel.GroupComparisonName ??
                                         bindingSource.GroupComparisonModel.GroupComparisonDef.Name;
            SetGroupComparisonName(bindingSource.GroupComparisonModel.DocumentContainer as IDocumentUIContainer, groupComparisonName);
        }

        public void SetGroupComparisonName(IDocumentUIContainer documentContainer, string groupComparisonName)
        {
            _documentContainer = documentContainer;
            _groupComparisonName = groupComparisonName;
            Text = TabText = GetTitle(groupComparisonName);
        }

        public virtual string GetTitle(string groupComparisonName)
        {
            if (string.IsNullOrEmpty(groupComparisonName))
            {
                return GroupComparisonStrings.FoldChangeForm_GetTitle_New_Group_Comparison;
            }
            return groupComparisonName;
        }

        public FoldChangeBindingSource FoldChangeBindingSource { get; private set; }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (null != FoldChangeBindingSource)
            {
                FoldChangeBindingSource.AddRef();
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (null == FoldChangeBindingSource)
            {
                if (null != _documentContainer)
                {
                    FoldChangeBindingSource = FindOrCreateBindingSource(_documentContainer, _groupComparisonName);
                    if (IsHandleCreated)
                    {
                        FoldChangeBindingSource.AddRef();
                    }
                }
            }
            _owner = Owner;
            if (null != _owner)
            {
                _owner.FormClosed += OwnerFormClosed;
            }
        }

        private void OwnerFormClosed(object sender, EventArgs args)
        {
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (null != _owner)
            {
                _owner.FormClosed -= OwnerFormClosed;
            }
            Dispose();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (null != FoldChangeBindingSource)
            {
                FoldChangeBindingSource.Release();
            }
            base.OnHandleDestroyed(e);
        }

        protected override string GetPersistentString()
        {

            return base.GetPersistentString() + '|' + Uri.EscapeDataString(_groupComparisonName);
        }

        protected string GroupComparisonName
        {
            get
            {
                return _groupComparisonName;
            }
        }

        public virtual bool IsComplete
        {
            get
            {
                return FoldChangeBindingSource.IsComplete;
            }
        }

        public static T FindForm<T>(IDocumentContainer documentContainer, string groupComparisonName) 
            where T : FoldChangeForm
        {
            foreach (var form in FormUtil.OpenForms.OfType<T>())
            {
                var foldChangeBindingSource = form.FoldChangeBindingSource;
                if (null == foldChangeBindingSource)
                {
                    continue;
                }
                if (groupComparisonName == foldChangeBindingSource.GroupComparisonModel.GroupComparisonName
                    && ReferenceEquals(documentContainer, foldChangeBindingSource.GroupComparisonModel.DocumentContainer))
                {
                    return form;
                }
            }
            return null;
        }

        public bool SameBindingSource(FoldChangeForm foldChangeForm)
        {
            if (null != FoldChangeBindingSource && null != foldChangeForm.FoldChangeBindingSource)
            {
                return ReferenceEquals(FoldChangeBindingSource, foldChangeForm.FoldChangeBindingSource);
            }
            if (!ReferenceEquals(_documentContainer, foldChangeForm._documentContainer))
            {
                return false;
            }
            if (string.IsNullOrEmpty(_groupComparisonName))
            {
                return false;
            }
            return _groupComparisonName == foldChangeForm._groupComparisonName;
        }

        public void ShowFoldChangeForm<T>() where T : FoldChangeForm, new()
        {
            IEnumerable<FoldChangeForm> forms;
            if (null != DockPanel)
            {
                forms = DockPanel.Contents.OfType<FoldChangeForm>();
            }
            else
            {
                forms = FormUtil.OpenForms.OfType<FoldChangeForm>();
            }

            FoldChangeForm otherOpenForm = null;
            foreach (var form in forms)
            {
                if (form is T)
                {
                    if (SameBindingSource(form))
                    {
                        form.Activate();
                        return;
                    }
                }
                else if (otherOpenForm == null && !(form is FoldChangeGrid) && SameBindingSource(form))
                {
                    otherOpenForm = form;
                }
            }

            var graph = new T();
            graph.SetBindingSource(FoldChangeBindingSource);

            var otherDockPane = FindPane(otherOpenForm);

            if (otherOpenForm != null && otherDockPane != null)
                graph.Show(otherDockPane, null);
            else if (Pane != null && this is FoldChangeGrid)
                graph.Show(Pane, DockPaneAlignment.Right, 0.5);
            else
                graph.Show(Owner);
        }

        private DockPane FindPane(IDockableForm form)
        {
            if (DockPanel == null)
                return null;

            int iPane = DockPanel.Panes.IndexOf(pane => !pane.IsHidden && pane.Contents.Contains(form));
            return (iPane != -1 ? DockPanel.Panes[iPane] : null);
        }
        public void ShowChangeSettings()
        {
            foreach (var form in FormUtil.OpenForms.OfType<EditGroupComparisonDlg>())
            {
                if (ReferenceEquals(form.GroupComparisonModel, FoldChangeBindingSource.GroupComparisonModel))
                {
                    form.Activate();
                    return;
                }
            }
            var foldChangeSettings = new EditGroupComparisonDlg(FoldChangeBindingSource);
            foldChangeSettings.Show(this);
        }

        protected virtual void BuildContextMenu(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            ZedGraphHelper.BuildContextMenu(sender, menuStrip, true);

            var index = 0;
            menuStrip.Items.Insert(index++, new ToolStripMenuItem(GroupComparisonResources.FoldChangeForm_BuildContextMenu_Grid, null, OnGridClick));
            if (!(sender.ParentForm is FoldChangeVolcanoPlot))
                menuStrip.Items.Insert(index++, new ToolStripMenuItem(GroupComparisonResources.FoldChangeForm_BuildContextMenu_Volcano_Plot, null, OnVolcanoPlotClick));
            if(!(sender.ParentForm is FoldChangeBarGraph))
                menuStrip.Items.Insert(index++, new ToolStripMenuItem(GroupComparisonResources.FoldChangeForm_BuildContextMenu_Bar_Graph, null, OnBarGraphClick));
            menuStrip.Items.Insert(index++, new ToolStripMenuItem(GroupComparisonResources.FoldChangeForm_BuildContextMenu_Settings, null, OnSettingsClick));
            menuStrip.Items.Insert(index++, new ToolStripSeparator());
        }

        private void OnSettingsClick(object sender, EventArgs eventArgs)
        {
            ShowChangeSettings();
        }

        private void OnVolcanoPlotClick(object o, EventArgs eventArgs)
        {
            ShowFoldChangeForm<FoldChangeVolcanoPlot>();
        }

        private void OnBarGraphClick(object o, EventArgs eventArgs)
        {
            ShowFoldChangeForm<FoldChangeBarGraph>();
        }

        private void OnGridClick(object o, EventArgs eventArgs)
        {
            Program.MainWindow.ShowGroupComparisonWindow(_groupComparisonName);
        }

        protected IEnumerable<FoldChangeBindingSource.FoldChangeRow> GetFoldChangeRows(
            BindingListSource bindingListSource)
        {
            return bindingListSource.OfType<RowItem>()
                .Select(rowItem => rowItem.Value)
                .OfType<FoldChangeBindingSource.AbstractFoldChangeRow>()
                .SelectMany(row => row.GetFoldChangeRows());
        }

        public static FoldChangeBindingSource FindOrCreateBindingSource(IDocumentUIContainer documentContainer,
            string groupComparisonName)
        {
            var form = FindForm<FoldChangeForm>(documentContainer, groupComparisonName);
            if (null != form)
            {
                return form.FoldChangeBindingSource;
            }

            return new FoldChangeBindingSource(new GroupComparisonModel(documentContainer, groupComparisonName));
        }

        public static FoldChangeForm RestoreFoldChangeForm(IDocumentUIContainer documentContainer, string persistentString)
        {
            var parsed = PersistentString.Parse(persistentString);
            if (parsed.Parts.Count < 2)
            {
                return null;
            }

            var formTypeName = parsed.Parts[0];
            var groupComparisonName = parsed.Parts[1];
            FoldChangeForm foldChangeForm;
            foreach (var type in new[]
                {typeof(FoldChangeGrid), typeof(FoldChangeBarGraph), typeof(FoldChangeVolcanoPlot)})
            {
                if (type.FullName == formTypeName)
                {
                    var constructor = type.GetConstructor(new Type[0]);
                    // ReSharper disable PossibleNullReferenceException
                    foldChangeForm = (FoldChangeForm) constructor.Invoke(new object[0]);
                    // ReSharper restore PossibleNullReferenceException
                    foldChangeForm.SetGroupComparisonName(documentContainer, groupComparisonName);
                    if (foldChangeForm is FoldChangeGrid grid)
                    {
                        if (parsed.Parts.Count >= 3)
                        {
                            grid.ViewToRestore = ViewName.Parse(parsed.Parts[2]);
                        }
                    }

                    if (foldChangeForm is FoldChangeVolcanoPlot volcano)
                    {
                        if (parsed.Parts.Count >= 3)
                            volcano.SetLayout(groupComparisonName, parsed.Parts[2]);
                    }

                    return foldChangeForm;
                }
            }
            return null;
        }

        public static void CloseInapplicableForms(IDocumentContainer documentContainer)
        {
            var groupComparisonNames = new HashSet<string>(
                documentContainer.Document.Settings.DataSettings.GroupComparisonDefs.Select(def => def.Name));
            foreach (var form in FormUtil.OpenForms.OfType<FoldChangeForm>())
            {
                if (!ReferenceEquals(documentContainer, form._documentContainer))
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(form._groupComparisonName) &&
                    !groupComparisonNames.Contains(form._groupComparisonName))
                {
                    form.BeginInvoke(new Action(form.Close));
                }
            }
        }
        private void FoldChangeForm_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    _documentContainer.FocusDocument();
                    e.Handled = true;
                    break;
            }
        }
    }
}
