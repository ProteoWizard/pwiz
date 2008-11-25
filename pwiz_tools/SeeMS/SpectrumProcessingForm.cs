using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.CLI.msdata;
using pwiz.CLI.analysis;

namespace seems
{
    public partial class SpectrumProcessingForm : DockableForm
    {
        private ProcessingListView<SpectrumList> processingListView;
        public ProcessingListView<SpectrumList> ProcessingListView { get { return processingListView; } }

        public ToolStripButton GlobalProcessingOverrideButton { get { return globalOverrideToolStripButton; } }
        public ToolStripButton RunProcessingOverrideButton { get { return runOverrideToolStripButton; } }

        public SpectrumList GetProcessingSpectrumList( SpectrumList spectrumList )
        {
            return processingListView.ProcessingWrapper( spectrumList );
        }

        private ContextMenuStrip processingListViewContextMenu;
        private ToolStripMenuItem deleteContextItem;

        public event EventHandler ProcessingChanged;
        private void OnProcessingChanged( object sender )
        {
            if( ProcessingChanged != null )
            {
                ProcessingChanged( this, EventArgs.Empty );
            }
        }

        public SpectrumProcessingForm()
        {
            InitializeComponent();

            ImageList processingListViewLargeImageList = new ImageList();
            processingListViewLargeImageList.Images.Add( Properties.Resources.Centroider );
            processingListViewLargeImageList.Images.Add( Properties.Resources.Smoother );
            processingListViewLargeImageList.Images.Add( Properties.Resources.Thresholder );
            processingListViewLargeImageList.ImageSize = new Size( 32, 32 );
            processingListViewLargeImageList.TransparentColor = Color.White;

            processingListViewContextMenu = new ContextMenuStrip();

            ToolStripMenuItem addContextItem = new ToolStripMenuItem( "Add Spectrum Processor",
                                      Properties.Resources.DataProcessing,
                                      new ToolStripMenuItem( "Native Centroider", processingListViewLargeImageList.Images[0], new EventHandler( addNativeCentroider_Click ) ),
                                      new ToolStripMenuItem( "Thresholder", processingListViewLargeImageList.Images[1], new EventHandler( addThresholder_Click ) ),
                                      new ToolStripMenuItem( "Savitzky-Golay Smoother", processingListViewLargeImageList.Images[2], new EventHandler( addSavitzkyGolaySmoother_Click ) ),
                                      new ToolStripMenuItem( "Charge State Calculator", processingListViewLargeImageList.Images[2], new EventHandler( addChargeStateCalculator_Click ) )
                                     );
            processingListViewContextMenu.Items.Add( addContextItem );
            addContextItem.ImageTransparentColor = Color.White;

            deleteContextItem = new ToolStripMenuItem( "Delete", null, new EventHandler( deleteProcessor_Click ) );
            processingListViewContextMenu.Items.Add( deleteContextItem );

            processingListViewContextMenu.Opening += new CancelEventHandler( ContextMenuStrip_Opening );


            processingListView = new ProcessingListView<SpectrumList>();
            processingListView.Name = "processingListView";
            processingListView.Dock = DockStyle.Fill;
            processingListView.ContextMenuStrip = processingListViewContextMenu;
            processingListView.ListView.LargeImageList = processingListViewLargeImageList;
            processingListView.ItemsChanged += new EventHandler( processingListView_ItemsChanged );
            processingListView.ListView.SelectedIndexChanged += new EventHandler( processingListView_SelectedIndexChanged );
            splitContainer.Panel1.Controls.Add( processingListView );
        }

        void processingListView_SelectedIndexChanged( object sender, EventArgs e )
        {
            splitContainer.Panel2.Controls.Clear();
            if( processingListView.ListView.SelectedIndices.Count > 0 )
            {
                ProcessingListViewItem<SpectrumList> processingListViewItem = processingListView.ListView.SelectedItems[0] as ProcessingListViewItem<SpectrumList>;
                splitContainer.Panel2.Controls.Add( processingListViewItem.OptionsPanel );
                processingListViewItem.OptionsChanged += new EventHandler( processingListViewItem_OptionsChanged );
            }
        }

