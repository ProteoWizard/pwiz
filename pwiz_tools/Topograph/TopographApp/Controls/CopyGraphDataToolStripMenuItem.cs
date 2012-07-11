using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.Graph;
using pwiz.MSGraph;
using pwiz.Topograph.Util;
using ZedGraph;

namespace pwiz.Topograph.ui.Controls
{
    /// <summary>
    /// Menu item to copy the data from a ZedGraph to the clipboard as tab separated values
    /// </summary>
    public class CopyGraphDataToolStripMenuItem : ToolStripMenuItem
    {
        public CopyGraphDataToolStripMenuItem(ZedGraphControl zedGraphControl)
        {
            ZedGraphControl = zedGraphControl;
            Text = "Copy Data";
            Click += CopyGraphDataToolStripMenuItem_Click;
        }
        
        public ZedGraphControl ZedGraphControl { get; private set; }

        void CopyGraphDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyGraphData(ZedGraphControl);
        }

        /// <summary>
        /// Copy the data from the curves in the ZedGraphControl to the clipboard. 
        /// </summary>
        public static void CopyGraphData(ZedGraphControl zedGraphControl)
        {
            var graphData = GraphData.GetGraphData(zedGraphControl.MasterPane);
            if (graphData.Panes.Count == 0)
            {
                return;
            }
            var allText = graphData.ToString();
            bool success = false;
            try
            {
                Clipboard.Clear();
                Clipboard.SetText(allText);
                success = true;
            }
            catch (Exception e)
            {
                ErrorHandler.LogException("Copy Data", "Error copying data to clipboard", e);
            }
            if (zedGraphControl.IsShowCopyMessage)
            {
                if (success)
                {
                    MessageBox.Show(zedGraphControl, "Data copied to clipboard");
                }
                else
                {
                    MessageBox.Show(zedGraphControl, "Unable to copy data to the clipboard.");
                }
            }
        }
    }
}
