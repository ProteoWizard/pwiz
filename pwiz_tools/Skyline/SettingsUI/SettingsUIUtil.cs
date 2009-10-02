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
using System.Windows.Forms;

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

        public delegate bool ValidateCellValues(string[] values);

        public static void DoPaste(this DataGridView grid, IWin32Window parent, ValidateCellValues validate)
        {
            grid.SuspendLayout();

            // Remove everything, and paste new contents
            grid.Rows.Clear();

            TextReader reader = new StringReader(Clipboard.GetText());
            int lineNum = 0;
            String line;
            while ((line = reader.ReadLine()) != null)
            {
                lineNum++;
                String[] columns = line.Split('\t');
                if (columns.Length > grid.ColumnCount)
                {
                    string message = string.Format("Incorrect number of columns ({0}) found at line {1}.",
                                                   columns.Length, lineNum);
                    MessageBox.Show(parent, message, Program.Name);
                    break;
                }

                for (int i = 0; i < columns.Length; i++)
                    columns[i] = columns[i].Trim();

                if (!validate(columns))
                    break;

                grid.Rows.Add(columns);
            }

            grid.ResumeLayout();            
        }

        public static void DoDelete(this DataGridView grid)
        {
            grid.SuspendLayout();

            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.SelectedCells())
                    grid.Rows.Remove(row);
            }

            grid.ResumeLayout();            
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
