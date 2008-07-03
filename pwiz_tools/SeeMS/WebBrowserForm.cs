using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using ManagedLibMSR;
using Extensions;

using Chromatogram = Extensions.Map<double, double>;
using FragmentToChromatogramMap = Extensions.Map<double, Extensions.Map<double, double>>;
using ParentToFragmentMap = Extensions.Map<double, Extensions.Map<double, Extensions.Map<double, double>>>;

namespace seems
{
	public partial class WebBrowserForm : Form
	{
		private HeaderComboBox.HeaderComboBoxControl scanNumberComboBox = new HeaderComboBox.HeaderComboBoxControl();
		private ToolStripControlHost scanNumberComboBoxToolStripHost;
		private System.Windows.Forms.Timer scanNumberChangedTimer = new System.Windows.Forms.Timer();
		private System.Windows.Forms.Timer webBrowserLeftKeyDownTimer = new System.Windows.Forms.Timer();
		private System.Windows.Forms.Timer webBrowserRightKeyDownTimer = new System.Windows.Forms.Timer();
		public Dictionary<string, List<KeyValuePair<double, int>>> peakLabels = new Dictionary<string, List<KeyValuePair<double, int>>>();
		public Dictionary<string, KeyValuePair<string, string>> peakAnnotationSettings;
		private ManagedInstrumentInterface instrumentInterface;
		private string scanNumberHeaderString = String.Format( "{0,-8}{1,-10}{2,-5}{3,-15:E}{4,15}",
			"Number", "Time (s)", "MSn", "TIC", "Parent M/Z" );
		private string sourceFilepath;
		private string navigatingQuery = "";

		public string CurrentSourceFilepath { get { return sourceFilepath; } set { sourceFilepath = value; } }
		public GraphItem CurrentScan { get { return (GraphItem) scanNumberComboBox.SelectedItem; } }
		public int CurrentScanIndex { get { return scanNumberComboBox.SelectedIndex; } }
		public string CurrentNavigatingQuery { get { return navigatingQuery; } }

		public ManagedInstrumentInterface InstrumentInterface { get { return instrumentInterface; } }
		public HeaderComboBox.HeaderComboBoxControl ScanNumberComboBox { get { return scanNumberComboBox; } }
		public ToolStripControlHost ScanNumberComboBoxHost { get { return scanNumberComboBoxToolStripHost; } }

		private seems SeemsMdiParent { get { return (seems) MdiParent; } }

		public WebBrowserForm()
		{
			InitForm(null);
		}

		public WebBrowserForm( Form mdiParent )
		{
			InitForm(mdiParent);
		}

		/*private double curMRMPrecursor;
		private bool MRMFilter( ManagedScan scan )
		{
			if( scan.getPrecursorScanInfo( 0 ).Mz == curMRMPrecursor )
				return true;
			return false;
		}*/

