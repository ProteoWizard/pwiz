/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Windows.Forms;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// Opens a combo box in the current cell of a grid, and prevents the combo box from closing until Dispose'd.
    /// </summary>
    public class ShowGridComboBox : IDisposable
    {
        private DataGridView _dataGridView;
        public ShowGridComboBox(DataGridView dataGridView)
        {
            _dataGridView = dataGridView;
            _dataGridView.Invoke(new Action(() =>
            {
                _dataGridView.BeginEdit(false);
                var comboBox = (ComboBox)dataGridView.EditingControl;
                comboBox.DropDownClosed += PreventComboClosing;
                comboBox.DroppedDown = true;
            }));
        }

        public void Dispose()
        {
            _dataGridView.Invoke(new Action(() =>
            {
                var comboBox = (ComboBox)_dataGridView.EditingControl;
                comboBox.DropDownClosed -= PreventComboClosing;
            }));
        }

        private static void PreventComboClosing(object sender, EventArgs args)
        {
            var comboBox = (ComboBox)sender;
            if (!comboBox.DroppedDown)
            {
                comboBox.DroppedDown = true;
            }
        }
    }
}
