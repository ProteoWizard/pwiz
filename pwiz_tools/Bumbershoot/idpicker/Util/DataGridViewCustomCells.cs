//
// $Id: DataGridViewProgressBar.cs 276 2011-06-03 20:48:38Z chambm $
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
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;
using System.ComponentModel;
using System.Timers;

namespace CustomProgressCell
{
    public sealed class DataGridViewProgressColumn : DataGridViewColumn
    {
        public DataGridViewProgressColumn()
        {
            CellTemplate = new DataGridViewProgressCell();
        }
    }

    public sealed class DataGridViewProgressCell : DataGridViewTextBoxCell
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
            set { _text = value; /*refresh();*/ }
        }

        /// <summary>
        /// Gets or sets the progress bar's drawing style.
        /// </summary>
        public ProgressBarStyle ProgressBarStyle
        {
            get { return _progressBar.Style; }
            set { if (_progressBar.Style != value) { _progressBar.Style = value; startAnimation(); } }
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
        System.Timers.Timer _animationStepTimer;
        System.Timers.Timer _animationStopTimer;
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
            _animationStepTimer = new System.Timers.Timer { Interval = 25, Enabled = true, SynchronizingObject = DataGridView };

            // stop repainting 3 seconds after progress becomes inactive
            _animationStopTimer = new System.Timers.Timer { Interval = 3000, Enabled = false, SynchronizingObject = DataGridView };

            _animationStepTimer.Elapsed += (x, y) =>
            {
                stopAnimation();
                refresh();
            };
            _animationStopTimer.Elapsed += (x, y) => { _animationStepTimer.Stop(); _animationStopTimer.Stop(); };
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
            else
                _animationStopTimer.Stop();
        }
    }
}

namespace CustomFileCell
{
    public class DataGridViewFileBrowseColumn : DataGridViewColumn
    {
        public DataGridViewFileBrowseColumn()
            : base(new FileBrowseCell())
        {
        }

        public override DataGridViewCell CellTemplate
        {
            get
            {
                return base.CellTemplate;
            }
            set
            {
                // Ensure that the cell used for the template is a FileBrowseCell.
                if (value != null &&
                    !value.GetType().IsAssignableFrom(typeof(FileBrowseCell)))
                {
                    throw new InvalidCastException("Must be a FileBrowseCell");
                }
                base.CellTemplate = value;
            }
        }
    }

    public class FileBrowseCell : DataGridViewTextBoxCell
    {

        public FileBrowseCell()
            : base()
        {
            // Use the short date format.
            this.Style.Format = "d";
        }

        public override void InitializeEditingControl(int rowIndex, object
            initialFormattedValue, DataGridViewCellStyle dataGridViewCellStyle)
        {
            // Set the value of the editing control to the current cell value.
            base.InitializeEditingControl(rowIndex, initialFormattedValue,
                dataGridViewCellStyle);
            FileBrowseEditingControl ctl =
                DataGridView.EditingControl as FileBrowseEditingControl;
            // Use the default row value when Value property is null.
            if (this.Value == null)
            {
                ctl.Value = (string)this.DefaultNewRowValue;
            }
            else
            {
                ctl.Value = (string)this.Value;
            }
        }

        public override Type EditType
        {
            get
            {
                // Return the type of the editing control that FileBrowseCell uses.
                return typeof(FileBrowseEditingControl);
            }
        }

        public override Type ValueType
        {
            get
            {
                // Return the type of the value that FileBrowseCell contains.

                return typeof(string);
            }
        }

        public override object DefaultNewRowValue
        {
            get
            {
                // Use the current date and time as the default value.
                return string.Empty;
            }
        }
    }

    class FileBrowseEditingControl : FileBrowseControl, IDataGridViewEditingControl
    {
        private bool valueChanged = false;

        // Implements the IDataGridViewEditingControl.EditingControlFormattedValue 
        // property.
        public object EditingControlFormattedValue
        {
            get
            {
                return this.Value;
            }
            set
            {
                if (value is String)
                {
                    try
                    {
                        // This will throw an exception of the string is 
                        // null, empty, or not in the format of a date.
                        this.Value = (String)value;
                    }
                    catch
                    {
                        // In the case of an exception, just use the 
                        // default value so we're not left with a null
                        // value.
                        this.Value = string.Empty;
                    }
                }
            }
        }

        // Implements the 
        // IDataGridViewEditingControl.GetEditingControlFormattedValue method.
        public object GetEditingControlFormattedValue(
            DataGridViewDataErrorContexts context)
        {
            return EditingControlFormattedValue;
        }

        // Implements the 
        // IDataGridViewEditingControl.ApplyCellStyleToEditingControl method.
        public void ApplyCellStyleToEditingControl(
            DataGridViewCellStyle dataGridViewCellStyle)
        {
            this.Font = dataGridViewCellStyle.Font;
            this.ForeColor = dataGridViewCellStyle.ForeColor;
            this.BackColor = dataGridViewCellStyle.BackColor;
        }

        // Implements the IDataGridViewEditingControl.EditingControlRowIndex 
        // property.
        public int EditingControlRowIndex { get; set; }

