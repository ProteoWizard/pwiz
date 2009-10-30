//
// $Id$
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
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.CLI;
using pwiz.CLI.msdata;
using pwiz.CLI.analysis;
using ExtensionMethods;

namespace seems
{
    public partial class SpectrumProcessingForm : DockableForm
    {
        private MassSpectrum currentSpectrum;
        public MassSpectrum CurrentSpectrum { get { return currentSpectrum; } }

        public event EventHandler ProcessingChanged;
        private void OnProcessingChanged( object sender, EventArgs e )
        {
            if( ProcessingChanged != null )
            {
                ProcessingChanged( this, e );
            }
        }

        public List<IProcessing> ProcessingList
        {
            get
            {
                return currentSpectrum.ProcessingList;
            }
        }

        public SpectrumList GetProcessingSpectrumList( SpectrumList spectrumList )
        {
            foreach( IProcessing item in ProcessingList )
            {
                if( item.Enabled )
                    spectrumList = item.ProcessList( spectrumList );
            }
            return spectrumList;
        }

        public SpectrumProcessingForm()
        {
            InitializeComponent();

            /*ImageList processingListViewLargeImageList = new ImageList();
            processingListViewLargeImageList.Images.Add( Properties.Resources.Centroider );
            processingListViewLargeImageList.Images.Add( Properties.Resources.Smoother );
            processingListViewLargeImageList.Images.Add( Properties.Resources.Thresholder );
            processingListViewLargeImageList.ImageSize = new Size( 32, 32 );
            processingListViewLargeImageList.TransparentColor = Color.White;*/
        }

        private void selectIndex( int index )
        {
            processingListView.SelectedIndices.Clear();
            if( index >= 0 )
                processingListView.SelectedIndices.Add( index );
        }

        public void UpdateProcessing( MassSpectrum spectrum )
        {
            if( processingListView.VirtualListSize != spectrum.ProcessingList.Count )
            {
                processingListView.VirtualListSize = spectrum.ProcessingList.Count;
                selectIndex( spectrum.ProcessingList.Count - 1 );
            }

            if( currentSpectrum != spectrum )
            {
                currentSpectrum = spectrum;
                Text = TabText = "Processing for spectrum " + spectrum.Id;
                runOverrideToolStripButton.Text = "Override " + spectrum.Source.Source.Name + " Processing";
                processingListView_SelectedIndexChanged( this, EventArgs.Empty );
            }

            processingListView.Refresh();
        }

        IProcessing lastSelectedProcessing = null;
        void processingListView_SelectedIndexChanged( object sender, EventArgs e )
        {
            if( lastSelectedProcessing != null )
                lastSelectedProcessing.OptionsChanged -= new EventHandler( OnProcessingChanged );

            splitContainer.Panel2.Controls.Clear();
            if( processingListView.SelectedIndices.Count > 0 &&
                currentSpectrum.ProcessingList.Count > processingListView.SelectedIndices[0] )
            {
                lastSelectedProcessing = currentSpectrum.ProcessingList[processingListView.SelectedIndices[0]];
                splitContainer.Panel2.Controls.Add( lastSelectedProcessing.OptionsPanel );
                lastSelectedProcessing.OptionsChanged += new EventHandler( OnProcessingChanged );

                moveUpProcessingButton.Enabled = processingListView.SelectedIndices[0] > 0;
                moveDownProcessingButton.Enabled = ( (int) processingListView.SelectedIndices.Back() ) < processingListView.Items.Count - 1;
            } else
            {
                lastSelectedProcessing = null;
                removeProcessingButton.Enabled = false;
                moveUpProcessingButton.Enabled = false;
                moveDownProcessingButton.Enabled = false;
            }
            splitContainer.Panel2.Refresh();
        }

        

        private void processingListView_VirtualItemsSelectionRangeChanged( object sender, ListViewVirtualItemsSelectionRangeChangedEventArgs e )
        {
            processingListView_SelectedIndexChanged( sender, e );
        }

        private void removeProcessingButton_Click( object sender, EventArgs e )
        {
            int start = processingListView.SelectedIndices[0];
            int count = processingListView.SelectedIndices.Count;
            currentSpectrum.ProcessingList.RemoveRange( start, count );
            processingListView.VirtualListSize -= count;
            processingListView_SelectedIndexChanged( sender, e );
            OnProcessingChanged( sender, e );
        }

        private void moveUpProcessingButton_Click( object sender, EventArgs e )
        {
            for( int i = 0; i < processingListView.SelectedIndices.Count; ++i )
            {
                int prevIndex = processingListView.SelectedIndices[i] - 1;
                currentSpectrum.ProcessingList.Insert( prevIndex, currentSpectrum.ProcessingList[processingListView.SelectedIndices[i]] );
                currentSpectrum.ProcessingList.RemoveAt( processingListView.SelectedIndices[i] + 1 );
                processingListView.Items[prevIndex].Selected = true;
                processingListView.Items[prevIndex + 1].Selected = false;
            }
            processingListView_SelectedIndexChanged( sender, e );
            OnProcessingChanged( sender, e );
        }

