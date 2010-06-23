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
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Xml;
using System.Text.RegularExpressions;
using System.Net;
using System.Reflection;
using System.ComponentModel;
using SourceList = System.Collections.Generic.Set<IDPicker.SourceInfo>;

namespace IDPicker
{
    /// <summary>
    /// ModificationSpecificity stores the site (amino acid), position,
    /// and classification of a modification (post-translational, 
    /// co-translational etc.) from unimod schema.
    /// </summary>
    public struct ModificationSpecificity
    {
        public string aminoAcid, position, classification;

        /**
        ModificationSpecificity constructor assigns the
        amino acid, position and classification of a modification.
        */

        /// <summary>
        /// ModificationSpecificity constructor assigns the amino acid, position and 
        /// classification of a modification.
        /// </summary>
        public ModificationSpecificity( string aa, string pos, string cls )
        {
            if( aa.CompareTo( "N-term" ) == 0 || aa.CompareTo( "n-term" ) == 0 )
            {
                aminoAcid = "n";
            } else if( aa.CompareTo( "C-term" ) == 0 || aa.CompareTo( "c-term" ) == 0 )
            {
                aminoAcid = "c";
            } else
            {
                aminoAcid = aa;
            }
            position = pos;
            classification = cls;
        }

        // Check to see if the mod can go here.
        public bool isCandidate( ModInfo mod )
        {
            if( mod.residue.CompareTo( aminoAcid.ToCharArray()[0] ) == 0 )
                return true;
            else if( mod.position == 'n' && aminoAcid == "n" )
                return true;
            else if( mod.position == 'c' && aminoAcid == "c" )
                return true;

            return false;
        }

        public override string ToString()
        {
            return aminoAcid + "," + position + "," + classification;
        }
    }

    /// <summary>
    /// ResidueMaps class defines three types of maps
    /// 1) aminoAcidToMass maps the single AA code to the mono-isotopic mass of the amino acid.
    /// 2) massToAminoAcid maps the mono-isotopic mass of the amino acid to the AA code.
    /// 3) codeMapping maps the single AA code to the three letter amino acid code.
    /// </summary>
    public class ResidueMaps
    {
        // Map the single letter AA code to its mono-isotopic mass
        public Map<char, float> aminoAcidToMass = new Map<char, float>();
        // Map the monoisotopic mass to the single letter AA code
        public Map<float, char> massToAminoAcid = new Map<float, char>();
        // Map the single letter AA code to the three letter AA name
        public Dictionary<char, string> codeMapping = new Dictionary<char, string>();

        public ResidueMaps()
        {
            aminoAcidToMass.Add( 'G', 57.021464f ); massToAminoAcid.Add( 57.021464f, 'G' ); codeMapping.Add( 'G', "Gly" );
            aminoAcidToMass.Add( 'A', 71.037114f ); massToAminoAcid.Add( 71.037114f, 'A' ); codeMapping.Add( 'A', "Ala" );
            aminoAcidToMass.Add( 'S', 87.032029f ); massToAminoAcid.Add( 87.032029f, 'S' ); codeMapping.Add( 'S', "Ser" );
            aminoAcidToMass.Add( 'P', 97.052764f ); massToAminoAcid.Add( 97.052764f, 'P' ); codeMapping.Add( 'P', "Pro" );
            aminoAcidToMass.Add( 'V', 99.068414f ); massToAminoAcid.Add( 99.068414f, 'V' ); codeMapping.Add( 'V', "Val" );
            aminoAcidToMass.Add( 'T', 101.04768f ); massToAminoAcid.Add( 101.04768f, 'T' ); codeMapping.Add( 'T', "Thr" );
            aminoAcidToMass.Add( 'C', 103.00919f ); massToAminoAcid.Add( 103.00919f, 'C' ); codeMapping.Add( 'C', "Cys" );
            aminoAcidToMass.Add( 'L', 113.08406f ); massToAminoAcid.Add( 113.08406f, 'L' ); codeMapping.Add( 'L', "Leu" );
            aminoAcidToMass.Add( 'I', 113.08406f ); massToAminoAcid.Add( 113.084065f, 'I' ); codeMapping.Add( 'I', "Ile" );
            aminoAcidToMass.Add( 'N', 114.04293f ); massToAminoAcid.Add( 114.04293f, 'N' ); codeMapping.Add( 'N', "Asn" );
            aminoAcidToMass.Add( 'D', 115.02694f ); massToAminoAcid.Add( 115.02694f, 'D' ); codeMapping.Add( 'D', "Asp" );
            aminoAcidToMass.Add( 'Q', 128.05858f ); massToAminoAcid.Add( 128.05858f, 'Q' ); codeMapping.Add( 'Q', "Gln" );
            aminoAcidToMass.Add( 'K', 128.09496f ); massToAminoAcid.Add( 128.09496f, 'K' ); codeMapping.Add( 'K', "Lys" );
            aminoAcidToMass.Add( 'E', 129.04259f ); massToAminoAcid.Add( 129.04259f, 'E' ); codeMapping.Add( 'E', "Glu" );
            aminoAcidToMass.Add( 'M', 131.04048f ); massToAminoAcid.Add( 131.04048f, 'M' ); codeMapping.Add( 'M', "Met" );
            aminoAcidToMass.Add( 'H', 137.05891f ); massToAminoAcid.Add( 137.05891f, 'H' ); codeMapping.Add( 'H', "His" );
            aminoAcidToMass.Add( 'F', 147.06841f ); massToAminoAcid.Add( 147.06841f, 'F' ); codeMapping.Add( 'F', "Phe" );
            aminoAcidToMass.Add( 'R', 156.10111f ); massToAminoAcid.Add( 156.10111f, 'R' ); codeMapping.Add( 'R', "Arg" );
            aminoAcidToMass.Add( 'Y', 163.06333f ); massToAminoAcid.Add( 163.06333f, 'Y' ); codeMapping.Add( 'Y', "Tyr" );
            aminoAcidToMass.Add( 'W', 186.07931f ); massToAminoAcid.Add( 186.07931f, 'W' ); codeMapping.Add( 'W', "Trp" );
        }
    }


