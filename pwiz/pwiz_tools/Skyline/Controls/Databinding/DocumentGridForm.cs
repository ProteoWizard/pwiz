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

using pwiz.Common.DataBinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Databinding
{
    public partial class DocumentGridForm : DataboundGridForm
    {
        public DocumentGridForm(SkylineViewContext viewContext)
        {
            InitializeComponent();
            BindingListSource = bindingListSource;
            DataGridView = boundDataGridView;
            NavBar = navBar;
            Icon = Resources.Skyline;
            bindingListSource.SetViewContext(viewContext);
        }

        public DocumentGridForm(IDocumentContainer documentContainer) 
            : this(new DocumentGridViewContext(new SkylineDataSchema(documentContainer)))
        {
            var skylineWindow = documentContainer as SkylineWindow;
            if (null != skylineWindow)
            {
                DataGridViewPasteHandler.Attach(skylineWindow, boundDataGridView);
            }
        }

        public ViewInfo ViewInfo
        {
            get
            {
                return bindingListSource.ViewInfo;
            }
            set
            {
                bindingListSource.SetView(value, bindingListSource.ViewContext.GetRowSource(value));
            }
        }

        public bool ShowViewsMenu
        {
            get
            {
                return navBar.ShowViewsButton;
            }
            set
            {
                navBar.ShowViewsButton = value;
            }
        }
    }
}
