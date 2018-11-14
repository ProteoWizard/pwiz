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
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using pwiz.CLI.cv;
using pwiz.CLI.data;
using pwiz.CLI.msdata;
using pwiz.CLI.analysis;

namespace seems
{
    public class ProcessingListView<ProcessableListType> : UserControl
    {
        private System.Windows.Forms.ListView listView1;
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
            listView1.View = View.Details;

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
        }

        #region Drag and Drop capability

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
#endregion

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

        public DataProcessing DataProcessing
        {
            get
            {
                DataProcessing dp = new DataProcessing();
                foreach( ProcessingListViewItem<ProcessableListType> item in ListView.Items )
                    dp.processingMethods.Add( item.ToProcessingMethod() );
                return dp;
            }
        }

        public void Add( ProcessingListViewItem<ProcessableListType> item )
        {
            listView1.Items.Add( item );
            OnItemsChanged();
        }

        public void AddRange( IEnumerable<ProcessingListViewItem<ProcessableListType>> items )
        {
            foreach( ProcessingListViewItem<ProcessableListType> item in items )
                listView1.Items.Add( item );
            OnItemsChanged();
        }

        public void Remove( ListViewItem item )
        {
            listView1.Items.Remove( item );
            OnItemsChanged();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // ProcessingListView
            // 
            this.Name = "ProcessingListView";
            this.ResumeLayout( false );

        }
    }

    public class ProcessingListViewItem<ProcessableListType> : ListViewItem
    {
        private bool enabled;
        public bool Enabled { get { return enabled; } set { enabled = value; } }

        public new virtual int ImageIndex { get { return 0; } }

        public virtual CVID CVID { get { return CVID.CVID_Unknown; } }

        private FlowLayoutPanel optionsPanel;
        public FlowLayoutPanel OptionsPanel { get { return optionsPanel; } }

        public event EventHandler OptionsChanged;

        protected void OnOptionsChangedHandler( object sender, EventArgs e )
        {
            OnOptionsChanged();
        }

        protected void OnOptionsChanged()
        {
            if( OptionsChanged != null )
                OptionsChanged( this, EventArgs.Empty );
        }

        public virtual ProcessingMethod ToProcessingMethod()
        {
            ProcessingMethod pm = new ProcessingMethod();
            pm.set( CVID );
            return pm;
        }

        public ProcessingListViewItem(string label)
            : base(label)
        {
            base.ImageIndex = ImageIndex;
            enabled = true;
            optionsPanel = new FlowLayoutPanel();
        }

        public virtual ProcessableListType ProcessList( ProcessableListType list )
        {
            return list;
        }
    }

    public class SpectrumList_Preexisting_ListViewItem
        : ProcessingListViewItem<SpectrumList>
    {
        private CVParam methodParam;

        public SpectrumList_Preexisting_ListViewItem( ProcessingMethod method )
            : base( method.cvParamChild( CVID.MS_data_processing_action ).name )
        {
            methodParam = method.cvParamChild( CVID.MS_data_processing_action );
        }

        public override CVID CVID { get { return methodParam.cvid; } }
    }

    public class SpectrumList_SavitzkyGolaySmoother_ListViewItem
        : ProcessingListViewItem<SpectrumList>
    {
        public SpectrumList_SavitzkyGolaySmoother_ListViewItem()
            : base("Savitzky-Golay Smoother")
        {
        }

        public override int ImageIndex { get { return 1; } }

        public override CVID CVID { get { return CVID.MS_smoothing; } }

        public override SpectrumList ProcessList( SpectrumList list )
        {
            return new SpectrumList_Smoother( list, null, new int[] { 1, 2, 3, 4, 5, 6 } );
        }
    }

    public class SpectrumList_ECD_ETD_PrecursorFilter : SpectrumListWrapper
    {
        public SpectrumList_ECD_ETD_PrecursorFilter( SpectrumList inner )
            : base(inner)
        {
            ProcessingMethod method = new ProcessingMethod();
            method.order = this.dataProcessing().processingMethods.Count;
            method.set( CVID.MS_charge_stripping );
            this.dataProcessing().processingMethods.Add( method );
        }

        public override Spectrum spectrum( int index, bool getBinaryData )
        {
            Spectrum s = base.spectrum( index, true );

            if( s.cvParam( CVID.MS_MSn_spectrum ).empty() ||
                (int) s.cvParam( CVID.MS_ms_level ).value == 1 )
            {
                if( !getBinaryData )
                    s.binaryDataArrays.Clear();
                return s;
            }

            PrecursorList pl = s.precursors;

            IList<double> mzArray = s.getMZArray().data.Storage();
            IList<double> intensityArray = s.getIntensityArray().data.Storage();
            PointDataMap<double> mziMap = new PointDataMap<double>();
            for( int i = 0; i < (int) s.defaultArrayLength; ++i )
                mziMap.Insert( mzArray[i], intensityArray[i] );

            Set<double> pointsToRemove = new Set<double>();

            foreach( Precursor p in pl )
            {
                foreach( SelectedIon si in p.selectedIons )
                {
                    double mz = (double) si.cvParam( CVID.MS_selected_ion_m_z ).value;
                    PointDataMap<double>.Enumerator itr = mziMap.LowerBound( mz - 4.0 );
                    if( itr != null && itr.IsValid )
                    {
                        while( itr.IsValid && itr.Current.Key - mz < 4.0 )
                        {
                            pointsToRemove.Add( itr.Current.Key );
                            itr.MoveNext();
                        }
                    }

                    CVParam chargeParam = si.cvParam(CVID.MS_charge_state);
                    if( !chargeParam.empty() )
                    {
                        int z = (int) chargeParam.value;
                        for( int i = 1; i < z; ++i )
                        {
                            double strippedMz = ( mz * z ) / ( z - i );
                            itr = mziMap.LowerBound( strippedMz - 4.0 );
                            if( itr != null && itr.IsValid )
                            {
                                while( itr.IsValid && itr.Current.Key - strippedMz < 4.0 )
                                {
                                    pointsToRemove.Add( itr.Current.Key );
                                    itr.MoveNext();
                                }
                            }
                        }
                    }
                }
            }

            if( getBinaryData )
            {
                mzArray.Clear();
                intensityArray.Clear();
                foreach( PointDataMap<double>.MapPair pair in mziMap )
                {
                    if( pointsToRemove.Contains( pair.Key ) )
                        continue;

                    mzArray.Add( pair.Key );
                    intensityArray.Add( pair.Value );
                }
                s.defaultArrayLength = (ulong) mzArray.Count;
            } else
            {
                s.binaryDataArrays.Clear();
                s.defaultArrayLength -= (ulong) pointsToRemove.Count;
            }

            return s;
        }
    }

    public class SpectrumList_ECD_ETD_PrecursorFilter_ListViewItem
        : ProcessingListViewItem<SpectrumList>
    {
        public SpectrumList_ECD_ETD_PrecursorFilter_ListViewItem()
            : base( "ECD/ETD Precursor Filter" )
        {
        }

        public override int ImageIndex { get { return 0; } }

        public override CVID CVID { get { return CVID.MS_charge_deconvolution; } }

        public override SpectrumList ProcessList( SpectrumList list )
        {
            return new SpectrumList_ECD_ETD_PrecursorFilter( list );
        }
    }

    public class SpectrumList_Thresholder_ListViewItem
        : ProcessingListViewItem<SpectrumList>
    {
        private ComboBox thresholdingTypeComboBox;
        private ComboBox thresholdingOrientationComboBox;
        private TextBox thresholdTextBox;

        public SpectrumList_Thresholder_ListViewItem()
            : base( "Thresholder" )
        {
            initializeComponents();
        }

        public SpectrumList_Thresholder_ListViewItem( ProcessingMethod method )
            : base( "Thresholder" )
        {
            initializeComponents();

            // parse type, orientation, and threshold from method
            UserParam param = method.userParam( "threshold" );
            if( param.type == "SeeMS" )
                thresholdTextBox.Text = param.value;

            param = method.userParam( "type" );
            if( param.type == "SeeMS" )
                thresholdingTypeComboBox.SelectedIndex = (int) param.value;

            param = method.userParam( "orientation" );
            if( param.type == "SeeMS" )
                thresholdingOrientationComboBox.SelectedIndex = (int) param.value;
        }

        private void initializeComponents()
        {
            KeyValuePair<string, ThresholdFilter.ThresholdingBy_Type> nameTypePair;
            KeyValuePair<string, ThresholdFilter.ThresholdingOrientation> nameOrientationPair;

            thresholdingTypeComboBox = new ComboBox();
            thresholdingTypeComboBox.DisplayMember = "Key";
            thresholdingTypeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            thresholdingTypeComboBox.SelectedIndexChanged += new EventHandler( OnOptionsChangedHandler );

            thresholdingOrientationComboBox = new ComboBox();
            thresholdingOrientationComboBox.DisplayMember = "Key";
            thresholdingOrientationComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            thresholdingOrientationComboBox.SelectedIndexChanged += new EventHandler( OnOptionsChangedHandler );

            thresholdTextBox = new TextBox();
            thresholdTextBox.TextChanged += new EventHandler( OnOptionsChangedHandler );

            // apply the threshold with this method
            nameTypePair = new KeyValuePair<string, ThresholdFilter.ThresholdingBy_Type>( "Count", ThresholdFilter.ThresholdingBy_Type.ThresholdingBy_Count );
            thresholdingTypeComboBox.Items.Add( nameTypePair );
            nameTypePair = new KeyValuePair<string, ThresholdFilter.ThresholdingBy_Type>( "Absolute Intensity", ThresholdFilter.ThresholdingBy_Type.ThresholdingBy_AbsoluteIntensity );
            thresholdingTypeComboBox.Items.Add( nameTypePair );
            nameTypePair = new KeyValuePair<string, ThresholdFilter.ThresholdingBy_Type>( "Fraction of TIC", ThresholdFilter.ThresholdingBy_Type.ThresholdingBy_FractionOfTotalIntensity );
            thresholdingTypeComboBox.Items.Add( nameTypePair );
            nameTypePair = new KeyValuePair<string, ThresholdFilter.ThresholdingBy_Type>( "Fraction of BPI", ThresholdFilter.ThresholdingBy_Type.ThresholdingBy_FractionOfBasePeakIntensity );
            thresholdingTypeComboBox.Items.Add( nameTypePair );
            nameTypePair = new KeyValuePair<string, ThresholdFilter.ThresholdingBy_Type>( "Fraction cutoff of TIC", ThresholdFilter.ThresholdingBy_Type.ThresholdingBy_FractionOfTotalIntensityCutoff );
            thresholdingTypeComboBox.Items.Add( nameTypePair );
            thresholdingTypeComboBox.SelectedIndex = 0;

            // apply the threshold according to this orientation
            nameOrientationPair = new KeyValuePair<string, ThresholdFilter.ThresholdingOrientation>( "Most Intense", ThresholdFilter.ThresholdingOrientation.Orientation_MostIntense );
            thresholdingOrientationComboBox.Items.Add( nameOrientationPair );
            nameOrientationPair = new KeyValuePair<string, ThresholdFilter.ThresholdingOrientation>( "Least Intense", ThresholdFilter.ThresholdingOrientation.Orientation_LeastIntense );
            thresholdingOrientationComboBox.Items.Add( nameOrientationPair );
            thresholdingOrientationComboBox.SelectedIndex = 0;

            // parse as double and apply as threshold
            thresholdTextBox.Text = "0.0";

            OptionsPanel.Controls.Add( thresholdingTypeComboBox );
            OptionsPanel.Controls.Add( thresholdingOrientationComboBox );
            OptionsPanel.Controls.Add( thresholdTextBox );
        }

        public override ProcessingMethod ToProcessingMethod()
        {
            ProcessingMethod pm = base.ToProcessingMethod();
            pm.userParams.Add( new UserParam( "threshold", thresholdTextBox.Text, "SeeMS" ) );
            pm.userParams.Add( new UserParam( "type", thresholdingTypeComboBox.SelectedIndex.ToString(), "SeeMS" ) );
            pm.userParams.Add( new UserParam( "orientation", thresholdingOrientationComboBox.SelectedIndex.ToString(), "SeeMS" ) );
            return pm;
        }

        public override int ImageIndex { get { return 2; } }

        public override CVID CVID { get { return CVID.MS_thresholding; } }

        public override SpectrumList ProcessList( SpectrumList list )
        {
            double threshold;
            if( !Double.TryParse( thresholdTextBox.Text, out threshold ) )
                threshold = 0;

            return new SpectrumList_PeakFilter(
                list, new ThresholdFilter(
                ( (KeyValuePair<string, ThresholdFilter.ThresholdingBy_Type>) thresholdingTypeComboBox.SelectedItem ).Value,
                threshold,
                ( (KeyValuePair<string, ThresholdFilter.ThresholdingOrientation>) thresholdingOrientationComboBox.SelectedItem ).Value ) );
        }
    }

    public class SpectrumList_ChargeStateCalculator_ListViewItem
        : ProcessingListViewItem<SpectrumList>
    {
        private CheckBox overrideExistingChargeStateCheckBox;
        private NumericUpDown maxMultipleChargeUpDown;
        private NumericUpDown minMultipleChargeUpDown;
        private TextBox fractionBelowPrecursorForSinglyChargedTextBox;

        public SpectrumList_ChargeStateCalculator_ListViewItem()
            : base( "Charge State Calculator" )
        {
            overrideExistingChargeStateCheckBox = new CheckBox();
            overrideExistingChargeStateCheckBox.CheckedChanged += new EventHandler( OnOptionsChangedHandler );
            overrideExistingChargeStateCheckBox.Text = "Override existing charge state";

            maxMultipleChargeUpDown = new NumericUpDown();
            maxMultipleChargeUpDown.Minimum = 1;
            maxMultipleChargeUpDown.ValueChanged += new EventHandler( maxMultipleChargeUpDown_ValueChanged );

            minMultipleChargeUpDown = new NumericUpDown();
            minMultipleChargeUpDown.Minimum = 1;
            minMultipleChargeUpDown.ValueChanged += new EventHandler( minMultipleChargeUpDown_ValueChanged );

            fractionBelowPrecursorForSinglyChargedTextBox = new TextBox();
            fractionBelowPrecursorForSinglyChargedTextBox.Text = "0.9";
            fractionBelowPrecursorForSinglyChargedTextBox.PreviewKeyDown += new PreviewKeyDownEventHandler( fractionBelowPrecursorForSinglyChargedTextBox_PreviewKeyDown );

            OptionsPanel.Controls.Add( overrideExistingChargeStateCheckBox );
            OptionsPanel.Controls.Add( maxMultipleChargeUpDown );
            OptionsPanel.Controls.Add( minMultipleChargeUpDown );
            OptionsPanel.Controls.Add( fractionBelowPrecursorForSinglyChargedTextBox );
        }

        void fractionBelowPrecursorForSinglyChargedTextBox_PreviewKeyDown( object sender, PreviewKeyDownEventArgs e )
        {
            if( e.KeyCode == Keys.Enter )
                OnOptionsChanged();
        }

        void minMultipleChargeUpDown_ValueChanged( object sender, EventArgs e )
        {
            maxMultipleChargeUpDown.Value = Math.Max( minMultipleChargeUpDown.Value, maxMultipleChargeUpDown.Value );
            OnOptionsChanged();
        }

        void maxMultipleChargeUpDown_ValueChanged( object sender, EventArgs e )
        {
            minMultipleChargeUpDown.Value = Math.Min( maxMultipleChargeUpDown.Value, minMultipleChargeUpDown.Value );
            OnOptionsChanged();
        }

        public override int ImageIndex { get { return 0; } }

        public override CVID CVID { get { return CVID.MS_charge_deconvolution; } }

        public override SpectrumList ProcessList( SpectrumList list )
        {
            return new SpectrumList_ChargeStateCalculator(
                list,
                overrideExistingChargeStateCheckBox.Checked,
                Convert.ToInt32( maxMultipleChargeUpDown.Value ),
                Convert.ToInt32( minMultipleChargeUpDown.Value ),
                Convert.ToDouble( fractionBelowPrecursorForSinglyChargedTextBox.Text ) );
        }
    }
}

