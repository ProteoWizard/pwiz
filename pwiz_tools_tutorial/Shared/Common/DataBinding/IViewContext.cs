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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Application-dependent class for persisting custom views, and displaying view-related UI.
    /// </summary>
    public interface IViewContext
    {
        ColumnDescriptor ParentColumn { get; }
        IEnumerable<ViewSpec> BuiltInViewSpecs { get; }
        IEnumerable<ViewSpec> CustomViewSpecs { get; }
        void Export(Control owner, BindingListView bindingListView);
        ViewSpec CustomizeView(Control owner, ViewSpec viewSpec);
        void ManageViews(Control owner);
        ViewSpec SaveView(ViewSpec viewSpec);
        void DeleteViews(IEnumerable<ViewSpec> viewSpecs);
        DialogResult ShowMessageBox(Control owner, string messsage, MessageBoxButtons messageBoxButtons);
        Icon ApplicationIcon { get; }
        IViewContext GetViewContext(ColumnDescriptor column);
        DataGridViewColumn CreateGridViewColumn(PropertyDescriptor propertyDescriptor);
    }
}
