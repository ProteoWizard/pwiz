/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    public static class SimpleFileDownloaderDlg
    {

        /// <summary>
        /// Shows a simple dialog with a table/grid of labels.
        /// </summary>
        public static DialogResult Show(Control parent, string title, IEnumerable<FileDownloadInfo> requiredFiles)
        {
            var layout = new TableLayoutPanel
            {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                //AutoScroll = true,
                BackColor = SystemColors.Window
            };
            var requiredFilesList = requiredFiles.ToList();
            layout.RowCount = requiredFilesList.Count + 3; // message in first row, then blank second row, and blank last row
            layout.ColumnCount = 2;
            foreach (ColumnStyle style in layout.ColumnStyles)
            {
                style.Width = 50;
                style.SizeType = SizeType.Percent;
            }

            bool multiple = requiredFilesList.Count > 1;
            string downloadMessage = multiple ? Resources.SimpleFileDownloaderDlg_Show_The_following_files_are_required__Do_you_want_to_download_them_ :
                Resources.SimpleFileDownloaderDlg_Show_The_following_file_is_required__Do_you_want_to_download_it_;

            var downloadMessageLabel = new Label
            {
                Text = downloadMessage,
                Dock = DockStyle.Fill,
                AutoSize = true,
                TextAlign = ContentAlignment.TopLeft,
                Margin = new Padding(6),
            };
            layout.Controls.Add(downloadMessageLabel, 0, 0);
            layout.SetColumnSpan(downloadMessageLabel, 2);

            int row = 2;
            var gridLabels = new List<Label>();
            foreach (var requiredFile in requiredFilesList)
            {
                var name = new Label
                {
                    Text = requiredFile.Filename,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.BottomRight,
                    AutoSize = true,
                    Margin = new Padding(6)
                };
                var url = new Label
                {
                    Text = requiredFile.DownloadUrl.ToString(),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.BottomLeft,
                    AutoSize = true,
                    Margin = new Padding(6)
                };

                layout.Controls.Add(name, 0, row);
                layout.Controls.Add(url, 1, row);
                gridLabels.Add(name);
                gridLabels.Add(url);
                ++row;
            }

            var activeScreen = parent == null ? Screen.PrimaryScreen : Screen.FromHandle(parent.Handle);
            int defaultWidth = layout.GetColumnWidths().Sum() + 100;
            int defaultHeight = Math.Min(3 * activeScreen.Bounds.Height / 4, layout.GetRowHeights().Sum() + 50);

            foreach (var label in gridLabels)
            {
                label.AutoSize = false;
                label.Width += 10;
            }

            using (var dlg = new MultiButtonMsgDlg(layout, Resources.AlertDlg_GetDefaultButtonText__Yes, Resources.AlertDlg_GetDefaultButtonText__No, false)
            {
                Text = title,
                ClientSize = new Size(defaultWidth, defaultHeight),
                StartPosition = FormStartPosition.CenterParent,
                ShowInTaskbar = false
            })
            {
                dlg.MinimumSize = dlg.Size;
                layout.Size = dlg.ClientSize;
                layout.Height -= 35;

                var result = parent == null ? dlg.ShowParentlessDialog() : dlg.ShowWithTimeout(parent, title);
                if (result == DialogResult.No)
                    return result;
            }

            using (var dlg = new LongWaitDlg { Message = Resources.SimpleFileDownloaderDlg_Show_Downloading_required_files___, ProgressValue = 0 })
            {
                dlg.PerformWork(parent, 50, () => SimpleFileDownloader.DownloadRequiredFiles(requiredFilesList, dlg));
            }
            return DialogResult.Yes;
        }
    }
}