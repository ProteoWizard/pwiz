namespace seems
{
	partial class AnnotationEditAddEditDialog
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
			this.labelTextBox = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.colorDialog1 = new System.Windows.Forms.ColorDialog();
			this.label3 = new System.Windows.Forms.Label();
			this.button1 = new System.Windows.Forms.Button();
			this.button2 = new System.Windows.Forms.Button();
			this.colorBox = new System.Windows.Forms.UserControl();
			this.pointTextBox = new System.Windows.Forms.TextBox();
			this.pointTextBoxLabel = new System.Windows.Forms.Label();
			this.widthUpDown = new System.Windows.Forms.NumericUpDown();
			this.label4 = new System.Windows.Forms.Label();
			( (System.ComponentModel.ISupportInitialize) ( this.widthUpDown ) ).BeginInit();
			this.SuspendLayout();
			// 
			// labelTextBox
			// 
			this.labelTextBox.Location = new System.Drawing.Point( 82, 27 );
			this.labelTextBox.Name = "labelTextBox";
			this.labelTextBox.Size = new System.Drawing.Size( 82, 20 );
			this.labelTextBox.TabIndex = 1;
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point( 84, 9 );
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size( 82, 15 );
			this.label1.TabIndex = 2;
			this.label1.Text = "Full Label";
			this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			// 
			// colorDialog1
			// 
			this.colorDialog1.SolidColorOnly = true;
			// 
			// label3
			// 
			this.label3.Location = new System.Drawing.Point( 167, 9 );
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size( 33, 15 );
			this.label3.TabIndex = 5;
			this.label3.Text = "Color";
			this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			// 
			// button1
			// 
			this.button1.Location = new System.Drawing.Point( 36, 53 );
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size( 82, 23 );
			this.button1.TabIndex = 4;
			this.button1.Text = "OK";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler( this.button1_Click );
			// 
			// button2
			// 
			this.button2.Location = new System.Drawing.Point( 127, 53 );
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size( 86, 23 );
			this.button2.TabIndex = 5;
			this.button2.Text = "Cancel";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler( this.button2_Click );
			// 
			// colorBox
			// 
			this.colorBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.colorBox.Location = new System.Drawing.Point( 170, 27 );
			this.colorBox.Name = "colorBox";
			this.colorBox.Size = new System.Drawing.Size( 29, 20 );
			this.colorBox.TabIndex = 2;
			this.colorBox.Paint += new System.Windows.Forms.PaintEventHandler( this.colorBox_Paint );
			this.colorBox.Click += new System.EventHandler( this.colorBox_Click );
			// 
			// pointTextBox
			// 
			this.pointTextBox.Location = new System.Drawing.Point( 12, 27 );
			this.pointTextBox.Name = "pointTextBox";
			this.pointTextBox.Size = new System.Drawing.Size( 64, 20 );
			this.pointTextBox.TabIndex = 0;
			this.pointTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler( this.pointTextBox_KeyDown );
			// 
			// pointTextBoxLabel
			// 
			this.pointTextBoxLabel.Location = new System.Drawing.Point( 9, 10 );
			this.pointTextBoxLabel.Name = "pointTextBoxLabel";
			this.pointTextBoxLabel.Size = new System.Drawing.Size( 69, 15 );
			this.pointTextBoxLabel.TabIndex = 10;
			this.pointTextBoxLabel.Text = "Point";
			this.pointTextBoxLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			// 
			// widthUpDown
			// 
			this.widthUpDown.Location = new System.Drawing.Point( 205, 27 );
			this.widthUpDown.Maximum = new decimal( new int[] {
            5,
            0,
            0,
            0} );
			this.widthUpDown.Minimum = new decimal( new int[] {
            1,
            0,
            0,
            0} );
			this.widthUpDown.Name = "widthUpDown";
			this.widthUpDown.Size = new System.Drawing.Size( 28, 20 );
			this.widthUpDown.TabIndex = 3;
			this.widthUpDown.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
			this.widthUpDown.Value = new decimal( new int[] {
            1,
            0,
            0,
            0} );
			this.widthUpDown.ValueChanged += new System.EventHandler( this.widthUpDown_ValueChanged );
			// 
			// label4
			// 
			this.label4.Location = new System.Drawing.Point( 200, 10 );
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size( 40, 15 );
			this.label4.TabIndex = 12;
			this.label4.Text = "Width";
			this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			// 
			// AnnotationEditAddEditDialog
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size( 248, 82 );
			this.Controls.Add( this.label4 );
			this.Controls.Add( this.widthUpDown );
			this.Controls.Add( this.pointTextBoxLabel );
			this.Controls.Add( this.pointTextBox );
			this.Controls.Add( this.colorBox );
			this.Controls.Add( this.button2 );
			this.Controls.Add( this.button1 );
			this.Controls.Add( this.label3 );
			this.Controls.Add( this.label1 );
			this.Controls.Add( this.labelTextBox );
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
			this.Name = "AnnotationEditAddEditDialog";
			this.Text = "Add/Edit Manual Annotation";
			( (System.ComponentModel.ISupportInitialize) ( this.widthUpDown ) ).EndInit();
			this.ResumeLayout( false );
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.TextBox labelTextBox;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.ColorDialog colorDialog1;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Button button1;
		private System.Windows.Forms.Button button2;
		private System.Windows.Forms.UserControl colorBox;
		private System.Windows.Forms.TextBox pointTextBox;
		private System.Windows.Forms.Label pointTextBoxLabel;
		private System.Windows.Forms.NumericUpDown widthUpDown;
		private System.Windows.Forms.Label label4;
	}
}