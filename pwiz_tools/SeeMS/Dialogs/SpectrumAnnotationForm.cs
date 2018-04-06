﻿//
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

        public IList<IAnnotation> AnnotationList
        {
            get
            {
                return currentSpectrum.AnnotationList;
            }
        }

        public SpectrumAnnotationForm()
        {
            InitializeComponent();

            // the ListView gets a handle even when Panel1Collapsed
            IntPtr handle = annotationsListView.Handle;
        }

        bool clearing = false;
        private void selectIndex( int index )
        {
            clearing = true;
            annotationsListView.SelectedIndices.Clear();
            clearing = false;
            annotationsListView.SelectedIndices.Add(index);
        }

        public void UpdateAnnotations( MassSpectrum spectrum )
        {
            if( currentSpectrum != spectrum )
            {
                currentSpectrum = spectrum;
                Text = TabText = "Annotations for spectrum " + spectrum.Title;
                runOverrideToolStripButton.Text = "Override " + spectrum.Source.Source.Name + " Annotations";

                annotationsListView.VirtualListSize = spectrum.AnnotationList.Count;

                if (spectrum.AnnotationList.Count > 0)
                    selectIndex(spectrum.AnnotationList.Count - 1);
                else
                {
                    splitContainer.Panel2.Controls.Clear();
                    lastSelectedAnnotation = null;
                    clearing = true;
                    annotationsListView.SelectedIndices.Clear();
                    clearing = false;
                }
            }

            annotationsListView.Refresh();
        }

        private void peptideFragmentationToolStripMenuItem_Click( object sender, EventArgs e )
        {
            currentSpectrum.AnnotationList.Add( new PeptideFragmentationAnnotation() );
            selectIndex( annotationsListView.VirtualListSize++ );
            OnAnnotationChanged( sender, e );
        }

        private void removeAnnotationButton_Click( object sender, EventArgs e )
        {
            int start = annotationsListView.SelectedIndices[0];
            int count = annotationsListView.SelectedIndices.Count;
            for (; start < count; ++start)
                currentSpectrum.AnnotationList.RemoveAt(start);
            annotationsListView.VirtualListSize -= count;
            annotationsListView_SelectedIndexChanged( sender, e );
            OnAnnotationChanged( sender, e );
        }

        void annotationsListView_KeyDown( object sender, KeyEventArgs e )
        {
            if( annotationsListView.SelectedIndices.Count > 0 )
            {
                if( e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back )
                {
                    e.Handled = true;
                    removeAnnotationButton_Click( sender, e );

                } else if( e.KeyCode == Keys.Space )
                {
                    e.Handled = true;
                    foreach( int index in annotationsListView.SelectedIndices )
                    {
                        IAnnotation annotation = currentSpectrum.AnnotationList[index];
                        annotation.Enabled = !annotation.Enabled;
                    }
                    OnAnnotationChanged( sender, e );
                    annotationsListView.Refresh();
                }
            }
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

            if( clearing )
                return;

            if( annotationsListView.SelectedIndices.Count > 0 &&
                currentSpectrum.AnnotationList.Count > annotationsListView.SelectedIndices[0] )
            {
                if (lastSelectedAnnotation == null ||
                    lastSelectedAnnotation.GetOptionsPanel(false) != currentSpectrum.AnnotationList[annotationsListView.SelectedIndices[0]].GetOptionsPanel(false))
                {
                    splitContainer.Panel2.Controls.Clear();
                    splitContainer.Panel2.Controls.Add(currentSpectrum.AnnotationList[annotationsListView.SelectedIndices[0]].GetOptionsPanel(false));
                }
                lastSelectedAnnotation = currentSpectrum.AnnotationList[annotationsListView.SelectedIndices[0]];
                lastSelectedAnnotation.OptionsChanged += new EventHandler(OnAnnotationChanged);
                lastSelectedAnnotation.GetOptionsPanel();

                removeAnnotationButton.Enabled = true;
            } else
            {
                splitContainer.Panel2.Controls.Clear();
                lastSelectedAnnotation = null;
                removeAnnotationButton.Enabled = false;
            }
            splitContainer.Panel2.Refresh();
        }

        private void annotationsListView_VirtualItemsSelectionRangeChanged( object sender, ListViewVirtualItemsSelectionRangeChangedEventArgs e )
        {
            annotationsListView_SelectedIndexChanged( sender, e );
        }

        private void annotationsListView_RetrieveVirtualItem( object sender, RetrieveVirtualItemEventArgs e )
        {
            if( currentSpectrum.AnnotationList.Count <= e.ItemIndex )
            {
                e.Item = new ListViewItem( "error" );
                return;
            }

            IAnnotation annotation = currentSpectrum.AnnotationList[e.ItemIndex];
            e.Item = new ListViewItem( new string[] { "", annotation.ToString() } );

            // weird workaround for unchecked checkboxes to display in virtual mode
            e.Item.Checked = true;
            e.Item.Checked = annotation.Enabled;

            if( annotation.Enabled )
                e.Item.ForeColor = Control.DefaultForeColor;
            else
                e.Item.ForeColor = Color.Gray;
        }

        void annotationsListView_MouseClick( object sender, MouseEventArgs e )
        {
            ListViewItem item = annotationsListView.GetItemAt( e.X, e.Y );
            if( item != null && e.X < ( item.Bounds.Left + 16 ) )
            {
                IAnnotation annotation = currentSpectrum.AnnotationList[item.Index];
                annotation.Enabled = !annotation.Enabled;
                OnAnnotationChanged( sender, e );
                annotationsListView.Invalidate( item.Bounds );
            }
        }

        void annotationsListView_MouseDoubleClick( object sender, MouseEventArgs e )
        {
            ListViewItem item = annotationsListView.GetItemAt( e.X, e.Y );
            if( item != null )
            {
                IAnnotation annotation = currentSpectrum.AnnotationList[item.Index];
                annotation.Enabled = !annotation.Enabled;
                OnAnnotationChanged( sender, e );
                annotationsListView.Invalidate( item.Bounds );
            }
        }

        private void annotationsListView_Layout( object sender, LayoutEventArgs e )
        {
            //annotationsListView.Columns[0].Width = annotationsListView.Width - 10;
        }
    }
}
