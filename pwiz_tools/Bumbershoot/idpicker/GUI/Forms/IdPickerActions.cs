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
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasaris
//

using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.IO;
using System.Xml;
using System.Diagnostics;
using System.ComponentModel;
using System.Security.Principal;
using System.Windows.Forms;

using IdPickerGui.MODEL;
using IDPicker;

namespace IdPickerGui
{
    public class IdPickerActions
    {
        private const string EXE_FILE_1 = @"idpQonvert.exe";
        private const string INPUT_LIST_NAME = @"_input_files.txt";
		private const string ERROR_LOG_NAME = @"_error_log.txt";
        private string BIN_DIR = @"c:\";
        private const string SCRATCH_DIR = @"c:\scratch";

        private const string LINE_BREAK = "---------------";

        private IDPickerInfo idPickerRequest;
        public IDPickerInfo IdPickerRequest
        {
            get { return idPickerRequest; }
            set { idPickerRequest = value; }
        }

        private ArrayList resourceFiles;
        public ArrayList ResourceFiles
        {
            get { return resourceFiles; }
            set { resourceFiles = value; }
        }

        public IdPickerActions()
        {
            ResourceFiles = new ArrayList();
            IdPickerRequest = new IDPickerInfo();
            BIN_DIR = Path.GetDirectoryName( Application.ExecutablePath );
        }

        /// <summary>
        /// Get database from search_database tag in the pepxml file.
        /// </summary>
        /// <param name="path">Path to source pepxml file.</param>
        /// <returns></returns>
        public static string getDatabaseFromPepxmlFile(string path)
        {
            string dbPath = string.Empty;

            try
            {


                using (XmlTextReader reader = new XmlTextReader(new StreamReader(path, true)))
                {

                    while (reader.Read())
                    {
                        if (reader.Name.Equals("search_database"))
                        {
                            reader.MoveToAttribute("local_path");
                            dbPath = reader.Value;
                            break;
                        }
                    }

                }
            }
            catch (Exception)
            {
                return string.Empty;
            }

            dbPath = dbPath.Replace( ".pro", "" ); // masquerade X! Tandem databases as FASTA
            return Path.GetFileName(dbPath);
        }

        /// <summary>
        /// Handler for output from process.
        /// </summary>
        /// <param name="sendingProcess"></param>
        /// <param name="outLine"></param>
        private void StdOutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            if( IdPickerRequest != null && outLine.Data != null )
            {
                if( !outLine.Data.Contains( "...parsed" ) )
                    IdPickerRequest.StdOutput.AppendLine( outLine.Data );
            }
        }

