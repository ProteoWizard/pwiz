//
// $Id$
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
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Drawing.Text;
using DigitalRune.Windows.Docking;
using proteome = pwiz.CLI.proteome;

namespace IDPicker
{
    public class SequenceCoverageForm : DockableForm
    {
        SequenceCoverageControl control;

        public SequenceCoverageForm (DataModel.Protein pro)
        {
            StartPosition = FormStartPosition.CenterParent;
            ShowIcon = false;
            WindowState = FormWindowState.Maximized;
            Text = pro.Accession;

            ResizeRedraw = true;
            DoubleBuffered = true;

            BackColor = SystemColors.Window;

            control = new SequenceCoverageControl(pro)
            {
                Dock = DockStyle.Fill,
                BackColor = this.BackColor
            };

            Controls.Add(control);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // SequenceCoverageForm
            // 
            this.ClientSize = new System.Drawing.Size(292, 266);
            this.DockAreas = ((DigitalRune.Windows.Docking.DockAreas)(((((DigitalRune.Windows.Docking.DockAreas.Left | DigitalRune.Windows.Docking.DockAreas.Right)
                        | DigitalRune.Windows.Docking.DockAreas.Top)
                        | DigitalRune.Windows.Docking.DockAreas.Bottom)
                        | DigitalRune.Windows.Docking.DockAreas.Document)));
            this.Name = "SequenceCoverageForm";
            this.ResumeLayout(false);

        }
    }

