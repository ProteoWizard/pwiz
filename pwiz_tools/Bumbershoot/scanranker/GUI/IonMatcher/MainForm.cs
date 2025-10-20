//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Initial Developer of the DirecTag peptide sequence tagger is Matt Chambers.
// Contributor(s): Surendra Dasaris
//
// The Initial Developer of the ScanRanker GUI is Zeqiang Ma.
// Contributor(s): 
//
// Copyright 2009 Vanderbilt University
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Collections;
using System.IO;
using System.Drawing.Imaging;

using pwiz.CLI;
using pwiz.CLI.cv;
using pwiz.CLI.msdata;
using pwiz.CLI.analysis;


namespace IonMatcher
{
    public partial  class MainForm : Form
    {
        DataGridViewRow currentPepGridSelection;

        public MainForm()
        {
            InitializeComponent();

            //tbSrcDir.Text = @"C:\Documents and Settings\maz\My Documents\IonMatch-test;";
            //tbOutDir.Text = @"C:\Documents and Settings\maz\My Documents\IonMatch-test";
            //tbSpecFile.Text = @"C:\Data\test\Vanacore\sh_2251_RV_060611p_C4_6.mzXML";
            //tbMetricsFile.Text = @"C:\Data\test\Vanacore\sh_2251_RV_060611p_C4_6-ScanRankerMetrics-local.txt";
        }

        public static string OpenFileBrowseDialog(string sPrevFile)
        {
            try
            {
                OpenFileDialog dlgBrowseSource = new OpenFileDialog();

                if (!sPrevFile.Equals(string.Empty))
                {
                    dlgBrowseSource.InitialDirectory = sPrevFile;
                }
                else
                {
                    dlgBrowseSource.InitialDirectory = "c:\\";
                }

                DialogResult result = dlgBrowseSource.ShowDialog();

                if (result == DialogResult.OK)
                {
                    return dlgBrowseSource.FileName;
                }

                return string.Empty;

            }
            catch (Exception exc)
            {
                throw new Exception("Error opening file dialog\r\n", exc);
            }
        }

        //public static string OpenDirBrowseDialog(string sPrevDir, Boolean newFolderOption)
        //{
        //    //try
        //    //{
        //        FolderBrowserDialog dlgBrowseSource = new FolderBrowserDialog();

        //        if (!sPrevDir.Equals(string.Empty))
        //        {
        //            dlgBrowseSource.SelectedPath = sPrevDir;
        //        }
        //        else
        //        {
        //            dlgBrowseSource.SelectedPath = "c:\\";
        //        }

        //        dlgBrowseSource.ShowNewFolderButton = newFolderOption;

        //        DialogResult result = dlgBrowseSource.ShowDialog();

        //        if (result == DialogResult.OK)
        //        {
        //            return dlgBrowseSource.SelectedPath;
        //        }

        //        return string.Empty;

        //    //}
        //    //catch (Exception exc)
        //    //{
        //    //    throw new Exception("Error opening directory dialog\r\n", exc);
        //    //}

        //}