        /// <summary>
        /// Handler for errors from process.
        /// </summary>
        /// <param name="sendingProcess"></param>
        /// <param name="outLine"></param>
        private void StdErrorHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            if( IdPickerRequest != null && outLine.Data != null )
            {
                if( !outLine.Data.Contains( "Process #0" ) )
                    IdPickerRequest.StdError.AppendLine( outLine.Data );
            }
        }
     
        /// <summary>
        /// Create idpicker input file with list of source files (pepxmls).
        /// </summary>
        /// <param name="path">Path to create file with list of input files.</param>
        private bool createSourceListFile(string path)
        {
            StringBuilder sbOut = new StringBuilder();

            ResourceFiles.Add(path);

            bool hasPepXmlFiles = false;
            foreach (InputFileTag tag in IdPickerRequest.SrcPathToTagCollection)
            {
                // allowing empty group names for non grouped files
                // used to rebuild cloned request properly
                if (tag.GroupName != string.Empty && tag.FileType == InputFileType.PepXML)
                {
                    hasPepXmlFiles = true;
                    sbOut.AppendLine(tag.FullPath);
                }
            }

            using (StreamWriter sw = new StreamWriter(path))
            {
                sw.WriteLine(sbOut.ToString());
            }
            return hasPepXmlFiles;
        }

        /// <summary>
        /// Currently unused.
        /// </summary>
        private void removeResourceFiles()
        {
            try
            {

                foreach (string sFile in ResourceFiles)
                {
                    if (File.Exists(sFile))
                    {
                        File.Delete(sFile);
                    }
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

        }

        /// <summary>
        /// Run convert step of idpicker.
        /// </summary>
        /// <param name="inputFilePath"></param>
        private void runConvert(string inputFilePath)
        {
            Process RunProc = new Process();
            Form ghettoProgressForm = new Form();

			string scoreWeightString = "";
			foreach( ScoreInfo score in IdPickerRequest.ScoreWeights )
				scoreWeightString += " " + score.ScoreName + " " + score.ScoreWeight;

			string args = "-MaxFDR " + IdPickerRequest.MaxFDR;
			args += " -ProteinDatabase \"" + IdPickerRequest.DatabasePath + "\"";
			args += " -DecoyPrefix " + IdPickerRequest.DecoyPrefix;
			args += " -SearchScoreWeights \"" + scoreWeightString.Trim() + "\"";
			args += " -OptimizeScoreWeights " + (IdPickerRequest.OptimizeScoreWeights ? "1" : "0");
			args += " -OptimizeScorePermutations " + IdPickerRequest.OptimizeScorePermutations;
			args += " -NormalizeSearchScores " + (IdPickerRequest.NormalizeSearchScores ? "1" : "0");
			args += " -WriteQonversionDetails 1";
			args += " -PreserveInputHierarchy 1";
            args += " -HasDecoyDatabase false";
			args += " -b \"" + inputFilePath + "\"";

			string qonvertedFilesRoot = Path.Combine( IdPickerRequest.ResultsDir, "qonverted" );
			if( !Directory.Exists( qonvertedFilesRoot ) )
				Directory.CreateDirectory( qonvertedFilesRoot );

			RunProc.EnableRaisingEvents = true;

			RunProc.StartInfo.CreateNoWindow = true;
			RunProc.StartInfo.WorkingDirectory = qonvertedFilesRoot;
			RunProc.StartInfo.UseShellExecute = false;
			RunProc.StartInfo.RedirectStandardError = true;
			RunProc.StartInfo.RedirectStandardOutput = true;
			RunProc.StartInfo.Arguments = args;
			RunProc.StartInfo.FileName = BIN_DIR + "\\" + EXE_FILE_1;

			RunProc.OutputDataReceived += new DataReceivedEventHandler( StdOutputHandler );
			RunProc.ErrorDataReceived += new DataReceivedEventHandler( StdErrorHandler );

			IdPickerRequest.QonvertCommandLine = RunProc.StartInfo.FileName + " " + args + "\n";

			RunProc.Start();

			RunProc.BeginOutputReadLine();
			RunProc.BeginErrorReadLine();

			try
			{
				ghettoProgressForm = new Form();
				ghettoProgressForm.SizeGripStyle = SizeGripStyle.Show;
				ghettoProgressForm.ShowInTaskbar = true;
				ghettoProgressForm.TopLevel = true;
				ghettoProgressForm.TopMost = true;
				ghettoProgressForm.AutoSize = true;
				ghettoProgressForm.AutoSizeMode = AutoSizeMode.GrowOnly;
				ghettoProgressForm.FormBorderStyle = FormBorderStyle.SizableToolWindow;
				ghettoProgressForm.StartPosition = FormStartPosition.CenterScreen;
				ghettoProgressForm.MaximizeBox = false;
				ghettoProgressForm.MinimizeBox = true;
				ghettoProgressForm.Text = "Calculating Q values...";
				ghettoProgressForm.Size = new System.Drawing.Size( 300, 50 );

				// can't close form on exception because new exception thrown

				ghettoProgressForm.FormClosing += new FormClosingEventHandler
					(
						delegate( object sender, FormClosingEventArgs e )
						{
							if( !RunProc.HasExited )
							{
								IdPickerRequest.RunStatus = RunStatus.Cancelled;
								RunProc.Kill();
							}
						}
					);



				ProgressBar ghettoProgressBar = new ProgressBar();
				ghettoProgressBar.Dock = DockStyle.Fill;
				ghettoProgressBar.Style = ProgressBarStyle.Continuous;
				ghettoProgressBar.Step = 1;
				ghettoProgressBar.Minimum = 0;
				ghettoProgressBar.Maximum = IdPickerRequest.NumGroupedFiles;
				ghettoProgressForm.Controls.Add( ghettoProgressBar );
				ghettoProgressForm.Show();

				while( !RunProc.HasExited )
				{
					RunProc.WaitForExit( 500 );

					string[] filepaths = Directory.GetFiles( qonvertedFilesRoot, "*.idpXML", SearchOption.AllDirectories );

					List<string> sourceList = new List<string>();
					foreach( string filepath in filepaths )
						sourceList.Add( filepath.Substring( qonvertedFilesRoot.Length ).TrimStart("/\\".ToCharArray() ) );

					int filesQonverted = 0;
                    foreach (InputFileTag tag in IdPickerRequest.SrcPathToTagCollection)
                    {
                        if (tag.GroupName != string.Empty && sourceList.Contains(Path.ChangeExtension(tag.FullPath, ".idpXML").Substring(3).TrimStart("/\\".ToCharArray())))
                            ++filesQonverted;
                    }

					ghettoProgressBar.Value = filesQonverted;
					ghettoProgressForm.Text = "Calculating Q values... (" + filesQonverted + " of " + IdPickerRequest.NumGroupedFiles + ")";
					Application.DoEvents();
				}

                if( RunProc.ExitCode != 0 && IdPickerRequest.RunStatus == RunStatus.InProgress )
				{
					IdPickerRequest.RunStatus = RunStatus.Error;

					throw new Exception( QonvertErrorInfo.GetErrorStringForCode( RunProc.ExitCode ) );
				}

			}
			finally
            {
				ghettoProgressForm.Close();
				Application.DoEvents();
            }
        }

		/// <summary>
		/// Read qonverted files into a workspace, run filters, and create a report.
		/// </summary>
		private void runReport()
		{
			Form ghettoProgressForm = new Form();

            try
            {
                ghettoProgressForm.SizeGripStyle = SizeGripStyle.Show;
                ghettoProgressForm.ShowInTaskbar = true;
                ghettoProgressForm.TopLevel = true;
                ghettoProgressForm.TopMost = true;
                ghettoProgressForm.AutoSize = true;
                ghettoProgressForm.AutoSizeMode = AutoSizeMode.GrowOnly;
                ghettoProgressForm.FormBorderStyle = FormBorderStyle.SizableToolWindow;
                ghettoProgressForm.StartPosition = FormStartPosition.CenterScreen;
                ghettoProgressForm.MaximizeBox = false;
                ghettoProgressForm.MinimizeBox = true;
                ghettoProgressForm.Text = "Generating report...";
                ghettoProgressForm.Size = new System.Drawing.Size(450, 50);

                ProgressBar ghettoProgressBar = new ProgressBar();
                ghettoProgressBar.Dock = DockStyle.Fill;
                ghettoProgressBar.Style = ProgressBarStyle.Continuous;
                ghettoProgressBar.Step = 1;
                ghettoProgressBar.Minimum = 0;
                ghettoProgressBar.Maximum = 20 + (IdPickerRequest.MinAdditionalPeptides > 0 ? 1 : 0);
                ghettoProgressForm.Controls.Add(ghettoProgressBar);
                ghettoProgressForm.Show();

                Application.DoEvents();

                Workspace ws = new Workspace();
                //ws.setStatusOutput( new StringWriter( IdPickerRequest.StdOutput ) );

                StringBuilder indistinctModsOverride = new StringBuilder();
                StringBuilder distinctModsOverride = new StringBuilder();
                foreach (ModOverrideInfo mod in IdPickerRequest.ModOverrides)
                    if (mod.Type.ModTypeValue == 0)
                        distinctModsOverride.Append(" " + mod.Name + " " + mod.Mass);
                    else
                        indistinctModsOverride.Append(" " + mod.Name + " " + mod.Mass);

                ws.distinctPeptideSettings = new Workspace.DistinctPeptideSettings(
                    IdPickerRequest.ModsAreDistinctByDefault, distinctModsOverride.ToString(), indistinctModsOverride.ToString());

                ghettoProgressForm.Text = "Reading files...";
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return;
                }

                int readFiles = 0;

				foreach (InputFileTag tag in IdPickerRequest.SrcPathToTagCollection)
                {
                    if (tag.GroupName != string.Empty)
                    {
                        string path;
                        if (tag.FileType == InputFileType.PepXML)
                            path = IdPickerRequest.ResultsDir + "\\qonverted\\" + Path.ChangeExtension(tag.FullPath, ".idpXML").Substring(3);
                        else
                            path = tag.FullPath;

                        string group = tag.GroupName;

                        using (StreamReader sr = new StreamReader(path))
                        {
                            ws.readPeptidesXml(sr, group, IdPickerRequest.MaxFDR, IdPickerRequest.MaxResultRank);
                            foreach (SourceInfo source in ws.groups[group].getSources())
                                if( source.name == Path.GetFileNameWithoutExtension(path) )
                                    source.filepath = path;
                            ++readFiles;
                            ghettoProgressForm.Text = "Reading files... (" + readFiles + " of " + IdPickerRequest.NumGroupedFiles + ")";
                            Application.DoEvents();
                        }
                    }

                }

                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return;
                }

                ProcessingEvent presentationEvent = new ProcessingEvent();
                presentationEvent.type = "presentation/filtration";
                presentationEvent.startTime = DateTime.Now;
                ProcessingParam presentationParam = new ProcessingParam();
                presentationParam.name = "software name";
                presentationParam.value = "IDPicker";
                presentationEvent.parameters.Add(presentationParam);
                presentationParam = new ProcessingParam();
                presentationParam.name = "software version";
                var assembly = Util.GetAssemblyByName( "Presentation" );
                presentationParam.value = Util.GetAssemblyVersion( assembly ) + " (" + Util.GetAssemblyLastModified( assembly ).ToShortDateString() + ")";
                presentationParam = new ProcessingParam();
                presentationParam.name = "MaxFDR";
                presentationParam.value = IdPickerRequest.MaxFDR.ToString("f2");
                presentationEvent.parameters.Add(presentationParam);
                presentationParam = new ProcessingParam();
                presentationParam.name = "MinPeptideLength";
                presentationParam.value = IdPickerRequest.MinPeptideLength.ToString();
                presentationEvent.parameters.Add(presentationParam);
                presentationParam = new ProcessingParam();
                presentationParam.name = "MinDistinctPeptides";
                presentationParam.value = IdPickerRequest.MinDistinctPeptides.ToString();
                presentationEvent.parameters.Add(presentationParam);
                presentationParam.name = "MinSpectraPerProtein";
                presentationParam.value = idPickerRequest.MinSpectraPerProetin.ToString();
                presentationEvent.parameters.Add( presentationParam );
                presentationParam = new ProcessingParam();
                presentationParam.name = "MaxAmbiguousIds";
                presentationParam.value = IdPickerRequest.MaxAmbiguousIds.ToString();
                presentationEvent.parameters.Add(presentationParam);
                presentationParam = new ProcessingParam();
                presentationParam.name = "MinAdditionalPeptides";
                presentationParam.value = IdPickerRequest.MinAdditionalPeptides.ToString();
                presentationEvent.parameters.Add(presentationParam);
                presentationParam = new ProcessingParam();
                presentationParam.name = "ModsAreDistinctByDefault";
                presentationParam.value = IdPickerRequest.ModsAreDistinctByDefault.ToString();
                presentationEvent.parameters.Add(presentationParam);
                presentationParam = new ProcessingParam();
                presentationParam.name = "AllowSharedSourceNames";
                presentationParam.value = IdPickerRequest.AllowSharedSourceNames.ToString();
                presentationEvent.parameters.Add(presentationParam);
                presentationParam = new ProcessingParam();
                presentationParam.name = "GenerateBipartiteGraphs";
                presentationParam.value = IdPickerRequest.GenerateBipartiteGraphs.ToString();
                presentationEvent.parameters.Add(presentationParam);

                ghettoProgressForm.Text = "Filtering out peptides shorter than " + IdPickerRequest.MinPeptideLength + " residues...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return;
                }

                ws.filterByMinimumPeptideLength(IdPickerRequest.MinPeptideLength);

                ghettoProgressForm.Text = "Filtering out results with more than " + IdPickerRequest.MaxAmbiguousIds + " ambiguous ids...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return;
                }

                ws.filterByResultAmbiguity(IdPickerRequest.MaxAmbiguousIds);

                ghettoProgressForm.Text = "Filtering out proteins with less than " + IdPickerRequest.MinDistinctPeptides + " distinct peptides...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return;
                }

                ws.filterByDistinctPeptides(IdPickerRequest.MinDistinctPeptides);

                ghettoProgressForm.Text = "Filtering out proteins with less than " + IdPickerRequest.MinSpectraPerProetin + " spectra...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if(ghettoProgressForm.IsDisposed) 
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return;
                }

                ws.filterBySpectralCount( IdPickerRequest.MinSpectraPerProetin );

                ghettoProgressForm.Text = "Assembling protein groups...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return;
                }

                ws.assembleProteinGroups();

                ghettoProgressForm.Text = "Assembling peptide groups...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return;
                }

                ws.assemblePeptideGroups();

                ghettoProgressForm.Text = "Assembling clusters...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return;
                }

                ws.assembleClusters();

                int clusterCount = 0;
                foreach (ClusterInfo c in ws.clusters)
                {
                    ++clusterCount;
                    ghettoProgressForm.Text = "Assembling minimum covering set for cluster " + clusterCount + " of " + ws.clusters.Count + "...";
                    Application.DoEvents();
                    if (ghettoProgressForm.IsDisposed)
                    {
                        IdPickerRequest.RunStatus = RunStatus.Cancelled;
                        return;
                    }

                    ws.assembleMinimumCoveringSet(c);
                }
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return;
                }

                if (IdPickerRequest.MinAdditionalPeptides > 0)
                {
                    ghettoProgressForm.Text = "Filtering workspace by minimum covering set...";
                    ghettoProgressBar.PerformStep();
                    Application.DoEvents();
                    if (ghettoProgressForm.IsDisposed)
                    {
                        IdPickerRequest.RunStatus = RunStatus.Cancelled;
                        return;
                    }

                    ws.filterByMinimumCoveringSet(IdPickerRequest.MinAdditionalPeptides);
                }

                ws.assembleSourceGroups();

                ghettoProgressForm.Text = "Verifying integrity of the workspace...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return;
                }

                try
                {
                    ws.validate(IdPickerRequest.MaxFDR, IdPickerRequest.MinDistinctPeptides, IdPickerRequest.MaxAmbiguousIds);
                }
                catch (Exception e)
                {
                    ghettoProgressForm.Close();
                    IdPickerRequest.StdError.AppendLine( e.Message );
                    IdPickerRequest.RunStatus = RunStatus.Error;
                    return;
                }

                ghettoProgressForm.Text = "Writing output files...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return;
                }

                string outputPrefix = IdPickerRequest.ReportName;
                string outputDir = IdPickerRequest.ResultsDir;
                if (!Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);
                string lastCurrentDirectory = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(outputDir);

                StreamWriter outputStream;

                string reportIndexFilename = "index.html";
                string idpickerJavascriptFilename = "idpicker-scripts.js";
                string idpickerStylesheetFilename = "idpicker-style.css";
                string navFrameFilename = outputPrefix + "-nav.html";
                string wsSummaryFilename = outputPrefix + "-summary.html";
                string wsDataProcessingDetailsFilename = outputPrefix + "-processing.html";
                string wsIndexByProteinFilename = outputPrefix + "-index-by-protein.html";
                string wsIndexBySpectrumFilename = outputPrefix + "-index-by-spectrum.html";
                string wsIndexByModificationFilename = outputPrefix + "-index-by-modification.html";
                string wsGroupsFilename = outputPrefix + "-groups.html";
                string wsSequencesPerProteinByGroupFilename = outputPrefix + "-sequences-per-protein-by-group.html";
                string wsSpectraPerProteinByGroupFilename = outputPrefix + "-spectra-per-protein-by-group.html";
                string wsSpectraPerPeptideByGroupFilename = outputPrefix + "-spectra-per-peptide-by-group.html";
                string wsCoverageStringsFilename = "coverage.txt";

                ghettoProgressForm.Text = "Writing navigation/scripting/styling files...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return;
                }

                try
                {
                    outputStream = new StreamWriter(reportIndexFilename);
                    outputStream.Write(Presentation.assembleReportIndex(outputPrefix, navFrameFilename, wsSummaryFilename));
                    outputStream.Close();
                }
                catch (Exception e)
                {
                    ghettoProgressForm.Close();
                    IdPickerRequest.StdError.AppendLine( e.Message );
                    IdPickerRequest.RunStatus = RunStatus.Error;
                    return;
                }

                try
                {
                    outputStream = new StreamWriter(idpickerJavascriptFilename);
                    outputStream.Write(Presentation.assembleJavascript());
                    outputStream.Close();
                }
                catch (Exception e)
                {
                    ghettoProgressForm.Close();
                    IdPickerRequest.StdError.AppendLine( e.Message );
                    IdPickerRequest.RunStatus = RunStatus.Error;
                    return;
                }

                try
                {
                    outputStream = new StreamWriter(idpickerStylesheetFilename);
                    outputStream.Write(Presentation.assembleStylesheet());
                    outputStream.Close();
                }
                catch (Exception e)
                {
                    ghettoProgressForm.Close();
                    IdPickerRequest.StdError.AppendLine( e.Message );
                    IdPickerRequest.RunStatus = RunStatus.Error;
                    return;
                }

                try
                {
                    Dictionary<string, string> navigationMap = new Dictionary<string, string>();
                    navigationMap.Add("Summary", wsSummaryFilename);
                    navigationMap.Add("Group association table", wsGroupsFilename);
                    navigationMap.Add("Index by protein", wsIndexByProteinFilename);
                    navigationMap.Add("Index by spectrum", wsIndexBySpectrumFilename);
                    navigationMap.Add("Index by modification", wsIndexByModificationFilename);
                    navigationMap.Add("Sequences per protein by group", wsSequencesPerProteinByGroupFilename);
                    navigationMap.Add("Spectra per protein by group", wsSpectraPerProteinByGroupFilename);
                    navigationMap.Add("Spectra per peptide by group", wsSpectraPerPeptideByGroupFilename);
                    navigationMap.Add("Data processing details", wsDataProcessingDetailsFilename);
                    outputStream = new StreamWriter(navFrameFilename);
                    Presentation.assembleNavFrameHtml(ws, outputStream, outputPrefix, navigationMap);
                    outputStream.Close();
                }
                catch (Exception e)
                {
                    ghettoProgressForm.Close();
                    IdPickerRequest.StdError.AppendLine( e.Message );
                    IdPickerRequest.RunStatus = RunStatus.Error;
                    return;
                }

                ghettoProgressForm.Text = "Writing index by protein...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return;
                }

                try
                {
                    outputStream = new StreamWriter(wsIndexByProteinFilename);
                    Presentation.assembleIndexByProteinHtml( ws, outputStream, outputPrefix );
                    outputStream.Close();
                }
                catch (Exception e)
                {
                    ghettoProgressForm.Close();
                    IdPickerRequest.StdError.AppendLine( e.Message );
                    IdPickerRequest.RunStatus = RunStatus.Error;
                    return;
                }

                ghettoProgressForm.Text = "Writing index by spectrum...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return;
                }

                try
                {
                    outputStream = new StreamWriter(wsIndexBySpectrumFilename);
                    Presentation.assembleIndexBySpectrumHtml( ws, outputStream, outputPrefix, IdPickerRequest.AllowSharedSourceNames );
                    outputStream.Close();
                }
                catch (Exception e)
                {
                    ghettoProgressForm.Close();
                    IdPickerRequest.StdError.AppendLine( e.Message );
                    IdPickerRequest.RunStatus = RunStatus.Error;
                    return;
                }

                ghettoProgressForm.Text = "Writing index by modification...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return;
                }

                try
                {
                    outputStream = new StreamWriter(wsIndexByModificationFilename);
                    Presentation.assembleIndexByModificationHtml( ws, outputStream, outputPrefix );
                    outputStream.Close();
                }
                catch (Exception e)
                {
                    ghettoProgressForm.Close();
                    IdPickerRequest.StdError.AppendLine( e.Message );
                    IdPickerRequest.RunStatus = RunStatus.Error;
                    return;
                }

                ghettoProgressForm.Text = "Writing overall summary...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return;
                }

                try
                {
                    outputStream = new StreamWriter(wsSummaryFilename);
                    Dictionary<string, string> parameterMap = new Dictionary<string, string>();
                    parameterMap.Add("Maximum FDR per result", IdPickerRequest.MaxFDR.ToString("f2"));
                    parameterMap.Add("Maximum ambiguous IDs per result", IdPickerRequest.MaxAmbiguousIds.ToString());
                    parameterMap.Add("Minimum peptide length per result", IdPickerRequest.MinPeptideLength.ToString());
                    parameterMap.Add("Minimum distinct peptides per protein", IdPickerRequest.MinDistinctPeptides.ToString());
                    parameterMap.Add("Minimum additional peptides per protein group", IdPickerRequest.MinAdditionalPeptides.ToString());
                    parameterMap.Add( "Minimum spectra per protein", IdPickerRequest.MinSpectraPerProetin.ToString() );
                    if (IdPickerRequest.ModsAreDistinctByDefault && indistinctModsOverride.ToString().Length > 0)
                        parameterMap.Add("Indistinct modifications", indistinctModsOverride.ToString());
                    else if (distinctModsOverride.ToString().Length > 0)
                        parameterMap.Add("Distinct modifications", distinctModsOverride.ToString());
                    Presentation.assembleSummaryHtml( ws, outputStream, outputPrefix, parameterMap );
                    outputStream.Close();
                }
                catch (Exception e)
                {
                    ghettoProgressForm.Close();
                    IdPickerRequest.StdError.AppendLine( e.Message );
                    IdPickerRequest.RunStatus = RunStatus.Error;
                    return;
                }

                ghettoProgressForm.Text = "Writing data processing summary...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return;
                }

                presentationEvent.endTime = DateTime.Now;
                foreach (SourceInfo source in ws.groups["/"].getSources(true))
                    source.processingEvents.Add(presentationEvent);
                try
                {
                    outputStream = new StreamWriter(wsDataProcessingDetailsFilename);
                    Presentation.assembleDataProcessingDetailsHtml( ws, outputStream, outputPrefix );
                    outputStream.Close();
                }
                catch (Exception e)
                {
                    ghettoProgressForm.Close();
                    IdPickerRequest.StdError.AppendLine( e.Message );
                    IdPickerRequest.RunStatus = RunStatus.Error;
                    return;
                }

                ghettoProgressForm.Text = "Writing group association summary...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return;
                }

                try
                {
                    outputStream = new StreamWriter(wsGroupsFilename);
                    Presentation.assembleGroupAssociationHtml( ws, outputStream, outputPrefix );
                    outputStream.Close();
                }
                catch (Exception e)
                {
                    ghettoProgressForm.Close();
                    IdPickerRequest.StdError.AppendLine( e.Message );
                    IdPickerRequest.RunStatus = RunStatus.Error;
                    return;
                }

                ghettoProgressForm.Text = "Writing sequences per protein by group table...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return;
                }

                try
                {
                    outputStream = new StreamWriter(wsSequencesPerProteinByGroupFilename);
                    Presentation.assembleProteinSequencesTable( ws, outputStream, outputPrefix );
                    outputStream.Close();
                }
                catch (Exception e)
                {
                    ghettoProgressForm.Close();
                    IdPickerRequest.StdError.AppendLine( e.Message );
                    IdPickerRequest.RunStatus = RunStatus.Error;
                    return;
                }

                ghettoProgressForm.Text = "Writing spectra per protein by group table...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return;
                }

                try
                {
                    outputStream = new StreamWriter(wsSpectraPerProteinByGroupFilename);
                    Presentation.assembleProteinSpectraTable( ws, outputStream, outputPrefix );
                    outputStream.Close();
                }
                catch (Exception e)
                {
                    ghettoProgressForm.Close();
                    IdPickerRequest.StdError.AppendLine( e.Message );
                    IdPickerRequest.RunStatus = RunStatus.Error;
                    return;
                }

                ghettoProgressForm.Text = "Writing spectra per peptide by group...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return;
                }

                try
                {
                    outputStream = new StreamWriter(wsSpectraPerPeptideByGroupFilename);
                    Presentation.assemblePeptideSpectraTable( ws, outputStream, outputPrefix );
                    outputStream.Close();
                }
                catch (Exception e)
                {
                    ghettoProgressForm.Close();
                    IdPickerRequest.StdError.AppendLine( e.Message );
                    IdPickerRequest.RunStatus = RunStatus.Error;
                    return;
                }


                try
                {
                    outputStream = new StreamWriter( wsCoverageStringsFilename );
                    Presentation.generateCoverageStrings( ws, outputStream );
                    outputStream.Close();
                } catch( Exception e )
                {
                    ghettoProgressForm.Close();
                    IdPickerRequest.StdError.AppendLine( e.Message );
                    IdPickerRequest.RunStatus = RunStatus.Error;
                    return;
                }

                for (int i = 0; i < ws.clusters.Count; ++i)
                {
                    int cid = i + 1;
                    ghettoProgressForm.Text = "Writing summary for cluster " + cid + " of " + ws.clusters.Count + "...";
                    Application.DoEvents();
                    if (ghettoProgressForm.IsDisposed)
                    {
                        IdPickerRequest.RunStatus = RunStatus.Cancelled;
                        return;
                    }

                    string clusterSummaryFilename = outputPrefix + "-cluster" + cid + ".html";
                    try
                    {
                        outputStream = new StreamWriter(clusterSummaryFilename);
                        Presentation.assembleClusterHtml( ws, outputStream, outputPrefix, i, false, "http://msgraph/", "/", ".*" );
                        outputStream.Close();
                    }
                    catch (Exception e)
                    {
                        ghettoProgressForm.Close();
                        IdPickerRequest.StdError.AppendLine( e.Message );
                        IdPickerRequest.RunStatus = RunStatus.Error;
                        return;
                    }
                }

                ghettoProgressBar.PerformStep();
                Application.DoEvents();

                ghettoProgressForm.Close();
                Application.DoEvents();

                Directory.SetCurrentDirectory(lastCurrentDirectory);
            }
            catch (Exception exc)
            {
                ghettoProgressForm.Close();

                throw exc;
            }
		}

        public void startRequest()
        {
            if (!IdPickerRequest.Id.Equals(-1))
            {
				try
				{
					string srcDirPath = IdPickerRequest.ResultsDir + "\\" + IdPickerRequest.ReportName + INPUT_LIST_NAME;

					IdPickerRequest.RunStatus = RunStatus.InProgress;
					IdPickerRequest.DateRunStart = DateTime.Now;
                    IdPickerRequest.StdError = new StringBuilder();
                    IdPickerRequest.StdOutput = new StringBuilder();

					bool hasPepXmlFiles = createSourceListFile( srcDirPath );

                    if(hasPepXmlFiles)
					    runConvert( srcDirPath );

					if( IdPickerRequest.RunStatus == RunStatus.InProgress )
						runReport();

					switch( IdPickerRequest.RunStatus )
					{
						default:
							break;
						case RunStatus.InProgress:
							IdPickerRequest.RunStatus = RunStatus.Complete;
							break;
						case RunStatus.Error:
                            IdPickerRequest.StdError.Insert( 0, "Report was aborted due to a fatal error: " );
                            return;
						case RunStatus.Cancelled:
                            IdPickerRequest.StdError.Insert( 0, Environment.NewLine + "Report was cancelled by user." );
                            return;
					}

					IdPickerRequest.DateRunComplete = DateTime.Now;

				} catch( Exception e )
				{
					// add simplified error message to detailed error log
                    IdPickerRequest.StdError.Insert( 0, "Report was aborted due to a fatal error: " + e.Message + Environment.NewLine );

					// don't override cancelled status with error status
					if( IdPickerRequest.RunStatus != RunStatus.Cancelled )
						IdPickerRequest.RunStatus = RunStatus.Error;
				} finally
				{
					//removeResourceFiles();
				}
            }
        }
    }
}