    /// <summary>
    /// class UnimodModification holds the title, full name, mono-isotopic mas,
    /// average mass, elemental comosition and specificity of a modification
    /// present in the unimod database. 
    /// See http://www.unimod.org/xmlns/schema/unimod_2/unimod_2.xsd for details 
    /// of the schema.
    /// </summary>
    public class UnimodModification
    {

        // Title and full name of the modification
        public string title, fullName;
        // Mass of the modification
        public float monoIsotopicMass, averageMass;
        // Elemental composition of the modification
        public string composition;
        // A List of residue specificities of the modification
        public List<ModificationSpecificity> specificities;

        //Constructor and desctructors
        public UnimodModification()
        {
        }

        public UnimodModification( string titl, string name )
        {
            title = titl;
            fullName = name;
            specificities = new List<ModificationSpecificity>();
        }
        /// <summary
        /// Function addASpecificity adds a residue specificity to a modification. 
        /// For example, an Oxidation modification can have a specificity of M at 
        /// any-where. The following types of modification specificities are allowed:
        ///     1) Post-translational
        ///     2) Artefactual
        ///     3) Pre-translational
        ///     4) Co-translational
        ///     5) Amino acid substitution
        ///     6) Multiple causes
        ///     7) Chemical derivatives
        /// </summary>
        public void addASpecificity( string aminoAcid, string pos, string cls )
        {
            if( cls.CompareTo( "Post-translational" ) == 0 || cls.CompareTo( "Artefact" ) == 0
                || cls.CompareTo( "Pre-translational" ) == 0 || cls.CompareTo( "Co-translational" ) == 0
                || cls.CompareTo( "Multiple" ) == 0 || cls.CompareTo( "AA substitution" ) == 0
                || cls.CompareTo( "Chemical derivative" ) == 0 )
            {
                specificities.Add( new ModificationSpecificity( aminoAcid, pos, cls ) );
            }
        }

        /// Sets the elemental composition
        public void setComposition( string comp )
        {
            composition = comp;
        }

        /// Sets the mono isotopic and average masses for modification
        public void setModificationMasses( float monoMass, float avgMass )
        {
            monoIsotopicMass = monoMass;
            averageMass = avgMass;
        }

        /// Get the total number of modification sites for a particular
        /// modification
        public int getTotalNumberOfModSites()
        {
            return specificities.Capacity;
        }

        // Get the average mass
        public float getAverageMass()
        {
            return averageMass;
        }

        // Get the monoisotopic mass
        float getMonoisotopicMass()
        {
            return monoIsotopicMass;
        }

        // Get the modification specificities.
        public List<ModificationSpecificity> getAminoAcidSpecificities()
        {
            return specificities;
        }

        public bool isMatching( ModInfo mod )
        {
            foreach( ModificationSpecificity spec in specificities )
            {
                if( spec.isCandidate( mod ) )
                {
                    return true;
                }
            }
            return false;
        }

        public string getTitle()
        {
            return title;
        }

        public override string ToString()
        {
            StringBuilder retStr = new StringBuilder();
            retStr.Append( title + "->" + fullName + "\n" );
            retStr.Append( "\t" + monoIsotopicMass + "," + averageMass + "\n" );
            retStr.Append( "\t" + composition + "\n" );
            List<ModificationSpecificity>.Enumerator mod = specificities.GetEnumerator();

            while( mod.MoveNext() )
            {
                retStr.Append( "\t" + mod.Current.ToString() );
            }
            return retStr.ToString();
        }
    }

    /// <summary>
    /// ImportSpectraExport maps all peptide interpretations with peptide scores. This map
    /// is used to reconcile the mutated/modified spectra of a subset database search to the 
    /// unmodified spectra from a parent database search.
    /// </summary>
    public class ImportSpectraExport
    {
        public Dictionary<string, string> peptideInterpretations;
        public Dictionary<string, float[]> peptideScores;
        public string[] scoreNames;

        public ImportSpectraExport( string filename, string[] sNames )
        {
            try
            {
                scoreNames = sNames;
                // Column delimiter
                char[] delimiter = new char[] { ',' };
                StreamReader reader = new StreamReader( filename );
                // Read the header
                string header = reader.ReadLine();
                // Various column indices
                int spectrumCol = -1;
                int nativeIDCol = -1;
                int chargeCol = -1;
                int peptideCol = -1;
                int modsCol = -1;
                int searchScoresCol = -1;
                int maxColNumber = -1;

                // Split the column header and locate the indices for
                // various columns of interest.
                string[] columnNames = header.Split( delimiter );
                for( int index = 0; index < columnNames.Length; ++index )
                {
                    if( String.Compare( columnNames[index], "Source", true ) == 0 )
                        spectrumCol = index;
                    else if( String.Compare( columnNames[index], "Native ID", true ) == 0 )
                        nativeIDCol = index;
                    else if( String.Compare( columnNames[index], "Charge", true ) == 0 )
                        chargeCol = index;
                    else if( String.Compare( columnNames[index], "Peptide", true ) == 0 )
                        peptideCol = index;
                    else if( String.Compare( columnNames[index], "Mods", true ) == 0 )
                        modsCol = index;
                    else if( String.Compare( columnNames[index], "Search Scores", true ) == 0 )
                        searchScoresCol = index;
                }
                // Check the sanity of the file.
                if( spectrumCol == -1 || nativeIDCol == -1 || chargeCol == -1 || peptideCol == -1 || modsCol == -1 || searchScoresCol == -1 )
                    return;
                // Determine the maximum column index.
                maxColNumber = Math.Max( spectrumCol, nativeIDCol );
                maxColNumber = Math.Max( maxColNumber, chargeCol );
                maxColNumber = Math.Max( maxColNumber, peptideCol );
                maxColNumber = Math.Max( maxColNumber, modsCol );
                maxColNumber = Math.Max( maxColNumber, searchScoresCol );

                //Initialize the data structures
                peptideInterpretations = new Dictionary<string, string>();
                peptideScores = new Dictionary<string, float[]>();

                // Get the input line
                string inputLine = reader.ReadLine();
                while( inputLine != null )
                {
                    // Split the columns
                    string[] columns = inputLine.Split( delimiter );
                    // Check the sanity again
                    if( columns.Length < maxColNumber )
                        return;
                    // Split the spectrum name and generate the spectrum name
                    string[] spectrumCols = columns[spectrumCol].Split( new char[] { '/' } );
                    string spectrumName = spectrumCols[spectrumCols.Length - 1] + "." + columns[nativeIDCol] + "." + columns[chargeCol];
                    // Get the modification column
                    string mods = "";
                    if( columns[modsCol].Length > 0 )
                        mods = "(" + columns[modsCol] + ")";
                    // Add the spectrum,interpretation pair to the dictionary
                    peptideInterpretations.Add( spectrumName, columns[peptideCol] + mods );
                    float[] extractedScores = new float[scoreNames.Length];
                    // Extract the scores
                    for( int index = 0; index < scoreNames.Length; ++index )
                    {
                        string pattern = @"" + scoreNames[index] + @" (\d+\.\d+)";
                        Match match = Regex.Match( columns[searchScoresCol], pattern, RegexOptions.IgnoreCase );
                        if( match.Success )
                        {
                            extractedScores[index] = (float) Convert.ToDouble( match.Groups[1].Value );
                        }
                    }
                    // Map the scores to the spectrum
                    peptideScores.Add( spectrumName, extractedScores );
                    inputLine = reader.ReadLine();
                }
            } catch( Exception e )
            {
                Console.Error.WriteLine( e.StackTrace );
                Console.Error.WriteLine( e.Message );
                Console.Error.WriteLine( "Error parsing the spectra export table:" + filename );
            }
        }

