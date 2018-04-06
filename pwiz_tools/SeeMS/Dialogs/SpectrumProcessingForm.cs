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
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.CLI;
using pwiz.CLI.msdata;
using pwiz.CLI.analysis;
using ExtensionMethods;
using pwiz.Common.Collections;

namespace seems
{
    public partial class SpectrumProcessingForm : DockableForm
    {
        private MassSpectrum currentSpectrum;
        public MassSpectrum CurrentSpectrum { get { return currentSpectrum; } }

        private IList<IProcessing> globalProcessingListOverride;
        public IList<IProcessing> GlobalProcessingListOverride
        {
            get { return globalProcessingListOverride; }
            set
            {
                globalProcessingListOverride = value;
                clearGlobalOverrideToolStripMenuItem.Enabled = value.Count > 0;
                OnProcessingChanged(this, new ProcessingChangedEventArgs(ProcessingChangedEventArgs.Scope.Global, CurrentSpectrum));
            }
        }

        public enum OverrideMode
        {
            /// <summary>
            /// Overrides are applied before per-spectrum processing.
            /// </summary>
            Before,

            /// <summary>
            /// Overrides are applied after per-spectrum processing.
            /// </summary>
            After,

            /// <summary>
            /// Overrides are applied in lieu of per-spectrum processing.
            /// </summary>
            Replace
        }

        private OverrideMode globalOverrideMode;
        public OverrideMode GlobalOverrideMode
        {
            get { return globalOverrideMode; }
            set
            {
                globalOverrideMode = value;
                beforeToolStripMenuItem.Checked = afterToolStripMenuItem.Checked = replaceToolStripMenuItem.Checked = false;
                switch (value)
                {
                    case OverrideMode.Before:
                        beforeToolStripMenuItem.Checked = true;
                        break;
                    case OverrideMode.After:
                        afterToolStripMenuItem.Checked = true;
                        break;
                    case OverrideMode.Replace:
                        replaceToolStripMenuItem.Checked = true;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("value", value, "invalid override mode");
                }
                OnProcessingChanged(this, new ProcessingChangedEventArgs(ProcessingChangedEventArgs.Scope.Global, CurrentSpectrum));
            }
        }

        private readonly Dictionary<ManagedDataSource, IList<IProcessing>> processingListOverrideBySource;

        public class ProcessingChangedEventArgs : EventArgs
        {
            public enum Scope { Global, Run, Spectrum };
            public Scope ChangeScope { get; private set; }
            public MassSpectrum Spectrum { get; private set; }

            public ProcessingChangedEventArgs(Scope scope, MassSpectrum spectrum)
            {
                ChangeScope = scope;
                Spectrum = spectrum;
            }
        }

        public event EventHandler<ProcessingChangedEventArgs> ProcessingChanged;

        // determines the needed scope automatically
        private void OnProcessingChanged( object sender, EventArgs e )
        {
            if (currentSpectrum != null) UpdateProcessing(currentSpectrum);
            processingListView.Refresh();

            if (ProcessingChanged == null) return;

            ProcessingChangedEventArgs.Scope scope;
            if (globalProcessingListOverride.Any())
                scope = ProcessingChangedEventArgs.Scope.Global;
            else if (processingListOverrideBySource.ContainsKey(currentSpectrum.Source))
                scope = ProcessingChangedEventArgs.Scope.Run;
            else
                scope = ProcessingChangedEventArgs.Scope.Spectrum;

            ProcessingChanged(this, new ProcessingChangedEventArgs(scope, CurrentSpectrum));
        }

        // caller specifies the scope
        private void OnProcessingChanged(object sender, ProcessingChangedEventArgs e)
        {
            if (e.Spectrum != null) UpdateProcessing(e.Spectrum);
            processingListView.Refresh();

            if (ProcessingChanged == null) return;
            ProcessingChanged(this, e);
        }

        public IList<IProcessing> ProcessingList
        {
            get
            {
                return currentSpectrum.ProcessingList;
            }
        }

        public SpectrumList GetProcessingSpectrumList( MassSpectrum spectrum, SpectrumList spectrumList )
        {
            IList<IProcessing> usedProcessingList = spectrum.ProcessingList.ToList();
            if (globalProcessingListOverride.Any())
            {
                if (replaceToolStripMenuItem.Checked)
                    usedProcessingList = globalProcessingListOverride;
                else if (beforeToolStripMenuItem.Checked)
                    usedProcessingList.InsertRange(0, globalProcessingListOverride);
                else if (afterToolStripMenuItem.Checked)
                    usedProcessingList.AddRange(globalProcessingListOverride);
            }
            else if (processingListOverrideBySource.ContainsKey(spectrum.Source))
                usedProcessingList = processingListOverrideBySource[spectrum.Source];

            foreach (IProcessing item in usedProcessingList)
            {
                if( item.Enabled )
                    spectrumList = item.ProcessList( spectrumList );
            }
            return spectrumList;
        }

