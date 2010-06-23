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
using System.ComponentModel;
using System.Data.SQLite;
using System.Linq;
using NHibernate;
using NHibernate.Linq;
using pwiz.CLI.chemistry;
using pwiz.CLI.proteome;

using SourceList = System.Collections.Generic.Set<IDPicker.SourceInfo>;

namespace IDPicker
{
    /// <summary>
    /// This class holds all necessary variables and functions to read
    /// peptide identification results from pepXML files, filter the
    /// peptide identifications, assemble protein identifications from
    /// the peptides, and write the assembly results to the idpXML files.
    /// </summary>
    public partial class Workspace
    {
        public class RunTimeConfig : BaseRunTimeConfig
        {
            public float MaxFDR = 0.05f;
            public int MaxResultRank = 1;
            public int MinPeptideLength = 5;
            public int MinDistinctPeptides = 2;
            public int MinSpectraPerProtein = 2;
            public int MaxAmbiguousIds = 2;
            public bool AllowSharedSourceNames = true;
            public float DeltaAnnotationTolerance = 0.1f;
            /// <summary>
            /// PreferUnmodifiedPeptides flag when set prefers
            /// unmodified interpretations of spectra over
            /// modified interpretation, given that both types
            /// of interpretations score the same.
            /// </summary>
            public bool PreferUnmodifiedPeptides = false;
        }

        public Workspace()
            : this( null )
        {
        }

        /// <summary>
        /// A constructor to initialize the IDPicker workspace. The
        /// function initializes the configuration and local variables
        /// that are used to parse, filter, assemble, and report the
        /// protein identifcation results.
        /// </summary>
        /// <param name="initConfig"></param>
        public Workspace( RunTimeConfig initConfig )
        {
            if( initConfig == null )
                rtConfig = new RunTimeConfig();
            else
                rtConfig = initConfig;

            spectra = new SpectrumList();
            results = new ResultList();
            proteins = new ProteinList();
            peptideGroups = new PeptideGroupList();
            proteinGroups = new ProteinGroupList();
            clusters = new ClusterList();
            groups = new SourceGroupList();
            sharedSpectra = new SharedSpectrumList();
            distinctPeptideSettings = new DistinctPeptideSettings();

            // Set the mass tolerance for delta annotation comparison.
            ModInfo.massTolerance = rtConfig.DeltaAnnotationTolerance;
        }

        public static string Version { get { return Util.GetAssemblyVersion( System.Reflection.Assembly.GetExecutingAssembly().GetName() ); } }
        public static DateTime LastModified { get { return Util.GetAssemblyLastModified( System.Reflection.Assembly.GetExecutingAssembly().GetName() ); } }
        public static string LICENSE = "Vanderbilt University Mass Spectrometry Research Center, D.Tabb/M.Chambers\n" +
                                        "Licensed under the Mozilla Public License.\n";

        public static string TimeFormat = "MM/dd/yyyy@HH:mm:ss";

        private RunTimeConfig rtConfig;
        public SpectrumList spectra;
        public ResultList results;
        public ProteinList proteins;
        public PeptideGroupList peptideGroups;
        public ProteinGroupList proteinGroups;
        public ClusterList clusters;
        public SourceGroupList groups;
        public SharedSpectrumList sharedSpectra;
        public SNPMetaDataGenerator snpAnntoations;
        public ResidueMaps residueMaps;
        public char[] knownModResidues;
        public float[] knownModMasses;
        public ImportSpectraExport spectraExport;

        public string dbFilepath;

        public void setStatusOutput( TextWriter newStatusOutput ) { statusOutput = newStatusOutput; }
        public double statusUpdateInterval = 1.0;
        private TextWriter statusOutput;
        private long lastStatusUpdateTicks = 0;
        private void reportStatus( string msg, bool forceUpdate )
        {
            if( statusOutput == null )
                return;

            long now = DateTime.Now.Ticks;
            if( forceUpdate || new TimeSpan( now - lastStatusUpdateTicks ).TotalSeconds > statusUpdateInterval )
            {
                StringBuilder status = new StringBuilder( 80 );
                status.AppendFormat( "\t...{0,-78}\r", msg );
                statusOutput.Write( status.ToString() );
                lastStatusUpdateTicks = now;
            }
        }

        public void pullSNPMetaData( string uniprotFlatFile )
        {
            Console.WriteLine( "Extracting known SNP annotations for the proteins...." );
            Set<string> swissProtAccessions = new Set<string>();
            if( snpAnntoations == null )
            {
                snpAnntoations = new SNPMetaDataGenerator();
            }
            foreach( ProteinList.MapPair proItr in proteins )
            {
                ProteinInfo pro = proItr.Value;
                if( !pro.locus.StartsWith( "rev_" ) )
                {
                    string accession = pro.getSwissProtAccession();
                    if( accession == null )
                    {
                        continue;
                    }
                    swissProtAccessions.Add( accession );
                }
            }
            snpAnntoations.getVariantDataFromSwissProtFlatFile( swissProtAccessions, uniprotFlatFile );
            Console.WriteLine( "\nFinished extracting SNP annotations" );
            //snpAnntoations.printCollectedMetaData();
        }

        internal int lastSpectrumId;
        //internal int lastResultId;
        internal int lastProteinId;
        //internal int lastPeptideId;

        /// <summary>
        /// This class defines the modifications that can and can not occur in
        /// peptides that are considered as distinct. Distinct peptides hold the
        /// power to discriminate between differentiable proteins.
        /// </summary>
        public class DistinctPeptideSettings
        {
            public DistinctPeptideSettings()
            {
                ModsAreDistinctByDefault = true;
                DistinctModsOverride = new HypotheticalModSet();
                IndistinctModsOverride = new HypotheticalModSet();
            }

            /// <summary>
            /// A constructor that takes a string representation of (user defined) of modifications
            /// that can and can not occur in distinct peptides.
            /// </summary>
            /// <param name="distinctByDefault">A flag to turn on/off the occurence of mods in 
            /// distinct peptides</param>
            /// <param name="distinctModsStr">A string ("mod1Res mod1Mass mod2Res mod2Mass...")
            /// representation of mods that can occur in distinct peptides.</param>
            /// <param name="indistinctModsStr">A string ("mod1Res mod1Mass mod2Res mod2Mass...")
            /// representation of mods that can not occur in distinct peptides.</param>
            public DistinctPeptideSettings( bool distinctByDefault, string distinctModsStr, string indistinctModsStr )
            {
                ModsAreDistinctByDefault = distinctByDefault;
                DistinctModsOverride = new HypotheticalModSet( distinctModsStr );
                IndistinctModsOverride = new HypotheticalModSet( indistinctModsStr );
            }

            /// <summary>
            /// This class tests whether the given mod is permissible to occur in
            /// distinct peptides. A distinct peptide by default has the power to 
            /// discrimiate between two otherwise differentiable proteins.
            /// </summary>
            /// <param name="mod">The modification that needs to be tested as distinct or not</param>
            /// <returns>A bool value if the modification can occur in distinct peptides</returns>
            public bool testModIsDistinct( HypotheticalModInfo mod )
            {
                bool modIsDistinct = false;
                // If there are no user defined distinct mods then all mods are distinct
                // by default, as long as they are not defined by the user as indistinct. 
                // This potentially allows unknown mods as distinct. This needs to be fixed.
                if( ModsAreDistinctByDefault && !IndistinctModsOverride.Contains( mod ) )
                {
                    modIsDistinct = true;
                } else if( !ModsAreDistinctByDefault && DistinctModsOverride.Contains( mod ) )
                {
                    modIsDistinct = true;
                }
                return modIsDistinct;
            }

            /// <summary>
            /// A flag to turn on whether mods can occur in distinct peptides
            /// </summary>
            public bool ModsAreDistinctByDefault;
            /// <summary>
            /// Set of mods defined by user that can occur in distinct peptides
            /// </summary>
            public HypotheticalModSet DistinctModsOverride;
            /// <summary>
            /// Set of mods defined by the user that can't occur in distinct peptides
            /// </summary>
            public HypotheticalModSet IndistinctModsOverride;
        }

        public DistinctPeptideSettings distinctPeptideSettings;

        #region utility functions
        // To remove a protein:
        // - for each of the protein's results:
        //		- for each of the result's peptides:
        //			- if the peptide has the protein, remove it from the peptide
        //			- if the peptide has no more proteins, remove it from the result
        //		- if the result has no more peptides:
        //			- remove the result from the global result set
        //		- if the result still has peptides:
        //			- if the new, smaller result is already in the global result set:
        //				- add the new result's spectra to the existing result's spectra
        //			- if the new, smaller result is not in the global result set:
        //				- add the result to the global result set
        //			- the spectra for the new, smaller result have their result lists updated to point to the new result
        //
        //
        //
        //
        public void removeProtein( ProteinInfo pro )
        {
            Map<ResultInfo, PeptideList> peptidesToRemove = new Map<ResultInfo, PeptideList>();

            foreach( ResultInfo r in pro.results )
            {
                // r = results.Find(rKey).Current;

                foreach( VariantInfo pep in r.peptides )
                {
                    pep.peptide.proteins.Remove( pro.locus );
                    if( pep.peptide.proteins.Count == 0 )
                        peptidesToRemove[r].Add( pep );
                }
            }

            Set<SpectrumId> spectraToRemove = new Set<SpectrumId>();
            foreach( Map<ResultInfo, PeptideList>.MapPair itr in peptidesToRemove )
            {
                results.removePeptidesFromResult( itr.Key, itr.Value );
                foreach( SpectrumInfo s in itr.Key.spectra.Values )
                    if( s.results.Count == 0 )
                        spectraToRemove.Add( s.id );
            }

            foreach( SpectrumId sKey in spectraToRemove )
            {
                spectra.Remove( sKey );

                foreach( SourceGroupList.MapPair groupItr in groups )
                    foreach( SourceInfo source in groupItr.Value.getSources() )
                        source.spectra.Remove( sKey );
            }

            proteins.Remove( pro.locus );
        }

        public void removeResult( ResultInfo r )
        {
            foreach( VariantInfo pep in r.peptides )
            {
                foreach( ProteinInstanceList.MapPair proItr in pep.peptide.proteins )
                {
                    ProteinInfo pro = proItr.Value.protein;
                    pro.results.Remove( r );
                    if( pro.results.Count == 0 )
                        proteins.Remove( pro.locus );
                    else
                    {
                        bool stillHasPeptide = false;
                        foreach( ResultInfo r2 in pro.results )
                            if( r2.peptides.Contains( pep ) )
                            {
                                stillHasPeptide = true;
                                break;
                            }
                        if( !stillHasPeptide )
                            pro.peptides.Remove( pep );
                    }
                }
            }

            foreach( SpectrumList.MapPair sItr in r.spectra )
            {
                SpectrumInfo s = sItr.Value;

                List<ResultInstance> instancesToRemove = new List<ResultInstance>();
                foreach( ResultInstance i in s.results.Values )
                    if( ReferenceEquals( i.info, r ) )
                        instancesToRemove.Add( i );

                foreach( ResultInstance i in instancesToRemove )
                    s.results.Remove( i.rank );

                if( s.results.Count == 0 )
                {
                    spectra.Remove( s.id );
                    s.id.source.spectra.Remove( s.id );
                }
            }

            results.Remove( r );
        }

        public void removeSpectrum( SpectrumInfo s )
        {
            List<ResultInfo> resultsToRemove = new List<ResultInfo>();
            foreach( ResultInstance i in s.results.Values )
            {
                ResultInfo r = i.info;
                r.spectra.Remove( s.id );

                if( r.spectra.Count == 0 )
                    resultsToRemove.Add( r );
            }

            foreach( ResultInfo r in resultsToRemove )
                removeResult( r );

            spectra.Remove( s.id );

            foreach( SourceGroupList.MapPair groupItr in groups )
                foreach( SourceInfo source in groupItr.Value.getSources() )
                    source.spectra.Remove( s.id );
        }

        /// <summary>
        /// This function checks the source groups, protein groups, peptide groups, and the
        /// clusters for inconsistencies like protein group with no peptides and throws
        /// appropriate errors.
        /// </summary>
        /// <param name="maxFDR">Maximum False Discovery Rate</param>
        /// <param name="minDistinctPeptides">Minimum number of distinct peptides per protein</param>
        /// <param name="maxAmbiguousIds">Maximum number of proteins that can be identified by
        /// a spectra with same score.</param>
        public void validate( float maxFDR, int minDistinctPeptides, int maxAmbiguousIds )
        {
            // Check sources
            foreach( SourceGroupList.MapPair groupItr in groups )
            {
                SourceGroupInfo group = groupItr.Value;

                if( group.getSources().Count < 1 && group.getChildGroups().Count < 1 )
                    throw new InvalidDataException( "group " + group.name + " has no sources or children" );
                if( ReferenceEquals( group.parent, group ) )
                    throw new InvalidDataException( "group " + group.name + " is its own parent" );
                if( group.getChildGroups().Contains( group.name ) )
                    throw new InvalidDataException( "group " + group.name + " has itself as a child" );

                foreach( SourceInfo source in group.getSources( true ) )
                {
                    foreach( SpectrumList.MapPair sItr in source.spectra )
                    {
                        SpectrumInfo s = sItr.Value;

                        if( s.results.Count == 0 )
                            throw new InvalidDataException( "spectrum " + s.id + " has no results" );

                        foreach( ResultInstance i in s.results.Values )
                        {
                            if( i.info == null )
                                throw new InvalidDataException( "spectrum " + s.id + " has a null result instance" );
                            if( !i.info.spectra.Contains( s.id ) )
                                throw new InvalidDataException( "spectrum " + s.id + " has a result that doesn't point back to the spectrum" );
                            if( i.info.peptides.numberOfAmbiguousResults > maxAmbiguousIds )
                                throw new InvalidDataException( "spectrum " + s.id + " has a result exceeding the MaxAmbiguousIds filter" );
                        }
                    }
                }
            }

            // Check spectra
            if( spectra.Count < 1 )
                throw new InvalidDataException( "no spectra in workspace" );
            foreach( SpectrumList.MapPair sItr in spectra )
            {
                SpectrumInfo s = sItr.Value;

                if( s.results.Count == 0 )
                    throw new InvalidDataException( "spectrum " + s.id + " has no results" );

                foreach( ResultInstance i in s.results.Values )
                {
                    if( i.info == null )
                        throw new InvalidDataException( "spectrum " + s.id + " has a null result instance" );
                    if( !results.Contains( i.info ) )
                        throw new InvalidDataException( "spectrum " + s.id + " has a result that isn't in the workspace" );
                    if( !i.info.spectra.Contains( s.id ) )
                        throw new InvalidDataException( "spectrum " + s.id + " has a result that doesn't point back to the spectrum" );
                }
            }

            // Check results
            if( results.Count < 1 )
                throw new InvalidDataException( "no results in workspace" );
            foreach( ResultInfo r in results )
            {
                if( r.peptides.Count == 0 )
                    throw new InvalidDataException( "result has no peptides" );

                if( peptideGroups.Count > 0 )
                {
                    if( r.peptideGroup == null )
                        throw new InvalidDataException( "result " + r.ToString() + " is not assigned to a peptide group" );
                    if( !peptideGroups.Contains( r.peptideGroup ) )
                        throw new InvalidDataException( "result " + r.ToString() + " is assigned to a peptide group that isn't in the workspace" );
                    if( r.peptideGroup.proteins.Count < 1)
                        throw new InvalidDataException( "result " + r.ToString() + " is not assinged to a protein" );
                }
                if( r.spectra.Count == 0 )
                    throw new InvalidDataException( "result " + r.ToString() + " has no spectra" );

                foreach( SpectrumInfo s in r.spectra.Values )
                {
                    if( !spectra.Contains( s.id ) )
                        throw new InvalidDataException( "result " + r.ToString() + " has a spectrum that isn't in the workspace" );
                    bool hasResultForMe = false;
                    foreach( ResultInstance i in s.results.Values )
                        if( i.info == r )
                            hasResultForMe = true;
                    if( !hasResultForMe )
                        throw new InvalidDataException( "result " + r.ToString() + " has a spectrum that doesn't point back to the result" );
                }
            }

            // Check proteins
            if( proteins.Count < 1 )
                throw new InvalidDataException( "no proteins in workspace" );
            foreach( ProteinList.MapPair proItr in proteins )
            {
                ProteinInfo pro = proItr.Value;

                if( pro.results.Count == 0 )
                    throw new InvalidDataException( "protein " + pro.locus + " has no results" );
                if( pro.spectra.Count == 0 )
                    throw new InvalidDataException( "protein " + pro.locus + " has no spectra" );
                if( proteinGroups.Count > 0 )
                {
                    if( pro.proteinGroup == null )
                        throw new InvalidDataException( "protein " + pro.locus + " is not assigned to a protein group" );
                    if( !proteinGroups.Contains( pro.proteinGroup ) )
                        throw new InvalidDataException( "protein " + pro.locus + " is assigned to a protein group that isn't in the workspace" );
                }
            }

            // Check protein groups
            if( proteinGroups.Count > 0 )
                foreach( ProteinGroupInfo proGroup in proteinGroups )
                {
                    if( proGroup.id == 0 )
                        throw new InvalidDataException( "protein group has invalid id" );
                    if( proGroup.proteins.Count == 0 )
                        throw new InvalidDataException( "protein group " + proGroup.id + " has no proteins" );
                    if( proGroup.results.Count == 0 )
                        throw new InvalidDataException( "protein group " + proGroup.id + " has no results" );
                    if( proGroup.spectra.Count == 0 )
                        throw new InvalidDataException( "protein group " + proGroup.id + " has no spectra" );
                    foreach( ProteinInfo pro in proGroup.proteins.Values )
                        if( pro.proteinGroup != proGroup )
                            throw new InvalidDataException( "protein group " + proGroup.id + " has a protein not assigned to it" );

                }

            // Check peptide groups
            if( peptideGroups.Count > 0 )
                foreach( PeptideGroupInfo pepGroup in peptideGroups )
                {
                    if( pepGroup.id == 0 )
                        throw new InvalidDataException( "peptide group has invalid id" );
                    if( pepGroup.proteins.Count == 0 )
                        throw new InvalidDataException( "peptide group " + pepGroup.id + " has no proteins" );
                    if( pepGroup.results.Count == 0 )
                        throw new InvalidDataException( "peptide group " + pepGroup.id + " has no results" );
                    foreach( ResultInfo r in pepGroup.results )
                        if( r.peptideGroup != pepGroup )
                            throw new InvalidDataException( "peptide group " + pepGroup.id + " has a result not assigned to it" );
                }

            // Check protein and peptide clusters
            if( clusters.Count > 0 )
                foreach( ClusterInfo c in clusters )
                {
                    if( c.id == 0 )
                        throw new InvalidDataException( "cluster has invalid id" );
                    if( c.proteins.Count == 0 )
                        throw new InvalidDataException( "cluster " + c.id + " has no proteins" );
                    if( c.results.Count == 0 )
                        throw new InvalidDataException( "cluster " + c.id + " has no results" );
                    if( c.peptideGroups.Count == 0 )
                        throw new InvalidDataException( "cluster " + c.id + " has no peptide groups" );
                    if( c.proteinGroups.Count == 0 )
                        throw new InvalidDataException( "cluster " + c.id + " has no protein groups" );

                    foreach( ProteinGroupInfo proGroup in c.proteinGroups )
                    {
                        if( !proteinGroups.Contains( proGroup ) )
                            throw new InvalidDataException( "cluster " + c.id + " has a protein group that isn't in the workspace" );
                        if( proGroup.cluster != c.id )
                            throw new InvalidDataException( "cluster " + c.id + " has a protein group not assigned to it" );
                        if( proGroup.peptideGroups.Count == 0 )
                            throw new InvalidDataException( "protein group " + proGroup.id + " is not connected to any peptide group" );
                        foreach( PeptideGroupInfo pepGroup in proGroup.peptideGroups )
                            if( !pepGroup.proteinGroups.Contains( proGroup ) )
                                throw new InvalidDataException( "protein group " + proGroup.id + " has a peptide group not connected to it" );
                    }
                    foreach( PeptideGroupInfo pepGroup in c.peptideGroups )
                    {
                        if( !peptideGroups.Contains( pepGroup ) )
                            throw new InvalidDataException( "cluster " + c.id + " has a peptide group that isn't in the workspace" );
                        if( pepGroup.cluster != c.id )
                            throw new InvalidDataException( "cluster " + c.id + " has a peptide group not assigned to it" );
                        if( pepGroup.proteinGroups.Count == 0 )
                            throw new InvalidDataException( "peptide group " + pepGroup.id + " is not connected to any protein group" );
                        foreach( ProteinGroupInfo proGroup in pepGroup.proteinGroups )
                            if( !proGroup.peptideGroups.Contains( pepGroup ) )
                                throw new InvalidDataException( "peptide group " + pepGroup.id + " has a protein group not connected to it" );
                    }
                    foreach( ResultInfo r in c.results )
                        if( !c.peptideGroups.Contains( r.peptideGroup ) )
                            throw new InvalidDataException( "cluster " + c.id + " has a result from a peptide group not assigned to the cluster" );
                    foreach( ProteinInfo pro in c.proteins.Values )
                        if( !c.proteinGroups.Contains( pro.proteinGroup ) )
                            throw new InvalidDataException( "cluster " + c.id + " has a protein from a protein group not assigned to the cluster" );
                }
        }
        #endregion

