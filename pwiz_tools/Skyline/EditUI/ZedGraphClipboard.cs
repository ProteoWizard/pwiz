/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.EditUI
{
    public static class ZedGraphClipboard
    {
        /// <summary>
        /// Replace the "Copy" menu item on the context menu with one that is implemented by Skyline.
        /// Also, below the Copy item, add menu items for "Copy Metafile" and "Copy Data".
        /// </summary>
        public static void AddToContextMenu(ZedGraphControl zedGraphControl, ContextMenuStrip contextMenuStrip)
        {
            var itemsToInsert = CreateCopyMenuItems(zedGraphControl);
            for (int i = 0; i < contextMenuStrip.Items.Count; i++)
            {
                var item = contextMenuStrip.Items[i];
                // We recognize the original Copy menu item by its name and tag, which are set in when 
                // the menu item is created in "ZedGraphControl.contextMenuStrip1_Opening".
                if (@"copy".Equals(item.Tag) && item.Name == @"copy")
                {
                    InsertMenuItems(contextMenuStrip.Items, i, itemsToInsert);
                    contextMenuStrip.Items.Remove(item);
                    return;
                }
            }
            // Did not find "Copy" menu item to replace (should not happen).
            // Add all of the copy menu items to the end of the context menu
            contextMenuStrip.Items.AddRange(itemsToInsert.ToArray());
        }

        private static IEnumerable<ToolStripItem> CreateCopyMenuItems(ZedGraphControl zedGraphControl)
        {
            yield return new ToolStripMenuItem(Resources.ZedGraphClipboard_CreateCopyMenuItems_Copy, null, (sender, args) => CopyZedGraphImage(zedGraphControl));
            yield return new CopyEmfToolStripMenuItem(zedGraphControl);
            yield return new CopyGraphDataToolStripMenuItem(zedGraphControl);
        }

        private static void InsertMenuItems(ToolStripItemCollection toolStripItemCollection, int position,
            IEnumerable<ToolStripItem> menuItems)
        {
            foreach (var item in menuItems)
            {
                toolStripItemCollection.Insert(position, item);
                position++;
            }
        }

        private static void CopyZedGraphImage(ZedGraphControl zedGraphControl)
        {
            var image = zedGraphControl.MasterPane.GetImage(zedGraphControl.MasterPane.IsAntiAlias);
            var dataObject = new DataObject(DataFormats.Bitmap, image);
            ClipboardHelper.SetClipboardData(zedGraphControl, dataObject, true);
        }
    }
}
