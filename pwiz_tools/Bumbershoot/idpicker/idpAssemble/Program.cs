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
using System.Text.RegularExpressions;
//using System.Runtime.InteropServices;
using IDPicker;

namespace idpAssemble
{
    class RunTimeConfig : Workspace.RunTimeConfig
    {
        internal string DefaultConfigFilename = "idpAssemble.cfg";
        internal string DefaultBufferDelimiters = "\r\n#";
        internal string DefaultFileDelimiters = "\r\n\t ";

        internal bool SkipEmptyFilemasks = false;

        internal string outputFilepath;
        internal List<string> batchFilepathList = new List<string>();
    }
    /// <summary>
    /// This class is the main interface for the idpAssemble program. The class contains
    /// functions to read the command line, initialize the environment, read in the result
    /// files, assembing (or arranging) the results into groups, generating combined results, 
    /// and writing the combined results to an XML file.
    /// </summary>
    class Program
    {
        public static string Version { get { return Util.GetAssemblyVersion( System.Reflection.Assembly.GetExecutingAssembly().GetName() ); } }
        public static DateTime LastModified { get { return Util.GetAssemblyLastModified( System.Reflection.Assembly.GetExecutingAssembly().GetName() ); } }
        public static string IDPASSEMBLE_LICENSE = Workspace.LICENSE;

        public static RunTimeConfig rtConfig;


        /// <summary>
        /// A function to process the command line for the idpAssemble program
        /// </summary>
        /// <param name="args">A string representing the command line</param>
        /// <returns>Whether the command line parsing was successful or not</returns>
        static bool InitProcess( ref List<string> args )
        {
            if( args.Count < 3 )
            {
                Console.Error.WriteLine( "Not enough arguments.\nUsage: idpAssemble [-dump] [-cfg <config filepath>]" +
                                        " <output filepath>" +
                                        " [<source group name> <source filemask> [<another group name> <another source filemask>] ...]" +
                                        " [-b <filepath to list of group/filemask pairs, one per line>]" );
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
                } else if( args[i] == "-b" && i + 1 <= args.Count )
                {
                    // Read the text file that contains the file mask and group mappings
                    rtConfig.batchFilepathList.Add( args[i + 1] );
                    args.RemoveAt( i );
                    args.RemoveAt( i );
                    --i;
                }
            }

            if( !rtConfig.initialized() )
            {
                if( rtConfig.initializeFromFile( rtConfig.DefaultConfigFilename, rtConfig.DefaultFileDelimiters ) )
                {
                    Console.Error.WriteLine( "Could not find the default configuration file (hard-coded defaults in use)." );
                }
            }

            // Initialize the arguments
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

            if( ( rtConfig.batchFilepathList.Count == 0 && args.Count < 3 ) || ( args.Count < 1 ) )
            {
                Console.Error.WriteLine( "Not enough arguments.\nUsage: idpAssemble [-dump] [-cfg <config filepath>]" +
                                        " <output filepath>" +
                                        " [<source group name> <source filemask> [<another group name> <another source filemask>] ...]" +
                                        " [-b <filepath to list of group/filemask pairs, one per line>]" );
                return true;
            }

            rtConfig.outputFilepath = args[0];

            return false;
        }

        /// <summary>
        /// This function takes a filemask and the name of the group for the files matching
        /// the mask. The function reads the results from all the files matching the group
        /// and assigns them to the given group name.
        /// </summary>
        /// <param name="ws">IDPicker workspace (see <see cref="IDPicker.Workspace"/>).</param>
        /// <param name="sourceGroup">The name of the group</param>
        /// <param name="sourceFilemask">A filemask for all the idpXML files that contain 
        /// unmodifiedPeptide identification results which should be read and assigned to the given
        /// group</param>
        static void ReadFilemaskGroupPair( Workspace ws, string sourceGroup, string sourceFilemask )
        {
            // Get all the files matching the mask
            string path = Path.GetDirectoryName( sourceFilemask );
            if( path.Length < 1 )
            {
                path = "." + Path.DirectorySeparatorChar;
            }
            //Console.WriteLine( path + " : " + Path.GetFileName( args[i+1] ) );

            string[] fpFiles = Directory.GetFiles( path, Path.GetFileName( sourceFilemask ) );
            if( fpFiles.Length == 0 )
            {
                if( !rtConfig.SkipEmptyFilemasks )
                    throw new ArgumentException( "filemask \"" + sourceFilemask + "\" doesn't match to any files" );
                else
                    Console.Error.WriteLine( "Warning: filemask \"" + sourceFilemask + "\" doesn't match to any files" );
            }

            // Parse each file and assign the results in each file to the 
            // user-supplied source group.
            foreach( string fp in fpFiles )
            {
                if( !Util.TestFileType( fp, "idpickerpeptides" ) )
                    continue;
                Console.WriteLine( "Reading peptides from filepath: " + fp );
                StreamReader stream = new StreamReader( fp );
                Console.WriteLine( "Parsing XML..." );
                // Read the results
                ws.readPeptidesXml( stream, sourceGroup, rtConfig.MaxFDR, rtConfig.MaxResultRank );
                stream.Close();
                Console.WriteLine( "\nFinished parsing." );
            }
        }

        static void recurse( SourceGroupInfo root )
        {
            Console.WriteLine( root.getGroupName() );
            foreach( SourceGroupInfo child in root.getChildGroups().Values )
                recurse( child );
        }


