using System;
using pwiz.Skyline.Properties;

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
    }
}
