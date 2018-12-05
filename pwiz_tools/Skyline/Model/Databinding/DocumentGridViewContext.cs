﻿/*
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

using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Databinding
{
    public class DocumentGridViewContext : SkylineViewContext
    {
        public DocumentGridViewContext(SkylineDataSchema dataSchema)
            : base(dataSchema, GetDocumentGridRowSources(dataSchema))
        {
        }

        public bool EnablePreview { get; set; }

        protected override ViewEditor CreateViewEditor(ViewGroup viewGroup, ViewSpec viewSpec)
        {
            var viewEditor = base.CreateViewEditor(viewGroup, viewSpec);
            viewEditor.SetViewTransformer(new DocumentViewTransformer());
            viewEditor.AddViewEditorWidget(new PivotReplicateAndIsotopeLabelWidget {Dock = DockStyle.Left});
#if DEBUG
            viewEditor.ShowSourceTab = true;
#else
            viewEditor.ShowSourceTab = false;
#endif
            if (EnablePreview)
            {
                viewEditor.PreviewButtonVisible = true;
                viewEditor.Text = Resources.DocumentGridViewContext_CreateViewEditor_Edit_Report;
            }
            return viewEditor;
        }

        public override void Preview(Control owner, ViewInfo viewInfo)
        {
            string title;
            if (string.IsNullOrEmpty(viewInfo.Name))
            {
                title = Resources.DocumentGridViewContext_Preview_Preview_New_Report;
            }
            else
            {
                title = string.Format(Resources.DocumentGridViewContext_Preview_Preview___0_, viewInfo.Name);
            }
            var dialog = new DocumentGridForm(this)
            {
                ViewInfo = viewInfo,
                ShowViewsMenu = false,
                Text = title,
            };
            dialog.ShowDialog(owner);
        }

        public BoundDataGridView BoundDataGridView { get; set; }

        public override bool DeleteEnabled
        {
            get
            {
                if (BoundDataGridView == null)
                {
                    return false;
                }
                var bindingListSource = BoundDataGridView.DataSource as BindingListSource;
                if (bindingListSource == null)
                {
                    return false;
                }
                var viewInfo = bindingListSource.ViewInfo;
                if (viewInfo == null)
                {
                    return false;
                }
                return typeof(SkylineDocNode).IsAssignableFrom(viewInfo.ParentColumn.PropertyType);
            }
        }

        public override void Delete()
        {
            DeleteSkylineDocNodes(BoundDataGridView, GetSelectedDocNodes(BoundDataGridView));
        }

        private List<SkylineDocNode> GetSelectedDocNodes(BoundDataGridView dataGridView)
        {
            var docNodes = new List<SkylineDocNode>();
            var identityPaths = new HashSet<IdentityPath>();
            if (null == dataGridView)
            {
                return docNodes;
            }
            var bindingSource = dataGridView.DataSource as BindingListSource;
            if (null == bindingSource)
            {
                return docNodes;
            }
            var selectedRows = dataGridView.SelectedRows.Cast<DataGridViewRow>()
                .Select(row => (RowItem) bindingSource[row.Index]).ToArray();
            if (!selectedRows.Any())
            {
                selectedRows = new[] {bindingSource.Current as RowItem};
            }

            foreach (var rowItem in selectedRows)
            {
                if (rowItem == null)
                {
                    continue;
                }
                var docNode = rowItem.Value as SkylineDocNode;
                if (docNode == null)
                {
                    continue;
                }
                if (identityPaths.Add(docNode.IdentityPath))
                {
                    docNodes.Add(docNode);
                }
            }
            return docNodes;
        }

        /// <summary>
        /// Creates a DocumentGridViewContext that can be used for exporting reports, importing report definitions, etc.
        /// </summary>
        public static DocumentGridViewContext CreateDocumentGridViewContext(SrmDocument document, DataSchemaLocalizer dataSchemaLocalizer)
        {
            if (document == null)
            {
                document = new SrmDocument(SrmSettingsList.GetDefault());
            }
            var memoryDocumentContainer = new MemoryDocumentContainer();
            memoryDocumentContainer.SetDocument(document, memoryDocumentContainer.Document);
            return new DocumentGridViewContext(new SkylineDataSchema(memoryDocumentContainer, dataSchemaLocalizer));
        }

        protected override ViewSpec GetBlankView()
        {
            return new ViewSpec().SetRowType(typeof (Protein)).SetSublistId(PropertyPath.Parse(@"Results!*"));
        }

        public void UpdateBuiltInViews()
        {
            RowSources = GetDocumentGridRowSources(SkylineDataSchema);
}
    }
}
