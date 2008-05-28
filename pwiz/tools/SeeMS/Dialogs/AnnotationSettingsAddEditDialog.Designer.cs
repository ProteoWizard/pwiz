namespace seems
{
	partial class AnnotationSettingsAddEditDialog
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
			this.aliasTextBox = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.colorDialog1 = new System.Windows.Forms.ColorDialog();
			this.label3 = new System.Windows.Forms.Label();
			this.button1 = new System.Windows.Forms.Button();
			this.button2 = new System.Windows.Forms.Button();
			this.colorBox = new System.Windows.Forms.UserControl();
			this.SuspendLayout();
			// 
			// labelTextBox
			// 
			this.labelTextBox.Location = new System.Drawing.Point( 12, 27 );
			this.labelTextBox.Name = "labelTextBox";
			this.labelTextBox.Size = new System.Drawing.Size( 82, 20 );
			this.labelTextBox.TabIndex = 0;
			// 
			// aliasTextBox
			// 
			this.aliasTextBox.Location = new System.Drawing.Point( 100, 27 );
			this.aliasTextBox.Name = "aliasTextBox";
			this.aliasTextBox.Size = new System.Drawing.Size( 46, 20 );
			this.aliasTextBox.TabIndex = 1;
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point( 12, 9 );
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size( 82, 15 );
			this.label1.TabIndex = 2;
			this.label1.Text = "Full Label";
			this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			// 
			// label2
			// 
			this.label2.Location = new System.Drawing.Point( 100, 9 );
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size( 46, 15 );
			this.label2.TabIndex = 3;
			this.label2.Text = "Alias";
			this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			// 
			// colorDialog1
			// 
			this.colorDialog1.SolidColorOnly = true;
			// 
			// label3
			// 
			this.label3.Location = new System.Drawing.Point( 152, 9 );
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size( 33, 15 );
			this.label3.TabIndex = 5;
			this.label3.Text = "Color";
			this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			// 
			// button1
			// 
			this.button1.Location = new System.Drawing.Point( 12, 53 );
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size( 82, 23 );
			this.button1.TabIndex = 6;
			this.button1.Text = "OK";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler( this.button1_Click );
			// 
			// button2
			// 
			this.button2.Location = new System.Drawing.Point( 100, 53 );
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size( 86, 23 );
			this.button2.TabIndex = 7;
			this.button2.Text = "Cancel";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler( this.button2_Click );
			// 
			// colorBox
			// 
			this.colorBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.colorBox.Location = new System.Drawing.Point( 155, 27 );
			this.colorBox.Name = "colorBox";
			this.colorBox.Size = new System.Drawing.Size( 29, 19 );
			this.colorBox.TabIndex = 8;
			this.colorBox.Paint += new System.Windows.Forms.PaintEventHandler( this.colorBox_Paint );
			this.colorBox.Click += new System.EventHandler( this.colorBox_Click );
			// 
			// AnnotationSettingsAddEditDialog
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size( 198, 82 );
			this.Controls.Add( this.colorBox );
			this.Controls.Add( this.button2 );
			this.Controls.Add( this.button1 );
			this.Controls.Add( this.label3 );
			this.Controls.Add( this.label2 );
			this.Controls.Add( this.label1 );
			this.Controls.Add( this.aliasTextBox );
			this.Controls.Add( this.labelTextBox );
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
			this.Name = "AnnotationSettingsAddEditDialog";
			this.Text = "Add/Edit Alias/Color Mapping";
			this.ResumeLayout( false );
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.TextBox labelTextBox;
		private System.Windows.Forms.TextBox aliasTextBox;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.ColorDialog colorDialog1;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Button button1;
		private System.Windows.Forms.Button button2;
		private System.Windows.Forms.UserControl colorBox;
	}
}