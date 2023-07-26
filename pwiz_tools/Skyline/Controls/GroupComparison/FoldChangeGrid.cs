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
using System.Drawing;
using DigitalRune.Windows.Docking;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.GroupComparison
{
    public partial class FoldChangeGrid : FoldChangeForm, IDataboundGridForm
    {
        public FoldChangeGrid()
        {
            InitializeComponent();
        }

        public override string GetTitle(string groupComparisonName)
        {
            return base.GetTitle(groupComparisonName) + ':' + GroupComparisonStrings.FoldChangeGrid_GetTitle_Grid;
        }

        public ViewName? ViewToRestore { get; set; }

        protected override string GetPersistentString()
        {
            var persistentString = PersistentString.Parse(base.GetPersistentString());
            var viewName = DataboundGridControl.GetViewName();
            if (viewName.HasValue)
            {
                persistentString = persistentString.Append(viewName.ToString());
            }

            return persistentString.ToString();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (null != FoldChangeBindingSource)
            {
                databoundGridControl.BindingListSource = FoldChangeBindingSource.GetBindingListSource();
                toolStripButtonChangeSettings.Visible =
                    !string.IsNullOrEmpty(FoldChangeBindingSource.GroupComparisonModel.GroupComparisonName);
                FoldChangeBindingSource.ViewContext.BoundDataGridView = DataboundGridControl.DataGridView;
                if (ViewToRestore.HasValue)
                {
                    DataboundGridControl.ChooseView(ViewToRestore.Value);
                }
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (null != FoldChangeBindingSource)
            {
                FoldChangeBindingSource.ViewContext.BoundDataGridView = null;
                databoundGridControl.BindingListSource = null;
            }
            base.OnHandleDestroyed(e);
        }

        public static FoldChangeGrid ShowFoldChangeGrid(DockPanel dockPanel, Rectangle rcFloating, IDocumentUIContainer documentContainer,
            string groupComparisonName)
        {
            var grid = FindForm<FoldChangeGrid>(documentContainer, groupComparisonName);
            if (grid != null)
            {
                grid.Activate();
                return grid;
            }
            
            grid = new FoldChangeGrid();
            grid.SetBindingSource(FindOrCreateBindingSource(documentContainer, groupComparisonName));
            grid.Show(dockPanel, rcFloating);
            return grid;
        }

        private void toolButtonShowGraph_Click(object sender, EventArgs e)
        {
            ShowGraph();
        }

        public void ShowGraph()
        {
            ShowFoldChangeForm<FoldChangeBarGraph>();
        }

        private void toolButtonVolcano_Click(object sender, EventArgs e)
        {
            ShowVolcanoPlot();
        }

        public void ShowVolcanoPlot()
        {
            ShowFoldChangeForm<FoldChangeVolcanoPlot>();
        }

        private void toolStripButtonChangeSettings_Click(object sender, EventArgs e)
        {
            ShowChangeSettings();
        }

        public DataboundGridControl DataboundGridControl { get { return databoundGridControl; } }

        public DataGridId DataGridId
        {
            get
            {
                return new DataGridId(DataGridType.GROUP_COMPARISON, GroupComparisonName);
            }
        }

        DataboundGridControl IDataboundGridForm.GetDataboundGridControl()
        {
            return DataboundGridControl;
        }

        public override bool IsComplete
        {
            get
            {
                return base.IsComplete && DataboundGridControl.IsComplete;
            }
        }
    }
}
