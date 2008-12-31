namespace seems
{
    partial class SpectrumAnnotationForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose( bool disposing )
        {
            if( disposing && ( components != null ) )
            {
                components.Dispose();
            }
            base.Dispose( disposing );
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager( typeof( SpectrumAnnotationForm ) );
            this.annotationsContextMenuStrip = new System.Windows.Forms.ContextMenuStrip( this.components );
            this.addContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addContextMenuStrip = new System.Windows.Forms.ContextMenuStrip( this.components );
            this.peptideFragmentationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.label1 = new System.Windows.Forms.Label();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.annotationsListView = new System.Windows.Forms.ListView();
            this.AnnotationHeader = new System.Windows.Forms.ColumnHeader();
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.addAnnotationDropDownButton = new System.Windows.Forms.ToolStripDropDownButton();
            this.removeAnnotationButton = new System.Windows.Forms.ToolStripButton();
            this.globalOverrideToolStripButton = new System.Windows.Forms.ToolStripButton();
            this.runOverrideToolStripButton = new System.Windows.Forms.ToolStripButton();
            this.annotationsContextMenuStrip.SuspendLayout();
            this.addContextMenuStrip.SuspendLayout();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.toolStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // annotationsContextMenuStrip
            // 
            this.annotationsContextMenuStrip.Items.AddRange( new System.Windows.Forms.ToolStripItem[] {
            this.addContextMenuItem,
            this.removeToolStripMenuItem} );
            this.annotationsContextMenuStrip.Name = "annotationsContextMenuStrip";
            this.annotationsContextMenuStrip.Size = new System.Drawing.Size( 118, 48 );
            this.annotationsContextMenuStrip.Opening += new System.ComponentModel.CancelEventHandler( this.ContextMenuStrip_Opening );
            // 
            // addContextMenuItem
            // 
            this.addContextMenuItem.DropDown = this.addContextMenuStrip;
            this.addContextMenuItem.Name = "addContextMenuItem";
            this.addContextMenuItem.Size = new System.Drawing.Size( 117, 22 );
            this.addContextMenuItem.Text = "Add";
            // 
            // addContextMenuStrip
            // 
            this.addContextMenuStrip.Items.AddRange( new System.Windows.Forms.ToolStripItem[] {
            this.peptideFragmentationToolStripMenuItem} );
            this.addContextMenuStrip.Name = "addContextMenuStrip";
            this.addContextMenuStrip.OwnerItem = this.addAnnotationDropDownButton;
            this.addContextMenuStrip.Size = new System.Drawing.Size( 184, 26 );
            // 
            // peptideFragmentationToolStripMenuItem
            // 
            this.peptideFragmentationToolStripMenuItem.Name = "peptideFragmentationToolStripMenuItem";
            this.peptideFragmentationToolStripMenuItem.Size = new System.Drawing.Size( 183, 22 );
            this.peptideFragmentationToolStripMenuItem.Text = "Peptide Fragmentation";
            this.peptideFragmentationToolStripMenuItem.Click += new System.EventHandler( this.peptideFragmentationToolStripMenuItem_Click );
            // 
            // removeToolStripMenuItem
            // 
            this.removeToolStripMenuItem.Name = "removeToolStripMenuItem";
            this.removeToolStripMenuItem.Size = new System.Drawing.Size( 117, 22 );
            this.removeToolStripMenuItem.Text = "Remove";
            this.removeToolStripMenuItem.Click += new System.EventHandler( this.removeAnnotationButton_Click );
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font( "Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.label1.Location = new System.Drawing.Point( 12, 9 );
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size( 119, 13 );
            this.label1.TabIndex = 1;
            this.label1.Text = "Current Annotations";
            // 
            // splitContainer
            // 
            this.splitContainer.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom )
                        | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.splitContainer.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.splitContainer.Location = new System.Drawing.Point( 0, 24 );
            this.splitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add( this.annotationsListView );
            this.splitContainer.Panel1.Controls.Add( this.label1 );
            this.splitContainer.Size = new System.Drawing.Size( 592, 250 );
            this.splitContainer.SplitterDistance = 241;
            this.splitContainer.SplitterWidth = 3;
            this.splitContainer.TabIndex = 2;
            // 
            // annotationsListView
            // 
            this.annotationsListView.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom )
                        | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.annotationsListView.CheckBoxes = true;
            this.annotationsListView.Columns.AddRange( new System.Windows.Forms.ColumnHeader[] {
            this.AnnotationHeader} );
            this.annotationsListView.ContextMenuStrip = this.annotationsContextMenuStrip;
            this.annotationsListView.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.annotationsListView.HideSelection = false;
            this.annotationsListView.LabelWrap = false;
            this.annotationsListView.Location = new System.Drawing.Point( 15, 25 );
            this.annotationsListView.Name = "annotationsListView";
            this.annotationsListView.Size = new System.Drawing.Size( 210, 210 );
            this.annotationsListView.TabIndex = 2;
            this.annotationsListView.UseCompatibleStateImageBehavior = false;
            this.annotationsListView.View = System.Windows.Forms.View.Details;
            this.annotationsListView.VirtualMode = true;
            this.annotationsListView.SelectedIndexChanged += new System.EventHandler( this.annotationsListView_SelectedIndexChanged );
            this.annotationsListView.Layout += new System.Windows.Forms.LayoutEventHandler( this.annotationsListView_Layout );
            this.annotationsListView.RetrieveVirtualItem += new System.Windows.Forms.RetrieveVirtualItemEventHandler( this.annotationsListView_RetrieveVirtualItem );
            this.annotationsListView.KeyDown += new System.Windows.Forms.KeyEventHandler( this.annotationsListView_KeyDown );
            // 
            // AnnotationHeader
            // 
            this.AnnotationHeader.Text = "Annotation";
            this.AnnotationHeader.Width = 10;
            // 
            // toolStrip
            // 
            this.toolStrip.Items.AddRange( new System.Windows.Forms.ToolStripItem[] {
            this.addAnnotationDropDownButton,
            this.removeAnnotationButton,
            this.globalOverrideToolStripButton,
            this.runOverrideToolStripButton} );
            this.toolStrip.Location = new System.Drawing.Point( 0, 0 );
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size( 592, 25 );
            this.toolStrip.TabIndex = 3;
            this.toolStrip.Text = "toolStrip";
            // 
            // addAnnotationDropDownButton
            // 
            this.addAnnotationDropDownButton.DropDown = this.addContextMenuStrip;
            this.addAnnotationDropDownButton.Image = ( (System.Drawing.Image) ( resources.GetObject( "addAnnotationDropDownButton.Image" ) ) );
            this.addAnnotationDropDownButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.addAnnotationDropDownButton.Name = "addAnnotationDropDownButton";
            this.addAnnotationDropDownButton.Size = new System.Drawing.Size( 55, 22 );
            this.addAnnotationDropDownButton.Text = "Add";
            // 
            // removeAnnotationButton
            // 
            this.removeAnnotationButton.Enabled = false;
            this.removeAnnotationButton.Image = ( (System.Drawing.Image) ( resources.GetObject( "removeAnnotationButton.Image" ) ) );
            this.removeAnnotationButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.removeAnnotationButton.Name = "removeAnnotationButton";
            this.removeAnnotationButton.Size = new System.Drawing.Size( 67, 22 );
            this.removeAnnotationButton.Text = "Remove";
            this.removeAnnotationButton.Click += new System.EventHandler( this.removeAnnotationButton_Click );
            // 
            // globalOverrideToolStripButton
            // 
            this.globalOverrideToolStripButton.Enabled = false;
            this.globalOverrideToolStripButton.Image = ( (System.Drawing.Image) ( resources.GetObject( "globalOverrideToolStripButton.Image" ) ) );
            this.globalOverrideToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.globalOverrideToolStripButton.Name = "globalOverrideToolStripButton";
            this.globalOverrideToolStripButton.Size = new System.Drawing.Size( 155, 22 );
            this.globalOverrideToolStripButton.Text = "Override Global Processing";
            // 
            // runOverrideToolStripButton
            // 
            this.runOverrideToolStripButton.Enabled = false;
            this.runOverrideToolStripButton.Image = ( (System.Drawing.Image) ( resources.GetObject( "runOverrideToolStripButton.Image" ) ) );
            this.runOverrideToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.runOverrideToolStripButton.Name = "runOverrideToolStripButton";
            this.runOverrideToolStripButton.Size = new System.Drawing.Size( 145, 22 );
            this.runOverrideToolStripButton.Text = "Override Run Processing";
            // 
            // SpectrumAnnotationForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size( 592, 273 );
            this.Controls.Add( this.toolStrip );
            this.Controls.Add( this.splitContainer );
            this.DoubleBuffered = true;
            this.Name = "SpectrumAnnotationForm";
            this.TabText = "Spectrum Annotation Manager";
            this.Text = "Spectrum Annotation Manager";
            this.annotationsContextMenuStrip.ResumeLayout( false );
            this.addContextMenuStrip.ResumeLayout( false );
            this.splitContainer.Panel1.ResumeLayout( false );
            this.splitContainer.Panel1.PerformLayout();
            this.splitContainer.ResumeLayout( false );
            this.toolStrip.ResumeLayout( false );
            this.toolStrip.PerformLayout();
            this.ResumeLayout( false );
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripDropDownButton addAnnotationDropDownButton;
        private System.Windows.Forms.ToolStripButton removeAnnotationButton;
        private System.Windows.Forms.ToolStripButton globalOverrideToolStripButton;
        private System.Windows.Forms.ToolStripButton runOverrideToolStripButton;
        private System.Windows.Forms.ContextMenuStrip addContextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem addContextMenuItem;
        private System.Windows.Forms.ContextMenuStrip annotationsContextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem peptideFragmentationToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeToolStripMenuItem;
        private System.Windows.Forms.ListView annotationsListView;
        private System.Windows.Forms.ColumnHeader AnnotationHeader;
    }
}