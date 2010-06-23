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
using System.Reflection;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;
using pwiz.CLI.proteome;

namespace IDPicker
{
    public class ProteomeDataFileSubset
    {
        public static void write (ProteomeDataFile pd, string filename, IList<string> accessionList)
        {
            StreamWriter file;
            try
            {
                file = new StreamWriter(filename);
            }
            catch (IOException e)
            {
                throw new IOException("unable to open \"" + filename + "\"", e);
            }

            ProteinList pl = pd.proteinList;
            for (int i = 0; i < pl.size(); ++i)
            {
                Protein p = pl.protein(i);
                if (accessionList.Contains(p.id))
                    file.Write(">" + p.id + " " + p.description + "\n" + p.sequence + "\n");
            }

            file.Close();
        }
    }

	public class ProteinDatabaseEntry : IComparable<ProteinDatabaseEntry>
	{
		public ProteinDatabaseEntry() {}
		public ProteinDatabaseEntry( string name ) { this.name = name; }

		public string		name;			// the protein's name
		public string		desc;			// a description of the protein
		public string		data;			// the protein's full amino acid sequence

		public int CompareTo( ProteinDatabaseEntry rhs ) { return name.CompareTo( rhs.name ); }
	};

	public class ProteinDatabase : List<ProteinDatabaseEntry>
	{
        public ProteinDatabase() { nameToIndex = new Map<string, int>(); }
        public ProteinDatabase( ProteinDatabase old ) : base( old ) { nameToIndex = new Map<string, int>(); }

        public bool Contains( string name )
        {
            return nameToIndex.Contains( name );
        }

        public ProteinDatabaseEntry this[string name]
        {
            get { return this[nameToIndex[name]]; }
        }

		public void readFASTA( string filename, UInt32 startIndex, UInt32 endIndex, string delimiter )
		{
			StreamReader file;
			try
			{
				file = new StreamReader( filename );
			} catch( IOException e )
			{
				throw new IOException( "unable to open \"" + filename + "\"", e );
			}

			UInt32 pIndex = 0;

			string buf = file.ReadLine();

			//Map<char, int> invalidResidueCount;

			while( !file.EndOfStream )
			{
				if( buf[0] == '>' ) // signifies a new protein record in a FASTA file
				{
					++pIndex;

					if( pIndex >= startIndex && pIndex <= endIndex )
					{
						ProteinDatabaseEntry p = new ProteinDatabaseEntry();
						string locusMetaData = buf;
						int locusEnd = locusMetaData.IndexOfAny( delimiter.ToCharArray() );
                        //Console.Write( locusMetaData );
                        if( locusEnd < 2 )
                        {
                            p.name = locusMetaData;
                            p.desc = "";
                        } else
                        {
                            p.name = locusMetaData.Substring( 1, locusEnd - 1 );
                            p.desc = locusMetaData.Substring( locusEnd + 1 );
                        }
						p.data = "";
						//nameToIndex[ p.name ] = size()-1;
						//indexToName[ size()-1 ] = p.name;

						buf = file.ReadLine();
						while( buf != null && buf[0] != '>' )
						{
							p.data += buf;
							buf = file.ReadLine();
						}

						p.data.Replace( "\r", "" );

						if( p.data.Length == 0 )
						{
							Console.Error.WriteLine( "Warning: protein \'" + p.name + "\' contains no residues." );
						} else
						{
                            nameToIndex[p.name] = Count;
                            Add( p );

							// Crop * from the end of the protein sequence, if necessary
							p.data.TrimEnd( "*".ToCharArray() );

							// Verify each residue is in the residue map
							//if( g_residueMap )
							//{
							//for( size_t i=0; i < p.data.length(); ++i )
							//	if( p.data[i] != 'X' && !g_residueMap->hasResidue( p.data[i] ) )
							//cerr << "Warning: protein \'" << p.name << "\' contains unknown residue \'" << p.data[i] << "\'" << endl;
							//		++invalidResidueCount[ p.data[i] ];
							//}
						}

						continue;
					}
				}

				buf = file.ReadLine();
			}
			file.Close();
			//for( map< char, int >::iterator itr = invalidResidueCount.begin(); itr != invalidResidueCount.end(); ++itr )
			//	cerr << "Warning: unknown residue \'" << itr->first << "\' appears " << itr->second << " times in database." << endl;
		}

		public void writeFASTA( string filename )
		{
			StreamWriter file;
			try
			{
				file = new StreamWriter( filename );
			} catch( IOException e )
			{
				throw new IOException( "unable to open \"" + filename + "\"", e );
			}

			foreach( ProteinDatabaseEntry p in this )
				file.Write( ">" + p.name + " " + p.desc + "\n" + p.data + "\n" );

			file.Close();
		}

        Map<string, int> nameToIndex;
	}

	public class Util
	{
        public static string Version { get { return GetAssemblyVersion( Assembly.GetExecutingAssembly().GetName() ); } }
        public static DateTime LastModified { get { return GetAssemblyLastModified( Assembly.GetExecutingAssembly().GetName() ); } }

