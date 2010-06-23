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
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using IDPicker;
using System.Diagnostics;

namespace idpReport
{
    public class RunTimeConfig : Workspace.RunTimeConfig
    {
        internal string DefaultConfigFilename = "idpReport.cfg";
        internal string DefaultBufferDelimiters = "\r\n#";
        internal string DefaultFileDelimiters = "\r\n\t ";

        public int MinAdditionalPeptides = 1;
        public bool GenerateBipartiteGraphs = false;
        public bool ModsAreDistinctByDefault = true;
        public string DistinctModsOverride = "";
        public string IndistinctModsOverride = "";

        public string QuantitationMethod = "None";
        public bool OutputWebReport = true;
        public bool OutputTextReport = false;
        public bool OutputXmlReport = false;

        public string RawSourceHostURL = "http://localhost/";
        public string RawSourceExtension = ".*"; // try all extensions until one works
        public string RawSourcePath = String.Empty;
        public string UnimodXMLPath = String.Empty;

        /// <summary>
        /// This variable provides a database to annotate the potential SNPs
        /// found by the database.
        /// </summary>
        public string AnnotateSNPs = "";

        /// <summary>
        /// These two variables tell the program where to find the
        /// input files for annotating potential SNPs using ProCanVar
        /// database.
        /// </summary>
        public string ProCanVarFasta = "";
        public string ProCanVarMap = "";

        /// <summary>
        /// This variable flags peptides that have
        /// unknown modificaitons.
        /// </summary>
        public string FlagUnknownMods = "";

        /// <summary>
        /// These two variables control the annotation of best unmodified
        /// peptide for a peptide with unknown modification.
        /// </summary>
        public string SpectraExport = "";
        public string SearchScoreNames = "";

        internal string outputDir;
        internal string outputPrefix;
        internal QuantitationInfo.Method quantitationMethod;

        protected override void finalize()
        {
            if( !RawSourceHostURL.EndsWith( "/" ) )
                RawSourceHostURL += "/";
            if( RawSourcePath != String.Empty && !RawSourcePath.EndsWith( "/" ) )
                RawSourcePath += "/";

            switch (QuantitationMethod.ToLower())
            {
                case "none": quantitationMethod = QuantitationInfo.Method.None; break;
                case "itraq4plex": quantitationMethod = QuantitationInfo.Method.ITRAQ4Plex; break;
                case "itraq8plex": quantitationMethod = QuantitationInfo.Method.ITRAQ8Plex; break;
                default:
                    throw new ArgumentException("invalid quantitation method \"" + QuantitationMethod + "\"");
            }
        }
    }

    public partial class Program
    {
        public static string Version { get { return Util.GetAssemblyVersion( System.Reflection.Assembly.GetExecutingAssembly().GetName() ); } }
        public static DateTime LastModified { get { return Util.GetAssemblyLastModified( System.Reflection.Assembly.GetExecutingAssembly().GetName() ); } }
        public static string IDPREPORT_LICENSE = Workspace.LICENSE;

        public static RunTimeConfig rtConfig;

        /// <summary>
        /// Reads the command line for idpReport program
        /// </summary>
        /// <param name="args">Command line</param>
        /// <returns>True if successfully read the command line</returns>
        static bool InitProcess( ref List<string> args )
        {
            if( args.Count < 2 )
            {
                Console.Error.WriteLine( "Not enough arguments.\nUsage: idpReport [-dump] [-cfg <config filepath>]" +
                                        " <output name> <input peptides XML filemask> [<another filemask> ...]" );
                return true;
            }

            rtConfig = new RunTimeConfig();

            // Read command line arguments
            for( int i = 1; i < args.Count; ++i )
            {
                if( args[i] == "-cfg" && i + 1 <= args.Count )
                {
                    if( rtConfig.initializeFromFile( args[i + 1], rtConfig.DefaultFileDelimiters ) )
                    {
                        Console.Error.WriteLine( "Could not find runtime configuration at \"" + args[i + 1] + "\"." );
                        return true;
                    }
                    args.RemoveAt( i );
                    args.RemoveAt( i );
                    --i;
                }
            }

            // Initialize the runtime based on either the defaults or user-supplied 
            // configuration file
            if( !rtConfig.initialized() )
            {
                if( rtConfig.initializeFromFile( rtConfig.DefaultConfigFilename, rtConfig.DefaultFileDelimiters ) )
                {
                    Console.Error.WriteLine( "Could not find the default configuration file (hard-coded defaults in use)." );
                }
            }

            args = rtConfig.initializeFromCLI( args );

            for( int i = 0; i < args.Count; ++i )
            {
                if( args[i] == "-dump" )
                {
                    rtConfig.dump();
                    args.RemoveAt( i );
                    --i;
                }
            }

            for( int i = 0; i < args.Count; ++i )
            {
                if( args[i][0] == '-' )
                {
                    Console.Error.WriteLine( "Warning: ignoring unrecognized parameter \"" + args[i] + "\"" );
                    args.RemoveAt( i );
                    --i;
                }
            }

            //Set the min spectra count per protein properly. 
            //rtConfig.MinSpectraPerProtein = Math.Max( rtConfig.MinSpectraPerProtein, rtConfig.MinSpectraPerPeptide * rtConfig.MinDistinctPeptides );

            if( args.Count < 2 )
            {
                Console.Error.WriteLine( "Not enough arguments.\nUsage: idpReport [-dump] [-cfg <config filepath>]" +
                                        " <output name> <input peptides XML filemask> [<another filemask> ...]" );
                return true;
            }

            // Set the output directory
            rtConfig.outputDir = args[0];
            string[] outputPathArray = rtConfig.outputDir.Split( Path.DirectorySeparatorChar );
            rtConfig.outputPrefix = outputPathArray[outputPathArray.Length - 1];

            return false;
        }

