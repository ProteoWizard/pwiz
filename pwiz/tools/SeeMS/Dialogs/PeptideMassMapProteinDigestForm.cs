using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace seems
{
	public partial class PeptideMassMapProteinDigestForm : Form
	{
		private static string BaseURL = "http://prospector.ucsf.edu";

		public PeptideMassMapProteinDigestForm()
		{
			InitializeComponent();
			webBrowser1.Navigate(BaseURL + "/cgi-bin/msform.cgi?form=msdigest");
		}

		public string MsDigestReportXml { get { return msDigestReportXml; } }
		private string msDigestReportXml;

		private void webBrowser1_DocumentCompleted( object sender, WebBrowserDocumentCompletedEventArgs e )
		{
			if( webBrowser1.Url.ToString() == BaseURL + "/cgi-bin/mssearch.cgi" )
			{
				string defacedXml = webBrowser1.DocumentText;
				defacedXml = defacedXml.Replace( "\r\n", "" );
				defacedXml = Regex.Replace( defacedXml, "<STYLE>.*</STYLE>", "", RegexOptions.IgnoreCase );
				defacedXml = Regex.Replace( defacedXml, "<SCRIPT>.*</SCRIPT>", "", RegexOptions.IgnoreCase );
				defacedXml = Regex.Replace( defacedXml, "<A .*?>-</A>", "", RegexOptions.IgnoreCase );
				defacedXml = Regex.Replace( defacedXml, "<.+?>", "" );
				defacedXml = defacedXml.Replace( "&nbsp;", "" );
				defacedXml = defacedXml.Replace( "&lt;", "<" );
				defacedXml = defacedXml.Replace( "&gt;", ">" );
				msDigestReportXml = defacedXml;
				DialogResult = DialogResult.OK;
				this.Close();
			} else if( webBrowser1.Url.ToString() == BaseURL + "/cgi-bin/msform.cgi?form=msdigest" )
			{
				// set up SeeMS-friendly form options
				webBrowser1.Document.GetElementById( "output_type" ).Children[1].SetAttribute( "selected", "selected" );
				webBrowser1.Document.GetElementById( "hide_html_links" ).Style = "display: none";
				webBrowser1.Document.GetElementById( "output_type" ).Style = "display: none";
				webBrowser1.Document.GetElementById( "results_to_file" ).Style = "display: none";
				webBrowser1.Document.GetElementById( "output_filename" ).Style = "display: none";
			}
		}
	}
}