        /// <summary>
        /// This function takes a set of spectra and the best search scores for the set. It attempts to 
        /// find the best scores from a previous search, which are taken in as an ImportSpectraExport
        /// object.
        /// </summary>
        /// <param name="spectra">Set of spectra</param>
        /// <param name="searchScores">Best search scores</param>
        /// <returns>Best search scores from a previous search (taken in as ImportSpectraExport object)</returns>
        public string getBestInterpretation( List<string> spectra, float[] searchScores )
        {
            string bestSpectrum = "";
            for( int i = 0; i < searchScores.Length; ++i )
                searchScores[i] = -1.0f;
            // For each spectrum
            for( int i = 0; i < spectra.Count; ++i )
            {
                if( !peptideScores.ContainsKey( spectra[i] ) )
                    continue;
                // Find the alternate scores from a previous search
                float[] scores = peptideScores[spectra[i]];
                int bitCompare = 0;
                // Compare them to the current best scores
                if( scores != null && scores.Length == searchScores.Length )
                {
                    for( int j = 0; j < scores.Length; ++j )
                    {
                        if( scores[j] >= searchScores[j] )
                            ++bitCompare;
                    }
                }
                // If the scores are higher, then remember them
                if( bitCompare == scores.Length )
                {
                    bestSpectrum = spectra[i];
                    scores.CopyTo( searchScores, 0 );
                }
            }
            StringBuilder scoreStr = new StringBuilder();
            for( int i = 0; i < searchScores.Length; ++i )
            {
                if( i > 0 )
                    scoreStr.Append( " " );
                scoreStr.Append( searchScores[i] );
            }
            // Return the best scores and the peptide interpretation that matched with
            // the best scores
            if( bestSpectrum != string.Empty )
                return peptideInterpretations[bestSpectrum] + " " + scoreStr.ToString();
            return "";
        }

        public override string ToString()
        {
            StringBuilder retStr = new StringBuilder();

            Dictionary<string, string>.Enumerator iter = peptideInterpretations.GetEnumerator();
            while( iter.MoveNext() )
            {
                retStr.Append( iter.Current.Key + "," + iter.Current.Value );
                float[] scores = peptideScores[iter.Current.Key];
                for( int index = 0; index < scores.Length; ++index )
                {
                    if( index > 0 )
                        retStr.Append( " " );
                    retStr.Append( scores[index] );
                }
                retStr.Append( "\n" );
            }
            return retStr.ToString();
        }
    }

    /// <summary>
    /// ProCanVar maps the SNPs found in the Cancer Protein Variation database to their
    /// protein sequences, and protein accessions. This information is used to annotate
    /// mutations found in a dataset by a TagRecon search.
    /// </summary>
    public class ProCanVar
    {
        /// <summary>
        /// A map that stores the ensembl protein sequences indexed by
        /// their accessions
        /// </summary>
        public Dictionary<string, string> EnsemblProteinSequences;
        /// <summary>
        /// A map that stores the IPI acessions and their corresponding
        /// ENSEMBL accessions
        /// </summary>
        public Dictionary<string, Set<string>> IPI2EnsemblMap;
        public Dictionary<string, SNPMetaDataGenerator.SNPMetaData> snpMap;