        static void Main( string[] args )
        {
            // Write program headers to the command line
            Console.WriteLine( "IDPickerReport " + Version + " (" + LastModified.ToShortDateString() + ")" );
            Console.WriteLine( "IDPickerWorkspace " + Workspace.Version + " (" + Workspace.LastModified.ToShortDateString() + ")" );
            Console.WriteLine( "IDPickerPresentation " + Presentation.Version + " (" + Presentation.LastModified.ToShortDateString() + ")" );
            Console.WriteLine( IDPREPORT_LICENSE );

            // Process the command line
            List<string> argsList = new List<string>( args );
            if( InitProcess( ref argsList ) )
            {
                return;
            }

            // Init the IDPicker workspace
            long start;
            Workspace ws = new Workspace( rtConfig );
            ws.setStatusOutput( Console.Out );

            // Initialize the modifications that can and can not appear in distinct peptides
            ws.distinctPeptideSettings = new Workspace.DistinctPeptideSettings(
                rtConfig.ModsAreDistinctByDefault, rtConfig.DistinctModsOverride, rtConfig.IndistinctModsOverride );

            // Read the spectra export from a previous search, if given
            if( rtConfig.SpectraExport != string.Empty && rtConfig.SearchScoreNames != string.Empty )
            {
                string[] scores = rtConfig.SearchScoreNames.Split( new char[] { ' ' } );
                ws.spectraExport = new ImportSpectraExport( rtConfig.SpectraExport, scores );
            }

            #region SNP Annotation stuff (TODO: simplify this logic, i.e. use a bool instead of testing for empty string)
            // Read the unimod modification database, if provided
            if( rtConfig.UnimodXMLPath != string.Empty )
            {
                try
                {
                    Console.WriteLine( "Reading modification annotations from unimod xml file path: " + rtConfig.UnimodXMLPath );
                    ws.readUniModXML( rtConfig.UnimodXMLPath );
                    Console.WriteLine( "Finished reading modification annotations." );
                    //ws.printUnimodObjects( 57.03f );
                    //Environment.Exit( 1 );
                } catch( Exception e )
                {
                    Console.Error.WriteLine( e.StackTrace.ToString() );
                    Console.Error.WriteLine( "Error reading unimod xml at : " + rtConfig.UnimodXMLPath );
                }
            }

            // Read the CanProVar database, if provided
            if( rtConfig.ProCanVarFasta != string.Empty && rtConfig.ProCanVarMap != string.Empty )
            {
                Console.WriteLine( "Reading ProCanVar SNP data....." );
                ws.snpAnntoations = new SNPMetaDataGenerator();
                ws.snpAnntoations.getVariantDataFromProCanVarFlatFile( rtConfig.ProCanVarFasta, rtConfig.ProCanVarMap );
                Console.WriteLine( "Finished reading ProCanVar SNP data." );

                /*SNPMetaDataGenerator snpData = new SNPMetaDataGenerator();
                snpData.getVariantDataFromProCanVarFlatFile( rtConfig.ProCanVarFasta, rtConfig.ProCanVarMap );
                Console.WriteLine( "Finished reading ProCanVar SNP data." );
                SNPMetaDataGenerator.SNP test = new SNPMetaDataGenerator.SNP( "Y", "H", "24" );
                string str = snpData.getSNPAnnotationFromProCanVar( "ENSP00000368054", test, "EGFYAVVIFLSIFVIIVTCLMILYR" );
                Console.WriteLine( str );
                Environment.Exit( 1 );*/
            }
            #endregion

            #region Read input idpXML files

            List<string> inputFilepaths = new List<string>();

            for( int i = 1; i < argsList.Count; ++i )
            {
                try
                {
                    string path = Path.GetDirectoryName(argsList[i]);
                    if (path.Length < 1)
                    {
                        path = "." + Path.DirectorySeparatorChar;
                    }
                    //Console.WriteLine( path + " : " + Path.GetFileName( args[i] ) );
                    inputFilepaths.AddRange(Directory.GetFiles(path, Path.GetFileName(argsList[i])));

                    foreach( string inputFilepath in inputFilepaths )
                    {
                        try
                        {
                            if( !Util.TestFileType( inputFilepath, "idpickerpeptides" ) )
                                continue;
                            start = DateTime.Now.Ticks;
                            Console.WriteLine( "Reading and parsing IDPicker XML from filepath: " + inputFilepath );
                            StreamReader inputStream = new StreamReader( inputFilepath );
                            // Read each file and assign them to the root group.
                            ws.readPeptidesXml( inputStream, "", rtConfig.MaxFDR, rtConfig.MaxResultRank );
                            inputStream.Close();
                            Console.WriteLine( "\nFinished reading and parsing IDPicker XML; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." );
                        } catch( Exception e )
                        {
                            Console.Error.WriteLine( e.StackTrace.ToString() );
                            Console.Error.WriteLine( "Error reading input filepath \"" + inputFilepath + "\": " + e.Message );
                            continue;
                        }
                    }
                } catch( Exception e )
                {
                    Console.Error.WriteLine( "Error parsing input filemask \"" + argsList[i] + "\": " + e.Message );
                    continue;
                }
            }

            if( ws.groups.Count == 0 )
            {
                Console.Error.WriteLine( "Error: no input files read; nothing to report." );
                return;
            }
            #endregion

            // Generate a processing event for the idpReport.
            ProcessingEvent presentationEvent = new ProcessingEvent();
            presentationEvent.type = "presentation/filtration";
            presentationEvent.startTime = DateTime.Now;
            ProcessingParam presentationParam = new ProcessingParam();
            presentationParam.name = "software name";
            presentationParam.value = "idpReport";
            presentationEvent.parameters.Add( presentationParam );
            presentationParam = new ProcessingParam();
            presentationParam.name = "software version";
            presentationParam.value = Version + " (" + LastModified.ToShortDateString() + ")";
            presentationEvent.parameters.Add( presentationParam );
            RunTimeVariableMap presentationVars = rtConfig.getVariables();
            foreach( KeyValuePair<string, string> itr in presentationVars )
            {
                presentationParam = new ProcessingParam();
                presentationParam.name = itr.Key;
                presentationParam.value = itr.Value;
                presentationEvent.parameters.Add( presentationParam );
            }

            #region Pre-filtering validation
            { ws.validate( rtConfig.MaxFDR, 0, 100 ); }
            #endregion

            #region Apply filtering criteria and assemble proteins
            start = DateTime.Now.Ticks;
            Console.WriteLine( "Filtering out peptides shorter than " + rtConfig.MinPeptideLength + " residues..." );
            ws.filterByMinimumPeptideLength( rtConfig.MinPeptideLength );
            Console.WriteLine( "\nFinished filtering by minimum peptide length; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." ); { ws.validate( rtConfig.MaxFDR, 0, 100 ); }

            start = DateTime.Now.Ticks;
            Console.WriteLine( "Filtering out results with more than " + rtConfig.MaxAmbiguousIds + " ambiguous ids..." );
            ws.filterByResultAmbiguity( rtConfig.MaxAmbiguousIds );
            Console.WriteLine( "\nFinished filtering by maximum ambiguous ids; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." ); { ws.validate( rtConfig.MaxFDR, 0, rtConfig.MaxAmbiguousIds ); }

            start = DateTime.Now.Ticks;
            Console.WriteLine( "Filtering out proteins with less than " + rtConfig.MinDistinctPeptides + " distinct peptides..." );
            ws.filterByDistinctPeptides( rtConfig.MinDistinctPeptides );
            Console.WriteLine( "\nFinished filtering by minimum distinct peptides; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." ); { ws.validate( rtConfig.MaxFDR, rtConfig.MinDistinctPeptides, rtConfig.MaxAmbiguousIds ); }

            start = DateTime.Now.Ticks;
            Console.WriteLine( "Filtering out proteins with less than " + rtConfig.MinSpectraPerProtein + " spectra..." );
            ws.filterBySpectralCount( rtConfig.MinSpectraPerProtein );
            Console.WriteLine( "\nFinished filtering by minimum protein spectral counts; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." ); { ws.validate( rtConfig.MaxFDR, rtConfig.MinDistinctPeptides, rtConfig.MaxAmbiguousIds ); }

            start = DateTime.Now.Ticks;
            Console.WriteLine( "Assembling protein groups..." );
            ws.assembleProteinGroups();
            Console.WriteLine( "\nFinished assembling protein groups; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." ); { ws.validate( rtConfig.MaxFDR, rtConfig.MinDistinctPeptides, rtConfig.MaxAmbiguousIds ); }

            start = DateTime.Now.Ticks;
            Console.WriteLine( "Assembling peptide groups..." );
            ws.assemblePeptideGroups();
            Console.WriteLine( "\nFinished assembling peptide groups; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." ); { ws.validate( rtConfig.MaxFDR, rtConfig.MinDistinctPeptides, rtConfig.MaxAmbiguousIds ); }

            start = DateTime.Now.Ticks;
            Console.WriteLine( "Assembling clusters..." );
            ws.assembleClusters();
            Console.WriteLine( "\nFinished assembling clusters; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." ); { ws.validate( rtConfig.MaxFDR, rtConfig.MinDistinctPeptides, rtConfig.MaxAmbiguousIds ); }
            start = DateTime.Now.Ticks;

            // Determine the minimum number of protein clusters needed
            // to explain all the peptides.
            Console.WriteLine( "Assembling minimum covering set for clusters..." );
            int clusterCount = 0;
            foreach( ClusterInfo c in ws.clusters )
            {
                ++clusterCount;
                Console.Write( "Assembling minimum covering set for cluster " + clusterCount + " of " + ws.clusters.Count + " (" + c.proteinGroups.Count + " protein groups, " + c.results.Count + " results)          \r" );
                ws.assembleMinimumCoveringSet( c );
            }
            Console.WriteLine( "\nFinished assembling minimum covering sets; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." ); { ws.validate( rtConfig.MaxFDR, rtConfig.MinDistinctPeptides, rtConfig.MaxAmbiguousIds ); }

            if( rtConfig.MinAdditionalPeptides > 0 )
            {
                start = DateTime.Now.Ticks;
                Console.WriteLine( "Filtering workspace by minimum covering set..." );
                ws.filterByMinimumCoveringSet( rtConfig.MinAdditionalPeptides );
                Console.WriteLine( "\nFinished filtering by minimum covering set; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." ); { ws.validate( rtConfig.MaxFDR, rtConfig.MinDistinctPeptides, rtConfig.MaxAmbiguousIds ); }
            }

            start = DateTime.Now.Ticks;
            Console.Write( "Assembling source groups..." );
            ws.assembleSourceGroups();
            Console.WriteLine( " finished; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." );
            #endregion

            #region Post-filtering validation
            // Check to see if the protein, peptides, sources, and the clusters have
            // all required fields and satisfy the user-defined criteria.
            try
            {
                ws.validate( rtConfig.MaxFDR, rtConfig.MinDistinctPeptides, rtConfig.MaxAmbiguousIds );
            } catch( Exception e )
            {
                Console.Error.WriteLine( "Error while validating workspace: " + e.Message );
                return;
            }
            #endregion

            #region SNP annotation stuff (TODO: move this into a Workspace function)
            if( rtConfig.FlagUnknownMods.Length > 0 )
            {
                string[] toks = rtConfig.FlagUnknownMods.Split( new char[] { ' ' } );
                if( toks.Length % 2 == 0 )
                {
                    ws.knownModMasses = new float[toks.Length / 2];
                    ws.knownModResidues = new char[toks.Length / 2];
                    int index = 0;
                    for( int i = 0; i < toks.Length - 1; i = i + 2 )
                    {
                        ws.knownModResidues[index] = toks[i][0];
                        ws.knownModMasses[index] = (float) Convert.ToDouble( toks[i + 1] );
                        ++index;
                    }
                }
            }

            // If the user wants to annotate the SNPs found in the
            // dataset then pull the annotations.
            if( rtConfig.AnnotateSNPs.Length > 0 )
            {
                ws.pullSNPMetaData( rtConfig.AnnotateSNPs );
            }
            #endregion

            // Make an output folder and generate files
            Console.WriteLine( "Creating output directory for report: " + rtConfig.outputDir );
            if( !Directory.Exists( rtConfig.outputDir ) )
                Directory.CreateDirectory( rtConfig.outputDir );
            Directory.SetCurrentDirectory( rtConfig.outputDir );

            StreamWriter outputStream;

            #region Web report
            if( rtConfig.OutputWebReport )
            {
                string reportIndexFilename = "index.html";
                string idpickerJavascriptFilename = "idpicker-scripts.js";
                string idpickerStylesheetFilename = "idpicker-style.css";
                string navFrameFilename = rtConfig.outputPrefix + "-nav.html";
                string wsSummaryFilename = rtConfig.outputPrefix + "-summary.html";
                string wsDataProcessingDetailsFilename = rtConfig.outputPrefix + "-processing.html";
                string wsIndexByProteinFilename = rtConfig.outputPrefix + "-index-by-protein.html";
                string wsIndexBySpectrumFilename = rtConfig.outputPrefix + "-index-by-spectrum.html";
                string wsIndexByModificationFilename = rtConfig.outputPrefix + "-index-by-modification.html";
                string wsGroupsFilename = rtConfig.outputPrefix + "-groups.html";
                string wsSequencesPerProteinByGroupFilename = rtConfig.outputPrefix + "-sequences-per-protein-by-group.html";
                string wsSpectraPerProteinByGroupFilename = rtConfig.outputPrefix + "-spectra-per-protein-by-group.html";
                string wsSpectraPerPeptideByGroupFilename = rtConfig.outputPrefix + "-spectra-per-peptide-by-group.html";


                try
                {
                    Console.WriteLine( "Writing report index: " + reportIndexFilename );
                    outputStream = new StreamWriter( reportIndexFilename );
                    outputStream.Write( Presentation.assembleReportIndex( rtConfig.outputPrefix, navFrameFilename, wsSummaryFilename ) );
                    outputStream.Close();
                } catch( Exception e )
                {
                    Console.Error.WriteLine( "\nError report index: " + e.Message );
                }

                try
                {
                    Console.WriteLine( "Writing javascript: " + idpickerJavascriptFilename );
                    outputStream = new StreamWriter( idpickerJavascriptFilename );
                    outputStream.Write( Presentation.assembleJavascript() );
                    outputStream.Close();
                } catch( Exception e )
                {
                    Console.Error.WriteLine( "\nError writing javascript: " + e.Message );
                }

                try
                {
                    Console.WriteLine( "Writing stylesheet: " + idpickerStylesheetFilename );
                    outputStream = new StreamWriter( idpickerStylesheetFilename );
                    outputStream.Write( Presentation.assembleStylesheet() );
                    outputStream.Close();
                } catch( Exception e )
                {
                    Console.Error.WriteLine( "\nError writing stylesheet: " + e.Message );
                }

                try
                {
                    Console.WriteLine( "Writing navigation frame: " + navFrameFilename );
                    Dictionary<string, string> navigationMap = new Dictionary<string, string>();
                    navigationMap.Add( "Summary", wsSummaryFilename );
                    navigationMap.Add( "Group association table", wsGroupsFilename );
                    navigationMap.Add( "Index by protein", wsIndexByProteinFilename );
                    navigationMap.Add( "Index by spectrum", wsIndexBySpectrumFilename );
                    navigationMap.Add( "Index by modification", wsIndexByModificationFilename );
                    navigationMap.Add( "Sequences per protein by group", wsSequencesPerProteinByGroupFilename );
                    navigationMap.Add( "Spectra per protein by group", wsSpectraPerProteinByGroupFilename );
                    navigationMap.Add( "Spectra per peptide by group", wsSpectraPerPeptideByGroupFilename );
                    navigationMap.Add( "Data processing details", wsDataProcessingDetailsFilename );
                    outputStream = new StreamWriter( navFrameFilename );
                    Presentation.assembleNavFrameHtml( ws, outputStream, rtConfig.outputPrefix, navigationMap );
                    outputStream.Close();
                } catch( Exception e )
                {
                    Console.Error.WriteLine( "\nError writing navigation frame: " + e.Message );
                }

                try
                {
                    Console.WriteLine( "Writing index by protein to filepath: " + wsIndexByProteinFilename );
                    outputStream = new StreamWriter( wsIndexByProteinFilename );
                    Presentation.assembleIndexByProteinHtml( ws, outputStream, rtConfig.outputPrefix );
                    outputStream.Close();
                } catch( Exception e )
                {
                    Console.Error.WriteLine( "\nError writing index by protein: " + e.Message );
                }

                try
                {
                    Console.WriteLine( "Writing index by spectrum to filepath: " + wsIndexBySpectrumFilename );
                    outputStream = new StreamWriter( wsIndexBySpectrumFilename );
                    Presentation.assembleIndexBySpectrumHtml( ws, outputStream, rtConfig.outputPrefix, rtConfig.AllowSharedSourceNames );
                    outputStream.Close();
                } catch( Exception e )
                {
                    Console.Error.WriteLine( "\nError writing index by spectrum: " + e.Message );
                }

                try
                {
                    Console.WriteLine( "Writing index by modification to filepath: " + wsIndexByModificationFilename );
                    outputStream = new StreamWriter( wsIndexByModificationFilename );
                    Presentation.assembleIndexByModificationHtml( ws, outputStream, rtConfig.outputPrefix );
                    outputStream.Close();
                } catch( Exception e )
                {
                    Console.Error.WriteLine( "\nError writing index by modification: " + e.Message );
                }

                try
                {
                    Console.WriteLine( "Writing overall summary to filepath: " + wsSummaryFilename );
                    outputStream = new StreamWriter( wsSummaryFilename );
                    Dictionary<string, string> parameterMap = new Dictionary<string, string>();
                    parameterMap.Add( "Maximum FDR", rtConfig.MaxFDR.ToString( "f2" ) );
                    parameterMap.Add( "Minimum distinct peptides", rtConfig.MinDistinctPeptides.ToString() );
                    parameterMap.Add( "Maximum ambiguous IDs", rtConfig.MaxAmbiguousIds.ToString() );
                    parameterMap.Add( "Minimum peptide length", rtConfig.MinPeptideLength.ToString() );
                    parameterMap.Add( "Minimum additional peptides", rtConfig.MinAdditionalPeptides.ToString() );
                    parameterMap.Add( "Minimum spectra per protein", rtConfig.MinSpectraPerProtein.ToString() );
                    if( rtConfig.ModsAreDistinctByDefault )
                        parameterMap.Add( "Indistinct modifications", rtConfig.IndistinctModsOverride );
                    else
                        parameterMap.Add( "Distinct modifications", rtConfig.DistinctModsOverride );
                    Presentation.assembleSummaryHtml( ws, outputStream, rtConfig.outputPrefix, parameterMap );
                    outputStream.Close();
                } catch( Exception e )
                {
                    Console.Error.WriteLine( "\nError writing overall summary: " + e.Message );
                }

                presentationEvent.endTime = DateTime.Now;
                foreach( SourceInfo source in ws.groups["/"].getSources( true ) )
                    source.processingEvents.Add( presentationEvent );
                try
                {
                    Console.WriteLine( "Writing data processing details to filepath: " + wsDataProcessingDetailsFilename );
                    outputStream = new StreamWriter( wsDataProcessingDetailsFilename );
                    Presentation.assembleDataProcessingDetailsHtml( ws, outputStream, rtConfig.outputPrefix );
                    outputStream.Close();
                } catch( Exception e )
                {
                    Console.Error.WriteLine( "\nError writing data processing details: " + e.Message );
                }

                try
                {
                    Console.WriteLine( "Writing source groups summary to filepath: " + wsGroupsFilename );
                    outputStream = new StreamWriter( wsGroupsFilename );
                    Presentation.assembleGroupAssociationHtml( ws, outputStream, rtConfig.outputPrefix );
                    outputStream.Close();
                } catch( Exception e )
                {
                    Console.Error.WriteLine( "\nError writing source groups summary: " + e.Message );
                }

                Console.WriteLine( "Writing sequences per protein table to filepath: " + wsSequencesPerProteinByGroupFilename );
                try
                {
                    outputStream = new StreamWriter( wsSequencesPerProteinByGroupFilename );
                    Presentation.assembleProteinSequencesTable( ws, outputStream, rtConfig.outputPrefix );
                    outputStream.Close();
                } catch( Exception e )
                {
                    Console.Error.WriteLine( "\nError writing sequences per protein table: " + e.Message );
                }

                try
                {
                    Console.WriteLine( "Writing spectra per protein table to filepath: " + wsSpectraPerProteinByGroupFilename );
                    outputStream = new StreamWriter( wsSpectraPerProteinByGroupFilename );
                    Presentation.assembleProteinSpectraTable( ws, outputStream, rtConfig.outputPrefix );
                    outputStream.Close();
                } catch( Exception e )
                {
                    Console.Error.WriteLine( "\nError writing spectra per protein table: " + e.Message );
                }

                try
                {
                    Console.WriteLine( "Writing spectra per peptide table to filepath: " + wsSpectraPerPeptideByGroupFilename );
                    outputStream = new StreamWriter( wsSpectraPerPeptideByGroupFilename );
                    Presentation.assemblePeptideSpectraTable( ws, outputStream, rtConfig.outputPrefix );
                    outputStream.Close();
                } catch( Exception e )
                {
                    Console.Error.WriteLine( "\nError writing spectra per peptide table: " + e.Message );
                }

                for( int i = 0; i < ws.clusters.Count; ++i )
                {
                    int cid = i + 1;
                    string clusterSummaryFilename = rtConfig.outputPrefix + "-cluster" + cid + ".html";
                    try
                    {
                        Console.Write( "Writing summary for cluster " + cid + " of " + ws.clusters.Count + " to filepath: " + clusterSummaryFilename + "          \r" );
                        outputStream = new StreamWriter( clusterSummaryFilename );
                        Presentation.assembleClusterHtml( ws, outputStream, rtConfig.outputPrefix, i, rtConfig.GenerateBipartiteGraphs,
                                            rtConfig.RawSourceHostURL, rtConfig.RawSourcePath, rtConfig.RawSourceExtension );
                        outputStream.Close();
                    } catch( System.Exception e )
                    {
                        Console.Error.WriteLine( "\nError writing summary for cluster " + cid + ": " + e.Message );
                    }
                }
                Console.WriteLine();
            }
            #endregion

            #region Generate GraphViz files
            /*outputStream = new StreamWriter( "workspace.dot" );
            outputStream.Write( assembleWorkspaceGraph( ws ) );
            outputStream.Close();*/

            if( rtConfig.GenerateBipartiteGraphs )
            {
                for( int i = 0; i < ws.clusters.Count; ++i )
                {
                    int cid = i + 1;
                    string clusterGraphTextFilename = rtConfig.outputPrefix + "-cluster" + cid + ".dot";
                    Console.Write( "Writing graph of cluster " + cid + " of " + ws.clusters.Count + " to filepath: " + clusterGraphTextFilename + "           \r" );
                    outputStream = new StreamWriter( clusterGraphTextFilename );
                    Presentation.assembleClusterGraph( ws, outputStream, i );
                    outputStream.Close();
                }
                Console.WriteLine();

                for( int i = 0; i < ws.clusters.Count; ++i )
                {
                    int cid = i + 1;
                    string clusterGraphTextFilename = rtConfig.outputPrefix + "-cluster" + cid + ".dot";
                    string clusterGraphImageFilename = rtConfig.outputPrefix + "-cluster" + cid + ".gif";
                    Console.Write( "Graphing cluster " + cid + " of " + ws.clusters.Count + " from filepath: " + clusterGraphTextFilename + "             \r" );
                    string dotCommand = "dot";
                    string dotArgs = "-Tgif -o" + clusterGraphImageFilename + " " + clusterGraphTextFilename;
                    Process dotProcess = new Process();
                    dotProcess.StartInfo.FileName = dotCommand;
                    dotProcess.StartInfo.Arguments = dotArgs;
                    dotProcess.StartInfo.CreateNoWindow = true;
                    dotProcess.StartInfo.UseShellExecute = false;
                    dotProcess.Start();
                    dotProcess.WaitForExit();
                    //File.Delete( clusterGraphTextFilename );
                }
                Console.WriteLine();
            }
            #endregion

            #region Text report
            if( rtConfig.OutputTextReport )
            {
                string wsSummaryFilename = rtConfig.outputPrefix + "-summary.tsv";
                string wsSequencesPerProteinByGroupFilename = rtConfig.outputPrefix + "-sequences-per-protein-by-group.tsv";
                string wsSpectraPerProteinByGroupFilename = rtConfig.outputPrefix + "-spectra-per-protein-by-group.tsv";
                string wsSpectraPerPeptideByGroupFilename = rtConfig.outputPrefix + "-spectra-per-peptide-by-group.tsv";
                string wsSpectraTableFilename = rtConfig.outputPrefix + "-spectra-table.tsv";
                string wsProteinGroupToPeptideGroupFilename = rtConfig.outputPrefix + "-protein-to-peptide-table.tsv";
                string wsSpectraPerPeptideGroupFilename = rtConfig.outputPrefix + "-spectra-per-peptide-group.tsv";

                // Write a protein modification report. This report takes all the modifications
                // seen in the dataset and writes out a tab-delimited file of each modification
                // and the samples it was identified
                /*try
                {
                    string modificationTableFilename = rtConfig.outputPrefix + "-prot-modification-table.csv";
                    Console.WriteLine( "Writing protein modification report to filepath: " + modificationTableFilename );
                    outputStream = new StreamWriter( modificationTableFilename );
                    outputStream.Write( Presentation.assembleDetailedModificationReport( ws ) );
                    outputStream.Close();
                } catch( Exception e )
                {
                    Console.Error.WriteLine( "\nError writing protein modification report: " + e.Message );
                }*/

                string rootInputDirectory;
                Util.LongestCommonPrefix(inputFilepaths, out rootInputDirectory);
                if (String.IsNullOrEmpty(rootInputDirectory))
                    rootInputDirectory = Directory.GetCurrentDirectory();
                else if (!Directory.Exists(rootInputDirectory))
                    rootInputDirectory = Path.GetDirectoryName(rootInputDirectory);

                QuantifyingTransmogrifier.quantify(ws, rootInputDirectory, rtConfig.quantitationMethod);

                try
                {
                    Console.WriteLine( "Writing overall summary to filepath: " + wsSummaryFilename );
                    outputStream = new StreamWriter( wsSummaryFilename );
                    Presentation.exportSummaryTable( ws, outputStream, rtConfig.outputPrefix, '\t' );
                    outputStream.Close();
                } catch( Exception e )
                {
                    Console.Error.WriteLine( "\nError writing overall summary: " + e.Message );
                }

                Console.WriteLine( "Writing sequences per protein table to filepath: " + wsSequencesPerProteinByGroupFilename );
                try
                {
                    outputStream = new StreamWriter( wsSequencesPerProteinByGroupFilename );
                    Presentation.exportProteinSequencesTable( ws, outputStream, rtConfig.outputPrefix, '\t' );
                    outputStream.Close();
                } catch( Exception e )
                {
                    Console.Error.WriteLine( "\nError writing sequences per protein table: " + e.Message );
                }

                try
                {
                    Console.WriteLine( "Writing spectra per protein table to filepath: " + wsSpectraPerProteinByGroupFilename );
                    outputStream = new StreamWriter( wsSpectraPerProteinByGroupFilename );
                    Presentation.exportProteinSpectraTable( ws, outputStream, rtConfig.outputPrefix, '\t' );
                    outputStream.Close();
                } catch( Exception e )
                {
                    Console.Error.WriteLine( "\nError writing spectra per protein table: " + e.Message );
                }

                try
                {
                    Console.WriteLine( "Writing spectra per peptide table to filepath: " + wsSpectraPerPeptideByGroupFilename );
                    outputStream = new StreamWriter( wsSpectraPerPeptideByGroupFilename );
                    Presentation.exportPeptideSpectraTable(ws, outputStream, rtConfig.outputPrefix, rtConfig.quantitationMethod, '\t');
                    outputStream.Close();
                } catch( Exception e )
                {
                    Console.Error.WriteLine( "\nError writing spectra per peptide table: " + e.Message );
                }

                try
                {
                    Console.WriteLine( "Writing spectra table to filepath: " + wsSpectraTableFilename );
                    outputStream = new StreamWriter( wsSpectraTableFilename );
                    Presentation.exportSpectraTable( ws, outputStream, rtConfig.outputPrefix, rtConfig.quantitationMethod, '\t' );
                    outputStream.Close();
                } catch( Exception e )
                {
                    Console.Error.WriteLine( "\nError writing spectra table: " + e.Message );
                }

                try
                {
                    Console.WriteLine( "Writing protein group to peptide group table to filepath: " + wsProteinGroupToPeptideGroupFilename );
                    outputStream = new StreamWriter( wsProteinGroupToPeptideGroupFilename );
                    Presentation.exportProteinGroupToPeptideGroupTable( ws, outputStream, '\t' );
                    outputStream.Close();
                } catch( Exception e )
                {
                    Console.Error.WriteLine( "\nError protein group to peptide group table: " + e.Message );
                }

                try
                {
                    Console.WriteLine( "Writing spectra per peptide group table to filepath: " + wsSpectraPerPeptideGroupFilename );
                    outputStream = new StreamWriter(wsSpectraPerPeptideGroupFilename);
                    Presentation.exportPeptideGroupSpectraTable(ws, outputStream, rtConfig.outputPrefix, rtConfig.quantitationMethod, '\t');
                    outputStream.Close();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine( "\nError spectra per peptide group table: " + e.Message );
                }
            }
            #endregion

            #region Xml report
            if( rtConfig.OutputXmlReport )
            {
                string wsAssembledXmlFilename = rtConfig.outputPrefix + "-assembled.idpXML";
                outputStream = new StreamWriter( wsAssembledXmlFilename );
                ws.assemblePeptidesXmlToStream( outputStream );
                outputStream.Close();
            }
            #endregion
        }
    }
}