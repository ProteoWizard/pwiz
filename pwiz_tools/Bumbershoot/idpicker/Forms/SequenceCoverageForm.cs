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
using IDPicker.DataModel;
using proteome = pwiz.CLI.proteome;

namespace IDPicker
{
    public class SequenceCoverageForm : DockableForm
    {
        SequenceCoverageControl control;

        public event SequenceCoverageFilterEventHandler SequenceCoverageFilter;

        public SequenceCoverageForm (NHibernate.ISession session, DataModel.Protein protein, DataFilter viewFilter)
        {
            StartPosition = FormStartPosition.CenterParent;
            ShowIcon = false;
            WindowState = FormWindowState.Maximized;
            Text = protein.Accession;

            ResizeRedraw = true;
            DoubleBuffered = true;

            BackColor = SystemColors.Window;

            control = new SequenceCoverageControl(session, protein, viewFilter)
            {
                Dock = DockStyle.Fill,
                BackColor = this.BackColor
            };

            control.SequenceCoverageFilter += (s, e) => OnSequenceCoverageFilter(e);

            Controls.Add(control);
        }

        public void SetData (NHibernate.ISession session, DataFilter viewFilter)
        {
            control.SetData(session, control.Protein, viewFilter);
        }

        public void SetData (NHibernate.ISession session, DataModel.Protein protein, DataFilter viewFilter)
        {
            Text = protein.Accession;
            control.SetData(session, protein, viewFilter);
        }

        public void ClearData ()
        {
            control.ClearData();
        }