        /// <summary>
        /// Arrange the results in different sources based on their heirarchial 
        /// relationships
        /// </summary>
        public void assembleSourceGroups()
        {
            groups.assembleParentGroups();
        }

        #region filters
        /// <summary>
        /// This function removes peptide identifications that doesn't
        /// meet a user-specified length.
        /// </summary>
        /// <param name="minPeptideLength">Minimum length of a peptide</param>
        public void filterByMinimumPeptideLength( int minPeptideLength )
        {
            if( statusOutput == null )
            {
                // Init a temp variable to hold the filtered out peptides
                List<ResultInfo> resultsToRemove = new List<ResultInfo>();
                // In each result
                foreach( ResultInfo r in results )
                {
                    // For each peptide
                    foreach( VariantInfo pep in r.peptides )
                    {
                        // Remove it if it's length is < a minimum length.
                        if( pep.peptide.sequence.Length < minPeptideLength )
                        {
                            resultsToRemove.Add( r );
                            break;
                        }
                    }
                }

                // Remove the peptides here.
                foreach( ResultInfo r in resultsToRemove )
                    removeResult( r );
            } else
            {
                // Do the samething as above. But, update the user 
                // while doing the processing.
                List<ResultInfo> resultsToRemove = new List<ResultInfo>();
                IEnumerator<ResultInfo> itr = results.GetEnumerator(); itr.MoveNext();
                for( int i = 0; i < results.Count; ++i, itr.MoveNext() )
                {
                    ResultInfo r = itr.Current;
                    reportStatus( "testing result " + ( i + 1 ) + " of " + results.Count, i + 1 == results.Count );
                    foreach( VariantInfo pep in r.peptides )
                        if( pep.peptide.sequence.Length < minPeptideLength )
                        {
                            resultsToRemove.Add( r );
                            break;
                        }
                }

                for( int i = 0; i < resultsToRemove.Count; ++i )
                {
                    reportStatus( "removing result " + ( i + 1 ) + " of " + resultsToRemove.Count, i + 1 == resultsToRemove.Count );
                    removeResult( resultsToRemove[i] );
                }
            }
        }

        /// <summary>
        /// This function removes proteins that doesn't have a minimum number
        /// of unique results. An unique result should match a spectrum to a 
        /// peptide either with distinct mods or no mods. Not all results are 
        /// unique. A peptide interpretation with non-distinct mods is a 
        /// non-unique result.
        /// </summary>
        /// <param name="minUniqueResults">Minimum number of unique results</param>
        public void filterByUniqueResults( int minUniqueResults )
        {
            // Filter out proteins that doesn't have atleast user-defined
            // number of minimum distinct peptides.
            int numProteins = proteins.Count;
            while( true )
            {
                // List to remember the proteins to be removed
                List<ProteinInfo> proteinsToRemove = new List<ProteinInfo>();
                foreach( ProteinList.MapPair proItr in proteins )
                {
                    // See if the protein has a minimum number of unique results
                    // Not all results are unique. A peptide interpretation with
                    // non-distinct mods is a non-unique result. Proteins that
                    // doesn't have a minimum number of unique results have to
                    // be removed.
                    //if( proItr.Value.results.uniqueResultCount < minUniqueResults )
                    if( proItr.Value.results.Count < minUniqueResults )
                    {
                        proteinsToRemove.Add( proItr.Value );
                    }
                }

                // Remove the proteins that met the criteria
                foreach( ProteinInfo pro in proteinsToRemove )
                {
                    removeProtein( pro );
                }

                if( numProteins == proteins.Count )
                    break;
                numProteins = proteins.Count;
            }
        }

        /// <summary>
        ///	filterBySpectralCount function filters out proteins that do not have a 
        ///	user set minimum number of spectral matches. This feature is handy to
        ///	control FP rate in small database searches, large 2-DLC analysis, or
        ///	large number of replicate analysis.
        /// </summary>
        /// <param name="minSpectraPerProtein">Minimum number of spectra per protein</param>
        public void filterBySpectralCount( int minSpectraPerProtein )
        {
            // Filter out proteins that doesn't have atleast user-defined
            // number of minimum spectra.
            int numProteins = proteins.Count;
            if( statusOutput == null )
            {
                while( true )
                {
                    // List to remember the proteins to be removed
                    List<ProteinInfo> proteinsToRemove = new List<ProteinInfo>();
                    // For each protein
                    foreach( ProteinList.MapPair proItr in proteins )
                    {
                        int protSPC = 0;
                        // Get each peptide and count the spectra matched to it
                        foreach( ResultInfo res in proItr.Value.results )
                        {
                            protSPC += res.spectra.Count;
                        }
                        // Add it to the list of proteins to be removed, if it does not have
                        // set minimum number of spectral matches
                        if( protSPC < minSpectraPerProtein )
                        {
                            proteinsToRemove.Add( proItr.Value );
                        }
                    }
                    // Remove the proteins that were flagged
                    foreach( ProteinInfo pro in proteinsToRemove )
                    {
                        removeProtein( pro );
                    }

                    if( numProteins == proteins.Count )
                        break;
                    numProteins = proteins.Count;
                }
            } else
            {
                while( true )
                {
                    // List to remember the proteins to be removed
                    List<ProteinInfo> proteinsToRemove = new List<ProteinInfo>();
                    // For each protein
                    ProteinList.Enumerator itr = proteins.GetEnumerator(); itr.MoveNext();
                    for( int i = 0; i < proteins.Count; ++i, itr.MoveNext() )
                    {
                        ProteinInfo pro = itr.Current.Value;
                        reportStatus( "testing protein " + ( i + 1 ) + " of " + proteins.Count, i + 1 == proteins.Count );
                        int protSPC = 0;
                        // Get each peptide and count the spectra matched to it
                        foreach( ResultInfo res in pro.results )
                        {
                            protSPC += res.spectra.Count;
                        }
                        // Add it to the list of proteins to be removed, if it does not have
                        // set minimum number of spectral matches
                        if( protSPC < minSpectraPerProtein )
                        {
                            proteinsToRemove.Add( pro );
                        } 
                    }
                    
                    // Remove the proteins that were flagged
                    for( int i = 0; i < proteinsToRemove.Count; ++i )
                    {
                        reportStatus( "removing protein " + ( i + 1 ) + " of " + proteinsToRemove.Count, i + 1 == proteinsToRemove.Count );
                        removeProtein( proteinsToRemove[i] );
                    }

                    if( numProteins == proteins.Count )
                        break;
                    numProteins = proteins.Count;
                }
            }
        }

        /// <summary>
        /// This function filters proteins first by number of peptides, followed
        /// by number of distinct peptides.
        /// </summary>
        /// <param name="minDistinctPeptides">Minimum number of distinct peptides</param>
        public void filterByDistinctPeptides( int minDistinctPeptides )
        {
            int numProteins = proteins.Count;
            if( statusOutput == null )
            {
                // Recursively remover proteins that doesn't have a minimum number
                // of peptides
                while( true )
                {
                    // Remove proteins with < a specified number of peptides
                    List<ProteinInfo> proteinsToRemove = new List<ProteinInfo>();
                    foreach( ProteinList.MapPair proItr in proteins )
                        if( proItr.Value.peptides.Count < minDistinctPeptides )
                            proteinsToRemove.Add( proItr.Value );

                    foreach( ProteinInfo pro in proteinsToRemove )
                    {
                        removeProtein( pro );
                    }

                    // If we failed to remove any proteins
                    // then we are done.
                    if( numProteins == proteins.Count )
                        break;
                    numProteins = proteins.Count;
                }

                // Filter out proteins that doesn't have atleast user-defined
                // number of distinct peptides.
                filterByUniqueResults( minDistinctPeptides );
            } else
            {
                // Do the same as above. But this time, report the progress to the user
                while( true )
                {
                    List<ProteinInfo> proteinsToRemove = new List<ProteinInfo>();
                    ProteinList.Enumerator itr = proteins.GetEnumerator(); itr.MoveNext();
                    for( int i = 0; i < proteins.Count; ++i, itr.MoveNext() )
                    {
                        ProteinInfo pro = itr.Current.Value;
                        reportStatus( "testing protein " + ( i + 1 ) + " of " + proteins.Count, i + 1 == proteins.Count );
                        if( pro.peptides.Count < minDistinctPeptides )
                            proteinsToRemove.Add( pro );
                    }

                    for( int i = 0; i < proteinsToRemove.Count; ++i )
                    {
                        reportStatus( "removing protein " + ( i + 1 ) + " of " + proteinsToRemove.Count, i + 1 == proteinsToRemove.Count );
                        removeProtein( proteinsToRemove[i] );
                    }

                    if( numProteins == proteins.Count )
                        break;
                    numProteins = proteins.Count;
                }

                filterByUniqueResults( minDistinctPeptides );
            }
        }

        /// <summary>
        /// This functions removes the results that match to too
        /// many peptides. The threshold is user-defined. The spectra
        /// that map to too may peptides are presumed to be noisy
        /// spectra with a low value for peptide and protein identification.
        /// </summary>
        /// <param name="maxIdsPerResult">Maximum number of peptides allowed to be
        /// identified by a spectrum result</param>
        public void filterByResultAmbiguity( int maxIdsPerResult )
        {
            if( statusOutput == null )
            {
                List<ResultInfo> resultsToRemove = new List<ResultInfo>();
                // For each result
                foreach( ResultInfo r in results )
                {
                    // Take out results that identify too many peptides (user-defined)
                    //if( r.peptides.Count > maxIdsPerResult )
                    if( r.peptides.numberOfAmbiguousResults > maxIdsPerResult )
                        resultsToRemove.Add( r );
                }

                foreach( ResultInfo r in resultsToRemove )
                    removeResult( r );
            } else
            {
                // Do the same as above while reporting the progress to the user
                List<ResultInfo> resultsToRemove = new List<ResultInfo>();
                ResultList.Enumerator itr = results.GetEnumerator(); itr.MoveNext();
                for( int i = 0; i < results.Count; ++i, itr.MoveNext() )
                {
                    ResultInfo r = itr.Current;
                    reportStatus( "testing result " + ( i + 1 ) + " of " + results.Count, i + 1 == results.Count );
                    //if( r.peptides.Count > maxIdsPerResult )
                    if( r.peptides.numberOfAmbiguousResults > maxIdsPerResult )
                        resultsToRemove.Add( r );
                }

                for( int i = 0; i < resultsToRemove.Count; ++i )
                {
                    reportStatus( "removing result " + ( i + 1 ) + " of " + resultsToRemove.Count, i + 1 == resultsToRemove.Count );
                    removeResult( resultsToRemove[i] );
                }
            }
        }

        /// <summary>
        /// This function removes protein clusters that do not have a 
        /// required number of additional unique peptides.
        /// </summary>
        /// <param name="minAdditionalPeptides"></param>
        public void filterByMinimumCoveringSet( int minAdditionalPeptides )
        {

            if( minAdditionalPeptides == 0 )
                return;

            if( statusOutput == null )
            {
                // Get the minimum protein set that can explain all the peptides
                List<ProteinInfo> proteinsToRemove = new List<ProteinInfo>();
                foreach( ProteinList.MapPair proItr in proteins )
                {
                    // Remove the protein cluster if it doesn't have the required number
                    // of unique peptides
                    if( proItr.Value.proteinGroup.uniquePeptideCount < minAdditionalPeptides )
                    {
                        proteinsToRemove.Add( proItr.Value );
                    } else
                    {
                        proItr.Value.proteinGroup = null;
                        foreach( ResultInfo r in proItr.Value.results )
                            r.peptideGroup = null;
                    }
                }

                foreach( ProteinInfo pro in proteinsToRemove )
                    removeProtein( pro );
            } else
            {
                // Same as above with status reporting.
                List<ProteinInfo> proteinsToRemove = new List<ProteinInfo>();
                ProteinList.Enumerator itr = proteins.GetEnumerator(); itr.MoveNext();
                for( int i = 0; i < proteins.Count; ++i, itr.MoveNext() )
                {
                    ProteinInfo pro = itr.Current.Value;
                    reportStatus( "testing protein " + ( i + 1 ) + " of " + proteins.Count, i + 1 == proteins.Count );

                    if( pro.proteinGroup.uniquePeptideCount < minAdditionalPeptides )
                        proteinsToRemove.Add( pro );
                    else
                    {
                        pro.proteinGroup = null;
                        foreach( ResultInfo r in pro.results )
                            r.peptideGroup = null;
                    }
                }

                for( int i = 0; i < proteinsToRemove.Count; ++i )
                {
                    reportStatus( "removing protein " + ( i + 1 ) + " of " + proteinsToRemove.Count, i + 1 == proteinsToRemove.Count );
                    removeProtein( proteinsToRemove[i] );
                }
            }

            assembleProteinGroups();
            assemblePeptideGroups();
            assembleClusters();

            // we lost the actual value by regrouping, but we know the minimum
            foreach( ProteinGroupInfo proGroup in proteinGroups )
                proGroup.uniquePeptideCount = minAdditionalPeptides;
        }
        #endregion

        #region protein group, peptide group, and cluster assembly
        /// <summary>
        /// This functions adds proteins to groups. Each protein found
        /// across multiple samples are collapsed into groups based on
        /// whether they share same peptides or not. 
        /// </summary>
        public void assembleProteinGroups()
        {
            proteinGroups.Clear();
            if( statusOutput == null )
            {
                // Add each protein to the list of protein groups while 
                // remembering the protein group to which the protein belongs
                foreach( ProteinList.MapPair proItr in proteins )
                {
                    proItr.Value.proteinGroup = proteinGroups.addProtein( proItr.Value );
                }
            } else
            {
                // Do the same as above while reporting the status to the user.
                ProteinList.Enumerator itr = proteins.GetEnumerator(); itr.MoveNext();
                for( int i = 0; i < proteins.Count; ++i, itr.MoveNext() )
                {
                    ProteinInfo pro = itr.Current.Value;
                    reportStatus( "adding protein " + ( i + 1 ) + " of " + proteins.Count, i + 1 == proteins.Count );
                    pro.proteinGroup = proteinGroups.addProtein( pro );
                }
            }
        }

        /// <summary>
        /// This function creates peptide groups. A peptide group contains
        /// peptide idenfications that matched to same proteins.
        /// </summary>
        public void assemblePeptideGroups()
        {
            peptideGroups.Clear();
            if( statusOutput == null )
            {
                // Take each result set and create peptide groups.
                // A peptide group contains peptide identifications
                // that matched to the same peptide across multiple
                // samples and multiple proteins.
                foreach( ResultInfo r in results )
                {
                    peptideGroups.addPeptide( r );
                }
            } else
            {
                // Do the same as above while reporting the status to the user
                ResultList.Enumerator itr = results.GetEnumerator(); itr.MoveNext();
                for( int i = 0; i < results.Count; ++i, itr.MoveNext() )
                {
                    ResultInfo r = itr.Current;
                    reportStatus( "adding result " + ( i + 1 ) + " of " + results.Count, i + 1 == results.Count );
                    peptideGroups.addPeptide( r );
                }
            }
        }

        /// <summary>
        /// This function takes a protein group, assigns all the peptides and results to the supplied
        /// cluster. It then takes all the peptides and the results in the cluster, traces all the 
        /// proteins mapped to those peptides and the resutls and adds them to the same cluster.
        /// </summary>
        /// <param name="proGroup">A <see cref="IDPicker.ProteinGroupInfo"/> object containing
        /// protein identification results</param>
        /// <param name="c">A <see cref="IDPicker.ClusterInfo"/> object</param>
        private void recursivelyAssignProteinGroupsToCluster( ProteinGroupInfo proGroup, ClusterInfo c )
        {
            reportStatus( "assigning protein group " + proGroup.id + " to cluster " + c.id, true );

            if( proGroup.cluster > 0 )
            {
                if( proGroup.cluster != c.id )
                    throw new InvalidDataException( "protein groups that are connected are assigned to different clusters" );

                return;
            }

            // Add the protein group to the cluster
            proGroup.cluster = c.id;
            c.proteinGroups.Add( proGroup );

            // For each protein in the group
            foreach( ProteinList.MapPair proItr in proGroup.proteins )
            {
                c.proteins[proItr.Value.locus] = proItr.Value;
                // Get the results and assign them to the cluster.
                // Also assign the corresponding peptides to the 
                // same cluster.
                foreach( ResultInfo r in proItr.Value.results )
                {
                    c.results.Add( r );
                    c.peptideGroups.Add( r.peptideGroup );
                    r.peptideGroup.cluster = c.id;
                }
            }

            // recursively add all "cousin" protein groups to the same cluster
            foreach( ResultInfo r in proGroup.results )
                foreach (ProteinGroupInfo cousinProGroup in r.peptideGroup.proteinGroups)
                {
                    if (!ReferenceEquals(cousinProGroup, proGroup) && cousinProGroup.cluster == 0)
                        recursivelyAssignProteinGroupsToCluster(cousinProGroup, c);
                    else if (cousinProGroup.cluster != c.id)
                        throw new InvalidDataException("protein groups that are connected are assigned to different clusters");
                }
        }

        /// <summary>
        /// This function takes peptide and protein groups and 
        /// assembles them together. A peptide group contains
        /// all peptide identifications that mapped to same 
        /// protein and a protein group contains all proteins that
        /// mapped to same peptides. The function generate clusters 
        /// and assign the proteins that are connected to the same 
        /// peptides to a single cluster. The proteins that map to
        /// a subset of the peptides in the generated cluster are 
        /// also assigned to the same cluster in a recursive fashion.
        /// </summary>
        public void assembleClusters()
        {
            clusters.Clear();

            if( statusOutput == null )
            {
                // Get each protein
                foreach( ProteinGroupInfo proGroup in proteinGroups )
                {
                    foreach( ResultInfo r in proGroup.results )
                    {
                        // Assign each peptide to corresponding group
                        proGroup.peptideGroups.Add( r.peptideGroup );
                        // Set the peptide to protein group mapping
                        r.peptideGroup.proteinGroups.Add( proGroup );
                    }
                }
            } else
            {
                // Do the same as above while reporting the status to the user
                ProteinGroupList.Enumerator itr = proteinGroups.GetEnumerator();
                for( int i = 0; i < proteinGroups.Count; ++i )
                {
                    itr.MoveNext();
                    ProteinGroupInfo proGroup = itr.Current;
                    IEnumerator<ResultInfo> itr2 = proGroup.results.GetEnumerator();
                    for( int j = 0; j < proGroup.results.Count; ++j )
                    {
                        itr2.MoveNext();
                        ResultInfo r = itr2.Current;
                        reportStatus( "linking peptide group " + ( j + 1 ) + " of " + proGroup.results.Count + " to protein group " + ( i + 1 ) + " of " + proteinGroups.Count, j + 1 == proGroup.results.Count );
                        proGroup.peptideGroups.Add( r.peptideGroup );
                        r.peptideGroup.proteinGroups.Add( proGroup );
                    }
                }
            }

            // Set the protein and peptide group cluster identifications to 0.
            foreach( ProteinGroupInfo proGroup in proteinGroups )
            {
                proGroup.cluster = 0;
            }
            foreach( PeptideGroupInfo pepGroup in peptideGroups )
            {
                pepGroup.cluster = 0;
            }

            // For each protein group
            foreach( ProteinGroupInfo proGroup in proteinGroups )
            {
                if( proGroup.cluster == 0 )
                {
                    // Generate a new cluster and assign an incremental ID.
                    ClusterInfo c = new ClusterInfo();
                    c.id = clusters.Count + 1;
                    // Add the protein to the cluster. Also add proteins that
                    // matched to the peptides of the given protein to the 
                    // cluster
                    recursivelyAssignProteinGroupsToCluster( proGroup, c );
                    clusters.Add( c );
                }
            }

            // Sort the clusters
            clusters.Sort( ClusterList.SortDescendingBySequencesThenSpectra );

            // Assign cluster idenfication numbers. These numbers signify
            // how many proteins in a cluster share peptides, and also 
            // how many protein are identified per peptide cluster.
            for( int i = 0; i < clusters.Count; ++i )
            {
                clusters[i].id = i + 1;
                foreach( ProteinGroupInfo proGroup in clusters[i].proteinGroups )
                    proGroup.cluster = i + 1;
                foreach( PeptideGroupInfo pepGroup in clusters[i].peptideGroups )
                    pepGroup.cluster = i + 1;
            }
        }

