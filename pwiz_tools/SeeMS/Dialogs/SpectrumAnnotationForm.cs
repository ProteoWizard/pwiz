using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace seems
{
    public partial class SpectrumAnnotationForm : DigitalRune.Windows.Docking.DockableForm
    {
        private MassSpectrum currentSpectrum;
        public MassSpectrum CurrentSpectrum { get { return currentSpectrum; } }

        public event EventHandler AnnotationChanged;
        private void OnAnnotationChanged( object sender, EventArgs e )
        {
            if( AnnotationChanged != null )
            {
                AnnotationChanged( this, e );
            }
        }

        public List<IAnnotation> AnnotationList
        {
            get
            {
                return currentSpectrum.AnnotationList;
            }
        }

        public SpectrumAnnotationForm()
        {
            InitializeComponent();
        }

        private void selectIndex( int index )
        {
            annotationsListView.SelectedIndices.Clear();
            if( index > 0)
                annotationsListView.SelectedIndices.Add(index);
        }

        public void UpdateAnnotations( MassSpectrum spectrum )
        {
            currentSpectrum = spectrum;
            Text = TabText = "Annotations for spectrum " + spectrum.Id;
            runOverrideToolStripButton.Text = "Override " + spectrum.Source.Source.Name + " Processing";
            if( annotationsListView.VirtualListSize != currentSpectrum.AnnotationList.Count )
            {
                annotationsListView.VirtualListSize = currentSpectrum.AnnotationList.Count;
                selectIndex( currentSpectrum.AnnotationList.Count - 1 );
            }
        }

        private void peptideFragmentationToolStripMenuItem_Click( object sender, EventArgs e )
        {
            currentSpectrum.AnnotationList.Add( new PeptideFragmentationAnnotation() );
            selectIndex( annotationsListView.VirtualListSize++ );
        }

        private void removeAnnotationButton_Click( object sender, EventArgs e )
        {
            int start = annotationsListView.SelectedIndices[0];
            int count = annotationsListView.SelectedIndices.Count;
            currentSpectrum.AnnotationList.RemoveRange( start, count );
            annotationsListView.VirtualListSize -= count;
            annotationsListView_SelectedIndexChanged( sender, e );
            OnAnnotationChanged( sender, e );
        }

        void annotationsListView_KeyDown( object sender, KeyEventArgs e )
        {
            e.Handled = true;
            if( e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back )
            {
                removeAnnotationButton_Click( sender, e );

            } else if( e.KeyCode == Keys.Space )
            {
                if( annotationsListView.Items.Count > 0 )
                {
                    foreach( int index in annotationsListView.SelectedIndices )
                    {
                        IAnnotation annotation = currentSpectrum.AnnotationList[index];
                        annotation.Enabled = !annotation.Enabled;
                    }
                    OnAnnotationChanged( sender, e );
                }
            } else
                e.Handled = false;
        }

        void ContextMenuStrip_Opening( object sender, CancelEventArgs e )
        {
            if( annotationsListView.SelectedIndices.Count > 0 )
                removeToolStripMenuItem.Enabled = true;
            else
                removeToolStripMenuItem.Enabled = false;
        }

        IAnnotation lastSelectedAnnotation = null;
        private void annotationsListView_SelectedIndexChanged( object sender, EventArgs e )
        {
            if( lastSelectedAnnotation != null )
                lastSelectedAnnotation.OptionsChanged -= new EventHandler( OnAnnotationChanged );

            splitContainer.Panel2.Controls.Clear();
            if( annotationsListView.SelectedIndices.Count > 0 )
            {
                lastSelectedAnnotation = currentSpectrum.AnnotationList[annotationsListView.SelectedIndices[0]];
                splitContainer.Panel2.Controls.Add( lastSelectedAnnotation.OptionsPanel );
                lastSelectedAnnotation.OptionsChanged += new EventHandler( OnAnnotationChanged );

                removeAnnotationButton.Enabled = true;
            } else
                removeAnnotationButton.Enabled = false;
        }

        private void annotationsListView_RetrieveVirtualItem( object sender, RetrieveVirtualItemEventArgs e )
        {
            IAnnotation annotation = currentSpectrum.AnnotationList[e.ItemIndex];
            e.Item = new ListViewItem( annotation.ToString() );

            if( annotation.Enabled )
                e.Item.ForeColor = Control.DefaultForeColor;
            else
                e.Item.ForeColor = Color.Gray;
        }

        private void annotationsListView_Layout( object sender, LayoutEventArgs e )
        {
            annotationsListView.Columns[0].Width = annotationsListView.Width - 10;
        }
    }
}