        // Implements the IDataGridViewEditingControl.EditingControlWantsInputKey 
        // method.
        public bool EditingControlWantsInputKey(
            Keys key, bool dataGridViewWantsInputKey)
        {
            // Let the FileBrowseControl handle the keys listed.
            switch (key & Keys.KeyCode)
            {
                case Keys.Left:
                case Keys.Up:
                case Keys.Down:
                case Keys.Right:
                case Keys.Home:
                case Keys.End:
                case Keys.PageDown:
                case Keys.PageUp:
                    return true;
                default:
                    return !dataGridViewWantsInputKey;
            }
        }

        // Implements the IDataGridViewEditingControl.PrepareEditingControlForEdit 
        // method.
        public void PrepareEditingControlForEdit(bool selectAll)
        {
            // No preparation needs to be done.
        }

        // Implements the IDataGridViewEditingControl
        // .RepositionEditingControlOnValueChange property.
        public bool RepositionEditingControlOnValueChange
        {
            get { return false; }
        }

        // Implements the IDataGridViewEditingControl
        // .EditingControlDataGridView property.
        public DataGridView EditingControlDataGridView { get; set; }

        // Implements the IDataGridViewEditingControl
        // .EditingControlValueChanged property.
        public bool EditingControlValueChanged
        {
            get
            {
                return valueChanged;
            }
            set
            {
                valueChanged = value;
            }
        }

        // Implements the IDataGridViewEditingControl
        // .EditingPanelCursor property.
        public Cursor EditingPanelCursor
        {
            get
            {
                return base.Cursor;
            }
        }

        protected override void OnValueChanged(EventArgs eventargs)
        {
            // Notify the DataGridView that the contents of the cell
            // have changed.
            valueChanged = true;
            this.EditingControlDataGridView.NotifyCurrentCellDirty(true);
        }
    }

    internal class FileBrowseControl : Control
    {
        private TextBox _locationBox;
        private Button _browseButton;

        public FileBrowseControl()
        {
            //set up Location Box
            _locationBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = false,
                AutoCompleteMode = AutoCompleteMode.Suggest,
                AutoCompleteSource = AutoCompleteSource.FileSystem
            };
            _locationBox.TextChanged += _locationBox_TextChanged;

            //Set up Browse Button
            _browseButton = new Button
            {
                Dock = DockStyle.Fill,
                TabStop = false,
                Image = FolderIconAccession.GetOpenFolderIcon().ToBitmap()
            };
            _browseButton.Click += _browseButton_Click;

            //Set up Split Container
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                IsSplitterFixed = true,
                SplitterWidth = 1
            };
            if (split.Width > 20)
                split.SplitterDistance = split.Width - 20;
            split.Panel1.Controls.Add(_locationBox);
            split.Panel2.Controls.Add(_browseButton);
            split.SizeChanged += split_SizeChanged;

            //Set up Control
            Controls.Add(split);
            this.GotFocus += FileBrowseControl_GotFocus;

        }

        void FileBrowseControl_GotFocus(object sender, EventArgs e)
        {
            _locationBox.Focus();
        }

        void _browseButton_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            var initialDirectory = System.IO.Path.GetDirectoryName(_locationBox.Text);
            if (!string.IsNullOrEmpty(initialDirectory))
                ofd.InitialDirectory = initialDirectory;
            if (ofd.ShowDialog() == DialogResult.OK)
                _locationBox.Text = ofd.FileName;
        }

        void split_SizeChanged(object sender, EventArgs e)
        {
            var split = (SplitContainer)sender;
            if (split.Width > 20)
                split.SplitterDistance = split.Width - 20;
        }

        void _locationBox_TextChanged(object sender, EventArgs e)
        {
            OnValueChanged(e);
        }

        protected virtual void OnValueChanged(EventArgs eventargs)
        {
            //in place simply to override
        }

        public string Value
        {
            get { return _locationBox.Text; }
            set { _locationBox.Text = value; }
        }
    }

    public static class FolderIconAccession
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        };

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool DestroyIcon(IntPtr hIcon);

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const uint SHGFI_OPENICON = 0x000000002;
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

        public static Icon GetOpenFolderIcon()
        {
            // Need to add size check, although errors generated at present!    
            uint flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES;

            flags += SHGFI_OPENICON;
            flags += SHGFI_SMALLICON;
            // Get the folder icon    
            var shfi = new SHFILEINFO();

            var res = SHGetFileInfo(@"C:\Windows",
                FILE_ATTRIBUTE_DIRECTORY,
                out shfi,
                (uint)Marshal.SizeOf(shfi),
                flags);

            if (res == IntPtr.Zero)
                throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());

            // Load the icon from an HICON handle  
            Icon.FromHandle(shfi.hIcon);

            // Now clone the icon, so that it can be successfully stored in an ImageList
            var icon = (Icon)Icon.FromHandle(shfi.hIcon).Clone();

            DestroyIcon(shfi.hIcon);        // Cleanup    

            return icon;
        }
    }
}