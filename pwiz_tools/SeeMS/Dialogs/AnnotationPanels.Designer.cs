namespace seems
{
    partial class AnnotationPanels
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.annotationPanelsTabControl = new System.Windows.Forms.TabControl();
            this.peptideFragmentationTabPage = new System.Windows.Forms.TabPage();
            this.peptideFragmentationPanel = new System.Windows.Forms.Panel();
            this.ionSeriesGroupBox = new System.Windows.Forms.GroupBox();
            this.zCheckBox = new System.Windows.Forms.CheckBox();
            this.yCheckBox = new System.Windows.Forms.CheckBox();
            this.xCheckBox = new System.Windows.Forms.CheckBox();
            this.cCheckBox = new System.Windows.Forms.CheckBox();
            this.bCheckBox = new System.Windows.Forms.CheckBox();
            this.aCheckBox = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.sequenceTextBox = new System.Windows.Forms.TextBox();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.minChargeUpDown = new System.Windows.Forms.NumericUpDown();
            this.maxChargeUpDown = new System.Windows.Forms.NumericUpDown();
            this.showMissesCheckBox = new System.Windows.Forms.CheckBox();
            this.annotationPanelsTabControl.SuspendLayout();
            this.peptideFragmentationTabPage.SuspendLayout();
            this.peptideFragmentationPanel.SuspendLayout();
            this.ionSeriesGroupBox.SuspendLayout();
            ( (System.ComponentModel.ISupportInitialize) ( this.minChargeUpDown ) ).BeginInit();
            ( (System.ComponentModel.ISupportInitialize) ( this.maxChargeUpDown ) ).BeginInit();
            this.SuspendLayout();
            // 
            // annotationPanelsTabControl
            // 
            this.annotationPanelsTabControl.Controls.Add( this.peptideFragmentationTabPage );
            this.annotationPanelsTabControl.Controls.Add( this.tabPage2 );
            this.annotationPanelsTabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.annotationPanelsTabControl.Location = new System.Drawing.Point( 0, 0 );
            this.annotationPanelsTabControl.Name = "annotationPanelsTabControl";
            this.annotationPanelsTabControl.SelectedIndex = 0;
            this.annotationPanelsTabControl.Size = new System.Drawing.Size( 716, 752 );
            this.annotationPanelsTabControl.TabIndex = 0;
            // 
            // peptideFragmentationTabPage
            // 
            this.peptideFragmentationTabPage.BackColor = System.Drawing.Color.DimGray;
            this.peptideFragmentationTabPage.Controls.Add( this.peptideFragmentationPanel );
            this.peptideFragmentationTabPage.Location = new System.Drawing.Point( 4, 22 );
            this.peptideFragmentationTabPage.Name = "peptideFragmentationTabPage";
            this.peptideFragmentationTabPage.Padding = new System.Windows.Forms.Padding( 3 );
            this.peptideFragmentationTabPage.Size = new System.Drawing.Size( 708, 726 );
            this.peptideFragmentationTabPage.TabIndex = 0;
            this.peptideFragmentationTabPage.Text = "Peptide Fragmentation";
            // 
            // peptideFragmentationPanel
            // 
            this.peptideFragmentationPanel.BackColor = System.Drawing.SystemColors.Control;
            this.peptideFragmentationPanel.Controls.Add( this.showMissesCheckBox );
            this.peptideFragmentationPanel.Controls.Add( this.maxChargeUpDown );
            this.peptideFragmentationPanel.Controls.Add( this.minChargeUpDown );
            this.peptideFragmentationPanel.Controls.Add( this.label3 );
            this.peptideFragmentationPanel.Controls.Add( this.label2 );
            this.peptideFragmentationPanel.Controls.Add( this.ionSeriesGroupBox );
            this.peptideFragmentationPanel.Controls.Add( this.label1 );
            this.peptideFragmentationPanel.Controls.Add( this.sequenceTextBox );
            this.peptideFragmentationPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.peptideFragmentationPanel.Location = new System.Drawing.Point( 3, 3 );
            this.peptideFragmentationPanel.Name = "peptideFragmentationPanel";
            this.peptideFragmentationPanel.Size = new System.Drawing.Size( 702, 720 );
            this.peptideFragmentationPanel.TabIndex = 0;
            // 
            // ionSeriesGroupBox
            // 
            this.ionSeriesGroupBox.Controls.Add( this.zCheckBox );
            this.ionSeriesGroupBox.Controls.Add( this.yCheckBox );
            this.ionSeriesGroupBox.Controls.Add( this.xCheckBox );
            this.ionSeriesGroupBox.Controls.Add( this.cCheckBox );
            this.ionSeriesGroupBox.Controls.Add( this.bCheckBox );
            this.ionSeriesGroupBox.Controls.Add( this.aCheckBox );
            this.ionSeriesGroupBox.Location = new System.Drawing.Point( 6, 89 );
            this.ionSeriesGroupBox.Name = "ionSeriesGroupBox";
            this.ionSeriesGroupBox.Size = new System.Drawing.Size( 127, 68 );
            this.ionSeriesGroupBox.TabIndex = 3;
            this.ionSeriesGroupBox.TabStop = false;
            this.ionSeriesGroupBox.Text = "Fragment Ion Series";
            // 
            // zCheckBox
            // 
            this.zCheckBox.AutoSize = true;
            this.zCheckBox.Location = new System.Drawing.Point( 87, 42 );
            this.zCheckBox.Name = "zCheckBox";
            this.zCheckBox.Size = new System.Drawing.Size( 31, 17 );
            this.zCheckBox.TabIndex = 5;
            this.zCheckBox.Text = "z";
            this.zCheckBox.UseVisualStyleBackColor = true;
            // 
            // yCheckBox
            // 
            this.yCheckBox.AutoSize = true;
            this.yCheckBox.Location = new System.Drawing.Point( 49, 42 );
            this.yCheckBox.Name = "yCheckBox";
            this.yCheckBox.Size = new System.Drawing.Size( 31, 17 );
            this.yCheckBox.TabIndex = 4;
            this.yCheckBox.Text = "y";
            this.yCheckBox.UseVisualStyleBackColor = true;
            // 
            // xCheckBox
            // 
            this.xCheckBox.AutoSize = true;
            this.xCheckBox.Location = new System.Drawing.Point( 11, 42 );
            this.xCheckBox.Name = "xCheckBox";
            this.xCheckBox.Size = new System.Drawing.Size( 31, 17 );
            this.xCheckBox.TabIndex = 3;
            this.xCheckBox.Text = "x";
            this.xCheckBox.UseVisualStyleBackColor = true;
            // 
            // cCheckBox
            // 
            this.cCheckBox.AutoSize = true;
            this.cCheckBox.Location = new System.Drawing.Point( 87, 19 );
            this.cCheckBox.Name = "cCheckBox";
            this.cCheckBox.Size = new System.Drawing.Size( 32, 17 );
            this.cCheckBox.TabIndex = 2;
            this.cCheckBox.Text = "c";
            this.cCheckBox.UseVisualStyleBackColor = true;
            // 
            // bCheckBox
            // 
            this.bCheckBox.AutoSize = true;
            this.bCheckBox.Location = new System.Drawing.Point( 49, 19 );
            this.bCheckBox.Name = "bCheckBox";
            this.bCheckBox.Size = new System.Drawing.Size( 32, 17 );
            this.bCheckBox.TabIndex = 1;
            this.bCheckBox.Text = "b";
            this.bCheckBox.UseVisualStyleBackColor = true;
            // 
            // aCheckBox
            // 
            this.aCheckBox.AutoSize = true;
            this.aCheckBox.Location = new System.Drawing.Point( 11, 19 );
            this.aCheckBox.Name = "aCheckBox";
            this.aCheckBox.Size = new System.Drawing.Size( 32, 17 );
            this.aCheckBox.TabIndex = 0;
            this.aCheckBox.Text = "a";
            this.aCheckBox.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point( 3, 14 );
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size( 46, 13 );
            this.label1.TabIndex = 2;
            this.label1.Text = "Peptide:";
            // 
            // sequenceTextBox
            // 
            this.sequenceTextBox.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.sequenceTextBox.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.sequenceTextBox.Location = new System.Drawing.Point( 55, 11 );
            this.sequenceTextBox.Name = "sequenceTextBox";
            this.sequenceTextBox.Size = new System.Drawing.Size( 636, 20 );
            this.sequenceTextBox.TabIndex = 1;
            this.sequenceTextBox.Text = "PEPTIDE";
            // 
            // tabPage2
            // 
            this.tabPage2.Location = new System.Drawing.Point( 4, 22 );
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding( 3 );
            this.tabPage2.Size = new System.Drawing.Size( 708, 726 );
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "tabPage2";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point( 4, 39 );
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size( 110, 13 );
            this.label2.TabIndex = 4;
            this.label2.Text = "Min. fragment charge:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point( 4, 64 );
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size( 113, 13 );
            this.label3.TabIndex = 6;
            this.label3.Text = "Max. fragment charge:";
            // 
            // minChargeUpDown
            // 
            this.minChargeUpDown.Location = new System.Drawing.Point( 120, 37 );
            this.minChargeUpDown.Minimum = new decimal( new int[] {
            1,
            0,
            0,
            0} );
            this.minChargeUpDown.Name = "minChargeUpDown";
            this.minChargeUpDown.Size = new System.Drawing.Size( 41, 20 );
            this.minChargeUpDown.TabIndex = 7;
            this.minChargeUpDown.Value = new decimal( new int[] {
            1,
            0,
            0,
            0} );
            this.minChargeUpDown.ValueChanged += new System.EventHandler( this.minChargeUpDown_ValueChanged );
            // 
            // maxChargeUpDown
            // 
            this.maxChargeUpDown.Location = new System.Drawing.Point( 120, 63 );
            this.maxChargeUpDown.Minimum = new decimal( new int[] {
            1,
            0,
            0,
            0} );
            this.maxChargeUpDown.Name = "maxChargeUpDown";
            this.maxChargeUpDown.Size = new System.Drawing.Size( 41, 20 );
            this.maxChargeUpDown.TabIndex = 8;
            this.maxChargeUpDown.Value = new decimal( new int[] {
            1,
            0,
            0,
            0} );
            this.maxChargeUpDown.ValueChanged += new System.EventHandler( this.maxChargeUpDown_ValueChanged );
            // 
            // showMissesCheckBox
            // 
            this.showMissesCheckBox.AutoSize = true;
            this.showMissesCheckBox.Location = new System.Drawing.Point( 7, 163 );
            this.showMissesCheckBox.Name = "showMissesCheckBox";
            this.showMissesCheckBox.Size = new System.Drawing.Size( 139, 17 );
            this.showMissesCheckBox.TabIndex = 6;
            this.showMissesCheckBox.Text = "Show missing fragments";
            this.showMissesCheckBox.UseVisualStyleBackColor = true;
            // 
            // AnnotationPanels
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add( this.annotationPanelsTabControl );
            this.Name = "AnnotationPanels";
            this.Size = new System.Drawing.Size( 716, 752 );
            this.annotationPanelsTabControl.ResumeLayout( false );
            this.peptideFragmentationTabPage.ResumeLayout( false );
            this.peptideFragmentationPanel.ResumeLayout( false );
            this.peptideFragmentationPanel.PerformLayout();
            this.ionSeriesGroupBox.ResumeLayout( false );
            this.ionSeriesGroupBox.PerformLayout();
            ( (System.ComponentModel.ISupportInitialize) ( this.minChargeUpDown ) ).EndInit();
            ( (System.ComponentModel.ISupportInitialize) ( this.maxChargeUpDown ) ).EndInit();
            this.ResumeLayout( false );

        }

        #endregion

        private System.Windows.Forms.TabControl annotationPanelsTabControl;
        private System.Windows.Forms.TabPage peptideFragmentationTabPage;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.GroupBox ionSeriesGroupBox;
        private System.Windows.Forms.Label label1;
        public System.Windows.Forms.Panel peptideFragmentationPanel;
        public System.Windows.Forms.CheckBox aCheckBox;
        public System.Windows.Forms.TextBox sequenceTextBox;
        public System.Windows.Forms.CheckBox zCheckBox;
        public System.Windows.Forms.CheckBox yCheckBox;
        public System.Windows.Forms.CheckBox xCheckBox;
        public System.Windows.Forms.CheckBox cCheckBox;
        public System.Windows.Forms.CheckBox bCheckBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        public System.Windows.Forms.NumericUpDown maxChargeUpDown;
        public System.Windows.Forms.NumericUpDown minChargeUpDown;
        public System.Windows.Forms.CheckBox showMissesCheckBox;
    }
}