        private void btnSpecFileBrowse_Click(object sender, EventArgs e)
        {
            //try
            //{
            //    string selFile = OpenDirBrowseDialog(tbSpecFile.Text,false);
            //    if (!selFile.Equals(string.Empty))
            //    {
            //        tbOutDir.Text = selFile;  // default src dir as output dir

            //        // allow multiple src folders
            //        if (Properties.Settings.Default.SourcePath.EndsWith(";"))
            //            Properties.Settings.Default.SourcePath += selFile + ";";
            //        else
            //            Properties.Settings.Default.SourcePath += ";" + selFile + ";";

            //        tbSpecFile.Text = Properties.Settings.Default.SourcePath; ;
            //    }

            //}
            //catch (Exception exc)
            //{
            //    throw new Exception("Error opening direcoty dialog\r\n", exc);
            //    //HandleExceptions(exc);
            //}
            try
            {
                string selFile = OpenFileBrowseDialog(tbSpecFile.Text);
                if (!selFile.Equals(string.Empty))
                {
                    tbSpecFile.Text = selFile;                   
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error opening file dialog\r\n", exc);
            }


        }

        private void btnMetricsFileBrowser_Click(object sender, EventArgs e)
        {
            try
            {
                string selFile = OpenFileBrowseDialog(tbMetricsFile.Text);
                if (!selFile.Equals(string.Empty))
                {
                    tbMetricsFile.Text = selFile;
                    //string fileBaseName = Path.GetFileNameWithoutExtension(selFile);
                    //tbOutFilePrefix.Text = fileBaseName;
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error opening file dialog\r\n", exc);
            }

        }

        //private void btnOutDirBrowser_Click(object sender, EventArgs e)
        //{
        //    try
        //    {
        //        string selFile = OpenDirBrowseDialog(tbOutDir.Text, true);
        //        if (!selFile.Equals(string.Empty))
        //        {
        //            tbOutDir.Text = selFile;
                    
        //        }
        //    }
        //    catch (Exception exc)
        //    {
        //        throw new Exception("Error opening direcoty dialog\r\n", exc);
        //    }
        //}

        //public static string[] FindFileInSearchPath (string fileNameWithoutExtension,
        //                                             string[] matchingFileExtensions,
        //                                             string[] directoriesToSearch,
        //                                             bool stopAtFirstMatch)
        //{
        //    List<string> fileMatches = new List<string>();
        //    foreach (string searchPath in directoriesToSearch)
        //    {
        //        DirectoryInfo dir = new DirectoryInfo(searchPath);
        //        foreach (string ext in matchingFileExtensions)
        //        {
        //            string queryPath = Path.Combine(dir.FullName, fileNameWithoutExtension + "." + ext);
        //            if (File.Exists(queryPath))
        //            {
        //                fileMatches.Add(queryPath);
        //                if (stopAtFirstMatch)
        //                    break;
        //            }
        //        }

        //        if (stopAtFirstMatch && fileMatches.Count > 0)
        //            break;
        //    }

        //    return fileMatches.ToArray();
        //}

 

        SpectrumViewer currentSpectrumViewer;

        /// <summary>
        /// This function trigges when an interpretation is double clicked. 
        /// It prepares the annotation and spectrum viewer panels.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void triggerSpectrumViewer(object sender, EventArgs e)
        {
            
            // Get the current row
            DataGridViewRow row = pepGridView.CurrentRow;
            tbPepNovoResult.Clear();
 
            if(row != null) 
            {
                string spectrumSrc = tbSpecFile.Text;
                //string sequence = row.Cells["Sequence"].Value.ToString();
                string interpetation = row.Cells["Sequence"].Value.ToString();
                string nativeID = row.Cells["NativeID"].Value.ToString();
                if (!spectrumSrc.Equals(string.Empty) && currentPepGridSelection != row) 
                {
                    try 
                    {
                        // Resolve the spectrum source
                        //var source = Path.GetFileNameWithoutExtension(spectrumSrc);
                        //var paths = Properties.Settings.Default.SourcePath.Split(";".ToCharArray());
                        //var matches = FindFileInSearchPath(source, new string[] { "mzXML", "mzML", "mgf", "RAW" }, paths.ToArray(), true);
                        //if(matches.Length == 0)
                        //    MessageBox.Show("Can't find source. Set the source folder path.");
                        //else 
                        //{
                           // var interpetation = sequence;
                            
                            //interpetation = interpetation.Replace('(', '[');
                            //interpetation = interpetation.Replace(')', ']');

                            splitContainer4.Panel1.Hide();
                            splitContainer4.Panel2.Hide();
                            //splitContainer3.Panel2.Hide();
                            splitContainer5.Panel1.Hide();
                            //splitContainer5.Panel2.Hide();
                            
                        
                        Application.DoEvents();
                            //UniqueSpectrumID uniqueSpectrumID;  // for alternative interpretations
                            // Create a spectrum viewer and add its components
                            if(nativeID!=null && nativeID.Length>0) 
                            {
                                //currentSpectrumViewer = new SpectrumViewer(matches[0],nativeID,interpetation);
                                currentSpectrumViewer = new SpectrumViewer(spectrumSrc, nativeID, interpetation);
                                //uniqueSpectrumID = new UniqueSpectrumID(source, spectrum.nativeID, spectrum.id.charge);
                            }
                            //else
                            //{
                            //    //currentSpectrumViewer = new SpectrumViewer(matches[0], index, interpetation);
                            //    //uniqueSpectrumID = new UniqueSpectrumID(source, spectrum.id.index, spectrum.id.charge);
                            //}
                            splitContainer4.Panel1.Controls.Clear();
                            splitContainer4.Panel1.Controls.Add(currentSpectrumViewer.annotationPanel);
                            splitContainer4.Panel2.Controls.Clear();
                            splitContainer4.Panel2.Controls.Add(currentSpectrumViewer.fragmentationPanel);
                            //splitContainer3.Panel2.Controls.Clear();
                            //splitContainer3.Panel2.Controls.Add(currentSpectrumViewer.spectrumPanel);
                            //splitContainer3.Panel2.AutoScroll = true;
                            splitContainer5.Panel1.Controls.Clear();
                            splitContainer5.Panel1.Controls.Add(currentSpectrumViewer.spectrumPanel);
                            splitContainer5.Panel1.AutoScroll = true;
                            //splitContainer1.Panel2.Controls.Add(splitContainer5);
                            splitContainer4.Panel1.Show();
                            splitContainer4.Panel2.Show();
                            //splitContainer3.Panel2.Show();
                            splitContainer5.Panel1.Show();
                            splitContainer5.Panel2.Show();

                            Application.DoEvents();
                            // If we have a seconday result associated with this spectrum, 
                            // set the mechanism in spectrum viewer to see it.
                            //if(alternativeInterpretations != null)
                            //{
                            //    if (alternativeInterpretations.Contains(uniqueSpectrumID))
                            //    {
                            //        VariantInfo alt = alternativeInterpretations[uniqueSpectrumID];
                            //        interpetation = alt.ToSimpleString();
                            //        interpetation = interpetation.Replace('(', '[');
                            //        interpetation = interpetation.Replace(')', ']');
                            //        currentSpectrumViewer.setSecondarySequence(interpetation);
                            //    }
                            //}
                            currentPepGridSelection = row;
                               
                        //}
                    } catch(Exception exp) { 
                        MessageBox.Show(exp.StackTrace);
                        MessageBox.Show("Failed to show spectrum. Check raw data path.");
                    }
                }
            }
        }

        /// <summary>
        /// convert modification format in peptide sequence for seems compatibility
        /// e.g. convert "M1INER{1=16}" to "M[16]INER"
        /// Retrun a string of peptide sequence
        /// </summary>
        public string convertToSeeMSFormat(string rawSequence)
        {
            //rawSequence = "LKECCEKPLLEK{3=57.0215;4=57.0215}";
            //output seq = LKEC[+57.0215]C[+57.0215]EKPLLEK
            Dictionary<string, string> dict = new Dictionary<string, string>();
            if (rawSequence.Contains("{"))
            {
                string[] subPep = rawSequence.Split(new char[] { '{', '}' });
                //subPep[0] = LKECCEKPLLEK; subPep[1] = 3=57.0215;4=57.0215 
                string[] mods = subPep[1].Split(';');
                //mods[0] = 3=57.0215; mods[1] = 4=57.0215
                foreach (string s in mods)
                {
                    string[] mod = s.Split('=');
                    string modMass;
                    Match m = Regex.Match(mod[1], @"-(\d+)");
                    if (m.Success)
                    {
                        modMass = "-" + m.Groups[1].Value;
                    }
                    else
                    {
                        modMass = "+" + mod[1];
                    }
                    dict.Add(mod[0], '[' + modMass + ']');
                }

                //add mod position to seq, then replace to mods
                string seq = subPep[0];
                int extend = 0;
                foreach (string k in dict.Keys)
                {
                    int pos = Convert.ToInt32(k) + extend + 1;
                    seq = seq.Insert(pos, k);
                    extend++;
                }
                // seq = LKEC3C4EKPLLEK
                Regex r = new Regex(@"(\d+)");
                string result = r.Replace(seq, (Match m) => dict[m.Value]);
                
                return result;
            }
            else
            {
                return rawSequence;
            }
        }

        private DataTable getPeptideTable(FileInfo file)
        {
            string filename = file.FullName;
            string line = "";
            string[] fields = null;
            DataTable dt = new DataTable();
            //metrics file header:
            // old: H    Index    NativeID    "PrecursorMZ","Charge","PrecursorMass", BestTagScore    BestTagTIC    TagMzRange    ScanRankerScore    AdjustedScore    Label    CumsumLabel    Peptide    Protein(locus;peptide starting position)
            //H    NativeID    PrecursorMZ    Charge    PrecursorMass    BestTagScore    BestTagTIC    TagMzRange    ScanRankerScore AdjustedScore    Label    CumsumLabel    Peptide    Protein
            //List<string> selectedColumns = new List<string>(new string[] { "NativeID", "PrecursorMZ", "Charge", "PrecursorMass", "ScanRankerScore", "Peptide" });
            //dt.TableName = tableName;  
            try
            {
                using (StreamReader sr = new StreamReader(filename))
                {
                    List<int> columnIndices = new List<int>();  //store column index of "NativeID","PrecursorMZ","Charge","PrecursorMass","ScanRankerScore" and "Peptide"
                    sr.ReadLine();  // first three lines are header lines
                    sr.ReadLine();
                    // thrid line has headers  
                    if ((line = sr.ReadLine()) != null)
                    {
                        fields = line.Split('\t');                                            
                        int i =0;                        
                        foreach (string s in fields)
                        {                            
                            //if (selectedColumns.Exists(element => element.Equals(s)))
                            //{
                            //    dt.Columns.Add(s);
                            //}
                            if (s.Equals("NativeID"))
                            {                                
                                columnIndices.Add(i);
                            }
                            else if (s.Equals("PrecursorMZ"))
                            {
                                columnIndices.Add(i);
                            }
                            else if (s.Equals("Charge"))
                            {
                                columnIndices.Add(i);
                            }
                            else if (s.Equals("PrecursorMass"))
                            {
                                columnIndices.Add(i);
                            }
                            else if (s.Equals("ScanRankerScore"))
                            {                                
                                columnIndices.Add(i);
                            }
                            else if (s.Equals("Peptide"))
                            {                                
                                columnIndices.Add(i);
                            }
                            i++;
                        }
                        //add  columns in datatable
                        dt.Columns.Add("NativeID", typeof(string));
                        dt.Columns.Add("PrecursorMZ", typeof(float));
                        dt.Columns.Add("Charge", typeof(int));
                        dt.Columns.Add("PrecursorMass", typeof(float));
                        dt.Columns.Add("SRscore", typeof(float));
                        dt.Columns.Add("Sequence", typeof(string));
                    }
                    else
                    {
                        // it's empty, that's an error  
                        throw new ApplicationException("The data provided is not in a valid format.");
                    }
                                       
                    //int[] selectedColumns = columnIndices.ToArray();
                    // sr.ReadLine();
                    //dt.Columns.Add("NativeID");
                    //dt.Columns.Add("SRscore");
                    //dt.Columns.Add("Sequence");   
                 
                    // fill the rest of the table; positional  
                    while ((line = sr.ReadLine()) != null)
                    {
                        DataRow row = dt.NewRow();
                        fields = line.Split('\t');
                        row[0] = fields[columnIndices[0]];  //NativeID
                        row[1] = Convert.ToSingle(fields[columnIndices[1]]); //PrecursorMZ
                        row[2] = Convert.ToInt16(fields[columnIndices[2]]); //Charge
                        row[3] = Convert.ToSingle(fields[columnIndices[3]]); //PrecursorMass
                        row[4] = Convert.ToSingle(fields[columnIndices[4]]); //ScanRankerScore
                        //MessageBox.Show(columnIndices[0].ToString() + " " + columnIndices[1].ToString() + " " +columnIndices[2].ToString() 
                         //   + " " +row[0] + " " + row[1] + "field Length: " + fields.Length.ToString());
                        if (columnIndices.Count == 6)  // exist peptide column
                        {
                            row[5] = (columnIndices[5] <= fields.Length - 1) ? convertToSeeMSFormat((fields[columnIndices[5]].Split(','))[0]) : ""; //Peptide, if more than one, only select the first one
                        }
                        else
                        {
                            row[5] = "";
                        }

                        //int i = 0;
                        //foreach (string s in fields)
                        //{
                        //    row[i] = s;
                        //    i++;
                        //}                       
                        
                        dt.Rows.Add(row);
                    }
                }
            }
            catch (Exception exe)
            {
                throw new Exception(exe.Message);
            }
            return dt;
        }

        private void updatePepGrid()
        {
            if (!tbMetricsFile.Text.Equals(string.Empty))
            {
                //display peptide table in pepGrid
                // Set the grid invisible while it's being prepped.
                pepGridView.DataSource = null;  // need to set datasource to null to clear grid view
                pepGridView.Visible = false;
                pepGridView.Rows.Clear();
                pepGridView.Columns.Clear();
                currentPepGridSelection = null;
                splitContainer4.Panel1.Controls.Clear();
                splitContainer4.Panel2.Controls.Clear();
                //splitContainer3.Panel2.Controls.Clear();
                splitContainer5.Panel1.Controls.Clear();
                //splitContainer5.Panel2.Controls.Clear();
                
                this.Refresh();

                //read peptide table to a data table and bind to pepGridView
                FileInfo file = new FileInfo(tbMetricsFile.Text);
                DataTable peptideTable = getPeptideTable(file);
                

                pepGridView.DataSource = peptideTable;  //bind all data to grid view

                // // Add columns
                // pepGridView.Columns.Add("Sequence", "Sequence");
                // pepGridView.Columns["Sequence"].ValueType = typeof(string);
                // pepGridView.Columns.Add("Source", "Source");
                // pepGridView.Columns["Source"].ValueType = typeof(string);
                // pepGridView.Columns.Add("NativeID", "NativeID");
                // pepGridView.Columns["NativeID"].ValueType = typeof(string);

                // Application.DoEvents();
                // // Add rows
                // foreach(Peptide pep in peptideList)
                // {
                //     DataGridViewRow row = new DataGridViewRow();
                //     int index = pepGridView.Rows.Add(row);
                //     pepGridView.Rows[index].Cells["Sequence"].Value = pep.sequence;
                //     pepGridView.Rows[index].Cells["Source"].Value = pep.source;
                //     pepGridView.Rows[index].Cells["NativeID"].Value = pep.nativeID;
                //      // ResultInstance and VariantInfo objects associated with this row.
                //      //  spectraGrid.Rows[index].Tag = new Object[] { spectrum, var };

                // }

                  // Format the columns and cells
                pepGridView.Dock = DockStyle.Fill;
                pepGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                pepGridView.RowHeadersVisible = false;
                pepGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                pepGridView.MultiSelect = false;
                pepGridView.Columns["Sequence"].Width = 200;
                pepGridView.Columns["SRscore"].Width = pepGridView.Columns["SRscore"].Width - 10;
                //pepGridView.Columns["NativeID"].Width = pepGridView.Columns["NativeID"].Width + 40;                
                pepGridView.Columns["NativeID"].ValueType = typeof(string);
                pepGridView.Columns["PrecursorMZ"].ValueType = typeof(float);
                pepGridView.Columns["Charge"].ValueType = typeof(int);
                pepGridView.Columns["PrecursorMass"].ValueType = typeof(float);
                pepGridView.Columns["SRscore"].ValueType = typeof(float);
                pepGridView.Columns["Sequence"].ValueType = typeof(string);                
                pepGridView.Columns["NativeID"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                pepGridView.Columns["SRscore"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                pepGridView.Columns["Sequence"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                pepGridView.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;                    
                Font newFont = new Font(pepGridView.Font, FontStyle.Bold);
                pepGridView.ReadOnly = true;
                pepGridView.ColumnHeadersDefaultCellStyle.Font = newFont;
                pepGridView.AllowUserToResizeColumns = true;
                pepGridView.AllowUserToOrderColumns = false; 

                // Set the event handler for spectrum viewer
                //pepGridView.DoubleClick += new EventHandler(triggerSpectrumViewer);
                pepGridView.Click += new EventHandler(triggerSpectrumViewer);
                pepGridView.Visible = true;
                Application.DoEvents();
              }
            }

        /// <summary>
        /// This function exports fragmentation and spectrum panels for all peptides
        /// </summary>
        //public void exportInterpretation()
        //{
        //    int fragmentationPanelWidth = 400;
        //    int fragmentationPanelHeight = 500;
        //    Panel fragmentationPanel = new Panel();
        //    //fragmentationPanel.Location = new Point(50, 70);
        //    fragmentationPanel.Size = new Size(fragmentationPanelWidth, fragmentationPanelHeight);
           
        //    int spectrumPanelWidth = 800;
        //    int spectrumPanelHeight = 600;
        //    Panel spectrumPanel = new Panel();
        //    //spectrumPanel.Location = new Point(100, 140);
        //    spectrumPanel.Size = new Size(spectrumPanelWidth, spectrumPanelHeight);

        //    int numPepTableRow = pepGridView.Rows.Count - 1;
        //    GhettoProgressControl progress = new GhettoProgressControl(numPepTableRow);

        //    foreach (DataGridViewRow row in pepGridView.Rows)
        //    {
        //        if (row != null && row.Cells["Source"].Value != null)
        //        {
        //            string spectrumSrc = row.Cells["Source"].Value.ToString();
        //            string sequence = row.Cells["Sequence"].Value.ToString();
        //            string nativeID = row.Cells["NativeID"].Value.ToString();
                    
        //            string outFileName = tbOutFilePrefix.Text + "_" + sequence + "_" + spectrumSrc + "_" + nativeID;
                    
        //            progress.showProgress("Exporting peptide " + sequence);
        //            //progress.updateMax(progress.getMax());

        //            if (!spectrumSrc.Equals(string.Empty))
        //            {
        //                try
        //                {
        //                    // Resolve the spectrum source
        //                    var source = Path.GetFileNameWithoutExtension(spectrumSrc);
        //                    var paths = Properties.Settings.Default.SourcePath.Split(";".ToCharArray());
        //                    var matches = FindFileInSearchPath(source, new string[] { "mzXML", "mzML", "mgf", "RAW" }, paths.ToArray(), true);
        //                    if (matches.Length == 0)
        //                        MessageBox.Show("Can't find source. Set the source folder path.");
        //                    else
        //                    {
        //                        // Convert the interpretation for seems compatibility
        //                        var interpetation = sequence;
        //                        interpetation = interpetation.Replace('(', '[');
        //                        interpetation = interpetation.Replace(')', ']');

        //                        //UniqueSpectrumID uniqueSpectrumID;  // for alternative interpretations
        //                        // Create a spectrum viewer and add its components
        //                        if (nativeID != null && nativeID.Length > 0)
        //                        {
        //                            currentSpectrumViewer = new SpectrumViewer(matches[0], nativeID, interpetation);
        //                            //uniqueSpectrumID = new UniqueSpectrumID(source, spectrum.nativeID, spectrum.id.charge);
        //                        }
        //                        //else
        //                        //{
        //                        //    //currentSpectrumViewer = new SpectrumViewer(matches[0], index, interpetation);
        //                        //    //uniqueSpectrumID = new UniqueSpectrumID(source, spectrum.id.index, spectrum.id.charge);
        //                        //}

        //                        // If we have a seconday result associated with this spectrum, 
        //                        // set the mechanism in spectrum viewer to see it.
        //                        //if(alternativeInterpretations != null)
        //                        //{
        //                        //    if (alternativeInterpretations.Contains(uniqueSpectrumID))
        //                        //    {
        //                        //        VariantInfo alt = alternativeInterpretations[uniqueSpectrumID];
        //                        //        interpetation = alt.ToSimpleString();
        //                        //        interpetation = interpetation.Replace('(', '[');
        //                        //        interpetation = interpetation.Replace(')', ']');
        //                        //        currentSpectrumViewer.setSecondarySequence(interpetation);
        //                        //    }
        //                        //}
        //                        //currentPepGridSelection = row;

        //                        // save panle content to bitmap
        //                        fragmentationPanel.Controls.Clear();
        //                        fragmentationPanel.Controls.Add(currentSpectrumViewer.fragmentationPanel);
                                
        //                        spectrumPanel.Controls.Clear();
        //                        spectrumPanel.Controls.Add(currentSpectrumViewer.spectrumPanel);

        //                        Directory.SetCurrentDirectory(tbOutDir.Text);

        //                        Bitmap spectrumBmp = new Bitmap(spectrumPanelWidth, spectrumPanelHeight);
        //                        spectrumPanel.DrawToBitmap(spectrumBmp, new Rectangle(0, 0, spectrumPanelWidth, spectrumPanelHeight));

        //                        Bitmap fragmentBmp = new Bitmap(fragmentationPanelWidth, fragmentationPanelHeight);
        //                        fragmentationPanel.DrawToBitmap(fragmentBmp, new Rectangle(0, 0, fragmentationPanelWidth, fragmentationPanelHeight));

        //                        ImageCodecInfo codec = null;
        //                        foreach (ImageCodecInfo cCodec in ImageCodecInfo.GetImageEncoders())
        //                        {
        //                            if (cCodec.MimeType == "image/tiff")
        //                                codec = cCodec;
        //                        }
        //                        EncoderParameters parameters = new EncoderParameters(1);
        //                        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.MultiFrame);
        //                        fragmentBmp.Save(outFileName + ".tiff", codec, parameters);
        //                        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.FrameDimensionPage);
        //                        fragmentBmp.SaveAdd(spectrumBmp, parameters);
        //                        spectrumBmp.Dispose();
        //                        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.Flush);
        //                        fragmentBmp.SaveAdd(parameters);
        //                        fragmentBmp.Dispose();
                                
        //                    }
        //                }
        //                catch (Exception exp)
        //                {
        //                    MessageBox.Show(exp.StackTrace);
        //                    MessageBox.Show("Failed to show spectrum. Check raw data path.");
        //                }
        //            }
        //        }
        //    }

        //    progress.form.Close();
        //}



         private void btnRun_Click(object sender, EventArgs e)
         {             
             if (tbMetricsFile.Text.Equals(string.Empty) || tbSpecFile.Text.Equals(string.Empty))
             {
                 MessageBox.Show("Please select spectrum file and metrics file!");
                 return;
             }
             else
             {
                 updatePepGrid();
             }

         }

         private void MainForm_Load(object sender, EventArgs e)
         {
             splitContainer5.Panel2.Hide();
         }

         public static void SetText(MainForm mainForm, string text)
         {
             mainForm.tbPepNovoResult.AppendText(text);
         }

        //private class SpectrumList_FilterPredicate_IndexSet
        //{
        //    public List<string> indexSet;
        //    public bool? accept(Spectrum s) { return indexSet.Contains(s.id); }
        //    public SpectrumList_FilterPredicate_IndexSet()
        //    {
        //        indexSet = new List<string>();
        //    }

        //}

        private class SpectrumList_FilterPredicate_NativeIDSet
        {
            public List<string> nativeIDSet;
            public bool? accept(Spectrum s)
            {
                if (String.IsNullOrEmpty(s.id)) return null;
                return nativeIDSet.Contains(s.id);
            }
            public SpectrumList_FilterPredicate_NativeIDSet()
            {
                nativeIDSet = new List<string>();
            }

        }

        private void writeSingleSpectrum(string sourceFile, string nativeID, string outFileName)
        {
            MSDataFile.WriteConfig writeConfig = new MSDataFile.WriteConfig();
            writeConfig.format = MSDataFile.Format.Format_MGF;
            writeConfig.precision = MSDataFile.Precision.Precision_32;

            //var predicate = new SpectrumList_FilterPredicate_IndexSet();
            //predicate.indexSet.Add(nativeID);

            var predicate = new SpectrumList_FilterPredicate_NativeIDSet();
            predicate.nativeIDSet.Add(nativeID);
            

            try
            {
                //Workspace.SetText("\r\nWriting selected spectrum to " + outFileName);
                MainForm.SetText(this, "Start writing selected spectrum to " + outFileName + "\r\n");
                using (MSDataFile msFile = new MSDataFile(sourceFile))
                {
                    msFile.run.spectrumList = new SpectrumList_Filter(msFile.run.spectrumList, new SpectrumList_FilterAcceptSpectrum(predicate.accept));
                    msFile.write(outFileName, writeConfig);
                }
                MainForm.SetText(this, "Finished writing selected spectrum to " + outFileName + "\r\n");
            }
            catch (Exception exc)
            {
                //throw new Exception("Error in writiing new spectra file", exc);
                //Workspace.SetText("\r\nError in writing new spectra file\r\n");
                tbPepNovoResult.AppendText("\r\nError in writing new spectra file\r\n");
                throw new Exception(exc.Message);
            }
        }

         private void btnRunPepNovo_Click(object sender, EventArgs e)
         {
             # region  Error checking
                string EXE = @"PepNovo.exe";
                string BIN_DIR = Path.GetDirectoryName(Application.ExecutablePath) + "\\" + "PepNovo";
                string pathAndExeFile = BIN_DIR + "\\" + EXE;   
                if (!File.Exists(pathAndExeFile))
                {
                    MessageBox.Show("Error: Cannot find PepNovo program! Please download PepNovo at http://proteomics.ucsd.edu/ and copy it to "
                        + BIN_DIR + " folder");
                    return;
                }
                if (tbPrecursorTolerance.Text.Equals(string.Empty))
                {
                    MessageBox.Show("Error: Please specify precursor tolerance!");
                    return;
                }
                if (tbFragmentTolerance.Text.Equals(string.Empty))
                {
                    MessageBox.Show("Error: Please specify fragment tolerance!");
                    return;
                }
                if (pepGridView.SelectedRows.Count != 1)
                {
                    MessageBox.Show("Error: Please select a single spectrum!");
                    return;
                }
             #endregion

             //write out a temp spectrum file for selected spectrum in data table
             tbPepNovoResult.Clear();
             string workingDir = Path.GetDirectoryName(tbMetricsFile.Text);
             Directory.SetCurrentDirectory(workingDir);

             string sourceFile = tbSpecFile.Text;
             string nativeID = Convert.ToString(pepGridView.SelectedRows[0].Cells["NativeID"].Value);
             string singleSpectrumFileName = "tempSpectrumForPepNovo.mgf";
             if (File.Exists(singleSpectrumFileName))
             {
                 File.Delete(singleSpectrumFileName);
             }
             writeSingleSpectrum(sourceFile, nativeID, singleSpectrumFileName);

             RunPepNovoAction runPepNovoAction = new RunPepNovoAction(this);
             runPepNovoAction.InFile = singleSpectrumFileName; 
             runPepNovoAction.PrecursorTolerance = Convert.ToSingle(tbPrecursorTolerance.Text);
             runPepNovoAction.FragmentTolerance = Convert.ToSingle(tbFragmentTolerance.Text);
             runPepNovoAction.PTMs = tbPTMs.Text;
             runPepNovoAction.UseSpectrumCharge = cbUseSpectrumCharge.Checked ? " -use_spectrum_charge " : " ";
             runPepNovoAction.UseSpectrumMz = cbUseSpectrumMZ.Checked ? " -use_spectrum_mz " : " ";
             runPepNovoAction.WorkingDir = workingDir;
             //runPepNovoAction.RunPepNovo();
             //tbPepNovoResult.AppendText("Start bgRunPepNovo");

             bgRunPepNovo.WorkerSupportsCancellation = true;
             bgRunPepNovo.RunWorkerCompleted += bgRunPepNovo_RunWorkerCompleted;
             bgRunPepNovo.RunWorkerAsync(runPepNovoAction);

         }//btnRunPepNovo_Click


         //private void btnExport_Click(object sender, EventArgs e)
         //{
         //    exportInterpretation();
         //}  

         private void bgRunPepNovo_DoWork(object sender, DoWorkEventArgs e)
         {
             BackgroundWorker bw = sender as BackgroundWorker;
             RunPepNovoAction arg = e.Argument as RunPepNovoAction;
             while (!bw.CancellationPending)
             {
                 arg.RunPepNovo();
                 return;
             }
         }

         private void bgRunPepNovo_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
         {
             if (e.Error != null)
             {
                 string msg = String.Format("An error occurred: {0}", e.Error.Message);
                 MessageBox.Show(msg);
             }
         }




    }//MainForm
}
