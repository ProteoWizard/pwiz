/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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

namespace SkylineTester
{
    /// <summary>
    /// Subclass of DataGridView which works around known bugs.
    /// </summary>
    public class SafeDataGridView : DataGridView
    {
        protected override void OnHandleCreated(EventArgs e)
        {
            // An exception is possible in "PerformLayoutPrivate" if ColumnHeadersHeightSizeMode is AutoSize
            // when the handle is created.
            RunWithSafeColumnHeaderHeightSizeMode(() => base.OnHandleCreated(e));
        }

        protected void RunWithSafeColumnHeaderHeightSizeMode(Action action)
        {
            DataGridViewColumnHeadersHeightSizeMode? columnHeadersHeightSizeModeOld = null;
            if (ColumnHeadersHeightSizeMode == DataGridViewColumnHeadersHeightSizeMode.AutoSize)
            {
                columnHeadersHeightSizeModeOld = ColumnHeadersHeightSizeMode;
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            }
            try
            {
                action();
            }
            finally
            {
                if (columnHeadersHeightSizeModeOld.HasValue)
                {
                    ColumnHeadersHeightSizeMode = columnHeadersHeightSizeModeOld.Value;
                }
            }
        }

    }
}