        public void assembleMinimumCoveringSet2( ClusterInfo c )
        {
            Map<int, ResultInfo> resultIndex = new Map<int, ResultInfo>();
            Set<int> clusterResults = new Set<int>();
            foreach( ResultInfo result in c.results )
            {
                resultIndex[result.id] = result;
                clusterResults.Add( result.id );
            }

            Map<int, ProteinGroupInfo> proteinGroupIndex = new Map<int, ProteinGroupInfo>();
            SortedList<int, Set<int>> groupResultSets = new SortedList<int, Set<int>>();
            Set<int> clusterGroups = new Set<int>();
            foreach( ProteinGroupInfo proGroup in c.proteinGroups )
            {
                Set<int> groupResults = new Set<int>();
                foreach( ResultInfo result in proGroup.results )
                    groupResults.Add( result.id );
                proteinGroupIndex[proGroup.id] = proGroup;
                groupResultSets.Add( proGroup.id, groupResults );
                clusterGroups.Add( proGroup.id );
            }

            while( clusterResults.Count > 0 )
            {
                SortedList<int, Set<int>> remainingResults = new SortedList<int, Set<int>>();
                foreach( int proGroupId in clusterGroups )
                    remainingResults[proGroupId] = clusterResults - groupResultSets[proGroupId];

                int lowestRemainingResultsCount = clusterResults.Count;
                KeyValuePair<int, Set<int>> smallestRemainingResults = new KeyValuePair<int, Set<int>>();
                foreach( KeyValuePair<int, Set<int>> itr in remainingResults )
                {
                    if( itr.Value.Count == lowestRemainingResultsCount )
                        Console.WriteLine( "\nWarning: greedy algorithm can't decide between equal groups." );
                    else if( itr.Value.Count < lowestRemainingResultsCount )
                    {
                        lowestRemainingResultsCount = itr.Value.Count;
                        smallestRemainingResults = itr;
                    }
                }
                clusterResults = smallestRemainingResults.Value;
                proteinGroupIndex[smallestRemainingResults.Key].uniquePeptideCount = clusterResults.Count;
                clusterGroups.Remove( smallestRemainingResults.Key );
                groupResultSets.Remove( smallestRemainingResults.Key );
            }
        }

        public void assembleMinimumCoveringSet( ClusterInfo c )
        {
            if( c.proteinGroups.Count == 1 ) // degenerate case
            {
                foreach( ProteinGroupInfo proGroup in c.proteinGroups )
                    proGroup.uniquePeptideCount = int.MaxValue; // value is n/a
                return;
            }

            /*Set<ResultInfo> clusterResults = new Set<ResultInfo>( c.results );
            ProteinGroupList clusterGroups = new ProteinGroupList();
            foreach( ProteinGroupInfo proGroup in c.proteinGroups )
                clusterGroups.Add( proGroup );
            //Console.WriteLine(); 
            while( clusterResults.Count > 0 )
            {
                List<ProteinGroupInfo> minRemainingResults = new List<ProteinGroupInfo>();
                int minRemainingResultCount = clusterResults.Count;
                //int n = 0;
                //Console.WriteLine( "groups: " + clusterGroups.Count + "; results: " + clusterResults.Count );
                foreach( ProteinGroupInfo proGroup in clusterGroups )
                {
                    //Console.Write( n++ + " of " + clusterGroups.Count + "\r" );
                    int count = clusterResults.Count;
                    foreach( ResultInfo r in proGroup.results )
                        if( clusterResults.Contains( r ) )
                            --count;
                    if( count <= minRemainingResultCount )
                    {
                        if( count < minRemainingResultCount )
                            minRemainingResults.Clear();
                        minRemainingResults.Add( proGroup );
                    }
                }

                ProteinGroupInfo mostGreedyGroup = minRemainingResults[0];
                minRemainingResults.Clear();
                int oldCount = clusterResults.Count;
                clusterResults.Subtract( mostGreedyGroup.results );
                if( clusterResults.Count >= oldCount )
                {
                    Console.Error.WriteLine( "Something has gone terribly wrong!" );
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                }

                mostGreedyGroup.minSet = true;
                clusterGroups.Remove( mostGreedyGroup );
            }*/

            // Get the results in the cluster
            Set<ResultInfo> clusterResults = new Set<ResultInfo>( c.results );
            // Get the protein groups in the cluster
            ProteinGroupList clusterGroups = new ProteinGroupList();
            foreach( ProteinGroupInfo proGroup in c.proteinGroups )
                clusterGroups.Add( proGroup );
            //Console.WriteLine(); 
            // while there are results in the cluster
            while( clusterResults.Count > 0 )
            {
                // Maps the number of results to a protein group
                Map<int, List<ProteinGroupInfo>> remainingResults = new Map<int, List<ProteinGroupInfo>>();
                //int n = 0;
                //Console.WriteLine( "groups: " + clusterGroups.Count + "; results: " + clusterResults.Count );
                // Iterate through protein groups
                foreach( ProteinGroupInfo proGroup in clusterGroups )
                {
                    //Console.Write( n++ + " of " + clusterGroups.Count + "\n" );
                    // Get the number of results in the cluster
                    int count = clusterResults.Count;
                    // Iterate over the cluster results and see how 
                    // many cluster group results can be explained
                    // by that protein group
                    foreach( ResultInfo r in proGroup.results )
                    {
                        if( clusterResults.Contains( r ) )
                            --count;
                    }
                    // Map the number of remaining results to that
                    // protein group
                    remainingResults[count].Add( proGroup );
                }

                // Take the first protein group that can explain the most results
                ProteinGroupInfo mostGreedyGroup = remainingResults.Values[0][0];
                // Subtract its results from the cluster results
                mostGreedyGroup.uniquePeptideCount = clusterResults.Count - remainingResults.Keys[0];
                clusterResults.Subtract( mostGreedyGroup.results );
                // Remove the most greedy group from the cluster groups
                clusterGroups.Remove( mostGreedyGroup );
            }
        }
        #endregion



        #region idpXML file I/O
        /// <summary>
        /// This function generates an XML formatted document string of the current state of the workspace.
        /// The XML document preserves all peptide-protein and peptide-spectra relationships 
        /// found in the result set.
        /// </summary>
        /// <returns>A string representation of XML document</returns>
        public string assemblePeptidesXmlToString()
        {
            StringWriter writer = new StringWriter();
            assemblePeptidesXmlToStream( writer );
            return writer.ToString();
        }