        /// <summary>
        /// This constructor reads a FASTA formatted CanProVar database.
        /// </summary>
        /// <param name="fastaFile">A fasta formmatted CanProVar database file</param>
        /// <param name="accessionMap">A txt file that maps the CanProVar protein accessions to IPI accessions</param>
        public ProCanVar( string fastaFile, string accessionMap )
        {
            EnsemblProteinSequences = new Dictionary<string, string>();
            IPI2EnsemblMap = new Dictionary<string, Set<string>>();
            snpMap = new Dictionary<string, SNPMetaDataGenerator.SNPMetaData>();
            try
            {
                // Read the fasta file
                StreamReader reader = new StreamReader( fastaFile );
                string annotation = null;
                StringBuilder proteinSequence = null;
                string inputLine = reader.ReadLine();
                while( inputLine != null )
                {
                    inputLine = inputLine.Trim();
                    // Reading the annotation line
                    if( inputLine.StartsWith( ">" ) )
                    {
                        // If we parsed out a protein entry
                        if( annotation != null )
                        {
                            // Parse out the accession out of the annotation part
                            string[] toks = annotation.Split( new char[] { '\t' } );
                            // If the protein has no SNP annotations then do not store it.
                            if( toks.Length < 2 )
                            {
                                annotation = null;
                                proteinSequence = null;
                                inputLine = reader.ReadLine();
                                continue;
                            }
                            // Split the annotation as accession<tab>snps
                            string accession = toks[0];
                            string snps = toks[1];
                            //Trim the fasta annotation header start character.
                            accession = accession.TrimStart( new char[] { '>' } );
                            // Store the sequence
                            EnsemblProteinSequences.Add( accession, proteinSequence.ToString() );
                            SNPMetaDataGenerator.SNPMetaData snpData = new SNPMetaDataGenerator.SNPMetaData( accession );
                            this.parseSNPs( snps, snpData );
                            if( snpData.knownMutations.Count > 0 )
                            {
                                snpMap.Add( accession, snpData );
                            }
                        }
                        annotation = inputLine;
                        proteinSequence = new StringBuilder();
                    } else
                    {
                        // If the entry is legit then parse out the sequence
                        if( annotation != null && proteinSequence != null )
                        {
                            proteinSequence.Append( inputLine );
                        }
                    }
                    inputLine = reader.ReadLine();
                }
            } catch( Exception e )
            {
                Console.Error.WriteLine( e.StackTrace );
                Console.Error.WriteLine( e.Source );
                Console.Error.WriteLine( "Error parsing the FASTA file: " + fastaFile );
            }

            // This section reads an IPI -> Ensembl protein map.
            try
            {
                StreamReader reader = new StreamReader( accessionMap );
                string inputLine = reader.ReadLine();
                while( inputLine != null )
                {
                    inputLine = inputLine.Trim();
                    // Skip the header
                    if( !inputLine.StartsWith( "#" ) )
                    {
                        // Split the line with help of a tab and store the map.
                        string[] toks = inputLine.Split( new char[] { '\t' } );
                        // If there is no associated IPI accession to the ENSP 
                        // accession then skip the entry
                        if( toks.Length < 2 )
                        {
                            inputLine = reader.ReadLine();
                            continue;
                        }
                        string ensemblID = toks[0];
                        // Skip the entry if the protein does not have any SNP annotations
                        if( !EnsemblProteinSequences.ContainsKey( ensemblID ) )
                        {
                            inputLine = reader.ReadLine();
                            continue;
                        }
                        // Store the map entry.
                        string IPIID = toks[1];
                        Set<string> ids = new Set<string>();
                        // If the map already has the entry then get the list
                        // of values and add the current value to the tail 
                        // of that list.
                        if( IPI2EnsemblMap.ContainsKey( IPIID ) )
                        {
                            ids = IPI2EnsemblMap[IPIID];
                            IPI2EnsemblMap.Remove( IPIID );
                        }
                        ids.Add( ensemblID );
                        IPI2EnsemblMap.Add( IPIID, ids );
                    }
                    inputLine = reader.ReadLine();
                }
            } catch( Exception e )
            {
                Console.Error.WriteLine( e.StackTrace );
                Console.Error.WriteLine( e.Source );
                Console.Error.WriteLine( "Error parsing the map file: " + accessionMap );
            }
        }

        /// <summary>
        /// This function takes a string in the following format and parses
        /// out the SNP annotations.
        /// rs1055138:L22V;rs34331648:S213N;rs13146272:Q259K;rs4561961:E404Q
        /// </summary>
        /// <param name="SNPString">A string representation of SNPs</param>
        public void parseSNPs( string SNPString, SNPMetaDataGenerator.SNPMetaData snpData )
        {
            if( SNPString.Length > 0 )
            {
                // Get each SNP
                string[] snps = SNPString.Split( new char[] { ';' } );
                for( int i = 0; i < snps.Length; ++i )
                {
                    // Split it into various fields
                    string[] snpToks = snps[i].Split( new char[] { ':' } );
                    if( snpToks.Length > 1 )
                    {
                        string snpID = snpToks[0];
                        //Console.WriteLine( snpToks[1] );
                        // Pull the from amino acid, location, and to amino acid information from the SNP annotation
                        Match match = Regex.Match( snpToks[1], @"(?<fromAA>([A-Z]))(?<loc>([0-9]+))(?<toAA>(.+))", RegexOptions.IgnoreCase );
                        if( match.Success )
                        {
                            string fromAA = match.Groups["fromAA"].Value;
                            string position = match.Groups["loc"].Value;
                            string toAA = match.Groups["toAA"].Value;
                            if( toAA.CompareTo( "*" ) == 0 )
                            {
                                continue;
                            }
                            // Add this meta data to a meta data generator. 
                            SNPMetaDataGenerator.SNP snp = new SNPMetaDataGenerator.SNP( fromAA, toAA, position );
                            SNPMetaDataGenerator.TypesOfMetaData type = new SNPMetaDataGenerator.TypesOfMetaData( snpToks[0] );
                            type.type = SNPMetaDataGenerator.MetaDataType.PROCANVAR;
                            type.url = SNPMetaDataGenerator.MetaDataURL.PROCANVAR;
                            snpData.knownMutations.Add( snp );
                            snpData.addMetaData( snp, type );
                        }
                    }
                }
            }
        }

        public override string ToString()
        {
            StringBuilder retStr = new StringBuilder();
            Dictionary<string, string>.Enumerator itr = EnsemblProteinSequences.GetEnumerator();
            while( itr.MoveNext() )
            {
                retStr.AppendLine( itr.Current.Key );
                retStr.AppendLine( itr.Current.Value );
            }

            Dictionary<string, Set<string>>.Enumerator itr2 = IPI2EnsemblMap.GetEnumerator();
            while( itr2.MoveNext() )
            {
                Set<string>.Enumerator ids = itr2.Current.Value.GetEnumerator();
                while( itr2.MoveNext() )
                {
                    retStr.AppendLine( itr.Current.Key + "\t" + itr2.Current.Value );
                }
            }

            return retStr.ToString();
        }
    }

    /// <summary>
    /// This class maps several types of SNP meta data to an abstract class. The class can hold meta
    /// data from ProCanVar, IPI, or Swiss-Prot databases. It can read the meta data from FASTA files, 
    /// dat files, or pull the meta data from a website.
    /// </summary>
    public class SNPMetaDataGenerator
    {
        // Web client that sends meta data requests
        public WebClient request = new WebClient();

        // A dictionary to hold all the collected meta data
        public Dictionary<string, SNPMetaData> CollectedMetaData = new Dictionary<string, SNPMetaData>();

        public char[] delimiters = new char[] { ' ' };

        // An object to hold CanProVar meta data
        public ProCanVar proCanVar;

        public void printCollectedMetaData()
        {
            Dictionary<string, SNPMetaData>.Enumerator data = CollectedMetaData.GetEnumerator();

            while( data.MoveNext() )
            {
                Console.WriteLine( data.Current.Value.ToString() );
            }
        }

