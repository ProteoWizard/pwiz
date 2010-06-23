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
using System.Diagnostics;
using System.IO;
using IDPicker;

namespace generateSubsetFasta
{
	class RunTimeConfig : Workspace.RunTimeConfig
	{
		internal string DefaultConfigFilename = "generateSubsetFasta.cfg";
		internal string DefaultBufferDelimiters = "\r\n#";
		internal string DefaultFileDelimiters = "\r\n\t ";

		public bool MostParsimoniousAnalysis = true;
		public int MinAdditionalPeptides = 1;
		public bool ModsAreDistinctByDefault = false;
		public string DistinctModsOverride = "";
		public string IndistinctModsOverride = "";

		internal string outputDir;
		internal string outputPrefix;

	}

	class Program
	{
        public static string Version { get { return Util.GetAssemblyVersion( System.Reflection.Assembly.GetExecutingAssembly().GetName() ); } }
        public static DateTime LastModified { get { return Util.GetAssemblyLastModified( System.Reflection.Assembly.GetExecutingAssembly().GetName() ); } }
        public static string GENERATESUBSETFASTA_LICENSE = Workspace.LICENSE;

		public static RunTimeConfig rtConfig;

		static bool InitProcess( ref List<string> args )
		{
			if( args.Count < 2 )
			{
				Console.Error.WriteLine( "Not enough arguments.\nUsage: generateSubsetFasta [-dump] [-cfg <config filepath>]" +
										" <output name> <input peptides XML filemask> [<another filemask> ...]" );
				return true;
			}

			rtConfig = new RunTimeConfig();

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

			if( args.Count < 2 )
			{
				Console.Error.WriteLine( "Not enough arguments.\nUsage: generateSubsetFasta [-dump] [-cfg <config filepath>]" +
										" <output name> <input peptides XML filemask> [<another filemask> ...]" );
				return true;
			}

			rtConfig.outputDir = args[0];
			string[] outputPathArray = rtConfig.outputDir.Split( Path.DirectorySeparatorChar );
			rtConfig.outputPrefix = outputPathArray[outputPathArray.Length - 1];

			return false;
		}