        /// <summary>
        /// This function writes an XML formatted document of the current state of the workspace
        /// to the specified TextWriter.
        /// The XML document preserves all peptide-protein and peptide-spectra relationships 
        /// found in the result set.
        /// </summary>
        public void assemblePeptidesXmlToStream( TextWriter textWriter )
        {
            Map<SourceInfo, SpectrumList> sourceReverseIndex = new Map<SourceInfo, SpectrumList>();
            int n;

            ModMap emptyModList = new ModMap();

            n = 0;
            // Generate the peptide index.
            Map<string, int> peptidesIndex = new Map<string, int>();
            // Iterate through all results and the peptides in the result and
            // generate an index.
            foreach( ResultInfo r in results )
            {
                foreach( VariantInfo pep in r.peptides )
                {
                    peptidesIndex.Add( pep.peptide.sequence, peptidesIndex.Count + 1 );
                }

                foreach( SpectrumList.MapPair sItr in r.spectra )
                {
                    sourceReverseIndex.Add( sItr.Value.id.source, new SpectrumList() );
                    SpectrumList spectra = sourceReverseIndex[sItr.Value.id.source];
                    spectra.Add( sItr.Value.id, sItr.Value );
                }
            }

            n = 0;
            // Generate identifiers for each protein.
            foreach( ProteinList.MapPair proItr in proteins )
            {
                ++n;
                proItr.Value.id = n;
            }

            // write XML document tag
            textWriter.Write( "<?xml version=\"1.0\" encoding=\"ASCII\"?>\n" );

            // Initialize the XML writer and set the string buffer for the XML
            // writer.
            XmlWriterSettings writerSettings = new XmlWriterSettings();
            writerSettings.Encoding = Encoding.ASCII;
            //writerSettings.ConformanceLevel = ConformanceLevel.Fragment;
            writerSettings.OmitXmlDeclaration = true;
            writerSettings.Indent = true;
            writerSettings.IndentChars = ( "\t" );
            writerSettings.NewLineChars = "\n";

            XmlWriter writer = XmlWriter.Create( textWriter, writerSettings );
            //writer.WriteStartDocument();
            // root element
            writer.WriteStartElement( "idPickerPeptides" );

            /*writer.WriteStartElement( "analysisParameters" );
            writer.WriteAttributeString( "count", "1" );
            {
                writer.WriteStartElement( "analysisParameter" );
                writer.WriteAttributeString( "name", "ProteinDatabase" );
                writer.WriteAttributeString( "value", dbFilepath );
                writer.WriteEndElement();
            }
            writer.WriteEndElement();*/
            // analysisParameters

            // Open proteinIndex tag and write the information
            // for all the proteins present in the results
            writer.WriteStartElement( "proteinIndex" );
            writer.WriteAttributeString( "count", proteins.Count.ToString() );
            writer.WriteAttributeString("database", dbFilepath);
            foreach( ProteinList.MapPair proItr in proteins )
            {
                ProteinInfo pro = proItr.Value;

                if( proItr.Value.spectra.Count == 0 )
                    continue;

                writer.WriteStartElement( "protein" );
                writer.WriteAttributeString( "id", pro.id.ToString() );
                writer.WriteAttributeString( "locus", pro.locus );
                writer.WriteAttributeString( "decoy", Convert.ToInt32( pro.isDecoy ).ToString() );
                writer.WriteAttributeString( "length", pro.length.ToString() );
                if( pro.description.Length > 0 )
                    writer.WriteAttributeString( "description", pro.description );
                writer.WriteEndElement();
            }
            writer.WriteEndElement(); // proteinIndex


            Set<string> peptidesAlreadyOutput = new Set<string>();
            // Start a peptideIndex and write the peptide information for all
            // the peptides seen in the result set.
            writer.WriteStartElement( "peptideIndex" );
            writer.WriteAttributeString( "count", Convert.ToString( peptidesIndex.Count ) );
            foreach( ResultInfo r in results )
            {
                if( r.spectra.Count == 0 )
                    continue;

                foreach( VariantInfo pep in r.peptides )
                {
                    Set<string>.InsertResult rv = peptidesAlreadyOutput.Insert( pep.peptide.sequence );
                    if( !rv.WasInserted )
                        continue;

                    writer.WriteStartElement( "peptide" );
                    writer.WriteAttributeString( "id", peptidesIndex[pep.peptide.sequence].ToString() );
                    writer.WriteAttributeString( "sequence", pep.peptide.sequence );
                    //writer.WriteAttributeString( "offset", Convert.ToString( pep.peptide.offset ) );
                    writer.WriteAttributeString( "mass", pep.peptide.mass.ToString() );
                    writer.WriteAttributeString( "unique", ( pep.peptide.unique ? "1" : "0" ) );
                    writer.WriteAttributeString( "NTerminusIsSpecific", Convert.ToInt32( pep.peptide.NTerminusIsSpecific ).ToString() );
                    writer.WriteAttributeString( "CTerminusIsSpecific", Convert.ToInt32( pep.peptide.CTerminusIsSpecific ).ToString() );

                    // Write the protein locus information for each peptide ID
                    foreach( ProteinInstanceList.MapPair proItr in pep.peptide.proteins )
                        if( proItr.Value.protein.spectra.Count > 0 )
                        {
                            writer.WriteStartElement( "locus" );
                            writer.WriteAttributeString( "id", proItr.Value.protein.id.ToString() );
                            writer.WriteAttributeString( "offset", proItr.Value.offset.ToString() );
                            writer.WriteEndElement();
                        }
                    writer.WriteEndElement(); // peptide
                }
            }
            writer.WriteEndElement(); // peptideIndex

            // Write out all the spectra sources
            writer.WriteStartElement( "spectraSources" );
            writer.WriteAttributeString( "count", sourceReverseIndex.Count.ToString() );
            // Write the source and processing information for each source
            foreach( Map<SourceInfo, SpectrumList>.MapPair itr in sourceReverseIndex )
            {
                SourceInfo source = itr.Key;
                SpectrumList spectra = itr.Value;
                writer.WriteStartElement( "spectraSource" );
                writer.WriteAttributeString( "group", source.group.name );
                writer.WriteAttributeString( "name", source.name );
                writer.WriteAttributeString( "count", spectra.Count.ToString() );

                writer.WriteStartElement( "processingEventList" );
                writer.WriteAttributeString( "count", source.processingEvents.Count.ToString() );
                foreach( ProcessingEvent evt in source.processingEvents )
                {
                    writer.WriteStartElement( "processingEvent" );
                    writer.WriteAttributeString( "type", evt.type );
                    writer.WriteAttributeString( "start", evt.startTime.ToString( TimeFormat ) );
                    writer.WriteAttributeString( "end", evt.endTime.ToString( TimeFormat ) );
                    foreach( ProcessingParam param in evt.parameters )
                    {
                        writer.WriteStartElement( "processingParam" );
                        writer.WriteAttributeString( "name", param.name );
                        writer.WriteAttributeString( "value", param.value );
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement(); // processingEvent
                }
                writer.WriteEndElement(); // processingEventList

                // Write spectrum information for each spectrum in the source
                foreach( SpectrumList.MapPair sItr in spectra )
                {
                    SpectrumInfo s = sItr.Value;

                    if( s.results.Count > 0 )
                    {
                        writer.WriteStartElement( "spectrum" );
                        writer.WriteAttributeString( "index", s.id.index.ToString() );
                        writer.WriteAttributeString( "id", s.nativeID );
                        //writer.WriteAttributeString( "nativeID", s.nativeID );
                        writer.WriteAttributeString( "z", s.id.charge.ToString() );
                        writer.WriteAttributeString( "mass", s.precursorMass.ToString() );
                        writer.WriteAttributeString( "time", s.retentionTime.ToString() );
                        writer.WriteAttributeString( "comparisons", s.numComparisons.ToString() );
                        writer.WriteAttributeString( "results", s.results.Count.ToString() );

                        // Write the results associated with each of the spectrum
                        foreach( ResultInstance i in s.results.Values )
                        {
                            writer.WriteStartElement( "result" );
                            writer.WriteAttributeString( "rank", i.rank.ToString() );
                            writer.WriteAttributeString( "FDR", i.FDR.ToString() );
                            // Write the search score string if it exists.
                            string scoreStr = i.getSearchScoreString();
                            if( scoreStr != null )
                                writer.WriteAttributeString( "scores", scoreStr );
                            writer.WriteAttributeString( "ids", i.info.peptides.Count.ToString() );

                            // Write the peptide index that maps the result to a peptide.
                            foreach( VariantInfo pep in i.info.peptides )
                            {
                                writer.WriteStartElement( "id" );
                                writer.WriteAttributeString( "peptide", peptidesIndex[pep.peptide.sequence].ToString() );
                                if( pep.mods.Count > 0 )
                                {
                                    StringBuilder modString = new StringBuilder();
                                    modString.Append( pep.mods.ToString() );
                                    if( pep.alternatives.Count > 0 )
                                    {
                                        foreach( VariantInfo alternative in pep.alternatives )
                                        {
                                            modString.Append( ";" + alternative.mods.ToString() );
                                        }
                                    }
                                    if( modString.Length > 0 )
                                    {
                                        writer.WriteAttributeString( "mods", modString.ToString() );
                                    }
                                }
                                /*if (i.mods[pep.peptide].Count > 0) {
                                    writer.WriteAttributeString("mods", i.mods.ToString(pep.peptide));
                                    //writer.WriteAttributeString( "mods", i.mods[pep.peptide].ToString() );
                                }*/

                                writer.WriteEndElement();
                            }
                            writer.WriteEndElement(); // result
                        }
                        writer.WriteEndElement(); // spectrum
                    }
                }
                writer.WriteEndElement(); // spectraSource
            }
            writer.WriteEndElement(); // spectraSources

            writer.WriteEndElement(); // root element
            writer.Close();
        }

        #region getAttribute convenience functions
        private string getAttribute( XmlReader reader, string attribute )
        {
            return getAttributeAs<string>( reader, attribute );
        }

        private string getAttribute( XmlReader reader, string attribute, bool throwIfAbsent )
        {
            return getAttributeAs<string>( reader, attribute, throwIfAbsent );
        }

        private T getAttributeAs<T>( XmlReader reader, string attribute )
        {
            return getAttributeAs<T>( reader, attribute, false );
        }

        private T getAttributeAs<T>( XmlReader reader, string attribute, bool throwIfAbsent )
        {
            if( reader.MoveToAttribute( attribute ) )
            {
                TypeConverter c = TypeDescriptor.GetConverter( typeof( T ) );
                if( c == null || !c.CanConvertFrom( typeof( string ) ) )
                    throw new Exception( "unable to convert from string to " + typeof( T ).Name );
                T value = (T) c.ConvertFromString( reader.Value );
                reader.MoveToElement();
                return value;
            } else if( throwIfAbsent )
                throw new Exception( "missing required attribute \"" + attribute + "\"" );
            else if( typeof( T ) == typeof( string ) )
                return (T) TypeDescriptor.GetConverter( typeof( T ) ).ConvertFromString( String.Empty );
            else
                return default( T );
        }
        #endregion

        /// <summary>
        /// This function reads a idpXML formatted file generates data structures for the
        /// protein present in the file, peptides matched to the proteins, spectra that
        /// identified the peptides, intepretations of each spectrum, and modifications
        /// present in peptide interpretations.
        /// </summary>
        /// <param name="sourceXml">A idpXML formatted document reader</param>
        /// <param name="sourceGroup">Name of the group</param>
        /// <param name="maxFDR">FDR cutoff</param>
        /// <param name="maxRank">Rank cutoff</param>
        public void readPeptidesXml( StreamReader sourceXml,
                                        string sourceGroup,
                                        float maxFDR,
                                        int maxRank )
        {
            XmlReader reader = new XmlTextReader( sourceXml );

            // Protein and peptide information
            ProteinInfo[] proteinIndex = null;
            PeptideInfo[] peptideIndex = null;
            // List of peptide interpretations
            PeptideList peptides = new PeptideList();
            int curId = 0;
            string curGroup = "";
            // The source and spectra information
            SourceInfo curSource = new SourceInfo();
            // Spectrum information
            SpectrumInfo curSpectrum = new SpectrumInfo();
            // Interpretation information
            ResultInstance curInstance = new ResultInstance();
            // Peptide and spectra match information
            ResultInfo curResult = new ResultInfo();
            //Search score names
            List<string> scoreNames = new List<string>();

            string tag;
            long lastStatusUpdatePosition = 0;
            long baseStreamLength = sourceXml.BaseStream.Length;

            while( reader.Read() )
            {
                // read at least 500kb between updates
                if( statusOutput != null )
                {
                    long baseStreamPosition = sourceXml.BaseStream.Position;
                    if( baseStreamPosition > lastStatusUpdatePosition + 500000 || baseStreamPosition == baseStreamLength )
                    {
                        lastStatusUpdatePosition = baseStreamPosition;
                        reportStatus( "parsed " + baseStreamPosition + " of " + baseStreamLength + " bytes; " +
                                        groups.Count + " groups; " + results.Count + " results; " + spectra.Count + " spectra; " +
                                        proteins.Count + " proteins", baseStreamPosition == baseStreamLength );
                    }
                }

                switch( reader.NodeType )
                {
                    case XmlNodeType.Element:
                        tag = reader.Name;

                        // Read the peptide information
                        // An example tag: <id peptide="103" mods="9:-272.143" />
                        if( tag == "id" )
                        {
                            if (maxRank == 0)
                                continue;

                            int id = getAttributeAs<int>( reader, "peptide", true );

                            PeptideInfo pep = peptideIndex[id];
                            VariantInfo distinctVariant = new VariantInfo();
                            distinctVariant.peptide = pep;

                            // Get the mods for the peptide
                            string modListStr = getAttribute( reader, "mods" );
                            if( modListStr.Length > 0 )
                            {
                                ++curInstance.numberOfModifiedPeptides;
                                // Remember the mods for the current peptide
                                ModMap fullModMap = new ModMap();
                                curInstance.mods[distinctVariant.peptide].Add( fullModMap );

                                // Parse the mod string
                                // Example mod string: "9:-272.143;10:-273.143"
                                // Each set of mods before the semi-colon represent an interpretation
                                string[] ambiguousLocations = modListStr.Split( ';' );
                                //  If there is more than one interpretation to this mod mas, get each interpretation
                                for( int interpretation = 0; interpretation < ambiguousLocations.Length; ++interpretation )
                                {
                                    VariantInfo alternativeInterpretation = new VariantInfo();
                                    if( interpretation > 0 )
                                    {
                                        alternativeInterpretation.peptide = pep;
                                    }
                                    string[] modInfoStrs = ambiguousLocations[interpretation].Split( ' ' );
                                    // Split the mod info in each interpretation and make a mod object
                                    foreach( string modInfoStr in modInfoStrs )
                                    {
                                        string[] modPosMassPair = modInfoStr.Split( ":".ToCharArray() );
                                        string modPosStr = modPosMassPair[0];
                                        string modMassStr = modPosMassPair[1];
                                        char modPos;
                                        if( modPosStr == "n" )
                                            modPos = 'n';
                                        else if( modPosStr == "c" )
                                            modPos = 'c';
                                        else
                                            modPos = Convert.ToChar( Convert.ToInt32( modPosStr ) );
                                        // Get a new mod object and test whether it can occur in a distinct peptide
                                        ModInfo mod = new ModInfo( distinctVariant.peptide, modPos, Convert.ToSingle( modMassStr ) );
                                        string title = getModificationAnnotation( mod );
                                        if( title != null && title.Length > 0 )
                                        {
                                            mod.title = title;
                                        }
                                        if( distinctPeptideSettings.testModIsDistinct( mod.ToHypotheticalModInfo() ) )
                                        {
                                            // Add the modification position, update the number of mods 
                                            // variable and add the modification mass to the array of
                                            // masses seen for the modification map
                                            if( interpretation > 0 )
                                            {
                                                alternativeInterpretation.mods[modPos].Add( mod );
                                                alternativeInterpretation.mods.numberOfMods++;
                                                alternativeInterpretation.mods.modMasses.Add( mod.mass );
                                            } else
                                            {
                                                distinctVariant.mods[modPos].Add( mod );
                                                distinctVariant.mods.numberOfMods++;
                                                distinctVariant.mods.modMasses.Add( mod.mass );
                                            }
                                        }
                                        if( interpretation == 0 )
                                        {
                                            // Add the mod to the full mod map.
                                            fullModMap[modPos].Add( mod );
                                        }
                                    }
                                    // Add the interpretation to the list of alternatives
                                    if( interpretation > 0 )
                                    {
                                        distinctVariant.alternatives.Add( alternativeInterpretation );
                                        alternativeInterpretation.mods.modMasses.Sort();
                                    }
                                }
                            } else
                            {
                                ++curInstance.numberOfUnmodifiedPeptides;
                            }
                            //Console.WriteLine("Read:" + curSpectrum.id.index + "," + distinctVariant.mods.ToString() + "," + pep.sequence + "," + modListStr.ToString());
                            // Add the variant
                            curResult.peptides.Add( distinctVariant );
                            distinctVariant.mods.modMasses.Sort();
                            //foreach (VariantInfo testPep in curResult.peptides) {
                            //    Console.WriteLine("\t"+curSpectrum.id.index + "," + testPep.peptide.sequence + "," + testPep.mods.ToString());
                            //}
                        } else if( tag == "spectrum" )
                        {
                            if (maxRank == 0)
                                continue;

                            // Read the spectrum tag
                            //  <spectrum id="614" nativeID="614" index="196" z="1" mass="569.32" 
                            //   time="16.7" targets="82" decoys="0" results="1">
                            int index = getAttributeAs<int>( reader, "index", true );
                            int z = getAttributeAs<int>( reader, "z", true );

                            curSpectrum = new SpectrumInfo();
                            curSpectrum.id.source = curSource;
                            curSpectrum.id.index = index;
                            curSpectrum.id.charge = z;

                            //curSpectrum.stringID = getAttribute( reader, "id" );
                            //curSpectrum.nativeID = getAttribute( reader, "nativeID" );
                            curSpectrum.nativeID = getAttribute( reader, "id" );

                            curSpectrum.precursorMass = getAttributeAs<float>( reader, "mass", true );
                            curSpectrum.retentionTime = getAttributeAs<float>( reader, "time" );

                            curSpectrum.numComparisons = getAttributeAs<int>( reader, "comparisons" );
                            curSpectrum.numComparisons += getAttributeAs<int>( reader, "targets" );
                            curSpectrum.numComparisons += getAttributeAs<int>( reader, "decoys" );

                        } else if( tag == "result" )
                        {
                            if (maxRank == 0)
                                continue;

                            // Read the result tag
                            // <result rank="1" FDR="0" ids="3">
                            curResult = new ResultInfo();
                            curInstance = new ResultInstance();

                            curInstance.rank = getAttributeAs<int>( reader, "rank", true );
                            curInstance.FDR = getAttributeAs<float>( reader, "FDR", true );
                            string scores = null;
                            try
                            {
                                scores = getAttribute(reader, "scores", true);
                            }
                            catch (Exception) { }
                            if( scores != null )
                            {
                                curInstance.searchScores = new Dictionary<string, float>();
                                string[] toks = scores.Split( new char[] { ' ' } );
                                for( int scoreIndex = 0; scoreIndex < toks.Length; scoreIndex++ )
                                {
                                    try
                                    {
                                        curInstance.searchScores.Add(scoreNames[scoreIndex], (float) Convert.ToDouble(toks[scoreIndex]));
                                    }
                                    catch (Exception) { }
                                }
                            }

                            // Read the resulf iff the rank and the FDR passes the thresholds
                            if( curInstance.rank <= maxRank &&// curSpectrum.results.Count == 0 &&
                                curInstance.FDR <= maxFDR )
                            {
                                // Add the spectrum to the list
                                SpectrumList.InsertResult rv = spectra.Insert( curSpectrum.id, curSpectrum );
                                if( rv.WasInserted )
                                {
                                    ++lastSpectrumId;
                                    rv.Element.Value.id.id = lastSpectrumId;
                                }

                                SpectrumList.InsertResult rv2 = curSource.spectra.Insert( curSpectrum.id, curSpectrum );

                                // Add the result to the spectrum
                                curInstance.spectrum = curSpectrum;
                                curInstance.numberOfModifiedPeptides = 0;
                                curInstance.numberOfUnmodifiedPeptides = 0;
                                curSpectrum.results.Add( curInstance.rank, curInstance );
                            }
                        } else if( tag == "protein" )
                        {
                            // Read the protein tag
                            // <protein id="17" locus="rev_P02413" decoy="1" length="144" />
                            int localId = getAttributeAs<int>( reader, "id", true );
                            string locus = getAttribute( reader, "locus", true );

                            // Create a protein entry iff it's not already in the
                            // map.
                            ProteinInfo pro;
                            ProteinList.Enumerator proItr = proteins.Find( locus );
                            if( proItr.IsValid )
                                pro = proItr.Current.Value;
                            else
                            {
                                pro = new ProteinInfo();
                                pro.locus = locus;

                                pro.isDecoy = Convert.ToBoolean( getAttributeAs<int>( reader, "decoy" ) );
                                pro.length = getAttributeAs<int>( reader, "length" );
                                pro.description = getAttribute( reader, "description" );
                            }
                            proteinIndex[localId] = pro;
                            //Console.WriteLine( pro.locus + " " + pro.description );

                        } else if( tag == "peptide" )
                        {
                            // Read peptide tag
                            //<peptide id="3" sequence="AILAAAGIAEDVK" mass="1240.70" unique="1">
                            curId = getAttributeAs<int>( reader, "id", true );
                            string sequence = getAttribute( reader, "sequence", true );

                            // Get the peptide information
                            PeptideInfo pep = new PeptideInfo();
                            pep.sequence = sequence;

                            if( pep.id == 0 )
                            {
                                pep.mass = getAttributeAs<float>( reader, "mass", true );
                                pep.unique = Convert.ToBoolean( getAttributeAs<int>( reader, "unique" ) );
                                pep.NTerminusIsSpecific = Convert.ToBoolean( getAttributeAs<int>( reader, "NTerminusIsSpecific", true ) );
                                pep.CTerminusIsSpecific = Convert.ToBoolean( getAttributeAs<int>( reader, "CTerminusIsSpecific", true ) );
                            }
                            peptideIndex[curId] = pep;

                        } else if( tag == "locus" )
                        {
                            // Read the locus tag
                            // <locus id="10" offset="165" /> The id in the locus points to the
                            // protein index.
                            int localId = getAttributeAs<int>( reader, "id", true );

                            // Get the protein instance using the locus ID.
                            ProteinInfo pro = proteinIndex[localId];
                            PeptideInfo pep = peptideIndex[curId];
                            ProteinInstanceInfo proInstance = new ProteinInstanceInfo();
                            proInstance.protein = pro;
                            // Get the peptide offset in the protein
                            proInstance.offset = getAttributeAs<int>( reader, "offset", true );
                            // Add it to the protein list that have the peptide
                            pep.proteins.Add( pro.locus, proInstance );
                            //pep.proteins.Insert( pro.locus, pro );

                        } else if( tag == "proteinIndex" )
                        {
                            // Read protein index tag
                            proteinIndex = new ProteinInfo[getAttributeAs<int>( reader, "count", true ) + 1];
                            proteinIndex.Initialize();

                            string database = getAttribute(reader, "database" );
                            if (dbFilepath != null && !String.IsNullOrEmpty(database) && database != dbFilepath)
                                Console.Error.WriteLine("warning: protein database should be the same in all input files");
                            else if (!String.IsNullOrEmpty(database))
                                dbFilepath = Path.GetFileName(database);

                        } else if( tag == "peptideIndex" )
                        {
                            // Read peptide index tag
                            peptideIndex = new PeptideInfo[getAttributeAs<int>( reader, "count", true ) + 1];
                            peptideIndex.Initialize();

                        } else if( tag == "spectraSource" )
                        {
                            // Read the spectra source
                            if( sourceGroup.Length == 0 )
                            {
                                curGroup = getAttribute( reader, "group" );
                            } else
                                curGroup = sourceGroup;

                            // Get the name
                            string sourceName = getAttribute( reader, "name", true );

                            if( curGroup.Length == 0 )
                                curGroup = "/";// + sourceName;
                            else if( curGroup[0] != '/' )
                                curGroup = "/" + curGroup;

                            // Create a souuce group
                            SourceGroupInfo group = groups[curGroup];
                            group.name = curGroup;
                            SourceList sources = group.getSources();
                            SourceInfo source = new SourceInfo( group, sourceName );
                            SourceList.InsertResult rv = sources.Insert( source );
                            if( rv.WasInserted )
                            {
                                curSource = source;
                            } else
                                throw new InvalidDataException( String.Format( "group \"{0}\" already contains a source named \"{1}\" (sources within a group must be named uniquely)", curGroup, source.name ) );

                            group.setSources( sources );

                        } else if( tag == "processingEvent" )
                        {
                            // Read in all the processing parameters used to process the speectra in the current
                            // source
                            ProcessingEvent anEvent = new ProcessingEvent();
                            curSource.processingEvents.Add( anEvent );

                            anEvent.type = getAttribute( reader, "type", true );

                            try
                            {
                                anEvent.startTime = DateTime.ParseExact( getAttribute( reader, "start" ), TimeFormat, null );
                                anEvent.endTime = DateTime.ParseExact( getAttribute( reader, "end" ), TimeFormat, null );
                            } catch { }

                        } else if( tag == "processingParam" )
                        {
                            // Read each processing parameter and store it
                            ProcessingParam aParam = new ProcessingParam();
                            curSource.processingEvents[curSource.processingEvents.Count - 1].parameters.Add( aParam );

                            aParam.name = getAttribute( reader, "name", true );
                            aParam.value = getAttribute( reader, "value", true );

                            if( aParam.name == "ProteinDatabase" )
                            {
                                if( dbFilepath != null && Path.GetFileName( aParam.value ) != dbFilepath )
                                    Console.Error.WriteLine( "warning: protein database filename should be the same in all input files" );
                                else
                                {
                                    dbFilepath = Path.GetFileName( aParam.value );
                                    if( dbFilepath == null )
                                        Console.Error.WriteLine( "warning: protein database for source \"" + curSource.ToString() + "\" is not a valid filename" );
                                }
                            } else if( aParam.name == "SearchScoreWeights" )
                            {
                                string scoreString = aParam.value;
                                string[] scores = scoreString.Split( new char[] { ' ' } );
                                for( int i = 0; i < scores.Length; i = i + 2 )
                                {
                                    scoreNames.Add( scores[i] );
                                }
                            }
                        }
                        break;

                    case XmlNodeType.EndElement:
                        tag = reader.Name;
                        // When we are done parsing the result tag
                        //<result rank="1" FDR="0" ids="3">
                        //  <id peptide="103" mods="9:-272.143" />
                        //  <id peptide="102" mods="8:-185.111" />
                        //  <id peptide="101" mods="7:-128.089" />
                        //</result>

                        if( tag == "result" )
                        {
                            // If the result instance passes the FDR and rank cutoffs
                            if( curInstance.rank <= maxRank &&// curSpectrum.results.Count == 0 &&
                                curInstance.FDR <= maxFDR )
                            {
                                if( this.rtConfig.PreferUnmodifiedPeptides && curInstance.numberOfModifiedPeptides > 0 && curInstance.numberOfUnmodifiedPeptides > 0 )
                                {
                                    PeptideList newPeptideList = new PeptideList();
                                    foreach( VariantInfo peptide in curResult.peptides )
                                    {
                                        if( peptide.mods.numberOfMods == 0 )
                                        {
                                            newPeptideList.Add( peptide );
                                        }
                                    }
                                    curResult.peptides = newPeptideList;
                                }
                                // Add the result to the list
                                ResultList.InsertResult rv = results.Insert( curResult );
                                if( rv.WasInserted )
                                {
                                    //++lastResultId;
                                    //curResult.id = lastResultId;
                                }
                                // Add the spectrum to the result
                                curResult = rv.Element;
                                curResult.spectra.Add( curSpectrum.id, curSpectrum );

                                curInstance.info = curResult;
                                if( curResult == null )
                                    throw new InvalidDataException( "read null result" );

                                // For all peptide variants
                                foreach( VariantInfo pep in curResult.peptides )
                                {
                                    //Console.WriteLine(curSpectrum.id.index + "," + pep.peptide.sequence + "," + pep.mods.ToString());
                                    foreach( ProteinInstanceList.MapPair proItr in pep.peptide.proteins )
                                    {
                                        ProteinInfo pro = proItr.Value.protein;
                                        // Insert the protein index in the list of proteins and give it
                                        // an identifier
                                        ProteinList.InsertResult rv2 = proteins.Insert( pro.locus, pro );
                                        if( rv2.WasInserted )
                                        {
                                            ++lastProteinId;
                                            rv2.Element.Value.id = lastProteinId;
                                        }

                                        // Add peptide to protein
                                        pro.peptides.Add( pep );
                                        // Add spectra to the protein
                                        pro.spectra.Add( curSpectrum.id, curSpectrum );
                                        // Add result to the protein
                                        pro.results.Add( curResult );
                                    }
                                }
                            }
                        } else if( tag == "spectraSource" )
                        {
                            if( rtConfig.AllowSharedSourceNames )
                                sharedSpectra.AddSharedSource( curSource );
                        }
                        break;
                } // switch
            } // while

            /*if( statusOutput != null )
                reportStatus(	reportStatus(	"parsed " + sourceXml.BaseStream.Position + " of " + sourceXml.BaseStream.Length + " bytes; " +
                                groups.Count + " groups; " + results.Count + " results; " + spectra.Count + " spectra; " +
                                peptideIndex.Count + " peptides; " + proteinIndex.Count + " proteins", sourceXml.BaseStream.Position == sourceXml.BaseStream.Length );*/

        }
        
        public enum InputFileType
        {
            Unknown,
            PepXML,
            IdpXML
        }

        void readInputFile(StreamReader xmlStream, out InputFileType inputFileType, out string dbPath)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.ValidationType = ValidationType.None;
            settings.ValidationFlags = System.Xml.Schema.XmlSchemaValidationFlags.None;
            settings.IgnoreProcessingInstructions = true;
            settings.ProhibitDtd = false;
            settings.XmlResolver = null;
            settings.CloseInput = false;

            int numTagsRead = 0;
            bool foundXMLTag = false;
            bool foundDatabase = false;
            inputFileType = InputFileType.Unknown;
            dbPath = null;

            using (XmlReader reader = XmlTextReader.Create(xmlStream, settings))
            {
                reader.Read();
                numTagsRead++;

                if (reader.Name.Equals("xml"))
                {
                    foundXMLTag = true;
                }

                if (foundXMLTag)
                {
                    // assuming msms tag appears in file before database tag
                    while (!foundDatabase && reader.Read() && numTagsRead < 20)
                    {
                        numTagsRead++;

                        if (reader.Name == "msms_pipeline_analysis")
                        {
                            inputFileType = InputFileType.PepXML;
                        }
                        else if (reader.Name == "idPickerPeptides")
                        {
                            inputFileType = InputFileType.IdpXML;
                        }
                        else if (reader.Name == "search_database")
                        {
                            dbPath = Path.GetFileName(getAttribute(reader, "local_path").Replace(".pro", ""));
                            foundDatabase = true;
                        }
                        else if (reader.Name == "proteinIndex")
                        {
                            dbPath = Path.GetFileName(getAttribute(reader, "database").Replace(".pro", ""));
                            if (!String.IsNullOrEmpty(dbPath))
                                foundDatabase = true;
                        }
                    }

                    if (inputFileType == InputFileType.IdpXML && !foundDatabase)
                    {
                        // old idpXML, look for database in spectraSources
                        while (!foundDatabase && reader.Read())
                        {
                            if (reader.Name == "processingParam" &&
                                getAttribute(reader, "name") == "ProteinDatabase")
                            {
                                dbPath = Path.GetFileName(getAttribute(reader, "value"));
                                foundDatabase = true;
                            }
                        }
                    }
                }
            }
        }

        public SQLiteCommand createSQLiteInsertCommand (SQLiteConnection conn, string table, int parameterCount)
        {
            var parameterPlaceholders = new List<string>();
            for (int i = 0; i < parameterCount; ++i) parameterPlaceholders.Add("?");
            var parameterPlaceholdersStr = String.Join(",", parameterPlaceholders.ToArray());
            var insertCommand = new SQLiteCommand(String.Format("INSERT INTO {0} VALUES({1})", table, parameterPlaceholdersStr), conn);
            for (int i = 0; i < parameterCount; ++i)
                insertCommand.Parameters.Add(new SQLiteParameter());
            return insertCommand;
        }

        public void executeSQLiteInsertCommand (SQLiteCommand cmd, IList<object[]> rows)
        {
            foreach (object[] row in rows)
                executeSQLiteInsertCommand(cmd, row);
        }

        public void executeSQLiteInsertCommand (SQLiteCommand cmd, object[] row)
        {
            for (int i = 0; i < row.Length; ++i)
                cmd.Parameters[i].Value = row[i];
            cmd.ExecuteScalar();
        }

        private class PeptideInstanceComparer : EqualityComparer<DataModel.PeptideInstance>
        {
            public override bool Equals (DataModel.PeptideInstance x, DataModel.PeptideInstance y)
            {
                return x.Offset == y.Offset &&
                       x.Length == y.Length &&
                       x.Protein.Accession == y.Protein.Accession;
            }

            public override int GetHashCode (DataModel.PeptideInstance obj)
            {
                return obj.Offset.GetHashCode() ^
                       obj.Length.GetHashCode() ^
                       obj.Protein.Accession.GetHashCode();
            }
        }

        public class DatabaseNotFoundEventArgs : EventArgs
        {
            public string DatabasePath { get; set; }
        }
        public event EventHandler<DatabaseNotFoundEventArgs> DatabaseNotFound;

        public class SourceNotFoundEventArgs : EventArgs
        {
            public string SourcePath { get; set; }
        }
        public event EventHandler<SourceNotFoundEventArgs> SourceNotFound;

        public class ParsingProgressEventArgs : CancelEventArgs
        {
            public long ParsedBytes { get; set; }
            public long TotalBytes { get; set; }
        }
        public event EventHandler<ParsingProgressEventArgs> ParsingProgress;

