using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.MSGraph;

namespace pwiz.Topograph.ui.Controls
{
    public class MSGraphControlEx : MSGraphControl
    {
        public MSGraphControlEx()
        {
            ContextMenuBuilder += MSGraphControlEx_ContextMenuBuilder;
        }

        void MSGraphControlEx_ContextMenuBuilder(ZedGraph.ZedGraphControl sender, System.Windows.Forms.ContextMenuStrip menuStrip, System.Drawing.Point mousePt, ZedGraph.ZedGraphControl.ContextMenuObjectState objState)
        {
            ZedGraphControlEx.AddContextMenuItems(sender, menuStrip);
        }
    }
}
