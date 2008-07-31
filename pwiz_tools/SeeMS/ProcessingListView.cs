using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using pwiz.CLI.msdata;
using pwiz.CLI.analysis;

namespace seems
{
    public class ProcessingListView<ProcessableListType> : UserControl
    {
        private System.Windows.Forms.ListView listView1;
        private Label hintLabel;
        public ListView ListView { get { return listView1; } }

        public event EventHandler ItemsChanged;

        protected void OnItemsChanged()
        {
            if( ItemsChanged != null )
                ItemsChanged( this, EventArgs.Empty );
        }

        public ProcessingListView()
        {
            listView1 = new ListView();

            SuspendLayout();

            AutoScaleDimensions = new SizeF( 6F, 13F );
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add( listView1 );
            Name = "ProcessingListView";
            Size = new Size( 355, 314 );

            listView1.Anchor =  AnchorStyles.Top | AnchorStyles.Bottom |
                                AnchorStyles.Left | AnchorStyles.Right;
            listView1.Location = new Point( 0, 0 );
            listView1.Size = Size;
            listView1.MultiSelect = false;
            listView1.BorderStyle = BorderStyle.None;
            listView1.Name = "listView";
            listView1.TabIndex = 0;
            listView1.UseCompatibleStateImageBehavior = false;
            listView1.LabelWrap = true;
            listView1.AutoArrange = true;
            listView1.Alignment = ListViewAlignment.Top;
            listView1.View = View.Tile;

            ResumeLayout( false );

            ContextMenuStrip = new ContextMenuStrip();

            listView1.LargeImageList = new ImageList();

            ItemsChanged += new EventHandler( ListView_Changed );
            listView1.KeyDown += new KeyEventHandler( ListView_KeyDown );

            // Initialize the drag-and-drop operation when running
            // under Windows XP or a later operating system.
            if( OSFeature.Feature.IsPresent( OSFeature.Themes ) )
            {
                listView1.AllowDrop = true;
                listView1.ItemDrag += new ItemDragEventHandler( ListView_ItemDrag );
                listView1.DragEnter += new DragEventHandler( ListView_DragEnter );
                listView1.DragOver += new DragEventHandler( ListView_DragOver );
                listView1.DragLeave += new EventHandler( ListView_DragLeave );
                listView1.DragDrop += new DragEventHandler( ListView_DragDrop );
                listView1.InsertionMark.Color = Color.Black;
            }

            hintLabel = new Label();
            hintLabel.Text = "Right click to add data processing elements";
            hintLabel.TextAlign = ContentAlignment.MiddleCenter;
            hintLabel.Dock = DockStyle.Fill;
            this.Controls.Add( hintLabel );
            updateHintVisibility();
        }

        // Starts the drag-and-drop operation when an item is dragged.
        void ListView_ItemDrag( object sender, ItemDragEventArgs e )
        {
            ListView.DoDragDrop( e.Item, DragDropEffects.Move );
        }

        void ListView_DragEnter( object sender, DragEventArgs e )
        {
            e.Effect = e.AllowedEffect;
        }

        void ListView_DragDrop( object sender, DragEventArgs e )
        {
            // Retrieve the index of the insertion mark;
            int targetIndex = ListView.InsertionMark.Index;

            // If the insertion mark is not visible, exit the method.
            if( targetIndex == -1 )
            {
                return;
            }

            // If the insertion mark is to the right of the item with
            // the corresponding index, increment the target index.
            if( ListView.InsertionMark.AppearsAfterItem )
            {
                targetIndex++;
            }

            ListViewItem item = e.Data.GetData( e.Data.GetFormats()[0] ) as ListViewItem;
            int oldIndex = item.Index;
            if( oldIndex < targetIndex )
                targetIndex--;
            ListView.Items.RemoveAt( oldIndex );
            ListView.Items.Insert( targetIndex, item );
            OnItemsChanged();
        }

        // Removes the insertion mark when the mouse leaves the control.
        void ListView_DragLeave( object sender, EventArgs e )
        {
            ListView.InsertionMark.Index = -1;
        }

