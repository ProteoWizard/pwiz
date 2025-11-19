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
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
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
            // Normalize and guard against null enumerable
            var requiredFilesList = (requiredFiles ?? Enumerable.Empty<FileDownloadInfo>()).ToList();

            // Only display and download entries that actually have a download URL.
            // Some flows (e.g., MSFragger) populate the URL after a license/verification step;
            // those entries must be handled by the specialized flow and should not be shown here.
            var downloadableFiles = requiredFilesList.Where(f => f.DownloadUrl != null).ToList();

            // Loop until success or cancellation
            for (;;)
            {
                using (var dlg = CreateMessageDlg(parent, downloadableFiles, out var layout, out var defaultWidth, out var defaultHeight))
                {
                    dlg.Text = title;
                    dlg.ClientSize = new Size(defaultWidth, defaultHeight);
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    dlg.ShowInTaskbar = false;
                    dlg.MinimumSize = dlg.Size;
                    layout.Size = dlg.ClientSize;
                    layout.Height -= 35;

                    var result = parent == null ? dlg.ShowParentlessDialog() : dlg.ShowWithTimeout(parent, title);
                    if (result == DialogResult.No)
                        return result;
                }

                try
                {
                    using (var dlg = new LongWaitDlg())
                    {
                        var status = dlg.PerformWork(parent, 50, pm => SimpleFileDownloader.DownloadRequiredFiles(downloadableFiles, pm));
                        if (!status.IsCanceled)
                            return DialogResult.Yes;    // Success!
                    }
                }
                catch (Exception e)
                {
                    ExceptionUtil.DisplayOrReportException(parent, e);
                }
            }
        }

        private static MultiButtonMsgDlg CreateMessageDlg(Control parent, IList<FileDownloadInfo> requiredFiles,
            out TableLayoutPanel layout, out int defaultWidth, out int defaultHeight)
        {
            var ctlTextRepresentation = new StringBuilder();
            layout = new TableLayoutPanel
            {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                //AutoScroll = true,
                BackColor = SystemColors.Window
            };

            layout.RowCount = requiredFiles.Count + 3; // message in first row, then blank second row, and blank last row
            layout.ColumnCount = 2;
            foreach (ColumnStyle style in layout.ColumnStyles)
            {
                style.Width = 50;
                style.SizeType = SizeType.Percent;
            }

            bool multiple = requiredFiles.Count > 1;
            string downloadMessage = multiple ? AlertsResources.SimpleFileDownloaderDlg_Show_The_following_files_are_required__Do_you_want_to_download_them_ :
                AlertsResources.SimpleFileDownloaderDlg_Show_The_following_file_is_required__Do_you_want_to_download_it_;

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
            ctlTextRepresentation.AppendLine(downloadMessage);

            int row = 2;
            var gridLabels = new List<Label>();
            // group by URL when presenting to user, although different FileDownloadInfos could have the same URL but different ToolType (e.g. Crux)
            var distinctFiles = requiredFiles.GroupBy(f => f.DownloadUrl).Select(g => g.First());
            foreach (var downloadableFile in distinctFiles)
            {
                var name = new Label
                {
                    Text = downloadableFile.Filename,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.BottomRight,
                    AutoSize = true,
                    Margin = new Padding(6)
                };
                var url = new Label
                {
                    Text = downloadableFile.DownloadUrl.ToString(),
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
                ctlTextRepresentation.AppendFormat(@"{0}{1}{2}{3}", name.Text, '\t', url.Text, Environment.NewLine);
            }

            var activeScreen = parent == null ? Screen.PrimaryScreen : Screen.FromHandle(parent.Handle);
            defaultWidth = layout.GetColumnWidths().Sum() + 100;
            defaultHeight = Math.Min(3 * activeScreen.Bounds.Height / 4, layout.GetRowHeights().Sum() + 50);

            foreach (var label in gridLabels)
            {
                label.AutoSize = false;
                label.Width += 10;
            }

            var multiButtonMessageDlg = new MultiButtonMsgDlg(layout,
                AlertsResources.AlertDlg_GetDefaultButtonText__Yes, AlertsResources.AlertDlg_GetDefaultButtonText__No,
                false, ctlTextRepresentation.ToString());
            return multiButtonMessageDlg;
        }
    }
}