namespace HeaderComboBox
{
	partial class HeaderComboBoxControl
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
			this.button1 = new System.Windows.Forms.Button();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.SuspendLayout();
			// 
			// button1
			// 
			this.button1.Anchor = System.Windows.Forms.AnchorStyles.None;
			this.button1.Font = new System.Drawing.Font( "Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
			this.button1.Location = new System.Drawing.Point( 130, 0 );
			this.button1.Margin = new System.Windows.Forms.Padding( 0 );
			this.button1.MaximumSize = new System.Drawing.Size( 20, 20 );
			this.button1.MinimumSize = new System.Drawing.Size( 20, 20 );
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size( 20, 20 );
			this.button1.TabIndex = 0;
			this.button1.Text = "v";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler( this.button1_Click );
			// 
			// textBox1
			// 
			this.textBox1.Anchor = System.Windows.Forms.AnchorStyles.None;
			this.textBox1.Location = new System.Drawing.Point( 0, 0 );
			this.textBox1.Margin = new System.Windows.Forms.Padding( 0 );
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size( 130, 20 );
			this.textBox1.TabIndex = 1;
			// 
			// HeaderComboBoxControl
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			this.Controls.Add( this.textBox1 );
			this.Controls.Add( this.button1 );
			this.Name = "HeaderComboBoxControl";
			this.Layout += new System.Windows.Forms.LayoutEventHandler( this.HeaderComboBoxControl_Layout );
			this.ResumeLayout( false );
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Button button1;
		private System.Windows.Forms.TextBox textBox1;
	}
}
