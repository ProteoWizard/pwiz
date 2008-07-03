namespace seems
{
	partial class AnnotationEditForm
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
			this.editAnnotationButton = new System.Windows.Forms.Button();
			this.clearAnnotationsButton = new System.Windows.Forms.Button();
			this.annotationListBox = new System.Windows.Forms.ListBox();
			this.removeAnnotationButton = new System.Windows.Forms.Button();
			this.addAnnotationButton = new System.Windows.Forms.Button();
			this.SuspendLayout();
			// 
			// okButton
			// 
			this.okButton.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right ) ) );
			this.okButton.Location = new System.Drawing.Point( 143, 464 );
			this.okButton.Name = "okButton";
			this.okButton.Size = new System.Drawing.Size( 60, 23 );
			this.okButton.TabIndex = 1;
			this.okButton.Text = "OK";
			this.okButton.UseVisualStyleBackColor = true;
			this.okButton.Click += new System.EventHandler( this.okButton_Click );
			// 
			// cancelButton
			// 
			this.cancelButton.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right ) ) );
			this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.cancelButton.Location = new System.Drawing.Point( 209, 464 );
			this.cancelButton.Name = "cancelButton";
			this.cancelButton.Size = new System.Drawing.Size( 60, 23 );
			this.cancelButton.TabIndex = 2;
			this.cancelButton.Text = "Cancel";
			this.cancelButton.UseVisualStyleBackColor = true;
			this.cancelButton.Click += new System.EventHandler( this.cancelButton_Click );
			// 
			// editAnnotationButton
			// 
			this.editAnnotationButton.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right ) ) );
			this.editAnnotationButton.Location = new System.Drawing.Point( 77, 12 );
			this.editAnnotationButton.Name = "editAnnotationButton";
			this.editAnnotationButton.Size = new System.Drawing.Size( 60, 23 );
			this.editAnnotationButton.TabIndex = 8;
			this.editAnnotationButton.Text = "Edit";
			this.editAnnotationButton.UseVisualStyleBackColor = true;
			this.editAnnotationButton.Click += new System.EventHandler( this.editAnnotationButton_Click );
			// 
			// clearAnnotationsButton
			// 
			this.clearAnnotationsButton.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right ) ) );
			this.clearAnnotationsButton.Location = new System.Drawing.Point( 209, 12 );
			this.clearAnnotationsButton.Name = "clearAnnotationsButton";
			this.clearAnnotationsButton.Size = new System.Drawing.Size( 60, 23 );
			this.clearAnnotationsButton.TabIndex = 7;
			this.clearAnnotationsButton.Text = "Clear";
			this.clearAnnotationsButton.UseVisualStyleBackColor = true;
			this.clearAnnotationsButton.Click += new System.EventHandler( this.clearAnnotationsButton_Click );
			// 
			// annotationListBox
			// 
			this.annotationListBox.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom )
						| System.Windows.Forms.AnchorStyles.Left )
						| System.Windows.Forms.AnchorStyles.Right ) ) );
			this.annotationListBox.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
			this.annotationListBox.Font = new System.Drawing.Font( "Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
			this.annotationListBox.FormattingEnabled = true;
			this.annotationListBox.ItemHeight = 14;
			this.annotationListBox.Location = new System.Drawing.Point( 12, 41 );
			this.annotationListBox.Name = "annotationListBox";
			this.annotationListBox.Size = new System.Drawing.Size( 256, 410 );
			this.annotationListBox.TabIndex = 4;
			this.annotationListBox.DrawItem += new System.Windows.Forms.DrawItemEventHandler( this.annotationListBox_DrawItem );
			// 
			// removeAnnotationButton
			// 
			this.removeAnnotationButton.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right ) ) );
			this.removeAnnotationButton.Location = new System.Drawing.Point( 143, 12 );
			this.removeAnnotationButton.Name = "removeAnnotationButton";
			this.removeAnnotationButton.Size = new System.Drawing.Size( 60, 23 );
			this.removeAnnotationButton.TabIndex = 6;
			this.removeAnnotationButton.Text = "Remove";
			this.removeAnnotationButton.UseVisualStyleBackColor = true;
			this.removeAnnotationButton.Click += new System.EventHandler( this.removeAnnotationButton_Click );
			// 
			// addAnnotationButton
			// 
			this.addAnnotationButton.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right ) ) );
			this.addAnnotationButton.Location = new System.Drawing.Point( 11, 12 );
			this.addAnnotationButton.Name = "addAnnotationButton";
			this.addAnnotationButton.Size = new System.Drawing.Size( 60, 23 );
			this.addAnnotationButton.TabIndex = 5;
			this.addAnnotationButton.Text = "Add";
			this.addAnnotationButton.UseVisualStyleBackColor = true;
			this.addAnnotationButton.Click += new System.EventHandler( this.addAnnotationButton_Click );
			// 
			// AnnotationEditForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			this.CancelButton = this.cancelButton;
			this.ClientSize = new System.Drawing.Size( 280, 499 );
			this.ControlBox = false;
			this.Controls.Add( this.clearAnnotationsButton );
			this.Controls.Add( this.editAnnotationButton );
			this.Controls.Add( this.removeAnnotationButton );
			this.Controls.Add( this.cancelButton );
			this.Controls.Add( this.annotationListBox );
			this.Controls.Add( this.addAnnotationButton );
			this.Controls.Add( this.okButton );
			this.MinimumSize = new System.Drawing.Size( 188, 200 );
			this.Name = "AnnotationEditForm";
			this.ShowInTaskbar = false;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Manual Annotation Editor";
			this.ResumeLayout( false );

		}

		#endregion

		private System.Windows.Forms.Button okButton;
		private System.Windows.Forms.Button cancelButton;
		private System.Windows.Forms.Button editAnnotationButton;
		private System.Windows.Forms.Button clearAnnotationsButton;
		private System.Windows.Forms.ListBox annotationListBox;
		private System.Windows.Forms.Button removeAnnotationButton;
		private System.Windows.Forms.Button addAnnotationButton;
	}
}