        private void moveDownProcessingButton_Click( object sender, EventArgs e )
        {
            for( int i = processingListView.SelectedIndices.Count - 1; i >= 0; --i )
            {
                int index = processingListView.SelectedIndices[i];
                IProcessing p = currentSpectrum.ProcessingList[index];
                currentSpectrum.ProcessingList.RemoveAt( index );
                currentSpectrum.ProcessingList.Insert( index + 1, p );
                processingListView.Items[index + 1].Selected = true;
                processingListView.Items[index].Selected = false;
            }
            processingListView_SelectedIndexChanged( sender, e );
            OnProcessingChanged( sender, e );
        }

        void processingListView_KeyDown( object sender, KeyEventArgs e )
        {
            if( processingListView.SelectedIndices.Count > 0 )
            {
                if( e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back )
                {
                    e.Handled = true;
                    removeProcessingButton_Click( sender, e );

                } else if( e.KeyCode == Keys.Space )
                {
                    e.Handled = true;
                    foreach( int index in processingListView.SelectedIndices )
                    {
                        IProcessing processing = currentSpectrum.ProcessingList[index];
                        processing.Enabled = !processing.Enabled;
                    }
                    OnProcessingChanged( sender, e );
                    processingListView.Refresh();
                }
            }
        }

        void ContextMenuStrip_Opening( object sender, CancelEventArgs e )
        {
            if( processingListView.SelectedIndices.Count > 0 )
                removeToolStripMenuItem.Enabled = true;
            else
                removeToolStripMenuItem.Enabled = false;
        }

        private void processingListView_RetrieveVirtualItem( object sender, RetrieveVirtualItemEventArgs e )
        {
            if( currentSpectrum.ProcessingList.Count <= e.ItemIndex )
            {
                e.Item = new ListViewItem( "error" );
                return;
            }

            IProcessing processing = currentSpectrum.ProcessingList[e.ItemIndex];
            e.Item = new ListViewItem( new string[] { "", processing.ToString() } );

            processingListView.Columns[1].Width = Math.Max( processingListView.Columns[1].Width,
                                                            e.Item.SubItems[1].Bounds.Width );

            // weird workaround for unchecked checkboxes to display in virtual mode
            e.Item.Checked = true;
            e.Item.Checked = processing.Enabled;

            if( processing.Enabled )
                e.Item.ForeColor = Control.DefaultForeColor;
            else
                e.Item.ForeColor = Color.Gray;
        }

        private void processingListView_Layout( object sender, LayoutEventArgs e )
        {
            //processingListView.Columns[1].Width = processingListView.Width - processingListView.Columns[0].Width - 1;
        }

        private void chargeStateCalculatorToolStripMenuItem_Click( object sender, EventArgs e )
        {
            currentSpectrum.ProcessingList.Add( new ChargeStateCalculationProcessor() );
            selectIndex( processingListView.VirtualListSize++ );
            OnProcessingChanged( sender, e );
        }

        private void smootherToolStripMenuItem_Click( object sender, EventArgs e )
        {
            currentSpectrum.ProcessingList.Add( new SmoothingProcessor() );
            selectIndex( processingListView.VirtualListSize++ );
            OnProcessingChanged( sender, e );
        }

        private void thresholderToolStripMenuItem_Click( object sender, EventArgs e )
        {
            currentSpectrum.ProcessingList.Add( new ThresholdingProcessor() );
            selectIndex( processingListView.VirtualListSize++ );
            OnProcessingChanged( sender, e );
        }

        private void centroiderToolStripMenuItem_Click( object sender, EventArgs e )
        {
            currentSpectrum.ProcessingList.Add( new PeakPickingProcessor() );
            selectIndex( processingListView.VirtualListSize++ );
            OnProcessingChanged( sender, e );
        }

        void processingListView_MouseClick( object sender, MouseEventArgs e )
        {
            ListViewItem item = processingListView.GetItemAt( e.X, e.Y );
            if( item != null && e.X < ( item.Bounds.Left + 16 ) )
            {
                IProcessing processing = currentSpectrum.ProcessingList[item.Index];
                processing.Enabled = !processing.Enabled;
                OnProcessingChanged( sender, e );
                processingListView.Invalidate( item.Bounds );
            }
        }

        void processingListView_MouseDoubleClick( object sender, MouseEventArgs e )
        {
            ListViewItem item = processingListView.GetItemAt( e.X, e.Y );
            if( item != null )
            {
                IProcessing processing = currentSpectrum.ProcessingList[item.Index];
                processing.Enabled = !processing.Enabled;
                OnProcessingChanged( sender, e );
                processingListView.Invalidate( item.Bounds );
            }
        }
    }
}