/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Lists;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.Lists
{
    public class ListGridForm : DataboundGridForm
    {
        public ListGridForm(IDocumentContainer documentContainer, string listName)
        {
            var skylineDataSchema = new SkylineDataSchema(documentContainer, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            ListViewContext = ListViewContext.CreateListViewContext(skylineDataSchema, listName);
            BindingListSource.QueryLock = ListViewContext.SkylineDataSchema.QueryLock;
            BindingListSource.ListChanged += BindingListSourceOnListChanged;
            ListViewContext.BoundDataGridView = DataGridView;
            DataboundGridControl.BindingListSource.SetViewContext(ListViewContext);
            DataboundGridControl.BindingListSource.NewRowHandler = ListViewContext;
            Text = TabText = TextUtil.SpaceSeparate(Text + ':', listName);
        }

        public ListViewContext ListViewContext { get; private set; }
        public string ListName {get { return ListViewContext.ListName; }}

        private void BindingListSourceOnListChanged(object sender, ListChangedEventArgs listChangedEventArgs)
        {
            string title = Resources.ListGridForm_BindingListSourceOnListChanged_List__ + ListName;
            ViewInfo view = BindingListSource.ViewInfo;
            if (null != view && view.Name != AbstractViewContext.DefaultViewName)
            {
                title = TextUtil.SpaceSeparate(title, view.Name);
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
}
