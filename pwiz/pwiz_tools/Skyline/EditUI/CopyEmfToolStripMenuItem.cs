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
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ZedGraph;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.EditUI
{
    /// <summary>
    /// Menu item which copies a metafile to the clipboard.
    /// CONSIDER(nicksh): it would be nice if this also copied CF_BITMAP format to the clipboard,
    /// but I haven't been able to get that to work.
    /// </summary>
    public class CopyEmfToolStripMenuItem : ToolStripMenuItem
    {
        // Not L10N
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
            Text = Resources.CopyEmfToolStripMenuItem_CopyEmfToolStripMenuItem_Copy_Metafile;
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
                    // CONSIDER(nicksh): It would be nice if we also set the CF_BITMAP
                    CloseClipboard();
                }
            }
            if (zedGraphControl.IsShowCopyMessage)
            {
                MessageDlg.Show(zedGraphControl,
                                success
                                    ? Resources.CopyEmfToolStripMenuItem_CopyEmf_Metafile_image_copied_to_clipboard
                                    : Resources.CopyEmfToolStripMenuItem_CopyEmf_Unable_to_copy_metafile_image_to_the_clipboard);
            }
        }

        /// <summary>
        /// Adds a new "copy metafile" and "copy graph data" menu items right below the existing "copy" command
        /// on the context menu.
        /// </summary>
        public static void AddToContextMenu(ZedGraphControl zedGraphControl, ContextMenuStrip contextMenuStrip)
        {
            int index = contextMenuStrip.Items.Count;
            for (int i = 0; i < contextMenuStrip.Items.Count; i++)
            {
                var item = contextMenuStrip.Items[i];
                if (item.Text == Resources.CopyEmfToolStripMenuItem_AddToContextMenu_Copy)
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
