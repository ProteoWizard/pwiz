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
using pwiz.Common.DataBinding.Controls;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Application-dependent class for persisting custom views, and displaying view-related UI.
    /// </summary>
    public interface IViewContext
    {
        IEnumerable<ViewSpec> BuiltInViews { get; }
        IEnumerable<ViewSpec> CustomViews { get; set; }
        IEnumerable GetRowSource(ViewInfo viewInfo);
        ViewInfo GetViewInfo(ViewSpec viewSpec);
        void Export(Control owner, BindingListSource bindingListSource);
        BindingListSource ExecuteQuery(Control owner, ViewSpec viewSpec);
        ViewSpec NewView(Control owner);
        ViewSpec CustomizeView(Control owner, ViewSpec viewSpec);
        ViewSpec CopyView(Control owner, ViewSpec currentView);
        void ManageViews(Control owner);
        void DeleteViews(IEnumerable<ViewSpec> viewSpecs);
        void ExportViews(Control owner, IEnumerable<ViewSpec> views);
        void ImportViews(Control owner);
        DialogResult ShowMessageBox(Control owner, string messsage, MessageBoxButtons messageBoxButtons);
        Icon ApplicationIcon { get; }
        DataGridViewColumn CreateGridViewColumn(PropertyDescriptor propertyDescriptor);
        void OnDataError(object sender, DataGridViewDataErrorEventArgs dataGridViewDataErrorEventArgs);
        bool DeleteEnabled { get; }
        void Delete();
        void Preview(Control owner, ViewInfo viewInfo);
    }
}
