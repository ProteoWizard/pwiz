namespace seems
{
	partial class ChromatogramAnnotationSettingsForm
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
			this.okButton = new System.Windows.Forms.Button();
			this.cancelButton = new System.Windows.Forms.Button();
			this.aliasAndColorMappingListBox = new System.Windows.Forms.ListBox();
			this.addAliasAndColorMappingButton = new System.Windows.Forms.Button();
			this.removeAliasAndColorMappingButton = new System.Windows.Forms.Button();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.editAliasAndColorMappingButton = new System.Windows.Forms.Button();
			this.clearAliasAndColorMappingButton = new System.Windows.Forms.Button();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.showTotalIntensityLabelsCheckbox = new System.Windows.Forms.CheckBox();
			this.showTimeLabelsCheckbox = new System.Windows.Forms.CheckBox();
			this.groupBox3 = new System.Windows.Forms.GroupBox();
			this.matchToleranceUnitsComboBox = new System.Windows.Forms.ComboBox();
			this.matchToleranceCheckbox = new System.Windows.Forms.CheckBox();
			this.matchToleranceTextBox = new System.Windows.Forms.TextBox();
			this.showUnmatchedAnnotationsCheckbox = new System.Windows.Forms.CheckBox();
			this.showMatchedAnnotationsCheckbox = new System.Windows.Forms.CheckBox();
			this.groupBox1.SuspendLayout();
			this.groupBox2.SuspendLayout();
			this.groupBox3.SuspendLayout();
			this.SuspendLayout();
			// 
			// okButton
			// 
			this.okButton.Anchor = System.Windows.Forms.AnchorStyles.Left;
			this.okButton.Location = new System.Drawing.Point( 19, 441 );
			this.okButton.Margin = new System.Windows.Forms.Padding( 10, 3, 3, 3 );
			this.okButton.Name = "okButton";
			this.okButton.Size = new System.Drawing.Size( 75, 23 );
			this.okButton.TabIndex = 2;
			this.okButton.Text = "OK";
			this.okButton.UseVisualStyleBackColor = true;
			this.okButton.Click += new System.EventHandler( this.okButton_Click );
			// 
			// cancelButton
			// 
			this.cancelButton.Anchor = System.Windows.Forms.AnchorStyles.Left;
			this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.cancelButton.Location = new System.Drawing.Point( 107, 441 );
			this.cancelButton.Margin = new System.Windows.Forms.Padding( 10, 3, 3, 3 );
			this.cancelButton.Name = "cancelButton";
			this.cancelButton.Size = new System.Drawing.Size( 75, 23 );
			this.cancelButton.TabIndex = 3;
			this.cancelButton.Text = "Cancel";
			this.cancelButton.UseVisualStyleBackColor = true;
			this.cancelButton.Click += new System.EventHandler( this.cancelButton_Click );
			// 
			// aliasAndColorMappingListBox
			// 
			this.aliasAndColorMappingListBox.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom )
						| System.Windows.Forms.AnchorStyles.Left )
						| System.Windows.Forms.AnchorStyles.Right ) ) );
			this.aliasAndColorMappingListBox.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
			this.aliasAndColorMappingListBox.Font = new System.Drawing.Font( "Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
			this.aliasAndColorMappingListBox.FormattingEnabled = true;
			this.aliasAndColorMappingListBox.ItemHeight = 14;
			this.aliasAndColorMappingListBox.Location = new System.Drawing.Point( 11, 19 );
			this.aliasAndColorMappingListBox.Name = "aliasAndColorMappingListBox";
			this.aliasAndColorMappingListBox.Size = new System.Drawing.Size( 159, 116 );
			this.aliasAndColorMappingListBox.TabIndex = 4;
			this.aliasAndColorMappingListBox.DrawItem += new System.Windows.Forms.DrawItemEventHandler( this.aliasAndColorMappingListBox_DrawItem );
			// 
			// addAliasAndColorMappingButton
			// 
			this.addAliasAndColorMappingButton.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right ) ) );
			this.addAliasAndColorMappingButton.Location = new System.Drawing.Point( 176, 19 );
			this.addAliasAndColorMappingButton.Name = "addAliasAndColorMappingButton";
			this.addAliasAndColorMappingButton.Size = new System.Drawing.Size( 60, 23 );
			this.addAliasAndColorMappingButton.TabIndex = 5;
			this.addAliasAndColorMappingButton.Text = "Add";
			this.addAliasAndColorMappingButton.UseVisualStyleBackColor = true;
			this.addAliasAndColorMappingButton.Click += new System.EventHandler( this.addAliasAndColorMappingButton_Click );
			// 
			// removeAliasAndColorMappingButton
			// 
			this.removeAliasAndColorMappingButton.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right ) ) );
			this.removeAliasAndColorMappingButton.Location = new System.Drawing.Point( 176, 77 );
			this.removeAliasAndColorMappingButton.Name = "removeAliasAndColorMappingButton";
			this.removeAliasAndColorMappingButton.Size = new System.Drawing.Size( 60, 23 );
			this.removeAliasAndColorMappingButton.TabIndex = 6;
			this.removeAliasAndColorMappingButton.Text = "Remove";
			this.removeAliasAndColorMappingButton.UseVisualStyleBackColor = true;
			this.removeAliasAndColorMappingButton.Click += new System.EventHandler( this.removeAliasAndColorMappingButton_Click );
			// 
			// groupBox1
			// 
			this.groupBox1.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom )
						| System.Windows.Forms.AnchorStyles.Left )
						| System.Windows.Forms.AnchorStyles.Right ) ) );
			this.groupBox1.Controls.Add( this.editAliasAndColorMappingButton );
			this.groupBox1.Controls.Add( this.clearAliasAndColorMappingButton );
			this.groupBox1.Controls.Add( this.aliasAndColorMappingListBox );
			this.groupBox1.Controls.Add( this.removeAliasAndColorMappingButton );
			this.groupBox1.Controls.Add( this.addAliasAndColorMappingButton );
			this.groupBox1.Location = new System.Drawing.Point( 19, 274 );
			this.groupBox1.Margin = new System.Windows.Forms.Padding( 10 );
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size( 248, 154 );
			this.groupBox1.TabIndex = 7;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Alias and Color Mapping";
			// 
			// editAliasAndColorMappingButton
			// 
			this.editAliasAndColorMappingButton.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right ) ) );
			this.editAliasAndColorMappingButton.Location = new System.Drawing.Point( 176, 48 );
			this.editAliasAndColorMappingButton.Name = "editAliasAndColorMappingButton";
			this.editAliasAndColorMappingButton.Size = new System.Drawing.Size( 60, 23 );
			this.editAliasAndColorMappingButton.TabIndex = 8;
			this.editAliasAndColorMappingButton.Text = "Edit";
			this.editAliasAndColorMappingButton.UseVisualStyleBackColor = true;
			this.editAliasAndColorMappingButton.Click += new System.EventHandler( this.editAliasAndColorMappingButton_Click );
			// 
			// clearAliasAndColorMappingButton
			// 
			this.clearAliasAndColorMappingButton.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right ) ) );
			this.clearAliasAndColorMappingButton.Location = new System.Drawing.Point( 176, 106 );
			this.clearAliasAndColorMappingButton.Name = "clearAliasAndColorMappingButton";
			this.clearAliasAndColorMappingButton.Size = new System.Drawing.Size( 60, 23 );
			this.clearAliasAndColorMappingButton.TabIndex = 7;
			this.clearAliasAndColorMappingButton.Text = "Clear";
			this.clearAliasAndColorMappingButton.UseVisualStyleBackColor = true;
			this.clearAliasAndColorMappingButton.Click += new System.EventHandler( this.clearAliasAndColorMappingButton_Click );
			// 
			// groupBox2
			// 
			this.groupBox2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
			this.groupBox2.Controls.Add( this.showTotalIntensityLabelsCheckbox );
			this.groupBox2.Controls.Add( this.showTimeLabelsCheckbox );
			this.groupBox2.Location = new System.Drawing.Point( 19, 19 );
			this.groupBox2.Margin = new System.Windows.Forms.Padding( 10 );
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size( 248, 96 );
			this.groupBox2.TabIndex = 8;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "Automatic Annotation Display Settings";
			// 
			// showTotalIntensityLabelsCheckbox
			// 
			this.showTotalIntensityLabelsCheckbox.Location = new System.Drawing.Point( 11, 53 );
			this.showTotalIntensityLabelsCheckbox.Name = "showTotalIntensityLabelsCheckbox";
			this.showTotalIntensityLabelsCheckbox.Size = new System.Drawing.Size( 231, 24 );
			this.showTotalIntensityLabelsCheckbox.TabIndex = 1;
			this.showTotalIntensityLabelsCheckbox.Text = "Show total intensity label";
			this.showTotalIntensityLabelsCheckbox.UseVisualStyleBackColor = true;
			// 
			// showTimeLabelsCheckbox
			// 
			this.showTimeLabelsCheckbox.Checked = true;
			this.showTimeLabelsCheckbox.CheckState = System.Windows.Forms.CheckState.Checked;
			this.showTimeLabelsCheckbox.Location = new System.Drawing.Point( 11, 29 );
			this.showTimeLabelsCheckbox.Name = "showTimeLabelsCheckbox";
			this.showTimeLabelsCheckbox.Size = new System.Drawing.Size( 231, 24 );
			this.showTimeLabelsCheckbox.TabIndex = 0;
			this.showTimeLabelsCheckbox.Text = "Show time label";
			this.showTimeLabelsCheckbox.UseVisualStyleBackColor = true;
			// 
			// groupBox3
			// 
			this.groupBox3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
			this.groupBox3.Controls.Add( this.matchToleranceUnitsComboBox );
			this.groupBox3.Controls.Add( this.matchToleranceCheckbox );
			this.groupBox3.Controls.Add( this.matchToleranceTextBox );
			this.groupBox3.Controls.Add( this.showUnmatchedAnnotationsCheckbox );
			this.groupBox3.Controls.Add( this.showMatchedAnnotationsCheckbox );
			this.groupBox3.Location = new System.Drawing.Point( 19, 135 );
			this.groupBox3.Margin = new System.Windows.Forms.Padding( 10 );
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.Size = new System.Drawing.Size( 248, 119 );
			this.groupBox3.TabIndex = 9;
			this.groupBox3.TabStop = false;
			this.groupBox3.Text = "Manual Annotation Display Settings";
			// 
			// matchToleranceUnitsComboBox
			// 
			this.matchToleranceUnitsComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.matchToleranceUnitsComboBox.FormattingEnabled = true;
			this.matchToleranceUnitsComboBox.Location = new System.Drawing.Point( 184, 79 );
			this.matchToleranceUnitsComboBox.Name = "matchToleranceUnitsComboBox";
			this.matchToleranceUnitsComboBox.Size = new System.Drawing.Size( 52, 21 );
			this.matchToleranceUnitsComboBox.TabIndex = 4;
			// 
			// matchToleranceCheckbox
			// 
			this.matchToleranceCheckbox.AutoSize = true;
			this.matchToleranceCheckbox.Location = new System.Drawing.Point( 11, 82 );
			this.matchToleranceCheckbox.Name = "matchToleranceCheckbox";
			this.matchToleranceCheckbox.Size = new System.Drawing.Size( 106, 17 );
			this.matchToleranceCheckbox.TabIndex = 3;
			this.matchToleranceCheckbox.Text = "Match tolerance:";
			this.matchToleranceCheckbox.UseVisualStyleBackColor = true;
			this.matchToleranceCheckbox.CheckedChanged += new System.EventHandler( this.matchToleranceCheckbox_CheckedChanged );
			// 
			// matchToleranceTextBox
			// 
			this.matchToleranceTextBox.Location = new System.Drawing.Point( 123, 80 );
			this.matchToleranceTextBox.Name = "matchToleranceTextBox";
			this.matchToleranceTextBox.Size = new System.Drawing.Size( 55, 20 );
			this.matchToleranceTextBox.TabIndex = 2;
			this.matchToleranceTextBox.TextChanged += new System.EventHandler( this.matchToleranceTextBox_TextChanged );
			// 
			// showUnmatchedAnnotationsCheckbox
			// 
			this.showUnmatchedAnnotationsCheckbox.Location = new System.Drawing.Point( 11, 52 );
			this.showUnmatchedAnnotationsCheckbox.Name = "showUnmatchedAnnotationsCheckbox";
			this.showUnmatchedAnnotationsCheckbox.Size = new System.Drawing.Size( 231, 24 );
			this.showUnmatchedAnnotationsCheckbox.TabIndex = 1;
			this.showUnmatchedAnnotationsCheckbox.Text = "Show unmatched annotations";
			this.showUnmatchedAnnotationsCheckbox.UseVisualStyleBackColor = true;
			// 
			// showMatchedAnnotationsCheckbox
			// 
			this.showMatchedAnnotationsCheckbox.Location = new System.Drawing.Point( 11, 28 );
			this.showMatchedAnnotationsCheckbox.Name = "showMatchedAnnotationsCheckbox";
			this.showMatchedAnnotationsCheckbox.Size = new System.Drawing.Size( 231, 24 );
			this.showMatchedAnnotationsCheckbox.TabIndex = 0;
			this.showMatchedAnnotationsCheckbox.Text = "Show matched annotations";
			this.showMatchedAnnotationsCheckbox.UseVisualStyleBackColor = true;
			// 
			// ChromatogramAnnotationSettingsForm
			// 
			this.AcceptButton = this.okButton;
			this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.CancelButton = this.cancelButton;
			this.ClientSize = new System.Drawing.Size( 290, 470 );
			this.ControlBox = false;
			this.Controls.Add( this.okButton );
			this.Controls.Add( this.cancelButton );
			this.Controls.Add( this.groupBox1 );
			this.Controls.Add( this.groupBox3 );
			this.Controls.Add( this.groupBox2 );
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			this.Name = "ChromatogramAnnotationSettingsForm";
			this.ShowInTaskbar = false;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Chromatogram Annotation Settings";
			this.groupBox1.ResumeLayout( false );
			this.groupBox2.ResumeLayout( false );
			this.groupBox3.ResumeLayout( false );
			this.groupBox3.PerformLayout();
			this.ResumeLayout( false );

		}

		#endregion

		private System.Windows.Forms.Button okButton;
		private System.Windows.Forms.Button cancelButton;
		private System.Windows.Forms.ListBox aliasAndColorMappingListBox;
		private System.Windows.Forms.Button addAliasAndColorMappingButton;
		private System.Windows.Forms.Button removeAliasAndColorMappingButton;
		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.Button clearAliasAndColorMappingButton;
		private System.Windows.Forms.GroupBox groupBox2;
		private System.Windows.Forms.CheckBox showTotalIntensityLabelsCheckbox;
		private System.Windows.Forms.CheckBox showTimeLabelsCheckbox;
		private System.Windows.Forms.GroupBox groupBox3;
		private System.Windows.Forms.CheckBox showUnmatchedAnnotationsCheckbox;
		private System.Windows.Forms.CheckBox showMatchedAnnotationsCheckbox;
		private System.Windows.Forms.CheckBox matchToleranceCheckbox;
		private System.Windows.Forms.TextBox matchToleranceTextBox;
		private System.Windows.Forms.ComboBox matchToleranceUnitsComboBox;
		private System.Windows.Forms.Button editAliasAndColorMappingButton;
	}
}