        public void readXmlToSQLite (IEnumerable<string> xmlFilepaths, string sqlitePath, string rootInputDirectory)
        {
            #region Manually curated sets of model entities
            IList<DataModel.SpectrumSourceGroup> dbGroups;
            IList<DataModel.SpectrumSource> dbSources;
            IList<DataModel.SpectrumSourceGroupLink> dbSourceGroupLinks;
            IList<DataModel.Analysis> dbAnalyses;

            var dbSpectra = new Dictionary<object[], DataModel.Spectrum>();
            var dbProteins = new Dictionary<string, DataModel.Protein>();
            var dbPeptides = new Dictionary<string, DataModel.Peptide>();
            var dbPeptideInstances = new Dictionary<DataModel.PeptideInstance, bool>(new PeptideInstanceComparer());
            var dbModifications = new Dictionary<double, DataModel.Modification>();

            int apCount = 0;
            int psmCount = 0;
            int pmCount = 0;
            #endregion

            long currentMaxProteinLength = 0;

            #region Create the database or load data from an existing database
            using (var sessionFactory = DataModel.SessionFactoryFactory.CreateSessionFactory(sqlitePath, !File.Exists(sqlitePath), false))
            using (var session = sessionFactory.OpenSession())
            {
                try { session.CreateSQLQuery("CREATE TABLE ProteinClusters (ProteinId INTEGER PRIMARY KEY, ClusterId INT)").ExecuteUpdate(); } catch { }
                try { session.CreateSQLQuery("CREATE TABLE ProteinGroups (ProteinId INTEGER PRIMARY KEY, ProteinGroup TEXT)").ExecuteUpdate(); } catch { }

                dbGroups = session.QueryOver<DataModel.SpectrumSourceGroup>().List();
                dbSources = session.QueryOver<DataModel.SpectrumSource>().List();
                dbSourceGroupLinks = session.QueryOver<DataModel.SpectrumSourceGroupLink>().List();
                dbAnalyses = session.QueryOver<DataModel.Analysis>().List();

                foreach (var protein in session.QueryOver<DataModel.Protein>().List())
                    dbProteins.Add(protein.Accession, protein);

                foreach (var peptide in session.QueryOver<DataModel.Peptide>().List())
                    dbPeptides.Add(peptide.Sequence, peptide);

                foreach (var peptideInstance in session.QueryOver<DataModel.PeptideInstance>().List())
                    dbPeptideInstances.Add(peptideInstance, true);

                foreach (var spectrum in session.QueryOver<DataModel.Spectrum>().List())
                    dbSpectra[new object[] { spectrum.Source, spectrum.NativeID }] = spectrum;

                foreach (var modification in session.QueryOver<DataModel.Modification>().List())
                    dbModifications[modification.MonoMassDelta] = modification;

                apCount = session.QueryOver<DataModel.AnalysisParameter>().RowCount();
                psmCount = session.QueryOver<DataModel.PeptideSpectrumMatch>().RowCount();
                pmCount = session.QueryOver<DataModel.PeptideModification>().RowCount();

                currentMaxProteinLength = session.CreateQuery("SELECT MAX(Length) FROM Protein").UniqueResult<long>();
            }
            #endregion

            long parsedBytes = 0;
            long totalBytes = xmlFilepaths.Sum(o => new FileInfo(o).Length);
            
            string lastDatabasePathLocation = Directory.GetCurrentDirectory();
            string lastSourcePathLocation = Directory.GetCurrentDirectory();

            foreach (string xmlFilepath in xmlFilepaths)
            {
                var sourceXml = new StreamReader(xmlFilepath);

                #region Determine input file type and protein database used; also locate database and open it
                InputFileType fileType;
                readInputFile(sourceXml, out fileType, out dbFilepath);
                string databaseFilepath = null;
                try
                {
                    databaseFilepath = Util.FindDatabaseInSearchPath(dbFilepath, rootInputDirectory);
                }
                catch
                {
                    try
                    {
                        databaseFilepath = Util.FindDatabaseInSearchPath(dbFilepath, lastDatabasePathLocation);
                    }
                    catch
                    {
                        if (DatabaseNotFound != null)
                        {
                            var eventArgs = new DatabaseNotFoundEventArgs() { DatabasePath = dbFilepath };
                            DatabaseNotFound(this, eventArgs);
                            if (File.Exists(eventArgs.DatabasePath))
                            {
                                lastDatabasePathLocation = Path.GetDirectoryName(eventArgs.DatabasePath);
                                databaseFilepath = eventArgs.DatabasePath;
                            }
                        }
                    }

                    if(databaseFilepath == null)
                        throw;
                }

                var pd = new pwiz.CLI.proteome.ProteomeDataFile(databaseFilepath);
                var proteinList = pd.proteinList;
                #endregion

                #region Initialize the XmlReader
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.ValidationType = ValidationType.None;
                settings.ValidationFlags = System.Xml.Schema.XmlSchemaValidationFlags.None;
                settings.IgnoreProcessingInstructions = true;
                settings.ProhibitDtd = false;
                settings.XmlResolver = null;

                sourceXml.BaseStream.Seek(0, SeekOrigin.Begin);
                sourceXml.DiscardBufferedData();
                XmlReader reader = XmlTextReader.Create(sourceXml, settings);
                #endregion

                #region Declare model data rows to be inserted into the database
                var proteinRows = new List<object[]>();
                var peptideRows = new List<object[]>();
                var peptideInstanceRows = new List<object[]>();
                var spectrumSourceGroupRows = new List<object[]>();
                var spectrumSourceRows = new List<object[]>();
                var spectrumSourceGroupLinkRows = new List<object[]>();
                var spectrumRows = new List<object[]>();
                var analysisRows = new List<object[]>();
                var apRows = new List<object[]>();
                var modificationRows = new List<object[]>();
                var psmRows = new List<object[]>();
                var psmScoreRows = new List<object[]>();
                var pmRows = new List<object[]>();
                #endregion

                long maxProteinLength = currentMaxProteinLength;

                #region Current object references, used to share information within the XML hierarchy
                int curId = 0;
                DataModel.SpectrumSourceGroup curGroup = null;
                DataModel.SpectrumSource curSource = null;
                DataModel.Spectrum curSpectrum = null;
                DataModel.Peptide curPeptide = null;
                DataModel.PeptideSpectrumMatch curPSM = null;
                DataModel.Analysis curAnalysis = null;
                ProcessingEvent curProcessingEvent = null;
                Map<int, double> curMods = null; // map mod positions to masses
                Map<string, int> curProteins = null; // map protein accessions to offsets for the current peptide
                #endregion

                pwiz.CLI.msdata.MSData curSourceData = null;
                pwiz.CLI.msdata.SpectrumList curSpectrumList = null;
                Set<int> curSpectraIndexSubset = null;

                string tag;
                long lastStatusUpdatePosition = 0;
                long baseStreamLength = sourceXml.BaseStream.Length;

                if (fileType == InputFileType.IdpXML)
                {
                    #region idpXML reading

                    string[] proteinIndex = null;
                    string[] peptideIndex = null;

                    while (reader.Read())
                    {
                        #region Send ParsingProgress event (but read at least 100kb between updates)
                        if (ParsingProgress != null)
                        {
                            long position = sourceXml.BaseStream.Position;
                            if (position > lastStatusUpdatePosition + 100000 || position == baseStreamLength)
                            {
                                parsedBytes += position - lastStatusUpdatePosition;
                                lastStatusUpdatePosition = position;
                                var eventArgs = new ParsingProgressEventArgs() { ParsedBytes = parsedBytes, TotalBytes = totalBytes };
                                ParsingProgress(this, eventArgs);
                                if (eventArgs.Cancel)
                                    return;
                            }
                        }
                        #endregion

                        switch (reader.NodeType)
                        {
                            case XmlNodeType.Element:
                                tag = reader.Name;

                                #region <id peptide="103" mods="9:-272.143" />
                                if (tag == "id")
                                {
                                    int id = getAttributeAs<int>(reader, "peptide", true);

                                    string pep = peptideIndex[id];
                                    double neutralPrecursorMass = curPSM.Spectrum.PrecursorMZ * curPSM.Charge - (curPSM.Charge * pwiz.CLI.chemistry.Proton.Mass);

                                    curPSM.Peptide = dbPeptides[pep];

                                    // Get the mods for the peptide
                                    string modListStr = getAttribute(reader, "mods");
                                    if (modListStr.Length > 0)
                                    {
                                        // Parse the mod string
                                        // Example mod string: "9:-272.143;10:-273.143"
                                        // Each set of mods before the semi-colon represent an interpretation
                                        string[] ambiguousLocations = modListStr.Split(';');

                                        #region Add each modified interpretation
                                        for (int i = 0; i < ambiguousLocations.Length; ++i)
                                        {
                                            curPSM.Id = ++psmCount;
                                            curPSM.MonoisotopicMass = curPSM.Peptide.MonoisotopicMass;
                                            curPSM.MolecularWeight = curPSM.Peptide.MolecularWeight;

                                            string[] modInfoStrs = ambiguousLocations[i].Split(' ');

                                            #region Add mods from this interpretation
                                            foreach (string modInfoStr in modInfoStrs)
                                            {
                                                string[] modPosMassPair = modInfoStr.Split(":".ToCharArray());
                                                string modPosStr = modPosMassPair[0];

                                                double modMass = Convert.ToDouble(modPosMassPair[1]);
                                                curPSM.MonoisotopicMass += modMass;
                                                curPSM.MolecularWeight += modMass;
                                                curPSM.Modifications = new List<DataModel.PeptideModification>();

                                                DataModel.Modification mod;
                                                if (!dbModifications.TryGetValue(modMass, out mod))
                                                {
                                                    mod = new DataModel.Modification()
                                                    {
                                                        Id = dbModifications.Count + 1,
                                                        MonoMassDelta = modMass,
                                                        AvgMassDelta = modMass,
                                                    };
                                                    dbModifications[modMass] = mod;

                                                    modificationRows.Add(new object[]
                                                    {
                                                        mod.Id,
                                                        mod.MonoMassDelta,
                                                        mod.AvgMassDelta,
                                                        mod.Formula,
                                                        mod.Name
                                                    });
                                                }

                                                var peptideModification = new DataModel.PeptideModification()
                                                {
                                                    Id = ++pmCount,
                                                    PeptideSpectrumMatch = curPSM,
                                                    Modification = mod
                                                };

                                                curPSM.Modifications.Add(peptideModification);

                                                int offset;
                                                char site;
                                                if (modPosStr == "n")
                                                {
                                                    offset = int.MinValue;
                                                    site = '(';
                                                }
                                                else if (modPosStr == "c")
                                                {
                                                    offset = int.MaxValue;
                                                    site = ')';
                                                }
                                                else
                                                {
                                                    offset = Convert.ToInt32(modPosStr);
                                                    site = pep[offset - 1];
                                                }

                                                pmRows.Add(new object[]
                                                {
                                                    peptideModification.Id,
                                                    peptideModification.PeptideSpectrumMatch.Id,
                                                    peptideModification.Modification.Id,
                                                    offset,
                                                    site.ToString()
                                                });
                                            }
                                            #endregion

                                            curPSM.MonoisotopicMassError = neutralPrecursorMass - curPSM.MonoisotopicMass;
                                            curPSM.MolecularWeightError = neutralPrecursorMass - curPSM.MolecularWeight;

                                            psmRows.Add(new object[]
                                            {
                                                curPSM.Id,
                                                curPSM.Spectrum.Id,
                                                curPSM.Analysis.Id,
                                                curPSM.Peptide.Id,
                                                curPSM.QValue,
                                                curPSM.MonoisotopicMass,
                                                curPSM.MolecularWeight,
                                                curPSM.MonoisotopicMassError,
                                                curPSM.MolecularWeightError,
                                                curPSM.Rank,
                                                curPSM.Charge
                                            });
                                        }
                                        #endregion
                                    }
                                    else
                                    {
                                        // add the unmodified PSM
                                        curPSM.Id = ++psmCount;
                                        curPSM.MonoisotopicMass = curPSM.Peptide.MonoisotopicMass;
                                        curPSM.MolecularWeight = curPSM.Peptide.MolecularWeight;
                                        curPSM.MonoisotopicMassError = neutralPrecursorMass - curPSM.MonoisotopicMass;
                                        curPSM.MolecularWeightError = neutralPrecursorMass - curPSM.MolecularWeight;

                                        psmRows.Add(new object[]
                                        {
                                            curPSM.Id,
                                            curPSM.Spectrum.Id,
                                            curPSM.Analysis.Id,
                                            curPSM.Peptide.Id,
                                            curPSM.QValue,
                                            curPSM.MonoisotopicMass,
                                            curPSM.MolecularWeight,
                                            curPSM.MonoisotopicMassError,
                                            curPSM.MolecularWeightError,
                                            curPSM.Rank,
                                            curPSM.Charge
                                        });
                                    }
                                }
                                #endregion
                                #region <spectrum id="614" nativeID="614" index="196" z="1" mass="569.32" time="16.7" targets="82" decoys="0" results="1">
                                else if (tag == "spectrum")
                                {
                                    int index = getAttributeAs<int>(reader, "index", true);
                                    string nativeID = getAttribute(reader, "id");
                                    int z = getAttributeAs<int>(reader, "z", true);

                                    object[] sourceIdPair = new object[] { curSource, nativeID };

                                    if (!dbSpectra.TryGetValue(sourceIdPair, out curSpectrum))
                                    {
                                        double neutralPrecursorMass = getAttributeAs<double>(reader, "mass", true);

                                        curSpectrum = dbSpectra[sourceIdPair] = new DataModel.Spectrum()
                                        {
                                            Id = dbSpectra.Count + 1,
                                            Index = index,
                                            NativeID = nativeID,
                                            PrecursorMZ = (neutralPrecursorMass + pwiz.CLI.chemistry.Proton.Mass * z) / z
                                        };

                                        //byte[] peakListBytes = null;

                                        if (curSourceData != null)
                                        {
                                            int realIndex = curSpectrumList.find(nativeID);
                                            if (realIndex != curSpectrumList.size())
                                                curSpectraIndexSubset.Add(realIndex);

                                            /*pwiz.CLI.msdata.Spectrum s = curSpectrumList.spectrum(realIndex, true);
                                            var mzArray = s.getMZArray().data;
                                            var intensityArray = s.getIntensityArray().data;

                                            var stream = new System.IO.MemoryStream();
                                            var writer = new System.IO.BinaryWriter(stream);
                                            for (int i = 0; i < mzArray.Count; ++i)
                                            {
                                                writer.Write(mzArray[i]);
                                                writer.Write(intensityArray[i]);
                                            }
                                            writer.Close();
                                            peakListBytes = stream.ToArray();*/
                                        }

                                        spectrumRows.Add(new object[]
                                    {
                                        dbSpectra.Count,
                                        curSource.Id,
                                        curSpectrum.Index,
                                        curSpectrum.NativeID,
                                        curSpectrum.PrecursorMZ,
                                        //peakListBytes
                                    });
                                    }

                                    curPSM = new DataModel.PeptideSpectrumMatch()
                                    {
                                        Spectrum = curSpectrum,
                                        Analysis = curAnalysis,
                                        Charge = z
                                    };

                                    //curSpectrum.retentionTime = getAttributeAs<float>(reader, "time");

                                    /*curSpectrum.numComparisons = getAttributeAs<int>(reader, "comparisons");
                                    curSpectrum.numComparisons += getAttributeAs<int>(reader, "targets");
                                    curSpectrum.numComparisons += getAttributeAs<int>(reader, "decoys");*/
                                }
                                #endregion
                                #region <result rank="1" FDR="0.1">
                                else if (tag == "result")
                                {
                                    curPSM.Rank = getAttributeAs<int>(reader, "rank", true);
                                    curPSM.QValue = getAttributeAs<float>(reader, "FDR", true);
                                    //getAttribute(reader, "scores", true);
                                }
                                #endregion
                                #region <protein id="17" locus="rev_P02413" decoy="1" length="144" />
                                else if (tag == "protein")
                                {
                                    // Read the protein tag
                                    int localId = getAttributeAs<int>(reader, "id", true);
                                    string locus = getAttribute(reader, "locus", true);
                                    if (locus.Contains("IPI:IPI"))
                                        locus = locus.Split('|')[0];

                                    proteinIndex[localId] = locus;

                                    if (!dbProteins.ContainsKey(locus))
                                    {
                                        //pro.isDecoy = Convert.ToBoolean(getAttributeAs<int>(reader, "decoy"));
                                        int index = proteinList.find(locus);
                                        pwiz.CLI.proteome.Protein pro = proteinList.protein(index);

                                        dbProteins[locus] = new IDPicker.DataModel.Protein()
                                        {
                                            Id = dbProteins.Count + 1,
                                            Accession = pro.id,
                                            Description = pro.description,
                                            Sequence = pro.sequence
                                        };

                                        maxProteinLength = Math.Max(pro.sequence.Length, maxProteinLength);

                                        proteinRows.Add(new object[]
                                    {
                                        dbProteins.Count,
                                        pro.id,
                                        pro.description.Replace("'", "''"),
                                        pro.sequence
                                    });
                                    }
                                }
                                #endregion
                                #region <peptide id="3" sequence="AILAAAGIAEDVK" mass="1240.70" unique="1">
                                else if (tag == "peptide")
                                {
                                    curId = getAttributeAs<int>(reader, "id", true);
                                    string sequence = getAttribute(reader, "sequence", true);

                                    peptideIndex[curId] = sequence;

                                    if (!dbPeptides.ContainsKey(sequence))
                                    {
                                        pwiz.CLI.proteome.Peptide pep = new Peptide(sequence);

                                        dbPeptides[sequence] = new DataModel.Peptide(sequence)
                                        {
                                            Id = dbPeptides.Count + 1,
                                            Instances = new List<DataModel.PeptideInstance>(),
                                            MonoisotopicMass = pep.monoisotopicMass(),
                                            MolecularWeight = pep.molecularWeight()
                                        };

                                        //pep.unique = Convert.ToBoolean(getAttributeAs<int>(reader, "unique"));
                                        bool NTerminusIsSpecific = Convert.ToBoolean(getAttributeAs<int>(reader, "NTerminusIsSpecific", true));
                                        bool CTerminusIsSpecific = Convert.ToBoolean(getAttributeAs<int>(reader, "CTerminusIsSpecific", true));

                                        peptideRows.Add(new object[]
                                    {
                                        dbPeptides.Count,
                                        pep.monoisotopicMass(),
                                        pep.molecularWeight()
                                    });
                                    }

                                }
                                #endregion
                                #region <locus id="10" offset="165" />
                                else if (tag == "locus")
                                {
                                    // Read the locus tag
                                    // 
                                    int localId = getAttributeAs<int>(reader, "id", true);

                                    string pro = proteinIndex[localId];
                                    string pep = peptideIndex[curId];

                                    int offset = getAttributeAs<int>(reader, "offset", true);
                                    var peptideInstance = new IDPicker.DataModel.PeptideInstance()
                                                            {
                                                                Peptide = dbPeptides[pep],
                                                                Protein = dbProteins[pro],
                                                                Offset = offset,
                                                                Length = pep.Length,
                                                                NTerminusIsSpecific = true,
                                                                CTerminusIsSpecific = true,
                                                                MissedCleavages = 0
                                                            };

                                    if (!dbPeptideInstances.ContainsKey(peptideInstance))
                                    {
                                        dbPeptideInstances.Add(peptideInstance, true);
                                        peptideInstanceRows.Add(new object[]
                                    {
                                        dbPeptideInstances.Count,
                                        peptideInstance.Protein.Id,
                                        peptideInstance.Peptide.Id,
                                        offset,
                                        pep.Length,
                                        1, 1, 0 // TODO: GET REAL VALUES!!
                                    });
                                    }
                                }
                                #endregion
                                #region Initialize protein/peptide indexes
                                else if (tag == "proteinIndex")
                                {
                                    // Read protein index tag
                                    proteinIndex = new string[getAttributeAs<int>(reader, "count", true) + 1];
                                    proteinIndex.Initialize();

                                    string database = getAttribute(reader, "database");
                                    if (dbFilepath != null && !String.IsNullOrEmpty(database) && database != dbFilepath)
                                        Console.Error.WriteLine("warning: protein database should be the same in all input files");
                                    else if (!String.IsNullOrEmpty(database))
                                        dbFilepath = database;

                                }
                                else if (tag == "peptideIndex")
                                {
                                    // Read peptide index tag
                                    peptideIndex = new string[getAttributeAs<int>(reader, "count", true) + 1];
                                    peptideIndex.Initialize();
                                }
                                #endregion
                                #region <spectraSource ...>
                                else if (tag == "spectraSource")
                                {
                                    curProcessingEvent = null;
                                    curAnalysis = null;

                                    string groupName = getAttribute(reader, "group");

                                    if (groupName.Length == 0)
                                        groupName = "/";
                                    else if (groupName[0] != '/')
                                        groupName = "/" + groupName;

                                    var groupQuery = from g in dbGroups
                                                     where g.Name == groupName
                                                     select g;

                                    if (groupQuery.Count() == 0)
                                    {
                                        dbGroups.Add(new DataModel.SpectrumSourceGroup()
                                        {
                                            Id = dbGroups.Count + 1,
                                            Name = groupName
                                        });

                                        curGroup = dbGroups.Last();

                                        spectrumSourceGroupRows.Add(new object[]
                                {
                                    curGroup.Id,
                                    curGroup.Name
                                });
                                    }
                                    else
                                        curGroup = groupQuery.First();

                                    string sourceName = getAttribute(reader, "name", true);

                                    var sourceQuery = from s in dbSources
                                                      where s.Name == sourceName
                                                      select s;

                                    if (sourceQuery.Count() == 0)
                                    {
                                        dbSources.Add(new DataModel.SpectrumSource()
                                        {
                                            Id = dbSources.Count + 1,
                                            Name = sourceName,
                                            //URL = source.filepath,
                                            //Spectra = new List<DataModel.Spectrum>(),
                                            Group = curGroup
                                        });

                                        curSource = dbSources.Last();

                                        spectrumSourceRows.Add(new object[]
                                    {
                                        dbSources.Count,
                                        curSource.Name,
                                        curSource.URL,
                                        curGroup.Id,
                                        null // placeholder for gzipped mzML
                                    });

                                        dbSourceGroupLinks.Add(new DataModel.SpectrumSourceGroupLink()
                                        {
                                            Id = dbSourceGroupLinks.Count + 1,
                                            Group = curGroup,
                                            Source = curSource
                                        });

                                        spectrumSourceGroupLinkRows.Add(new object[]
                                    {
                                        dbSourceGroupLinks.Count,
                                        curSource.Id,
                                        curGroup.Id
                                    });

                                        if (groupName != "/")
                                        {
                                            // add the group and all its parent groups to the source
                                            string groupPath = curSource.Group.Name;
                                            string parentGroupName = groupPath.Substring(0, groupPath.LastIndexOf("/"));
                                            while (true)
                                            {
                                                if (String.IsNullOrEmpty(parentGroupName))
                                                    parentGroupName = "/";

                                                // add the parent group if it doesn't exist yet
                                                if (dbGroups.Count(o => o.Name == parentGroupName) == 0)
                                                {
                                                    dbGroups.Add(new DataModel.SpectrumSourceGroup()
                                                    {
                                                        Id = dbGroups.Count + 1,
                                                        Name = parentGroupName
                                                    });

                                                    spectrumSourceGroupRows.Add(new object[]
                                                {
                                                    dbGroups.Last().Id,
                                                    dbGroups.Last().Name
                                                });
                                                }

                                                dbSourceGroupLinks.Add(new DataModel.SpectrumSourceGroupLink()
                                                {
                                                    Id = dbSourceGroupLinks.Count + 1,
                                                    Group = dbGroups.First(o => o.Name == parentGroupName),
                                                    Source = curSource
                                                });

                                                spectrumSourceGroupLinkRows.Add(new object[]
                                            {
                                                dbSourceGroupLinks.Count,
                                                curSource.Id,
                                                dbSourceGroupLinks.Last().Group.Id
                                            });

                                                if (parentGroupName == "/")
                                                    break;
                                                parentGroupName = parentGroupName.Substring(0, parentGroupName.LastIndexOf("/"));
                                            }
                                        }
                                    }
                                    else
                                        curSource = sourceQuery.First();

                                    curSourceData = null;
                                    curSpectrumList = null;
                                    curSpectraIndexSubset = null;

                                    #region Create subset spectrum list
                                    try
                                    {
                                        string sourcePath = Util.FindSourceInSearchPath(curSource.Name, rootInputDirectory);
                                        curSourceData = new pwiz.CLI.msdata.MSDataFile(sourcePath);
                                    }
                                    catch
                                    {
                                        try
                                        {
                                            string sourcePath = Util.FindSourceInSearchPath(curSource.Name, lastSourcePathLocation);
                                            curSourceData = new pwiz.CLI.msdata.MSDataFile(sourcePath);
                                        }
                                        catch
                                        {
                                            if (SourceNotFound != null)
                                            {
                                                var eventArgs = new SourceNotFoundEventArgs() { SourcePath = curSource.Name };
                                                SourceNotFound(this, eventArgs);
                                                if (File.Exists(eventArgs.SourcePath) || Directory.Exists(eventArgs.SourcePath))
                                                {
                                                    lastSourcePathLocation = Path.GetDirectoryName(eventArgs.SourcePath);
                                                    curSourceData = new pwiz.CLI.msdata.MSDataFile(eventArgs.SourcePath);
                                                }
                                            }
                                        }
                                    }

                                    if (curSourceData != null)
                                    {
                                        curSpectrumList = curSourceData.run.spectrumList;
                                        curSpectrumList = new pwiz.CLI.analysis.SpectrumList_PeakPicker(curSpectrumList,
                                            new pwiz.CLI.analysis.LocalMaximumPeakDetector(5), true, new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                                        curSpectrumList = new pwiz.CLI.analysis.SpectrumList_PeakFilter(curSpectrumList,
                                            new pwiz.CLI.analysis.ThresholdFilter(pwiz.CLI.analysis.ThresholdFilter.ThresholdingBy_Type.ThresholdingBy_Count,
                                                                                  50,
                                                                                  pwiz.CLI.analysis.ThresholdFilter.ThresholdingOrientation.Orientation_MostIntense));
                                        curSpectraIndexSubset = new Set<int>();
                                    }
                                    #endregion
                                }
                                #endregion
                                #region Processing events
                                else if (tag == "processingEvent")
                                {
                                    string eventType = getAttribute(reader, "type", true);

                                    if (eventType == "identification")
                                    {
                                        curAnalysis = new DataModel.Analysis();
                                        curAnalysis.Parameters = new Iesi.Collections.Generic.SortedSet<DataModel.AnalysisParameter>();
                                        curProcessingEvent = new ProcessingEvent();
                                        curProcessingEvent.type = eventType;

                                        try
                                        {
                                            curProcessingEvent.startTime = DateTime.ParseExact(getAttribute(reader, "start"), TimeFormat, null);
                                            curAnalysis.StartTime = curProcessingEvent.startTime;
                                            curProcessingEvent.endTime = DateTime.ParseExact(getAttribute(reader, "end"), TimeFormat, null);
                                        }
                                        catch { }
                                    }
                                }
                                else if (curProcessingEvent != null && tag == "processingParam")
                                {
                                    string paramName = getAttribute(reader, "name", true);
                                    string paramValue = getAttribute(reader, "value", true);

                                    if (paramName == "software name")
                                    {
                                        curAnalysis.Software = new IDPicker.DataModel.AnalysisSoftware();
                                        curAnalysis.Software.Name = paramValue;
                                    }
                                    else if (paramName == "software version")
                                    {
                                        curAnalysis.Software.Version = paramValue;
                                        curAnalysis.Name = curAnalysis.Software.Name + " " + curAnalysis.Software.Version;
                                    }
                                    else if (!paramName.Contains(": "))
                                    {
                                        curAnalysis.Parameters.Add(new DataModel.AnalysisParameter()
                                                                  {
                                                                      Name = paramName,
                                                                      Value = paramValue
                                                                  });
                                    }
                                }
                                #endregion
                                break;

                            case XmlNodeType.EndElement:
                                tag = reader.Name;

                                #region Create subset source mzML and add the current rows to the database
                                if (tag == "spectraSource")
                                {
                                    #region Create subset source mzML
                                    if (spectrumSourceRows.Count > 0 &&
                                        spectrumSourceRows.Last()[1] as string == curSource.Name &&
                                        curSourceData != null &&
                                        curSpectraIndexSubset.Count > 0)
                                    {
                                        curSourceData.run.spectrumList = new pwiz.CLI.analysis.SpectrumList_Filter(curSpectrumList,
                                            delegate(pwiz.CLI.msdata.Spectrum s)
                                            {
                                                return curSpectraIndexSubset.Contains(s.index);
                                            });

                                        string tempFilepath = Path.GetTempFileName() + ".mzML.gz";
                                        var writeConfig = new pwiz.CLI.msdata.MSDataFile.WriteConfig()
                                        {
                                            format = pwiz.CLI.msdata.MSDataFile.Format.Format_mzML,
                                            gzipped = true
                                        };

                                        pwiz.CLI.msdata.MSDataFile.write(curSourceData, tempFilepath, writeConfig);
                                        spectrumSourceRows.Last()[4] = File.ReadAllBytes(tempFilepath);
                                        File.Delete(tempFilepath);
                                    }
                                    #endregion

                                    // insert and commit changes on a per-source basis
                                    string connectionString = new SQLiteConnectionStringBuilder() { DataSource = sqlitePath }.ToString();
                                    using (var db = new System.Data.SQLite.SQLiteConnection(connectionString))
                                    {
                                        db.Open();
                                        var transaction = db.BeginTransaction();

                                        var spectrumSourceGroupInsert = createSQLiteInsertCommand(db, "SpectrumSourceGroups", spectrumSourceGroupRows.Count > 0 ? spectrumSourceGroupRows.First().Length : 0);
                                        var spectrumSourceInsert = createSQLiteInsertCommand(db, "SpectrumSources", spectrumSourceRows.Count > 0 ? spectrumSourceRows.First().Length : 0);
                                        var spectrumSourceGroupLinkInsert = createSQLiteInsertCommand(db, "SpectrumSourceGroupLinks", spectrumSourceGroupLinkRows.Count > 0 ? spectrumSourceGroupLinkRows.First().Length : 0);
                                        var spectrumInsert = createSQLiteInsertCommand(db, "Spectra", spectrumRows.Count > 0 ? spectrumRows.First().Length : 0);
                                        var analysisInsert = createSQLiteInsertCommand(db, "Analyses", analysisRows.Count > 0 ? analysisRows.First().Length : 0);
                                        var proteinInsert = createSQLiteInsertCommand(db, "Proteins", proteinRows.Count > 0 ? proteinRows.First().Length : 0);
                                        var peptideInsert = createSQLiteInsertCommand(db, "Peptides", peptideRows.Count > 0 ? peptideRows.First().Length : 0);
                                        var peptideInstanceInsert = createSQLiteInsertCommand(db, "PeptideInstances", peptideInstanceRows.Count > 0 ? peptideInstanceRows.First().Length : 0);
                                        var modificationInsert = createSQLiteInsertCommand(db, "Modifications", modificationRows.Count > 0 ? modificationRows.First().Length : 0);
                                        var apInsert = createSQLiteInsertCommand(db, "AnalysisParameters", apRows.Count > 0 ? apRows.First().Length : 0);
                                        var psmInsert = createSQLiteInsertCommand(db, "PeptideSpectrumMatches", psmRows.Count > 0 ? psmRows.First().Length : 0);
                                        var pmInsert = createSQLiteInsertCommand(db, "PeptideModifications", pmRows.Count > 0 ? pmRows.First().Length : 0);

                                        executeSQLiteInsertCommand(spectrumSourceGroupInsert, spectrumSourceGroupRows);
                                        executeSQLiteInsertCommand(spectrumSourceInsert, spectrumSourceRows);
                                        executeSQLiteInsertCommand(spectrumSourceGroupLinkInsert, spectrumSourceGroupLinkRows);
                                        executeSQLiteInsertCommand(spectrumInsert, spectrumRows);
                                        executeSQLiteInsertCommand(analysisInsert, analysisRows);
                                        executeSQLiteInsertCommand(proteinInsert, proteinRows);
                                        executeSQLiteInsertCommand(peptideInsert, peptideRows);
                                        executeSQLiteInsertCommand(peptideInstanceInsert, peptideInstanceRows);
                                        executeSQLiteInsertCommand(modificationInsert, modificationRows);
                                        executeSQLiteInsertCommand(apInsert, apRows);
                                        executeSQLiteInsertCommand(psmInsert, psmRows);
                                        executeSQLiteInsertCommand(pmInsert, pmRows);

                                        spectrumSourceGroupRows.Clear();
                                        spectrumSourceRows.Clear();
                                        spectrumSourceGroupLinkRows.Clear();
                                        spectrumRows.Clear();
                                        analysisRows.Clear();
                                        proteinRows.Clear();
                                        peptideRows.Clear();
                                        peptideInstanceRows.Clear();
                                        modificationRows.Clear();
                                        apRows.Clear();
                                        psmRows.Clear();
                                        pmRows.Clear();

                                        #region Add an integer set from [0, maxProteinLength)
                                        var createIntegerSetTable = new SQLiteCommand("CREATE TABLE IntegerSet (Value INTEGER PRIMARY KEY)", db);
                                        try { createIntegerSetTable.ExecuteNonQuery(); }
                                        catch { }

                                        var integerInsert = createSQLiteInsertCommand(db, "IntegerSet", 1);
                                        var integerRows = new List<object[]>();
                                        for (long i = currentMaxProteinLength; i < maxProteinLength; ++i)
                                            integerRows.Add(new object[] { i });

                                        executeSQLiteInsertCommand(integerInsert, integerRows);
                                        currentMaxProteinLength = maxProteinLength;
                                        #endregion

                                        transaction.Commit();
                                    }
                                }
                                #endregion
                                #region Determine if the current analysis is already in the database
                                else if (curProcessingEvent != null && tag == "processingEvent")
                                {
                                    // an analysis is unique if its name is unique and its parameter set has some
                                    // difference with other analyses
                                    var analysisQuery = from a in dbAnalyses
                                                        where a.Name == curAnalysis.Name &&
                                                              a.Parameters.ExclusiveOr(curAnalysis.Parameters).Count == 0
                                                        select a;

                                    if (analysisQuery.Count() == 0)
                                    {
                                        dbAnalyses.Add(curAnalysis);
                                        curAnalysis.Id = dbAnalyses.Count;
                                        curAnalysis.Type = DataModel.AnalysisType.DatabaseSearch;

                                        analysisRows.Add(new object[]
                                                    {
                                                        curAnalysis.Id,
                                                        curAnalysis.Name,
                                                        curAnalysis.Software.Name,
                                                        curAnalysis.Software.Version,
                                                        curAnalysis.Type,
                                                        curAnalysis.StartTime.ToString("yyyy-MM-dd HH:mm:ss")
                                                    });

                                        foreach (var analysisParameter in curAnalysis.Parameters)
                                        {
                                            apRows.Add(new object[]
                                                    {
                                                        ++apCount,
                                                        curAnalysis.Id,
                                                        analysisParameter.Name,
                                                        analysisParameter.Value
                                                    });
                                        }
                                    }
                                    else
                                        curAnalysis = analysisQuery.Single();

                                    curProcessingEvent = null;
                                }
                                #endregion
                                break;
                        } // switch
                    } // while
                    #endregion
                }
                else
                {
                    #region pepXML reading

                    while (reader.Read())
                    {
                        #region Send ParsingProgress event (but read at least 100kb between updates)
                        if (ParsingProgress != null)
                        {
                            long position = sourceXml.BaseStream.Position;
                            if (position > lastStatusUpdatePosition + 100000 || position == baseStreamLength)
                            {
                                parsedBytes += position - lastStatusUpdatePosition;
                                lastStatusUpdatePosition = position;
                                var eventArgs = new ParsingProgressEventArgs() { ParsedBytes = parsedBytes, TotalBytes = totalBytes };
                                ParsingProgress(this, eventArgs);
                                if (eventArgs.Cancel)
                                    return;
                            }
                        }
                        #endregion

                        switch (reader.NodeType)
                        {
                            case XmlNodeType.Element:
                                tag = reader.Name;

                                #region <search_score name="mvh" value="42"/>
                                if (tag == "search_score")
                                {
                                    try
                                    {
                                        string name = getAttribute(reader, "name");
                                        double value = getAttributeAs<double>(reader, "value");
                                        psmScoreRows.Add(new object[]
                                                        {
                                                            curPSM.Id,
                                                            value,
                                                            name,
                                                        });
                                    }
                                    catch
                                    {
                                        // ignore non-numeric values
                                    }
                                }
                                #endregion
                                #region <search_hit hit_rank="1" peptide="QTSSM" peptide_prev_aa="R" peptide_next_aa="-" protein="rev_RPA0405" peptide_offset="476" num_tot_proteins="1" num_matched_ions="0" tot_num_ions="6" calc_neutral_pep_mass="123" massdiff="123" num_tol_term="2" num_missed_cleavages="0">
                                else if (tag == "search_hit")
                                {
                                    #region Get current peptide (from dbPeptides map if possible)
                                    string sequence = getAttribute(reader, "peptide");
                                    if (!dbPeptides.TryGetValue(sequence, out curPeptide))
                                    {
                                        pwiz.CLI.proteome.Peptide pep = new Peptide(sequence);

                                        curPeptide = dbPeptides[sequence] = new DataModel.Peptide(sequence)
                                        {
                                            Id = dbPeptides.Count + 1,
                                            Instances = new List<DataModel.PeptideInstance>(),
                                            MonoisotopicMass = pep.monoisotopicMass(),
                                            MolecularWeight = pep.molecularWeight()
                                        };

                                        //pep.unique = Convert.ToBoolean(getAttributeAs<int>(reader, "unique"));
                                        //bool NTerminusIsSpecific = Convert.ToBoolean(getAttributeAs<int>(reader, "NTerminusIsSpecific", true));
                                        //bool CTerminusIsSpecific = Convert.ToBoolean(getAttributeAs<int>(reader, "CTerminusIsSpecific", true));

                                        peptideRows.Add(new object[]
                                                        {
                                                            curPeptide.Id,
                                                            curPeptide.MonoisotopicMass,
                                                            curPeptide.MolecularWeight
                                                        });
                                    }
                                    #endregion

                                    curPSM.Id = ++pmCount;
                                    curPSM.Peptide = curPeptide;
                                    curPSM.Rank = getAttributeAs<int>(reader, "hit_rank");
                                    curPSM.MonoisotopicMass = curPSM.Peptide.MonoisotopicMass;
                                    curPSM.MolecularWeight = curPSM.Peptide.MolecularWeight;

                                    curProteins = new Map<string, int>();

                                    string locus = getAttribute(reader, "protein");

                                    if (locus.Contains("IPI:IPI"))
                                        locus = locus.Split('|')[0];

                                    string offsetString = getAttribute(reader, "peptide_offset");
                                    int offset;
                                    if (int.TryParse(offsetString, out offset))
                                        curProteins[locus] = offset;
                                    else
                                        curProteins[locus] = -1;
                                }
                                #endregion
                                #region <alternative_protein protein="ABCD" />
                                else if (tag == "alternative_protein")
                                {
                                    string locus = getAttribute(reader, "protein");

                                    if (locus.Contains("IPI:IPI"))
                                        locus = locus.Split('|')[0];

                                    string offsetString = getAttribute(reader, "peptide_offset");
                                    int offset;
                                    if (int.TryParse(offsetString, out offset))
                                        curProteins[locus] = offset;
                                    else
                                        curProteins[locus] = -1;
                                }
                                #endregion
                                #region <modification_info mod_nterm_mass="42" mod_cterm_mass="42">
                                else if (tag == "modification_info")
                                {
                                    curMods = new Map<int, double>();

                                    double nTermModMass = getAttributeAs<double>(reader, "mod_nterm_mass");
                                    if (nTermModMass > 0)
                                        curMods[int.MinValue] = nTermModMass;

                                    double cTermModMass = getAttributeAs<double>(reader, "mod_cterm_mass");
                                    if (cTermModMass > 0)
                                        curMods[int.MaxValue] = cTermModMass;
                                }
                                #endregion
                                #region <mod_aminoacid_mass position="7" mass="42" />
                                else if (tag == "mod_aminoacid_mass")
                                {
                                    double modMass = getAttributeAs<double>(reader, "mass");
                                    int position = getAttributeAs<int>(reader, "position");
                                    Formula aaFormula = AminoAcidInfo.record(curPeptide.Sequence[position - 1]).residueFormula;
                                    curMods.Add(position, modMass - aaFormula.monoisotopicMass());
                                }
                                #endregion
                                #region <spectrum_query spectrum="abc.42.42.2" start_scan="42" end_scan="42" spectrumNativeID="controllerType=0 controllerNumber=1 scan=42" spectrumIndex="42" precursor_neutral_mass="42" assumed_charge="2" index="1" retention_time_sec="42">
                                else if (tag == "spectrum_query")
                                {
                                    int index;
                                    string nativeID;
                                    try
                                    {
                                        index = getAttributeAs<int>(reader, "spectrumIndex", true);
                                        nativeID = getAttribute(reader, "spectrumNativeID", true);
                                    }
                                    catch
                                    {
                                        nativeID = getAttribute(reader, "start_scan", true);
                                        index = Convert.ToInt32(nativeID);
                                    }

                                    int z = getAttributeAs<int>(reader, "assumed_charge", true);

                                    object[] sourceIdPair = new object[] { curSource, nativeID };

                                    if (!dbSpectra.TryGetValue(sourceIdPair, out curSpectrum))
                                    {
                                        double neutralPrecursorMass = getAttributeAs<double>(reader, "precursor_neutral_mass", true);

                                        curSpectrum = dbSpectra[sourceIdPair] = new DataModel.Spectrum()
                                        {
                                            Id = dbSpectra.Count + 1,
                                            Index = index,
                                            NativeID = nativeID,
                                            PrecursorMZ = (neutralPrecursorMass + pwiz.CLI.chemistry.Proton.Mass * z) / z
                                        };

                                        if (curSourceData != null)
                                        {
                                            int realIndex = curSpectrumList.find(nativeID);
                                            if (realIndex != curSpectrumList.size())
                                                curSpectraIndexSubset.Add(realIndex);
                                        }

                                        spectrumRows.Add(new object[]
                                                        {
                                                            dbSpectra.Count,
                                                            curSource.Id,
                                                            curSpectrum.Index,
                                                            curSpectrum.NativeID,
                                                            curSpectrum.PrecursorMZ
                                                        });
                                    }

                                    curPSM = new DataModel.PeptideSpectrumMatch()
                                    {
                                        Spectrum = curSpectrum,
                                        Analysis = curAnalysis,
                                        Charge = z
                                    };

                                    //curSpectrum.retentionTime = getAttributeAs<float>(reader, "time");

                                    /*curSpectrum.numComparisons = getAttributeAs<int>(reader, "comparisons");
                                    curSpectrum.numComparisons += getAttributeAs<int>(reader, "targets");
                                    curSpectrum.numComparisons += getAttributeAs<int>(reader, "decoys");*/
                                }
                                #endregion
                                #region <search_summary base_name="abc" search_engine="MyriMatch" precursor_mass_type="monoisotopic" fragment_mass_type="monoisotopic" out_data_type="n/a" out_data="n/a" search_id="1">
                                else if (tag == "search_summary")
                                {
                                    curAnalysis = new DataModel.Analysis()
                                    {
                                        Software = new DataModel.AnalysisSoftware() 
                                        {
                                            Name = getAttribute(reader, "search_engine")
                                        },
                                        Parameters = new Iesi.Collections.Generic.SortedSet<DataModel.AnalysisParameter>()
                                    };

                                    #region Get root group (from dbGroups if possible)
                                    string groupName = "/";
                                    var groupQuery = from g in dbGroups
                                                     where g.Name == groupName
                                                     select g;

                                    if (groupQuery.Count() == 0)
                                    {
                                        dbGroups.Add(new DataModel.SpectrumSourceGroup()
                                        {
                                            Id = dbGroups.Count + 1,
                                            Name = groupName
                                        });

                                        curGroup = dbGroups.Last();

                                        spectrumSourceGroupRows.Add(new object[]
                                                                    {
                                                                        curGroup.Id,
                                                                        curGroup.Name
                                                                    });
                                    }
                                    else
                                        curGroup = groupQuery.Single();
                                    #endregion

                                    #region Get current spectrum source (from dbSources if possible)
                                    string sourceName = getAttribute(reader, "base_name", true);
                                    var sourceQuery = from s in dbSources
                                                      where s.Name == sourceName
                                                      select s;

                                    if (sourceQuery.Count() == 0)
                                    {
                                        dbSources.Add(new DataModel.SpectrumSource()
                                        {
                                            Id = dbSources.Count + 1,
                                            Name = sourceName,
                                            //URL = source.filepath,
                                            //Spectra = new List<DataModel.Spectrum>(),
                                            Group = curGroup
                                        });

                                        curSource = dbSources.Last();

                                        spectrumSourceRows.Add(new object[]
                                                                {
                                                                    dbSources.Count,
                                                                    curSource.Name,
                                                                    curSource.URL,
                                                                    curGroup.Id,
                                                                    null // placeholder for gzipped mzML
                                                                });

                                        dbSourceGroupLinks.Add(new DataModel.SpectrumSourceGroupLink()
                                                                {
                                                                    Id = dbSourceGroupLinks.Count + 1,
                                                                    Group = curGroup,
                                                                    Source = curSource
                                                                });

                                        spectrumSourceGroupLinkRows.Add(new object[]
                                                                        {
                                                                            dbSourceGroupLinks.Count,
                                                                            curSource.Id,
                                                                            curGroup.Id
                                                                        });
                                    }
                                    else
                                        curSource = sourceQuery.Single();
                                    #endregion

                                    curSourceData = null;
                                    curSpectrumList = null;
                                    curSpectraIndexSubset = null;

                                    #region Create subset spectrum list
                                    try
                                    {
                                        string sourcePath = Util.FindSourceInSearchPath(curSource.Name, rootInputDirectory);
                                        curSourceData = new pwiz.CLI.msdata.MSDataFile(sourcePath);
                                    }
                                    catch
                                    {
                                        try
                                        {
                                            string sourcePath = Util.FindSourceInSearchPath(curSource.Name, lastSourcePathLocation);
                                            curSourceData = new pwiz.CLI.msdata.MSDataFile(sourcePath);
                                        }
                                        catch
                                        {
                                            if (SourceNotFound != null)
                                            {
                                                var eventArgs = new SourceNotFoundEventArgs() { SourcePath = curSource.Name };
                                                SourceNotFound(this, eventArgs);
                                                if (File.Exists(eventArgs.SourcePath) || Directory.Exists(eventArgs.SourcePath))
                                                {
                                                    lastSourcePathLocation = Path.GetDirectoryName(eventArgs.SourcePath);
                                                    curSourceData = new pwiz.CLI.msdata.MSDataFile(eventArgs.SourcePath);
                                                }
                                            }
                                        }
                                    }

                                    if (curSourceData != null)
                                    {
                                        curSpectrumList = curSourceData.run.spectrumList;
                                        curSpectrumList = new pwiz.CLI.analysis.SpectrumList_PeakPicker(curSpectrumList,
                                            new pwiz.CLI.analysis.LocalMaximumPeakDetector(5), true, new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                                        curSpectrumList = new pwiz.CLI.analysis.SpectrumList_PeakFilter(curSpectrumList,
                                            new pwiz.CLI.analysis.ThresholdFilter(pwiz.CLI.analysis.ThresholdFilter.ThresholdingBy_Type.ThresholdingBy_Count,
                                                                                  50,
                                                                                  pwiz.CLI.analysis.ThresholdFilter.ThresholdingOrientation.Orientation_MostIntense));
                                        curSpectraIndexSubset = new Set<int>();
                                    }
                                    #endregion
                                }
                                else if (tag == "parameter")
                                {
                                    string paramName = getAttribute(reader, "name", true);
                                    string paramValue = getAttribute(reader, "value", true);

                                    if (paramName == "SearchEngine: Version")
                                    {
                                        curAnalysis.Software.Version = paramValue;
                                        curAnalysis.Name = curAnalysis.Software.Name + " " + curAnalysis.Software.Version;
                                    }
                                    else if(paramName == "SearchTime: Started")
                                    {
                                        curAnalysis.StartTime = DateTime.ParseExact(paramValue, "HH:mm:ss 'on' dd/MM/yyyy", System.Globalization.DateTimeFormatInfo.CurrentInfo);
                                    }
                                    else if(paramName.Contains("Config: "))
                                    {
                                        curAnalysis.Parameters.Add(new DataModel.AnalysisParameter()
                                                                    {
                                                                        Name = paramName.Substring(8),
                                                                        Value = paramValue
                                                                    });
                                    }
                                    else if (!paramName.Contains(": "))
                                    {
                                        curAnalysis.Parameters.Add(new DataModel.AnalysisParameter()
                                                                    {
                                                                        Name = paramName,
                                                                        Value = paramValue
                                                                    });
                                    }
                                }
                                #endregion
                                break;

                            case XmlNodeType.EndElement:
                                tag = reader.Name;

                                if (tag == "search_hit")
                                {
                                    #region Add proteins and peptide instances

                                    foreach (var itr in curProteins)
                                    {
                                        string locus = itr.Key;
                                        int offset = itr.Value;

                                        DataModel.Protein curProtein;
                                        if (!dbProteins.TryGetValue(locus, out curProtein))
                                        {
                                            //pro.isDecoy = Convert.ToBoolean(getAttributeAs<int>(reader, "decoy"));
                                            int index = proteinList.find(locus);
                                            pwiz.CLI.proteome.Protein pro = proteinList.protein(index);

                                            curProtein = dbProteins[locus] = new DataModel.Protein()
                                            {
                                                Id = dbProteins.Count + 1,
                                                Accession = pro.id,
                                                Description = pro.description,
                                                Sequence = pro.sequence
                                            };

                                            maxProteinLength = Math.Max(pro.sequence.Length, maxProteinLength);

                                            proteinRows.Add(new object[]
                                                            {
                                                                dbProteins.Count,
                                                                pro.id,
                                                                pro.description.Replace("'", "''"),
                                                                pro.sequence
                                                            });
                                        }

                                        var peptideOffets = new List<int>();

                                        // if necessary, look up the real offset(s) of the peptide
                                        if (offset < 0 ||
                                            (offset + curPeptide.Sequence.Length - 1) >= curProtein.Sequence.Length ||
                                            curProtein.Sequence.Substring(offset, curPeptide.Sequence.Length) != curPeptide.Sequence)
                                        {
                                            int start = curProtein.Sequence.IndexOf(curPeptide.Sequence);
                                            do
                                            {
                                                peptideOffets.Add(start);
                                                start = curProtein.Sequence.IndexOf(curPeptide.Sequence, start + 1);
                                            }
                                            while (start >= 0);
                                        }
                                        else
                                            peptideOffets.Add(offset);

                                        foreach (int peptideOffset in peptideOffets)
                                        {
                                            var peptideInstance = new DataModel.PeptideInstance()
                                            {
                                                Peptide = curPeptide,
                                                Protein = curProtein,
                                                Offset = peptideOffset,
                                                Length = curPeptide.Sequence.Length,
                                                // TODO: GET REAL VALUES!!
                                                NTerminusIsSpecific = true,
                                                CTerminusIsSpecific = true,
                                                MissedCleavages = 0
                                            };

                                            if (!dbPeptideInstances.ContainsKey(peptideInstance))
                                            {
                                                dbPeptideInstances.Add(peptideInstance, true);
                                                peptideInstanceRows.Add(new object[]
                                                                        {
                                                                            dbPeptideInstances.Count,
                                                                            peptideInstance.Protein.Id,
                                                                            peptideInstance.Peptide.Id,
                                                                            peptideInstance.Offset,
                                                                            peptideInstance.Length,
                                                                            peptideInstance.NTerminusIsSpecific ? 1 : 0,
                                                                            peptideInstance.CTerminusIsSpecific ? 1 : 0,
                                                                            peptideInstance.MissedCleavages
                                                                        });
                                            }
                                        }
                                    }
                                    #endregion

                                    double neutralPrecursorMass = curPSM.Spectrum.PrecursorMZ * curPSM.Charge - (curPSM.Charge * pwiz.CLI.chemistry.Proton.Mass);

                                    curPSM.MonoisotopicMassError = neutralPrecursorMass - curPSM.MonoisotopicMass;
                                    curPSM.MolecularWeightError = neutralPrecursorMass - curPSM.MolecularWeight;

                                    psmRows.Add(new object[]
                                                {
                                                    curPSM.Id,
                                                    curPSM.Spectrum.Id,
                                                    curPSM.Analysis.Id,
                                                    curPSM.Peptide.Id,
                                                    curPSM.QValue,
                                                    curPSM.MonoisotopicMass,
                                                    curPSM.MolecularWeight,
                                                    curPSM.MonoisotopicMassError,
                                                    curPSM.MolecularWeightError,
                                                    curPSM.Rank,
                                                    curPSM.Charge
                                                });
                                }
                                #region Add current modifications
                                else if (tag == "modification_info")
                                {
                                    foreach (var itr in curMods)
                                    {
                                        int position = itr.Key;
                                        double mass = itr.Value;

                                        DataModel.Modification mod;
                                        if (!dbModifications.TryGetValue(mass, out mod))
                                        {
                                            mod = dbModifications[mass] = new DataModel.Modification()
                                            {
                                                Id = dbModifications.Count + 1,
                                                MonoMassDelta = mass,
                                                AvgMassDelta = mass,
                                            };

                                            modificationRows.Add(new object[]
                                                                {
                                                                    mod.Id,
                                                                    mod.MonoMassDelta,
                                                                    mod.AvgMassDelta,
                                                                    mod.Formula,
                                                                    mod.Name
                                                                });
                                        }

                                        curPSM.MonoisotopicMass += mass;
                                        curPSM.MolecularWeight += mass;

                                        char site;
                                        if (position == int.MinValue)
                                            site = '(';
                                        else if (position == int.MaxValue)
                                            site = ')';
                                        else
                                            site = curPSM.Peptide.Sequence[position - 1];

                                        pmRows.Add(new object[]
                                                    {
                                                        ++pmCount,
                                                        curPSM.Id,
                                                        mod.Id,
                                                        position,
                                                        site.ToString()
                                                    });
                                    }
                                }
                                #endregion
                                #region Create subset source mzML and add the current rows to the database
                                else if (tag == "msms_run_summary")
                                {
                                    #region Create subset source mzML
                                    if (spectrumSourceRows.Count > 0 &&
                                        spectrumSourceRows.Last()[1] as string == curSource.Name &&
                                        curSourceData != null &&
                                        curSpectraIndexSubset.Count > 0)
                                    {
                                        curSourceData.run.spectrumList = new pwiz.CLI.analysis.SpectrumList_Filter(curSpectrumList,
                                            delegate(pwiz.CLI.msdata.Spectrum s)
                                            {
                                                return curSpectraIndexSubset.Contains(s.index);
                                            });

                                        string tempFilepath = Path.GetTempFileName() + ".mzML.gz";
                                        var writeConfig = new pwiz.CLI.msdata.MSDataFile.WriteConfig()
                                        {
                                            format = pwiz.CLI.msdata.MSDataFile.Format.Format_mzML,
                                            gzipped = true
                                        };

                                        pwiz.CLI.msdata.MSDataFile.write(curSourceData, tempFilepath, writeConfig);
                                        spectrumSourceRows.Last()[4] = File.ReadAllBytes(tempFilepath);
                                        File.Delete(tempFilepath);
                                    }
                                    #endregion

                                    // insert and commit changes on a per-source basis
                                    string connectionString = new SQLiteConnectionStringBuilder() { DataSource = sqlitePath }.ToString();
                                    using (var db = new System.Data.SQLite.SQLiteConnection(connectionString))
                                    {
                                        db.Open();
                                        var transaction = db.BeginTransaction();

                                        var spectrumSourceGroupInsert = createSQLiteInsertCommand(db, "SpectrumSourceGroups", spectrumSourceGroupRows.Count > 0 ? spectrumSourceGroupRows.First().Length : 0);
                                        var spectrumSourceInsert = createSQLiteInsertCommand(db, "SpectrumSources", spectrumSourceRows.Count > 0 ? spectrumSourceRows.First().Length : 0);
                                        var spectrumSourceGroupLinkInsert = createSQLiteInsertCommand(db, "SpectrumSourceGroupLinks", spectrumSourceGroupLinkRows.Count > 0 ? spectrumSourceGroupLinkRows.First().Length : 0);
                                        var spectrumInsert = createSQLiteInsertCommand(db, "Spectra", spectrumRows.Count > 0 ? spectrumRows.First().Length : 0);
                                        var analysisInsert = createSQLiteInsertCommand(db, "Analyses", analysisRows.Count > 0 ? analysisRows.First().Length : 0);
                                        var proteinInsert = createSQLiteInsertCommand(db, "Proteins", proteinRows.Count > 0 ? proteinRows.First().Length : 0);
                                        var peptideInsert = createSQLiteInsertCommand(db, "Peptides", peptideRows.Count > 0 ? peptideRows.First().Length : 0);
                                        var peptideInstanceInsert = createSQLiteInsertCommand(db, "PeptideInstances", peptideInstanceRows.Count > 0 ? peptideInstanceRows.First().Length : 0);
                                        var modificationInsert = createSQLiteInsertCommand(db, "Modifications", modificationRows.Count > 0 ? modificationRows.First().Length : 0);
                                        var apInsert = createSQLiteInsertCommand(db, "AnalysisParameters", apRows.Count > 0 ? apRows.First().Length : 0);
                                        var psmInsert = createSQLiteInsertCommand(db, "PeptideSpectrumMatches", psmRows.Count > 0 ? psmRows.First().Length : 0);
                                        var psmScoresInsert = createSQLiteInsertCommand(db, "PeptideSpectrumMatchScores", psmScoreRows.Count > 0 ? psmScoreRows.First().Length : 0);
                                        var pmInsert = createSQLiteInsertCommand(db, "PeptideModifications", pmRows.Count > 0 ? pmRows.First().Length : 0);

                                        executeSQLiteInsertCommand(spectrumSourceGroupInsert, spectrumSourceGroupRows);
                                        executeSQLiteInsertCommand(spectrumSourceInsert, spectrumSourceRows);
                                        executeSQLiteInsertCommand(spectrumSourceGroupLinkInsert, spectrumSourceGroupLinkRows);
                                        executeSQLiteInsertCommand(spectrumInsert, spectrumRows);
                                        executeSQLiteInsertCommand(analysisInsert, analysisRows);
                                        executeSQLiteInsertCommand(proteinInsert, proteinRows);
                                        executeSQLiteInsertCommand(peptideInsert, peptideRows);
                                        executeSQLiteInsertCommand(peptideInstanceInsert, peptideInstanceRows);
                                        executeSQLiteInsertCommand(modificationInsert, modificationRows);
                                        executeSQLiteInsertCommand(apInsert, apRows);
                                        executeSQLiteInsertCommand(psmInsert, psmRows);
                                        executeSQLiteInsertCommand(psmScoresInsert, psmScoreRows);
                                        executeSQLiteInsertCommand(pmInsert, pmRows);

                                        spectrumSourceGroupRows.Clear();
                                        spectrumSourceRows.Clear();
                                        spectrumSourceGroupLinkRows.Clear();
                                        spectrumRows.Clear();
                                        analysisRows.Clear();
                                        proteinRows.Clear();
                                        peptideRows.Clear();
                                        peptideInstanceRows.Clear();
                                        modificationRows.Clear();
                                        apRows.Clear();
                                        psmRows.Clear();
                                        psmScoreRows.Clear();
                                        pmRows.Clear();

                                        #region Add an integer set from [0, maxProteinLength)
                                        var createIntegerSetTable = new SQLiteCommand("CREATE TABLE IntegerSet (Value INTEGER PRIMARY KEY)", db);
                                        try { createIntegerSetTable.ExecuteNonQuery(); }
                                        catch { }

                                        var integerInsert = createSQLiteInsertCommand(db, "IntegerSet", 1);
                                        var integerRows = new List<object[]>();
                                        for (long i = currentMaxProteinLength; i < maxProteinLength; ++i)
                                            integerRows.Add(new object[] { i });

                                        executeSQLiteInsertCommand(integerInsert, integerRows);
                                        currentMaxProteinLength = maxProteinLength;
                                        #endregion

                                        transaction.Commit();
                                    }
                                }
                                #endregion
                                #region Determine if the current analysis is already in the database
                                else if (curProcessingEvent != null && tag == "processingEvent")
                                {
                                    // an analysis is unique if its name is unique and its parameter set has some
                                    // difference with other analyses
                                    var analysisQuery = from a in dbAnalyses
                                                        where a.Name == curAnalysis.Name &&
                                                              a.Parameters.ExclusiveOr(curAnalysis.Parameters).Count == 0
                                                        select a;

                                    if (analysisQuery.Count() == 0)
                                    {
                                        dbAnalyses.Add(curAnalysis);
                                        curAnalysis.Id = dbAnalyses.Count;
                                        curAnalysis.Type = DataModel.AnalysisType.DatabaseSearch;

                                        analysisRows.Add(new object[]
                                                    {
                                                        curAnalysis.Id,
                                                        curAnalysis.Name,
                                                        curAnalysis.Software.Name,
                                                        curAnalysis.Software.Version,
                                                        curAnalysis.Type,
                                                        curAnalysis.StartTime.ToString("yyyy-MM-dd HH:mm:ss")
                                                    });

                                        foreach (var analysisParameter in curAnalysis.Parameters)
                                        {
                                            apRows.Add(new object[]
                                                    {
                                                        ++apCount,
                                                        curAnalysis.Id,
                                                        analysisParameter.Name,
                                                        analysisParameter.Value
                                                    });
                                        }
                                    }
                                    else
                                        curAnalysis = analysisQuery.Single();

                                    curProcessingEvent = null;
                                }
                                #endregion
                                
                                break;
                        } // switch
                    } // while
                    #endregion
                }
            }
        }
        #endregion

        public void assembleSQLiteFile (string filename)
        {
            string databaseFilepath = Util.FindDatabaseInSearchPath(dbFilepath, ".");
            var pd = new pwiz.CLI.proteome.ProteomeDataFile(databaseFilepath);
            var proteinList = pd.proteinList;

            using (var sessionFactory = DataModel.SessionFactoryFactory.CreateSessionFactory(filename, !File.Exists(filename), false))
            using (var session = sessionFactory.OpenSession())
            {
                //var databaseRow = we.PeptideSet..Databases.AddDatabasesRow(Path.GetFileNameWithoutExtension(dbFilepath), dbFilepath);
                var dbGroups = new List<DataModel.SpectrumSourceGroup>();
                var dbSources = new List<DataModel.SpectrumSource>();
                var dbSourceGroupLinks = new List<DataModel.SpectrumSourceGroupLink>();
                var dbSpectra = new List<DataModel.Spectrum>();
                var dbModifications = session.QueryOver<DataModel.Modification>().List();
                var dbPSMs = new List<DataModel.PeptideSpectrumMatch>();
                var dbPeptideModifications = new List<DataModel.PeptideModification>();
                var dbPeptideInstances = session.QueryOver<DataModel.PeptideInstance>().List();

                var dbProteins = new Dictionary<string, DataModel.Protein>();
                foreach (var protein in session.QueryOver<DataModel.Protein>().List())
                    dbProteins.Add(protein.Accession, protein);

                var dbPeptides = new Dictionary<string, DataModel.Peptide>();
                foreach (var peptide in session.QueryOver<DataModel.Peptide>().List())
                    dbPeptides.Add(peptide.Sequence, peptide);

                groups.assembleParentGroups();

                foreach (SourceGroupInfo group in groups.Values)
                {
                    if (dbGroups.Count(o => o.Name == group.name) == 0)
                        dbGroups.Add(new DataModel.SpectrumSourceGroup()
                                    {
                                        Name = group.name,
                                    }
                        );

                    foreach (SourceInfo source in group.sources)
                    {
                        dbSources.Add(new DataModel.SpectrumSource()
                                        {
                                            Name = source.name,
                                            URL = source.filepath,
                                            Spectra = new List<DataModel.Spectrum>(),
                                            Group = dbGroups.Find(o => o.Name == group.name)
                                        }
                                    );

                        DataModel.SpectrumSource dbSource = dbSources.Last();

                        // add the group and all its parent groups to the source
                        string groupPath = dbSource.Group.Name;
                        var parentGroup = group;
                        while(true)
                        {
                            // add the parent group if it doesn't exist yet
                            if (dbGroups.Count(o => o.Name == parentGroup.name) == 0)
                                dbGroups.Add(new DataModel.SpectrumSourceGroup()
                                            {
                                                Name = group.name,
                                            }
                                );

                            dbSourceGroupLinks.Add(new DataModel.SpectrumSourceGroupLink()
                                                    {
                                                        Group = dbGroups.Find(o => o.Name == parentGroup.name),
                                                        Source = dbSource
                                                    });
                            //dbSource.Groups.Add(dbGroups.Find(o=> o.Name == parentGroup.name));

                            if (parentGroup.isRootGroup())
                                break;
                            parentGroup = parentGroup.parent;
                        }

                        foreach (SpectrumInfo s in source.spectra.Values)
                        {
                            var query = from dbs in dbSource.Spectra
                                        where dbs.Index == s.id.index &&
                                              dbs.Source.Name == source.name
                                        select dbs;

                            DataModel.Spectrum spectrum;
                            if (query.Count() == 0)
                            {
                                spectrum = new DataModel.Spectrum()
                                            {
                                                Index = s.id.index,
                                                NativeID = s.nativeID,
                                                PrecursorMZ = (s.precursorMass + (pwiz.CLI.chemistry.Proton.Mass * s.id.charge)) / s.id.charge,
                                                Source = dbSources.Last()
                                            };
                                dbSpectra.Add(spectrum);
                                dbSource.Spectra.Add(spectrum);
                            }
                            else
                                spectrum = query.First();

                            //dbSources.Last().Spectra.Add(spectrum);
                            foreach (ResultInstance r in s.results.Values)
                                foreach (VariantInfo v in r.info.peptides)
                                {
                                    DataModel.Peptide peptide;
                                    if (!dbPeptides.TryGetValue(v.peptide.sequence, out peptide))
                                    {
                                        var pwizPeptide = new pwiz.CLI.proteome.Peptide(v.peptide.sequence);
                                        peptide = new DataModel.Peptide()
                                        {
                                            MonoisotopicMass = pwizPeptide.monoisotopicMass(),
                                            MolecularWeight = pwizPeptide.molecularWeight(),
                                            Instances = new List<DataModel.PeptideInstance>()
                                        };
                                        dbPeptides.Add(v.peptide.sequence, peptide);
                                    }

                                    foreach (ProteinInstanceInfo instance in v.peptide.proteins.Values)
                                    {
                                        string locus = instance.protein.locus;
                                        if (locus.Contains("IPI:IPI"))
                                            locus = locus.Split('|')[0];

                                        DataModel.Protein protein;
                                        if (!dbProteins.TryGetValue(locus, out protein))
                                        {
                                            protein = new DataModel.Protein()
                                                        {
                                                            Accession = locus,
                                                            Sequence = proteinList.protein(proteinList.find(locus)).sequence
                                                        };
                                            dbProteins.Add(protein.Accession, protein);
                                        }

                                        var query4 = from dbpi in dbPeptideInstances
                                                     where dbpi.Offset == instance.offset &&
                                                           dbpi.Length == v.peptide.sequence.Length &&
                                                           dbpi.Protein.Accession == protein.Accession
                                                     select dbpi;

                                        DataModel.PeptideInstance peptideInstance;
                                        if (query4.Count() == 0)
                                        {
                                            peptideInstance = new DataModel.PeptideInstance()
                                            {
                                                Peptide = peptide,
                                                Protein = protein,
                                                Offset = instance.offset,
                                                Length = v.peptide.sequence.Length,
                                                NTerminusIsSpecific = v.peptide.NTerminusIsSpecific,
                                                CTerminusIsSpecific = v.peptide.CTerminusIsSpecific,
                                                MissedCleavages = 0
                                            };
                                            dbPeptideInstances.Add(peptideInstance);
                                        }
                                        else
                                            peptideInstance = query4.First();
                                        peptide.Instances.Add(peptideInstance);
                                    }

                                    var pwizModifiedPeptide = new pwiz.CLI.proteome.Peptide(v.ToSimpleString(), ModificationParsing.ModificationParsing_Auto, ModificationDelimiter.ModificationDelimiter_Brackets);

                                    var psm = new DataModel.PeptideSpectrumMatch()
                                    {
                                        Rank = r.rank,
                                        QValue = r.FDR,
                                        MonoisotopicMass = pwizModifiedPeptide.monoisotopicMass(),
                                        MolecularWeight = pwizModifiedPeptide.molecularWeight(),
                                        Charge = s.id.charge,
                                        Spectrum = spectrum,
                                        Peptide = peptide
                                    };

                                    psm.MonoisotopicMassError = psm.MonoisotopicMass - s.precursorMass;
                                    psm.MolecularWeightError = psm.MolecularWeight - s.precursorMass;

                                    foreach (var position in v.mods)
                                    {

                                        foreach (var mod in position.Value)
                                        {
                                            var query5 = from dbm in dbModifications
                                                         where dbm.MonoMassDelta == mod.mass ||
                                                               dbm.AvgMassDelta == mod.mass
                                                         select dbm;

                                            DataModel.Modification modification;
                                            if (query5.Count() == 0)
                                            {
                                                modification = new DataModel.Modification()
                                                {
                                                    MonoMassDelta = mod.mass,
                                                    AvgMassDelta = mod.mass,
                                                    Name = mod.title
                                                };
                                                dbModifications.Add(modification);
                                            }
                                            else
                                                modification = query5.First();

                                            dbPeptideModifications.Add(new DataModel.PeptideModification
                                                                        {
                                                                            Modification = modification,
                                                                            PeptideSpectrumMatch = psm
                                                                        }
                                                                      );
                                            if (mod.position == 'n')
                                                dbPeptideModifications.Last().Offset = int.MinValue;
                                            else if (mod.position == 'c')
                                                dbPeptideModifications.Last().Offset = int.MaxValue;
                                            else
                                                dbPeptideModifications.Last().Offset = (int) mod.position;
                                        }
                                    }
                                    dbPSMs.Add(psm);
                                }
                        }
                    }
                }

                session.FlushMode = NHibernate.FlushMode.Commit;
                session.BeginTransaction();

                foreach (var itr in dbGroups) session.Save(itr);
                foreach (var itr in dbSources) session.Save(itr);
                foreach (var itr in dbSourceGroupLinks) session.Save(itr);
                foreach (var itr in dbPeptides.Values) session.Save(itr);
                foreach (var itr in dbProteins.Values) session.Save(itr);
                foreach (var itr in dbPeptideInstances) session.Save(itr);
                foreach (var itr in dbModifications) session.Save(itr);
                foreach (var itr in dbSpectra) session.Save(itr);
                foreach (var itr in dbPSMs) session.Save(itr);
                foreach (var itr in dbPeptideModifications) session.Save(itr);

                session.Transaction.Commit();
            }
        }

        public void addRandomData(IList<string> groups,
                                   int sourcesPerGroup,
                                   int spectraPerSource,
                                   string fastaFilepath,
                                   int identifiedProteinCount,
                                   double proteinMatchProbability,
                                   Map<double, int> chargeProbabilities)
        {
            Random rng = new Random(0);
            char[] aaTable = "ACDEFGHIKLMNPQRSTUVWY".ToCharArray();

            var pd = new ProteomeDataFile(fastaFilepath);
            var pl = pd.proteinList;
            var randomProteinIndexSet = new Dictionary<int,bool>();

            while(randomProteinIndexSet.Count < pl.size())
            {
                int randomIndex = rng.Next(0, pl.size());
                if (!randomProteinIndexSet.ContainsKey(randomIndex))
                    randomProteinIndexSet.Add(randomIndex, true);
            }

            var randomProteinIndexes = new List<int>(randomProteinIndexSet.Keys);

            var peptides = new Map<string, PeptideInfo>();

            var proteins = new ProteinList();
            for (int i = 0; i < identifiedProteinCount; ++i)
            {
                var protein = pl.protein(randomProteinIndexes[i]);
                proteins[protein.id] = new ProteinInfo()
                {
                    locus = protein.id,
                    description = protein.description,
                    isDecoy = rng.Next(0, 1) == 1,
                    length = protein.sequence.Length,
                };

                var digestion = new Digestion(protein, pwiz.CLI.cv.CVID.MS_Trypsin_P, new Digestion.Config(2, 6, 30, Digestion.Specificity.FullySpecific));
                foreach (var peptide in digestion)
                    if (rng.NextDouble() > proteinMatchProbability)
                    {
                        if (peptides.Contains(peptide.sequence))
                            peptides[peptide.sequence].proteins[protein.id] = new ProteinInstanceInfo() { protein = proteins[protein.id], offset = peptide.offset() };
                        else
                        {
                            peptides[peptide.sequence] = new PeptideInfo()
                            {
                                sequence = peptide.sequence,
                                NTerminusIsSpecific = peptide.NTerminusIsSpecific(),
                                CTerminusIsSpecific = peptide.CTerminusIsSpecific(),
                                proteins = new ProteinInstanceList()
                            };
                            peptides[peptide.sequence].proteins.Add(protein.id, new ProteinInstanceInfo() { protein = proteins[protein.id], offset = peptide.offset() });
                        }
                        proteins[protein.id].peptides.Add(new VariantInfo() { peptide = peptides[peptide.sequence] });
                    }
            }

            var results = new ResultList();
            foreach(PeptideInfo pep in peptides.Values)
            {
                var result = new ResultInfo();
                result.peptides.Add(new VariantInfo() { peptide = pep });
                results.Add(result);
            }
            var resultsFlatList = results.Keys;
            if (results.Count == 0)
                return;

            int numSources = 0;
            foreach (string group in groups)
            {
                SourceGroupInfo groupInfo = this.groups[group];
                groupInfo.name = group;

                for (int i = 0; i < sourcesPerGroup; ++i)
                {
                    SourceInfo sourceInfo = new SourceInfo();
                    sourceInfo.group = groupInfo;
                    sourceInfo.name = "source" + (numSources++);
                    groupInfo.sources.Add(sourceInfo);

                    for (int j = 0; j < spectraPerSource; ++j)
                    {
                        SpectrumInfo spectrumInfo = new SpectrumInfo();
                        spectrumInfo.id.id = spectrumInfo.id.index = j;
                        spectrumInfo.id.source = sourceInfo;
                        Map<double, int>.Enumerator itr = chargeProbabilities.LowerBound(rng.NextDouble());
                        spectrumInfo.id.charge = itr.IsValid ? itr.Current.Value : 1;

                        spectrumInfo.nativeID = "scan=" + (j + 1);

                        // for each rank
                        ResultInstance resultInstance = spectrumInfo.results[1] = new ResultInstance()
                        {
                            FDR = (float) rng.NextDouble(),
                            info = resultsFlatList[rng.Next(0, results.Count-1)],
                            rank = 1,
                            spectrum = spectrumInfo
                        };
                        foreach (var variant in resultInstance.info.peptides)
                        {
                            resultInstance.mods[variant.peptide].Add(new ModMap());
                            resultInstance.mods[variant.peptide].Last().Add('A', new List<ModInfo>());
                        }
                        resultInstance.info.spectra.Add(spectrumInfo.id, spectrumInfo);
                        sourceInfo.spectra[spectrumInfo.id] = spectrumInfo;
                    }
                }
            }

            this.proteins = proteins;
            this.results = results;
        }

        #region unimodXML file I/O

        // A table that maps a modification mass to a list of modifications that can
        // have this mass. The modifications are fully annotated using UniMod.
        public Map<float, List<UnimodModification>> modificationAnnotations;

        /// <summary>
        /// The unimod XML file doesnot contain all possible substitutions. This
        /// function checks for the missing substitutions and adds them back into
        /// the unimod file.
        /// </summary>
        public void fillMissingSubstitutions()
        {
            if( residueMaps == null )
            {
                residueMaps = new ResidueMaps();
            }
            // For each amino acid
            Map<char, float>.Enumerator fromAA = residueMaps.aminoAcidToMass.GetEnumerator();
            while( fromAA.MoveNext() )
            {
                //Get the next one
                Map<char, float>.Enumerator toAA = residueMaps.aminoAcidToMass.GetEnumerator();
                while( toAA.MoveNext() )
                {
                    // Make sure it's not the same as the previous
                    if( fromAA.Current.Key != toAA.Current.Key )
                    {
                        //Get the mass diff
                        float massDiff = -1.0f * ( fromAA.Current.Value - toAA.Current.Value );
                        // Make a title
                        string title = residueMaps.codeMapping[fromAA.Current.Key] + "->" + residueMaps.codeMapping[toAA.Current.Key];
                        // Check to see if the sub is already in the database
                        bool isInDB = false;
                        Map<float, List<UnimodModification>>.Enumerator lowerBound = modificationAnnotations.LowerBound( massDiff - 0.1f );
                        Map<float, List<UnimodModification>>.Enumerator upperBound = modificationAnnotations.UpperBound( massDiff + 0.1f );
                        // Go through each of the list one at a time
                        while( lowerBound.Current != upperBound.Current && !isInDB )
                        {
                            // Get the modification list
                            List<UnimodModification> list = lowerBound.Current.Value;
                            // For each modification

                            foreach( UnimodModification uniMod in list )
                            {
                                if( uniMod.title.CompareTo( title ) == 0 )
                                {
                                    isInDB = true;
                                    break;
                                }
                            }
                            lowerBound.MoveNext();
                        }
                        // If it's not in the db then add it
                        if( !isInDB )
                        {
                            UnimodModification newMod = new UnimodModification( title, title + " substitution" );
                            newMod.setModificationMasses( massDiff, massDiff );
                            newMod.addASpecificity( fromAA.Current.Key + "", "Any where", "AA substitution" );

                            List<UnimodModification> list = new List<UnimodModification>();
                            if( modificationAnnotations.Contains( newMod.monoIsotopicMass ) )
                            {
                                list = modificationAnnotations.Find( newMod.monoIsotopicMass ).Current.Value;
                                modificationAnnotations.Remove( newMod.monoIsotopicMass );
                            }
                            list.Add( newMod );
                            modificationAnnotations.Insert( newMod.monoIsotopicMass, list );
                            //Console.WriteLine( "Added:" + newMod.ToString() );
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Returns the unimod annotation for the modification object
        /// </summary>
        /// <param name="mod">A <see cref="IDPicker.ModInfo"/> ModInfo Object</param>
        /// <returns>A string containing the unimod modification</returns>
        public string getModificationAnnotation( ModInfo mod )
        {
            // Return null if there are no unimod annotations
            if( modificationAnnotations == null )
            {
                return null;
            }
            // Locate the candidate modications that can have the mass of this modification
            StringBuilder modificationAnnotation = new StringBuilder();
            Map<float, List<UnimodModification>>.Enumerator lowerBound = modificationAnnotations.LowerBound( mod.mass - 0.25f );
            Map<float, List<UnimodModification>>.Enumerator upperBound = modificationAnnotations.UpperBound( mod.mass + 0.25f );

            int modTitleCount = 0;
            string OR = " or ";
            bool annotationFound = false;

            String modResidue = new String( mod.residue, 1 );
            string modPosition = " at [" + Convert.ToInt32( mod.position ).ToString() + "]";

            // Go through each of the list one at a time
            while( lowerBound.Current != upperBound.Current )
            {
                // Get the modification list
                List<UnimodModification> list = lowerBound.Current.Value;
                // For each modification

                foreach( UnimodModification uniMod in list )
                {
                    // If this mod has the similar mass and same resiude then
                    // get the title of the mod
                    if( uniMod.isMatching( mod ) )
                    {
                        annotationFound = true;

                        if( mod.position == 'n' )
                        {
                            modResidue = "n-term";
                            modPosition = "";
                        } else if( mod.position == 'c' )
                        {
                            modResidue = "c-term";
                            modPosition = "";
                        }
                        ++modTitleCount;
                        if( modTitleCount > 1 )
                        {
                            modificationAnnotation.Append( OR );
                        }
                        modificationAnnotation.Append( uniMod.title + " of " + modResidue + modPosition + ";" );

                    }
                }
                lowerBound.MoveNext();
            }
            if( !annotationFound )
            {
                modificationAnnotation.Append( "Unknown modification " + mod.residue + "+" + ( (int) Math.Round( mod.mass ) ) + modPosition + ";" );
            }
            // Return the annotation
            return "(" + modificationAnnotation.ToString() + ")";
        }

        /// <summary>
        /// A print function that prints the unimod modification annotations that
        /// are read from a unimod XML file.
        /// </summary>
        public void printUnimodObjects()
        {
            Map<float, List<UnimodModification>>.Enumerator modMass = modificationAnnotations.GetEnumerator();
            while( modMass.MoveNext() )
            {
                List<UnimodModification>.Enumerator mods = modMass.Current.Value.GetEnumerator();
                while( mods.MoveNext() )
                {
                    Console.WriteLine( mods.Current.ToString() );
                }
            }
        }

        /// <summary>
        /// A print function that prints the unimod modification annotations that
        /// are read from a unimod XML file.
        /// </summary>
        public void printUnimodObjects( float mass )
        {
            Map<float, List<UnimodModification>>.Enumerator lowerBound = modificationAnnotations.LowerBound( mass - 0.25f );
            Map<float, List<UnimodModification>>.Enumerator upperBound = modificationAnnotations.UpperBound( mass + 0.25f );

            // Go through each of the list one at a time
            while( lowerBound.Current != upperBound.Current )
            {
                // Get the modification list
                List<UnimodModification> list = lowerBound.Current.Value;
                // For each modification
                foreach( UnimodModification uniMod in list )
                {
                    Console.WriteLine( uniMod.ToString() );
                }
                lowerBound.MoveNext();
            }
        }

        /// <summary>
        /// A function to read the unimod XML file and make a table that
        /// maps each modification mass to a list of modifications that can
        /// have the mass. The table also stores the annotations for each
        /// of the modifications.
        /// </summary>
        /// <param name="filename">An unimod XML file</param>
        public void readUniModXML( string filename )
        {
            // Initialize the modification object table
            modificationAnnotations = new Map<float, List<UnimodModification>>();

            //Initialize the readers
            StreamReader sourceXml = new StreamReader( filename );
            XmlTextReader reader = new XmlTextReader( sourceXml );

            string tag;
            long baseStreamLength = sourceXml.BaseStream.Length;

            //Object to hold the current modification to be read
            UnimodModification currentMod = null;

            //Read the data
            while( reader.Read() )
            {
                //Check for the node type
                switch( reader.NodeType )
                {
                    case XmlNodeType.Element:
                        tag = reader.Name;
                        // If we are at the start of a new mod.
                        if( tag == "umod:mod" )
                        {
                            // Get the title and name
                            string title = getAttributeAs<string>( reader, "title", true );
                            string fullName = getAttributeAs<string>( reader, "full_name", true );
                            // Create a new mod and remember it as the
                            // current mod being parsed.
                            currentMod = new UnimodModification( title, fullName );
                        } else if( tag == "umod:specificity" )
                        {
                            // Parse out the specificity parameters of the mod
                            // and add it the list of the specificities
                            string aminoAcid = getAttributeAs<string>( reader, "site", true );
                            string position = getAttributeAs<string>( reader, "position", true );
                            string classification = getAttributeAs<string>( reader, "classification", true );

                            if( currentMod != null )
                            {
                                currentMod.addASpecificity( aminoAcid, position, classification );
                            }
                        } else if( tag == "umod:delta" )
                        {
                            // Parse out the mass of the mod and update the
                            // mass parameters along with its composition
                            float monoMass = getAttributeAs<float>( reader, "mono_mass", true );
                            float avgMass = getAttributeAs<float>( reader, "avge_mass", true );
                            string comp = getAttributeAs<string>( reader, "composition", true );

                            if( currentMod != null )
                            {
                                currentMod.setModificationMasses( monoMass, avgMass );
                                currentMod.setComposition( comp );
                            }
                        }
                        break;
                    case XmlNodeType.EndElement:
                        // If we are at the tail of the modification element
                        // then add it to the list
                        tag = reader.Name;
                        if( tag == "umod:mod" && currentMod.getTotalNumberOfModSites() > 0 )
                        {
                            List<UnimodModification> list = new List<UnimodModification>();
                            if( modificationAnnotations.Contains( currentMod.monoIsotopicMass ) )
                            {
                                list = modificationAnnotations.Find( currentMod.monoIsotopicMass ).Current.Value;
                                modificationAnnotations.Remove( currentMod.monoIsotopicMass );
                            }
                            list.Add( currentMod );
                            modificationAnnotations.Insert( currentMod.monoIsotopicMass, list );
                            currentMod = null;
                        }
                        break;
                }
            }
            this.fillMissingSubstitutions();
        }
        #endregion
    }
}