        void ListView_DragOver( object sender, DragEventArgs e )
        {
            // Retrieve the client coordinates of the mouse pointer.
            Point targetPoint = ListView.PointToClient( new Point( e.X, e.Y ) );

            // Retrieve the index of the item closest to the mouse pointer.
            int targetIndex = ListView.InsertionMark.NearestIndex( targetPoint );

            // Confirm that the mouse pointer is not over the dragged item.
            if( targetIndex > -1 )
            {
                // Determine whether the mouse pointer is to the left or
                // the right of the midpoint of the closest item and set
                // the InsertionMark.AppearsAfterItem property accordingly.
                Rectangle itemBounds = ListView.GetItemRect( targetIndex );
                if( targetPoint.X > itemBounds.Left + ( itemBounds.Width / 2 ) )
                {
                    ListView.InsertionMark.AppearsAfterItem = true;
                } else
                {
                    ListView.InsertionMark.AppearsAfterItem = false;
                }
            }

            // Set the location of the insertion mark. If the mouse is
            // over the dragged item, the targetIndex value is -1 and
            // the insertion mark disappears.
            ListView.InsertionMark.Index = targetIndex;
        }

        void ListView_KeyDown( object sender, KeyEventArgs e )
        {
            e.Handled = true;
            if( e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back )
            {
                if( ListView.Items.Count > 0 )
                {
                    foreach( ListViewItem item in ListView.SelectedItems )
                        ListView.Items.Remove( item );
                    OnItemsChanged();
                }
            } else if( e.KeyCode == Keys.Space )
            {
                if( ListView.Items.Count > 0 )
                {
                    foreach( ListViewItem item in ListView.SelectedItems )
                        if( item is ProcessingListViewItem<ProcessableListType> )
                        {
                            ProcessingListViewItem<ProcessableListType> processingItem = item as ProcessingListViewItem<ProcessableListType>;
                            processingItem.Enabled = !processingItem.Enabled;
                            if( processingItem.Enabled )
                                processingItem.ForeColor = Control.DefaultForeColor;
                            else
                                processingItem.ForeColor = Color.Gray;
                        }
                    OnItemsChanged();
                }
            } else
                e.Handled = false;
        }

        void ListView_Changed( object sender, EventArgs e )
        {
            updateHintVisibility();
        }

        void updateHintVisibility()
        {
            hintLabel.Visible = ( ListView.Items.Count == 0 );
        }

        private System.ComponentModel.IContainer components = null;
        protected override void Dispose( bool disposing )
        {
            if( disposing && ( components != null ) )
            {
                components.Dispose();
            }
            base.Dispose( disposing );
        }

        public ProcessableListType ProcessingWrapper( ProcessableListType processableList )
        {
            ProcessableListType list = processableList;
            foreach( ListViewItem item in ListView.Items )
            {
                ProcessingListViewItem<ProcessableListType> processorItem = item as ProcessingListViewItem<ProcessableListType>;
                if( processorItem.Enabled )
                    list = processorItem.ProcessList( list );
            }
            return list;
        }

        public void Add( ProcessingListViewItem<ProcessableListType> item )
        {
            listView1.Items.Add( item );
            OnItemsChanged();
        }

        public void Remove( ListViewItem item )
        {
            listView1.Items.Remove( item );
            OnItemsChanged();
        }
    }

    public class ProcessingListViewItem<ProcessableListType> : ListViewItem
    {
        private bool enabled;
        public bool Enabled { get { return enabled; } set { enabled = value; } }

        public ProcessingListViewItem(string label)
            : base(label)
        {
            enabled = true;
        }

        public virtual ProcessableListType ProcessList( ProcessableListType list )
        {
            return list;
        }
    }

    public class SpectrumList_SavitzkyGolaySmoother_ListViewItem
        : ProcessingListViewItem<SpectrumList>
    {
        public SpectrumList_SavitzkyGolaySmoother_ListViewItem()
            : base("Savitzky-Golay Smoother")
        {
        }

        public override SpectrumList ProcessList( SpectrumList list )
        {
            return new SpectrumList_SavitzkyGolaySmoother( list, new int[] { 1, 2, 3, 4, 5, 6 } );
        }
    }

    public class SpectrumList_NativeCentroider_ListViewItem
        : ProcessingListViewItem<SpectrumList>
    {
        public SpectrumList_NativeCentroider_ListViewItem()
            : base("Native Centroider")
        {
        }

        public override SpectrumList ProcessList( SpectrumList list )
        {
            return new SpectrumList_NativeCentroider( list, new int[] { 1, 2, 3, 4, 5, 6 } );
        }
    }
}
