/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2023 University of Washington - Seattle, WA
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

using System.Linq;
using System.Windows.Forms;
using CustomProgressCell;

namespace pwiz.Common.Controls
{
    public partial class MultiProgressControl : UserControl
    {
        public MultiProgressControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Add or update job info in the grid.
        /// </summary>
        public void Update(string rowName, int progress, string progressMessage, bool error = false)
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => Update(rowName, progress, progressMessage, error)));
                return;
            }

            var existingRow = progressGridView.Rows.Cast<DataGridViewRow>()
                .FirstOrDefault(row => row.Cells[NameColumn.Index].Value.Equals(rowName));
            if (existingRow == null)
                existingRow = progressGridView.Rows[progressGridView.Rows.Add(rowName, progress)];
            var progressCell = existingRow.Cells[ProgressColumn.Index] as DataGridViewProgressCell;
            if (progressCell != null)
            {
                progressCell.Value = progress;
                progressCell.Text = progressMessage;
                if (error)
                    progressCell.ErrorText = progressMessage;
            }
        }

        public int RowCount => progressGridView.RowCount;
    }
}
