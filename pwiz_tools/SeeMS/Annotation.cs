using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using pwiz.CLI.proteome;
using ZedGraph;

namespace seems
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
        /// the update can use the pointList argument and any existing annotations
        /// to modify how this annotation is presented
        /// </summary>
        void Update( MSGraph.MSPointList pointList, GraphObjList annotations );

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

        public virtual void Update( MSGraph.MSPointList pointList, GraphObjList annotations )
        {
            throw new NotImplementedException();
        }

        internal static AnnotationPanels annotationPanels = new AnnotationPanels();

        public abstract Panel OptionsPanel { get; }
    }

    public class PeptideFragmentationAnnotation : AnnotationBase
    {
        Panel panel = annotationPanels.peptideFragmentationPanel;
        string sequence;
        bool a, b, c, x, y, z;

        public PeptideFragmentationAnnotation()
        {
            sequence = "PEPTIDE";

            annotationPanels.sequenceTextBox.TextChanged += new EventHandler( sequenceTextBox_TextChanged );
            annotationPanels.aCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.bCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.cCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.xCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.yCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.zCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
        }

        public PeptideFragmentationAnnotation( string sequence,
                                               bool a, bool b, bool c,
                                               bool x, bool y, bool z )
        {
            this.sequence = sequence;
            this.a = a; this.b = b; this.c = c;
            this.x = x; this.y = y; this.z = z;

            annotationPanels.sequenceTextBox.TextChanged += new EventHandler( sequenceTextBox_TextChanged );
            annotationPanels.aCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.bCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.cCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.xCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.yCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
            annotationPanels.zCheckBox.CheckedChanged += new EventHandler( checkBox_CheckedChanged );
        }

        void sequenceTextBox_TextChanged( object sender, EventArgs e )
        {
            if( panel.Tag == this )
            {
                sequence = annotationPanels.sequenceTextBox.Text;
                OnOptionsChanged( this, EventArgs.Empty );
            }
        }

        void checkBox_CheckedChanged( object sender, EventArgs e )
        {
            if( panel.Tag == this )
            {
                a = annotationPanels.aCheckBox.Checked;
                b = annotationPanels.bCheckBox.Checked;
                c = annotationPanels.cCheckBox.Checked;
                x = annotationPanels.xCheckBox.Checked;
                y = annotationPanels.yCheckBox.Checked;
                z = annotationPanels.zCheckBox.Checked;
                OnOptionsChanged( this, EventArgs.Empty );
            }
        }

        public override string ToString()
        {
            return "Peptide Fragmentation (" + sequence + ")";
        }

        private void addFragment( GraphObjList list, MSGraph.MSPointList points, char series, int length, int charge, double mz )
        {
            string label = String.Format("{0}{1}{2}", series, length, (charge > 1 ? "+" + charge.ToString() : ""));

            Color color;
            switch( series )
            {
                default: color = Color.Gray; break;
                case 'a': color = Color.YellowGreen; break;
                case 'x': color = Color.Green; break;
                case 'b': color = Color.BlueViolet; break;
                case 'y': color = Color.Blue; break;
                case 'c': color = Color.Orange; break;
                case 'z': color = Color.OrangeRed; break;
            }

            int index = -1;
            if( points != null )
                index = points.LowerBound( mz - 0.5 );

            if( index == -1 || points.ScaledList[index].X > ( mz + 0.5 ) )
            // no matching point: present a "missed" fragment annotation
            {
                color = Color.FromArgb( 115, color ); // transparent to emphasize miss

                LineObj stick = new LineObj( color, mz, 0.1, mz, 1 );
                stick.Location.CoordinateFrame = CoordType.XScaleYChartFraction;
                stick.Line.Width = 2;

                TextObj text = new TextObj( label, mz, 0.1, CoordType.XScaleYChartFraction,
                                            AlignH.Center, AlignV.Bottom );
                text.ZOrder = ZOrder.A_InFront;
                text.FontSpec = new FontSpec( "Arial", 12, color, false, false, false );
                text.FontSpec.Border.IsVisible = false;
                //text.IsClippedToChartRect = true;

                list.Add( stick );
                list.Add( text );

            } else
            // matching point found: present the point as the fragment
            {
                LineObj stick = new LineObj( color, mz, points.ScaledList[index].Y, mz, 0 );
                stick.Location.CoordinateFrame = CoordType.AxisXYScale;
                stick.Line.Width = 2;
                list.Add( stick );

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

        public override void Update( MSGraph.MSPointList points, GraphObjList annotations )
        {
            if( !Enabled )
                return;

            GraphObjList list = annotations;
            Peptide peptide = new Peptide( sequence );
            Fragmentation fragmentation = peptide.fragmentation( true, true );
            for( int charge = 1; charge <= 1; ++charge )
            {
                for( int i = 1; i <= sequence.Length; ++i )
                {
                    if( a ) addFragment( list, points, 'a', i, charge, fragmentation.a( i, charge ) );
                    if( b ) addFragment( list, points, 'b', i, charge, fragmentation.b( i, charge ) );
                    if( y ) addFragment( list, points, 'y', i, charge, fragmentation.y( i, charge ) );
                    if( z ) addFragment( list, points, 'z', i, charge, fragmentation.z( i, charge ) );

                    if( i < sequence.Length )
                    {
                        if( c ) addFragment( list, points, 'c', i, charge, fragmentation.c( i, charge ) );
                        if( x ) addFragment( list, points, 'x', i, charge, fragmentation.x( i, charge ) );
                    }
                }
            }
        }

        public override Panel OptionsPanel
        {
            get
            {
                panel.Tag = null;
                annotationPanels.sequenceTextBox.Text = sequence;
                annotationPanels.aCheckBox.Checked = a;
                annotationPanels.bCheckBox.Checked = b;
                annotationPanels.cCheckBox.Checked = c;
                annotationPanels.xCheckBox.Checked = x;
                annotationPanels.yCheckBox.Checked = y;
                annotationPanels.zCheckBox.Checked = z;
                panel.Tag = this;

                return panel;
            }
        }
    }
}