        static void Main( string[] args )
        {
            //args = new string[]{"combined.xml" , "/1", "H:\\home\\dasaris\\data\\20080827_Beth_Locken\\new_compiled\\sh_1338_BL_081508p_04_H2_EDB_3.idpXML"};
            // Write the program header to the console 
            Console.WriteLine( "IDPickerAssemble " + Version + " (" + LastModified.ToShortDateString() + ")" );
            Console.WriteLine( "IDPickerWorkspace " + Workspace.Version + " (" + Workspace.LastModified.ToShortDateString() + ")" );
            Console.WriteLine( IDPASSEMBLE_LICENSE );

            // Read command line and process
            List<string> argsList = new List<string>( args );
            if( InitProcess( ref argsList ) )
                return;

            // Generate a new processing event for IDPAssemble
            ProcessingEvent assembleEvent = new ProcessingEvent();
            assembleEvent.type = "assembly/validation";
            assembleEvent.startTime = DateTime.Now;
            ProcessingParam assembleParam = new ProcessingParam();
            assembleParam.name = "software name";
            assembleParam.value = "idpAssemble";
            assembleEvent.parameters.Add( assembleParam );
            assembleParam = new ProcessingParam();
            assembleParam.name = "software version";
            assembleParam.value = Version + " (" + LastModified + ")";
            assembleEvent.parameters.Add( assembleParam );
            RunTimeVariableMap assembleVars = rtConfig.getVariables();
            foreach( KeyValuePair<string, string> itr in assembleVars )
            {
                assembleParam = new ProcessingParam();
                assembleParam.name = itr.Key;
                assembleParam.value = itr.Value;
                assembleEvent.parameters.Add( assembleParam );
            }

            // Init the workspace
            Console.Write( "Initializing workspace..." );
            Workspace ws = new Workspace( rtConfig );
            ws.setStatusOutput( Console.Out );
            Console.WriteLine( " done." );

            long start = DateTime.Now.Ticks;

            try
            {
                // If the user has supplied a text file with a bunch of file masks and
                // their group names.
                foreach( string batchFilepath in rtConfig.batchFilepathList )
                {

                    // For each file mask in the the file.
                    StreamReader stream = new StreamReader( batchFilepath );
                    string line;
                    while( ( line = stream.ReadLine() ) != null )
                    {
                        if( line.Length == 0 )
                            continue;
                        Regex groupFilemaskPair = new Regex( "((\"(.+)\")|(\\S+))\\s+((\"(.+)\")|(\\S+))" );
                        Match lineMatch = groupFilemaskPair.Match( line );
                        string group = lineMatch.Groups[3].ToString() + lineMatch.Groups[4].ToString();
                        string filepath = lineMatch.Groups[7].ToString() + lineMatch.Groups[8].ToString();

                        try
                        {
                            // Read all the files matching the mask as a single group.
                            ReadFilemaskGroupPair( ws, group, filepath );
                        } catch( Exception e )
                        {
                            Console.Error.WriteLine( "Caught fatal exception while parsing \"" + filepath + "\": " + e.Message );
                            System.Diagnostics.Process.GetCurrentProcess().Kill();
                        }
                    }
                }

                // If the user has supplied the group name and file mask on the command line.
                for( int i = 1; i < argsList.Count; i += 2 )
                {
                    // Read files matching the file mask and assign them to the group
                    // name supplied by the user.
                    try
                    {
                        ReadFilemaskGroupPair( ws, argsList[i], argsList[i + 1] );
                    } catch( Exception e )
                    {
                        Console.Error.WriteLine( "Caught fatal exception while parsing \"" + argsList[i + 1] + "\": " + e.Message );
                        System.Diagnostics.Process.GetCurrentProcess().Kill();
                    }
                }
            } catch( Exception e )
            {
                Console.Error.WriteLine( "Caught fatal exception: " + e.Message );
                //Console.Error.Write( Environment.StackTrace );
                System.Diagnostics.Process.GetCurrentProcess().Kill();
            }
            Console.WriteLine( "Finished reading input; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." );

            if( ws.groups.Count == 0 )
            {
                Console.Error.WriteLine( "Error: no input and no assembly possible." );
                System.Diagnostics.Process.GetCurrentProcess().Kill();
            }

            // Arrange the sources into hierarchial groups supplied by the user
            ws.assembleSourceGroups();
            assembleEvent.endTime = DateTime.Now;
            foreach( SourceInfo source in ws.groups["/"].getSources( true ) )
                source.processingEvents.Add( assembleEvent );

            // Generate an XML document with combined results from all the
            // idpXML result files. The combined XML file preserves all the
            // source groupings of the result files, and also the unmodifiedPeptide to
            // protein and unmodifiedPeptide to spectra relationships.
            try
            {
                Console.WriteLine( "Writing assembled IDPicker XML to filepath: " + rtConfig.outputFilepath );
                StreamWriter outputStream = new StreamWriter( rtConfig.outputFilepath );
                ws.assemblePeptidesXmlToStream( outputStream );
                outputStream.Close();
            } catch( Exception e )
            {
                Console.Error.WriteLine( "Caught fatal exception: " + e.Message );
                System.Diagnostics.Process.GetCurrentProcess().Kill();
            }
        }
    }
}
