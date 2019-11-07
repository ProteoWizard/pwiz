//
// $Id$
//
//
// Original author: Jay Holman <jay.holman .@. vanderbilt.edu>
//
// Copyright 2011 Vanderbilt University - Nashville, TN 37232
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


using System;
using System.Windows.Forms;
using System.Drawing;
using System.ComponentModel;


namespace CustomProgressCell
{
    /// <summary>
    /// Must be built 32-bit to show in ProgressForm in design mode
    /// </summary>
    public sealed class DataGridViewProgressColumn : DataGridViewColumn
    {
        public DataGridViewProgressColumn()
        {
            CellTemplate = new DataGridViewProgressCell();
        }
    }
}

namespace CustomProgressCell
{
    sealed class DataGridViewProgressCell : DataGridViewTextBoxCell
    {
        #region Public data accessors
        /// <summary>
        /// Gets or sets the progress bar's Maximum property.
        /// </summary>
        public int Maximum
        {
            get { return _progressBar.Maximum; }
            set { _progressBar.Maximum = value; startAnimation(); }
        }

        /// <summary>
        /// Gets or sets the progress bar's Minimum property.
        /// </summary>
        public int Minimum
        {
            get { return _progressBar.Minimum; }
            set { _progressBar.Minimum = value; startAnimation(); }
        }

        /// <summary>
        /// Gets or sets the text to display on top of the progress bar.
        /// </summary>
        public string Text
        {
            get { return _text; }
            set { _text = value; refresh(); }
        }

        /// <summary>
        /// Gets or sets the progress bar's drawing style.
        /// </summary>
        public ProgressBarStyle ProgressBarStyle
        {
            get { return _progressBar.Style; }
            set { _progressBar.Style = value; startAnimation(); }
        }
        #endregion

        /// <summary>
        /// Use these keywords in the Text property to their respective values in the text.
        /// </summary>
        public abstract class MessageSpecialValue
        {
            public const string Minimum = "<<Minimum>>";
            public const string Maximum = "<<Maximum>>";
            public const string CurrentValue = "<<CurrentValue>>";
        }

        #region Private member variables
        ProgressBar _progressBar;
        Timer _animationStepTimer;
        Timer _animationStopTimer;
        string _text;
        #endregion

        public DataGridViewProgressCell()
        {
            _progressBar = new ProgressBar()
            {
                Minimum = 0,
                Maximum = 100,
                Style = ProgressBarStyle.Continuous
            };

            _text = String.Format("{0} of {1}", MessageSpecialValue.CurrentValue, MessageSpecialValue.Maximum);

            ValueType = typeof(int);

            // repaint every 25 milliseconds while progress is active
            _animationStepTimer = new Timer { Interval = 25, Enabled = true };

            // stop repainting 3 seconds after progress becomes inactive
            _animationStopTimer = new Timer { Interval = 3000, Enabled = false };

            _animationStepTimer.Tick += (x, y) => { stopAnimation(); refresh(); };
            _animationStopTimer.Tick += (x, y) => { _animationStepTimer.Stop(); _animationStopTimer.Stop(); };
        }

        protected override object GetValue (int rowIndex)
        {
            return _progressBar.Value;
        }

        protected override bool SetValue (int rowIndex, object value)
        {
            if (value is int)
            {
                _progressBar.Value = (int) value;
                refresh();
                return true;
            }
            return false;
        }

        protected override void Paint (Graphics g, Rectangle clipBounds, Rectangle cellBounds, int rowIndex, DataGridViewElementStates cellState, object value, object formattedValue, string errorText, DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
        {
            ReadOnly = true;

            // Draw the cell border
            base.Paint(g, clipBounds, cellBounds,
                       rowIndex, cellState, value, formattedValue, errorText,
                       cellStyle, advancedBorderStyle, DataGridViewPaintParts.Border);

            try
            {
                // Draw the ProgressBar to an in-memory bitmap
                Bitmap bmp = new Bitmap(cellBounds.Width, cellBounds.Height);
                Rectangle bmpBounds = new Rectangle(0, 0, cellBounds.Width, cellBounds.Height);
                _progressBar.Size = cellBounds.Size;
                _progressBar.DrawToBitmap(bmp, bmpBounds);

                // Draw the bitmap on the cell
                g.DrawImage(bmp, cellBounds);

                // Replace special value placeholders
                var editedMessage = _text.Replace(MessageSpecialValue.CurrentValue, Value.ToString())
                                         .Replace(MessageSpecialValue.Maximum, Maximum.ToString())
                                         .Replace(MessageSpecialValue.Minimum, Minimum.ToString());

                // Write text over bar
                base.Paint(g, clipBounds, cellBounds,
                           rowIndex, cellState, value, editedMessage, errorText,
                           cellStyle, advancedBorderStyle, DataGridViewPaintParts.ContentForeground);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Row probably couldn't be accessed
            }
        }

        private void refresh ()
        {
            if (DataGridView != null) DataGridView.InvalidateCell(this);
        }

        private void startAnimation ()
        {
            if (_progressBar.Style == ProgressBarStyle.Marquee ||
                (_progressBar.Value > _progressBar.Minimum && _progressBar.Value < _progressBar.Maximum))
                _animationStepTimer.Start();
        }

        private void stopAnimation ()
        {
            if (_progressBar.Style != ProgressBarStyle.Marquee &&
                (_progressBar.Value == _progressBar.Minimum || _progressBar.Value == _progressBar.Maximum))
                _animationStopTimer.Start();
        }
    }
}