        public static AssemblyName GetAssemblyByName( string assemblyName )
        {
            if( Assembly.GetCallingAssembly().GetName().FullName.Contains( assemblyName ) )
                return Assembly.GetCallingAssembly().GetName();

            foreach( AssemblyName a in Assembly.GetCallingAssembly().GetReferencedAssemblies() )
            {
                if( a.FullName.Contains( assemblyName ) )
                    return a;
            }
            return null;
        }

        public static string GetAssemblyVersion( AssemblyName assembly )
        {
            Match versionMatch = Regex.Match( assembly.ToString(), @"Version=([\d.]+)" );
            return versionMatch.Groups[1].Success ? versionMatch.Groups[1].Value : "unknown";
        }

        public static DateTime GetAssemblyLastModified( AssemblyName assembly )
        {
            return File.GetLastWriteTime( Assembly.ReflectionOnlyLoad( assembly.FullName ).Location );
        }


        public static string[] StringCollectionToStringArray (System.Collections.Specialized.StringCollection collection)
        {
            string[] output = new string[collection.Count];
            collection.CopyTo(output, 0);
            return output;
        }

        public static string[] ReplaceKeysWithValues (string[] input, KeyValuePair<string, string>[] kvPairs)
        {
            List<string> output = new List<string>();
            foreach (string str in input)
            {
                string outStr = str;
                foreach (KeyValuePair<string, string> kvp in kvPairs)
                    outStr = outStr.Replace(kvp.Key, kvp.Value);
                output.Add(outStr);
            }
            return output.ToArray();
        }

        public static string[] FindFileInSearchPath (string fileNameWithoutExtension,
                                                     string[] matchingFileExtensions,
                                                     string[] directoriesToSearch,
                                                     bool stopAtFirstMatch)
        {
            List<string> fileMatches = new List<string>();
            foreach (string searchPath in directoriesToSearch)
            {
                DirectoryInfo dir = new DirectoryInfo(searchPath);
                foreach (string ext in matchingFileExtensions)
                {
                    string queryPath = Path.Combine(dir.FullName, fileNameWithoutExtension + "." + ext);
                    if (File.Exists(queryPath))
                    {
                        fileMatches.Add(queryPath);
                        if (stopAtFirstMatch)
                            break;
                    }
                }

                if (stopAtFirstMatch && fileMatches.Count > 0)
                    break;
            }

            return fileMatches.ToArray();
        }

        public static string FindDatabaseInSearchPath (string databaseName, string rootInputDirectory)
        {
            databaseName = Path.GetFileNameWithoutExtension(databaseName);
            List<string> paths = new List<string>(StringCollectionToStringArray(Properties.Settings.Default.FastaPaths));
            KeyValuePair<string, string>[] replacePairs = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("<DatabaseDirectory>", databaseName),
                new KeyValuePair<string, string>("<RootInputDirectory>", rootInputDirectory)
            };
            paths = new List<string>(ReplaceKeysWithValues(paths.ToArray(), replacePairs));

            string[] extensions = new string[] { "fasta" };
            string[] matches = FindFileInSearchPath(databaseName, extensions, paths.ToArray(), true);

            if (matches.Length == 0)
                throw new Exception("Cannot find database file corresponding to \"" +
                                     databaseName + "\"\r\n\r\n" +
                                     "Check that this database file can be " +
                                     "found in the database search paths " +
                                     "(configured in Tools/Options) with a database file " +
                                     "extension (FASTA).");

            return matches[0];
        }

        public static string FindSourceInSearchPath (string source, string rootInputDirectory)
        {
            source = Path.GetFileNameWithoutExtension(source);
            List<string> paths = new List<string>(StringCollectionToStringArray(Properties.Settings.Default.SourcePaths));
            KeyValuePair<string, string>[] replacePairs = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("<SourceName>", source),
                new KeyValuePair<string, string>("<RootInputDirectory>", rootInputDirectory)
            };
            paths = new List<string>(ReplaceKeysWithValues(paths.ToArray(), replacePairs));

            string[] extensions = Properties.Settings.Default.SourceExtensions.Split(";".ToCharArray());
            string[] matches = FindFileInSearchPath(source, extensions, paths.ToArray(), true);

            if (matches.Length == 0)
                throw new Exception("Cannot find source file corresponding to \"" +
                                     source + "\"\r\n\r\n" +
                                     "Check that this source file can be " +
                                     "found in the source search paths " +
                                     "(configured in Tools/Options) with one of the " +
                                     "configured source file extensions:\r\n" +
                                     Properties.Settings.Default.SourceExtensions);

