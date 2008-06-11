using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;

namespace seems
{
    public partial class SpectrumProcessingForm : DockableForm
    {
        private ProcessingListView<pwiz.CLI.msdata.SpectrumList> processingListView;
        public ProcessingListView<pwiz.CLI.msdata.SpectrumList> ProcessingListView { get { return processingListView; } }

        public pwiz.CLI.msdata.SpectrumList GetProcessingSpectrumList( pwiz.CLI.msdata.SpectrumList spectrumList )
        { return processingListView.ProcessingWrapper( spectrumList ); }

        private ToolStripMenuItem deleteContextItem;

        public SpectrumProcessingForm()
        {
            InitializeComponent();

            processingListView = new ProcessingListView<pwiz.CLI.msdata.SpectrumList>();
            processingListView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
                                AnchorStyles.Left | AnchorStyles.Right;
            processingListView.Location = new Point( 0, 0 );
            processingListView.Size = Size;

            this.Controls.Add( processingListView );

            this.HideOnClose = true;

            ToolStripMenuItem addContextItem = new ToolStripMenuItem( "Add Spectrum Processor",
                                      Properties.Resources.DataProcessing,
                                      new ToolStripMenuItem( "Native Centroider", null, new EventHandler( addNativeCentroider_Click ) ),
                                      new ToolStripMenuItem( "Savitzky Golay Smoother", null, new EventHandler( addSavitzkyGolaySmoother_Click ) )
                                     );
            processingListView.ContextMenuStrip.Items.Add(addContextItem);
            addContextItem.ImageTransparentColor = Color.White;

            deleteContextItem = new ToolStripMenuItem( "Delete", null, new EventHandler( deleteProcessor_Click ) );
            processingListView.ContextMenuStrip.Items.Add( deleteContextItem );

            processingListView.ContextMenuStrip.Opening += new CancelEventHandler( ContextMenuStrip_Opening );

            processingListView.ListView.LargeImageList.Images.Add( Properties.Resources.Centroider );
            processingListView.ListView.LargeImageList.Images.Add( Properties.Resources.Smoother );
            processingListView.ListView.LargeImageList.ImageSize = new Size( 32, 32 );
            processingListView.ListView.LargeImageList.TransparentColor = Color.White;
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
            ProcessingListViewItem<pwiz.CLI.msdata.SpectrumList> item = new SpectrumList_NativeCentroider_ListViewItem();
            item.ImageIndex = 0;
            processingListView.Add( item );
        }

        private void addSavitzkyGolaySmoother_Click( object sender, EventArgs e )
        {
            ProcessingListViewItem<pwiz.CLI.msdata.SpectrumList> item = new SpectrumList_SavitzkyGolaySmoother_ListViewItem();
            item.ImageIndex = 1;
            processingListView.Add( item );
        }

        private void deleteProcessor_Click( object sender, EventArgs e )
        {
            processingListView.Remove( processingListView.ListView.SelectedItems[0] );
        }
    }
}