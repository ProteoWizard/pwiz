using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ZedGraph;

namespace pwiz.Skyline.EditUI
{
    /// <summary>
    /// Menu item which copies a metafile to the clipboard.
    /// TODO(nicksh): it would be nice if this also copied CF_BITMAP format to the clipboard,
    /// but I haven't been able to get that to work.
    /// </summary>
    public class CopyEmfToolStripMenuItem : ToolStripMenuItem
    {
        [DllImport("user32.dll")]
        static extern bool OpenClipboard(IntPtr hWndNewOwner);
        [DllImport("user32.dll")]
        static extern bool EmptyClipboard();
        [DllImport("user32.dll")]
        static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
        [DllImport("user32.dll")]
        static extern bool CloseClipboard();

        public CopyEmfToolStripMenuItem(ZedGraphControl zedGraphControl)
        {
            ZedGraphControl = zedGraphControl;
            Text = "Copy Metafile";
            Click += CopyEmfToolStripMenuItem_Click;
        }

        public ZedGraphControl ZedGraphControl { get; private set; }

        void CopyEmfToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Metafile mf = ZedGraphControl.MasterPane.GetMetafile();
            bool success = false;
			if (OpenClipboard(ZedGraphControl.Handle))
			{
				if (EmptyClipboard())
				{
				    success = true;
					SetClipboardData(14 /*CF_ENHMETAFILE*/, mf.GetHenhmetafile());
                    // TODO (nicksh): It would be nice if we also set the CF_BITMAP
					CloseClipboard();
				}
			}
            if (ZedGraphControl.IsShowCopyMessage)
            {
                if (success)
                {
                    MessageBox.Show(ZedGraphControl, "Metafile image copied to clipboard");
                }
                else
                {
                    MessageBox.Show(ZedGraphControl, "Unable to copy metafile image to the clipboard.");
                }
            }
        }

        /// <summary>
        /// Adds a new "copy metafile" menu item right below the existing "copy" command
        /// on the context menu.
        /// </summary>
        public static void AddToContextMenu(ZedGraphControl zedGraphControl, ContextMenuStrip contextMenuStrip)
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
            contextMenuStrip.Items.Insert(index, new CopyEmfToolStripMenuItem(zedGraphControl));
        }
	}
}