        public void ClearSession ()
        {
            control.ClearSession();
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

        protected void OnSequenceCoverageFilter (DataModel.DataFilter sequenceCoverageFilter)
        {
            if (SequenceCoverageFilter != null)
                SequenceCoverageFilter(control, sequenceCoverageFilter);
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
        public int ResidueGroupSize { get; set; }

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
        public Color HeaderColor { get; set; }

        /// <summary>
        /// Gets or sets the color used to display residue offsets in the left-hand of the control margin.
        /// </summary>
        [Browsable(true)]
        public Color OffsetColor { get; set; }

        /// <summary>
        /// Gets or sets the color used to display the portions of the protein that are covered.
        /// </summary>
        [Browsable(true)]
        public Color CoveredSequenceColor { get; set; }

        /// <summary>
        /// Gets or sets the color used to display the portions of the protein that are uncovered.
        /// </summary>
        [Browsable(true)]
        public Color UncoveredSequenceColor { get; set; }

        /// <summary>
        /// Gets or sets the color used to display the portions of the protein under the mouse cursor.
        /// </summary>
        [Browsable(true)]
        public Color HoverSequenceColor { get; set; }

        /// <summary>
        /// Gets or sets the color used to display the portions of the protein with modifications.
        /// </summary>
        [Browsable(true)]
        public Color ModifiedSequenceColor { get; set; }

        /// <summary>
        /// Gets or sets the font used to display protein metadata in the first few lines of the control.
        /// </summary>
        [Browsable(true)]
        public Font HeaderFont { get; set; }

        /// <summary>
        /// Gets or sets the font used to display residue offsets in the left-hand of the control margin.
        /// </summary>
        [Browsable(true)]
        public Font OffsetFont { get; set; }

        /// <summary>
        /// Gets or sets the font used to display the amino acid sequence.
        /// </summary>
        [Browsable(true)]
        public Font SequenceFont { get; set; }

        /// <summary>
        /// Gets or sets the font used to display the portions of the protein under the mouse cursor.
        /// </summary>
        [Browsable(true)]
        public Font HoverFont { get; set; }
        #endregion

        public event SequenceCoverageFilterEventHandler SequenceCoverageFilter;

        int lineSpacing;

        public DataModel.Protein Protein { get; private set; }
        public ImmutableMap<int, PeptideModification> Modifications { get; private set; }

        int scrollY;
        SequenceCoverageSurface surface;
        NHibernate.ISession session;
        DataFilter viewFilter;

        public SequenceCoverageControl (NHibernate.ISession session, DataModel.Protein protein, DataFilter viewFilter)
        {
            ResizeRedraw = true;
            DoubleBuffered = true;
            AutoScroll = true;

            ResidueGroupSize = 10;
            lineSpacing = 3;

            HeaderColor = Color.FromKnownColor(KnownColor.WindowText);
            OffsetColor = Color.FromKnownColor(KnownColor.GrayText);
            CoveredSequenceColor = Color.FromKnownColor(KnownColor.WindowText);
            UncoveredSequenceColor = Color.FromKnownColor(KnownColor.GrayText);
            HoverSequenceColor = Color.FromKnownColor(KnownColor.HotTrack);
            ModifiedSequenceColor = Color.Red;

            HeaderFont = new Font(new FontFamily(GenericFontFamilies.Monospace), 12);
            OffsetFont = new Font(new FontFamily(GenericFontFamilies.Monospace), 12);
            SequenceFont = new Font(new FontFamily(GenericFontFamilies.Monospace), 12);
            HoverFont = new Font(SequenceFont, FontStyle.Bold | FontStyle.Underline);

            SetData(session, protein, viewFilter);

            surface.SequenceCoverageFilter += (s, e) => OnSequenceCoverageFilter(e);
        }

        public void SetData(NHibernate.ISession session, DataModel.Protein protein, DataFilter viewFilter)
        {
            this.session = session;
            this.viewFilter = viewFilter;

            if (protein == Protein)
            {
                surface.viewFilter = viewFilter;
                Refresh();
                return;
            }

            var query = session.CreateQuery("SELECT pi.Offset+pm.Offset, pm " +
                                            "FROM PeptideInstance pi " +
                                            "JOIN pi.Peptide pep " +
                                            "JOIN pep.Matches psm " +
                                            "JOIN psm.Modifications pm " +
                                            "JOIN FETCH pm.Modification mod " +
                                            "WHERE pi.Protein=" + protein.Id.ToString());

            IList<object[]> queryRows;
            lock (session) queryRows = query.List<object[]>();

            var modifications = new Map<int, PeptideModification>();
            foreach (var queryRow in queryRows)
                modifications[Convert.ToInt32(queryRow[0])] = queryRow[1] as PeptideModification;

            Protein = protein;
            Modifications = new ImmutableMap<int, PeptideModification>(modifications);

            Controls.Clear();

            var rect = ClientRectangle;
            rect.Offset(1, 1);
            rect.Height -= 2;
            rect.Width -= 2;
            surface = new SequenceCoverageSurface(this)
                          {
                              viewFilter = viewFilter,

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

        public void ClearData ()
        {
            Controls.Clear();
        }

        public void ClearSession ()
        {
            ClearData();
            if (session != null && session.IsOpen)
            {
                session.Dispose();
                session = null;
            }
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

        protected void OnSequenceCoverageFilter (DataModel.DataFilter sequenceCoverageFilter)
        {
            if (SequenceCoverageFilter != null)
                SequenceCoverageFilter(this, sequenceCoverageFilter);
        }
    }

    class SequenceCoverageSurface : UserControl
    {
        SequenceCoverageControl owner;
        public DataFilter viewFilter { get; set; }
        string header;
        //List<int> totalCoverageMask = null;
        //List<List<int>> groupCoverageMasks = null;
        SizeF residueBounds;

        public event SequenceCoverageFilterEventHandler SequenceCoverageFilter;

        //ToolTip toolTip;

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

            var mouseLocation = PointToClient(MousePosition);
            int lineNumberUnderMouse = getLineNumberAtPoint(mouseLocation);
            bool onSequenceLine = lineNumberUnderMouse % owner.LineSpacing == 0;
            int residueUnderMouse = onSequenceLine ? getResidueOffsetAtPoint(mouseLocation) : 0;
            bool isProteinInFilter = viewFilter.Protein != null && viewFilter.Protein.Contains(owner.Protein);

            Brush coveredBrush = new SolidBrush(owner.CoveredSequenceColor);
            Brush uncoveredBrush = new SolidBrush(owner.UncoveredSequenceColor);
            Brush hoverBrush = new SolidBrush(owner.HoverSequenceColor);
            Brush modifiedBrush = new SolidBrush(owner.ModifiedSequenceColor);
            foreach (var pair in residueIndexToLocation)
                if (owner.Protein.CoverageMask[pair.Key] > 0)
                {
                    if (onSequenceLine && pair.Key == residueUnderMouse ||
                        isProteinInFilter && viewFilter.AminoAcidOffset.Contains(pair.Key))
                        e.Graphics.DrawString(owner.Protein.Sequence[pair.Key].ToString(), owner.HoverFont, hoverBrush, pair.Value);
                    else if (owner.Modifications.Contains(pair.Key))
                        e.Graphics.DrawString(owner.Protein.Sequence[pair.Key].ToString(), owner.SequenceFont, modifiedBrush, pair.Value);
                    else
                        e.Graphics.DrawString(owner.Protein.Sequence[pair.Key].ToString(), owner.SequenceFont, coveredBrush, pair.Value);
                }
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

        int getLineNumberAtPoint (Point pt)
        {
            return (int) Math.Floor((pt.Y - sequenceLocation.Y) / residueBounds.Height);
        }

        int getResidueOffsetAtPoint (Point pt)
        {
            if (pt.X < sequenceLocation.X) return -1;
            float charOnLine = (pt.X - sequenceLocation.X) / residueBounds.Width;
            // FIXME: if (charOnLine > residuesPerLine) return -1;
            int lineNumber = (int) Math.Floor((float) getLineNumberAtPoint(pt) / owner.LineSpacing);
            return residuesPerLine * lineNumber + (int) Math.Floor(charOnLine - Math.Floor(charOnLine / owner.ResidueGroupSize));
        }

        int lastLineNumber = -1, lastResidueOffset = -1;
        protected override void OnMouseMove (MouseEventArgs e)
        {
            //int offset = getResidueOffsetAtPoint(e.Location);
            //toolTip.Show(String.Format("Offset: {0}", offset), this, 100);

            int currentLineNumber = getLineNumberAtPoint(e.Location);
            int currentResidueOffset = getResidueOffsetAtPoint(e.Location);
            if (lastLineNumber != currentLineNumber || currentResidueOffset != lastResidueOffset)
            {
                lastLineNumber = currentLineNumber;
                lastResidueOffset = currentResidueOffset;
                Refresh();
            }
        }

        protected override void OnMouseDoubleClick (MouseEventArgs e)
        {
            if (SequenceCoverageFilter == null)
                return;

            int currentResidueOffset = getResidueOffsetAtPoint(e.Location);
            if (currentResidueOffset < 0 || currentResidueOffset >= owner.Protein.Length)
                return;

            if (owner.Protein.CoverageMask[currentResidueOffset] == 0)
                return;

            var newDataFilter = new DataModel.DataFilter()
            {
                FilterSource = owner,
                Protein = new List<Protein> { owner.Protein },
                AminoAcidOffset = new List<int> { currentResidueOffset }
            };

            SequenceCoverageFilter(owner, newDataFilter);
        }
    }

    public delegate void SequenceCoverageFilterEventHandler (SequenceCoverageControl sender, DataModel.DataFilter sequenceCoverageFilter);
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