using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ZedGraph;

namespace pwiz.Topograph.ui.Controls
{
    public class ZedGraphControlEx : ZedGraphControl
    {
        public ZedGraphControlEx()
        {
            ContextMenuBuilder += ZedGraphControlEx_ContextMenuBuilder;
        }

        void ZedGraphControlEx_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, System.Drawing.Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
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
