/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI
{
    public static class SettingsUIUtil
    {
        public static void FocusFirstTabStop(this TabControl tabControl)
        {
            Control.ControlCollection controls = tabControl.SelectedTab.Controls;
            for (int i = controls.Count - 1; i >= 0; i--)
            {
                if (controls[i].TabStop)
                {
                    controls[i].Focus();
                    break;
                }
            }            
        }

        public delegate bool ValidateCellValues(string[] values, IWin32Window parent, int lineNumber);

        public static bool DoPaste(this DataGridView grid, IWin32Window parent, ValidateCellValues validate)
        {
            string textClip = GetClipBoardText(parent);
            if (string.IsNullOrEmpty(textClip) || !grid.EndEdit())
                return false;

            grid.SuspendLayout();
            // Remove everything, and paste new contents
            grid.Rows.Clear();

            bool result = DoPasteText(parent, textClip, grid, validate,
                (values, lineNum) => grid.Rows.Add(values.Cast<object>().ToArray()));

            grid.ResumeLayout();
            return result;
        }

        public static bool DoPaste(this DataGridView grid, IWin32Window parent, ValidateCellValues validate, Action<string[]> addRow)
        {
            return grid.DoPaste(parent, validate, (s, i) => addRow(s));
        }

        private static bool DoPaste(this DataGridView grid, IWin32Window parent, ValidateCellValues validate, Action<string[], int> addRow)
        {
            string textClip = GetClipBoardText(parent);
            if (string.IsNullOrEmpty(textClip) || !grid.EndEdit())
                return false;

            return DoPasteText(parent, textClip, grid, validate, addRow);
        }

        private static string GetClipBoardText(IWin32Window parent)
        {
            try
            {
                return ClipboardEx.GetText();
            }
            catch (ExternalException)
            {
                MessageDlg.Show(parent, ClipboardHelper.GetOpenClipboardMessage(
                    Resources.SettingsUIUtil_GetClipBoardText_Failed_getting_data_from_the_clipboard));
                return null;
            }
        }

        private static bool DoPasteText(IWin32Window parent, string textClip, DataGridView grid, ValidateCellValues validate, Action<string[], int> addRow)
        {
            TextReader reader = new StringReader(textClip);

            int columnCount = grid.Columns.Cast<DataGridViewColumn>().Count(column => column.Visible);

            int lineNum = 0;
            String line;
            while ((line = reader.ReadLine()) != null)
            {
                lineNum++;
                String[] columns = line.Split(TextUtil.SEPARATOR_TSV);
                if (columns.Length == 0)
                    continue;
                if (columns.Length > columnCount)
                {
                    string message = string.Format(Resources.SettingsUIUtil_DoPasteText_Incorrect_number_of_columns__0__found_on_line__1__,
                                                   columns.Length, lineNum);
                    MessageDlg.Show(parent, message);
                    return false;
                }

                for (int i = 0; i < columns.Length; i++)
                    columns[i] = columns[i].Trim();

                if (!validate(columns, parent, lineNum))
                    return false;

                addRow(columns, lineNum);
            }

            return true;
        }

        public static bool DoDelete(this DataGridView grid)
        {
            if (!grid.EndEdit())
                return false;

            grid.SuspendLayout();

            foreach (DataGridViewRow row in grid.Rows)
            {
                if (!row.IsNewRow && row.SelectedCells())
                    grid.Rows.Remove(row);
            }

            grid.ResumeLayout();
            return true;
        }

        public static bool SelectedCells(this DataGridViewRow row)
        {
            foreach (DataGridViewCell cell in row.Cells)
            {
                if (!cell.Selected)
                    return false;
            }

            return true;
        }
    }
}
