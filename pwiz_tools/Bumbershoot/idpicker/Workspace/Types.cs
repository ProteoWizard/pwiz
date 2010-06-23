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
    /// The class stores an instance of a result. The class holds the rank, FDR, 
    /// peptide interpretations, spectral identifications, and modification 
    /// objects for a particular result.
    /// </summary>
    public class ResultInstance : IComparable<ResultInstance>
    {
        /// <summary>
        /// Creates a new result instance mods list for sorting the mods.
        /// </summary>
        public ResultInstance()
        {
            mods = new ResultInstanceModList();
        }

        /// <summary>
        /// Sorts the results bases on the rank (increasing order).
        /// </summary>
        /// <param name="other">Another <see cref="IDPicker.ResultInstance"/> object.</param>
        /// <returns></returns>
        public int CompareTo( ResultInstance other )
        {
            return -rank.CompareTo( other.rank );
        }

        /// <summary>
        /// Returns a string representing the first peptide of a result instance
        /// with annotations for both distinct and indistinct mods,
        /// e.g. "M[16]INER" even when M+16 is set as indistinct.
        /// </summary>
        public string ToSimpleString()
        {
            PeptideInfo firstPeptide = info.peptides.Keys[0].peptide;

            if (mods[firstPeptide].Count > 0)
            {
                StringBuilder str = new StringBuilder();

                ModMap firstModMap = mods[firstPeptide][0];

                // Find all n-terminal mods and start appending them to the string
                ModMap.Enumerator nMod = firstModMap.Find('n');
                if (nMod.IsValid)
                    foreach (ModInfo mod in nMod.Current.Value)
                        str.AppendFormat("[{0}]", mod.mass);

                // Start with the peptide sequence
                for (int r = 0; r < firstPeptide.sequence.Length; ++r)
                {
                    // For each amino acid append the residue, followed
                    // by all modifications on that residue. Mods are 
                    // indexed from 1. So (r+1) has to be used in order
                    // to place the mods after the right residue.
                    str.Append(firstPeptide.sequence[r]);
                    ModMap.Enumerator posMod = firstModMap.Find(Convert.ToChar(r + 1));
                    if (posMod.IsValid)
                        foreach (ModInfo mod in posMod.Current.Value)
                        {
                            str.AppendFormat("[{0}]", mod.mass);
                        }
                }

                // Append the c-terminal mods to the end.
                ModMap.Enumerator cMod = firstModMap.Find('c');
                if (cMod.IsValid)
                    foreach (ModInfo mod in cMod.Current.Value)
                        str.AppendFormat("[{0}]", mod.mass);

                return str.ToString();
            }
            else
                return firstPeptide.sequence;
        }

        /// <summary>
        /// returns the ToString() of each peptide in info (with mods) and joined by '/'
        /// </summary>
        public override string ToString()
        {
            PeptideList.Enumerator itr = info.peptides.GetEnumerator(); itr.MoveNext();
            StringBuilder str = new StringBuilder( itr.Current.ToString() );
            while( itr.MoveNext() )
                str.Append( "/" + itr.Current.ToString() );
            return str.ToString();
        }

        /// <summary>
        /// This procedure checks to see if there are any scores associated
        /// with the result. It returns the string version of the scores.
        /// </summary>
        /// <returns>A string representation of the scores</returns>
        public string getSearchScoreString()
        {
            StringBuilder retStr = new StringBuilder();
            if( searchScores == null || searchScores.Count == 0 )
            {
                return null;
            }
            Dictionary<string, float>.Enumerator iter = searchScores.GetEnumerator();
            int count = 0;
            while( iter.MoveNext() )
            {
                if( count > 0 )
                    retStr.Append( " " );
                retStr.Append( iter.Current.Value );
                ++count;
            }
            return retStr.ToString();
        }

        public string getSearchScoreStringWithNames()
        {
            StringBuilder retStr = new StringBuilder();
            if( searchScores != null && searchScores.Count > 0 )
            {
                Dictionary<string, float>.Enumerator iter = searchScores.GetEnumerator();
                while( iter.MoveNext() )
                {
                    retStr.Append( "(" + iter.Current.Key + " " + iter.Current.Value + ")" );
                }
            }
            return retStr.ToString();
        }

        /// <summary>
        /// rank holds the rank of an interpretation
        /// </summary>
        public int rank;
        /// <summary>
        /// FDR hods the flase discovery rate for the interpretation
        /// </summary>
        public float FDR;
        /// <summary>
        /// info holds a list of peptide identifications and the spectra mapping
        /// the interpretations for the peptide identifications
        /// </summary>
        public ResultInfo info;
        /// <summary>
        /// spectrum holds information about a spectrum
        /// </summary>
        public SpectrumInfo spectrum;
        /// <summary>
        /// mods holds modifications for each peptide, both distinct and indistinct
        /// </summary>
        public ResultInstanceModList mods;
        /// <summary>
        /// numberOfModifiedPeptides counts the number of peptides with modification in
        /// the result.
        /// </summary>
        public int numberOfModifiedPeptides;
        /// <summary>
        /// numberOfUnmodifiedPeptides counts the number of peptides without modifications in
        /// the result.
        /// </summary>
        public int numberOfUnmodifiedPeptides;
        /// <summary>
        /// This dictionary holds the search scores if present in the idpXML
        /// </summary>
        public Dictionary<string, float> searchScores;
    }

    /// <summary>
    /// This class maps a PeptideInfo object (see <see cref="IDPicker.PeptideInfo"/>) 
    /// to the list of mods (see <see cref="IDPicker.ModMap"/>) found on the peptide.
    /// </summary>
    public class ResultInstanceModList : Map<PeptideInfo, List<ModMap>>
    {
        /// <summary>
        /// returns the ToString() of each ModList, joined by '/'
        /// </summary>
        public string ModsToString()
        {
            Enumerator itr = GetEnumerator(); itr.MoveNext();
            if( !itr.IsValid )
                return String.Empty;
            StringBuilder str = new StringBuilder( itr.Current.Value.ToString() );
            while( itr.MoveNext() )
                str.Append( "/" + itr.Current.Value.ToString() );
            return str.ToString();
        }

        /// <summary>
        /// Returns a space-separated list of mods for a particular
        /// peptide.
        /// </summary>
        /// <param name="key">A peptide to lookup the mods. (see <see cref="IDPicker.PeptideInfo"/>)</param>
        /// <returns></returns>
        public string ToString( PeptideInfo key )
        {
            List<ModMap> mods = this[key];
            StringBuilder retString = new StringBuilder();
            // Enumerate through the list of mods and convert them
            // to the string format and append them to the string.
            List<ModMap>.Enumerator itr = mods.GetEnumerator();
            while( itr.MoveNext() )
            {
                retString.Append( itr.Current.ToString() );
            }
            return retString.ToString();
        }
    }

    /// <summary>
    /// ResultInfo contains a set of peptide identifications (see <see cref="IDPicker.PeptideList"/>)
    /// and a list of spectra (see <see cref="IDPicker.SpectrumList"/>). The spectra list maps the
    /// spectra to their corresponding interpretations. The list of peptide identifications in this
    /// set are considered as equivalent (meaning, peptides with same scores).
    /// </summary>
    public class ResultInfo : IComparable<ResultInfo>
    {
        public ResultInfo()
        {
            peptides = new PeptideList();
            spectra = new SpectrumList();
        }

        /// <summary>
        /// Initializes a new ResultInfo object using the user supplied peptides
        /// and a new spectrum list (see <see cref="IDPicker.SpectrumList"/>).
        /// </summary>
        /// <param name="parts"></param>
        public ResultInfo( PeptideList parts )
        {
            peptides = parts;
            spectra = new SpectrumList();
        }

        /// <summary>
        /// Initialize the result set using the peptides and spectra from the argument
        /// </summary>
        /// <param name="other">A <see cref="IDPicker.ResultInfo"/> object. </param>
        public ResultInfo( ResultInfo other )
        {
            peptides = new PeptideList( other.peptides );
            spectra = new SpectrumList( other.spectra );
        }

        /// <summary>
        /// This function compares two result sets based on their system generated
        /// id or the peptides in the result set.
        /// </summary>
        /// <param name="other">A <see cref="IDPicker.ResultInfo"/> object. </param>
        /// <returns>An int value depending on whether the two objects are either
        /// equal to each other, or one of it is either less than, or greater than
        /// the other.</returns>
        public int CompareTo( ResultInfo other )
        {
            if( id > 0 && other.id > 0 && id == other.id )
                return 0;
            return peptides.CompareTo( other.peptides );
        }

        /// <summary>
        /// returns the ToString() of each peptide (with mods) in the result, joined by '/'
        /// The '/' represents that the all peptides have equal scores.
        /// </summary>
        public override string ToString()
        {
            PeptideList.Enumerator itr = peptides.GetEnumerator(); itr.MoveNext();
            StringBuilder str = new StringBuilder( itr.Current.ToString() );
            while( itr.MoveNext() )
                str.Append( "/" + itr.Current.ToString() );
            return str.ToString();
        }

        public string extractUnknownMods( char[] residues, float[] masses )
        {
            int unknownInterps = 0;
            PeptideList.Enumerator itr = peptides.GetEnumerator();
            StringBuilder str = new StringBuilder();
            while( itr.MoveNext() )
            {
                string tmp = itr.Current.mods.extractUnknownMod( residues, masses );
                if( tmp != null )
                {
                    ++unknownInterps;
                    if( str.Length > 0 )
                        str.Append( "/" );
                    str.Append( tmp );
                }
            }
            if( unknownInterps > 0 )
                return str.ToString();
            return null;
        }

        public string ModAnnotations()
        {
            PeptideList.Enumerator itr = peptides.GetEnumerator(); itr.MoveNext();
            StringBuilder str = new StringBuilder( itr.Current.ModAnnotations() );
            while( itr.MoveNext() )
                str.Append( "/" + itr.Current.ModAnnotations() );
            return str.ToString();
        }

        /// <summary>
        /// Returns modifications objects of all peptides present in the
        /// result in a string format joined by '/'.
        /// </summary>
        public string ModsToString( char listDelimiter, char pairDelimiter )
        {
            StringBuilder returnString = new StringBuilder();
            PeptideList.Enumerator itr = peptides.GetEnumerator(); itr.MoveNext();
            returnString.Append("\"" + itr.Current.ModsToString(listDelimiter, pairDelimiter));
            while( itr.MoveNext() )
                returnString.Append("/" + itr.Current.ModsToString(listDelimiter, pairDelimiter));
            returnString.Append( "\"" );
            return returnString.ToString();
        }

        /// <summary>
        /// DecoyState represents whether the result set matched to real, decoy, unknown,
        /// or ambiguous proteins.
        /// </summary>
        public enum DecoyState
        {
            REAL,
            DECOY,
            AMBIGUOUS,
            UNKNOWN
        }

        /// <summary>
        /// isDistinctPeptide falg is true if the result set has atleast one
        /// peptide that can be used to as evidence for the protein
        /// </summary>
        public bool isDistinctPeptide
        {
            get
            {
                bool distinctPep = false;
                foreach( VariantInfo pepItr in peptides )
                {
                    if( pepItr.isDistinctPeptide )
                    {
                        distinctPep = true;
                        break;
                    }
                }
                return distinctPep;
            }
        }
        /// <summary>
        /// decoyState variable is set to real if none of the peptides are matched to a
        /// decoy protein (reversed entry) or ambiguous entry. Otherwise, the value is set
        /// to either decoy or ambiguous.
        /// </summary>
        public DecoyState decoyState
        {
            get
            {
                DecoyState curState = DecoyState.UNKNOWN;
                // For each peptide spectrum interpretation
                foreach( VariantInfo pepItr in peptides )
                {
                    // Get the protein identification
                    foreach( ProteinInstanceList.MapPair proItr in pepItr.peptide.proteins )
                    {
                        // Set the state according to protein status.
                        if( curState == DecoyState.UNKNOWN )
                        {
                            if( proItr.Value.protein.isDecoy )
                            {
                                curState = DecoyState.DECOY;
                            } else
                            {
                                curState = DecoyState.REAL;
                            }
                        } else if( ( curState == DecoyState.DECOY && !proItr.Value.protein.isDecoy ) ||
                                ( curState == DecoyState.REAL && proItr.Value.protein.isDecoy ) )
                        {
                            // If the peptide matched to both real and decoy proteins then set
                            // its status as ambiguous
                            curState = DecoyState.AMBIGUOUS;
                            break;
                        }
                    }
                    if( curState == DecoyState.AMBIGUOUS )
                        break;
                }
                return curState;
            }
        }

        public string getSNPMetaDataFromProCanVar( Workspace ws )
        {
            StringBuilder retStr = new StringBuilder();
            // For each peptide spectrum interpretation
            foreach( VariantInfo pepItr in peptides )
            {
                ModMap pepMods = pepItr.mods;
                Map<string, int> potentialSNPs = pepMods.extractPotentialSNPs( ws.residueMaps, ws.knownModResidues, ws.knownModMasses );
                if( potentialSNPs.Count > 0 )
                {
                    //Console.WriteLine( this.ToString() );
                    // Get the protein identification
                    foreach( ProteinInstanceList.MapPair proItr in pepItr.peptide.proteins )
                    {
                        string proteinAcc = proItr.Value.protein.getIPIAccession();
                        //Console.WriteLine( proItr.Value.protein.locus );
                        //Console.WriteLine( proteinAcc );
                        if( proteinAcc == null )
                        {
                            continue;
                        }
                        //Console.WriteLine( proteinAcc );
                        Map<string, int>.Enumerator itr = potentialSNPs.GetEnumerator();
                        while( itr.MoveNext() )
                        {
                            SNPMetaDataGenerator.SNP potentialSNP = new SNPMetaDataGenerator.SNP( itr.Current.Key[0] + "", itr.Current.Key[1] + "", itr.Current.Value + "" );
                            //Console.WriteLine( potentialSNP );
                            string ann = ws.snpAnntoations.getSNPAnnotationFromProCanVar( proteinAcc, potentialSNP, pepItr.peptide.sequence );
                            if( ann != null && ann.Length > 0 )
                            {
                                retStr.Append( ann );
                            }
                        }
                    }
                }
            }
            return retStr.ToString();
        }

        public string getSNPMetaData( Workspace ws )
        {
            StringBuilder retStr = new StringBuilder();
            // For each peptide spectrum interpretation
            foreach( VariantInfo pepItr in peptides )
            {
                ModMap pepMods = pepItr.mods;
                Map<string, int> potentialSNPs = pepMods.extractPotentialSNPs( ws.residueMaps, ws.knownModResidues, ws.knownModMasses );
                if( potentialSNPs.Count > 0 )
                {
                    //Console.WriteLine( this.ToString() );
                    // Get the protein identification
                    foreach( ProteinInstanceList.MapPair proItr in pepItr.peptide.proteins )
                    {
                        string protLocus = proItr.Value.protein.locus;
                        string protAccession = proItr.Value.protein.getSwissProtAccession();
                        if( protAccession == null )
                        {
                            continue;
                        }
                        int pos = proItr.Value.offset;
                        Map<string, int>.Enumerator itr = potentialSNPs.GetEnumerator();
                        while( itr.MoveNext() )
                        {
                            SNPMetaDataGenerator.SNP potentialSNP = new SNPMetaDataGenerator.SNP( itr.Current.Key[0] + "", itr.Current.Key[1] + "", ( pos + itr.Current.Value ) + "" );
                            string ann = ws.snpAnntoations.getSNPAnnotation( protAccession, potentialSNP );

                            if( ann != null )
                            {
                                retStr.Append( ann );
                            }
                        }
                    }
                }
            }
            return retStr.ToString();
        }

        public string getProteinLoci()
        {
            bool first = true;
            StringBuilder proteinLoci = new StringBuilder();
            // For each peptide spectrum interpretation
            foreach( VariantInfo pepItr in peptides )
            {
                // Get the protein identification
                foreach( ProteinInstanceList.MapPair proItr in pepItr.peptide.proteins )
                {
                    if( first )
                    {
                        proteinLoci.Append( proItr.Value.protein.locus + "," + ( proItr.Value.offset + 1 ) + "," + ( proItr.Value.offset + 1 + pepItr.peptide.sequence.Length ) + "," );
                        first = false;
                    } else
                    {
                        proteinLoci.Append( "(" + proItr.Value.protein.locus + ";" + ( proItr.Value.offset + 1 ) + ") " );
                    }
                }
            }
            return proteinLoci.ToString();
        }

        /// <summary>
        /// id is a system generated identifier for the result
        /// </summary>
        public int id;
        /// <summary>
        /// peptides is a list of peptide identifications in the result set (see <see cref="IDPicker.PeptideList"/>)
        /// </summary>
        public PeptideList peptides;
        /// <summary>
        /// spectra is a map of spectrum ids to their corresponding interpretations (see <see cref="IDPicker.SpectrumList"/>)
        /// </summary>
        public SpectrumList spectra;
        public PeptideGroupInfo peptideGroup;
    }

    ///<summary>
    /// SpectrumId stores the identification elements needed to uniquely identify 
    /// a spectrum. The basic identification elements are spectrum source, scan
    /// number and its charge state.</summary>
    /// 
    /// <remarks> <para>
    /// The member variable of the class are: <br/>
    /// <param name='source'>A <see cref="IDPicker.SourceInfo"/> type representing the 
    /// source of the spectrum</param>
    /// <param name='index'>A <see cref="System.Int32"/> type representing the scan number 
    /// of the spectrum  </param> 
    /// <param name='charge'>A <see cref="System.Int32"/> type representing the charge state
    /// of the spectrum</param> 
    /// </para> </remarks>
    public class SpectrumId : IComparable<SpectrumId>
    {
        /// <summary>
        /// A default constructor with no arguments. Caution: This constructor
        /// doesn't initialize any object members.
        /// </summary>
        public SpectrumId() { }

        /// <summary>
        ///  A constuctor that initializes a SpectrumID object
        /// </summary>
        /// <param name="source">Source of the spectrum</param>
        /// <param name="scan">Scan number of the spectrum</param>
        /// <param name="charge">Charge state of the spectrum</param>
        public SpectrumId( SourceInfo source, int scan, int charge )
        {
            this.source = source;
            this.index = scan;
            this.charge = charge;
        }

        /// <summary>
        /// Compares one spectrum to another using source ID, scan number
        /// and charge state
        /// </summary>
        /// <param name="other"> A <see cref="SpectrumID"/> type of another
        /// spectrum.</param>
        /// <returns>An int value depending on whether the two objects are either
        /// equal to each other, or one of it is either less than, or greater than
        /// the other.</returns>
        public int CompareTo( SpectrumId other )
        {
            // Compare their ids
            if( id > 0 && other.id > 0 && id == other.id )
            {
                return 0;
            }

            if (source == other.source)
                if (index == other.index)
                    return charge.CompareTo(other.charge);
                else
                    return index.CompareTo(other.index);
            else
                return source.CompareTo(other.source);
        }

        /// <summary>
        /// A function to convert the object members into a string
        /// </summary>
        /// <returns>A string value in format of sourceName.index.charge</returns>
        public override string ToString()
        {
            return source.ToString() + "." + index + "." + charge;
        }

        public int id;
        // Source information of the spectrum
        public SourceInfo source;
        // Index of the spectrum
        public int index;
        // Charge of the spectrum
        public int charge;
    }

    /// <summary>
    /// SpectrumInfo stores information about a spectrum. The
    /// object holds the spectrum ID, peptide identification 
    /// results, precursor mass, retention time and the number
    /// of comparisons done with the spectrum.
    /// </summary>
    public class SpectrumInfo : IComparable<SpectrumInfo>
    {
        public SpectrumInfo()
        {
            id = new SpectrumId();
            results = new ResultInstanceList();
        }

        /**
         * CompareTo param name="SpectrumInfo" 
         */
        public int CompareTo( SpectrumInfo other )
        {
            return id.CompareTo( other.id );
        }

        public override string ToString()
        {
            return id.ToString();
        }

        //public string group;
        // Basic spectrum identification elements
        public SpectrumId id;
        /// <summary>
        /// String version of the identification.
        /// </summary>
        //public string stringID;
        // Native ID (supplied by the instrument)
        public string nativeID;
        // List of peptide identification results.
        public ResultInstanceList results;
        // Neutral mass of the precursor
        public float precursorMass;
        // Retention time of the precursor
        public float retentionTime;
        // Total number of peptides compared to this spectrum.
        public int numComparisons;

        public QuantitationInfo quantitation = null;
    }

    /// <summary>
    /// Stores info about a spectrum from a quantitative experiment like ITRAQ, SILAC, or ICAT
    /// HACK: currently only supports ITRAQ
    /// </summary>
    public class QuantitationInfo
    {
        public enum Method
        {
            None,
            ITRAQ4Plex,
            ITRAQ8Plex
        }

        public Method method;

        public double ITRAQ_113_intensity;
        public double ITRAQ_114_intensity;
        public double ITRAQ_115_intensity;
        public double ITRAQ_116_intensity;
        public double ITRAQ_117_intensity;
        public double ITRAQ_118_intensity;
        public double ITRAQ_119_intensity;
        public double ITRAQ_121_intensity;

        public string ToDelimitedString (char delimiter)
        {
            if (method == QuantitationInfo.Method.ITRAQ4Plex)
                return String.Format("{1}{0}{2}{0}{3}{0}{4}", delimiter, ITRAQ_114_intensity, ITRAQ_115_intensity, ITRAQ_116_intensity, ITRAQ_117_intensity);
            else if (method == QuantitationInfo.Method.ITRAQ8Plex)
                return String.Format("{1}{0}{2}{0}{3}{0}{4}{0}{5}{0}{6}{0}{7}{0}{8}", delimiter, ITRAQ_113_intensity, ITRAQ_114_intensity, ITRAQ_115_intensity, ITRAQ_116_intensity, ITRAQ_117_intensity, ITRAQ_118_intensity, ITRAQ_119_intensity, ITRAQ_121_intensity);
            else
                return String.Empty;
        }
    }

    public class MultiSourceQuantitationInfo : QuantitationInfo
    {
        public int spectralCount;
    }

    ///<summary> 
    ///  HypotheticalModInfo stores the residue and the mass of a modification
    ///  that is considered as unknown. These type of modifications are generally
    ///  considered as indistinct modifications. A peptide with an indistinct
    ///  modification doesn't have enough power to identify a protein that is
    ///  otherwise subsumable by another protein or a cluster. 
    /// </summary>
    public class HypotheticalModInfo : IComparable<HypotheticalModInfo>
    {
        /// <summary>
        /// A constructor that initalizes the object with a residue name
        /// that holds the modification and its modification mass.
        /// </summary>
        /// <param name="residue">A residue character that holds the modification</param>
        /// <param name="mass">Monoisotopic mass of the modification</param>
        public HypotheticalModInfo( char residue, float mass )
        {
            this.residue = residue;
            this.mass = mass;
        }

        /// <summary>
        /// A constructor that initalizes the object with a residue name
        /// that holds the modification and its modification mass.
        /// </summary>
        /// <param name="residue">A residue character that holds the modification</param>
        /// <param name="mass">Monoisotopic mass of the modification</param>
        /// <param name="title">Unimod title for the mod, if any</param>
        public HypotheticalModInfo( char residue, float mass, string title )
        {
            this.residue = residue;
            this.mass = mass;
            this.title = title;
        }

        /// <summary>
        /// CompareTo compares two hypothetical modifications iff the
        /// modified residue and the mass of the modification are the
        /// same.
        /// </summary>
        /// <param name="other">Another HypotheticalModInfo object</param>
        /// <returns>An int based on wheter the objects are equal or
        /// one is greater than or less than each other</returns>
        public int CompareTo( HypotheticalModInfo other )
        {
            // Compare the modified amino acid
            int residueCompare = residue.CompareTo( other.residue );
            // Compare the mass if the amino acid resiudes are the same
            if( residueCompare == 0 )
            {
                float massDiff = Math.Abs( mass - other.mass );
                if( massDiff < ModInfo.massTolerance )
                {
                    return 0;
                } else
                {
                    return mass.CompareTo( other.mass );
                }
            }
            return residueCompare;
        }

        /// <summary>
        /// This function takes another hypothetical modification object
        /// and checks to see if it is equal to the current object
        /// </summary>
        /// <param name="other">Another HypotheticalModInfo object</param>
        /// <returns>A bool value based on whether the objects are equal or not</returns>
        public bool Equals( HypotheticalModInfo other )
        {
            return ( this.CompareTo( other ) == 0 );
        }

        // Name of the modified residue
        public char residue;
        // Mass of the modification
        public float mass;
        // Unimod title
        public string title;
    }

    /// <summary>
    /// This class holds a list of <see cref="IDPicker.HypotheticalModInfo"/> objects.
    /// </summary>
    public class HypotheticalModSet : Set<HypotheticalModInfo>
    {
        public HypotheticalModSet()
        {
        }

        /// <summary>
        /// Parses out a string of modified_residue modification_mass pairs and creates a list
        /// of <see cref="IDPicker.HypotheticalModInfo"/> objects
        /// </summary>
        /// <para>
        /// <param name="cfgStr">Format of this string is a space delimited list of abstract mod info 
        ///  strings, of the form:
        /// "modified_residue" "modification_mass"
        /// "modified_residue" can be a single letter amino acid symbol, or 'n' or 'c' for N and C termini
        /// </param></para>
        public HypotheticalModSet( string cfgStr )
        {
            if( cfgStr.Trim().Length == 0 )
                return;
            cfgStr = cfgStr.Trim();

            string[] modInfoStrs = cfgStr.Split( " ".ToCharArray() );
            for( int i = 0; i < modInfoStrs.Length; i += 2 )
            {
                Add( new HypotheticalModInfo( modInfoStrs[i][0], Convert.ToSingle( modInfoStrs[i + 1] ) ) );
            }
        }
    }

    /// <summary>
    /// This class holds all the information about a modification seen on a protein.
    /// It holds the mass, location, residue, peptide, peptide group ID, protein, 
    /// and protein group ID of the modification
    /// </summary>
    public class ProteinModInfo : IComparable<ProteinModInfo>
    {
        /// <summary>
        /// Cluster ID of the whole protein and the peptides
        /// </summary>
        public int clusterID;

        /// <summary>
        /// Peptide and protein group ids that contain the peptides 
        /// with this modification 
        /// </summary>
        public int protGroupID;
        public int peptideGroupID;

        /// <summary>
        /// Protein locus 
        /// </summary>
        public string proteinID;

        /// <summary>
        /// Peptide start and stop for the protein 
        /// </summary>
        public int peptideStart;
        public int peptideStop;

        /// <summary>
        /// Peptide string annotated with the mods 
        /// </summary>
        public string peptideAnnotation;

        /// <summary>
        /// Modification mass, position, residue, and
        /// type of mod 
        /// </summary>
        public int modPosition;
        public float modMass;
        public string modResidue;

        /// <summary>
        /// Constructor of the ProteinModInfo object
        /// </summary>
        /// <param name="clusID">ClusterID of the protein and petpides with a particular mod</param>
        /// <param name="protID">ProteinGroup ID of the protein with a particular mod</param>
        /// <param name="pepID">PeptideGroupID of a peptide with a particular mod</param>
        /// <param name="protein">Protein locus of the protein with a particular mod</param>
        /// <param name="ann">Annotated peptide sequence (with all mods) that contains a particular mod</param>
        /// <param name="modPos">Position of the modification in the protein</param>
        /// <param name="mass">Mass of the modification</param>
        /// <param name="residue">Residue affected by the modification</param>
        /// <param name="pepStart">Peptide start of the peptide that contains the mod</param>
        /// <param name="pepStop">Peptide stop of the peptide that contains the mod</param>
        public ProteinModInfo( int clusID, int protID, int pepID, string protein, string ann, int modPos,
                                float mass, string residue, int pepStart, int pepStop )
        {
            clusterID = clusID;
            protGroupID = protID;
            peptideGroupID = pepID;
            proteinID = protein;
            peptideAnnotation = ann;
            modPosition = modPos;
            modMass = mass;
            modResidue = residue;
            peptideStart = pepStart;
            peptideStop = pepStop;
        }

        /// <summary>
        /// This function compares two ProteinModInfo objects on all the data fields except
        /// terminalMod
        /// </summary>
        /// <param name="other">Another <see cref="IDPicker.ProteinModInfo"/> object</param>
        /// <returns>An integer based on whether two protein modifications are equal or not</returns>
        public int CompareTo( ProteinModInfo other )
        {
            int clusterIDCompareTo = clusterID.CompareTo( other.clusterID );
            if( clusterIDCompareTo != 0 )
            {
                return clusterIDCompareTo;
            }

            int protClusCompareTo = protGroupID.CompareTo( other.protGroupID );
            if( protClusCompareTo != 0 )
            {
                return protClusCompareTo;
            }

            int pepClusCompareTo = peptideGroupID.CompareTo( other.peptideGroupID );
            if( pepClusCompareTo != 0 )
            {
                return pepClusCompareTo;
            }

            int proteinIDCompareTo = proteinID.CompareTo( other.proteinID );
            if( proteinIDCompareTo != 0 )
            {
                return proteinIDCompareTo;
            }

            int modPosCompareTo = modPosition.CompareTo( other.modPosition );
            if( modPosCompareTo != 0 )
            {
                return modPosCompareTo;
            }

            int modMassCompareTo = modMass.CompareTo( other.modMass );
            if( modMassCompareTo != 0 )
            {
                return modMassCompareTo;
            }

            int annotationCompareTo = peptideAnnotation.CompareTo( other.peptideAnnotation );
            if( annotationCompareTo != 0 )
            {
                return annotationCompareTo;
            }
            int modResidueCompareTo = modResidue.CompareTo( other.modResidue );
            return modResidueCompareTo;

        }
    }
    /// <summary>
    /// ModInfo class stores a modification in a peptide using the residue that has
    /// been modified, the position of the modified residue and the mass of the 
    /// modification. 
    /// </summary>
    public class ModInfo : IComparable<ModInfo>
    {
        /// <summary>
        /// massTolerance used while comparing two modification masses
        /// </summary>
        public static float massTolerance = 0.0f;

        /// <summary>
        /// A constructor for constructing a ModInfo object
        /// </summary>
        /// <param name="peptide">A <see cref="IDPicker.PeptideInfo"/> object</param>
        /// <param name="position">Position of the modification in the pepitde</param>
        /// <param name="mass">Mass of the modification</param>
        /// <remarks> The position of the modification is a interger 1-based character.
        /// N-terminal and C-terminal of the peptide are represented as 'n' and 'c' 
        /// respectively</remarks>
        public ModInfo( PeptideInfo peptide, char position, float mass )
        {
            if( position != 'n' && position != 'c' )
            {
                int positionValue = Convert.ToInt32( position );
                if( positionValue < 1 || positionValue > peptide.sequence.Length )
                    throw new InvalidDataException( String.Format( "mod position {0} is out of range for peptide {1}", positionValue, peptide.sequence ) );
                residue = peptide.sequence[positionValue - 1];
            }
            this.position = position;
            this.mass = mass;
        }

        /// <summary>
        /// This function takes an array of residues and their mod
        /// masses and check to see if the current mod object has
        /// belongs to any of them.
        /// </summary>
        /// <param name="residues"></param>
        /// <param name="masses"></param>
        /// <returns></returns>
        public bool isKnownMod( char[] residues, float[] masses )
        {
            bool isUnknownMod = false;
            // For each residue
            for( int i = 0; i < residues.Length; ++i )
            {
                // Get the mass match
                bool massMatch = Math.Abs( masses[i] - this.mass ) < 0.1f ? true : false;

                // Match the residues
                if( residues[i] != 'n' && residues[i] != 'c' )
                {
                    if( residues[i] == this.residue && massMatch )
                    {
                        return true;
                    }
                } else
                {
                    if( residues[i] == this.position && massMatch )
                    {
                        return true;
                    }
                }
            }
            return isUnknownMod;
        }
        /// <summary>
        /// Compares two ModInfo objects based on their positions
        /// </summary>
        /// <param name="other">A <see cref="IDPicker.ModInfo"/> object</param>
        /// <returns>An integer based on whether the positions (int-based)
        /// of two modifications in a peptide are same, less than or greater 
        /// than each other.</returns>
        public int CompareTo( ModInfo other )
        {
            int tmp1 = position;
            int tmp2 = other.position;

            // Assign 0 to the n-terminal and Int.MaxValue to the c-terminal
            if( tmp1 == 'n' )
                tmp1 = 0;
            else if( tmp1 == 'c' )
                tmp1 = int.MaxValue;

            if( tmp2 == 'n' )
                tmp2 = 0;
            else if( tmp2 == 'c' )
                tmp2 = int.MaxValue;

            // Compare the positions
            if( tmp1.CompareTo( tmp2 ) == 0 )
            {
                float massDiff = Math.Abs( mass - other.mass );
                if( massDiff < massTolerance )
                {
                    return 0;
                } else
                {
                    return mass.CompareTo( other.mass );
                }
            } else
            {
                return tmp1.CompareTo( tmp2 );
            }

            //return position.CompareTo(other.position);

        }

        /// <summary>
        /// This function takes modification position and mass pair and converts then
        /// into a string of following format:
        /// "mod1Pos mod1Mass mod2Pos mod2Mass....". N- and C-terminals are represented
        /// as "n" and "c" respectively.
        /// </summary>
        /// <returns>a string</returns>
        public override string ToString()
        {
            StringBuilder str = new StringBuilder();
            if( position == 'n' || position == 'c' )
            {
                str.AppendFormat( "{0} {1}", position, mass );
            } else
            {
                str.AppendFormat( "{0} {1}", residue, mass );
            }
            return str.ToString();
        }

        /// <summary>
        /// Converts a <see cref="IdPicker.ModInfo"/> object to <see cref="IdPicker.HypotheticalModInfo"/>
        ///  object
        /// </summary>
        /// <returns>A <see cref="IdPicker.HypotheticalModInfo"/> object using the current
        /// modification object residue and the mass.</returns>
        public HypotheticalModInfo ToHypotheticalModInfo()
        {
            return new HypotheticalModInfo( residue, mass );
        }

        // Character of the modified residue
        public char residue;
        // Integer 1-based offset of location of modification in the peptide.
        // 'n' and 'c' are used to represent N-terminal and c-terminal of the
        // peptide
        public char position;
        // Mass of the modification
        public float mass;
        // Unimod annotation of the modification
        public string title;

        /// <summary>
        /// This class sorts the ModInfo objects (See <see cref="IDPicker.ModInfo"/>) based
        /// on their amino acid residue that holds the modification and the mass of the
        /// modification. The position of the modification in the peptide is being ignored
        /// in the comparisons.
        /// </summary>
        public class SortByIgnoringPosition : IComparer<ModInfo>
        {

            /// <summary>
            /// This function compares two ModInfo (See <see cref="IDPicker.ModInfo"/>) objects
            /// based on the amino acid that holds the modification and the mass of the modification.
            /// </summary>
            /// <param name="lhs">Left hand side ModInfo object <see cref="IDPicker.ModInfo"/></param>
            /// <param name="rhs">Right hand side ModInfo object <see cref="IDPicker.ModInfo"/></param>
            /// <returns>An integer based on wheather the modification resiude and the mass are
            /// comparable or not</returns>
            public int Compare( ModInfo lhs, ModInfo rhs )
            {
                char lhsResidue = lhs.residue;
                char rhsResidue = rhs.residue;
                // Assign '(' for n-terminal mods and ')' for c-terminal mods
                // as the residue to be compared.
                if( lhs.position == 'n' )
                {
                    lhsResidue = '(';
                } else if( lhs.position == 'c' )
                {
                    lhsResidue = ')';
                }

                if( rhs.position == 'n' )
                {
                    rhsResidue = '(';
                } else if( rhs.position == 'c' )
                {
                    rhsResidue = ')';
                }


                // Compare by the residue first
                if( lhsResidue == rhsResidue )
                {
                    // Followed by mass
                    if( lhs.mass == rhs.mass )
                    {
                        return 0;
                    } else
                    {
                        return lhs.mass.CompareTo( rhs.mass );
                    }
                } else
                {
                    return lhsResidue.CompareTo( rhsResidue );
                }
            }
        }

        /// <summary>
        /// This class sorts the ModInfo objects (See <see cref="IDPicker.ModInfo"/>) based
        /// on the position and mass of the modification.
        /// </summary>
        public class SortByMassAndPosition : IComparer<ModInfo>
        {

            /// <summary>
            /// This function compares two ModInfo (See <see cref="IDPicker.ModInfo"/>) objects
            /// based on the mass and position of the modification in a peptide.
            /// </summary>
            /// <param name="lhs">Left hand side ModInfo object <see cref="IDPicker.ModInfo"/></param>
            /// <param name="rhs">Right hand side ModInfo object <see cref="IDPicker.ModInfo"/></param>
            /// <returns>An integer based on wheather the modification resiude and the mass are
            /// comparable or not</returns>
            public int Compare( ModInfo lhs, ModInfo rhs )
            {
                int lhsPosition = lhs.position;
                int rhsPosition = rhs.position;

                // Assign 0 to the n-terminal and Int.MaxValue to the c-terminal
                if( lhsPosition == 'n' )
                    lhsPosition = 0;
                else if( lhsPosition == 'c' )
                    lhsPosition = int.MaxValue;

                if( rhsPosition == 'n' )
                    rhsPosition = 0;
                else if( rhsPosition == 'c' )
                    rhsPosition = int.MaxValue;

                // Compare by the residue first
                if( lhsPosition == rhsPosition )
                {
                    // Followed by mass
                    if( lhs.mass == rhs.mass )
                    {
                        return 0;
                    } else
                    {
                        return lhs.mass.CompareTo( rhs.mass );
                    }
                } else
                {
                    return lhsPosition.CompareTo( rhsPosition );
                }
            }
        }
    }

    /// <summary>
    /// A class that maps a list of <see cref="IDPicker.ModInfo"/> objects to modification
    /// residue that holds those modifications.
    /// </summary>
    public class ModMap : Map<char, List<ModInfo>>
    {
        public ModMap()
        {
            numberOfMods = 0;
            modMasses = new ArrayList();
        }

        /// <summary>
        /// This function adds a modification to the list of
        /// modifications at a particular residue.
        /// </summary>
        /// <param name="pos">The position of the modification</param>
        /// <param name="mod">The modification as ModInfo <see cref="IDPicker.ModInfo"/> object.</param>
        public void addToList( char pos, ModInfo mod )
        {
            List<ModInfo> list = null;
            if( this.Contains( pos ) )
            {
                list = this.Find( pos ).Current.Value;
                this.Remove( pos );
            } else
            {
                list = new List<ModInfo>();
            }
            list.Add( mod );
            this.Add( pos, list );
        }

        /// <summary>
        /// This function compares two modification maps. The comparator compares
        /// the keys and also modifications.
        /// </summary>
        /// <param name="other">Another <see cref="IDPicker.ModInfo"/> object.</param>
        /// <returns>An integer based on whether two modification maps are equal</returns>
        public int CompareTo( ModMap other )
        {
            if( Count != other.Count )
            {
                return Count.CompareTo( other.Count );
            }
            int compare = 0;

            Enumerator lhsItr = GetEnumerator(); lhsItr.MoveNext();
            Enumerator rhsItr = other.GetEnumerator(); rhsItr.MoveNext();
            for( int i = 0; i < Count && compare == 0; ++i, lhsItr.MoveNext(), rhsItr.MoveNext() )
            {
                compare = lhsItr.Current.Key.CompareTo( rhsItr.Current.Key );
                if( lhsItr.Current.Value.Count != rhsItr.Current.Value.Count )
                {
                    return lhsItr.Current.Value.Count.CompareTo( rhsItr.Current.Value.Count );
                }
                List<ModInfo>.Enumerator lhsValueItr = lhsItr.Current.Value.GetEnumerator(); lhsValueItr.MoveNext();
                List<ModInfo>.Enumerator rhsValueItr = rhsItr.Current.Value.GetEnumerator(); rhsValueItr.MoveNext();
                for( int j = 0; j < lhsItr.Current.Value.Count && compare == 0; ++j, lhsValueItr.MoveNext(), rhsValueItr.MoveNext() )
                {
                    compare = lhsValueItr.Current.CompareTo( rhsValueItr.Current );
                }
            }
            return compare;
        }

        /// <summary>
        /// getMassAtResidue takes a location in the peptide and returns
        /// the total mass of all modifications together at that residue.
        /// </summary>
        /// <param name="res">A character representation of the modification position. 
        /// The format is 'n','c' for termimi and index based (starting from 1) for
        /// internal amino acids.</param>
        /// <returns>The total mass of all modifications present at the specified
        /// position.</returns>
        public float getMassAtResidue( char res )
        {
            float mass = 0.0f;
            // Get the list at the position
            List<ModInfo> modList = this[res];
            // Add up masses of all the mods at the position
            foreach( ModInfo mod in modList )
            {
                mass += mod.mass;
            }
            return mass;
        }

        /// <summary>
        /// This function returns if two modification maps can be
        /// collapsed and represented by one notation disregarding
        /// positional information of the modification masses. 
        /// </summary>
        /// <param name="anotherMap">Another <see cref="IDPicker.ModMap"/> object.</param>
        /// <returns>true of the two modification maps reperesent the same mods</returns>
        public bool isCollapsable( ModMap anotherMap )
        {
            // The number of mods in both maps have to be the same
            if( numberOfMods != anotherMap.numberOfMods )
            {
                return false;
            }

            // The mod masses have to be the same.
            for( int index = 0; index < modMasses.Count; index++ )
            {
                if( ( (float) modMasses[index] ) != ( (float) anotherMap.modMasses[index] ) )
                {
                    return false;
                }
            }
            return true;
        }

        public Map<string, int> extractPotentialSNPs( ResidueMaps residues, char[] knownModRes, float[] knownModMasses )
        {
            Map<string, int> retVals = new Map<string, int>();
            foreach( List<ModInfo> modList in Values )
            {
                foreach( ModInfo mod in modList )
                {
                    if( mod.isKnownMod( knownModRes, knownModMasses ) )
                    {
                        continue;
                    }
                    if( mod.position != 'n' && mod.position != 'c' )
                    {
                        float toAAMass = mod.mass + residues.aminoAcidToMass[mod.residue];
                        Map<float, char>.Enumerator itr = residues.massToAminoAcid.GetEnumerator();
                        while( itr.MoveNext() )
                        {
                            if( Math.Abs( itr.Current.Key - toAAMass ) < 0.1f )
                            {
                                retVals.Add( "" + mod.residue + itr.Current.Value, Convert.ToInt32( mod.position ) );
                            }
                        }
                    }
                }
            }
            return retVals;
        }

        /// <summary>
        /// This function calls the ToString(char delimiter) function with space as default
        /// delimiter
        /// </summary>
        public override string ToString()
        {
            return ToString(' ', ':');
        }

        /// <summary>
        /// This function a list of modifications and converts them into a string with following format:
        /// "mod1Pos:mod1Mass mod2Pos:mod2Mass ...". N-terminal and c-terminal positions are indicated
        /// as 'n' and 'c'.
        /// </summary>
        public string ToString( char listDelimiter, char pairDelimiter )
        {
            StringBuilder str = new StringBuilder();
            bool firstModOut = false;
            foreach( List<ModInfo> modList in Values )
            {
                foreach( ModInfo mod in modList )
                {
                    if( firstModOut )
                    {
                        str.Append(listDelimiter);
                    }

                    if( mod.position == 'n' )
                    {
                        str.AppendFormat("n{0}{1}", pairDelimiter, mod.mass);
                    } else if( mod.position == 'c' )
                    {
                        str.AppendFormat("c{0}{1}", pairDelimiter, mod.mass);
                    } else
                    {
                        str.AppendFormat("{0}{1}{2}", Convert.ToInt32(mod.position), pairDelimiter, mod.mass);
                        //str.AppendFormat( "{0}:{1}", mod.residue, mod.mass );
                    }
                    firstModOut = true;
                }
            }
            return str.ToString();
        }

        /// <summary>
        /// This function takes a set of residues and a set of mod masses
        /// and checks the ModMap to see if it contains any unknown mods.
        /// </summary>
        /// <param name="residues">Set of residues with known mods</param>
        /// <param name="masses">The known mod masses</param>
        /// <returns>Returns true if the mod map contains an unknown mod</returns>
        public string extractUnknownMod( char[] residues, float[] masses )
        {
            int numUnknownMods = 0;
            StringBuilder unknownMod = new StringBuilder();
            foreach( List<ModInfo> modList in Values )
            {
                foreach( ModInfo mod in modList )
                {
                    if( !mod.isKnownMod( residues, masses ) )
                    {
                        ++numUnknownMods;
                        string sign = "";
                        if( mod.mass > 0 )
                            sign = "+";
                        if( unknownMod.Length > 0 )
                            unknownMod.Append( ";" );
                        if( mod.position == 'n' || mod.position == 'c' )
                            unknownMod.Append( mod.position + "t" + sign + (int) Math.Round( mod.mass ) );
                        else
                            unknownMod.Append( mod.residue + sign + (int) Math.Round( mod.mass ) );
                    }
                }
            }
            if( numUnknownMods > 0 )
            {
                return unknownMod.ToString(); ;
            }
            return null;
        }

        /// <summary>
        /// This function checcs the modification map for a particular modification
        /// and counts its occurences.
        /// </summary>
        /// <param name="residue">modification amino acid</param>
        /// <param name="mass">modification mass</param>
        /// <returns>number of occurences of the mod</returns>
        public int countKnownMods( char residue, float mass )
        {
            int numMods = 0;
            foreach( List<ModInfo> modList in Values )
            {
                foreach( ModInfo mod in modList )
                {
                    if( mod.isKnownMod( new char[] { residue }, new float[] { mass } ) )
                        ++numMods;
                }
            }
            return numMods;
        }

        public string ModAnnotations()
        {
            StringBuilder str = new StringBuilder();
            foreach( List<ModInfo> modList in Values )
            {
                foreach( ModInfo mod in modList )
                {
                    str.Append( mod.title );
                }
            }
            return str.ToString();
        }

        ///<summary>Mass is a variable that stores the sum of all masses in a
        ///<see cref="IDPciker.ModInfo"/> list. The variable must only be accessed
        ///on a list of mod instances, not abstract mods.</summary>
        public float Mass
        {
            get
            {
                float mass = 0;
                // For each ModInfo list
                foreach( List<ModInfo> modList in Values )
                {
                    // For each modification
                    foreach( ModInfo mod in modList )
                    {
                        if( mod.position == 0 )
                        {
                            throw new InvalidDataException( "ModList.Mass must only be called on a list of mod instances" );
                        }
                        // Add the mod mass
                        mass += mod.mass;
                    }
                }
                // Return the cumulative mass
                return mass;
            }
        }

        /// <summary>
        /// numberOfMods holds the total number of modification in the modification
        /// map
        /// </summary>
        public int numberOfMods;

        /// <summary>
        /// modMasses stores all the masses seen in a modification map.
        /// </summary>
        public ArrayList modMasses;
    }

    /// <summary>
    /// VariantInfo class stores a peptide and the list of distinct modifications
    /// objects for the peptide.
    /// </summary>
    public class VariantInfo : IComparable<VariantInfo>
    {
        public VariantInfo()
        {
            mods = new ModMap();
            alternatives = new ArrayList();
            collapsableMods = new Hashtable();
            isDistinctPeptide = true;
        }

        /// <summary>
        /// Compares two peptide interpretations. Firstly, it compares the
        /// peptide objects. If the peptides are similar then it compares the
        /// modifications on the peptides.
        /// </summary>
        /// <param name="other">Another <see cref="IDPicker.VariantInfo"/> object.</param>
        /// <returns>An integer value based on whether the peptides are equal, less than
        /// or greater than each other.</returns>
        public int CompareTo( VariantInfo other )
        {
            int pepCompare = peptide.CompareTo( other.peptide );
            if( pepCompare == 0 )
                return mods.CompareTo( other.mods );
            return pepCompare;
        }

        /// <summary>
        /// This function takes a peptide and converts it into a InsPecT style interpretation.
        /// This style internalizes the modification masses next to the amino acids
        /// </summary>
        /// <returns>InsPecT style interpretation</returns>
        public string ToInsPecTStyle()
        {
            StringBuilder retStr = new StringBuilder();
            // Find all n-terminal mods and start appending
            // them to the string
            ModMap.Enumerator nMod = mods.Find( 'n' );
            if( nMod.IsValid )
            {
                float mass = mods.getMassAtResidue( 'n' );
                if( mass > 0.0f )
                    retStr.AppendFormat( "{0}{1}", "+", Math.Round( mass ) );
                else
                    retStr.AppendFormat( "{0}", Math.Round( mass ) );
            }
            // Start with the peptide sequence
            for( int r = 0; r < peptide.sequence.Length; ++r )
            {
                retStr.Append( peptide.sequence[r] );
                ModMap.Enumerator posMod = mods.Find( Convert.ToChar( r + 1 ) );
                if( posMod.IsValid )
                {
                    float mass = mods.getMassAtResidue( Convert.ToChar( r + 1 ) );
                    if( mass > 0.0f )
                        retStr.AppendFormat( "{0}{1}", "+", Math.Round( mass ) );
                    else
                        retStr.AppendFormat( "{0}", Math.Round( mass ) );
                }
            }
            // Append the c-terminal mods to the end.
            ModMap.Enumerator cMod = mods.Find( 'c' );
            if( cMod.IsValid )
            {
                float mass = mods.getMassAtResidue( 'c' );
                if( mass > 0.0f )
                    retStr.AppendFormat( "{0}{1}", "+", Math.Round( mass ) );
                else
                    retStr.AppendFormat( "{0}", Math.Round( mass ) );
            }
            return retStr.ToString();
        }

        /// <summary>
        /// Converts a peptide sequence and its modifications into
        /// a string. This string is displayed to user in the IDPicker report. An example
        /// string representation of peptide ILFEGNMEK with a methionine oxidation on M
        /// is ILFEGNM(15.996)EK. Terminal mods are placed either before or after the 
        /// n-terminal or c-terminal respectively.
        /// </summary>
        /// <returns>A string representation of a peptide with modifications, like ILFEGNM[15.996]EK </returns>
        public string ToSimpleString()
        {
            // Get a string buffer
            StringBuilder str = new StringBuilder();
            // Find all n-terminal mods and start appending
            // them to the string
            ModMap.Enumerator nMod = mods.Find( 'n' );
            if( nMod.IsValid )
                foreach( ModInfo mod in nMod.Current.Value )
                    str.AppendFormat( "[{0}]", mod.mass );

            // Start with the peptide sequence
            for( int r = 0; r < peptide.sequence.Length; ++r )
            {
                // For each amino acid append the residue, followed
                // by all modifications on that resiude. Mods are 
                // indexed from 1. So (r+1) has to be used in order
                // to place the mods after the right residue.
                str.Append( peptide.sequence[r] );
                ModMap.Enumerator posMod = mods.Find( Convert.ToChar( r + 1 ) );
                if( posMod.IsValid )
                    foreach( ModInfo mod in posMod.Current.Value )
                    {
                        str.AppendFormat( "[{0}]", mod.mass );
                    }
            }

            // Append the c-terminal mods to the end.
            ModMap.Enumerator cMod = mods.Find( 'c' );
            if( cMod.IsValid )
                foreach( ModInfo mod in cMod.Current.Value )
                    str.AppendFormat( "[{0}]", mod.mass );

            return str.ToString();
        }

        /// <summary>
        /// ToString functions converts a peptide sequence and its modifications into
        /// a string. This string is displayed to the user in the IDPicker report. An 
        /// example string representation of peptide ILFEGNMEK with a methionine 
        /// oxidation on M is ILFEGNM1EK{1=15.996}. 
        /// </summary>
        /// <returns>A string representation of a peptide with modifications, like 
        /// ILFEGNM1EK{1=15.996} </returns>
        public override string ToString()
        {
            // If the annotated sequence is already generated then just return that.
            // Generating the annotated sequence is computationally expensive. It's
            // only done once and used every time this function is called.
            if( annotatedSequence != null && annotatedSequence.Length > 0 )
            {
                return annotatedSequence;
            }
            // Collapse the ambiguous interpretations that can be represented
            // by a single peptide string format.
            this.collpaseInterpretations();
            String[] peptideStrings = this.generateModificationIndices();

            StringBuilder str = new StringBuilder();
            bool firstString = true;
            for( int representation = 0; representation < peptideStrings.Length; ++representation )
            {
                if( firstString )
                {
                    str.Append( peptideStrings[representation] );
                    firstString = false;
                } else
                {
                    str.AppendFormat( "{0}{1}", "/", peptideStrings[representation] );
                }
            }
            annotatedSequence = str.ToString();
            return annotatedSequence;
        }

        /// <summary>
        /// Returns the string representation of the ModMap object
        /// </summary>
        /// <returns>A string</returns>
        public string ModsToString( char listDelimiter, char pairDelimiter )
        {
            return mods.ToString(listDelimiter, pairDelimiter);
        }

        public string ModAnnotations()
        {
            return mods.ModAnnotations();
        }

        /// <summary>
        /// collapseInterpretations takes all the modification interpretations
        /// and identified ambiguous interpretations. Ambiguos interpretations
        /// by definition have same modifications but the location of modifications
        /// vary.
        /// </summary>
        public Hashtable collpaseInterpretations()
        {
            if( collapsableMods.Count > 0 )
            {
                return collapsableMods;
            }

            // Table to remember the processed interpretations
            Hashtable processedInterpretations = new Hashtable();
            // Get the modification maps from all the variants into
            // a single list
            ArrayList allModMaps = new ArrayList();
            allModMaps.Add( mods );
            foreach( VariantInfo alternative in alternatives )
            {
                allModMaps.Add( alternative.mods );
            }

            int index = 0;
            // While there are more modification maps
            while( allModMaps.Count > 0 )
            {
                // Get the first map
                ModMap currentModMap = (ModMap) allModMaps[0];
                ArrayList modMaps = new ArrayList();
                // See if it is not already processed
                if( !processedInterpretations.ContainsKey( currentModMap ) )
                {
                    //Add it to the processed list
                    modMaps.Add( currentModMap );
                    processedInterpretations.Add( currentModMap, 1 );
                    // Check to see if any other modification maps can be
                    // combined with the current modification map.
                    for( int next = 1; next < allModMaps.Count; next++ )
                    {
                        ModMap nextModMap = (ModMap) allModMaps[next];
                        //Console.WriteLine("Comparing..." + currentModMap.ToString() + "->" + nextModMap.ToString() + ":"+currentModMap.isCollapsable(nextModMap));
                        // If so, then add that to the collapsable list and proceed.
                        if( !processedInterpretations.ContainsKey( nextModMap ) && currentModMap.isCollapsable( nextModMap ) )
                        {
                            processedInterpretations.Add( nextModMap, 1 );
                            modMaps.Add( nextModMap );
                        }
                    }
                }
                if( modMaps.Count > 0 )
                {
                    collapsableMods.Add( index, modMaps );
                    index++;
                    // Remove the processed modification maps from
                    // the list.
                    foreach( ModMap mod in modMaps )
                    {
                        allModMaps.Remove( mod );
                    }
                }
            }

            /*Console.WriteLine("Peptide:"+peptide.sequence);
            foreach (DictionaryEntry entry in collapsableMods) {
                foreach (ModMap mod in ((ArrayList)entry.Value)) {
                    Console.WriteLine("\t"+ mod.ToString());
                }
                Console.WriteLine("Next Entry:");
            }*/
            return collapsableMods;
        }

        /// <summary>
        /// This function takes sets of ambiguous interpretations that can be represented
        /// as a single string and creates the modification indices for each modificaiton.
        /// These modification indices are used to generate an unified representation of
        /// all ambiguous modifications in a single string format.
        /// </summary>
        /// <returns>A list of string representation of the peptide with the modification
        /// indices placed at corresponding sites</returns>
        public String[] generateModificationIndices()
        {
            ArrayList retStrings = new ArrayList();
            // For each set of collapsable modifications
            foreach( DictionaryEntry ambiguousInterpretations in collapsableMods )
            {
                // Get the unique modifications and remember them
                Map<ModInfo, int> uniqueMods = new Map<ModInfo, int>( new ModInfo.SortByMassAndPosition() );
                ModMap uniqueModMap = new ModMap();
                // For each interpretation
                foreach( ModMap mods in ( (ArrayList) ambiguousInterpretations.Value ) )
                {
                    // Get the list of mods
                    foreach( List<ModInfo> modList in mods.Values )
                    {
                        // For each mod remember how many interpretations it was seen across. We consider 
                        // a modification as persistent if it is seem at the same position across all 
                        // interpretations. Such modifications will be given their own indices. Non-persistent
                        // modifications change their locations across the interpretations. They are given the
                        // same index across all interpretations.
                        // For example:
                        // Interp1: n:43 6:15.996 10:15.996
                        // Interp2: 1:43 6:15.996 10:15.996
                        // Modifications at locations 6 and 10 are persistent and given separate indices, where 
                        // are the modification of mass 43 at n and 1 are non-persistent and given the same 
                        // index.
                        foreach( ModInfo mod in modList )
                        {
                            if( !uniqueMods.Contains( mod ) )
                            {
                                uniqueMods.Add( mod, 1 );
                                uniqueModMap.addToList( mod.position, mod );
                            } else
                            {
                                int value = ( (int) uniqueMods[mod] );
                                uniqueMods.Remove( mod );
                                uniqueMods.Add( mod, value + 1 );
                            }
                        }
                    }
                }

                // Generate the indices here.
                Map<ModInfo, int> modIndices = new Map<ModInfo, int>( new ModInfo.SortByMassAndPosition() );
                // Holds the mass to index map for non-presistent modifications
                Map<float, int> modMasses = new Map<float, int>();
                Map<int, float> modIndexToMassMap = new Map<int, float>();
                int index = 1;
                // For each unique modification
                foreach( Map<ModInfo, int>.MapPair pair in uniqueMods )
                {
                    ModInfo mod = (ModInfo) pair.Key;
                    // Check if the modification is persistent
                    if( ( (int) uniqueMods[mod] ) == ( (ArrayList) ambiguousInterpretations.Value ).Count )
                    {
                        // Give it a separate index
                        if( !modIndices.Contains( mod ) )
                        {
                            modIndices.Add( mod, index );
                            modIndexToMassMap.Add( index, mod.mass );
                            index++;
                        }
                    } else
                    {
                        // Give it a separate index if the non-persistent modification
                        // has not been seen before. Otherwise, get the pre-assigned
                        // index and give that to it.
                        if( modMasses.Contains( mod.mass ) && !modIndices.Contains( mod ) )
                        {
                            modIndices.Add( mod, modMasses[mod.mass] );
                        } else if( !modIndices.Contains( mod ) )
                        {
                            modIndices.Add( mod, index );
                            modMasses.Add( mod.mass, index );
                            modIndexToMassMap.Add( index, mod.mass );
                            index++;
                        }
                    }
                }

                /*foreach (Map<ModInfo,int>.MapPair entry in modIndices) {
                    ModInfo mod = (ModInfo)entry.Key;
                    Console.WriteLine("\t" + mod.ToString() + "," + ((int)modIndices[mod]));
                }*/
                retStrings.Add( this.buildStringRepresentation( modIndices, uniqueModMap, modIndexToMassMap ) );
            }

            return (String[]) retStrings.ToArray( typeof( String ) );
        }

        /// <summary>
        /// This function takes a set of modification indices, a modification map, and a modification index
        /// to modification mass map and generates a string representation of the modified peptide sequence.
        /// For example:
        /// For modifications 6:57.03, 9:-155.791 in peptide GPPVSCIKR
        /// The function takes the indices for the modifications (6:57.03->1, 8:-155.791->2, 9:-155.791->2), 
        /// a modification (6:57.03, 8:-155.791, 9:-155.791), a modification index to mass map 1->57.03,2->-155.791 
        /// and creates a string GPPVSC1IK2R2{1=57.03,2=-155.791}.
        /// </summary>
        /// <param name="modIndices">A map of modification info object to their corresponding indices </param>
        /// <param name="uniqueModMap">A map of unique modifications for a set of ambiguous interpretations</param>
        /// <param name="modIndexToMassMap">A map of modification index to modification mass</param>
        /// <returns>A html compatible string representation of the sequence</returns>
        public String buildStringRepresentation( Map<ModInfo, int> modIndices, ModMap uniqueModMap, Map<int, float> modIndexToMassMap )
        {
            // Get a string buffer
            StringBuilder str = new StringBuilder();
            // Find all n-terminal mods and start appending
            // them to the string
            ModMap.Enumerator nMod = uniqueModMap.Find( 'n' );
            if( nMod.IsValid )
            {
                bool firstMod = true;
                // The class index is defined in idpicker-style.css.
                // This class defined the format of the font to be
                // used for the characters of the index class.
                //str.Append("<span class=\"index\">");
                foreach( ModInfo mod in nMod.Current.Value )
                {
                    if( firstMod )
                    {
                        str.AppendFormat( "{0}", modIndices[mod] );
                        firstMod = false;
                    } else
                    {
                        str.AppendFormat( "{0}{1}", ";", modIndices[mod] );
                    }
                }
                //str.Append("</span>");
            }

            // Start with the peptide sequence
            for( int r = 0; r < peptide.sequence.Length; ++r )
            {
                // For each amino acid append the residue, followed
                // by all modifications on that resiude. Mods are 
                // indexed from 1. So (r+1) has to be used in order
                // to place the mods after the right residue.
                str.Append( peptide.sequence[r] );
                ModMap.Enumerator posMod = uniqueModMap.Find( Convert.ToChar( r + 1 ) );
                if( posMod.IsValid )
                {
                    bool firstMod = true;
                    //str.Append("<span class=\"index\">");
                    foreach( ModInfo mod in posMod.Current.Value )
                    {
                        if( firstMod )
                        {
                            str.AppendFormat( "{0}", modIndices[mod] );
                            firstMod = false;
                        } else
                        {
                            str.AppendFormat( "{0}{1}", ";", modIndices[mod] );
                        }
                    }
                    //str.Append("</span>");
                }
            }

            // Append the c-terminal mods to the end.
            ModMap.Enumerator cMod = uniqueModMap.Find( 'c' );
            if( cMod.IsValid )
            {
                bool firstMod = true;
                //str.Append("<span class=\"index\">");
                foreach( ModInfo mod in cMod.Current.Value )
                {
                    if( firstMod )
                    {
                        str.AppendFormat( "{0}", modIndices[mod] );
                        firstMod = false;
                    } else
                    {
                        str.AppendFormat( "{0}{1}", ";", modIndices[mod] );
                    }
                }
                //str.Append("</span>");
            }

            // Return the string if there are no modifications
            if( modIndexToMassMap.Count == 0 )
            {
                return str.ToString();
            }

            // Write the index to mass map
            bool firstIndex = true;
            // The indexAnn class is same as the index class
            // This CSS class specifies the formating to be
            // used for the character
            //str.Append("<span class=\"indexAnn\">");
            str.Append( "{" );
            foreach( Map<int, float>.MapPair pair in modIndexToMassMap )
            {
                if( firstIndex )
                {
                    str.AppendFormat( "{0}{1}{2}", pair.Key, "=", pair.Value );
                    firstIndex = false;
                } else
                {
                    str.AppendFormat( "{0}{1}{2}{3}", ";", pair.Key, "=", pair.Value );
                }
            }
            str.Append( "}" );
            //str.Append("</span>");

            return str.ToString();
        }

        /// <summary>
        /// Mass variable is a sum of peptide neutral mass and the total mass of
        /// all modifications seen for that peptide.
        /// </summary>
        public float Mass
        {
            get
            {
                return peptide.mass + mods.Mass;
            }
        }
        /// <summary>
        /// peptide stores an instance of <see cref="IDPicker.PeptideInfo"/>.
        /// </summary>
        public PeptideInfo peptide;
        /// <summary>
        /// mods stores an instance of <see cref="IDPicker.ModMap"/>.
        /// This variable stores distinct mods seen for the peptide.
        /// </summary>
        public ModMap mods;
        /// <summary>
        /// alternatives stores the ambiguous modification locations.
        /// </summary>
        public ArrayList alternatives;
        /// <summary>
        /// collapsableMods combines all the modification interpretations
        /// that have the same modification masses but different locations.
        /// These type of modification interpretations are called as ambiguous 
        /// inpterpretations.
        /// </summary>
        public Hashtable collapsableMods;
        /// <summary>
        /// isDistinctPeptide is true if the peptide doesn't have indistinct mods.
        /// </summary>
        public bool isDistinctPeptide;
        /// <summary>
        /// annotatedSequence stores the annotated peptide sequence. This
        /// sequence is generated only once using ToString function. 
        /// </summary>
        public string annotatedSequence;
    }

    /// <summary>
    /// Class VariantList stores a list of peptide variants. Each peptide variant
    /// represents a peptide interpretation.
    /// </summary>
    public class VariantList : Set<VariantInfo>
    {
    }

    /// <summary>
    /// PeptideInfo class stores the id, sequence, neutral mass of the peptide.
    /// It also stores a list of interpretations <see cref="IDPicker.VariantList"/>
    /// that map spectra to that peptide.
    /// </summary>
    public class PeptideInfo : IComparable<PeptideInfo>
    {
        public PeptideInfo()
        {
            variants = new VariantList();
            proteins = new ProteinInstanceList();
        }

        /// <summary>
        /// Compraes two peptides based on their id. If the ids are the
        /// same then compares their amino acid sequences.
        /// </summary>
        /// <param name="other">Another <see cref="IDPicker.PeptideInfo"/> object.</param>
        /// <returns>An integer value based on whether the peptides are equal, less than
        /// or greater than each other.</returns>
        public int CompareTo( PeptideInfo other )
        {
            if( id > 0 && other.id > 0 && id == other.id )
                return 0;
            return sequence.CompareTo( other.sequence );
        }

        /// <summary>
        /// id stores the unique identifier for this peptide. This identifier is a
        /// system generated interger of increasing value.
        /// </summary>
        public int id;
        /// <summary>
        /// sequence stores the amino acid sequence of the peptide in <see cref="System.string"/>
        /// format.
        /// </summary>
        public string sequence;
        /// <summary>
        /// mass stores the neutral mass of the peptide.
        /// </summary>
        public float mass;
        /// <summary>
        /// unique stores whether the peptide is unique or not.
        /// </summary>
        public bool unique;

        /// <summary>
        /// true if the N terminus of the peptide is expected according to proteolysis
        /// </summary>
        public bool NTerminusIsSpecific;

        /// <summary>
        /// true if the C terminus of the peptide is expected according to proteolysis
        /// </summary>
        public bool CTerminusIsSpecific;

        /// <summary>
        /// variants is a list of peptide interpretations (see <see cref="IDPicker.VariantList"/>).
        /// </summary>
        public VariantList variants;
        /// <summary>
        /// List of proteins that matched to this peptide. (see <see cref="IDPicker.ProteinInstanceList"/>).
        /// </summary>
        public ProteinInstanceList proteins;
    }

    /// <summary>
    /// ProteinInfo class stores the information about the protein, peptides that were identified
    /// in the protein, and the spectra that matched to the identified peptides.
    /// </summary>
    public class ProteinInfo : IComparable<ProteinInfo>
    {
        public ProteinInfo()
        {
            results = new ResultList();
            peptides = new PeptideList();
            spectra = new SpectrumList();
        }

        /// <summary>
        /// Compares the system identifier and the locus of two proteins.
        /// </summary>
        /// <param name="other">A <see cref="IDPicker.ProteinInfo"/> object that holds
        /// information about a protein. </param>
        /// <returns>An integer value based on whether the proteins are equal, less than
        /// or greater than each other.</returns>
        public int CompareTo( ProteinInfo other )
        {
            // Return 0 if the ids are the same
            if( id > 0 && other.id > 0 && id == other.id )
                return 0;
            // Otherwise compare the locus
            return locus.CompareTo( other.locus );
        }

        /// <summary>
        /// A method that overrides the normal ToString method
        /// </summary>
        /// <returns>Locus of the protein</returns>
        public override string ToString()
        {
            return locus;
        }

        public string getSwissProtAccession()
        {
            Match match = Regex.Match( locus, @"SWISS\-PROT:(.*).", RegexOptions.IgnoreCase );
            if( match.Success )
            {
                string[] rest = match.Groups[1].Value.Split( new char[] { '|' } );
                return rest[0];
            }
            return null;
        }

        public string getIPIAccession()
        {
            Match match = Regex.Match( locus, @"(IPI[0-9]+)\.[0-9]", RegexOptions.IgnoreCase );
            if( match.Success )
            {
                return match.Groups[1].Value;
            }
            return null;
        }

        /// <summary>
        /// Coverage variable computes the percent coverage of the protein
        /// </summary>
        public float Coverage
        {
            get
            {
                if( length == 0 )
                    return 0;
                // Get an int array equal to protein length.
                int[] coverageMask = new int[length];

                // For each peptide
                foreach( VariantInfo pep in peptides )
                {
                    // Get the start and stop
                    int startOffset = pep.peptide.proteins[locus].offset;
                    int stopOffset = startOffset + pep.peptide.sequence.Length - 1;
                    if( stopOffset >= length )
                    {
                        continue;
                    }
                    // Increment the coverage array
                    for( int i = startOffset; i <= stopOffset; ++i )
                    {
                        ++coverageMask[i];
                    }
                }

                int covered = 0;
                // For the whole protein length get the number
                // of residues that have been covered by a peptide
                // identification
                for( int i = 0; i < length; ++i )
                {
                    if( coverageMask[i] > 0 )
                    {
                        ++covered;
                    }
                }
                // Return the total residues covered / length of the protein
                return (float) covered / length;
            }
        }

        /// <summary>
        /// id stores the system generated identifier for the protein
        /// </summary>
        public int id;
        /// <summary>
        /// locus stores the acession string of the protein
        /// </summary>
        public string locus;
        /// <summary>
        /// length stores the length of the protein
        /// </summary>
        public int length;
        /// <summary>
        /// isDecoy is a flag, when true represents a reverse protein
        /// </summary>
        public bool isDecoy;
        /// <summary>
        /// description is a string that represents the description of the protein
        /// </summary>
        public string description;

        public ResultList results;
        //public ProteinList proteins;
        /// <summary>
        /// peptides (see <see cref="IDPicker.PeptideList"/>) stores a list
        /// of peptide identifications for the protein
        /// </summary>
        public PeptideList peptides;
        /// <summary>
        /// spectra is a list of spectrum that matched to the protein.
        /// spectra is of type (see <see cref="IDPicker.SpectrumList"/>).
        /// </summary>
        public SpectrumList spectra;
        public ProteinGroupInfo proteinGroup;
    }

    /// <summary>
    /// An instance of a protein sequence. This class stores the ProteinInfo 
    /// (See <see cref="IDPicker.ProteinInfo"/>) of a protein and the offset.
    /// </summary>
    public class ProteinInstanceInfo : IComparable<ProteinInstanceInfo>
    {
        public int CompareTo( ProteinInstanceInfo other )
        {
            return protein.CompareTo( other.protein );
        }

        public override string ToString()
        {
            return protein.ToString();
        }

        /// <summary>
        /// offset is index of the protein
        /// </summary>
        public int offset;
        /// <summary>
        /// protein is a <see cref="IDPicker.ProteinInfo"/> object.
        /// </summary>
        public ProteinInfo protein;
    }

    /// <summary>
    /// This class holds a cluster of peptides, their spectral matches,
    /// proteins that identified the peptides, and information of about the
    /// proteins that were identified by the peptides.
    /// </summary>
    public class PeptideGroupInfo : IComparable<PeptideGroupInfo>
    {
        public PeptideGroupInfo( ResultInfo parts )
        {
            // Init the members
            cluster = 0;
            results = new ResultList();
            proteins = new ProteinList();
            proteinGroups = new ProteinGroupList();

            // For each peptide interpretation
            foreach( VariantInfo pep in parts.peptides )
            {
                // Get the proteins that contain the peptide
                foreach( ProteinInstanceList.MapPair proItr in pep.peptide.proteins )
                {
                    // Create a map of proteinLocus and ProteinInfo object.
                    proteins[proItr.Key] = proItr.Value.protein;
                }
            }
            //spectra = new SpectrumList();
            //spectra = parts.Values[0].spectra;
            /*foreach( ProteinInfo pro in parts.Values )
            {
                if( pro != parts.Values[0] )
                foreach( SpectrumInfo s in pro.spectra.Values )
                    if( !spectra.ContainsKey( s.id ) )
                        spectra.Add( s.id, s );
            }*/
        }

        /// <summary>
        /// Sorts two peptide groups based on their id and the proteins
        /// they have been mapped.
        /// </summary>
        /// <param name="other">A <see cref="IDPicker.PeptideGroupInfo"/> object. </param>
        /// <returns></returns>
        public int CompareTo( PeptideGroupInfo other )
        {
            // Compare the ids
            if( id > 0 && other.id > 0 && id == other.id )
                return 0;
            // Compare the protein lists
            return proteins.CompareTo( other.proteins );
        }

        /// <summary>
        /// Returns a string of groupID:proteinList. See ProteinList.ToString() for
        /// how a protein list is formatted for the string.
        /// </summary>
        /// <returns>A string </returns>
        public override string ToString()
        {
            return id + ":" + proteins.ToString();
        }

        /// <summary>
        /// cluster is the identifier for the peptide cluster
        /// </summary>
        public int cluster;
        /// <summary>
        /// id is the system generated identifier for the peptide group
        /// </summary>
        public int id;
        /// <summary>
        /// results is a set of ResultInfo <see cref="IDPicker.ResultList"/> objects
        /// that map a set of spectra to a peptide.
        /// </summary>
        public ResultList results;
        /// <summary>
        /// proteins maps a protein locus to its ProteinInfo object 
        /// (See <see cref="IDPicker.ProteinList"/>).
        /// </summary>
        public ProteinList proteins;
        /// <summary>
        /// proteinGroups holds the set of proteins that contain the peptides
        /// in the cluster. (See <see cref="IDPicker.ProteinGroupList"/>).
        /// </summary>
        public ProteinGroupList proteinGroups;
        //public SpectrumList spectra;
    }

    /// <summary>
    /// ProteinGroupInfo class stores the list of results (see <see cref="IDPicker.ResultList"/>)
    /// associated with a group of proteins, the proteins in the group (see <see cref="IDPicker.ProteinList"/>),
    /// spectra that mapped to peptides (see <see cref="IDPicker.SpectrumList"/>) of proteins in the group,
    /// peptides that were identified to the proteins in the group (see <see cref="IDPicker.PeptideGroupList"/>),
    /// and the list of sources (see <see cref="IDPicker.SourceGroupList"/>) that have identified the proteins 
    /// present in the group.
    /// </summary>
    public class ProteinGroupInfo : IComparable<ProteinGroupInfo>
    {
        public ProteinGroupInfo()
        {
        }

        /// <summary>
        /// This constructor takes a result list (See <see cref="IDPicker.ResultList"/>) and adds 
        /// the results to this protein group.
        /// </summary>
        /// <param name="parts">A <see cref="IDPicker.ResultList"/> object</param>
        public ProteinGroupInfo( ResultList parts )
        {
            // Init the variables.
            cluster = 0;
            uniquePeptideCount = 0;
            results = new ResultList();
            sourceGroups = new SourceGroupList();
            proteins = new ProteinList();
            spectra = new SpectrumList();
            peptideGroups = new PeptideGroupList();

            // Access each result 
            foreach( ResultInfo r in parts )
            {
                // Add it to the list
                results.Add( r );
                //Get the spectra that contained in the the result set.
                foreach( SpectrumList.MapPair sItr in r.spectra )
                {
                    // Add them to the list.
                    SpectrumList.InsertResult rv = spectra.Insert( sItr );
                    if( rv.WasInserted )
                    {
                        // Add the source of the spectra to the source groups if the spectra has
                        // been inserted succesfully.
                        sourceGroups.Add( sItr.Value.id.source.group.name, sItr.Value.id.source.group );
                    }
                }
                /*foreach( PeptideInfo pep in r.peptides.Values )
                {
                    peptides.Add( pep.sequence, pep );
                    foreach( SpectrumInfo s in pep.spectra.Values )
                        if( !spectra.ContainsKey( s.id ) )
                            spectra.Add( s.id, s );
                }*/
            }
        }

        /// <summary>
        /// Sorts the ProteinGroupInfo objects based on their id or
        /// on the rank of their results.
        /// </summary>
        /// <param name="other">A ProteinGroupInfo (See <see cref="IDPicker.ProteinGroupInfo"/>) object.</param>
        /// <returns>An int based on whether two protein groups are equal or not</returns>
        public int CompareTo( ProteinGroupInfo other )
        {
            if( id > 0 && other.id > 0 && id == other.id )
                return 0;
            return -results.CompareTo( other.results );
        }

        /// <summary>
        /// Returns a concatenated string of id:results.ToString(). 
        /// result.ToString() returns a list of comma-separated
        /// peptides.
        /// </summary>
        /// <returns>A string</returns>
        public override string ToString()
        {
            return id + ":" + results.ToString();
        }

        /// <summary>
        /// cluster is the identifier for the protein cluster
        /// </summary>
        public int cluster;
        /// <summary>
        /// id is the system generated identifier for the protein group
        /// </summary>
        public int id;
        /// <summary>
        /// uniquePeptideCount is the number of unique peptides identified
        /// by the proteins in the group.
        /// </summary>
        public int uniquePeptideCount;
        /// <summary>
        /// results contain the set of results that contain peptides which 
        /// identified the proteins in the group
        /// </summary>
        public ResultList results;
        /// <summary>
        /// sourceGroups is a list of sources that have identified the proteins
        /// in the group.
        /// </summary>
        public SourceGroupList sourceGroups;
        /// <summary>
        /// A list of proteins in the cluster
        /// </summary>
        public ProteinList proteins;
        /// <summary>
        /// A list of spectra that mapped to the peptides of the proteins 
        /// in the group.
        /// </summary>
        public SpectrumList spectra;
        /// <summary>
        /// A list of peptides that were mapped to the proteins in the 
        /// group.
        /// </summary>
        public PeptideGroupList peptideGroups;
    }

    /// <summary>
    /// ClusterInfo represents a cluster of protein identifications, peptides that map to the proteins,
    /// a list of result sets that map peptides to spectrum (see <see cref="IDPicker.ResultList"/>), and
    /// a list of protein identifications mapped to peptides, and corresponding spectra (See <see cref="IDPicker.ProteinList"/>).
    /// </summary>
    public class ClusterInfo
    {
        public ClusterInfo()
        {
            id = 0;
            proteinGroups = new ProteinGroupList();
            peptideGroups = new PeptideGroupList();
            results = new ResultList();
            proteins = new ProteinList();
        }

        /// <summary>
        /// id is a system generated cluster identification
        /// </summary>
        public int id;
        /// <summary>
        /// proteinGroups is a list of protein group info objects. (See <see cref="IDPicker.ProteinGroupInfo"/>).
        /// </summary>
        public ProteinGroupList proteinGroups;
        /// <summary>
        /// peptideGroups is a list of peptides that mapped to the proteins
        /// in the group. (See <see cref="IDPicker.PeptideGroupList"/>).
        /// </summary>
        public PeptideGroupList peptideGroups;
        /// <summary>
        /// results is a list of results that map spectra to the interpretations,
        /// peptides and proteins.
        /// </summary>
        public ResultList results;
        /// <summary>
        /// proteins is a list of protein information objects. (See <see cref="IDPicker.ProteinInfo"/>).
        /// </summary>
        public ProteinList proteins;
    }

    /// <summary>
    /// ProcessingParam stores a parameter tuple (name, value)
    /// </summary>
    public class ProcessingParam
    {
        public string name;
        public string value;
    }

    /// <summary>
    /// ProcessingEvent stores a list of processing parameters, type of
    /// the process, start time, and end time.
    /// </summary>
    public class ProcessingEvent
    {
        public ProcessingEvent()
        {
            parameters = new List<ProcessingParam>();
        }


        /// <summary>
        /// type represents a string type of the process
        /// </summary>
        public string type;
        /// <summary>
        /// startTime (see <see cref="System.DateTime"/>) of the process
        /// </summary>
        public DateTime startTime;
        /// <summary>
        /// endTime (see <see cref="System.DateTime"/>) of the process
        /// </summary>
        public DateTime endTime;
        /// <summary>
        /// parameters is a list of processing parameters (See <see cref="IDPicker.ProcessingParam"/>).
        /// </summary>
        public List<ProcessingParam> parameters;
    }

    /// <summary>
    /// SourceInfo stores a list of spectra and the processing events that 
    /// have been done on the list of spectra.
    /// </summary>
    public class SourceInfo : IComparable<SourceInfo>
    {
        /// <summary>
        /// Initalize a new spectra source
        /// </summary>
        public SourceInfo()
        {
            spectra = new SpectrumList();
            processingEvents = new List<ProcessingEvent>();
        }

        /// <summary>
        /// Initialize a new spectra source with an existing group.
        /// </summary>
        /// <param name="group">A <see cref="IDPicker.SourceGroupInfo"/> object.</param>
        /// <param name="name">Name of the source.</param>
        public SourceInfo( SourceGroupInfo group, string name )
        {
            spectra = new SpectrumList();
            processingEvents = new List<ProcessingEvent>();
            this.group = group;
            this.name = name;
        }

        /// <summary>
        /// This function compares the source based on their 
        /// names.
        /// </summary>
        /// <param name="other">A <see cref="IDPicker.SourceInfo"/> object.</param>
        /// <returns>An int value based on the source name comparison</returns>
        public int CompareTo( SourceInfo other )
        {
            if( group.name == other.group.name )
                return name.CompareTo( other.name );
            else
                return group.name.CompareTo( other.group.name );
        }

        /// <summary>
        /// Returns the source name.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if( group.isRootGroup() )
                return name;
            return group.name + "/" + name;
        }

        /// <summary>
        /// filepath  stores the file of the source
        /// </summary>
        public string filepath;
        /// <summary>
        /// name stores the name of the source
        /// </summary>
        public string name;
        /// <summary>
        /// group stores the tree hierarchy of the sources to which
        /// this source belongs.
        /// </summary>
        public SourceGroupInfo group;
        /// <summary>
        /// spectra stores the information about spectra in the group.
        /// </summary>
        public SpectrumList spectra;
        /// <summary>
        /// processingEvents stores the list of processing parameters of
        /// used to process the spectra in the source file.
        /// </summary>
        public List<ProcessingEvent> processingEvents;
        /// <summary>
        /// DynamicMods stores the list of dynamic mods in the source file
        /// </summary>
        public string DynamicMods;
        /// <summary>
        /// StaticMods stores the list of static mods in the source file
        /// </summary>
        public string StaticMods;
    }

    /// <summary>
    /// This class stores the list of sources that belong to a group and the list
    /// of sources that are children to a source.
    /// </summary>
    public class SourceGroupInfo : IComparable<SourceGroupInfo>
    {
        public SourceGroupInfo()
        {
            parent = null;
            sources = new SourceList();
            children = new SourceGroupList();
        }

        /// <summary>
        /// Returns the parent group path given a group name.
        /// </summary>
        /// <param name="name">Name of a group</param>
        /// <returns>Path of the parent of the group</returns>
        public static string getParentGroupPath( string name )
        {
            if( isRootGroup( name ) )
                return "/";

            string path = name.Substring( 0, name.LastIndexOf( "/" ) );
            if( path.Length > 0 )
                return path;
            return "/";
        }

        public static Set<string> getAllParentGroupNames(string name)
        {
            Set<string> parentGroups = new Set<string>();
            string parentGroup = getParentGroupPath(name);
            parentGroups.Add(parentGroup);
            while(parentGroup != "/")
            {
                parentGroup = getParentGroupPath(parentGroup);
                parentGroups.Add(parentGroup);
            }
            return parentGroups;
        }

        /// <summary>
        /// Returns the name of the group given a full path
        /// </summary>
        /// <param name="name">Path of the group</param>
        /// <returns>Name of the group</returns>
        public static string getGroupName( string name )
        {
            if( isRootGroup( name ) )
                return "/";

            return name.Substring( name.LastIndexOf( "/" ) + 1 );
        }

        /// <summary>
        /// Returns the depth of the group given a group path
        /// </summary>
        /// <param name="name">Path of a group</param>
        /// <returns>Depth of the group</returns>
        public static int getGroupPathDepth( string name )
        {
            if( isRootGroup( name ) )
                return 0;

            int pathDepth = 0;
            for( int i = 0; i < name.Length; ++i )
                if( name[i] == '/' )
                    ++pathDepth;
            return pathDepth;
        }

        public string getParentGroupPath() { return getParentGroupPath(name); }
        public Set<string> getAllParentGroupPaths() { return getAllParentGroupNames(name); }
        public string getGroupName() { return getGroupName(name); }
        public int getGroupPathDepth() { return getGroupPathDepth(name); }
        /// <summary>
        /// Compares source groups based on their names.
        /// </summary>
        /// <param name="other">A <see cref="IDPicker.SourceGroupInfo"/> object.</param>
        /// <returns>An int value based on the name comparison</returns>
        public int CompareTo (SourceGroupInfo other)
        {
            return name.CompareTo(other.name);
        }

        public static bool isRootGroup( string name ) { return name == "/"; }
        public bool isRootGroup() { return isRootGroup( name ); }
        public bool isLeafGroup() { return children.Count == 0; }
        public SourceList getSources() { return getSources( false ); }
        public SourceGroupList getChildGroups() { return getChildGroups( false ); }

        /// <summary>
        /// Get all the sources in the source
        /// </summary>
        /// <param name="recursive">A flag to enable recursive tracing of sources of the children</param>
        /// <returns>A list of sources</returns>
        public SourceList getSources( bool recursive )
        {
            SourceList someSources = new SourceList( sources );
            if( recursive )
                foreach( SourceGroupInfo group in children.Values )
                    someSources.Union( group.getSources( true ) );
            return someSources;
        }

        public void setSources( SourceList sources )
        {
            this.sources = sources;
        }

        /// <summary>
        /// Get all the direct children of the source group
        /// </summary>
        /// <param name="recursive">A flag to enable recursive tracing of children the children</param>
        /// <returns>A list of children of this source</returns>
        public SourceGroupList getChildGroups( bool recursive )
        {
            SourceGroupList someChildGroups = children;
            if( recursive )
                foreach( SourceGroupInfo group in children.Values )
                {
                    SourceGroupList someMoreChildGroups = group.getChildGroups( true );
                    foreach( SourceGroupInfo childGroup in someMoreChildGroups.Values )
                        someChildGroups.Add( childGroup.name, childGroup );
                }
            return someChildGroups;
        }

        /// <summary>
        /// name of the source
        /// </summary>
        public string name;
        /// <summary>
        /// parent of this source group
        /// </summary>
        public SourceGroupInfo parent;
        /// <summary>
        /// direct children of this group
        /// </summary>
        internal SourceGroupList children;
        /// <summary>
        /// sources that belong directly to this group
        /// </summary>
        internal SourceList sources;
    }

    public class ResultInstanceList : SortedList<int, ResultInstance>
    {
    }

    /// <summary>
    /// ResultList is contains a set of ResultInfo (see <see cref="IDPicker.ResultInfo"/>) objects.
    /// Result info maps a set of peptide identifications to a set of spectra.
    /// </summary>
    public class ResultList : Set<ResultInfo>
    {
        /// <summary>
        /// Returns the number of results in the list
        /// </summary>
        /// <returns>A int-based count</returns>
        public int Counts()
        {
            return this.Count;
        }

        public int uniqueResultCount
        {
            get
            {
                int count = 0;
                foreach( ResultInfo res in this )
                {
                    if( res.isDistinctPeptide )
                    {
                        ++count;
                    }
                }
                return count;
            }
        }

        /// <summary>
        /// ResultFDR is a variable that computes the false discovery rate (FDR) for
        /// the result set.
        /// </summary>
        public float ResultFDR
        {
            get
            {
                int numReals = 0, numDecoys = 0;
                // For each result
                foreach( ResultInfo r in this )
                {
                    // Count number of real and decoy sequences
                    if( r.decoyState == ResultInfo.DecoyState.DECOY )
                        ++numDecoys;
                    else if( r.decoyState == ResultInfo.DecoyState.REAL )
                        ++numReals;
                }

                //FDR = (2*Decoy)/(Real+Decoy)
                return 1 - Math.Max( (float) ( numReals - ( numDecoys * 1 ) ) / (float) ( numReals + numDecoys ), 0 );
            }
        }

        /// <summary>
        /// removePeptidesFromResult removes a given set of peptides from a given result set.
        /// The method also integrates the newly formed result with the global results.
        /// </summary>
        /// <param name="result">A <see cref="IDPicker.ResultInfo"/> object that holds a result set.</param>
        /// <param name="peps">A <see cref="IDPicker.PeptideList"/> object that holds a list of
        /// peptides that need to be removed from the given result</param>
        public void removePeptidesFromResult( ResultInfo result, PeptideList peps )
        {
            // Initialize a new result
            ResultInfo newResult = new ResultInfo();
            // Assign the peptides in the result to the new result
            newResult.peptides = new PeptideList( result.peptides );
            // Go through the result peptides and remove the peptides
            // that are present in the give peptide list
            foreach( VariantInfo pep in peps )
            {
                newResult.peptides.Remove( pep );
            }

            // new result needs to be integrated with global results
            if( newResult.peptides.Count > 0 )
            {
                InsertResult rv = Insert( newResult );
                // "new result" is either new or it points to the existing result
                newResult = rv.Element;

                // remove old result from and add new result to any associated proteins
                foreach( VariantInfo pep2 in result.peptides )
                    foreach( ProteinInstanceList.MapPair proItr in pep2.peptide.proteins )
                    {
                        ProteinInfo pro = proItr.Value.protein;
                        pro.results.Remove( result );
                        pro.results.Add( newResult );
                    }

                // if new result points to existing result, add old result's spectra to existing result
                if( !rv.WasInserted )
                {
                    foreach( SpectrumList.MapPair sItr in result.spectra )
                    {
                        SpectrumInfo s = sItr.Value;
                        SpectrumList.InsertResult rv2 = newResult.spectra.Insert( s.id, s );
                        if( rv2.WasInserted )
                        {
                            foreach( ResultInstance i in s.results.Values )
                                if( ReferenceEquals( i.info, result ) )
                                    i.info = newResult;
                        }
                    }
                } else
                {
                    // new result's spectra are the same as the old result's spectra
                    newResult.spectra = result.spectra;
                    foreach( SpectrumList.MapPair sItr in newResult.spectra )
                        foreach( ResultInstance i in sItr.Value.results.Values )
                            if( ReferenceEquals( i.info, result ) )
                                i.info = newResult;
                }

                // remove old result from global results
                Remove( result );

            } else
            {
                // new result is empty, so remove the old result from global results
                foreach( SpectrumList.MapPair sItr in result.spectra )
                {
                    SpectrumInfo s = sItr.Value;
                    ResultInstance instanceToRemove = null;
                    foreach( ResultInstance i in s.results.Values )
                        if( ReferenceEquals( i.info, result ) )
                        {
                            instanceToRemove = i;
                            break;
                        }
                    s.results.Remove( instanceToRemove.rank );
                }

                Remove( result );
            }
        }

        /// <summary>
        /// This method returns a comma separated list of interpretations present in
        /// the result set.
        /// </summary>
        /// <returns>a string</returns>
        public override string ToString()
        {
            StringBuilder rv = new StringBuilder();
            foreach( ResultInfo r in this )
            {
                rv.Append( r.peptides + "," );
            }
            return rv.ToString();
        }
    }

    /// <summary>
    /// SpectrumList class maps a spectrum identifier (see <see cref="IDPicker.SpectrumID"/>) to
    /// the meta data about the spectrum (see <see cref="IDPicker.SpectrumInfo"/>). The meta data
    /// contains information about the peptides identified by a spectrum.
    /// </summary>
    public class SpectrumList : Map<SpectrumId, SpectrumInfo>
    {
        public SpectrumList()
            : base() { }

        public SpectrumList( SpectrumList other )
            : base( other ) { }
    }

    /// <summary>
    /// This class maps each SpectrumID (See <see cref="IDPicker.SpectrumID"/>) to its 
    /// corresponding SpectrumList (See <see cref="IDPicker.SpectrumList"/>). This class
    /// essentially maps each SpectrumID object to its corresponding SpectrumInfo object
    /// (See <see cref="IDPicker.SpectrumInfo"/>). The class sorts the spectra based on
    /// their source name, and index, while ignoring the group and the charge state of the
    /// spectra.
    /// </summary>
    public class SharedSpectrumList : Map<SpectrumId, SpectrumList>
    {
        /// <summary>
        /// Initialize a new map with <see cref="IDPicker.SharedSpectrumList.SortByIgnoringGroupAndCharge"/>
        /// comparator.
        /// </summary>
        public SharedSpectrumList()
            : base( new SortByIgnoringGroupAndCharge() ) { }

        /// <summary>
        /// Add the supplied map contents to the existing map.
        /// </summary>
        /// <param name="other"></param>
        public SharedSpectrumList( SharedSpectrumList other )
            : base( other, new SortByIgnoringGroupAndCharge() ) { }

        /// <summary>
        /// Adds the SpectrumList objects contained in a source to the 
        /// existing list.
        /// </summary>
        /// <param name="source">A <see cref="IDPicker.SourceInfo"/> object.</param>
        public void AddSharedSource( SourceInfo source )
        {
            foreach( SpectrumList.MapPair itr in source.spectra )
                this[itr.Key].Add( itr.Key, itr.Value );
        }

        /// <summary>
        /// This class extends the default comparator for the SpectrumID objects (see <see cref="IDPicker.SpectrumID"/>).
        /// The comparator compares two spectrumID objects by ignoring the  group and the charge state.
        /// </summary>
        public class SortByIgnoringGroupAndCharge : IComparer<SpectrumId>
        {
            /// <summary>
            /// Compare two SpectrumID objects by ignoring the group and the charge state of the
            /// spectra
            /// </summary>
            /// <param name="x">A <see cref="IDPicker.SpectrumID"/> object.</param>
            /// <param name="y">A <see cref="IDPicker.SpectrumID"/> object.</param>
            /// <returns>An integer based on whether the source name and indices of 
            /// the two spectra are equal or not.</returns>
            public int Compare( SpectrumId x, SpectrumId y )
            {
                // Compare the source name and the index.
                if( x.source.name == y.source.name )
                {
                    if( x.index == y.index )
                    {
                        return 0;
                    } else
                    {
                        return x.index.CompareTo( y.index );
                    }
                } else
                {
                    return x.source.name.CompareTo( y.source.name );
                }
            }
        }
    }

    /// <summary>
    /// This class maps a peptide sequence to its PeptideInfo object. (see <see cref="IDPicker.PeptideInfo"/>).
    /// </summary>
    public class RawPeptideList : Map<string, PeptideInfo>
    {
        public RawPeptideList()
            : base() { }

        public RawPeptideList( RawPeptideList other )
            : base( other ) { }

        /// <summary>
        /// This function returns a comma separated string of peptide 
        /// sequences present in the list.
        /// </summary>
        /// <returns>a string</returns>
        public override string ToString()
        {
            StringBuilder rv = new StringBuilder();
            // Append each peptide sequence to the string
            foreach( MapPair pepItr in this )
            {
                rv.Append( pepItr.Key + "," );
            }
            return rv.ToString();
        }
    }

    /// <summary>
    /// PeptideList is a list of variants (see <see cref="IDPicker.VariantInfo"/>).
    /// Variants stores all interpretations of a peptide sequence including the modifications
    /// seen on the peptide.
    /// </summary>
    public class PeptideList : Set<VariantInfo>
    {
        public PeptideList()
            : base() { }

        /// <summary>
        /// Uses the list of given variants to create a new list.
        /// </summary>
        /// <param name="other">A <see cref="IDPicker.PeptideList"/> object containing a list
        /// of variants.</param>
        public PeptideList( PeptideList other )
            : base( other ) { }

        /// <summary>
        /// Checks to see if a given peptide sequence is in
        /// the set of peptides present in the current list.
        /// </summary>
        /// <param name="peptideSequence">A <see cref="System.string"/> representation
        /// of a peptide sequence.</param>
        /// <returns>Return true if the list contains the given peptide</returns>
        public bool Contains( string peptideSequence )
        {
            foreach( VariantInfo v in this )
            {
                if( v.peptide.sequence == peptideSequence )
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// This class returns a comma separated interpretations present in a peptide list.
        /// </summary>
        /// <returns>A string</returns>
        public override string ToString()
        {
            StringBuilder rv = new StringBuilder();
            // Generate intepretation for each variant and append to the string.
            foreach( VariantInfo pepItr in this )
            {
                rv.Append( pepItr.ToString() + "," );
            }
            return rv.ToString();
        }

        public int distinctPeptideCount
        {
            get
            {
                int distinctCount = 0;
                foreach( VariantInfo peptide in this )
                {
                    if( peptide.isDistinctPeptide )
                    {
                        ++distinctCount;
                    }
                }
                return distinctCount;
            }
        }

        public int numberOfAmbiguousResults
        {
            get
            {
                Set<string> peptideSequences = new Set<string>();
                foreach( var variant in this )
                {
                    peptideSequences.Add( variant.peptide.sequence );
                }
                return peptideSequences.Count;
            }
        }
    }

    /// <summary>
    /// ProteinList maps a protein locus to a <see cref="IDPicker.ProteinInfo"/> object.
    /// </summary>
    public class ProteinList : Map<string, ProteinInfo>
    {
        /// <summary>
        /// ProteinFDR computes the FDR for all proteins in the map
        /// </summary>
        public float ProteinFDR
        {
            get
            {
                int numReals = 0, numDecoys = 0;
                // For each protein
                foreach( MapPair proItr in this )
                {
                    // See if the protein is real or a decoy
                    if( proItr.Value.isDecoy )
                    {
                        ++numDecoys;
                    } else
                    {
                        ++numReals;
                    }
                }

                // Compute the FDR as (2*numOfDecoys)/(NumReals+NumDecoys)
                return 1 - Math.Max( (float) ( numReals - ( numDecoys * 1 ) ) / (float) ( numReals + numDecoys ), 0 );
            }
        }

        /// <summary>
        /// This function returns a comma separated list of protein locus present 
        /// in the map.
        /// </summary>
        /// <returns>A string</returns>
        public override string ToString()
        {
            StringBuilder rv = new StringBuilder();
            // Append the locus of each protein in the map to the string.
            foreach( MapPair proItr in this )
            {
                rv.Append( proItr.Key + "," );
            }
            return rv.ToString();
        }
    }

    /// <summary>
    /// This class maps a protein locus to a ProteinInstanceInfo object (see <see cref="IDPicker.ProteinInstanceInfo"/>).
    /// The ProteinInstanceInfo object contains a ProteinInfo object (see <see cref="IDPicker.ProteinInfo"/>).
    /// </summary>
    public class ProteinInstanceList : Map<string, ProteinInstanceInfo>
    {
        /// <summary>
        /// This function returns a comma delimited string 
        /// of protein locus present in the map.
        /// </summary>
        /// <returns>A string</returns>
        public override string ToString()
        {
            StringBuilder rv = new StringBuilder();
            // Add the protein locus to the string
            foreach( MapPair proItr in this )
            {
                rv.Append( proItr.Key + "," );
            }
            return rv.ToString();
        }
    }

    /// <summary>
    /// PeptideGroupList is a set of PeptideGroupInfo <see cref="IDPicker.PeptideGroupInfo"/> objects.
    /// </summary>
    public class PeptideGroupList : Set<PeptideGroupInfo>
    {
        /// <summary>
        /// Creates a new peptide group if the peptide is new. This function
        /// assembles peptides matched to same proteins into a single group.
        /// </summary>
        /// <param name="r">A <see cref="IDPicker.ResultInfo"/> object.</param>
        public void addPeptide( ResultInfo r )
        {
            // Create a new peptide group.
            PeptideGroupInfo pepGroup = new PeptideGroupInfo( r );
            PeptideGroupList.InsertResult rv = Insert( pepGroup );
            // We will generate a new peptide group if the peptide didn't
            // match to same proteins as any other peptides that are already
            // in the set.
            if( rv.WasInserted )
                pepGroup.id = Count;
            else
                pepGroup = rv.Element;

            // Insert the result into the peptide group.
            pepGroup.results.Insert( r );
            r.peptideGroup = pepGroup;
        }
    }

    /// <summary>
    /// ProteinGroupList is a set of ProteinGroupInfo (see <see cref="IDPicker.ProteinGroupInfo"/>)
    /// objects. This class is capable of arranging the proteins into groups. The proteins that share
    /// same peptides are grouped togehter as a single group. Otherwise, they are seprated into different
    /// groups.
    /// </summary>
    public class ProteinGroupList : Set<ProteinGroupInfo>
    {
        public ProteinGroupList()
            : base( new GroupByResultList() )
        {
        }

        public ProteinGroupList( IComparer<ProteinGroupInfo> comparer )
            : base( comparer )
        {
        }

        /// <summary>
        /// addProtein function adds a new ProteinInfo (see <see cref="IDPicker.ProteinInfo"/>)
        /// object to the list. The list sorts the proteins with same peptides together and
        /// proteins with large number of peptides to the top. This essentially generates a
        /// list that contains proteins that are either equivalent or subsets of each other.
        /// Proteins that do not share any peptides are inserted into a new group.
        /// </summary>
        /// <param name="pro">A <see cref="IDPicker.ProteinInfo"/> object </param>
        /// <returns>The </returns>
        public ProteinGroupInfo addProtein( ProteinInfo pro )
        {
            ProteinGroupInfo newProGroup = new ProteinGroupInfo( pro.results );
            ProteinGroupList.InsertResult rv = Insert( newProGroup );
            if( rv.WasInserted )
                newProGroup.id = Count;
            else
                newProGroup = rv.Element;

            newProGroup.proteins.Insert( pro.locus, pro );

            return newProGroup;
        }

        /// <summary>
        /// Comparator that sorts two ProteinGroupInfo objects based on their
        /// result lists.
        /// </summary>
        public class GroupByResultList : IComparer<ProteinGroupInfo>
        {
            public int Compare( ProteinGroupInfo x, ProteinGroupInfo y )
            {
                return -x.results.CompareTo( y.results );
            }
        }

        public int Counts()
        {
            return this.Count;
        }

        /*public class GroupBySourceGroups : IComparer<ProteinGroupInfo>
        {
            public int Compare( ProteinGroupInfo x, ProteinGroupInfo y )
            {
                return -x.sourceGroups.CompareTo( y.sourceGroups );
            }
        }*/
    }

    /// <summary>
    /// Holds a list of clusters. A cluster contains protein, peptide, spectra, and modification
    /// identifications
    /// </summary>
    public class ClusterList : List<ClusterInfo>
    {
        /// <summary>
        /// Sorts two cluster based on descending order of their peptide counts followed 
        /// by the total spectral count.
        /// </summary>
        /// <param name="lhs">A cluster info object (See <see cref="IDPicker.ClusterInfo"/></param>
        /// <param name="rhs">A cluster info object (See <see cref="IDPicker.ClusterInfo"/></param>
        /// <returns>An int based on how the objects compare</returns>
        public static int SortDescendingBySequencesThenSpectra( ClusterInfo lhs, ClusterInfo rhs )
        {
            if( lhs == null )
                return 1;
            if( rhs == null )
                return -1;

            if( lhs.results.Count == rhs.results.Count )
            {
                int lhsSpectraCount = 0;
                int rhsSpectraCount = 0;
                foreach( ResultInfo r in lhs.results )
                    lhsSpectraCount += r.spectra.Count;
                foreach( ResultInfo r in rhs.results )
                    rhsSpectraCount += r.spectra.Count;
                return -lhsSpectraCount.CompareTo( rhsSpectraCount );
            } else
                return -lhs.results.Count.CompareTo( rhs.results.Count );
        }
    }

    /*public class SourceList : Set<SourceInfo>
    {
        public SourceList() { }
        public SourceList( Set<SourceInfo> old )
            : base( old )
        { }
    }*/

    /// <summary>
    /// This class holds a map of group names and the groups that belong the name.
    /// </summary>
    public class SourceGroupList : Map<string, SourceGroupInfo>
    {
        /// <summary>
        /// Assembles a list of parent groups and the children of the
        /// parent groups from the list of groups present in the map.
        /// </summary>
        public void assembleParentGroups()
        {
            SourceGroupList parentGroups = new SourceGroupList();
            // For each group
            foreach( SourceGroupInfo group in Values )
            {
                // Skip the root groups
                if( group.isRootGroup() )
                    continue;

                string parentGroupName = group.name;
                SourceGroupInfo childGroup = group;
                while( true )
                {
                    // Get the parent group name
                    parentGroupName = SourceGroupInfo.getParentGroupPath( parentGroupName );

                    SourceGroupInfo parentGroup;

                    if( !Contains( parentGroupName ) )
                    {
                        // Check if we haven't processed it already as a parent group
                        if( !parentGroups.Contains( parentGroupName ) )
                        {
                            parentGroup = new SourceGroupInfo();
                            parentGroup.name = parentGroupName;
                            parentGroups.Add( parentGroupName, parentGroup );
                        } else
                        {
                            // If we already processed it then get the parent group using the name
                            parentGroup = parentGroups[parentGroupName];
                        }
                    } else
                    {
                        // if it's already in the list then get the parent group
                        parentGroup = this[parentGroupName];
                    }

                    // Add the current node as a child to its parent group
                    parentGroup.children.Add( childGroup.name, childGroup );

                    if( childGroup.parent == null )
                        childGroup.parent = parentGroup;

                    // Make the child group as the parent and recursively
                    // build the children for that parent.
                    childGroup = parentGroup;

                    /*foreach( SourceInfo source in group.sources )
                        if( !parentGroup.sources.Contains( source ) )
                            parentGroup.sources.Add( source );*/

                    if( parentGroup.isRootGroup() )
                        break;
                }
            }

            foreach( SourceGroupInfo group in parentGroups.Values )
                Add( group.name, group );
        }

        public int Counts()
        {
            return this.Count;
        }

        public static int SortAscendingByPathDepthThenName( SourceGroupInfo lhs, SourceGroupInfo rhs )
        {
            if( lhs == null )
                return 1;
            if( rhs == null )
                return -1;

            int lhsDepth = 0;
            for( int i = 0; i < lhs.name.Length - 1; ++i )
                if( lhs.name[i] == '/' )
                    ++lhsDepth;

            int rhsDepth = 0;
            for( int i = 0; i < rhs.name.Length - 1; ++i )
                if( rhs.name[i] == '/' )
                    ++rhsDepth;

            if( lhsDepth == rhsDepth )
                return lhs.name.CompareTo( rhs.name );
            else
                return lhsDepth.CompareTo( rhsDepth );
        }
    }
}