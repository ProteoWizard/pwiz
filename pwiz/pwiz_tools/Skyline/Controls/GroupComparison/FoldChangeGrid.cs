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
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.GroupComparison
{
    public partial class FoldChangeGrid : FoldChangeForm
    {
        public FoldChangeGrid()
        {
            InitializeComponent();
        }

        public override string GetTitle(string groupComparisonName)
        {
            return base.GetTitle(groupComparisonName) + ':' + GroupComparisonStrings.FoldChangeGrid_GetTitle_Grid;
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
                var skylineWindow = FoldChangeBindingSource.GroupComparisonModel.DocumentContainer as SkylineWindow;
                if (null != skylineWindow)
                {
                    DataGridViewPasteHandler.Attach(skylineWindow, DataboundGridControl.DataGridView);
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

        public static FoldChangeGrid ShowFoldChangeGrid(DockPanel dockPanel, Rectangle rcFloating, IDocumentContainer documentContainer,
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
            IEnumerable<FoldChangeBarGraph> barGraphs;
            if (null != DockPanel)
            {
                barGraphs = DockPanel.Contents.OfType<FoldChangeBarGraph>();
            }
            else
            {
                barGraphs = Application.OpenForms.OfType<FoldChangeBarGraph>();
            }
            foreach (var form in barGraphs)
            {
                if (SameBindingSource(form)) 
                {
                    form.Activate();
                    return;
                }
            }
            var graph = new FoldChangeBarGraph();
            graph.SetBindingSource(FoldChangeBindingSource);
            if (null != Pane)
            {
                graph.Show(Pane, null);
            }
            else
            {
                graph.Show(Owner);
            }
        }

        private void toolStripButtonChangeSettings_Click(object sender, EventArgs e)
        {
            ShowChangeSettings();
        }

        public void ShowChangeSettings()
        {
            foreach (var form in Application.OpenForms.OfType<GroupComparisonSettingsForm>())
            {
                if (ReferenceEquals(form.GroupComparisonModel, FoldChangeBindingSource.GroupComparisonModel))
                {
                    form.Activate();
                    return;
                }
            }
            var foldChangeSettings = new GroupComparisonSettingsForm(FoldChangeBindingSource);
            foldChangeSettings.Show(this);
        }

        public DataboundGridControl DataboundGridControl { get { return databoundGridControl; } }
    }
}
