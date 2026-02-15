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
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls
{
    /// <summary>
    /// Implement to provide custom tool tips for tree nodes.
    /// </summary>
    public interface ITipProvider
    {
        /// <summary>
        /// Return false to disable tips on the implementing node depending
        /// on application state.
        /// </summary>
        bool HasTip { get; }

        /// <summary>
        /// In the process of showing a custom tip, this function is called
        /// multiple times. First, it is called with <see cref="draw"/> set to false,
        /// and a maximum size allowable for the tip client area. The implementing code
        /// is expected to return a desired size for the tip client area.  The caller
        /// may call as many times as necessary with <see cref="draw"/> set to false
        /// in order to negotiate a tip size.  The implementation must not actually
        /// draw on the <see cref="Graphics"/> supplied in these cases.
        ///
        /// Finally, the method will be called once with <see cref="draw"/> set to true
        /// and a maximum size.  The implementation must then use the <see cref="Graphics"/>
        /// supplied to draw its tip with origin (0,0) and within the maximum size.
        /// </summary>
        /// <param name="g">Graphics to use for measuring or drawing the tip</param>
        /// <param name="sizeMax">Maximum size within which the tip must fit</param>
        /// <param name="draw">True if the implementation should paint, or false if it should measure</param>
        /// <returns>The best size for the tip that fits within the maximum specified</returns>
        Size RenderTip(Graphics g, Size sizeMax, bool draw);
    }

    /// <summary>
    /// Optional interface for tip providers that want to expose their tip content as text for testing.
    /// This allows tests to verify tooltip content without needing to analyze rendered graphics.
    /// </summary>
    public interface ITipProviderWithText : ITipProvider
    {
        /// <summary>
        /// Gets a text representation of the tip content for testing purposes.
        /// Typically formatted as a markdown table or other human-readable format.
        /// </summary>
        string TipText { get; }
    }

    /// <summary>
    /// Implement to enable a control to display tool tips.
    /// </summary>
    public interface ITipDisplayer
    {
        /// <summary>
        /// Gets the bounds of the control in which the tip is displayed.
        /// </summary>
        Rectangle ScreenRect { get; }

        /// <summary>
        /// Indicates if the tip should be displayed.
        /// </summary>
        bool AllowDisplayTip { get; }

        /// <summary>
        /// Gets the screen coordinates of the given rectangle,
        /// which in this case is the node whose tip we are displaying.
        /// </summary>
        Rectangle RectToScreen(Rectangle r);
    }

    public class NodeTip : CustomTip
    {
        public static string FontFace { get { return @"Arial"; } }
        public static float FontSize { get { return 8f; } }

        public static int TipDelayMs { get { return 500; } }

        private ITipProvider _tipProvider;
        private readonly ITipDisplayer _tipDisplayer;
        private Rectangle _rectItem;
        private Timer _timer;
        private readonly MoveThreshold _moveThreshold = new MoveThreshold(5, 5);

        private const int NODE_SPACE_Y = 5;

        public NodeTip(ITipDisplayer tipDisplayer)
        {
            _timer = new Timer { Interval = TipDelayMs };
            _timer.Tick += Timer_Tick;
            _tipDisplayer = tipDisplayer;
        }

        protected override void Dispose(bool disposing)
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }
            base.Dispose(disposing);
        }

        public void HideTip()
        {
            SetTipProvider(null, new Rectangle(), new Point());
        }

        public void SetTipProvider(ITipProvider tipProvider, Rectangle rectItem, Point cursorPos)
        {
            if (!_moveThreshold.Moved(cursorPos))
                return;
            _timer.Stop();
            if (Visible)
            {
                AnimateMode animate = (Y < _rectItem.Y ?
                AnimateMode.SlideTopToBottom : AnimateMode.SlideBottomToTop);
                HideAnimate(animate);
            }
            _tipProvider = tipProvider;
            _rectItem = _tipDisplayer.RectToScreen(rectItem);
            _moveThreshold.Location = cursorPos;
            if (tipProvider != null)
                _timer.Start();
        }

        public override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_tipProvider != null)
            {
                // Render in unrestricted size, since current algorithms may
                // not render completely, if given exactly the ClientSize.
                _tipProvider.RenderTip(e.Graphics, ClientSize, true);
            }
        }

        private void Timer_Tick(Object sender, EventArgs e)
        {
            _timer.Stop();
            if (_tipDisplayer == null || !_tipDisplayer.AllowDisplayTip)
                return;
            try
            {
                DisplayTip();
            }
            catch (ObjectDisposedException)
            {
                // In case of a tip trying to display while the window is closing, just ignore
            }
            catch (Exception exception)
            {
                ExceptionUtil.DisplayOrReportException(this, exception, ControlsResources.NodeTip_Timer_Tick_An_error_occurred_displaying_a_tooltip_);
            }
        }

        private void DisplayTip()
        {
            Rectangle rectScreen = _tipDisplayer.ScreenRect;
            AnimateMode animate = AnimateMode.SlideTopToBottom;

            using (Bitmap bitmap1 = new Bitmap(1, 1, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bitmap1))
                {
                    Size size = _tipProvider.RenderTip(g, rectScreen.Size, false);
                    int yPos = _rectItem.Y + _rectItem.Height + NODE_SPACE_Y;
                    if (yPos + size.Height > rectScreen.Bottom)
                    {
                        if (rectScreen.Bottom - yPos > _rectItem.Top - NODE_SPACE_Y - rectScreen.Top)
                        {
                            size.Height = rectScreen.Bottom - yPos;

                            // Recalc size based to fit into restricted area.
                            size = _tipProvider.RenderTip(g, size, false);
                        }
                        else
                        {
                            yPos = _rectItem.Top - NODE_SPACE_Y;
                            if (yPos - size.Height < rectScreen.Top)
                            {
                                size.Height = yPos - rectScreen.Top;

                                // Recalc size based to fit into restricted area.
                                size = _tipProvider.RenderTip(g, size, false);
                            }
                            yPos -= size.Height;
                            animate = AnimateMode.SlideBottomToTop;
                        }
                    }
                    Location = new Point(_rectItem.X, yPos);
                    ClientSize = size;
                }
            }

            ShowAnimate(X, Y, animate); // Not really animated anymore, because of GDI handle leak on Windows 10
        }

        #region Test Support

        public ITipProvider Provider => _tipProvider;

        public string TipText => (_tipProvider as ITipProviderWithText)?.TipText;

        #endregion
    }

    /// <summary>
    /// Implement to enable a control to display tool tips.
    /// </summary>
    internal class RenderTools : IDisposable
    {
        bool _disposed;

        public RenderTools()
        {
            FontNormal = new Font(NodeTip.FontFace, NodeTip.FontSize);
            FontBold = new Font(NodeTip.FontFace, NodeTip.FontSize, FontStyle.Bold);
            BrushNormal = Brushes.Black;
            BrushChoice = BrushNormal;
            BrushChosen = Brushes.Blue;
            BrushSelected = Brushes.Red;
        }

        public Font FontNormal { get; private set; }
        public Font FontBold { get; private set; }
        public Brush BrushNormal { get; private set; }
        public Brush BrushChoice { get; private set; }
        public Brush BrushChosen { get; private set; }
        public Brush BrushSelected { get; private set; }

        #region IDisposable Members

        public void Dispose()
        {
            if (!_disposed)
            {
                if (FontNormal != null)
                    FontNormal.Dispose();
                if (FontBold != null)
                    FontBold.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }

    internal class TableDesc : List<RowDesc>
    {
        public const int COL_SPACING = 2;
        public const int TABLE_SPACING = 6;

        public void AddDetailRow(string name, string value, RenderTools rt, StringAlignment dataAlign, bool allBold = false)
        {
            var row = new RowDesc
            {
                new CellDesc(name, rt) { Font = rt.FontBold },
                new CellDesc(value, rt) { Align = dataAlign, Font = allBold ? rt.FontBold : rt.FontNormal}
            };
            row.ColumnSpacing = COL_SPACING;
            Add(row);
        }

        public void AddDetailRow(string name, string value, RenderTools rt, bool allBold = false)
        {
            AddDetailRow(name, value, rt, StringAlignment.Near, allBold);
        }

        private const string X80 =
            @"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX";

        /// <summary>
        /// Adds a text column a with potential line wrap.
        /// </summary>
        /// <param name="g">The graphics object in which the text will be rendered</param>
        /// <param name="name">Field name</param>
        /// <param name="value">Field value text</param>
        /// <param name="rt">Rendering tools used to render the text</param>
        /// <returns>True if the text was multi-line</returns>
        public bool AddDetailRowLineWrap(Graphics g, string name, string value, RenderTools rt)
        {
            SizeF sizeX80 = g.MeasureString(X80, rt.FontNormal);
            float widthLine = sizeX80.Width;
            var words = value.Split(TextUtil.SEPARATOR_SPACE);
            string line = string.Empty;
            bool firstRow = true;
            // This is a little bit strange, but it works.  Because the split call
            // splits only on spaces, newlines are preserved, and MeasureString will
            // account for them.  So, only when a line gets too long will it be wrapped
            // by creating a new row.  This does mean, however, that firstRow is not
            // a valid indicator on its own of whether the text is multi-line.
            foreach (string word in words)
            {
                if (g.MeasureString(TextUtil.SpaceSeparate(line + word, string.Empty), rt.FontNormal).Width > widthLine)
                {
                    AddDetailRow(firstRow ? name : string.Empty, line, rt);
                    line = string.Empty;
                    firstRow = false;
                }
                line += word + TextUtil.SEPARATOR_SPACE;
            }
            AddDetailRow(firstRow ? name : string.Empty, line, rt);
            // The text is multi-line if either it required wrapping to multiple rows,
            // or it contains new-line characters.
            return !firstRow || value.Contains('\n');
        }

        public SizeF CalcDimensions(Graphics g)
        {
            SizeF size = new SizeF(0, 0);
            List<float> colWidths = new List<float>();

            foreach (RowDesc row in this)
            {
                float heightMax = 0f;

                row.CalcDimensions(g);
                for (int i = 0; i < row.Count; i++)
                {
                    if (i == colWidths.Count)
                        colWidths.Add(0f);
                    SizeF sizeCell = row[i].SizeF;
                    colWidths[i] = Math.Max(colWidths[i], sizeCell.Width);
                    // Add spacing, if this is not the last column
                    colWidths[i] += i < row.Count - 1 ? row.ColumnSpacing : 1;
                    heightMax = Math.Max(heightMax, sizeCell.Height);
                }

                // Reset the heights all to the same value
                foreach (CellDesc cell in row)
                    cell.Height = heightMax;

                size.Height += heightMax;
            }

            foreach (RowDesc row in this)
            {
                // Reset widths for each column to the same value
                for (int i = 0; i < row.Count; i++)
                    row[i].Width = colWidths[i];
            }

            // Total the widths used.
            foreach (float width in colWidths)
                size.Width += width;

            return size;
        }

        public void Draw(Graphics g)
        {
            StringFormat sf = new StringFormat();
            float y = 0f;
            foreach (RowDesc row in this)
            {
                float x = 0f;
                foreach (CellDesc cell in row)
                {
                    sf.Alignment = cell.Align;
                    sf.LineAlignment = StringAlignment.Near;
                    RectangleF rect = new RectangleF(x, y, cell.Width, cell.Height);
                    Font font = cell.Font;
                    Brush brush = cell.Brush;
                    g.DrawString(cell.Text, font, brush, rect, sf);
                    x += cell.Width;
                }
                y += row[0].Height;
            }
        }

        public override string ToString()
        {
            return string.Join(Environment.NewLine, this.Select(row => row.ToString()));
        }
    }

    internal class RowDesc : List<CellDesc>
    {
        public int ColumnSpacing { get; set; }

        public void CalcDimensions(Graphics g)
        {
            foreach (CellDesc cell in this)
                cell.SizeF = g.MeasureString(cell.Text, cell.Font);
        }

        public override string ToString()
        {
            return string.Join(@" | ", this.Select(cell => cell.ToString()));
        }
    }

    internal class CellDesc
    {
        private SizeF _sizeF;

        public CellDesc(string text, RenderTools rt)
        {
            Text = text;
            Align = StringAlignment.Near;
            Font = rt.FontNormal;
            Brush = rt.BrushNormal;
        }

        public string Text { get; set; }
        public Font Font { get; set; }
        public Brush Brush { get; set; }
        public StringAlignment Align { get; set; }
        public SizeF SizeF
        {
            get { return _sizeF; }
            set { _sizeF = value; }
        }
        public float Width
        {
            get { return _sizeF.Width; }
            set { _sizeF.Width = value; }
        }
        public float Height
        {
            get { return _sizeF.Height; }
            set { _sizeF.Height = value; }
        }

        public override string ToString()
        {
            return Text;
        }
    }
}