        /// <summary>
        /// This function takes a protein accession, SNP, and the peptide sequence. It looks up in the
        /// CanProVar database to see if the SNP has any prior annotations
        /// </summary>
        /// <param name="proteinAcc">IPI protein accession</param>
        /// <param name="potentialSNP">SNP information</param>
        /// <param name="peptideSequence">Peptide sequence containing the SNP</param>
        /// <returns>CanProVar SNP annotation</returns>
        public string getSNPAnnotationFromProCanVar( string proteinAcc, SNP potentialSNP, string peptideSequence )
        {
            StringBuilder str = new StringBuilder();
            // Dictionary that holds the annotation
            Dictionary<string, TypesOfMetaData> anns = new Dictionary<string, TypesOfMetaData>();
            if( CollectedMetaData.Count == 0 || proCanVar == null )
            {
                return str.ToString();
            }

            // Map the IPI accession back to ensembl accessions
            Set<string> ensemblAccs = new Set<string>();
            if( proCanVar.EnsemblProteinSequences.ContainsKey( proteinAcc ) )
            {
                ensemblAccs.Add( proteinAcc );
            }
            if( proCanVar.IPI2EnsemblMap.ContainsKey( proteinAcc ) )
            {
                Set<string>.Enumerator itr = proCanVar.IPI2EnsemblMap[proteinAcc].GetEnumerator();
                while( itr.MoveNext() )
                {
                    ensemblAccs.Add( itr.Current );
                }
            }

            if( ensemblAccs.Count > 0 )
            {
                Set<string>.Enumerator itr = ensemblAccs.GetEnumerator();
                // For each protein accession
                while( itr.MoveNext() )
                {
                    // Get the protein sequence
                    string proteinSeq = proCanVar.EnsemblProteinSequences[itr.Current];
                    if( !CollectedMetaData.ContainsKey( itr.Current ) )
                    {
                        continue;
                    }
                    SNPMetaData knownSNPs = CollectedMetaData[itr.Current];
                    List<int[]> matches = new List<int[]>();
                    // Check to see if the protein sequence contains the peptide sequence
                    foreach( Match match in Regex.Matches( proteinSeq, peptideSequence ) )
                    {
                        // Get the peptide start and stop and remember them
                        int pepStart = match.Index;
                        int pepStop = match.Length + match.Index;

                        int[] positions = new int[] { pepStart, pepStop };
                        matches.Add( positions );
                        //Console.WriteLine( pepStart + "," + pepStop );
                    }
                    // For each peptide start and stop
                    List<int[]>.Enumerator pos = matches.GetEnumerator();
                    while( pos.MoveNext() )
                    {
                        // Get the known mutations for the protein sequence
                        Set<SNP>.Enumerator potentialSNPs = knownSNPs.knownMutations.GetEnumerator();
                        // Go through each one and see if any of them fall between the peptide start and stop
                        while( potentialSNPs.MoveNext() )
                        {
                            if( //Convert.ToInt32( potentialSNPs.Current.position ) >= pos.Current[0] &&
                                //Convert.ToInt32( potentialSNPs.Current.position ) <= pos.Current[1] &&
                                potentialSNPs.Current.fromAA.CompareTo( potentialSNP.fromAA ) == 0 &&
                                potentialSNPs.Current.toAA.CompareTo( potentialSNP.toAA ) == 0 )
                            {
                                // Compute the relative location of the SNP between peptide start and stop
                                //Console.WriteLine( potentialSNPs.Current.ToString() );
                                int SNPRelativeLoc = Convert.ToInt32( potentialSNPs.Current.position ) - pos.Current[0];
                                //Console.WriteLine( SNPRelativeLoc );
                                // If the relative position of the annotated SNP matches with the query SNP
                                // then remember the annotation
                                if( SNPRelativeLoc == Convert.ToInt32( potentialSNP.position ) )
                                {
                                    List<TypesOfMetaData> annotations = knownSNPs.metaData[potentialSNPs.Current];
                                    List<TypesOfMetaData>.Enumerator annotation = annotations.GetEnumerator();
                                    while( annotation.MoveNext() )
                                    {
                                        if( !anns.ContainsKey( annotation.Current.metaDataID ) )
                                        {
                                            anns.Add( annotation.Current.metaDataID, annotation.Current );
                                        }
                                        //str.Append( "(" + annotation.Current.ToString() + ")" );
                                    }
                                }
                            }
                        }
                    }
                }
            }
            // Add the new annotations and send them back
            Dictionary<string, TypesOfMetaData>.Enumerator annItr = anns.GetEnumerator();
            while( annItr.MoveNext() )
            {
                str.Append( "(" + annItr.Current.Value.ToString() + ")" );
            }
            return str.ToString();
        }

        /// <summary>
        /// This function takes a protein accession and a potential SNP. It looks up all the SNP
        /// annotations that can be found for that SNP. 
        /// </summary>
        /// <param name="proteinAcc">protein accession</param>
        /// <param name="potentialSNP">potential SNP</param>
        /// <returns>known SNP annotations</returns>
        public string getSNPAnnotation( string proteinAcc, SNP potentialSNP )
        {
            StringBuilder str = new StringBuilder();
            if( CollectedMetaData.Count == 0 )
            {
                return str.ToString();
            }
            // Check the meta data dictionary to see if the protein has any SNP annotations
            Dictionary<string, SNPMetaData>.Enumerator data = CollectedMetaData.GetEnumerator();
            SNPMetaData snpData = null;
            while( data.MoveNext() )
            {
                if( data.Current.Key.CompareTo( proteinAcc ) == 0 )
                {
                    snpData = data.Current.Value;
                    break;
                }
            }

            if( snpData != null )
            {
                //Console.WriteLine( snpData.ToString() );
                //Console.WriteLine( potentialSNP.ToString());

                // Get all typesof meta data that is available for the SNP. The meta data could come
                // from IPI dat files, Swiss-prot dat files, or even supported websites.
                Dictionary<SNP, List<TypesOfMetaData>>.Enumerator itr = snpData.metaData.GetEnumerator();

                while( itr.MoveNext() )
                {
                    // For each known SNP annotation of this protein, check if it matches with the found SNP
                    // If so, remember the annotation
                    if( itr.Current.Key.fromAA == potentialSNP.fromAA && itr.Current.Key.toAA == potentialSNP.toAA &&
                        itr.Current.Key.position == potentialSNP.position )
                    {
                        List<TypesOfMetaData> annotations = itr.Current.Value;
                        List<TypesOfMetaData>.Enumerator annotation = annotations.GetEnumerator();
                        while( annotation.MoveNext() )
                        {
                            str.Append( "(" + annotation.Current.ToString() + ")" );
                        }
                    }
                }
            }
            return str.ToString();
        }

