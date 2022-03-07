/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Databinding
{
    public partial class ExportLiveReportDlg : FormEx
    {
        private const int indexLocalizedLanguage = 0;
        private const int indexInvariantLanguage = 1;
        private const int indexImageFolder = 0;
        private const int indexImageBlank = 1;
        private const int indexFirstImage = 2;
        private readonly IDocumentUIContainer _documentUiContainer;
        private DocumentGridViewContext _viewContext;

        public ExportLiveReportDlg(IDocumentUIContainer documentUIContainer)
        {
            InitializeComponent();
            Icon = Resources.Skyline;
            _documentUiContainer = documentUIContainer;
            Debug.Assert(indexLocalizedLanguage == comboLanguage.Items.Count);
            comboLanguage.Items.Add(CultureInfo.CurrentUICulture.DisplayName);
            Debug.Assert(indexInvariantLanguage == comboLanguage.Items.Count);
            comboLanguage.Items.Add(Resources.ExportLiveReportDlg_ExportLiveReportDlg_Invariant);
            comboLanguage.SelectedIndex = 0;
        }

        public void OkDialog()
        {
            if (!ExportReport(null, ','))
                return;

            DialogResult = DialogResult.OK;
            Close();
        }

        public void OkDialog(string fileName, char separator)
        {
            Settings.Default.ExportDirectory = Path.GetDirectoryName(fileName);

            if (!ExportReport(fileName, separator))
                return;

            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (null != _documentUiContainer)
            {
                _viewContext = new DocumentGridViewContext(new SkylineDataSchema(_documentUiContainer,
                        DataSchemaLocalizer.INVARIANT));
                _viewContext.ViewsChanged += OnViewsChanged;
                imageList1.Images.Clear();
                imageList1.Images.Add(Resources.Folder);
                imageList1.Images.Add(Resources.Blank);
                imageList1.Images.AddRange(_viewContext.GetImageList());
                Repopulate();
            }
        }

        void OnViewsChanged()
        {
            Repopulate();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            if (null != _viewContext)
            {
                _viewContext.ViewsChanged -= OnViewsChanged;
                _viewContext = null;
            }
        }

        private DataSchemaLocalizer GetDataSchemaLocalizer()
        {
            return InvariantLanguage
                ? DataSchemaLocalizer.INVARIANT
                : SkylineDataSchema.GetLocalizedSchemaLocalizer();
        }
       
        private bool ExportReport(string filename, char separator)
        {
            var viewContext = GetViewContext(true);
            var viewInfo = viewContext.GetViewInfo(SelectedViewName);
            if (null == viewInfo)
            {
                return false;
            }
            if (null == filename)
            {
                return viewContext.Export(this, viewInfo);
            }
            return viewContext.ExportToFile(this, viewInfo, filename, viewContext.GetDsvWriter(separator));
        }

        private void Repopulate()
        {
            treeView1.Nodes.Clear();
            if (null == _viewContext)
            {
                return;
            }
            var newNodes = new List<TreeNode>();
            foreach (var group in _viewContext.ViewGroups)
            {
                var groupNode = new TreeNode(group.Label) {Name = group.Id.Name, Tag=group};
                groupNode.SelectedImageIndex = groupNode.ImageIndex = indexImageFolder;
                foreach (var viewSpec in _viewContext.GetViewSpecList(group.Id).ViewSpecs)
                {
                    if (null == _viewContext.GetViewInfo(group, viewSpec))
                    {
                        continue;
                    }
                    var viewNode = new TreeNode(viewSpec.Name)
                    {
                        Name = viewSpec.Name
                    };
                    int imageIndex = _viewContext.GetImageIndex(viewSpec);
                    if (imageIndex >= 0)
                    {
                        imageIndex += indexFirstImage;
                    }
                    else
                    {
                        imageIndex = indexImageBlank;
                    }
                    if (imageIndex >= 0)
                    {
                        viewNode.SelectedImageIndex = viewNode.ImageIndex = imageIndex;
                    }
                    groupNode.Nodes.Add(viewNode);
                }
                newNodes.Add(groupNode);
            }
            treeView1.Nodes.AddRange(newNodes.ToArray());
            treeView1.ExpandAll();
            UpdateButtons();
        }

        private SkylineViewContext GetViewContext(bool clone)
        {
            SkylineDataSchema dataSchema;
            if (clone)
            {
                var documentContainer = new MemoryDocumentContainer();
                documentContainer.SetDocument(_documentUiContainer.DocumentUI, documentContainer.Document);
                dataSchema = new SkylineDataSchema(documentContainer, GetDataSchemaLocalizer());
            }
            else
            {
                dataSchema = new SkylineDataSchema(_documentUiContainer, GetDataSchemaLocalizer());
            }
            return new DocumentGridViewContext(dataSchema) {EnablePreview = true};
        }

        public bool InvariantLanguage
        {
            get { return comboLanguage.SelectedIndex == indexInvariantLanguage; }
        }

        public void SetUseInvariantLanguage(bool useInvariantLanguage)
        {
            comboLanguage.SelectedIndex = useInvariantLanguage ? indexInvariantLanguage : indexLocalizedLanguage;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void btnPreview_Click(object sender, EventArgs e)
        {
            ShowPreview();
        }

        public void ShowPreview()
        {
            var viewContext = GetViewContext(false);
            var viewInfo = viewContext.GetViewInfo(SelectedViewName);
            var form = new DocumentGridForm(viewContext)
            {
                Text = Resources.ExportLiveReportDlg_ShowPreview_Preview__ + viewInfo.Name,
                ShowViewsMenu = false,
            };
            form.GetModeUIHelper().IgnoreModeUI = true; // Don't want any "peptide"=>"molecule" translation in title etc
            form.BindingListSource.SetViewContext(viewContext, viewInfo);
            form.Show(Owner);
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            EditList();
        }

        public void EditList()
        {
            using (var manageViewsForm = new ManageViewsForm(GetViewContext(false)))
            {
                manageViewsForm.ShowDialog(this);
            } 
        }

        public void CancelClick()
        {
            DialogResult = btnCancel.DialogResult;
        }

        public string ReportName
        {
            get
            {
                return SelectedViewName.GetValueOrDefault().Name;
            }
            set 
            {
                foreach (TreeNode groupNode in treeView1.Nodes)
                {
                    foreach (TreeNode viewNode in groupNode.Nodes)
                    {
                        if (viewNode.Name == value)
                        {
                            treeView1.SelectedNode = viewNode;
                        }
                    }
                }
            }
        }

        public ViewName? SelectedViewName
        {
            get { return GetViewName(treeView1.SelectedNode); }
        }

        private ViewName? GetViewName(TreeNode node)
        {
            if (null == node || null == node.Parent)
            {
                return null;
            }
            return new ViewGroupId(node.Parent.Name).ViewName(node.Name);
        }

        public void UpdateButtons()
        {
            btnExport.Enabled = btnPreview.Enabled = null != SelectedViewName;
        }

        private void treeView1_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node != null && null == e.Node.Parent)
            {
                // Prevent the root elements of the tree from being selected
                e.Cancel = true;
            }
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            UpdateButtons();
        }

        private void treeView1_DoubleClick(object sender, EventArgs e)
        {
            // Export the selected report
            OkDialog();
        }
    }
}