    /// <summary>
    /// Reusable control for reading a protein from a FASTA database and displaying either the naked sequence or
    /// one or more lines of sequence coverage based on peptide start offsets and lengths. Groups residues by a
    /// user-specified parameter. Automatically word-wraps the groups based on the width of the control.
    /// </summary>
    [Designer("System.Windows.Forms.Design.ControlDesigner, System.Design, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
    public class SequenceCoverageControl : UserControl
    {
        #region Public properties
        /// <summary>
        /// Gets or sets the number of residues to display before inserting a space.
        /// </summary>
        [Browsable(true)]
        public int ResidueGroupSize
        {
            get { return residueGroupSize; }
            set { residueGroupSize = value; }
        }

        /// <summary>
        /// Gets or sets the line spacing between sequence lines. The minimum is 2 plus the number of groups to show coverage for.
        /// </summary>
        [Browsable(true)]
        public int LineSpacing
        {
            get { return lineSpacing; }
            set { lineSpacing = Math.Max(2, value); }
        }

        /// <summary>
        /// Gets or sets the color used to display protein metadata in the first few lines of the control.
        /// </summary>
        [Browsable(true)]
        public Color HeaderColor
        {
            get { return headerColor; }
            set { headerColor = value; }
        }

        /// <summary>
        /// Gets or sets the color used to display residue offsets in the left-hand of the control margin.
        /// </summary>
        [Browsable(true)]
        public Color OffsetColor
        {
            get { return offsetColor; }
            set { offsetColor = value; }
        }

        /// <summary>
        /// Gets or sets the color used to display the portions of the protein that are covered.
        /// </summary>
        [Browsable(true)]
        public Color CoveredSequenceColor
        {
            get { return coveredSequenceColor; }
            set { coveredSequenceColor = value; }
        }

        /// <summary>
        /// Gets or sets the color used to display the portions of the protein that are uncovered.
        /// </summary>
        [Browsable(true)]
        public Color UncoveredSequenceColor
        {
            get { return uncoveredSequenceColor; }
            set { uncoveredSequenceColor = value; }
        }

        /// <summary>
        /// Gets or sets the font used to display protein metadata in the first few lines of the control.
        /// </summary>
        [Browsable(true)]
        public Font HeaderFont
        {
            get { return headerFont; }
            set { headerFont = value; }
        }

        /// <summary>
        /// Gets or sets the font used to display residue offsets in the left-hand of the control margin.
        /// </summary>
        [Browsable(true)]
        public Font OffsetFont
        {
            get { return offsetFont; }
            set { offsetFont = value; }
        }

        /// <summary>
        /// Gets or sets the font used to display the amino acid sequence.
        /// </summary>
        [Browsable(true)]
        public Font SequenceFont
        {
            get { return sequenceFont; }
            set { sequenceFont = value; }
        }
        #endregion

        int residueGroupSize = 10;
        int lineSpacing = 3;

        Color headerColor = Color.FromKnownColor(KnownColor.WindowText);
        Color offsetColor = Color.FromKnownColor(KnownColor.GrayText);
        Color coveredSequenceColor = Color.FromKnownColor(KnownColor.WindowText);
        Color uncoveredSequenceColor = Color.FromKnownColor(KnownColor.GrayText);

        Font headerFont = new Font(new FontFamily(GenericFontFamilies.Monospace), 12);
        Font offsetFont = new Font(new FontFamily(GenericFontFamilies.Monospace), 12);
        Font sequenceFont = new Font(new FontFamily(GenericFontFamilies.Monospace), 12);

        DataModel.Protein protein;
        public DataModel.Protein Protein
        {
            get { return protein; }
            set
            {
                protein = value;

                Controls.Clear();

                var rect = ClientRectangle;
                rect.Offset(1, 1);
                rect.Height -= 2;
                rect.Width -= 2;
                surface = new SequenceCoverageSurface(this)
                {
                    Bounds = rect,

                    // the SequenceCoverageSurface is anchored everywhere but the bottom: when its height becomes
                    // larger than the form's height, the form automatically adds a scroll bar to allow the user
                    // to pan the surface
                    Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,

                    // the surface's height must be at least the form's height
                    MinimumSize = new Size(0, ClientSize.Height)
                };

                Controls.Add(surface);
                Refresh();
            }
        }

        int scrollY;
        SequenceCoverageSurface surface;

        public SequenceCoverageControl (DataModel.Protein pro)
        {
            ResizeRedraw = true;
            DoubleBuffered = true;
            AutoScroll = true;

            Protein = pro;
        }

        protected override void OnMouseWheel (MouseEventArgs e)
        {
            scrollY = Math.Max(0, Math.Min(surface.Height, scrollY - e.Delta));
            base.OnMouseWheel(e);
            Refresh();
        }

        protected override void OnResize (EventArgs e)
        {
            surface.MinimumSize = ClientSize;
            base.OnResize(e);
            Refresh();
        }

        protected override void OnScroll (ScrollEventArgs e)
        {
            scrollY = e.NewValue;
            base.OnScroll(e);
            Refresh();
        }

        void leave (object sender, EventArgs e) { scrollY = -AutoScrollOffset.Y; }

        protected override void OnPaint (PaintEventArgs e)
        {
            this.SetRedraw(false);
            AutoScrollPosition = new Point(0, scrollY);
            this.SetRedraw(true);

            base.OnPaint(e);

            surface.Refresh();
        }
    }

    class SequenceCoverageSurface : UserControl
    {
        SequenceCoverageControl owner;
        string header;
        //List<int> totalCoverageMask = null;
        //List<List<int>> groupCoverageMasks = null;
        SizeF residueBounds;

        //ToolTip toolTip

        public SequenceCoverageSurface (SequenceCoverageControl owner)
        {
            this.owner = owner;
            header = String.Format("{0}\r\n{1} residues, {2} kDa (MW), {3}% coverage",
                                   owner.Protein.Description,
                                   owner.Protein.Length,
                                   (new proteome.Peptide(owner.Protein.Sequence).molecularWeight() / 1000).ToString("f3"),
                                   owner.Protein.Coverage.ToString("f1"));

            DoubleBuffered = true;
            ResizeRedraw = true;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            //toolTip = new ToolTip() { Active = true, ShowAlways = true, InitialDelay = 100, ReshowDelay = 100, AutoPopDelay = 0 };
            //MouseMove += new MouseEventHandler( hover );

            calculateBoundingInfo();
        }

        #region Implementation details
        protected override void OnResize (EventArgs e)
        {
            calculateBoundingInfo();
            base.OnResize(e);
        }

        protected override void OnSizeChanged (EventArgs e)
        {
            calculateBoundingInfo();
            base.OnSizeChanged(e);
        }

        Map<int, PointF> residueIndexToLocation;
        PointF sequenceLocation;
        int lineCount;
        int residuesPerLine;

        void calculateBoundingInfo ()
        {
            SizeF averageBounds = CreateGraphics().MeasureString("ABCDEFGHIJKLMNOPQRSTUVWXYZ", owner.SequenceFont, 0);
            residueBounds = new SizeF(averageBounds.Width / 26, averageBounds.Height);

            sequenceLocation = new PointF(residueBounds.Width * 10, owner.SequenceFont.Height * 4);
            residueIndexToLocation = new Map<int, PointF>();

            int charsOnLine = 0;
            int realCharsPerLine = 0;
            residuesPerLine = 0;
            int maxCharsPerLine = (int) Math.Floor(((float) ClientSize.Width) / residueBounds.Width);

            for (int i = 0; i < owner.Protein.Length; ++i, ++charsOnLine, ++residuesPerLine)
            {
                if (owner.ResidueGroupSize == 0 && charsOnLine + 15 >= maxCharsPerLine)
                {
                    realCharsPerLine = charsOnLine;
                    break;
                }
                else if (i > 0 && owner.ResidueGroupSize > 0 && i % owner.ResidueGroupSize == 0)
                {
                    if (charsOnLine + owner.ResidueGroupSize + 15 >= maxCharsPerLine)
                    {
                        realCharsPerLine = charsOnLine;
                        break;
                    }
                    else
                        ++charsOnLine;
                }
            }

            charsOnLine = 0;
            lineCount = 1;
            for (int i = 0; i < owner.Protein.Length; ++i, ++charsOnLine)
            {
                if (owner.ResidueGroupSize == 0 && charsOnLine + 15 >= maxCharsPerLine)
                {
                    charsOnLine = 0;
                    ++lineCount;
                }
                else if (i > 0 && owner.ResidueGroupSize > 0 && i % owner.ResidueGroupSize == 0)
                {
                    if (charsOnLine == realCharsPerLine)
                    {
                        charsOnLine = 0;
                        ++lineCount;
                    }
                    else
                        ++charsOnLine;
                }

                PointF location = new PointF(sequenceLocation.X + charsOnLine * residueBounds.Width,
                                             sequenceLocation.Y + (lineCount - 1) * owner.LineSpacing * residueBounds.Height);
                residueIndexToLocation.Add(i, location);
                //residueLocationToIndex.Add( location, i );
            }

            Height = Math.Max(MinimumSize.Height, (int) (sequenceLocation.Y + (lineCount + 1) * owner.LineSpacing * residueBounds.Height));
        }
        #endregion

        protected override void OnPaint (PaintEventArgs e)
        {
            e.Graphics.FillRectangle(new SolidBrush(owner.BackColor), e.ClipRectangle);
            e.Graphics.DrawString(header, owner.HeaderFont, new SolidBrush(owner.HeaderColor), PointF.Empty);

            Brush coveredBrush = new SolidBrush(owner.CoveredSequenceColor);
            Brush uncoveredBrush = new SolidBrush(owner.UncoveredSequenceColor);
            foreach (var pair in residueIndexToLocation)
                if (owner.Protein.CoverageMask[pair.Key] > 0)
                    e.Graphics.DrawString(owner.Protein.Sequence[pair.Key].ToString(), owner.SequenceFont, coveredBrush, pair.Value);
                else
                    e.Graphics.DrawString(owner.Protein.Sequence[pair.Key].ToString(), owner.SequenceFont, uncoveredBrush, pair.Value);

            for (int i = 0; i < lineCount; ++i)
            {
                int lastResidue = i + 1 == lineCount ? owner.Protein.Length : (i + 1) * residuesPerLine;
                string lineHeader = String.Format("{0}-{1}", i * residuesPerLine + 1, lastResidue);
                e.Graphics.DrawString(lineHeader,
                                      owner.OffsetFont,
                                      new SolidBrush(owner.OffsetColor),
                                      0,
                                      sequenceLocation.Y + (i * residueBounds.Height * owner.LineSpacing));
            }

            Font boldSequenceFont = new Font(owner.SequenceFont, FontStyle.Underline);
            int coverageMaskOffset = 1;
            //foreach (List<int> coverageMask in groupCoverageMasks)
            {
                foreach (var pair in residueIndexToLocation)
                {
                    char c;
                    Font f = owner.SequenceFont;
                    switch (owner.Protein.CoverageMask[pair.Key])
                    {
                        case 0: c = ' '; break;
                        case 1: c = '-'; break;
                        case 2: c = '='; break;
                        case 3: c = '≡'; break;
                        default: c = '≡'; f = boldSequenceFont; break;
                    }

                    e.Graphics.DrawString(c.ToString(),
                                          f,
                                          coveredBrush,
                                          pair.Value.X,
                                          pair.Value.Y + (coverageMaskOffset * residueBounds.Height));
                }

                ++coverageMaskOffset;
            }

            //base.OnPaint(e);
        }

        /*void hover( object sender, MouseEventArgs e )
        {
            Point pt = e.Location;
            int lineNumber = (int) Math.Floor( ( pt.Y - sequenceLocation.Y ) / lineSpacing / residueBounds.Height );
            float charOnLine = ( pt.X - sequenceLocation.X ) / residueBounds.Width;
            int residueNumber = residuesPerLine * lineNumber + (int) Math.Floor( charOnLine - Math.Floor( charOnLine / residueGroupSize ) );
            toolTip.Show( String.Format( "Row: {0}  Col: {1}", lineNumber, residueNumber ), this, 100 );
        }*/
    }
}

namespace System.Drawing
{
    public static class Extensions
    {
        public static RectangleF ToRectangleF (this Rectangle r)
        {
            return new RectangleF((float) r.X, (float) r.Y, (float) r.Width, (float) r.Height);
        }

        public static void SetRedraw (this Control ctl, bool enable)
        {
            SendMessage(ctl.Handle, 0xb, enable ? (IntPtr) 1 : IntPtr.Zero, IntPtr.Zero);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage (IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
    }
}