        void processingListViewItem_OptionsChanged( object sender, EventArgs e )
        {
            OnProcessingChanged( sender );
        }

        void processingListView_ItemsChanged( object sender, EventArgs e )
        {
            OnProcessingChanged( sender );
        }

        void ContextMenuStrip_Opening( object sender, CancelEventArgs e )
        {
            if( processingListView.ListView.SelectedItems.Count > 0 )
                deleteContextItem.Enabled = true;
            else
                deleteContextItem.Enabled = false;
        }

        private void addNativeCentroider_Click( object sender, EventArgs e )
        {
            ProcessingListViewItem<SpectrumList> item = new SpectrumList_NativeCentroider_ListViewItem();
            processingListView.Add( item );
            OnProcessingChanged( sender );
        }

        private void addSavitzkyGolaySmoother_Click( object sender, EventArgs e )
        {
            ProcessingListViewItem<SpectrumList> item = new SpectrumList_SavitzkyGolaySmoother_ListViewItem();
            processingListView.Add( item );
            OnProcessingChanged( sender );
        }

        private void addThresholder_Click( object sender, EventArgs e )
        {
            ProcessingListViewItem<SpectrumList> item = new SpectrumList_Thresholder_ListViewItem();
            processingListView.Add( item );
            OnProcessingChanged( sender );
        }

        private void addChargeStateCalculator_Click( object sender, EventArgs e )
        {
            ProcessingListViewItem<SpectrumList> item = new SpectrumList_ChargeStateCalculator_ListViewItem();
            processingListView.Add( item );
            OnProcessingChanged( sender );
        }

        private void deleteProcessor_Click( object sender, EventArgs e )
        {
            processingListView.Remove( processingListView.ListView.SelectedItems[0] );
            processingListView_SelectedIndexChanged( sender, e );
            OnProcessingChanged( sender );
        }

        private ProcessingListViewItem<SpectrumList> getListViewItem(ProcessingMethod method)
        {
            ProcessingListViewItem<SpectrumList> item;

            CVParam action = method.cvParamChild( CVID.MS_data_processing_action );

            switch( action.cvid )
            {
                case CVID.MS_smoothing:
                    item = new SpectrumList_SavitzkyGolaySmoother_ListViewItem();
                    break;

                case CVID.MS_peak_picking:
                    item = new SpectrumList_NativeCentroider_ListViewItem();
                    break;

                case CVID.MS_thresholding:
                    item = new SpectrumList_Thresholder_ListViewItem( method );
                    break;

                case CVID.MS_charge_deconvolution:
                    item =  new SpectrumList_ChargeStateCalculator_ListViewItem();
                    break;

                default:
                    string label = "unknown method";
                    if( method.userParams.Count > 0 )
                        label = method.userParams[0].name + ": " + method.userParams[0].value + " (" + method.userParams[0].type + ")";
                    item = new ProcessingListViewItem<SpectrumList>(label);
                    break;
            }
            return item;
        }

        private MassSpectrum currentSpectrum;
        public MassSpectrum CurrentSpectrum { get { return currentSpectrum; } }

        public void UpdateProcessing( MassSpectrum spectrum )
        {
            currentSpectrum = spectrum;
            runOverrideToolStripButton.Text = "Override " + spectrum.Source.Source.Name + " Processing";
            processingListView.ListView.Clear();

            List<ProcessingListViewItem<SpectrumList>> items = new List<ProcessingListViewItem<SpectrumList>>();

            // populate pre-existing spectrum data processing
            //if( spectrum.Element.dataProcessing != null )
            //    foreach( ProcessingMethod method in spectrum.Element.dataProcessing.processingMethods )
            //        items.Add( new SpectrumList_Preexisting_ListViewItem( method ) );

            // populate SeeMS-originated spectrum data processing
            if( spectrum.DataProcessing != null )
                foreach( ProcessingMethod method in spectrum.DataProcessing.processingMethods )
                    items.Add( getListViewItem( method ) );

            processingListView.AddRange( items );
        }
    }
}