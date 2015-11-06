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

using System.ComponentModel;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.Databinding
{
    public partial class DocumentGridForm : DataboundGridForm
    {
        private string _originalFormTitle;
        public DocumentGridForm(SkylineViewContext viewContext)
        {
            InitializeComponent();
            _originalFormTitle = Text;
            BindingListSource.SetViewContext(viewContext);
            BindingListSource.ListChanged += BindingListSourceOnListChanged;
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
                Text = TabText = title;
            }
        }

        public DocumentGridForm(IDocumentContainer documentContainer) 
            : this(new DocumentGridViewContext(new SkylineDataSchema(documentContainer, SkylineDataSchema.GetLocalizedSchemaLocalizer())))
        {
            var skylineWindow = documentContainer as SkylineWindow;
            if (null != skylineWindow)
            {
                DataGridViewPasteHandler.Attach(skylineWindow, DataGridView);
            }
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

    }
}
