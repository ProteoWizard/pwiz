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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace pwiz.Common.Controls
{
    /// <summary>
    /// Subclass of DataGridView which works around known bugs.
    /// </summary>
    public class CommonDataGridView : DataGridView
    {
        [DefaultValue(null)]
        public int? MaximumColumnCount { get; set; }

        private int _scaledDefaultRowHeight;

        public CommonDataGridView()
        {
            ScaleRowTemplateHeight();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            ScaleRowTemplateHeight();
        }

        /// <summary>
        /// WinForms derives the default row height from the font plus 9 pixels of padding that it
        /// does not scale with the display DPI, so rows get denser as the scale goes up. Scale the
        /// padding too, leaving alone a grid that set its own row height (issue #4599).
        /// </summary>
        private void ScaleRowTemplateHeight()
        {
            const int defaultPadding = 9;
            float factor = DeviceDpi / 96f;
            if (Math.Abs(factor - 1) < 0.01f)
                return;
            int fontHeight = Font.Height;
            if (RowTemplate.Height != fontHeight + defaultPadding && RowTemplate.Height != _scaledDefaultRowHeight)
                return;
            _scaledDefaultRowHeight = fontHeight + (int) Math.Round(defaultPadding * factor);
            RowTemplate.Height = _scaledDefaultRowHeight;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            // An exception is possible in "PerformLayoutPrivate" if ColumnHeadersHeightSizeMode is AutoSize
            // when the handle is created.
            RunWithSafeColumnHeaderHeightSizeMode(()=>base.OnHandleCreated(e));
        }

        /// <summary>
        /// Adds multiple columns to the <see cref="DataGridView.Columns"/> collection.
        /// If more than one column is being added, then turns off "DataGridViewColumnHeadersHeightSizeMode",
        /// because it takes ridiculously long to add a few thousand columns when that property is "AutoSize".
        /// </summary>
        /// <param name="columns">Columns to add</param>
        /// <returns>The number of columns successfully added</returns>
        public int AddColumns(IEnumerable<DataGridViewColumn> columns)
        {
            var allColumns = columns.ToArray();
            var columnsToAdd = allColumns;
            if (MaximumColumnCount.HasValue && MaximumColumnCount.Value < ColumnCount + allColumns.Length)
            {
                columnsToAdd = allColumns.Take(MaximumColumnCount.Value - ColumnCount).ToArray();
            }
            if (columnsToAdd.Length <= 2)
            {
                Columns.AddRange(columnsToAdd);
            }
            else
            {
                RunWithSafeColumnHeaderHeightSizeMode(()=>Columns.AddRange(columnsToAdd));
            }
            if (columnsToAdd.Length < allColumns.Length)
            {
                NotifyColumnLimitExceeded(allColumns.Length - columnsToAdd.Length);
            }
            return columnsToAdd.Length;
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

        /// <summary>
        /// Add an extra column to the end of the column set which shows the user than one or more columns
        /// were not able to be added.
        /// </summary>
        public virtual void NotifyColumnLimitExceeded(int columnsNotShown)
        {
            ColumnLimitExceededColumn columnLimitExceededColumn = null;
            if (ColumnCount > 0)
            {
                columnLimitExceededColumn = Columns[ColumnCount - 1] as ColumnLimitExceededColumn;
            }
            if (columnLimitExceededColumn == null)
            {
                columnLimitExceededColumn = new ColumnLimitExceededColumn(columnsNotShown);
                Columns.Add(columnLimitExceededColumn);
            }
            else
            {
                columnLimitExceededColumn.ColumnsNotShownCount += columnsNotShown;
            }
        }
    }
}
