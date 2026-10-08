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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Controls;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls
{
    public class DataGridViewEx : CommonDataGridView
    {
        private bool _columnWidthsScaled;

        /// <summary>
        /// Column widths set in the designer are 96-DPI pixel values that auto-scaling
        /// never touches: only a column left at the default width gets the DPI-dependent
        /// default. True (the default) scales every other fixed-width column once, when
        /// the handle is created. A grid that sizes its columns itself sets this false
        /// (issue #4599).
        /// </summary>
        public bool ScaleDesignerColumnWidths { get; set; } = true;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (_columnWidthsScaled || !ScaleDesignerColumnWidths)
                return;
            _columnWidthsScaled = true;
            var factor = DpiUtil.GetFactor(this);
            if (Math.Abs(factor - 1) < 0.01f)
                return;
            int defaultWidth = DpiUtil.Scale(this, 100);    // what an untouched column already has
            foreach (DataGridViewColumn column in Columns)
            {
                var autoSize = column.InheritedAutoSizeMode;
                if (autoSize != DataGridViewAutoSizeColumnMode.None && autoSize != DataGridViewAutoSizeColumnMode.NotSet)
                    continue;   // width follows the content or the grid, not the designer
                int designWidth = column.Width == defaultWidth ? 100 : column.Width;    // 96-DPI pixels
                if (column.Width != defaultWidth)
                    column.Width = DpiUtil.Scale(this, column.Width);
                // A header that fit on one line at 96 DPI can wrap at the scaled width, because
                // text does not scale linearly with the font; keep it on one line. A header that
                // already wrapped in the designer is left to wrap, so it takes no space from
                // the other columns. (GetPreferredWidth measures against the wrapped header
                // height, so measure the text directly.)
                var headerFont = ColumnHeadersDefaultCellStyle.Font ?? Font;
                int reserve = column.SortMode == DataGridViewColumnSortMode.NotSortable ? 16 : 36;    // sort glyph and padding
                int headerTextWidth = TextRenderer.MeasureText(column.HeaderText ?? string.Empty, headerFont,
                    System.Drawing.Size.Empty, TextFormatFlags.SingleLine).Width;
                bool fitAt96 = headerTextWidth / factor + reserve <= designWidth;
                int headerWidth = headerTextWidth + DpiUtil.Scale(this, reserve);
                if (fitAt96 && column.Width < headerWidth)
                    column.Width = headerWidth;
            }
        }

        public string GetCopyText()
        {
            return string.Join(TextUtil.SEPARATOR_TSV_STR, Columns.OfType<DataGridViewColumn>().Where(col => col.Visible).Select(col => col.HeaderText))
                + Environment.NewLine + TextUtil.LineSeparate(Rows 
                       .OfType<DataGridViewRow>().Select(row =>
                           string.Join(TextUtil.SEPARATOR_TSV_STR,
                               row.Cells.OfType<DataGridViewCell>().Where(cell => cell.Visible).Select(cell =>
                                   cell.Value == null ? string.Empty : cell.Value.ToString()))));
        }

        protected override bool ProcessDataGridViewKey(KeyEventArgs e)
        {
            try
            {
                if (DataGridViewKey != null)
                {
                    DataGridViewKey.Invoke(this, e);
                    if (e.Handled)
                    {
                        return true;
                    }
                }

                return base.ProcessDataGridViewKey(e);
            }
            catch (Exception exception)
            {
                ExceptionUtil.HandleProcessKeyException(this, exception, e.KeyData);
                return true;
            }
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (DataGridViewKey != null)
            {
                var keyEventArgs = new KeyEventArgs(keyData);
                DataGridViewKey.Invoke(this, keyEventArgs);
                if (keyEventArgs.Handled)
                {
                    return true;
                }
            }
            return base.ProcessDialogKey(keyData);
        }

        /// <summary>
        /// DataGridViews somehow manage to get access to keystrokes such as cursor
        /// keys before the textbox with the focus gets them.  In order to allow
        /// other people to process these keys, the DataGridViewKey event is exposed
        /// in this class.
        /// </summary>
        public event EventHandler<KeyEventArgs> DataGridViewKey;

        /// <summary>
        /// If this control is a child of the SkylineWindow (not a popup), then returns the
        /// SkylineWindow.  Otherwise returns null.
        /// </summary>
        protected SkylineWindow FindParentSkylineWindow()
        {
            for (Control control = this; control != null; control = control.Parent)
            {
                var skylineWindow = control as SkylineWindow;
                if (skylineWindow != null)
                {
                    return skylineWindow;
                }
            }
            return null;
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            var skylineWindow = FindParentSkylineWindow();
            if (skylineWindow != null)
            {
                skylineWindow.ClipboardControlGotFocus(this);
            }
            // Fix for Issue 85(nicksh): For some reason ContainerControl.UpdateFocusedControl can
            // get into an infinite loop sometimes.
            // Setting ActiveControl to this prevents the hang from happening, and seems like a safe
            // thing to do here.
            var form = FindForm();
            if (form != null)
            {
                form.ActiveControl = this;
            }
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            var skylineWindow = FindParentSkylineWindow();
            if (skylineWindow != null)
            {
                skylineWindow.ClipboardControlLostFocus(this);
            }
        }

        /// <summary>
        /// Testing method: Sends Ctrl-V to this control.
        /// </summary>
        public void SendPaste()
        {
            OnKeyDown(new KeyEventArgs(Keys.V | Keys.Control));
        }

        public void ClickCurrentCell()
        {
            OnCellContentClick(new DataGridViewCellEventArgs(CurrentCell.ColumnIndex, CurrentCell.RowIndex));
        }
    }
}
