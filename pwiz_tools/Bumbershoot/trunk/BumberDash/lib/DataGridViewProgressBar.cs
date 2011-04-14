using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.ComponentModel;


namespace CustomProgressCell
{
    public sealed class DataGridViewProgressColumn : DataGridViewImageColumn
    {
        public DataGridViewProgressColumn()
        {
            CellTemplate = new DataGridViewProgressCell();
        }
    }
}

namespace CustomProgressCell
{
    sealed class DataGridViewProgressCell : DataGridViewImageCell
    {
        #region Public data accessors

        public int MaxValue
        {
            get { return _maxValue; }
            set
            {
                _maxValue = value;
                DataGridView.InvalidateCell(this);
            }
        }

        public int MinValue
        {
            get { return _minValue; }
            set
            {
                _minValue = value;
                DataGridView.InvalidateCell(this);
            }
        }

        public string Message
        {
            get { return _message; }
            set
            {
                _message = value;
                DataGridView.InvalidateCell(this);
            }
        }

        public bool MarqueeMode
        {
            get { return _marqueeMode; }
            set
            {
                _marqueeMode = value;
                DataGridView.InvalidateCell(this);
            }
        }

        #endregion

        private int _maxValue;
        private int _minValue;
        private string _message;
        private bool _marqueeMode; //internal representation of whether marquee is on or off
        private int _marqueeIndex; //Determines where in the bar the marquee has gotten to
        private int _lastCheckedValue; //helps keep marquee speed mostly consistant

        /// <summary>
        /// Use these in the Message property to show other properties of the cell in the text
        /// </summary>
        public static class MessageSpecialValue
        {
            public const string MinValue = "<<MinValue>>";
            public const string MaxValue = "<<MaxValue>>";
            public const string CurrentValue = "<<CurrentValue>>";
            public const string Percentage = "<<Percentage>>";
        }

        private Timer _animationTimer;

        // Used to make custom cell consistent with a DataGridViewImageCell
        static Image emptyImage;
        static DataGridViewProgressCell()
        {
            emptyImage = new Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        }
        public DataGridViewProgressCell()
        {
            _minValue = 0;
            _maxValue = 100;

            _message = string.Empty;

            ValueType = typeof(int);
            _animationTimer = new Timer { Interval = 25, Enabled = false };
            _animationTimer.Tick += (x, y) =>
            {
                if (_lastCheckedValue == (int)Value && DataGridView != null)
                    DataGridView.InvalidateCell(this);
                else
                    _lastCheckedValue = (int)(Value ?? 0);
            };
        }
        // Method required to make the Progress Cell consistent with the default Image Cell.
        // The default Image Cell assumes an Image as a value, although the value of the Progress Cell is an int.
        protected override object GetFormattedValue(object value,
                            int rowIndex, ref DataGridViewCellStyle cellStyle,
                            TypeConverter valueTypeConverter,
                            TypeConverter formattedValueTypeConverter,
                            DataGridViewDataErrorContexts context)
        {
            return emptyImage;
        }

        protected override void Paint(Graphics g, Rectangle clipBounds, Rectangle cellBounds, int rowIndex, DataGridViewElementStates cellState, object value, object formattedValue, string errorText, DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
        {
            // Draws the cell grid
            base.Paint(g, clipBounds, cellBounds,
             rowIndex, cellState, value, formattedValue, errorText,
             cellStyle, advancedBorderStyle, (paintParts & ~DataGridViewPaintParts.ContentForeground));

            try
            {
                if (Value == null || !(Value is int))
                    return;

                if (_marqueeMode)
                {
                    //Start animation timer if it isnt on already
                    if (!_animationTimer.Enabled)
                    {
                        _animationTimer.Enabled = true;
                        _marqueeIndex = 25;
                    }

                    double barWidth = _marqueeIndex < 75
                                  ? .5
                                  : (100 - _marqueeIndex) / 50.0;
                    int marqueeStart = Convert.ToInt32(((_marqueeIndex - 25) / 50.0) * (cellBounds.Width - 4));

                    //Draw main progress bar
                    g.FillRectangle(new SolidBrush(Color.FromArgb(163, 189, 242)), cellBounds.X + 2 + marqueeStart, cellBounds.Y + 2,
                                        Convert.ToInt32((barWidth * (cellBounds.Width - 4))), cellBounds.Height - 4);

                    //if Bar is far enough in draw a second one
                    if (_marqueeIndex > 75)
                    {
                        double secondBarWidth = .5 - barWidth;

                        g.FillRectangle(new SolidBrush(Color.FromArgb(163, 189, 242)), cellBounds.X + 2, cellBounds.Y + 2,
                                        Convert.ToInt32((secondBarWidth * (cellBounds.Width - 4))), cellBounds.Height - 4);
                    }

                    //go through range 25-100 (0-25 have tendency to write outside control)
                    if (_marqueeIndex == 100)
                        _marqueeIndex = 25;
                    else
                        _marqueeIndex++;
                }
                else if (_maxValue > 0)
                {
                    _animationTimer.Enabled = false;

                    if ((int)Value < MinValue)
                        Value = MinValue;

                    double percentage = ((int)Value / (double)_maxValue);
                    if (percentage > 1)
                        percentage = 1;
                    if (percentage < 0)
                        percentage = 0;

                    if (percentage > 0.0)
                    {
                        g.FillRectangle(new SolidBrush(Color.FromArgb(163, 189, 242)), cellBounds.X + 2, cellBounds.Y + 2,
                                        Convert.ToInt32((percentage * cellBounds.Width - 4)), cellBounds.Height - 4);
                    }
                }
                else
                    _animationTimer.Enabled = false;

                var editedMessage = _message.Replace(MessageSpecialValue.CurrentValue, Value.ToString())
            .Replace(MessageSpecialValue.MaxValue, _maxValue.ToString())
            .Replace(MessageSpecialValue.MinValue, _minValue.ToString())
            .Replace(MessageSpecialValue.Percentage, Math.Floor(((int)Value / (double)_maxValue) * 100.0) + "%");

                //write message over bar
                if (!string.IsNullOrEmpty(_message))
                {
                    Brush foreColorBrush = new SolidBrush(cellStyle.ForeColor);
                    g.DrawString(editedMessage, cellStyle.Font, foreColorBrush, cellBounds.X + 6, cellBounds.Y + 2);
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                //Row couldn't be accessed
            }
        }
    }
}