		private void InitForm( Form mdiParent )
		{
			MdiParent = mdiParent;
			InitializeComponent();

			initPeakAnnotationSettings();

			scanNumberComboBox.AccessibleName = "scanNumberComboBox";
			scanNumberComboBox.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right ) ) );
			scanNumberComboBox.Enabled = false;
			scanNumberComboBox.IntegralHeight = true;
			scanNumberComboBox.ItemHeight = 13;
			scanNumberComboBox.ListDisplayMember = "ComboBoxView";
			scanNumberComboBox.Location = new System.Drawing.Point( 397, 3 );
			scanNumberComboBox.Name = "scanNumberComboBox";
			scanNumberComboBox.SelectedIndex = -1;
			scanNumberComboBox.SelectedItem = null;
			scanNumberComboBox.SelectedValue = null;
			scanNumberComboBox.Size = new System.Drawing.Size( 72, 22 );
			scanNumberComboBox.TabIndex = 8;
			scanNumberComboBox.TextDisplayMember = "Id";
			scanNumberComboBox.ValueMember = "Id";
			scanNumberComboBox.ListBox.SelectedIndexChanged += new EventHandler( scanNumberComboBox_SelectedIndexChanged );
			scanNumberComboBox.ListHeaderText = scanNumberHeaderString;
			scanNumberComboBoxToolStripHost = new ToolStripControlHost( scanNumberComboBox );
			scanNumberComboBoxToolStripHost.Alignment = ToolStripItemAlignment.Right;
			if( SeemsMdiParent != null )
				SeemsMdiParent.ToolStrip1.Items.Insert( SeemsMdiParent.ToolStrip1.Items.IndexOf( SeemsMdiParent.ToolStripScanLabel ), scanNumberComboBoxToolStripHost );

			scanNumberChangedTimer.Tick += new EventHandler( scanNumberChangedTimer_Tick );
			scanNumberChangedTimer.Interval = 500;

			webBrowserLeftKeyDownTimer.Tick += new EventHandler( webBrowserLeftKeyDownTimer_Tick );
			webBrowserLeftKeyDownTimer.Interval = 50;

			webBrowserRightKeyDownTimer.Tick += new EventHandler( webBrowserRightKeyDownTimer_Tick );
			webBrowserRightKeyDownTimer.Interval = 50;

			instrumentInterface = new ManagedInstrumentInterface();
			if( !instrumentInterface.initInterface() )
			{
				MessageBox.Show( "Fatal error: failed to initialize data interface." );
				Process.GetCurrentProcess().Kill();
			}
		}

		private void WebBrowserForm_Layout( object sender, LayoutEventArgs e )
		{
			if( WindowState == FormWindowState.Maximized )
				FormBorderStyle = FormBorderStyle.None;
			else
				FormBorderStyle = FormBorderStyle.Sizable;
		}

		private void initPeakAnnotationSettings()
		{
			// ms-product-label -> (svg label, svg color)
			peakAnnotationSettings = new Dictionary<string, KeyValuePair<string, string>>();
			peakAnnotationSettings["y"] = new KeyValuePair<string, string>( "y", "blue" );
			peakAnnotationSettings["b"] = new KeyValuePair<string, string>( "b", "red" );
			peakAnnotationSettings["y-NH3"] = new KeyValuePair<string, string>( "y^", "green" );
			peakAnnotationSettings["y-H2O"] = new KeyValuePair<string, string>( "y*", "cyan" );
			peakAnnotationSettings["b-NH3"] = new KeyValuePair<string, string>( "b^", "orange" );
			peakAnnotationSettings["b-H2O"] = new KeyValuePair<string, string>( "b*", "violet" );
		}

		public bool readScanMetadata(string sourceFilepath)
		{
			try
			{
				if( !File.Exists( sourceFilepath ) )
					return false;
				CurrentSourceFilepath = sourceFilepath;

				SeemsMdiParent.setFileControls( false );
				SeemsMdiParent.setScanControls( false );

				SeemsMdiParent.StatusLabel.Text = "Loading source metadata...";
				SeemsMdiParent.StatusProgressBar.MarqueeAnimationSpeed = 100;
				Application.DoEvents();
				if( !instrumentInterface.setInputFile( CurrentSourceFilepath ) )
					throw new Exception( "failed to open file \"" + CurrentSourceFilepath + "\"" );
				instrumentInterface.setCentroiding( false, false );
				SeemsMdiParent.StatusProgressBar.MarqueeAnimationSpeed = 0;

			} catch( Exception ex )
			{
				string message = "SeeMS encountered an error opening the source file you specified (" + ex.Message + ")";
				if( ex.InnerException != null )
					message += "\n\nAdditional information: " + ex.InnerException.Message;
				MessageBox.Show( message,
								"Error opening source file",
								MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
								0, false );
				SeemsMdiParent.setFileControls( true );
				return false;
			}

			try
			{
				scanNumberComboBox.Items.Clear();
				SeemsMdiParent.StatusProgressBar.Visible = true;
				SeemsMdiParent.StatusProgressBar.Value = 0;
				SeemsMdiParent.StatusProgressBar.Minimum = 0;
				SeemsMdiParent.StatusProgressBar.Maximum = instrumentInterface.getTotalScanCount();
				SeemsMdiParent.StatusProgressBar.Step = 1;
				Application.DoEvents();

				scanNumberComboBox.BeginUpdate();

				ManagedScan chromatogram = instrumentInterface.getChromatogram();
				if( chromatogram == null )
					throw new Exception( "failed to generate chromatogram" );
				else
					scanNumberComboBox.Items.Add( new SeemsScan( chromatogram, "Chromatogram" ) );

				ParentToFragmentMap transitionMap = new ParentToFragmentMap();
				//Dictionary<double, int> transitionMap = new Dictionary<double,int>();

				//long beforeScanHeaders = Environment.WorkingSet;
				// get all scans by sequential access
				ManagedScan curScan = instrumentInterface.getScanHeader( instrumentInterface.getFirstScanNumber() );
				while( curScan != null )
				{
					SeemsMdiParent.StatusProgressBar.PerformStep();
					Application.DoEvents();
					if( curScan.ScanType == ScanTypeEnum.SRM || curScan.ScanType == ScanTypeEnum.MRM )
					{
						ManagedScan curScanWithPeakData = instrumentInterface.getScan( curScan.ScanNumber );
						if( curScanWithPeakData.getTotalPeakCount() > 0 )
						{
							if( curScan.getPrecursorScanCount() > 0 )
							{
								FragmentToChromatogramMap fragmentMap = transitionMap[curScan.getPrecursorScanInfo( 0 ).Mz];
								for( int i = 0; i < curScanWithPeakData.getTotalPeakCount(); ++i )
									fragmentMap[curScanWithPeakData.getPeakMz( i )].Add( curScan.RetentionTime, curScanWithPeakData.getPeakIntensity( i ) );
							}
						}
						//if( curScan.getPrecursorScanCount() > 0 )
						//	transitionMap[curScan.getPrecursorScanInfo( 0 ).Mz] = 0;
					} else
						scanNumberComboBox.Items.Add( new SeemsScan( curScan ) );
					curScan = instrumentInterface.getScanHeader();
				}

				if( transitionMap.Count > 0 )
				{
					SeemsMdiParent.StatusLabel.Text = "Generating chromatograms for SRM/MRM data...";
					SeemsMdiParent.StatusProgressBar.Value = 0;
					SeemsMdiParent.StatusProgressBar.Minimum = 0;
					SeemsMdiParent.StatusProgressBar.Maximum = transitionMap.Count;
					SeemsMdiParent.StatusProgressBar.Step = 1;
					Application.DoEvents();
					Map<double, RefPair<ManagedScan, Map<double, ManagedScan>>> transitionChromatograms = new Map<double, RefPair<ManagedScan, Map<double, ManagedScan>>>();
					foreach( ParentToFragmentMap.MapPair pfPair in transitionMap )
					{
						Map<double, double> parentPeaks = new Map<double, double>();
						foreach( FragmentToChromatogramMap.MapPair fcPair in pfPair.Value )
						{
							ManagedScan fragmentChromatogram = transitionChromatograms[pfPair.Key].second[fcPair.Key] = new ManagedScan();
							foreach( Chromatogram.MapPair tiPair in fcPair.Value )
							{
								fragmentChromatogram.addPeak( tiPair.Key, tiPair.Value );
								parentPeaks[tiPair.Key] += tiPair.Value;
							}
						}

						ManagedScan parentChromatogram = transitionChromatograms[pfPair.Key].first = new ManagedScan();
						foreach( Map<double, double>.MapPair peak in parentPeaks )
							parentChromatogram.addPeak( peak.Key, peak.Value );
						
						SeemsMdiParent.StatusProgressBar.PerformStep();
						Application.DoEvents();
					}

					foreach( Map<double, RefPair<ManagedScan, Map<double, ManagedScan>>>.MapPair kvp1 in transitionChromatograms )
					{
						scanNumberComboBox.Items.Add( new SeemsScan( kvp1.Value.first, kvp1.Key.ToString() ) );
						foreach( Map<double, ManagedScan>.MapPair kvp2 in kvp1.Value.second )
							scanNumberComboBox.Items.Add( new SeemsScan( kvp2.Value, String.Format( "{0} -> {1}", Math.Round( kvp1.Key, 2 ), Math.Round( kvp2.Key, 2 ) ) ) );
					}
				}

				scanNumberComboBox.EndUpdate();
				SeemsMdiParent.StatusProgressBar.Visible = false;
				Application.DoEvents();
				//long afterScanHeaders = Environment.WorkingSet;
				//MessageBox.Show( "Before adding headers: " + beforeScanHeaders + "   After adding headers: " + afterScanHeaders );


				SeemsMdiParent.setFileControls( true );

				if( scanNumberComboBox.Items.Count > 0 )
				{
					SeemsMdiParent.setScanControls( true );
					scanNumberComboBox.SelectedIndex = 0; // triggers initial graph generation
				} else
				{
					SeemsMdiParent.setScanControls( false );
					return false;
				}
			} catch( Exception ex )
			{
				string message = "SeeMS encountered an error reading metadata from the source file you specified (" + ex.Message + ")";
				if( ex.InnerException != null )
					message += "\n\nAdditional information: " + ex.InnerException.Message;
				MessageBox.Show( message,
								"Error reading source metadata",
								MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
								0, false );
				SeemsMdiParent.setFileControls( true );
				return false;
			}
			return true;
		}

		private string tmpSvgFilename;
		//private decimal lastMaxPeakCountValue;
		//private string lastExtraOptionsText;
		private bool generateSpectrumSvg()
		{
			try
			{
				scanNumberChangedTimer.Stop();
				Application.DoEvents();

				if( scanNumberComboBox.Enabled == false )
					return false;

				SeemsScan selectedScan = (SeemsScan) scanNumberComboBox.SelectedItem;
				if( selectedScan.Scan.IsCentroided )
				{
					SeemsMdiParent.CentroidMenuItem.Enabled = false;
					SeemsMdiParent.CentroidMenuItem.Checked = true;
				} else
					SeemsMdiParent.CentroidMenuItem.Enabled = true;

				ManagedScan curScan;
				SortedDictionary<double, string> peakLabels2 = new SortedDictionary<double, string>();
				SortedDictionary<double, string> peakColors = new SortedDictionary<double, string>();
				SortedDictionary<double, int> peakWidths = new SortedDictionary<double, int>();

				if( selectedScan.Scan.ScanNumber > 0 )
				{
					bool doCentroid = SeemsMdiParent.CentroidMenuItem.Enabled && SeemsMdiParent.CentroidMenuItem.Checked;
					instrumentInterface.setCentroiding( doCentroid, doCentroid, SeemsMdiParent.UseVendorCentroidMenuItem.Checked );

					curScan = instrumentInterface.getScan( selectedScan.Scan.ScanNumber );
					if( curScan == null )
						throw new Exception( "failed to get scan data for scan " + selectedScan.Scan.ScanNumber );

					foreach( KeyValuePair<string, List<KeyValuePair<double, int>>> ionListItr in peakLabels )
					{
						foreach( KeyValuePair<double, int> ionMzChargePair in ionListItr.Value )
						{
							string[] labelNameIndexPair = ionListItr.Key.Split( " ".ToCharArray() );
							if( peakAnnotationSettings.ContainsKey( labelNameIndexPair[0] ) )
							{
								KeyValuePair<string, string> svgLabelColorPair = peakAnnotationSettings[labelNameIndexPair[0]];
								if( ionMzChargePair.Value > 1 )
									peakLabels2[ionMzChargePair.Key] = String.Format( "{0}{1} (+{2})", svgLabelColorPair.Key, labelNameIndexPair[1], ionMzChargePair.Value );
								else
									peakLabels2[ionMzChargePair.Key] = String.Format( "{0}{1}", svgLabelColorPair.Key, labelNameIndexPair[1] );

								peakColors[ionMzChargePair.Key] = svgLabelColorPair.Value;
							} else
							{
								peakLabels2[ionMzChargePair.Key] = ionListItr.Key;
								peakColors[ionMzChargePair.Key] = "blue";
							}
							peakWidths[ionMzChargePair.Key] = 2;
						}
					}

					SeemsMdiParent.PeakProcessingButton.Enabled = true;
					SeemsMdiParent.AnnotateButton.Enabled = true;
				} else
				{
					curScan = selectedScan.Scan;
					SeemsMdiParent.PeakProcessingButton.Enabled = false;
					SeemsMdiParent.AnnotateButton.Enabled = false;
				}

				String tmpSvgFilename = Path.GetTempPath() + "seems.svg";
				StreamWriter tmpSvgFile = new StreamWriter( tmpSvgFilename );
				tmpSvgFile.Write( curScan.writeToSvg( peakLabels2, peakColors, peakWidths ) );
				tmpSvgFile.Close();
				webBrowser1.Url = new Uri( "file://" + tmpSvgFilename + navigatingQuery.ToString() );
				Process.GetCurrentProcess().Exited += new EventHandler( generateSpectrumSvg_cleanup );

				scanNumberChangedTimer.Stop();
				webBrowser1.Focus();
				//Application.DoEvents();
			} catch( Exception ex )
			{
				string message = "SeeMS encountered an error generating the spectrum graphics (" + ex.Message + ")";
				if( ex.InnerException != null )
					message += "\n\nAdditional information: " + ex.InnerException.Message;
				MessageBox.Show( message,
								"Error generating spectrum graphics",
								MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
								0, false );
				return false;
			}
			return true;
		}

		private void generateSpectrumSvg_cleanup( object sender, EventArgs e )
		{
			if( tmpSvgFilename.Length > 0 )
			{
				File.Delete( tmpSvgFilename );
				tmpSvgFilename = "";
			}
		}

		public void updateSvg()
		{
			long start = DateTime.Now.Ticks;
			if( generateSpectrumSvg() )
				SeemsMdiParent.StatusLabel.Text = "Scan graph generated in " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds.";
		}

		private void scanNumberComboBox_SelectedIndexChanged( object sender, EventArgs e )
		{
			if( scanNumberChangedTimer.Enabled )
			{
				// restart the timer
				scanNumberChangedTimer.Stop();
			}
			scanNumberChangedTimer.Start();
		}

		private void scanNumberChangedTimer_Tick( object sender, EventArgs e )
		{
			updateSvg();
		}

		private void webBrowser1_PreviewKeyDown( object sender, PreviewKeyDownEventArgs e )
		{
			int key = (int) e.KeyCode;
			if( key == (int) Keys.Left && scanNumberComboBox.SelectedIndex > 0 )
			{
				if( webBrowserLeftKeyDownTimer.Enabled )
				{
					// restart the timer
					webBrowserLeftKeyDownTimer.Stop();
				}
				webBrowserLeftKeyDownTimer.Start();

			} else if( key == (int) Keys.Right && scanNumberComboBox.SelectedIndex < scanNumberComboBox.Items.Count - 1 )
			{
				if( webBrowserRightKeyDownTimer.Enabled )
				{
					// restart the timer
					webBrowserRightKeyDownTimer.Stop();
				}
				webBrowserRightKeyDownTimer.Start();
			}
		}

		private void webBrowserLeftKeyDownTimer_Tick( object sender, EventArgs e )
		{
			webBrowserLeftKeyDownTimer.Stop();
			scanNumberComboBox.SelectedIndex = scanNumberComboBox.SelectedIndex - 1;
		}

		private void webBrowserRightKeyDownTimer_Tick( object sender, EventArgs e )
		{
			webBrowserRightKeyDownTimer.Stop();
			scanNumberComboBox.SelectedIndex = scanNumberComboBox.SelectedIndex + 1;
		}

		private void webBrowser1_Navigating( object sender, WebBrowserNavigatingEventArgs e )
		{
			Match svgMessageMatch = Regex.Match( e.Url.ToString(), "javascript:window\\.navigate\\(\"(\\?.*)\"\\)" );
			if( svgMessageMatch.Success )
			{
				navigatingQuery = svgMessageMatch.Groups[1].ToString();
				e.Cancel = true;
			}
		}
	}
}