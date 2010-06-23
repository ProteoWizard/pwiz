//
// $Id: Annotation.cs 1599 2009-12-04 01:35:39Z brendanx $
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using pwiz.CLI.proteome;
using ZedGraph;
using Forms.Controls;

namespace Forms
{
    public interface IAnnotation
    {
        /// <summary>
        /// Returns a short description of the annotation,
        /// e.g. "Fragmentation (PEPTIDE)"
        /// </summary>
        string ToString();

        /// <summary>
        /// Updates the list of ZedGraph graph objects to display the annotation;
        /// the update can use the graph item, the pointList argument and/or
        /// any existing annotations to modify how this annotation is presented
        /// </summary>
        void Update( GraphItem item, pwiz.MSGraph.MSPointList pointList, GraphObjList annotations );

        /// <summary>
        /// Gets or sets whether the annotation is currently active
        /// </summary>
        bool Enabled { get; set; }

        /// <summary>
        /// Gets the panel containing controls to configure the annotation,
        /// e.g. which ion series to annotate
        /// </summary>
        Panel OptionsPanel { get; }

        /// <summary>
        /// Occurs when the options stored in the panel are changed.
        /// </summary>
        event EventHandler OptionsChanged;
    }

    public class AnnotationFactory
    {
        public static IAnnotation ParseArgument( string arg )
        {
            if( String.IsNullOrEmpty( arg ) )
                return null;

            try
            {
                string[] annotationArgs = arg.Split( " ".ToCharArray() );
                if( annotationArgs.Length > 0 &&
                    annotationArgs[0] == "pfr" ) // peptide fragmentation
                {
                    if( annotationArgs.Length != 5 )
                        throw new ArgumentException( "peptide fragmentation annotation requires 5 arguments" );

                    string sequence = annotationArgs[1];
                    int minCharge = 1;
                    try {
                        minCharge = int.Parse(annotationArgs[2]);
                    }catch (Exception) {}
                    int maxCharge = Math.Max( minCharge, Convert.ToInt32( annotationArgs[3] ) );
                    string seriesArgs = annotationArgs[4];
                    string[] seriesList = seriesArgs.Split( ",".ToCharArray() );
                    bool a, b, c, x, y, z, zRadical;
                    a = b = c = x = y = z = zRadical = false;
                    foreach( string series in seriesList )
                        switch( series )
                        {
                            case "a": a = true; break;
                            case "b": b = true; break;
                            case "c": c = true; break;
                            case "x": x = true; break;
                            case "y": y = true; break;
                            case "z": z = true; break;
                            case "z*": zRadical = true; break;
                        }
                    return (IAnnotation) new PeptideFragmentationAnnotation( sequence, minCharge, maxCharge, a, b, c, x, y, z, zRadical, true, false, true );
                }

                return null;
            } catch( Exception e )
            {
                throw new ArgumentException( "Caught exception parsing command-line arguments: " + e.Message );
            }
        }
    }

    public abstract class AnnotationBase : IAnnotation
    {
        bool enabled;
        public bool Enabled { get { return enabled; } set { enabled = value; } }

        public event EventHandler OptionsChanged;
        protected void OnOptionsChanged( object sender, EventArgs e )
        {
            if( OptionsChanged != null )
                OptionsChanged( sender, e );
        }

        public AnnotationBase()
        {
            enabled = true;
        }

        public virtual void Update( GraphItem item, pwiz.MSGraph.MSPointList pointList, GraphObjList annotations )
        {
            throw new NotImplementedException();
        }

        internal /*static*/ AnnotationPanels annotationPanels = new AnnotationPanels();

        public abstract Panel OptionsPanel { get; }
    }

    public class PeptideFragmentationAnnotation : AnnotationBase
    {
        Panel panel;// = annotationPanels.peptideFragmentationPanel;
        string currentSequence;
        string primarySequence;
        string secondarySequence;
        int min, max;
        int precursorMassType; // 0=mono, 1=avg
        int fragmentMassType; // 0=mono, 1=avg
        bool a, b, c, x, y, z, zRadical;
        bool showLadders;
        bool showMisses;
        bool showLabels;
        Map<string, Set<double>> numFragmentsPredicted;
        Map<string, Set<double>> numFragmentsMatched;

        public PeptideFragmentationAnnotation()
        {
            
            currentSequence = "PEPTIDE";
            min = 1;
            max = 1;
            precursorMassType = 0;
            fragmentMassType = 0;
            showLadders = true;
            showMisses = false;
            showLabels = true;

            annotationPanels.precursorMassTypeComboBox.SelectedIndex = precursorMassType;
            annotationPanels.fragmentMassTypeComboBox.SelectedIndex = fragmentMassType;

            numFragmentsPredicted = new Map<string, Set<double>>();
            numFragmentsMatched = new Map<string, Set<double>>();

            annotationPanels.sequenceTextBox.TextChanged += new EventHandler( sequenceTextBox_TextChanged );
            annotationPanels.minChargeUpDown.ValueChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.maxChargeUpDown.ValueChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.precursorMassTypeComboBox.SelectedIndexChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.fragmentMassTypeComboBox.SelectedIndexChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.aCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.bCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.cCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.xCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.yCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.zCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.zRadicalCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.showFragmentationLaddersCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.showMissesCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );

            panel = annotationPanels.peptideFragmentationPanel;
        }

