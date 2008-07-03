namespace seems
{
	partial class PeptideFragmentationForm
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
			this.webBrowser1 = new System.Windows.Forms.WebBrowser();
			this.SuspendLayout();
			// 
			// webBrowser1
			// 
			this.webBrowser1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.webBrowser1.Location = new System.Drawing.Point( 0, 0 );
			this.webBrowser1.MinimumSize = new System.Drawing.Size( 20, 20 );
			this.webBrowser1.Name = "webBrowser1";
			this.webBrowser1.Size = new System.Drawing.Size( 892, 866 );
			this.webBrowser1.TabIndex = 0;
			this.webBrowser1.Url = new System.Uri( "http://prospector.ucsf.edu/prospector/4.27.1/cgi-bin/msform.cgi?form=msproduct", System.UriKind.Absolute );
			this.webBrowser1.DocumentCompleted += new System.Windows.Forms.WebBrowserDocumentCompletedEventHandler( this.webBrowser1_DocumentCompleted );
			// 
			// PeptideFragmentationForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			this.ClientSize = new System.Drawing.Size( 892, 866 );
			this.Controls.Add( this.webBrowser1 );
			this.Name = "PeptideFragmentationForm";
			this.ShowInTaskbar = false;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Annotate Peptide Fragmentation";
			this.ResumeLayout( false );

		}

		#endregion

		private System.Windows.Forms.WebBrowser webBrowser1;

	}
}