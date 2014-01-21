/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using ZedGraph;

namespace pwiz.Topograph.ui.Controls
{
    public class ZedGraphControlEx : ZedGraphControl
    {
        public ZedGraphControlEx()
        {
            ContextMenuBuilder += ZedGraphControlExOnContextMenuBuilder;
        }

        void ZedGraphControlExOnContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, System.Drawing.Point mousePt, ContextMenuObjectState objState)
        {
            AddContextMenuItems(this, menuStrip);
        }

        public static void AddContextMenuItems(ZedGraphControl zedGraphControl, ContextMenuStrip contextMenuStrip)
        {
            int index = contextMenuStrip.Items.Count;
            for (int i = 0; i < contextMenuStrip.Items.Count; i++)
            {
                var item = contextMenuStrip.Items[i];
                if (item.Text == "Copy")
                {
                    index = i + 1;
                    break;
                }
            }
            contextMenuStrip.Items.Insert(index, new CopyGraphDataToolStripMenuItem(zedGraphControl));
            contextMenuStrip.Items.Insert(index, new CopyEmfToolStripMenuItem(zedGraphControl));
        }
    }
}