        public PeptideFragmentationAnnotation( string sequence,
                                               int minCharge, int maxCharge,
                                               bool a, bool b, bool c,
                                               bool x, bool y, bool z, bool zRadical,
                                               bool showFragmentationLadders,
                                               bool showMissedFragments,
                                               bool showLabels )
        {
            this.currentSequence = sequence;
            this.min = minCharge;
            this.max = maxCharge;
            this.a = a; this.b = b; this.c = c;
            this.x = x; this.y = y; this.z = z; this.zRadical = zRadical;
            this.showLadders = showFragmentationLadders;
            this.showMisses = showMissedFragments;
            this.showLabels = showLabels;

            numFragmentsPredicted = new Map<string, Set<double>>();
            numFragmentsMatched = new Map<string, Set<double>>();

            annotationPanels.precursorMassTypeComboBox.SelectedIndex = 0;
            annotationPanels.fragmentMassTypeComboBox.SelectedIndex = 0;

            annotationPanels.sequenceTextBox.TextChanged += new EventHandler( sequenceTextBox_TextChanged );
            annotationPanels.minChargeUpDown.ValueChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.maxChargeUpDown.ValueChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.precursorMassTypeComboBox.SelectedIndexChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.fragmentMassTypeComboBox.SelectedIndexChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.aCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.bCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.cCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.xCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.yCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.zCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.zRadicalCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.showFragmentationLaddersCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.showMissesCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );

            annotationPanels.fragmentInfoGridView.Columns.Clear();

