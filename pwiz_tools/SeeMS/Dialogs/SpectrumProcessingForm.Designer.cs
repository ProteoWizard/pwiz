namespace seems
{
    partial class SpectrumProcessingForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager( typeof( SpectrumProcessingForm ) );
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.toolStripButton1 = new System.Windows.Forms.ToolStripButton();
            this.globalOverrideToolStripButton = new System.Windows.Forms.ToolStripButton();
            this.runOverrideToolStripButton = new System.Windows.Forms.ToolStripButton();
            this.imageList1 = new System.Windows.Forms.ImageList( this.components );
            this.splitContainer.SuspendLayout();
            this.toolStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer
            // 
            this.splitContainer.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom )
                        | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.splitContainer.Location = new System.Drawing.Point( 0, 28 );
            this.splitContainer.Name = "splitContainer";
            this.splitContainer.Size = new System.Drawing.Size( 627, 386 );
            this.splitContainer.SplitterDistance = 398;
            this.splitContainer.TabIndex = 1;
            // 
            // toolStrip
            // 
            this.toolStrip.Items.AddRange( new System.Windows.Forms.ToolStripItem[] {
            this.toolStripButton1,
            this.globalOverrideToolStripButton,
            this.runOverrideToolStripButton} );
            this.toolStrip.Location = new System.Drawing.Point( 0, 0 );
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size( 627, 25 );
            this.toolStrip.TabIndex = 1;
            this.toolStrip.Text = "toolStrip1";
            // 
            // toolStripButton1
            // 
            this.toolStripButton1.Image = global::seems.Properties.Resources.DataProcessing;
            this.toolStripButton1.ImageTransparentColor = System.Drawing.Color.White;
            this.toolStripButton1.Name = "toolStripButton1";
            this.toolStripButton1.Size = new System.Drawing.Size( 144, 22 );
            this.toolStripButton1.Text = "Add Spectrum Processor";
            // 
            // globalOverrideToolStripButton
            // 
            this.globalOverrideToolStripButton.Image = ( (System.Drawing.Image) ( resources.GetObject( "globalOverrideToolStripButton.Image" ) ) );
            this.globalOverrideToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.globalOverrideToolStripButton.Name = "globalOverrideToolStripButton";
            this.globalOverrideToolStripButton.Size = new System.Drawing.Size( 155, 22 );
            this.globalOverrideToolStripButton.Text = "Override Global Processing";
            // 
            // runOverrideToolStripButton
            // 
            this.runOverrideToolStripButton.Image = ( (System.Drawing.Image) ( resources.GetObject( "runOverrideToolStripButton.Image" ) ) );
            this.runOverrideToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.runOverrideToolStripButton.Name = "runOverrideToolStripButton";
            this.runOverrideToolStripButton.Size = new System.Drawing.Size( 145, 22 );
            this.runOverrideToolStripButton.Text = "Override Run Processing";
            // 
            // imageList1
            // 
            this.imageList1.ImageStream = ( (System.Windows.Forms.ImageListStreamer) ( resources.GetObject( "imageList1.ImageStream" ) ) );
            this.imageList1.TransparentColor = System.Drawing.Color.Transparent;
            this.imageList1.Images.SetKeyName( 0, "Thresholder.png" );
            this.imageList1.Images.SetKeyName( 1, "Centroider.png" );
            this.imageList1.Images.SetKeyName( 2, "DataProcessing.png" );
            this.imageList1.Images.SetKeyName( 3, "Smoother.png" );
            // 
            // SpectrumProcessingForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size( 627, 412 );
            this.Controls.Add( this.toolStrip );
            this.Controls.Add( this.splitContainer );
            this.Name = "SpectrumProcessingForm";
            this.TabText = "Spectrum Data Processing Manager";
            this.Text = "Spectrum Data Processing Manager";
            this.splitContainer.ResumeLayout( false );
            this.toolStrip.ResumeLayout( false );
            this.toolStrip.PerformLayout();
            this.ResumeLayout( false );
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripButton toolStripButton1;
        private System.Windows.Forms.ToolStripButton runOverrideToolStripButton;
        private System.Windows.Forms.ToolStripButton globalOverrideToolStripButton;
        private System.Windows.Forms.ImageList imageList1;


    }
}