            return matches[0];
        }

        public static string FindSearchInSearchPath (string source, string rootInputDirectory)
        {
            source = Path.GetFileNameWithoutExtension(source);
            List<string> paths = new List<string>(StringCollectionToStringArray(Properties.Settings.Default.SearchPaths));
            KeyValuePair<string, string>[] replacePairs = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("<SourceName>", source),
                new KeyValuePair<string, string>("<RootInputDirectory>", rootInputDirectory)
            };
            paths = new List<string>(ReplaceKeysWithValues(paths.ToArray(), replacePairs));

            string[] extensions = new string[] { "pepXML", "idpXML" };
            string[] matches = FindFileInSearchPath(source, extensions, paths.ToArray(), true);

            if (matches.Length == 0)
                throw new Exception("Cannot find search file corresponding to \"" +
                                     source + "\"\r\n\r\n" +
                                     "Check that this search file can be " +
                                     "found in the search file search paths " +
                                     "(configured in Tools/Options) with a search file " +
                                     "extension (pepXML or idpXML).");

            return matches[0];
        }

		static public string GetFileType( string filepath )
		{
			StreamReader file;
			try
			{
				file = new StreamReader( filepath );
			} catch( IOException e )
			{
				return "error: " + e.Message;
			}

			string line1 = file.ReadLine();

			// Is this an XML file?
			if( line1.Contains( "<?xml" ) )
			{
				// Yes, so the first (root) element gives the type
				int rootElIdx = line1.IndexOf( '<', 1 );
				while( rootElIdx == -1 || line1[rootElIdx+1] == '?' || line1[rootElIdx+1] == '!' )
				{
					line1 = file.ReadLine();
					rootElIdx = line1.IndexOf( '<' );
				}
				string rootEl = line1.Substring( rootElIdx + 1, line1.IndexOfAny( " >".ToCharArray(), rootElIdx + 2 ) - rootElIdx - 1 );
				return rootEl.ToLower();
			}

			if( line1.IndexOf( "H\tSQTGenerator" ) == 0 )
				return "sqt";
			else if( line1.IndexOf( "H\tTagsGenerator" ) == 0 || line1.IndexOf( "GutenTag" ) >= 0 )
				return "tags";
			else if( line1.IndexOf( '>' ) == 0 )
				return "fasta";
			else
				return "unknown";
		}

		static public bool TestFileType( string filepath, string type, bool printErrorMsg )
		{
			string actualType = GetFileType( filepath );
			if( actualType != type )
			{
				if( printErrorMsg )
					Console.Error.WriteLine( "Error: expected \"" + type + "\" file; the type of \"" + filepath + "\" is \"" + actualType + "\"" );
				return false;
			}
			return true;
		}

		static public bool TestFileType( string filepath, string type )
		{
			return TestFileType( filepath, type, true );
		}

        static public int LongestCommonPrefix (IList<string> strings, out string sequence)
        {
            sequence = string.Empty;
            if (strings.Count == 0)
                return 0;

            sequence = strings.First();
            for (int i = 1; i < strings.Count; ++i)
                if (LongestCommonPrefix(sequence, strings[i], out sequence) == 0)
                    return 0;
            return sequence.Length;
        }

        static public int LongestCommonPrefix (string str1, string str2, out string sequence)
        {
            sequence = string.Empty;
            if (String.IsNullOrEmpty(str1) || String.IsNullOrEmpty(str2))
                return 0;

            StringBuilder sequenceBuilder = new StringBuilder();
            for (int i = 0; i < str1.Length && i < str2.Length && str1[i] == str2[i]; ++i)
                sequenceBuilder.Append(str1[i]);
            sequence = sequenceBuilder.ToString();
            return sequence.Length;
        }

        static public int LongestCommonSubstring (string str1, string str2, out string sequence)
		{
			sequence = string.Empty;
			if( String.IsNullOrEmpty( str1 ) || String.IsNullOrEmpty( str2 ) )
				return 0;

			int[,] num = new int[str1.Length, str2.Length];
			int maxlen = 0;
			int lastSubsBegin = 0;
			StringBuilder sequenceBuilder = new StringBuilder();

			for( int i = 0; i < str1.Length; i++ )
			{
				for( int j = 0; j < str2.Length; j++ )
				{
					if( str1[i] != str2[j] )
						num[i, j] = 0;
					else
					{
						if( ( i == 0 ) || ( j == 0 ) )
							num[i, j] = 1;
						else
							num[i, j] = 1 + num[i - 1, j - 1];

						if( num[i, j] > maxlen )
						{
							maxlen = num[i, j];
							int thisSubsBegin = i - num[i, j] + 1;
							if( lastSubsBegin == thisSubsBegin )
							{//if the current LCS is the same as the last time this block ran
								sequenceBuilder.Append( str1[i] );
							} else //this block resets the string builder if a different LCS is found
							{
								lastSubsBegin = thisSubsBegin;
								sequenceBuilder.Remove( 0, sequenceBuilder.Length );//clear it
								sequenceBuilder.Append( str1.Substring( lastSubsBegin, ( i + 1 ) - lastSubsBegin ) );
							}
						}
					}
				}
			}
			sequence = sequenceBuilder.ToString();
			return maxlen;
		}
	}
}