            panel = annotationPanels.peptideFragmentationPanel;
        }

        void sequenceTextBox_TextChanged( object sender, EventArgs e )
        {
            if( panel.Tag == this )
            {
                currentSequence = annotationPanels.sequenceTextBox.Text;
                OnOptionsChanged( this, EventArgs.Empty );
            }
        }

        void toggleSecondaryPeptideCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if( panel.Tag == this) {
                if(currentSequence == primarySequence) 
                    currentSequence = secondarySequence;
                else 
                    currentSequence = primarySequence;
                annotationPanels.sequenceTextBox.Text = currentSequence;
                OnOptionsChanged(this, EventArgs.Empty);
            }
        }

        void checkBox_CheckedChanged( object sender, EventArgs e )
        {
            if( panel.Tag == this )
            {
                min = (int) annotationPanels.minChargeUpDown.Value;
                max = (int) annotationPanels.maxChargeUpDown.Value;

                precursorMassType = annotationPanels.precursorMassTypeComboBox.SelectedIndex;
                fragmentMassType = annotationPanels.fragmentMassTypeComboBox.SelectedIndex;

                showLadders = annotationPanels.showFragmentationLaddersCheckBox.Checked;

                // any control which affects the columns displayed for fragments clears the column list;
                // it gets repopulated on the next call to Update()
                if(!ReferenceEquals(sender, annotationPanels.showMissesCheckBox))
                    annotationPanels.fragmentInfoGridView.Columns.Clear();

                // when showLadders is checked, the ion series checkboxes act like radio buttons:
                // series from the same terminus are grouped together
                if( showLadders && sender is CheckBox )
                {
                    panel.Tag = null;
                    if( ReferenceEquals( sender, annotationPanels.showFragmentationLaddersCheckBox ) )
                    {
                        // uncheck all but the first checked checkbox
                        annotationPanels.bCheckBox.Checked = !annotationPanels.aCheckBox.Checked;
                        annotationPanels.cCheckBox.Checked = !annotationPanels.aCheckBox.Checked && !annotationPanels.bCheckBox.Checked;
                        annotationPanels.yCheckBox.Checked = !annotationPanels.xCheckBox.Checked;
                        annotationPanels.zCheckBox.Checked = !annotationPanels.xCheckBox.Checked && !annotationPanels.yCheckBox.Checked;
                        annotationPanels.zRadicalCheckBox.Checked = !annotationPanels.xCheckBox.Checked && !annotationPanels.yCheckBox.Checked && !annotationPanels.zCheckBox.Checked;
                    }
                    else if( ReferenceEquals( sender, annotationPanels.aCheckBox ) )
                        annotationPanels.bCheckBox.Checked = annotationPanels.cCheckBox.Checked = false;

                    else if( ReferenceEquals( sender, annotationPanels.bCheckBox ) )
                        annotationPanels.aCheckBox.Checked = annotationPanels.cCheckBox.Checked = false;

                    else if( ReferenceEquals( sender, annotationPanels.cCheckBox ) )
                        annotationPanels.aCheckBox.Checked = annotationPanels.bCheckBox.Checked = false;

                    else if( ReferenceEquals( sender, annotationPanels.xCheckBox ) )
                        annotationPanels.yCheckBox.Checked = annotationPanels.zCheckBox.Checked = annotationPanels.zRadicalCheckBox.Checked = false;

                    else if( ReferenceEquals( sender, annotationPanels.yCheckBox ) )
                        annotationPanels.xCheckBox.Checked = annotationPanels.zCheckBox.Checked = annotationPanels.zRadicalCheckBox.Checked = false;

                    else if( ReferenceEquals( sender, annotationPanels.zCheckBox ) )
                        annotationPanels.xCheckBox.Checked = annotationPanels.yCheckBox.Checked = annotationPanels.zRadicalCheckBox.Checked = false;

                    else if( ReferenceEquals( sender, annotationPanels.zRadicalCheckBox ) )
                        annotationPanels.xCheckBox.Checked = annotationPanels.yCheckBox.Checked = annotationPanels.zCheckBox.Checked = false;

                    panel.Tag = this;
                }

                a = annotationPanels.aCheckBox.Checked;
                b = annotationPanels.bCheckBox.Checked;
                c = annotationPanels.cCheckBox.Checked;
                x = annotationPanels.xCheckBox.Checked;
                y = annotationPanels.yCheckBox.Checked;
                z = annotationPanels.zCheckBox.Checked;
                zRadical = annotationPanels.zRadicalCheckBox.Checked;
                showMisses = annotationPanels.showMissesCheckBox.Checked;
                OnOptionsChanged( this, EventArgs.Empty );
            }
        }

        public override string ToString()
        {
            return "Peptide Fragmentation (" + currentSequence + ")";
        }

        public int FragmentMassType {
            get { return fragmentMassType; }
        }

        public string SecondarySequence {
            get { return secondarySequence; }
            set { secondarySequence = value; }
        }

        public void enableSecondarySequenceDisplay(string secondarySeq)
        {
            primarySequence = currentSequence;
            secondarySequence = secondarySeq;
            annotationPanels.toggleSecondaryPeptideCheckBox.Visible = true;
            annotationPanels.toggleSecondaryPeptideCheckBox.CheckedChanged += new EventHandler(toggleSecondaryPeptideCheckBox_CheckedChanged);
        }

        public Peptide Peptide
        {
            get
            {
                Peptide pep; 
                try
                {
                    pep = new Peptide(currentSequence,
                        pwiz.CLI.proteome.ModificationParsing.ModificationParsing_Auto,
                        pwiz.CLI.proteome.ModificationDelimiter.ModificationDelimiter_Brackets);
                }
                catch (Exception)
                {
                    return null;
                }
                return pep;
            }
        }

        private void addFragment( GraphObjList list, pwiz.MSGraph.MSPointList points, string series, int length, int charge, double mz )
        {
            string label = String.Format("{0}{1}{2}", series, length, (charge > 1 ? "+" + charge.ToString() : ""));

            Color color;
            double offset;
            switch( series )
            {
                default: color = Color.Gray; offset = 0.1;  break;
                case "a": color = Color.YellowGreen; offset = 0.1; break;
                case "x": color = Color.Green; offset = 0.12; break;
                case "b": color = Color.BlueViolet; offset = 0.14; break;
                case "y": color = Color.Blue; offset = 0.16; break;
                case "c": color = Color.Orange; offset = 0.18; break;
                case "z": color = Color.OrangeRed; offset = 0.2; break;
                case "z*": color = Color.Crimson; offset = 0.4; break;
            }

            numFragmentsPredicted[series+(charge > 1 ? "+" + charge.ToString() : "")].Add(mz);

            int index = -1;
            if( points != null )
                index = points.LowerBound( mz - 0.5 );

            if( index == -1 || points.ScaledList[index].X > ( mz + 0.5 ) )
            // no matching point: present a "missed" fragment annotation
            {
                if( !showMisses )
                    return;

                color = Color.FromArgb( 115, color ); // transparent to emphasize miss

                LineObj stick = new LineObj( color, mz, offset, mz, 1 );
                stick.Location.CoordinateFrame = CoordType.XScaleYChartFraction;
                stick.Line.Width = 2;
                stick.Line.Style = System.Drawing.Drawing2D.DashStyle.Dot;
                list.Add( stick );

                if( showLabels )
                {
                    TextObj text = new TextObj( label, mz, offset, CoordType.XScaleYChartFraction,
                                                AlignH.Center, AlignV.Bottom );
                    text.ZOrder = ZOrder.A_InFront;
                    text.FontSpec = new FontSpec( "Arial", 12, color, false, false, false );
                    text.FontSpec.Border.IsVisible = false;
                    //text.IsClippedToChartRect = true;
                    list.Add( text );
                }
            } else
            // matching point found: present the point as the fragment
            {
                numFragmentsMatched[series + (charge > 1 ? "+" + charge.ToString() : "")].Add(mz);
                LineObj stick = new LineObj( color, mz, points.ScaledList[index].Y, mz, 0 );
                stick.Location.CoordinateFrame = CoordType.AxisXYScale;
                stick.Line.Width = 2;
                list.Add( stick );

                if( showLabels )
                {
                    // use an existing text point annotation if possible
                    TextObj text = null;
                    foreach( GraphObj obj in list )
                    {
                        if( obj is TextObj &&
                            ( obj.Location.CoordinateFrame == CoordType.AxisXYScale ||
                              obj.Location.CoordinateFrame == CoordType.XScaleYChartFraction ) &&
                            Math.Abs( obj.Location.X - mz ) < 0.5 )
                        {
                            text = obj as TextObj;
                            text.Text = String.Format( "{0}\n{1}", label, text.Text );
                            break;
                        }
                    }

                    if( text == null )
                    {
                        text = new TextObj( label, mz, points.ScaledList[index].Y, CoordType.AxisXYScale,
                                            AlignH.Center, AlignV.Bottom );
                        list.Add( text );
                    }

                    text.ZOrder = ZOrder.A_InFront;
                    text.FontSpec = new FontSpec( "Arial", 12, color, false, false, false );
                    text.FontSpec.Border.IsVisible = false;
                    //text.IsClippedToChartRect = true;
                }
            }
        }

        ///<summary>
        /// Takes a left mz value and right mz value and returns true if both are found in the spectrum.
        /// TODO: make mass tolerance user-configurable (currently hard-coded to 0.5 m/z)
        ///</summary>
        private bool aminoAcidHasFragmentEvidence( pwiz.MSGraph.MSPointList points, double leftMZ, double rightMZ )
        {
            // Search index
            int index = -1;
            bool leftMZFound = false;
            bool righMZFound = false;
            if( points != null )
            {
                // Find the left mz value using a mass tolerance of 0.5 da.
                index = points.LowerBound( leftMZ - 0.5 );
                if( index != -1 && points.ScaledList[index].X <= ( leftMZ + 0.5 ) )
                    leftMZFound = true;
                // Find the right mz value using a mass tolerance of 0.5 da.
                index = points.LowerBound( rightMZ - 0.5 );
                if( index != -1 && points.ScaledList[index].X <= ( rightMZ + 0.5 ) )
                    righMZFound = true;
            }
            // Return if both are found
            return (leftMZFound & righMZFound);
        }

        ///<summary>Adds user requested ion series on top of the chart.</summary>
        private void addIonSeries( GraphObjList list, pwiz.MSGraph.MSPointList points, Peptide peptide, Fragmentation fragmentation, string topSeries, string bottomSeries)
        {
            int ionSeriesChargeState = min;
            string sequence = peptide.sequence;
            ModificationMap modifications = peptide.modifications();

            // Select the color for the ion series.
            Color topSeriesColor;
            Color bottomSeriesColor;
            switch( topSeries )
            {
                default: topSeriesColor = Color.Gray; break;
                case "a": topSeriesColor = Color.YellowGreen; break;
                case "b": topSeriesColor = Color.BlueViolet; break;
                case "c": topSeriesColor = Color.Orange; break;
            }

            switch( bottomSeries )
            {
                default: bottomSeriesColor = Color.Gray; break;
                case "x": bottomSeriesColor = Color.Green; break;
                case "y": bottomSeriesColor = Color.Blue; break;
                case "z": bottomSeriesColor = Color.OrangeRed; break;
                case "z*": bottomSeriesColor = Color.Crimson; break;
            }
            // Ion series offsets. These offsets control where on the chart a particular ion series
            // get displayed
            double topSeriesOffset = 0.025;
            double bottomSeriesOffset = 0.1;
            if( topSeries.Length == 0 )
                bottomSeriesOffset = topSeriesOffset;

            double topSeriesLeftPoint = 0.0;
            double bottomSeriesLeftPoint = 0.0;
            // If the series is a, b, y, z, or z radical
            if( topSeries == "a" || topSeries == "b" || bottomSeries == "y" || bottomSeries == "z" || bottomSeries == "z*" )
            {
                // Step through each fragmentation site
                for( int i = 1; i <= sequence.Length; ++i )
                {
                    // Paint the top series first
                    double rightPoint = 0.0;
                    // Figure out the right mz for this fragmentaion site
                    switch( topSeries )
                    {
                        case "a": rightPoint = fragmentation.a( i, ionSeriesChargeState ); break;
                        case "b": rightPoint = fragmentation.b( i, ionSeriesChargeState ); break;
                        default: rightPoint = 0.0; break;
                    }
                    // If the left mz and right mz are different
                    if( topSeriesLeftPoint != rightPoint )
                    {
                        LineObj line;
                        // Use a dashed line format if there are fragment ions supporting this
                        // amino acid
                        if( !aminoAcidHasFragmentEvidence( points, topSeriesLeftPoint, rightPoint ) )
                        {
                            // Draw the line from previous mz to site to this mz in trasparent color.
                            line = new LineObj( Color.FromArgb( 115,topSeriesColor), topSeriesLeftPoint, topSeriesOffset, rightPoint, topSeriesOffset );
                            line.Line.Style = System.Drawing.Drawing2D.DashStyle.Dash;
                        } else
                        {
                            // Draw the line from previous mz to site to this mz in solid color.
                            line = new LineObj( topSeriesColor, topSeriesLeftPoint, topSeriesOffset, rightPoint, topSeriesOffset );
                        }
                        line.Location.CoordinateFrame = CoordType.XScaleYChartFraction;
                        line.Line.Width = 2;
                        line.ZOrder = ZOrder.F_BehindGrid;
                        line.IsClippedToChartRect = true;
                        list.Add( line );
                        // Add a tick demarking the fragmentation site.
                        LineObj tick = new LineObj( topSeriesColor, rightPoint, (topSeriesOffset-0.015), rightPoint, (topSeriesOffset+0.015) );
                        tick.Location.CoordinateFrame = CoordType.XScaleYChartFraction;
                        tick.Line.Width = 2;
                        tick.IsClippedToChartRect = true;
                        list.Add( tick );
                        // Add a text box in the middle of the left and right mz boundaries
                        StringBuilder label = new StringBuilder(sequence[i - 1].ToString());
                        // Figure out if any mods are there on this amino acid
                        double deltaMass = modifications[i-1].monoisotopicDeltaMass();
                        // Round the mod mass and append it to the amino acid as a string
                        if( deltaMass > 0.0 )
                        {
                            label.Append( "+" + Math.Round( deltaMass ) );
                        } else if( deltaMass < 0.0 )
                        {
                            label.Append( Math.Round( deltaMass ) );
                        }
                        TextObj text = new TextObj( label.ToString(), ( topSeriesLeftPoint + rightPoint ) / 2.0,
                            topSeriesOffset, CoordType.XScaleYChartFraction, AlignH.Center, AlignV.Center);
                        text.ZOrder = ZOrder.A_InFront;
                        text.FontSpec = new FontSpec( "Arial", 13, Color.Black, true, false, false );
                        text.FontSpec.Border.IsVisible = false;
                        text.FontSpec.Fill.Color = Color.White;
                        text.IsClippedToChartRect = true;
                        list.Add( text );
                        topSeriesLeftPoint = rightPoint;
                    }

                    // Time to paint the bottom series
                    // Get the right mz for this series
                    switch( bottomSeries )
                    {
                        case "y": rightPoint = fragmentation.y( i, ionSeriesChargeState ); break;
                        case "z": rightPoint = fragmentation.z( i, ionSeriesChargeState ); break;
                        case "z*": rightPoint = fragmentation.zRadical( i, ionSeriesChargeState ); break;
                        default: rightPoint = 0.0; break;
                    }
                    // If the left and right mz are different
                    if( bottomSeriesLeftPoint != rightPoint )
                    {
                        LineObj line;
                        // Use a dashed line format if there are fragment ions supporting this
                        // amino acid
                        if( !aminoAcidHasFragmentEvidence( points, bottomSeriesLeftPoint, rightPoint ) )
                        {
                            // Draw the line from previous mz to site to this mz in trasparent color.
                            line = new LineObj( Color.FromArgb( 115,bottomSeriesColor ), bottomSeriesLeftPoint, bottomSeriesOffset, rightPoint, bottomSeriesOffset );
                            line.Line.Style = System.Drawing.Drawing2D.DashStyle.Dash;
                        } else
                        {
                            // Draw the line from previous mz to site to this mz in solid color.
                            line = new LineObj( bottomSeriesColor, bottomSeriesLeftPoint, bottomSeriesOffset, rightPoint, bottomSeriesOffset );
                        }
                        line.Location.CoordinateFrame = CoordType.XScaleYChartFraction;
                        line.Line.Width = 2;
                        line.ZOrder = ZOrder.F_BehindGrid;
                        line.IsClippedToChartRect = true;
                        list.Add( line );
                        // Draw a tick mark demarking the fragmentation site
                        LineObj tick = new LineObj( bottomSeriesColor, rightPoint, (bottomSeriesOffset-0.015), rightPoint, (bottomSeriesOffset+0.015) );
                        tick.Location.CoordinateFrame = CoordType.XScaleYChartFraction;
                        tick.Line.Width = 2;
                        tick.IsClippedToChartRect = true;
                        list.Add( tick );
                        // Add the text label containing the amino acid
                        StringBuilder label = new StringBuilder( sequence[sequence.Length - i].ToString() );
                        // Figure out if any mods are there on this amino acid
                        double deltaMass = modifications[sequence.Length - i].monoisotopicDeltaMass();
                        // Round the mod mass and append it to the amino acid as a string
                        if( deltaMass > 0.0 )
                        {
                            label.Append( "+" + Math.Round( deltaMass ) );
                        } else if( deltaMass < 0.0 )
                        {
                            label.Append( Math.Round( deltaMass ) );
                        }
                        TextObj text = new TextObj( label.ToString(),( bottomSeriesLeftPoint + rightPoint ) / 2.0,
                            bottomSeriesOffset, CoordType.XScaleYChartFraction, AlignH.Center, AlignV.Center );
                        text.ZOrder = ZOrder.A_InFront;
                        text.FontSpec = new FontSpec( "Arial", 13, Color.Black, true, false, false );
                        text.FontSpec.Border.IsVisible = false;
                        text.FontSpec.Fill.Color = Color.White;
                        text.IsClippedToChartRect = true;
                        list.Add( text );
                        bottomSeriesLeftPoint = rightPoint;
                    }
                }
            }
            // Handle the C and X series separately
            if( topSeries == "c" || bottomSeries == "x" )
            {
                topSeriesLeftPoint = 0.0;
                bottomSeriesLeftPoint = 0.0;
                for( int i = 1; i < sequence.Length; ++i )
                {
                    double rightPoint = fragmentation.c( i, ionSeriesChargeState );
                    if( topSeriesLeftPoint != rightPoint && topSeries == "c")
                    {
                        LineObj line;
                        // Use a dashed line format if there are fragment ions supporting this
                        // amino acid
                        if( !aminoAcidHasFragmentEvidence( points, topSeriesLeftPoint, rightPoint ) )
                        {
                            // Draw the line from previous mz to site to this mz in trasparent color.
                            line = new LineObj( Color.FromArgb( 115, topSeriesColor ), topSeriesLeftPoint, topSeriesOffset, rightPoint, topSeriesOffset );
                            line.Line.Style = System.Drawing.Drawing2D.DashStyle.Dash;
                        } else
                        {
                            // Draw the line from previous mz to site to this mz in solid color.
                            line = new LineObj( topSeriesColor, topSeriesLeftPoint, topSeriesOffset, rightPoint, topSeriesOffset );
                        }
                        line.Location.CoordinateFrame = CoordType.XScaleYChartFraction;
                        line.Line.Width = 2;
                        line.ZOrder = ZOrder.F_BehindGrid;
                        line.IsClippedToChartRect = true;
                        list.Add( line );
                        // Add a tick to mark the fragmentation site
                        LineObj tick = new LineObj( topSeriesColor, rightPoint, (topSeriesOffset-0.015), rightPoint, (topSeriesOffset+0.015));
                        tick.Location.CoordinateFrame = CoordType.XScaleYChartFraction;
                        tick.Line.Width = 2;
                        tick.IsClippedToChartRect = true;
                        list.Add( tick );

                        // Add a text box in the middle of the left and right mz boundaries
                        StringBuilder label = new StringBuilder( sequence[i - 1].ToString() );
                        // Figure out if any mods are there on this amino acid
                        double deltaMass = modifications[i - 1].monoisotopicDeltaMass();
                        // Round the mod mass and append it to the amino acid as a string
                        if( deltaMass > 0.0 )
                        {
                            label.Append( "+" + Math.Round( deltaMass ) );
                        } else if( deltaMass < 0.0 )
                        {
                            label.Append( Math.Round( deltaMass ) );
                        }
                        TextObj text = new TextObj( label.ToString(),( topSeriesLeftPoint + rightPoint ) / 2.0, 
                            topSeriesOffset, CoordType.XScaleYChartFraction, AlignH.Center, AlignV.Center );
                        text.ZOrder = ZOrder.A_InFront;
                        text.FontSpec = new FontSpec( "Arial", 13, Color.Black, true, false, false );
                        text.FontSpec.Border.IsVisible = false;
                        text.FontSpec.Fill.Color = Color.White;
                        text.IsClippedToChartRect = true;
                        list.Add( text );
                        topSeriesLeftPoint = rightPoint;

                    }
                    rightPoint = fragmentation.x( i, ionSeriesChargeState );
                    if( bottomSeriesLeftPoint != rightPoint && bottomSeries == "x")
                    {
                        LineObj line;
                        // Use a dashed line format if there are fragment ions supporting this
                        // amino acid
                        if( !aminoAcidHasFragmentEvidence( points, bottomSeriesLeftPoint, rightPoint ) )
                        {
                            // Draw the line from previous mz to site to this mz in trasparent color.
                            line = new LineObj( Color.FromArgb( 115, bottomSeriesColor ), bottomSeriesLeftPoint, bottomSeriesOffset, rightPoint, bottomSeriesOffset );
                            line.Line.Style = System.Drawing.Drawing2D.DashStyle.Dash;
                        } else
                        {
                            // Draw the line from previous mz to site to this mz in solid color.
                            line = new LineObj( bottomSeriesColor, bottomSeriesLeftPoint, bottomSeriesOffset, rightPoint, bottomSeriesOffset );
                        }
                        line.Location.CoordinateFrame = CoordType.XScaleYChartFraction;
                        line.Line.Width = 2;
                        line.ZOrder = ZOrder.F_BehindGrid;
                        line.IsClippedToChartRect = true;
                        list.Add( line );
                        LineObj tick = new LineObj( bottomSeriesColor, rightPoint, (bottomSeriesOffset-0.015), rightPoint, (bottomSeriesOffset+0.015) );
                        tick.Location.CoordinateFrame = CoordType.XScaleYChartFraction;
                        tick.Line.Width = 2;
                        tick.IsClippedToChartRect = true;
                        list.Add( tick );
                        // Add the text label containing the amino acid
                        StringBuilder label = new StringBuilder( sequence[sequence.Length - i].ToString() );
                        // Figure out if any mods are there on this amino acid
                        double deltaMass = modifications[sequence.Length - i].monoisotopicDeltaMass();
                        // Round the mod mass and append it to the amino acid as a string
                        if( deltaMass > 0.0 )
                        {
                            label.Append( "+" + Math.Round( deltaMass ) );
                        } else if( deltaMass < 0.0 )
                        {
                            label.Append( Math.Round( deltaMass ) );
                        }
                        TextObj text = new TextObj( label.ToString(),( bottomSeriesLeftPoint + rightPoint ) / 2.0, 
                            bottomSeriesOffset, CoordType.XScaleYChartFraction, AlignH.Center, AlignV.Center );
                        text.ZOrder = ZOrder.A_InFront;
                        text.FontSpec = new FontSpec( "Arial", 13, Color.Black, true, false, false );
                        text.FontSpec.Border.IsVisible = false;
                        text.FontSpec.Fill.Color = Color.White;
                        text.IsClippedToChartRect = true;
                        list.Add( text );
                        bottomSeriesLeftPoint = rightPoint;
                    }
                }
            }
        }

        public override void Update( GraphItem item, pwiz.MSGraph.MSPointList points, GraphObjList annotations )
        {
            if( !Enabled )
                return;

            if( !( item is MassSpectrum ) )
                return; // throw exception?

            GraphObjList list = annotations;
            Peptide peptide;

            try
            {
                peptide = new Peptide( currentSequence,
                    pwiz.CLI.proteome.ModificationParsing.ModificationParsing_Auto,
                    pwiz.CLI.proteome.ModificationDelimiter.ModificationDelimiter_Brackets );
            } catch( Exception )
            {
                return;
            }

            string unmodifiedSequence = peptide.sequence;
            int sequenceLength = unmodifiedSequence.Length;
            Fragmentation fragmentation = peptide.fragmentation( fragmentMassType == 0 ? true : false, true );
            // Keeps track of number of fragments predicted and matched in each ion series
            numFragmentsMatched.Clear();
            numFragmentsPredicted.Clear();
            for( int charge = min; charge <= max; ++charge )
            {
                for( int i = 1; i <= sequenceLength; ++i )
                {
                    if( a ) addFragment( list, points, "a", i, charge, fragmentation.a( i, charge ) );
                    if( b ) addFragment( list, points, "b", i, charge, fragmentation.b( i, charge ) );
                    if( y ) addFragment( list, points, "y", i, charge, fragmentation.y( i, charge ) );
                    if( z ) addFragment( list, points, "z", i, charge, fragmentation.z( i, charge ) );
                    if( zRadical ) addFragment( list, points, "z*", i, charge, fragmentation.zRadical( i, charge ) );

                    if( i < sequenceLength )
                    {
                        if( c ) addFragment( list, points, "c", i, charge, fragmentation.c( i, charge ) );
                        if( x ) addFragment( list, points, "x", i, charge, fragmentation.x( i, charge ) );
                    }
                }
            }

            if( showLadders )
            {
                string topSeries = a ? "a" : b ? "b" : c ? "c" : "";
                string bottomSeries = x ? "x" : y ? "y" : z ? "z" : zRadical ? "z*" : "";
                addIonSeries( list, points, peptide, fragmentation, topSeries, bottomSeries );
            }

            // fill peptide info table
            annotationPanels.peptideInfoGridView.Rows.Clear();

            var spectrum = ( item as MassSpectrum ).Element;
            if( spectrum.precursors.Count > 0 &&
                spectrum.precursors[0].selectedIons.Count > 0 &&
                spectrum.precursors[0].selectedIons[0].hasCVParam( pwiz.CLI.CVID.MS_selected_ion_m_z ) &&
                spectrum.precursors[0].selectedIons[0].hasCVParam( pwiz.CLI.CVID.MS_charge_state ) )
            {
                double selectedMz = (double) spectrum.precursors[0].selectedIons[0].cvParam( pwiz.CLI.CVID.MS_selected_ion_m_z ).value;
                int chargeState = (int) spectrum.precursors[0].selectedIons[0].cvParam( pwiz.CLI.CVID.MS_charge_state ).value;
                double calculatedMass = ( precursorMassType == 0 ? peptide.monoisotopicMass( chargeState ) : peptide.molecularWeight( chargeState ) ) * chargeState;
                double observedMass = selectedMz * chargeState;
                annotationPanels.peptideInfoGridView.Rows.Add( "Calculated mass:", calculatedMass, "Mass error (daltons):", observedMass - calculatedMass );
                annotationPanels.peptideInfoGridView.Rows.Add( "Observed mass:", observedMass, "Mass error (ppm):", ( ( observedMass - calculatedMass ) / calculatedMass ) * 1e6 );
            } else
                annotationPanels.peptideInfoGridView.Rows.Add( "Calculated neutral mass:", precursorMassType == 0 ? peptide.monoisotopicMass() : peptide.molecularWeight() );

            // Adds number of fragments matched/predicted in each ion series
            foreach(var ionSeries in numFragmentsMatched.Keys)
                annotationPanels.peptideInfoGridView.Rows.Add( ionSeries+":", numFragmentsMatched[ionSeries].Count +"/" + numFragmentsPredicted[ionSeries].Count);

            annotationPanels.peptideInfoGridView.Columns[1].DefaultCellStyle.Format = "F4";
            foreach( DataGridViewRow row in annotationPanels.peptideInfoGridView.Rows )
                row.Height = row.InheritedStyle.Font.Height + 2;

            annotationPanels.fragmentInfoGridView.SuspendLayout();
            if( a || b || c || x || y || z || zRadical )
            {
                if( annotationPanels.fragmentInfoGridView.Columns.Count == 0 )
                {
                    #region Add columns for fragment types
                    if( a )
                        for( int charge = min; charge <= max; ++charge )
                            annotationPanels.fragmentInfoGridView.Columns.Add(
                                "a" + charge.ToString(),
                                "a" + ( charge > 1 ? "(+" + charge.ToString() + ")" : "" ) );
                    if( b )
                        for( int charge = min; charge <= max; ++charge )
                            annotationPanels.fragmentInfoGridView.Columns.Add(
                                "b" + charge.ToString(),
                                "b" + ( charge > 1 ? "(+" + charge.ToString() + ")" : "" ) );
                    if( c )
                        for( int charge = min; charge <= max; ++charge )
                            annotationPanels.fragmentInfoGridView.Columns.Add(
                                "c" + charge.ToString(),
                                "c" + ( charge > 1 ? "(+" + charge.ToString() + ")" : "" ) );

                    annotationPanels.fragmentInfoGridView.Columns.Add( "N", "" );
                    annotationPanels.fragmentInfoGridView.Columns.Add( "Sequence", "" );
                    annotationPanels.fragmentInfoGridView.Columns.Add( "C", "" );

                    if( x )
                        for( int charge = min; charge <= max; ++charge )
                            annotationPanels.fragmentInfoGridView.Columns.Add(
                                "x" + charge.ToString(),
                                "x" + ( charge > 1 ? "(+" + charge.ToString() + ")" : "" ) );
                    if( y )
                        for( int charge = min; charge <= max; ++charge )
                            annotationPanels.fragmentInfoGridView.Columns.Add(
                                "y" + charge.ToString(),
                                "y" + ( charge > 1 ? "(+" + charge.ToString() + ")" : "" ) );
                    if( z )
                        for( int charge = min; charge <= max; ++charge )
                            annotationPanels.fragmentInfoGridView.Columns.Add(
                                "z" + charge.ToString(),
                                "z" + ( charge > 1 ? "(+" + charge.ToString() + ")" : "" ) );
                    if( zRadical )
                        for( int charge = min; charge <= max; ++charge )
                            annotationPanels.fragmentInfoGridView.Columns.Add(
                                "z*" + charge.ToString(),
                                "z*" + ( charge > 1 ? "(+" + charge.ToString() + ")" : "" ) );
                #endregion

                    foreach( DataGridViewColumn column in annotationPanels.fragmentInfoGridView.Columns )
                    {
                        column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        if( column.Name != "N" && column.Name != "C" && column.Name != "Sequence" )
                            column.DefaultCellStyle.Format = "F3";
                    }
                }

                while( annotationPanels.fragmentInfoGridView.Rows.Count > sequenceLength )
                    annotationPanels.fragmentInfoGridView.Rows.RemoveAt( annotationPanels.fragmentInfoGridView.Rows.Count - 1 );
                if( sequenceLength - annotationPanels.fragmentInfoGridView.Rows.Count > 0 )
                    annotationPanels.fragmentInfoGridView.Rows.Add( sequenceLength - annotationPanels.fragmentInfoGridView.Rows.Count );
                for( int i = 1; i <= sequenceLength; ++i )
                {
                    int cTerminalLength = sequenceLength - i + 1;
                    var row = annotationPanels.fragmentInfoGridView.Rows[i - 1];
                    var values = new List<object>( 10 );
                    //var row = annotationPanels.fragmentInfoGridView.Rows.Add()];

                    if( a )
                        for( int charge = min; charge <= max; ++charge )
                            values.Add( fragmentation.a( i, charge ) );
                    if( b )
                        for( int charge = min; charge <= max; ++charge )
                            values.Add( fragmentation.b( i, charge ) );
                    if( c )
                        for( int charge = min; charge <= max; ++charge )
                            if( i < sequenceLength )
                                values.Add( fragmentation.c( i, charge ) );
                            else
                                values.Add( "" );

                    values.Add( i );
                    values.Add( unmodifiedSequence[i - 1] );
                    values.Add( cTerminalLength );

                    if( x )
                        for( int charge = min; charge <= max; ++charge )
                            if( i > 1 )
                                values.Add( fragmentation.x( cTerminalLength, charge ) );
                            else
                                values.Add( "" );
                    if( y )
                        for( int charge = min; charge <= max; ++charge )
                            values.Add( fragmentation.y( cTerminalLength, charge ) );
                    if( z )
                        for( int charge = min; charge <= max; ++charge )
                            values.Add( fragmentation.z( cTerminalLength, charge ) );
                    if( zRadical )
                        for( int charge = min; charge <= max; ++charge )
                            values.Add( fragmentation.zRadical( cTerminalLength, charge ) );
                    row.SetValues( values.ToArray() );
                }

                foreach( DataGridViewRow row in annotationPanels.fragmentInfoGridView.Rows )
                {
                    row.Height = row.InheritedStyle.Font.Height + 2;

                    foreach( DataGridViewCell cell in row.Cells )
                    {
                        if( !( cell.Value is double ) )
                            continue;

                        double mz = (double) cell.Value;

                        int index = -1;
                        if( points != null )
                            index = points.LowerBound( mz - 0.5 );

                        if( index == -1 || points.ScaledList[index].X > ( mz + 0.5 ) )
                            continue;
                        cell.Style.Font = new Font( annotationPanels.fragmentInfoGridView.Font, FontStyle.Bold );
                    }
                }
            }
            else
                annotationPanels.fragmentInfoGridView.Rows.Clear();

            annotationPanels.fragmentInfoGridView.ResumeLayout();
        }
        
        public DataGridView FragmentInfoGridView {

            get
            {
                return annotationPanels.fragmentInfoGridView;
            }
        }

        public override Panel OptionsPanel
        {
            get
            {
                // disable update handlers
                panel.Tag = null;

                // toggle docking to fix docking glitches
                annotationPanels.peptideFragmentationPanel.Dock = DockStyle.None;
                annotationPanels.peptideFragmentationPanel.Dock = DockStyle.Fill;

                // set form controls based on model values
                annotationPanels.sequenceTextBox.Text = currentSequence;
                annotationPanels.minChargeUpDown.Value = min;
                annotationPanels.maxChargeUpDown.Value = max;
                annotationPanels.precursorMassTypeComboBox.SelectedIndex = precursorMassType;
                annotationPanels.fragmentMassTypeComboBox.SelectedIndex = fragmentMassType;
                annotationPanels.aCheckBox.Checked = a;
                annotationPanels.bCheckBox.Checked = b;
                annotationPanels.cCheckBox.Checked = c;
                annotationPanels.xCheckBox.Checked = x;
                annotationPanels.yCheckBox.Checked = y;
                annotationPanels.zCheckBox.Checked = z;
                annotationPanels.zRadicalCheckBox.Checked = zRadical;
                annotationPanels.showFragmentationLaddersCheckBox.Checked = showLadders;
                annotationPanels.showMissesCheckBox.Checked = showMisses;

                // enable update handlers
                panel.Tag = this;

                return panel;
            }
        }
    }
}
