using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Properties;
using System;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Menus
{
    public partial class ContextMenuControl : SkylineControl
    {
        public ContextMenuControl(SkylineWindow skylineWindow) : base(skylineWindow)
        {
            InitializeComponent();
        }

        private ContextMenuControl()
        {
        }

        private void selectionContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowReplicateSelection = selectionContextMenuItem.Checked;
            SkylineWindow.UpdateSummaryGraphs();
        }

        private void synchronizeSummaryZoomingContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.SynchronizeSummaryZooming = synchronizeSummaryZoomingContextMenuItem.Checked;
            SkylineWindow.SynchronizeSummaryZooming();
        }

        private void peptideCvsContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowCVValues(peptideCvsContextMenuItem.Checked);
        }

        protected void AddTransitionContextMenu(ToolStrip menuStrip, int iInsert)
        {
            using var chromatogramContextMenu = new ChromatogramContextMenu(SkylineWindow);
            chromatogramContextMenu.AddTransitionContextMenu(menuStrip, iInsert);
        }

        protected void AddPeptideOrderContextMenu(ToolStrip menuStrip, int iInsert)
        {
            menuStrip.Items.Insert(iInsert, peptideOrderContextMenuItem);
            if (peptideOrderContextMenuItem.DropDownItems.Count == 0)
            {
                peptideOrderContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                {
                    peptideOrderDocumentContextMenuItem,
                    peptideOrderRTContextMenuItem,
                    peptideOrderAreaContextMenuItem
                });
            }
        }

        protected void AddScopeContextMenu(ToolStrip menuStrip, int iInsert)
        {
            menuStrip.Items.Insert(iInsert, scopeContextMenuItem);
            if (scopeContextMenuItem.DropDownItems.Count == 0)
            {
                scopeContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                {
                    documentScopeContextMenuItem,
                    proteinScopeContextMenuItem
                });
            }
        }

        protected int AddReplicatesContextMenu(ToolStrip menuStrip, int iInsert)
        {
            if (DocumentUI.Settings.HasResults &&
                DocumentUI.Settings.MeasuredResults.Chromatograms.Count > 1)
            {
                menuStrip.Items.Insert(iInsert++, replicatesRTContextMenuItem);
                if (replicatesRTContextMenuItem.DropDownItems.Count == 0)
                {
                    replicatesRTContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                        averageReplicatesContextMenuItem,
                        singleReplicateRTContextMenuItem,
                        bestReplicateRTContextMenuItem
                    });
                }
            }
            return iInsert;
        }

        private void peptideOrderContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            SummaryPeptideOrder peptideOrder = SummaryPeptideGraphPane.PeptideOrder;
            peptideOrderDocumentContextMenuItem.Checked = (peptideOrder == SummaryPeptideOrder.document);
            peptideOrderRTContextMenuItem.Checked = (peptideOrder == SummaryPeptideOrder.time);
            peptideOrderAreaContextMenuItem.Checked = (peptideOrder == SummaryPeptideOrder.area);
            peptideOrderMassErrorContextMenuItem.Checked = (peptideOrder == SummaryPeptideOrder.mass_error);
        }

        private void peptideOrderDocumentContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPeptideOrder(SummaryPeptideOrder.document);
        }

        private void peptideOrderRTContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPeptideOrder(SummaryPeptideOrder.time);
        }

        private void peptideOrderAreaContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPeptideOrder(SummaryPeptideOrder.area);
        }

        private void peptideOrderMassErrorContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPeptideOrder(SummaryPeptideOrder.mass_error);
        }

        private void scopeContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var areaScope = AreaGraphController.AreaScope;
            documentScopeContextMenuItem.Checked = (areaScope == AreaScope.document);
            proteinScopeContextMenuItem.Checked = (areaScope == AreaScope.protein);
        }

        private void documentScopeContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.AreaScopeTo(AreaScope.document);
        }

        private void proteinScopeContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.AreaScopeTo(AreaScope.protein);
        }

        private void replicatesRTContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            ReplicateDisplay replicate = RTLinearRegressionGraphPane.ShowReplicate;
            averageReplicatesContextMenuItem.Checked = (replicate == ReplicateDisplay.all);
            singleReplicateRTContextMenuItem.Checked = (replicate == ReplicateDisplay.single);
            bestReplicateRTContextMenuItem.Checked = (replicate == ReplicateDisplay.best);
        }

        private void averageReplicatesContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowAverageReplicates();
        }

        private void singleReplicateRTContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowSingleReplicate();
        }

        private void bestReplicateRTContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowRegressionReplicateEnum = ReplicateDisplay.best.ToString();
            Settings.Default.ShowPeptideCV = false;
            SkylineWindow.UpdateSummaryGraphs();
        }

        protected int AddReplicateOrderAndGroupByMenuItems(ToolStrip menuStrip, int iInsert)
        {
            ReplicateValue currentGroupBy = ReplicateValue.FromPersistedString(DocumentUI.Settings, SummaryReplicateGraphPane.GroupByReplicateAnnotation);
            var groupByValues = ReplicateValue.GetGroupableReplicateValues(DocumentUI).ToArray();

            var orderByReplicateAnnotationDef = groupByValues.FirstOrDefault(
                value => SummaryReplicateGraphPane.OrderByReplicateAnnotation == value.ToPersistedString());
            menuStrip.Items.Insert(iInsert++, replicateOrderContextMenuItem);
            replicateOrderContextMenuItem.DropDownItems.Clear();
            replicateOrderContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                replicateOrderDocumentContextMenuItem,
                replicateOrderAcqTimeContextMenuItem
            });
            replicateOrderDocumentContextMenuItem.Checked
                = null == orderByReplicateAnnotationDef &&
                  SummaryReplicateOrder.document == SummaryReplicateGraphPane.ReplicateOrder;
            replicateOrderAcqTimeContextMenuItem.Checked
                = null == orderByReplicateAnnotationDef &&
                  SummaryReplicateOrder.time == SummaryReplicateGraphPane.ReplicateOrder;
            foreach (var replicateValue in groupByValues)
            {
                replicateOrderContextMenuItem.DropDownItems.Add(OrderByReplicateAnnotationMenuItem(
                    replicateValue, SummaryReplicateGraphPane.OrderByReplicateAnnotation));
            }

            if (groupByValues.Length > 0)
            {
                menuStrip.Items.Insert(iInsert++, groupReplicatesByContextMenuItem);
                groupReplicatesByContextMenuItem.DropDownItems.Clear();
                groupReplicatesByContextMenuItem.DropDownItems.Add(groupByReplicateContextMenuItem);
                groupByReplicateContextMenuItem.Checked = currentGroupBy == null;
                foreach (var replicateValue in groupByValues)
                {
                    groupReplicatesByContextMenuItem.DropDownItems
                        .Add(GroupByReplicateAnnotationMenuItem(replicateValue, Equals(replicateValue, currentGroupBy)));
                }
            }
            return iInsert;
        }

        private ToolStripMenuItem GroupByReplicateAnnotationMenuItem(ReplicateValue replicateValue, bool isChecked)
        {
            return new ToolStripMenuItem(replicateValue.Title, null,
                (sender, eventArgs) => SkylineWindow.GroupByReplicateValue(replicateValue))
            {
                Checked = isChecked
            };
        }

        private ToolStripMenuItem OrderByReplicateAnnotationMenuItem(ReplicateValue replicateValue, string currentOrderBy)
        {
            return new ToolStripMenuItem(replicateValue.Title, null,
                                         (sender, eventArgs) => SkylineWindow.OrderByReplicateAnnotation(replicateValue))
                {
                    Checked = replicateValue.ToPersistedString() == currentOrderBy
                };
        }

        private void replicateOrderDocumentContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowReplicateOrder(SummaryReplicateOrder.document);
        }

        private void replicateOrderAcqTimeContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowReplicateOrder(SummaryReplicateOrder.time);
        }

        private void groupByReplicateContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.GroupByReplicateValue(null);
        }
    }
}