        /// <summary>
        /// Type of the SNP annotation. Is it a dbSNP variant, Swiss-Prot variant, or CanProVar variant.
        /// </summary>
        public enum MetaDataType
        {
            SWISSPROT_VARIANT, DBSNP, PROCANVAR
        }

        /// <summary>
        /// The website URL needed to pull the SNP meta data.
        /// </summary>
        public enum MetaDataURL
        {
            [DescriptionAttribute( "http://www.expasy.org/cgi-bin/get-sprot-variant.pl?" )]
            SWISSPROT_VARIANT,
            [DescriptionAttribute( "http://www.ncbi.nlm.nih.gov/projects/SNP/snp_ref.cgi?" )]
            DBSNP,
            [DescriptionAttribute( "ProCanVar: URL not available" )]
            PROCANVAR
        }

        /// <summary>
        /// A class of utilities that print a string valued enum.
        /// </summary>
        public class EnumUtils
        {
            public static string stringValueOf( Enum value )
            {
                FieldInfo fi = value.GetType().GetField( value.ToString() );
                DescriptionAttribute[] attributes = (DescriptionAttribute[]) fi.GetCustomAttributes( typeof( DescriptionAttribute ), false );
                if( attributes.Length > 0 )
                {
                    return attributes[0].Description;
                } else
                {
                    return value.ToString();
                }
            }

            public static object enumValueOf( string value, Type enumType )
            {
                string[] names = Enum.GetNames( enumType );
                foreach( string name in names )
                {
                    if( stringValueOf( (Enum) Enum.Parse( enumType, name ) ).Equals( value ) )
                    {
                        return Enum.Parse( enumType, name );
                    }
                }

                throw new ArgumentException( "The string is not a description or value of the specified enum." );
            }
        }

        /// <summary>
        /// A class to store the type of the meta data. It stores the type, ID, and the URL used
        /// to pull the meta data.
        /// </summary>
        public class TypesOfMetaData
        {
            /// <summary>
            /// MetaDataType is either swiss-prot or dbSNP
            /// </summary>
            public MetaDataType type;
            /// <summary>
            /// The unique identifier that is used to index the
            /// meta data
            /// </summary>
            public string metaDataID;
            /// <summary>
            /// The website URL used to pull the meta data.
            /// </summary>
            public MetaDataURL url;

            public TypesOfMetaData( string id )
            {
                metaDataID = id;
            }

            public override string ToString()
            {
                StringBuilder retStr = new StringBuilder();
                retStr.Append( metaDataID );
                if( type == MetaDataType.SWISSPROT_VARIANT )
                {
                    retStr.Append( ";" + EnumUtils.stringValueOf( url ) + metaDataID );
                } else if( type == MetaDataType.DBSNP )
                {
                    retStr.Append( ";" + EnumUtils.stringValueOf( url ) + metaDataID.Substring( 0, 2 ) + "=" + metaDataID );
                } else if( type == MetaDataType.PROCANVAR )
                {
                    retStr.Append( ";" + EnumUtils.stringValueOf( url ) );
                }
                return retStr.ToString();
            }
        }

        /// <summary>
        /// This class stores the actual SNP information.
        /// </summary>
        public class SNP : IComparable<SNP>
        {
            /// <summary>
            /// fromAA is the original amino acid
            /// </summary>
            public string fromAA;
            /// <summary>
            /// toAA is the changed amino acid
            /// </summary>
            public string toAA;
            /// <summary>
            /// The location of the snp in the protien
            /// </summary>
            public string position;

            public SNP( string fAA, string tAA, string pos )
            {
                fromAA = fAA;
                toAA = tAA;
                position = pos;
            }

            public override string ToString()
            {
                return fromAA + "," + toAA + "," + position;
            }

            public int CompareTo( SNP other )
            {
                if( fromAA.CompareTo( other.fromAA ) == 0 && toAA.CompareTo( other.toAA ) == 0 && position.CompareTo( other.position ) == 0 )
                    return 0;

                int thisPos = Convert.ToInt32( position );
                int otherPos = Convert.ToInt32( other.position );
                return thisPos.CompareTo( otherPos );
            }

            public bool Equals( SNP other )
            {
                if( fromAA.Equals( other.fromAA ) && toAA.Equals( other.toAA ) && position.Equals( other.position ) )
                    return true;
                return false;
            }

        }

        /// <summary>
        /// This class maps the mutations to the protein and also all mutations to 
        /// their respective annotations.
        /// </summary>
        public class SNPMetaData
        {
            /// <summary>
            /// accession number of the protein
            /// </summary>
            public string proteinAccession;

            /// <summary>
            /// A set of known mutations
            /// </summary>
            public Set<SNP> knownMutations;
            /// <summary>
            /// The annotations of the known mutations
            /// </summary>
            public Dictionary<SNP, List<TypesOfMetaData>> metaData;

            public SNPMetaData() { }

            public SNPMetaData( string protAcc )
            {
                proteinAccession = protAcc;
                metaData = new Dictionary<SNP, List<TypesOfMetaData>>();
                knownMutations = new Set<SNP>();
            }

            /// <summary>
            /// This function adds a known mutation to set of the known 
            /// mutations.
            /// </summary>
            /// <param name="mutation">The mutation to be added</param>
            /// <param name="datum">Type of the meta data. (dbSNP or Swiss-Prot)</param>
            public void addMetaData( SNP mutation, TypesOfMetaData datum )
            {
                List<TypesOfMetaData> data = null;
                if( metaData.ContainsKey( mutation ) )
                {
                    data = metaData[mutation];
                    metaData.Remove( mutation );
                } else
                {
                    data = new List<TypesOfMetaData>();
                }
                data.Add( datum );
                metaData.Add( mutation, data );
            }

