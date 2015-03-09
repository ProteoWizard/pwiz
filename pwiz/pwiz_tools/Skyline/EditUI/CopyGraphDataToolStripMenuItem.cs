/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Runtime.InteropServices;
using System.Windows.Forms;
using pwiz.Common.Graph;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.EditUI
{
    /// <summary>
    /// Menu item to copy the data from a ZedGraph to the clipboard as tab separated values
    /// </summary>
    public class CopyGraphDataToolStripMenuItem : ToolStripMenuItem
    {
        public CopyGraphDataToolStripMenuItem(ZedGraphControl zedGraphControl)
        {
            ZedGraphControl = zedGraphControl;
            Text = Resources.CopyGraphDataToolStripMenuItem_CopyGraphDataToolStripMenuItem_Copy_Data;
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
            try
            {
                ClipboardEx.Clear();
                ClipboardEx.SetText(graphData.ToString());
            }
            catch (ExternalException)
            {
                MessageBox.Show(ClipboardHelper.GetOpenClipboardMessage(Resources.CopyGraphDataToolStripMenuItem_CopyGraphData_Failed_setting_data_to_clipboard), Program.Name);
            }
        }
    }
}
