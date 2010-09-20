//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
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

            BackColor = SystemColors.Window;

            ResizeRedraw = true;
            DoubleBuffered = true;
            AutoScroll = true;

            SetProtein(pro);

            SizeChanged += new EventHandler(resize);
            Scroll += new ScrollEventHandler(scroll);
        }

        void resize (object sender, EventArgs e)
        {
            control.MinimumSize = new Size(0, ClientSize.Height);
        }

        void scroll (object sender, ScrollEventArgs e)
        {
            control.Refresh();
        }

        void SetProtein (DataModel.Protein pro)
        {
            Controls.Clear();

            control = new SequenceCoverageControl(pro)
            {
                BackColor = this.BackColor,
                Bounds = this.Bounds,

                // the SequenceCoverageControl is anchored everywhere but the bottom: when its height becomes
                // larger than the form's height, the form automatically adds a scroll bar to allow the user
                // to pan the control
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,

                // the control's height must be at least the form's height
                MinimumSize = new Size(0, ClientSize.Height)
            };

            Controls.Add(control);
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
        //List<int> totalCoverageMask = null;
        //List<List<int>> groupCoverageMasks = null;
        SizeF residueBounds;

        //ToolTip toolTip;

        public SequenceCoverageControl (DataModel.Protein pro)
        {
            protein = pro;

            /*#region Calculate sequence coverage (masks and percentages)
            if (coverageArrays != null && coverageArrays.Count > 0)
            {
                totalCoverageMask = new List<int>(protein.Length);
                groupCoverageMasks = new List<List<int>>();

                for (int i = 0; i < protein.Length; ++i)
                    totalCoverageMask.Add(0);

                foreach (CoverageArray coverageArray in coverageArrays)
                {
                    List<int> coverageMask = new List<int>(protein.Length);
                    for (int i = 0; i < protein.Length; ++i)
                        coverageMask.Add(0);
                    foreach (CoveragePair coveragePair in coverageArray)
                        for (int i = 0; i < coveragePair.Value; ++i)
                        {
                            ++coverageMask[coveragePair.Key + i];
                            ++totalCoverageMask[coveragePair.Key + i];
                        }
                    groupCoverageMasks.Add(coverageMask);
                }
            }
            #endregion*/

            DoubleBuffered = true;
            ResizeRedraw = true;
            Paint += new PaintEventHandler(paint);
            Resize += new EventHandler(resize);
            SizeChanged += new EventHandler(resize);

            //toolTip = new ToolTip() { Active = true, ShowAlways = true, InitialDelay = 100, ReshowDelay = 100, AutoPopDelay = 0 };
            //MouseMove += new MouseEventHandler( hover );

            calculateBoundingInfo();
        }

        #region Implementation details
        /*void hover( object sender, MouseEventArgs e )
        {
            Point pt = e.Location;
            int lineNumber = (int) Math.Floor( ( pt.Y - sequenceLocation.Y ) / lineSpacing / residueBounds.Height );
            float charOnLine = ( pt.X - sequenceLocation.X ) / residueBounds.Width;
            int residueNumber = residuesPerLine * lineNumber + (int) Math.Floor( charOnLine - Math.Floor( charOnLine / residueGroupSize ) );
            toolTip.Show( String.Format( "Row: {0}  Col: {1}", lineNumber, residueNumber ), this, 100 );
        }*/

        void resize (object sender, EventArgs e)
        {
            calculateBoundingInfo();
        }

        Map<int, PointF> residueIndexToLocation;
        PointF sequenceLocation;
        int lineCount;
        int residuesPerLine;

        void calculateBoundingInfo ()
        {
            SizeF averageBounds = CreateGraphics().MeasureString("ABCDEFGHIJKLMNOPQRSTUVWXYZ", sequenceFont, 0);
            residueBounds = new SizeF(averageBounds.Width / 26, averageBounds.Height);

            sequenceLocation = new PointF(residueBounds.Width * 10, sequenceFont.Height * 4);
            residueIndexToLocation = new Map<int, PointF>();

            int charsOnLine = 0;
            int realCharsPerLine = 0;
            residuesPerLine = 0;
            int maxCharsPerLine = (int) Math.Floor(((float) ClientSize.Width) / residueBounds.Width);

            for (int i = 0; i < protein.Length; ++i, ++charsOnLine, ++residuesPerLine)
            {
                if (residueGroupSize == 0 && charsOnLine + 15 >= maxCharsPerLine)
                {
                    realCharsPerLine = charsOnLine;
                    break;
                }
                else if (i > 0 && residueGroupSize > 0 && i % residueGroupSize == 0)
                {
                    if (charsOnLine + residueGroupSize + 15 >= maxCharsPerLine)
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
            for (int i = 0; i < protein.Length; ++i, ++charsOnLine)
            {
                if (residueGroupSize == 0 && charsOnLine + 15 >= maxCharsPerLine)
                {
                    charsOnLine = 0;
                    ++lineCount;
                }
                else if (i > 0 && residueGroupSize > 0 && i % residueGroupSize == 0)
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
                                             sequenceLocation.Y + (lineCount - 1) * lineSpacing * residueBounds.Height);
                residueIndexToLocation.Add(i, location);
                //residueLocationToIndex.Add( location, i );
            }

            Height = Math.Max(MinimumSize.Height, (int) (sequenceLocation.Y + (lineCount + 1) * lineSpacing * residueBounds.Height));
        }
        #endregion

        void paint (object sender, PaintEventArgs e)
        {
            e.Graphics.FillRectangle(new SolidBrush(BackColor), e.ClipRectangle);

            string header = String.Format("{0}\r\n{1} residues, {2} kDa (MW), {3}% coverage",
                                          protein.Description,
                                          protein.Length,
                                          (new proteome.Peptide(protein.Sequence).molecularWeight() / 1000).ToString("f3"),
                                          protein.Coverage.ToString("f1"));


            e.Graphics.DrawString(header, headerFont, new SolidBrush(headerColor), PointF.Empty);

            Brush coveredBrush = new SolidBrush(coveredSequenceColor);
            Brush uncoveredBrush = new SolidBrush(uncoveredSequenceColor);
            foreach (var pair in residueIndexToLocation)
                if (protein.CoverageMask[pair.Key] > 0)
                    e.Graphics.DrawString(protein.Sequence[pair.Key].ToString(), sequenceFont, coveredBrush, pair.Value);
                else
                    e.Graphics.DrawString(protein.Sequence[pair.Key].ToString(), sequenceFont, uncoveredBrush, pair.Value);

            for (int i = 0; i < lineCount; ++i)
            {
                int lastResidue = i + 1 == lineCount ? protein.Length : (i + 1) * residuesPerLine;
                string lineHeader = String.Format("{0}-{1}", i * residuesPerLine + 1, lastResidue);
                e.Graphics.DrawString(lineHeader,
                                      offsetFont,
                                      new SolidBrush(offsetColor),
                                      0,
                                      sequenceLocation.Y + (i * residueBounds.Height * lineSpacing));
            }

            Font boldSequenceFont = new Font(sequenceFont, FontStyle.Underline);
            int coverageMaskOffset = 1;
            //foreach (List<int> coverageMask in groupCoverageMasks)
            {
                foreach (var pair in residueIndexToLocation)
                {
                    char c;
                    Font f = sequenceFont;
                    switch (protein.CoverageMask[pair.Key])
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
        }
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
    }
}