            public override string ToString()
            {
                if( knownMutations == null || knownMutations.Count == 0 )
                {
                    return "";
                }

                StringBuilder str = new StringBuilder( proteinAccession );

                Set<SNP>.Enumerator mutation = knownMutations.GetEnumerator();

                while( mutation.MoveNext() )
                {
                    str.Append( "\n\t" + mutation.Current.ToString() );
                    if( metaData.ContainsKey( mutation.Current ) )
                    {
                        int cap = metaData[mutation.Current].Count;

                        List<TypesOfMetaData> annotations = metaData[mutation.Current];
                        List<TypesOfMetaData>.Enumerator annotation = annotations.GetEnumerator();
                        while( annotation.MoveNext() )
                        {
                            str.Append( "\n\t" + annotation.Current.ToString() );
                        }
                    }
                }
                return str.ToString();
            }
        }

        /// <summary>
        /// This finction reads a CanProVar fasta formatted file, calls the approriate function to parse
        /// the meta data out of the file
        /// </summary>
        /// <param name="proCanVarFasta">FASTA formatted CanProVar database</param>
        /// <param name="proCanVarAccMap">IPI->Ensembl accession map</param>
        public void getVariantDataFromProCanVarFlatFile( string proCanVarFasta, string proCanVarAccMap )
        {
            proCanVar = new ProCanVar( proCanVarFasta, proCanVarAccMap );
            // Check for existing data and then add the annotations from ProCanVar
            if( CollectedMetaData.Count == 0 )
            {
                CollectedMetaData = proCanVar.snpMap;
            } else
            {
                Dictionary<string, SNPMetaData>.Enumerator itr = proCanVar.snpMap.GetEnumerator();
                while( itr.MoveNext() )
                {
                    if( !CollectedMetaData.ContainsKey( itr.Current.Key ) )
                    {
                        CollectedMetaData.Add( itr.Current.Key, itr.Current.Value );
                    }
                }
            }
        }