		static void Main( string[] args )
		{
            Console.WriteLine( "GenerateSubsetFASTA " + Version + " (" + LastModified.ToShortDateString() + ")" );
            Console.WriteLine( "IDPickerWorkspace " + Workspace.Version + " (" + Workspace.LastModified.ToShortDateString() + ")" );
			Console.WriteLine( GENERATESUBSETFASTA_LICENSE );

			List<string> argsList = new List<string>( args );

			if( InitProcess( ref argsList ) )
				return;

			long start;
			Workspace ws = new Workspace( rtConfig );
			ws.setStatusOutput( Console.Out );

			// Initialize the modifications that can and can not appear in distinct peptides
			ws.distinctPeptideSettings = new Workspace.DistinctPeptideSettings(
				rtConfig.ModsAreDistinctByDefault, rtConfig.DistinctModsOverride, rtConfig.IndistinctModsOverride );

			for( int i = 1; i < argsList.Count; ++i )
			{
				try
				{
					List<string> inputFilepaths = new List<string>();
					string path = Path.GetDirectoryName( argsList[i] );
					if( path.Length < 1 )
						path = "." + Path.DirectorySeparatorChar;
					//Console.WriteLine( path + " : " + Path.GetFileName( args[i] ) );
					inputFilepaths.AddRange( Directory.GetFiles( path, Path.GetFileName( argsList[i] ) ) );

					foreach( string inputFilepath in inputFilepaths )
					{
						try
						{
							if( !Util.TestFileType( inputFilepath, "idpickerpeptides" ) )
								continue;
							start = DateTime.Now.Ticks;
							Console.WriteLine( "Reading and parsing peptides XML from filepath: " + inputFilepath );
							StreamReader inputStream = new StreamReader( inputFilepath );
							ws.readPeptidesXml( inputStream, "", rtConfig.MaxFDR, rtConfig.MaxResultRank );
							inputStream.Close();
							Console.WriteLine( "\nFinished reading and parsing peptides XML; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." );
						} catch( Exception e )
						{
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

			if( ws.groups.Counts() == 0 )
			{
				Console.Error.WriteLine( "Error: no input files read; nothing to report." );
				return;
			}

			// Remove peptides that are shorter than user specified length
			start = DateTime.Now.Ticks;
			Console.WriteLine( "Filtering out peptides shorter than " + rtConfig.MinPeptideLength + " residues..." );
			ws.filterByMinimumPeptideLength( rtConfig.MinPeptideLength );
			Console.WriteLine( "\nFinished filtering by minimum peptide length; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." ); { ws.validate( rtConfig.MaxFDR, 0, 100 ); }
			// Remove peptides by result ambiguity
			start = DateTime.Now.Ticks;
			Console.WriteLine( "Filtering out results with more than " + rtConfig.MaxAmbiguousIds + " ambiguous ids..." );
			ws.filterByResultAmbiguity( rtConfig.MaxAmbiguousIds );
			Console.WriteLine( "\nFinished filtering by maximum ambiguous ids; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." );
			//ws.validate();
			// Remove proteins that doesn't have a minimum number of distinct peptides
			start = DateTime.Now.Ticks;
			Console.WriteLine( "Filtering out proteins with less than " + rtConfig.MinDistinctPeptides + " distinct peptides..." );
			ws.filterByDistinctPeptides( rtConfig.MinDistinctPeptides );
			Console.WriteLine( "\nFinished filtering by minimum distinct peptides; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." );
			//ws.validate();
			start = DateTime.Now.Ticks;
			Console.WriteLine( "Assembling protein groups..." );
			ws.assembleProteinGroups();
			Console.WriteLine( "\nFinished assembling protein groups; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." );
			//ws.validate();
			start = DateTime.Now.Ticks;
			Console.WriteLine( "Assembling peptide groups..." );
			ws.assemblePeptideGroups();
			Console.WriteLine( "\nFinished assembling peptide groups; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." );
			//ws.validate();
			start = DateTime.Now.Ticks;
			Console.WriteLine( "Assembling clusters..." );
			ws.assembleClusters();
			Console.WriteLine( "\nFinished assembling clusters; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." );
			//ws.validate();
			start = DateTime.Now.Ticks;
			Console.WriteLine( "Assembling minimum covering set for clusters..." );
			int clusterCount = 0;
			foreach( ClusterInfo c in ws.clusters )
			{
				++clusterCount;
				Console.Write( "Assembling minimum covering set for cluster " + clusterCount + " of " + ws.clusters.Count + " (" + c.proteinGroups.Counts() + " protein groups, " + c.results.Counts() + " results)          \r" );
				ws.assembleMinimumCoveringSet( c );
			}
			Console.WriteLine( "\nFinished assembling minimum covering sets; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." );
			//ws.validate();
			/*if( rtConfig.MostParsimoniousAnalysis )
			{
				start = DateTime.Now.Ticks;
				Console.WriteLine( "Filtering workspace by minimum covering set..." );
				ws.filterByMinimumCoveringSet();
				Console.WriteLine( "\nFinished filtering by minimum covering set; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." );
				//ws.validate();
			}*/

			if( rtConfig.MinAdditionalPeptides > 0 )
			{
				// Remove protein clusters that do not have
				// required number of unique peptides
				start = DateTime.Now.Ticks;
				Console.WriteLine( "Filtering workspace by minimum covering set..." );
				ws.filterByMinimumCoveringSet( rtConfig.MinAdditionalPeptides );
				Console.WriteLine( "\nFinished filtering by minimum covering set; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." ); { ws.validate( rtConfig.MaxFDR, rtConfig.MinDistinctPeptides, rtConfig.MaxAmbiguousIds ); }
			}

			start = DateTime.Now.Ticks;
			Console.Write( "Assembling source groups..." );
			ws.assembleSourceGroups();
			Console.WriteLine( " finished; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." );

			try
			{
				ws.validate( rtConfig.MaxFDR, rtConfig.MinDistinctPeptides, rtConfig.MaxAmbiguousIds );
			} catch( Exception e )
			{
				Console.Error.WriteLine( "Error while validating workspace: " + e.Message );
				return;
			}


			string inputFastaFilepath = ws.dbFilepath;
			if( !File.Exists( inputFastaFilepath ) )
			{
				Console.WriteLine( "Unable to open FASTA database used by IDPicker workspace: \"" + inputFastaFilepath + "\"" );
				Console.Write( "Type a valid path to the FASTA database and press <enter>: " );
				inputFastaFilepath = Console.ReadLine();
				while( !File.Exists( inputFastaFilepath ) )
				{
					Console.WriteLine( "Unable to open FASTA database: \"" + inputFastaFilepath + "\"" );
					Console.Write( "Type a valid path to the FASTA database and press <enter>: " );
					inputFastaFilepath = Console.ReadLine();
				}
			}

			start = DateTime.Now.Ticks;
			Console.Write( "Reading input FASTA database..." );
			ProteinDatabase inputFasta = new ProteinDatabase();
			inputFasta.readFASTA( inputFastaFilepath, 0, (uint) 1 << 31, " " );
			Console.WriteLine( " finished; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." );

			start = DateTime.Now.Ticks;
			Console.Write( "Generating subset FASTA database..." );
			ProteinDatabase outputFasta = new ProteinDatabase();
			foreach( ProteinDatabaseEntry p in inputFasta )
				if( ws.proteins.Contains( p.name ) )
					outputFasta.Add( p );
			Console.WriteLine( " finished; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." );

			string outputFastaName = rtConfig.outputPrefix + ".fasta";
			start = DateTime.Now.Ticks;
			Console.Write( "Writing subset FASTA database \"" + outputFastaName + "\"..." );
			outputFasta.writeFASTA( outputFastaName );
			Console.WriteLine( " finished; " + new TimeSpan( DateTime.Now.Ticks - start ).TotalSeconds + " seconds elapsed." );
		}
	}
}
