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
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Layout;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Application-dependent class for persisting custom views, and displaying view-related UI.
    /// </summary>
    public interface IViewContext
    {
        IEnumerable<ViewGroup> ViewGroups { get; }
        ViewSpecList GetViewSpecList(ViewGroupId groupId);
        bool TryRenameView(ViewGroupId group, string oldName, string newName);
        void AddOrReplaceViews(ViewGroupId group, IEnumerable<ViewSpecLayout> viewSpecs);
        void DeleteViews(ViewGroupId groupId, IEnumerable<string> names);
        ViewGroup DefaultViewGroup { get; }
        ViewGroup FindGroup(ViewGroupId groupId);
        IRowSource GetRowSource(ViewInfo viewInfo);
        ViewInfo GetViewInfo(ViewGroup viewGroup, ViewSpec viewSpec);
        ViewInfo GetViewInfo(ViewName? viewName);
        RowSourceInfo GetRowSourceInfo(ViewSpec viewSpec);
        /// <summary>
        /// Returns true if this view is able to be displayed in this ViewContext.
        /// </summary>
        bool CanDisplayView(ViewSpec viewSpec);
        void Export(Control owner, BindingListSource bindingListSource);
        void CopyAll(Control owner, BindingListSource bindingListSource);
        ViewSpec NewView(Control owner, ViewGroup viewGroup);
        ViewSpec CustomizeView(Control owner, ViewSpec viewSpec, ViewGroup viewGroup);
        ViewLayoutList GetViewLayoutList(ViewName viewName);
        void SetViewLayoutList(ViewGroupId viewGroup, ViewLayoutList list);
        void ManageViews(Control owner);
        void ExportViews(Control owner, ViewSpecList views);
        void ExportViewsToFile(Control owner, ViewSpecList views, string fileName);
        void ImportViews(Control owner, ViewGroup group);
        void ImportViewsFromFile(Control owner, ViewGroup group, string fileName);
        void CopyViewsToGroup(Control owner, ViewGroup group, ViewSpecList viewSpecList);
        DialogResult ShowMessageBox(Control owner, string messsage, MessageBoxButtons messageBoxButtons);
        Icon ApplicationIcon { get; }
        DataGridViewColumn CreateGridViewColumn(PropertyDescriptor propertyDescriptor);
        void OnDataError(object sender, DataGridViewDataErrorEventArgs dataGridViewDataErrorEventArgs);
        bool DeleteEnabled { get; }
        void Delete();
        bool HasRowActions { get; }
        void RowActionsDropDownOpening(ToolStripItemCollection dropDownItems);
        void Preview(Control owner, ViewInfo viewInfo);
        Image[] GetImageList();
        int GetImageIndex(ViewSpec viewSpec);
        event Action ViewsChanged;
        DataSchema DataSchema { get; }
        IEnumerable<IUiModeInfo> AvailableUiModes { get; }

        void ToggleClustering(BindingListSource bindingListSource, bool turnClusteringOn);
    }
}