        public SpectrumProcessingForm()
        {
            InitializeComponent();

            globalProcessingListOverride = new List<IProcessing>();
            processingListOverrideBySource = new Dictionary<ManagedDataSource, IList<IProcessing>>();

            /*ImageList processingListViewLargeImageList = new ImageList();
            processingListViewLargeImageList.Images.Add( Properties.Resources.Centroider );
            processingListViewLargeImageList.Images.Add( Properties.Resources.Smoother );
            processingListViewLargeImageList.Images.Add( Properties.Resources.Thresholder );
            processingListViewLargeImageList.ImageSize = new Size( 32, 32 );
            processingListViewLargeImageList.TransparentColor = Color.White;*/
        }

        private void selectIndex(int index)
        {
            processingListView.SelectedIndices.Clear();
            if (index >= 0)
            {
                processingListView.SelectedIndices.Add(index);
                globalOverrideToolStripButton.Enabled = runOverrideToolStripButton.Enabled = true;
            }
        }

        public void UpdateProcessing( MassSpectrum spectrum )
        {
            int newVirtualSize = spectrum.ProcessingList.Count;
            if (globalOverrideMode == OverrideMode.Replace)
                newVirtualSize = globalProcessingListOverride.Count;
            else
                newVirtualSize += globalProcessingListOverride.Count;

            if (processingListView.VirtualListSize != newVirtualSize)
            {
                processingListView.VirtualListSize = newVirtualSize;
                selectIndex(newVirtualSize - 1);
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
                currentSpectrum.ProcessingList.Count > virtualIndexToSpectrumIndex((int)processingListView.SelectedIndices.Back()))
            {
                int firstSpectrumIndex = virtualIndexToSpectrumIndex(processingListView.SelectedIndices[0]);
                int lastSpectrumIndex = virtualIndexToSpectrumIndex((int)processingListView.SelectedIndices.Back());

                lastSelectedProcessing = getProcessingAtIndex(processingListView.SelectedIndices[0]);
                splitContainer.Panel2.Controls.Add( lastSelectedProcessing.OptionsPanel );
                lastSelectedProcessing.OptionsChanged += new EventHandler( OnProcessingChanged );

                moveUpProcessingButton.Enabled = firstSpectrumIndex > 0;
                moveDownProcessingButton.Enabled = lastSpectrumIndex >= 0 && lastSpectrumIndex < processingListView.Items.Count - 1;
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

        // returns -1 if the virtual index does not correspond with a spectrum index
        int virtualIndexToSpectrumIndex(int index)
        {
            if (globalOverrideMode == OverrideMode.Replace)
            {
                return -1;
            }

            if (globalOverrideMode == OverrideMode.Before)
            {
                if (index < globalProcessingListOverride.Count)
                    return -1;
                return index - globalProcessingListOverride.Count;
            }

            if (globalOverrideMode == OverrideMode.After)
            {
                if (index >= currentSpectrum.ProcessingList.Count)
                    return -1;
                return index;
            }

            return index;
        }

        private void removeProcessingButton_Click( object sender, EventArgs e )
        {
            int start = virtualIndexToSpectrumIndex(processingListView.SelectedIndices[0]);
            if (start < 0)
                return;
            int count = Math.Min(currentSpectrum.ProcessingList.Count, processingListView.SelectedIndices.Count);
            for (int i = start, end = start + count; i < end; ++i)
                currentSpectrum.ProcessingList.RemoveAt(i);
            processingListView.VirtualListSize -= count;
            processingListView_SelectedIndexChanged( sender, e );
            if (processingListView.VirtualListSize == 0)
                globalOverrideToolStripButton.Enabled = runOverrideToolStripButton.Enabled = false;
            OnProcessingChanged( sender, e );
        }

        private void moveUpProcessingButton_Click( object sender, EventArgs e )
        {
            for( int i = 0; i < processingListView.SelectedIndices.Count; ++i )
            {
                int prevIndex = processingListView.SelectedIndices[i] - 1;
                currentSpectrum.ProcessingList.Insert( prevIndex, currentSpectrum.ProcessingList[virtualIndexToSpectrumIndex(processingListView.SelectedIndices[i])] );
                currentSpectrum.ProcessingList.RemoveAt(virtualIndexToSpectrumIndex(processingListView.SelectedIndices[i] + 1));
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
                currentSpectrum.ProcessingList.RemoveAt(virtualIndexToSpectrumIndex(index));
                currentSpectrum.ProcessingList.Insert(virtualIndexToSpectrumIndex(index + 1), p);
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
                        IProcessing processing = getProcessingAtIndex(index);
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

        private IProcessing getProcessingAtIndex(int index)
        {
            if (currentSpectrum.ProcessingList.Count + globalProcessingListOverride.Count <= index)
                return null;

            if (globalOverrideMode == OverrideMode.Replace)
            {
                if (globalProcessingListOverride.Count <= index)
                    return null;
                return globalProcessingListOverride[index];
            }

            if (globalOverrideMode == OverrideMode.Before)
            {
                if (index < globalProcessingListOverride.Count)
                    return globalProcessingListOverride[index];
                return currentSpectrum.ProcessingList[index - globalProcessingListOverride.Count];
            }

            if (globalOverrideMode == OverrideMode.After)
            {
                if (index >= currentSpectrum.ProcessingList.Count)
                    return globalProcessingListOverride[index - currentSpectrum.ProcessingList.Count];
                return currentSpectrum.ProcessingList[index];
            }

            throw new ArgumentException("invalid global override mode");
        }

        private void processingListView_RetrieveVirtualItem( object sender, RetrieveVirtualItemEventArgs e )
        {
            var processing = getProcessingAtIndex(e.ItemIndex);

            if (processing == null)
            {
                e.Item = new ListViewItem(new string[] {"", "error"});
                return;
            }
            else
                e.Item = new ListViewItem(new string[] {"", processing.ToString()});

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

        private void lockmassRefinerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            currentSpectrum.ProcessingList.Add(new LockmassRefinerProcessor());
            selectIndex(processingListView.VirtualListSize++);
            OnProcessingChanged(sender, e);
        }

        void processingListView_MouseClick( object sender, MouseEventArgs e )
        {
            ListViewItem item = processingListView.GetItemAt( e.X, e.Y );
            if( item != null && e.X < ( item.Bounds.Left + 16 ) )
            {
                IProcessing processing = getProcessingAtIndex(item.Index);
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
                IProcessing processing = getProcessingAtIndex(item.Index);
                processing.Enabled = !processing.Enabled;
                OnProcessingChanged( sender, e );
                processingListView.Invalidate( item.Bounds );
            }
        }

        private void global_withAllListedProcessorsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            globalProcessingListOverride = ProcessingList;
            clearGlobalOverrideToolStripMenuItem.Enabled = true;
            OnProcessingChanged(sender, new ProcessingChangedEventArgs(ProcessingChangedEventArgs.Scope.Global, CurrentSpectrum));
        }

        private void global_withCurrentlySelectedProcessorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            globalProcessingListOverride = new List<IProcessing> { lastSelectedProcessing };
            clearGlobalOverrideToolStripMenuItem.Enabled = true;
            OnProcessingChanged(sender, new ProcessingChangedEventArgs(ProcessingChangedEventArgs.Scope.Global, CurrentSpectrum));
        }

        private void clearGlobalOverrideToolStripMenuItem_Click(object sender, EventArgs e)
        {
            globalProcessingListOverride.Clear();
            clearGlobalOverrideToolStripMenuItem.Enabled = false;
            OnProcessingChanged(sender, new ProcessingChangedEventArgs(ProcessingChangedEventArgs.Scope.Global, CurrentSpectrum));
        }

        private void run_withAllListedProcessorsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            processingListOverrideBySource[currentSpectrum.Source] = ProcessingList;
            OnProcessingChanged(sender, new ProcessingChangedEventArgs(ProcessingChangedEventArgs.Scope.Run, CurrentSpectrum));
        }

        private void run_withCurrentlySelectedProcessorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            processingListOverrideBySource[currentSpectrum.Source] = new List<IProcessing> {lastSelectedProcessing};
            OnProcessingChanged(sender, new ProcessingChangedEventArgs(ProcessingChangedEventArgs.Scope.Run, CurrentSpectrum));
        }

        private void clearRunOverrideToolStripMenuItem_Click(object sender, EventArgs e)
        {
            processingListOverrideBySource.Remove(currentSpectrum.Source);
            OnProcessingChanged(sender, new ProcessingChangedEventArgs(ProcessingChangedEventArgs.Scope.Run, CurrentSpectrum));
        }

        private void runOverrideToolStripButton_DropDownOpening(object sender, EventArgs e)
        {
            clearRunOverrideToolStripMenuItem.Enabled = processingListOverrideBySource.ContainsKey(currentSpectrum.Source);
        }

        private void global_overrideModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!(sender is ToolStripMenuItem)) return;
            var button = sender as ToolStripMenuItem;
            if (!button.Checked) { button.Checked = true; return; }

            if (beforeToolStripMenuItem == button)
                GlobalOverrideMode = OverrideMode.Before;
            else if(afterToolStripMenuItem == button)
                GlobalOverrideMode = OverrideMode.After;
            else if (replaceToolStripMenuItem == button)
                GlobalOverrideMode = OverrideMode.Replace;

            OnProcessingChanged(sender, new ProcessingChangedEventArgs(ProcessingChangedEventArgs.Scope.Global, CurrentSpectrum));
        }
    }
}