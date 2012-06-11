using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ZedGraph;

namespace pwiz.Topograph.ui.Controls
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
            CopyEmf(ZedGraphControl);
        }

        public static void CopyEmf(ZedGraphControl zedGraphControl)
        {
            Metafile mf = zedGraphControl.MasterPane.GetMetafile();
            bool success = false;
            if (OpenClipboard(zedGraphControl.Handle))
            {
                if (EmptyClipboard())
                {
                    success = true;
                    SetClipboardData(14 /*CF_ENHMETAFILE*/, mf.GetHenhmetafile());
                    // TODO (nicksh): It would be nice if we also set the CF_BITMAP
                    CloseClipboard();
                }
            }
            if (zedGraphControl.IsShowCopyMessage)
            {
                if (success)
                {
                    MessageBox.Show(zedGraphControl, "Metafile image copied to clipboard");
                }
                else
                {
                    MessageBox.Show(zedGraphControl, "Unable to copy metafile image to the clipboard.");
                }
            }
        }
	}
}
