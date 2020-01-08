/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.Databinding
{
    public partial class DocumentGridForm : DataboundGridForm
    {
        private readonly string _originalFormTitle;
        private readonly SkylineWindow _skylineWindow;
        private IList<AnnotationDef> _annotations;

        public DocumentGridForm(SkylineViewContext viewContext) :
            this(viewContext, null)
        {
        }

        public DocumentGridForm(SkylineViewContext viewContext, string text) 
        {
            InitializeComponent();
            BindingListSource.QueryLock = viewContext.SkylineDataSchema.QueryLock;
            if (!string.IsNullOrEmpty(text))
                Text = text;
            _originalFormTitle = Text;
            BindingListSource.SetViewContext(viewContext);
            BindingListSource.ListChanged += BindingListSourceOnListChanged;
            _skylineWindow = viewContext.SkylineDataSchema.SkylineWindow;
            var documentGridViewContext = viewContext as DocumentGridViewContext;
            if (documentGridViewContext != null)
            {
                documentGridViewContext.BoundDataGridView = DataGridView;
            }
        }

        private void BindingListSourceOnListChanged(object sender, ListChangedEventArgs listChangedEventArgs)
        {
            if (ShowViewsMenu)
            {
                ViewInfo view = BindingListSource.ViewInfo;
                string title;
                if (null == view)
                {
                    title = _originalFormTitle;
                }
                else
                {
                    title = TextUtil.SpaceSeparate(_originalFormTitle + ':', view.Name);
                    if (null != view.ViewGroup)
                    {
                        Settings.Default.DocumentGridView = view.ViewGroup.Id.ViewName(view.Name).ToString();
                    }
                }
                if (TabText != title)
                {
                    TabText = title;
                }
                if (Text != title)
                {
                    Text = title;
                }
            }
        }

        public DocumentGridForm(IDocumentContainer documentContainer) 
            : this(new DocumentGridViewContext(new SkylineDataSchema(documentContainer, SkylineDataSchema.GetLocalizedSchemaLocalizer())))
        {
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (null != _skylineWindow)
            {
                _skylineWindow.DocumentUIChangedEvent += SkylineWindowOnDocumentUIChangedEvent;
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (null != _skylineWindow)
            {
                _skylineWindow.DocumentUIChangedEvent -= SkylineWindowOnDocumentUIChangedEvent;
            }
            base.OnHandleDestroyed(e);
        }

        public ViewInfo ViewInfo
        {
            get
            {
                return BindingListSource.ViewInfo;
            }
            set
            {
                BindingListSource.SetView(value, BindingListSource.ViewContext.GetRowSource(value));
            }
        }

        public bool ShowViewsMenu
        {
            get
            {
                return NavBar.ShowViewsButton;
            }
            set
            {
                NavBar.ShowViewsButton = value;
            }
        }

        private void SkylineWindowOnDocumentUIChangedEvent(object sender, DocumentChangedEventArgs documentChangedEventArgs)
        {
            var newAnnotations = ImmutableList.ValueOf(_skylineWindow.DocumentUI.Settings.DataSettings.AnnotationDefs);
            if (!Equals(newAnnotations, _annotations))
            {
                _annotations = newAnnotations;
                UpdateViewContext();
            }
        }

        private void UpdateViewContext()
        {
            var documentGridViewContext = BindingListSource.ViewContext as DocumentGridViewContext;
            if (documentGridViewContext == null)
            {
                return;
            }
            documentGridViewContext.UpdateBuiltInViews();
            if (null != BindingListSource.ViewInfo && null != BindingListSource.ViewInfo.ViewGroup && ViewGroup.BUILT_IN.Id.Equals(BindingListSource.ViewInfo.ViewGroup.Id))
            {
                var viewName = BindingListSource.ViewInfo.ViewGroup.Id.ViewName(BindingListSource.ViewInfo.Name);
                var newViewInfo = documentGridViewContext.GetViewInfo(viewName);
                if (null != newViewInfo && !ColumnsEqual(newViewInfo, BindingListSource.ViewInfo))
                {
                    BindingListSource.SetView(newViewInfo, BindingListSource.RowSource);
                }
            }
        }

        private bool ColumnsEqual(ViewInfo viewInfo1, ViewInfo viewInfo2)
        {
            if (!Equals(viewInfo1.ViewSpec, viewInfo2.ViewSpec))
            {
                return false;
            }

            if (viewInfo1.DisplayColumns.Count != viewInfo2.DisplayColumns.Count)
            {
                return false;
            }

            for (int icol = 0; icol < viewInfo1.DisplayColumns.Count; icol++)
            {
                if (!viewInfo1.DisplayColumns[icol].ColumnDescriptor.GetAttributes()
                    .SequenceEqual(viewInfo2.DisplayColumns[icol].ColumnDescriptor.GetAttributes()))
                {
                    return false;
                }
            }

            return true;
        }

        //Adjusts column width to make sure the headers are displayed in a single line. Used for tutorials testing.
        public void ExpandColumns()
        {
            using (Graphics g = DataGridView.CreateGraphics())
            {
                foreach (DataGridViewColumn col in DataGridView.Columns)
                {
                    col.Width = (int)g.MeasureString(col.HeaderText, DataGridView.Font).Width + 40;
                }
            }
        }

        private void DocumentGridForm_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    _skylineWindow.FocusDocument();
                    break;
            }
        }
    }
}