        /// <summary>
        /// This function takes a set of swiss-prot accessions, a flat DAT file, and
        /// extracts all the SNP annotaions for the corresponding annotations.
        /// </summary>
        /// <param name="swissProtAccessions">Swiss-Prot accessions</param>
        /// <param name="filename">Swiss-Prot dat file</param>
        public void getVariantDataFromSwissProtFlatFile( Set<string> swissProtAccessions, string filename )
        {
            StreamReader reader = new StreamReader( filename );
            //Read the data
            string inputLine = reader.ReadLine();
            SNPMetaData snpData = null;
            SNP currentMutation = null;
            bool go = false;
            while( inputLine != null )
            {
                try
                {
                    if( inputLine.StartsWith( "AC" ) )
                    {
                        go = false;
                        string[] toks = inputLine.Split( new char[] { ' ', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries );
                        if( swissProtAccessions.Contains( toks[1] ) )
                        {
                            go = true;
                            if( snpData != null && snpData.knownMutations.Count > 0 )
                            {
                                Console.Write( "\rExtracted " + snpData.knownMutations.Count + " SNP annotations for " + snpData.proteinAccession + "        " );
                                CollectedMetaData.Add( snpData.proteinAccession, snpData );
                            }
                            snpData = new SNPMetaData( toks[1] );
                        }
                    }

                    if( go )
                    {
                        //Check for the variant annotations
                        if( inputLine.StartsWith( "FT" ) )
                        {
                            if( inputLine.Contains( "VARIANT" ) )
                            {
                                //Parse out the fromAA -> toAA 
                                string[] tokens = inputLine.Split( delimiters, StringSplitOptions.RemoveEmptyEntries );

                                currentMutation = null;
                                if( tokens.Length >= 7 )
                                {
                                    string position = tokens[2];
                                    string fromAA = tokens[4];
                                    string toAA = tokens[6];
                                    currentMutation = new SNP( fromAA, toAA, position );
                                    if( snpData != null )
                                        snpData.knownMutations.Add( currentMutation );
                                }
                            } else if( inputLine.Contains( "/FTId=VAR_" ) )
                            {
                                //Get the meta data identifier for the SNP and add it to the SNP as Swiss-Prot
                                //variant.
                                Match match = Regex.Match( inputLine, @"FTId=(.*)\.", RegexOptions.IgnoreCase );
                                if( match.Success )
                                {
                                    string metaDataID = match.Groups[1].Value;
                                    TypesOfMetaData annotation = new TypesOfMetaData( metaDataID );
                                    annotation.type = MetaDataType.SWISSPROT_VARIANT;
                                    annotation.url = MetaDataURL.SWISSPROT_VARIANT;
                                    if( snpData != null && currentMutation != null )
                                        snpData.addMetaData( currentMutation, annotation );
                                }
                            } else if( inputLine.Contains( "dbSNP:" ) )
                            {
                                //Get the meta data identifier for the SNP and add it to the SNP as dbSNP
                                //variant.
                                Match match = Regex.Match( inputLine, @"dbSNP:(.*)\).", RegexOptions.IgnoreCase );
                                if( match.Success )
                                {
                                    string metaDataID = match.Groups[1].Value;
                                    TypesOfMetaData annotation = new TypesOfMetaData( metaDataID );
                                    annotation.type = MetaDataType.DBSNP;
                                    annotation.url = MetaDataURL.DBSNP;
                                    if( snpData != null && currentMutation != null )
                                        snpData.addMetaData( currentMutation, annotation );
                                }
                            }
                        }
                    }
                } catch( Exception e )
                {
                    Console.Error.WriteLine( e.StackTrace );
                    Console.Error.WriteLine( e.Message );
                    Console.Error.WriteLine( "Failed to get SNPs for one of the accessions." );
                }

                inputLine = reader.ReadLine();
            }
        }

        /// <summary>
        /// This function takes a Swiss-Prot accession number and pulls out the 
        /// SNP annotation from the web site. Be careful while using this routine. 
        /// Because, most of the websites block the automated requests after some 
        /// set number of requests. For example, IPI stipulates that the automated 
        /// program should chill for atleast 5 seconds in between the requests.
        /// </summary>
        /// <param name="swissProtAccession">Protein accession number</param>
        /// <returns>The meta data for the SNP</returns>
        public SNPMetaData getVariantDataFromSwissProtWebSite( string swissProtAccession )
        {
            try
            {
                //Form the URL and retrieve the data
                string url = "http://www.uniprot.org/uniprot/" + swissProtAccession + ".txt";
                request = new WebClient();
                Stream data = request.OpenRead( url );
                StreamReader reader = new StreamReader( data );

                //Read the data
                string inputLine = reader.ReadLine();
                SNPMetaData snpData = new SNPMetaData( swissProtAccession );
                SNP currentMutation = null;
                while( inputLine != null )
                {
                    //Check for the variant annotations
                    if( inputLine.StartsWith( "FT" ) )
                    {
                        if( inputLine.Contains( "VARIANT" ) )
                        {
                            //Parse out the fromAA -> toAA 
                            string[] tokens = inputLine.Split( delimiters, StringSplitOptions.RemoveEmptyEntries );

                            currentMutation = null;
                            if( tokens.Length >= 7 )
                            {
                                string position = tokens[2];
                                string fromAA = tokens[4];
                                string toAA = tokens[6];
                                currentMutation = new SNP( fromAA, toAA, position );
                                snpData.knownMutations.Add( currentMutation );
                            }
                        } else if( inputLine.Contains( "/FTId=VAR_" ) )
                        {
                            //Get the meta data identifier for the SNP and add it to the SNP as Swiss-Prot
                            //variant.
                            Match match = Regex.Match( inputLine, @"FTId=(.*)\.", RegexOptions.IgnoreCase );
                            if( match.Success )
                            {
                                string metaDataID = match.Groups[1].Value;
                                TypesOfMetaData annotation = new TypesOfMetaData( metaDataID );
                                annotation.type = MetaDataType.SWISSPROT_VARIANT;
                                annotation.url = MetaDataURL.SWISSPROT_VARIANT;
                                snpData.addMetaData( currentMutation, annotation );
                            }
                        } else if( inputLine.Contains( "dbSNP:" ) )
                        {
                            //Get the meta data identifier for the SNP and add it to the SNP as dbSNP
                            //variant.
                            Match match = Regex.Match( inputLine, @"dbSNP:(.*)\).", RegexOptions.IgnoreCase );
                            if( match.Success )
                            {
                                string metaDataID = match.Groups[1].Value;
                                TypesOfMetaData annotation = new TypesOfMetaData( metaDataID );
                                annotation.type = MetaDataType.DBSNP;
                                annotation.url = MetaDataURL.DBSNP;
                                snpData.addMetaData( currentMutation, annotation );
                            }
                        }
                    }
                    inputLine = reader.ReadLine();
                }

                if( snpData.knownMutations.Count > 0 )
                    return snpData;

            } catch( Exception e )
            {
                Console.Error.WriteLine( e.StackTrace );
                Console.Error.WriteLine( e.Message );
                Console.Error.WriteLine( "Error getting the variant data for accession:" + swissProtAccession );
            }
            return null;
        }

        /// <summary>
        /// This function takes an IPI protein accession number and pulls out the
        /// SNP annotations for that protein. Be careful while using this routine. 
        /// Because,most of the websites block the automated requests after some 
        /// set number of requests. For example, IPI stipulates that the automated 
        /// program should chill for atleast 5 seconds in between the requests.
        /// </summary>
        /// <param name="ipiAccession">An IPI accession number</param>
        /// <returns>SNP meta data</returns>
        public SNPMetaData getVariationDataFromIPIWebSite( string ipiAccession )
        {
            try
            {
                //Form the URL and get the record
                string url = "http://srs.ebi.ac.uk/srsbin/cgi-bin/wgetz?-noSession+-e+[IPI-acc:" + ipiAccession + "]+-vn+2";
                System.Net.HttpWebRequest wr = (System.Net.HttpWebRequest) System.Net.HttpWebRequest.Create( url );
                System.Net.HttpWebResponse ws = (System.Net.HttpWebResponse) wr.GetResponse();
                System.IO.Stream str = ws.GetResponseStream();

                StreamReader reader = new StreamReader( str );
                //Console.WriteLine( url );
                string inputLine = reader.ReadLine();
                while( inputLine != null )
                {
                    //Get the corresponding Swiss-Prot ID and use it to pull
                    //the SNP annotations.
                    if( inputLine.StartsWith( "DR" ) && inputLine.Contains( "Swiss-Prot" ) )
                    {
                        Match match = Regex.Match( inputLine, @"/Swiss-Prot; <.*>(.*)<.*>;", RegexOptions.IgnoreCase );
                        if( match.Success )
                        {
                            string swissProtID = match.Groups[1].Value.Trim();
                            SNPMetaData snpData = getVariantDataFromSwissProtWebSite( swissProtID );
                            Console.WriteLine( swissProtID );
                            if( snpData != null )
                            {
                                Console.WriteLine( "Found " + snpData.knownMutations.Count + " known SNP annotations." );
                            }
                            if( snpData != null )
                            {
                                snpData.proteinAccession = ipiAccession;
                                return snpData;
                            }
                        }
                    }
                    inputLine = reader.ReadLine();
                }
            } catch( Exception e )
            {
                Console.Error.WriteLine( e.StackTrace );
                Console.Error.WriteLine( e.Message );
                Console.Error.WriteLine( "Error getting the variant data for accession:" + ipiAccession );
            }
            return null;
        }

        /// <summary>
        /// This function takes a protein accession number, check to see if it's either
        /// an IPI accession or a Swiss-Prot accession and calls the corresponding
        /// functions that pull the SNP annotations.
        /// </summary>
        /// <param name="proteinAccession">A protein accession number</param>
        public void getSNPMetaData( string proteinAccession )
        {
            SNPMetaData data = null;
            if( proteinAccession.StartsWith( "IPI" ) )
            {
                data = getVariationDataFromIPIWebSite( proteinAccession );

            } else if( proteinAccession.StartsWith( "P" ) || proteinAccession.StartsWith( "Q" ) )
            {
                data = getVariantDataFromSwissProtWebSite( proteinAccession );
            }
            if( data != null )
            {
                CollectedMetaData.Add( proteinAccession, data );
                Console.WriteLine( "Found " + data.knownMutations.Count + " known SNP annotations" );
            }
        }
    }
}